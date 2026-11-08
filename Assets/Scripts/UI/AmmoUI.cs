using System.Collections.Generic;
using Platformer.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// One pip per round in the magazine, drawn above the warp gauge. During a reload the
    /// pips refill left to right, which doubles as the reload timer.
    /// </summary>
    public class AmmoUI : MonoBehaviour
    {
        /// <summary>
        /// Offset of the pip row from the bottom left corner of the screen.
        /// </summary>
        public Vector2 screenOffset = new Vector2(28f, 56f);
        public Vector2 pipSize = new Vector2(26f, 10f);
        public float pipGap = 4f;

        public Color loadedColor = new Color(1f, 0.78f, 0.25f, 0.95f);
        public Color spentColor = new Color(0.2f, 0.2f, 0.22f, 0.8f);
        /// <summary>
        /// Tint of the pips that have been refilled by the running reload.
        /// </summary>
        public Color reloadingColor = new Color(0.55f, 0.45f, 0.2f, 0.95f);

        PlayerController player;
        RectTransform row;
        readonly List<Image> pips = new List<Image>();

        public void Bind(PlayerController source)
        {
            player = source;
            if (row != null) BuildPips();
        }

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            Build();
        }

        void Update()
        {
            if (player == null || pips.Count == 0) return;

            if (player.IsReloading)
            {
                var refilled = Mathf.FloorToInt(player.ReloadProgress * pips.Count);
                for (var i = 0; i < pips.Count; i++)
                {
                    pips[i].color = i < refilled ? reloadingColor : spentColor;
                }
            }
            else
            {
                for (var i = 0; i < pips.Count; i++)
                {
                    pips[i].color = i < player.Ammo ? loadedColor : spentColor;
                }
            }
        }

        void Build()
        {
            var canvasObject = new GameObject("AmmoCanvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var rowObject = new GameObject("AmmoRow", typeof(RectTransform));
            rowObject.transform.SetParent(canvasObject.transform, false);
            row = rowObject.GetComponent<RectTransform>();
            row.anchorMin = row.anchorMax = row.pivot = Vector2.zero;
            row.anchoredPosition = screenOffset;
            row.sizeDelta = new Vector2(0f, pipSize.y);

            BuildPips();
        }

        void BuildPips()
        {
            foreach (var pip in pips) Destroy(pip.gameObject);
            pips.Clear();

            if (player == null) return;

            var count = Mathf.Max(1, player.magazineSize);
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Pip", typeof(RectTransform));
                go.transform.SetParent(row, false);

                var image = go.AddComponent<Image>();
                image.color = loadedColor;
                image.raycastTarget = false;

                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(i * (pipSize.x + pipGap), 0f);
                rect.sizeDelta = pipSize;

                pips.Add(image);
            }

            row.sizeDelta = new Vector2(count * (pipSize.x + pipGap) - pipGap, pipSize.y);
        }
    }
}
