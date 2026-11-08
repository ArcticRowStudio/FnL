using System.Collections.Generic;
using Platformer.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// Displays the player's health gauge in the top-left corner of the screen.
    /// Features a retro HUD aesthetic, segmented HP blocks, damage trail lerping,
    /// and a damage flash animation when hit.
    /// </summary>
    public class PlayerHealthUI : MonoBehaviour
    {
        [Header("Positioning and Sizing")]
        public Vector2 screenOffset = new Vector2(36f, -36f);
        public Vector2 barSize = new Vector2(260f, 22f);
        public Vector2 badgeSize = new Vector2(32f, 22f);

        [Header("Colors")]
        public Color fillColor = new Color(0.92f, 0.20f, 0.24f, 0.98f);
        public Color damageTrailColor = new Color(1.0f, 0.80f, 0.35f, 0.95f);
        public Color backgroundColor = new Color(0.08f, 0.08f, 0.11f, 0.85f);
        public Color frameBorderColor = new Color(0.18f, 0.19f, 0.22f, 0.95f);
        public Color flashColor = new Color(1f, 1f, 1f, 1f);
        public Color crossIconColor = new Color(0.95f, 0.25f, 0.28f, 1f);

        [Header("Animation Settings")]
        public float trailLerpSpeed = 3.5f;

        private Health health;
        private RectTransform barContainer;
        private RectTransform fillRect;
        private RectTransform trailRect;
        private Image fillImage;
        private Image trailImage;
        private readonly List<GameObject> segmentTicks = new List<GameObject>();

        private float displayedHealthRatio = 1f;
        private float trailHealthRatio = 1f;
        private float flashTimer = 0f;

        /// <summary>
        /// Bind the UI to a specific Health component.
        /// </summary>
        public void Bind(Health source)
        {
            if (health != null)
            {
                health.OnHealthChanged -= OnHealthChanged;
            }

            health = source;

            if (health != null)
            {
                health.OnHealthChanged += OnHealthChanged;
                displayedHealthRatio = health.maxHP > 0 ? (float)health.CurrentHP / health.maxHP : 1f;
                trailHealthRatio = displayedHealthRatio;
            }

            if (barContainer != null)
            {
                BuildSegmentTicks();
            }
        }

        private void Start()
        {
            if (health == null)
            {
                var player = FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    Bind(player.health);
                }
            }
            Build();
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnHealthChanged -= OnHealthChanged;
            }
        }

        private void OnHealthChanged(Health h)
        {
            if (h.maxHP <= 0) return;
            float newRatio = Mathf.Clamp01((float)h.CurrentHP / h.maxHP);

            if (newRatio < displayedHealthRatio)
            {
                flashTimer = 0.25f;
            }
            else if (newRatio > displayedHealthRatio)
            {
                trailHealthRatio = newRatio;
            }

            displayedHealthRatio = newRatio;
            BuildSegmentTicks();
        }

        public void Flash()
        {
            flashTimer = 0.25f;
        }

        private void Update()
        {
            if (fillRect == null || trailRect == null) return;

            if (trailHealthRatio > displayedHealthRatio)
            {
                trailHealthRatio = Mathf.MoveTowards(trailHealthRatio, displayedHealthRatio, trailLerpSpeed * Time.deltaTime);
            }
            else
            {
                trailHealthRatio = displayedHealthRatio;
            }

            float usableWidth = barSize.x - 4f;
            fillRect.sizeDelta = new Vector2(usableWidth * displayedHealthRatio, -4f);
            trailRect.sizeDelta = new Vector2(usableWidth * trailHealthRatio, -4f);

            if (flashTimer > 0f)
            {
                flashTimer -= Time.unscaledDeltaTime;
                fillImage.color = Mathf.Repeat(flashTimer, 0.1f) > 0.05f ? flashColor : fillColor;
            }
            else
            {
                fillImage.color = fillColor;
            }
        }

        private void Build()
        {
            var canvasObject = new GameObject("PlayerHealthCanvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var hudRoot = new GameObject("PlayerHealthHUD", typeof(RectTransform));
            hudRoot.transform.SetParent(canvasObject.transform, false);
            var rootRect = hudRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = screenOffset;
            rootRect.sizeDelta = new Vector2(badgeSize.x + 4f + barSize.x, barSize.y);

            // 1. HP Badge / Icon Box on Left
            var badgeObj = CreateImage(hudRoot.transform, "HPBadge", frameBorderColor);
            var badgeRect = badgeObj.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.anchoredPosition = Vector2.zero;
            badgeRect.sizeDelta = badgeSize;

            var badgeInner = CreateImage(badgeRect, "Inner", backgroundColor);
            var badgeInnerRect = badgeInner.rectTransform;
            badgeInnerRect.anchorMin = Vector2.zero;
            badgeInnerRect.anchorMax = Vector2.one;
            badgeInnerRect.sizeDelta = new Vector2(-4f, -4f);
            badgeInnerRect.anchoredPosition = Vector2.zero;

            // Medical cross icon '+'
            var crossH = CreateImage(badgeInnerRect, "CrossH", crossIconColor);
            crossH.rectTransform.anchorMin = crossH.rectTransform.anchorMax = crossH.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            crossH.rectTransform.sizeDelta = new Vector2(12f, 4f);

            var crossV = CreateImage(badgeInnerRect, "CrossV", crossIconColor);
            crossV.rectTransform.anchorMin = crossV.rectTransform.anchorMax = crossV.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            crossV.rectTransform.sizeDelta = new Vector2(4f, 12f);

            // 2. Health Bar Outer Frame
            var frameObj = CreateImage(hudRoot.transform, "BarFrame", frameBorderColor);
            barContainer = frameObj.rectTransform;
            barContainer.anchorMin = barContainer.anchorMax = barContainer.pivot = new Vector2(0f, 1f);
            barContainer.anchoredPosition = new Vector2(badgeSize.x + 4f, 0f);
            barContainer.sizeDelta = barSize;

            var bgObj = CreateImage(barContainer, "Background", backgroundColor);
            var bgRect = bgObj.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = new Vector2(-4f, -4f);
            bgRect.anchoredPosition = Vector2.zero;

            trailImage = CreateImage(bgRect, "DamageTrail", damageTrailColor);
            trailRect = trailImage.rectTransform;
            trailRect.anchorMin = new Vector2(0f, 0f);
            trailRect.anchorMax = new Vector2(0f, 1f);
            trailRect.pivot = new Vector2(0f, 0.5f);
            trailRect.anchoredPosition = Vector2.zero;
            trailRect.sizeDelta = new Vector2(barSize.x - 4f, 0f);

            fillImage = CreateImage(bgRect, "HealthFill", fillColor);
            fillRect = fillImage.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(barSize.x - 4f, 0f);

            var sheen = CreateImage(fillRect, "Sheen", new Color(1f, 1f, 1f, 0.28f));
            sheen.rectTransform.anchorMin = new Vector2(0f, 1f);
            sheen.rectTransform.anchorMax = new Vector2(1f, 1f);
            sheen.rectTransform.pivot = new Vector2(0.5f, 1f);
            sheen.rectTransform.anchoredPosition = Vector2.zero;
            sheen.rectTransform.sizeDelta = new Vector2(0f, 2f);

            BuildSegmentTicks();
        }

        private void BuildSegmentTicks()
        {
            foreach (var t in segmentTicks)
            {
                if (t != null) Destroy(t);
            }
            segmentTicks.Clear();

            if (barContainer == null || health == null || health.maxHP <= 1) return;

            int maxHP = health.maxHP;
            float usableWidth = barSize.x - 4f;

            for (int i = 1; i < maxHP; i++)
            {
                float ratio = (float)i / maxHP;
                var tick = CreateImage(barContainer, $"Tick_{i}", new Color(0.08f, 0.08f, 0.11f, 0.85f));
                var r = tick.rectTransform;
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(2f + usableWidth * ratio, 0f);
                r.sizeDelta = new Vector2(2f, -4f);
                segmentTicks.Add(tick.gameObject);
            }
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
