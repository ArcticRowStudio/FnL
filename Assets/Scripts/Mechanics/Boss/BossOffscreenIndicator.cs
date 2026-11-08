using UnityEngine;
using UnityEngine.UI;

namespace Platformer.Mechanics.Boss
{
    // An arrow pinned to the screen edge pointing at a boss that is out of shot, with how far off
    // it is, so a boss roaming a wide map is never simply lost. Hidden while the boss is on screen.
    // Added and bound by the boss at runtime; its own overlay canvas, so no scene setup.
    public class BossOffscreenIndicator : MonoBehaviour
    {
        const float EdgeMargin = 48f;       // reference pixels from the screen edge
        const float ArrowSize = 44f;

        Transform _target;
        Color _accent = new Color(0.25f, 0.85f, 1f, 1f);
        System.Func<bool> _threatening;
        GameObject _canvasObj;
        RectTransform _root;
        RectTransform _arrow;
        Image _arrowImage;
        Text _label;
        float _shown;

        static Sprite _arrowSprite;

        // threatening: while it returns true the arrow pulses red (the boss is about to attack).
        public void Bind(Transform target, Color accent, System.Func<bool> threatening = null)
        {
            _target = target;
            _accent = accent;
            _threatening = threatening;
            if (_canvasObj == null) Build();
        }

        void Build()
        {
            _canvasObj = new GameObject("BossOffscreenIndicator");
            var canvas = _canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = _canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _root = new GameObject("Marker").AddComponent<RectTransform>();
            _root.SetParent(_canvasObj.transform, false);
            _root.anchorMin = _root.anchorMax = Vector2.zero;
            _root.sizeDelta = new Vector2(ArrowSize, ArrowSize);

            _arrow = new GameObject("Arrow").AddComponent<RectTransform>();
            _arrow.SetParent(_root, false);
            _arrow.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            _arrowImage = _arrow.gameObject.AddComponent<Image>();
            _arrowImage.sprite = ArrowSprite;
            _arrowImage.raycastTarget = false;

            var labelRt = new GameObject("Distance").AddComponent<RectTransform>();
            labelRt.SetParent(_root, false);
            labelRt.sizeDelta = new Vector2(120f, 24f);
            _label = labelRt.gameObject.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 18;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.raycastTarget = false;
            var shadow = labelRt.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);

            _root.gameObject.SetActive(false);
        }

        // A chunky 16 px arrowhead pointing right, point-filtered to match the pixel art.
        static Sprite ArrowSprite
        {
            get
            {
                if (_arrowSprite != null) return _arrowSprite;
                const int n = 16;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        // Triangle from the left edge to a point at the right, with a dark rim.
                        float half = (n - 1 - x) * 0.5f;
                        float d = Mathf.Abs(y - (n - 1) * 0.5f);
                        bool inside = d <= half;
                        bool rim = inside && (d > half - 1.5f || x < 2);
                        px[y * n + x] = !inside ? new Color32(0, 0, 0, 0)
                            : rim ? new Color32(20, 20, 24, 255) : new Color32(255, 255, 255, 255);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _arrowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return _arrowSprite;
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (_root == null || cam == null || _target == null)
            {
                if (_root != null) _root.gameObject.SetActive(false);
                return;
            }

            Vector3 v = cam.WorldToViewportPoint(_target.position);
            bool onScreen = v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
            _shown = Mathf.MoveTowards(_shown, onScreen ? 0f : 1f, Time.unscaledDeltaTime * 6f);
            _root.gameObject.SetActive(_shown > 0.01f);
            if (_shown <= 0.01f) return;

            // From the screen centre toward the boss, clamped onto a rectangle inset from the edge.
            var canvasRect = (RectTransform)_canvasObj.transform;
            Vector2 size = canvasRect.rect.size;
            Vector2 centre = size * 0.5f;
            Vector2 dir = new Vector2(v.x - 0.5f, v.y - 0.5f);
            if (v.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            Vector2 half = centre - Vector2.one * EdgeMargin;
            float t = Mathf.Min(half.x / Mathf.Max(1e-4f, Mathf.Abs(dir.x * size.x)), half.y / Mathf.Max(1e-4f, Mathf.Abs(dir.y * size.y)));
            Vector2 pos = centre + new Vector2(dir.x * size.x, dir.y * size.y) * t;
            _root.anchoredPosition = pos;

            float angle = Mathf.Atan2(dir.y * size.y, dir.x * size.x) * Mathf.Rad2Deg;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            // The label sits on the inner side of the arrow, away from the edge.
            Vector2 inward = -new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            ((RectTransform)_label.transform).anchoredPosition = inward * 40f;

            float distance = Vector2.Distance(cam.transform.position, _target.position);
            _label.text = Mathf.RoundToInt(distance) + "m";

            bool threat = _threatening != null && _threatening();
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (threat ? 14f : 5f));
            Color c = threat ? Color.Lerp(new Color(1f, 0.25f, 0.2f), Color.white, pulse * 0.4f) : _accent;
            c.a = _shown * (0.75f + 0.25f * pulse);
            _arrowImage.color = c;
            _label.color = new Color(1f, 1f, 1f, _shown * 0.9f);
            _arrow.localScale = Vector3.one * (threat ? 1f + 0.18f * pulse : 1f);
        }

        void OnDestroy()
        {
            if (_canvasObj != null) Destroy(_canvasObj);
        }
    }
}
