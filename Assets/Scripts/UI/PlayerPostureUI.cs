using Platformer.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// Renders the Sekiro-style Posture (체간) gauge for the player.
    /// Positioned right above the Warp Stamina bar.
    /// Fills up from center/left with fiery orange/red, flashing when close to breaking.
    /// </summary>
    public class PlayerPostureUI : MonoBehaviour
    {
        public Vector2 screenOffset = new Vector2(28f, 52f);
        public Vector2 barSize = new Vector2(260f, 14f);

        public Color safeColor = new Color(1f, 0.65f, 0.15f, 0.95f);      // Vibrant fiery orange
        public Color warningColor = new Color(1f, 0.25f, 0.1f, 1f);       // Critical glowing red
        public Color brokenColor = new Color(0.85f, 0.1f, 0.1f, 1f);      // Glass-broken deep crimson
        public Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.65f);

        private PlayerPosture posture;
        private RectTransform barRect;
        private RectTransform fillRect;
        private Image fill;
        private CanvasGroup canvasGroup;
        private float flashTimer;

        public void Bind(PlayerPosture source)
        {
            if (posture != null)
            {
                posture.OnPostureChanged -= HandlePostureChanged;
                posture.OnGuardBreak -= HandleGuardBreak;
            }

            posture = source;
            if (posture != null)
            {
                posture.OnPostureChanged += HandlePostureChanged;
                posture.OnGuardBreak += HandleGuardBreak;
            }
        }

        void OnDestroy()
        {
            if (posture != null)
            {
                posture.OnPostureChanged -= HandlePostureChanged;
                posture.OnGuardBreak -= HandleGuardBreak;
            }
        }

        void Start()
        {
            EnsureCanvas();
        }

        private void EnsureCanvas()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("PlayerPostureCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 95;

                var scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                transform.SetParent(canvasObj.transform, false);
            }

            canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Background Frame
            var bgObj = new GameObject("Posture_Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(transform, false);
            barRect = bgObj.GetComponent<RectTransform>();
            barRect.anchorMin = Vector2.zero;
            barRect.anchorMax = Vector2.zero;
            barRect.pivot = Vector2.zero;
            barRect.anchoredPosition = screenOffset;
            barRect.sizeDelta = barSize;

            var bgImage = bgObj.GetComponent<Image>();
            bgImage.color = backgroundColor;

            // Fill Bar
            var fillObj = new GameObject("Posture_Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(bgObj.transform, false);
            fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.zero;
            fillRect.pivot = Vector2.zero;
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, barSize.y);

            fill = fillObj.GetComponent<Image>();
            fill.color = safeColor;
        }

        private void HandlePostureChanged(float current, float max)
        {
            if (fillRect == null || posture == null) return;

            float norm = posture.Normalized;
            fillRect.sizeDelta = new Vector2(barSize.x * norm, barSize.y);

            if (posture.isBroken)
            {
                fill.color = brokenColor;
            }
            else if (norm > 0.7f)
            {
                // Warning pulse between orange and crimson
                fill.color = Color.Lerp(safeColor, warningColor, Mathf.PingPong(Time.time * 6f, 1f));
            }
            else
            {
                fill.color = safeColor;
            }
        }

        private void HandleGuardBreak()
        {
            flashTimer = 0.5f;
        }

        void Update()
        {
            if (posture == null) return;

            // Auto fade out when posture is empty to keep screen clean
            if (canvasGroup != null)
            {
                float targetAlpha = posture.currentPosture > 0.05f || posture.isBroken ? 1f : 0.25f;
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * 3f);
            }

            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (fill != null) fill.color = Color.white;
            }
            else
            {
                HandlePostureChanged(posture.currentPosture, posture.maxPosture);
            }
        }
    }
}
