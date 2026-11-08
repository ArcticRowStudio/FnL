using System.Collections.Generic;
using Platformer.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// Draws the warp stamina gauge. The canvas is built at runtime so the gauge needs
    /// no scene setup - PlayerController creates this component if the scene has none.
    /// </summary>
    public class WarpStaminaUI : MonoBehaviour
    {
        /// <summary>
        /// Offset of the bar from the bottom left corner of the screen.
        /// </summary>
        public Vector2 screenOffset = new Vector2(28f, 28f);
        public Vector2 barSize = new Vector2(260f, 18f);

        public Color fillColor = new Color(0.35f, 0.8f, 1f, 0.95f);
        /// <summary>
        /// Tint used while the gauge can't afford a warp.
        /// </summary>
        public Color emptyColor = new Color(0.45f, 0.5f, 0.6f, 0.95f);
        /// <summary>
        /// Tint flashed when a warp is attempted without enough stamina.
        /// </summary>
        public Color flashColor = new Color(1f, 0.35f, 0.3f, 1f);
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);

        WarpStamina stamina;
        float segmentCost;

        RectTransform barRect;
        RectTransform fillRect;
        Image fill;
        readonly List<GameObject> ticks = new List<GameObject>();
        float flashTimer;

        /// <summary>
        /// Point the gauge at a stamina source. <paramref name="costPerUse"/> draws the
        /// tick marks, so the player can read how many warps are left at a glance.
        /// </summary>
        public void Bind(WarpStamina source, float costPerUse)
        {
            stamina = source;
            segmentCost = costPerUse;
            if (barRect != null) BuildSegmentTicks();
        }

        /// <summary>
        /// Flash the bar to signal a warp that couldn't be paid for.
        /// </summary>
        public void Flash()
        {
            flashTimer = 0.3f;
        }

        void Start()
        {
            if (stamina == null)
            {
                var player = FindAnyObjectByType<PlayerController>();
                if (player != null) stamina = player.GetComponent<WarpStamina>();
            }
            Build();
        }

        void Update()
        {
            if (fillRect == null) return;

            var normalized = stamina != null ? stamina.Normalized : 0f;
            fillRect.sizeDelta = new Vector2((barSize.x - 4f) * normalized, -4f);

            if (flashTimer > 0f)
            {
                flashTimer -= Time.unscaledDeltaTime;
                // Blink between the warning tint and the normal fill.
                fill.color = Mathf.Repeat(flashTimer, 0.12f) > 0.06f ? flashColor : fillColor;
            }
            else
            {
                var canWarp = stamina != null && segmentCost > 0f && stamina.Has(segmentCost);
                fill.color = canWarp ? fillColor : emptyColor;
            }
        }

        void Build()
        {
            var canvasObject = new GameObject("WarpStaminaCanvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Background.
            barRect = CreateImage(canvasObject.transform, "Background", backgroundColor).rectTransform;
            barRect.anchorMin = barRect.anchorMax = barRect.pivot = Vector2.zero;
            barRect.anchoredPosition = screenOffset;
            barRect.sizeDelta = barSize;

            // Fill, inset by 2px on every edge and resized every frame.
            fill = CreateImage(barRect, "Fill", fillColor);
            fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(2f, 0f);
            fillRect.sizeDelta = new Vector2(0f, -4f);

            BuildSegmentTicks();
        }

        /// <summary>
        /// Draws a divider for every warp the full gauge is worth.
        /// </summary>
        void BuildSegmentTicks()
        {
            foreach (var tick in ticks) Destroy(tick);
            ticks.Clear();

            if (stamina == null || segmentCost <= 0f || stamina.maxStamina <= 0f) return;

            var segments = Mathf.FloorToInt(stamina.maxStamina / segmentCost);
            for (var i = 1; i <= segments; i++)
            {
                var ratio = segmentCost * i / stamina.maxStamina;
                if (ratio >= 0.999f) break;

                var tick = CreateImage(barRect, "Tick", new Color(0f, 0f, 0f, 0.65f));
                var rect = tick.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(barSize.x * ratio, 0f);
                rect.sizeDelta = new Vector2(2f, -4f);
                ticks.Add(tick.gameObject);
            }
        }

        static Image CreateImage(Transform parent, string name, Color color)
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
