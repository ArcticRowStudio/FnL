using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// Renders dynamic widescreen cinematic letterbox black bars (top and bottom)
    /// across the screen with smooth easing.
    /// Supports a global disable switch so letterbox can be cleanly and completely shut off.
    /// </summary>
    public class CinematicLetterboxUI : MonoBehaviour
    {
        public static bool IsGloballyDisabled = true;

        private static CinematicLetterboxUI _instance;
        public static CinematicLetterboxUI Instance
        {
            get
            {
                if (IsGloballyDisabled) return null;
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<CinematicLetterboxUI>();
                    if (_instance == null)
                    {
                        var go = new GameObject("CinematicLetterboxCanvas");
                        DontDestroyOnLoad(go);
                        _instance = go.AddComponent<CinematicLetterboxUI>();
                        _instance.InitializeUI();
                    }
                }
                return _instance;
            }
        }

        [Header("Settings")]
        [Range(0.05f, 0.25f)]
        public float letterboxScreenRatio = 0.12f;

        private RectTransform _topBar;
        private RectTransform _bottomBar;
        private Coroutine _animCoroutine;
        private float _currentProgress = 0f;

        private void Awake()
        {
            if (IsGloballyDisabled)
            {
                Destroy(gameObject);
                return;
            }

            if (_instance == null)
            {
                _instance = this;
                InitializeUI();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void InitializeUI()
        {
            if (IsGloballyDisabled) return;

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            // Top Bar
            if (_topBar == null)
            {
                var topObj = new GameObject("Letterbox_Top", typeof(RectTransform), typeof(Image));
                topObj.transform.SetParent(transform, false);
                _topBar = topObj.GetComponent<RectTransform>();
                _topBar.anchorMin = new Vector2(0f, 1f);
                _topBar.anchorMax = new Vector2(1f, 1f);
                _topBar.pivot = new Vector2(0.5f, 1f);
                _topBar.sizeDelta = new Vector2(0f, 0f);

                var img = topObj.GetComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;
            }

            // Bottom Bar
            if (_bottomBar == null)
            {
                var btmObj = new GameObject("Letterbox_Bottom", typeof(RectTransform), typeof(Image));
                btmObj.transform.SetParent(transform, false);
                _bottomBar = btmObj.GetComponent<RectTransform>();
                _bottomBar.anchorMin = new Vector2(0f, 0f);
                _bottomBar.anchorMax = new Vector2(1f, 0f);
                _bottomBar.pivot = new Vector2(0.5f, 0f);
                _bottomBar.sizeDelta = new Vector2(0f, 0f);

                var img = btmObj.GetComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;
            }
        }

        public static void Show(bool visible, float duration = 1.5f)
        {
            if (IsGloballyDisabled)
            {
                HideAllBarsImmediately();
                return;
            }

            if (Instance != null)
            {
                Instance.SetVisible(visible, duration);
            }
        }

        public static void HideAllBarsImmediately()
        {
            var uis = Object.FindObjectsByType<CinematicLetterboxUI>(FindObjectsInactive.Include);
            foreach (var ui in uis)
            {
                if (ui != null)
                {
                    if (ui._animCoroutine != null) ui.StopCoroutine(ui._animCoroutine);
                    ui._currentProgress = 0f;
                    ui.ApplyHeight();
                    if (ui._topBar != null) ui._topBar.gameObject.SetActive(false);
                    if (ui._bottomBar != null) ui._bottomBar.gameObject.SetActive(false);
                    if (Application.isPlaying)
                    {
                        Destroy(ui.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(ui.gameObject);
                    }
                }
            }
        }

        public void SetVisible(bool visible, float duration = 1.5f)
        {
            if (IsGloballyDisabled)
            {
                HideAllBarsImmediately();
                return;
            }

            if (_animCoroutine != null)
            {
                StopCoroutine(_animCoroutine);
            }
            _animCoroutine = StartCoroutine(AnimateBars(visible ? 1f : 0f, duration));
        }

        private IEnumerator AnimateBars(float targetProgress, float duration)
        {
            float start = _currentProgress;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / (duration > 0.001f ? duration : 0.001f));
                t = t * t * (3f - 2f * t);
                _currentProgress = Mathf.Lerp(start, targetProgress, t);
                ApplyHeight();
                yield return null;
            }

            _currentProgress = targetProgress;
            ApplyHeight();
            _animCoroutine = null;
        }

        private void ApplyHeight()
        {
            if (_topBar == null || _bottomBar == null) return;

            float maxBarHeight = 1080f * letterboxScreenRatio;
            float currentHeight = maxBarHeight * _currentProgress;

            _topBar.sizeDelta = new Vector2(0f, currentHeight);
            _bottomBar.sizeDelta = new Vector2(0f, currentHeight);
        }
    }
}
