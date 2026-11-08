using System;
using System.Collections.Generic;
using Platformer.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// Displays a dedicated boss health bar at the top-center of the screen.
    /// Features:
    /// - Boss title ("ARACHNE-MK4 // HEAVY QUADPOD")
    /// - Phase status subtitle ("PHASE 1: TACTICAL SIEGE" -> "PHASE 2: OVERCLOCKED ASSAULT")
    /// - Smooth lerping health gauge with damage trail
    /// - 50% Phase 2 threshold marker
    /// - Danger pulsing glow during Phase 2
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        [Header("Colors")]
        public Color phase1FillColor = new Color(1.0f, 0.45f, 0.1f, 0.95f);
        public Color phase2FillColor = new Color(1.0f, 0.15f, 0.15f, 0.98f);
        public Color damageTrailColor = new Color(1.0f, 0.85f, 0.4f, 0.85f);
        public Color backgroundColor = new Color(0.06f, 0.07f, 0.10f, 0.9f);
        public Color frameBorderColor = new Color(0.35f, 0.4f, 0.45f, 0.95f);

        private Health health;
        private Func<bool> isPhase2Query;
        private string titleLabel;
        private string phase1Label;
        private string phase2Label;

        private GameObject rootUI;
        private RectTransform barContainer;
        private RectTransform fillRect;
        private RectTransform trailRect;
        private Image fillImage;
        private Image trailImage;
        private Text titleText;
        private Text subtitleText;

        private float targetHealthRatio = 1f;
        private float trailHealthRatio = 1f;
        private bool isPhase2 = false;
        private RectTransform frameRect;
        private GameObject defaultMarker;
        private bool statusLocked;

        public void Bind(QuadpodBossAI boss, Health bossHealth)
        {
            Bind(bossHealth, () => boss != null && boss.isPhase2,
                "ARACHNE-MK4 // HEAVY QUADPOD", "PHASE 1: TACTICAL SIEGE", "PHASE 2: OVERCLOCKED ASSAULT [CRITICAL]");
        }

        /// <summary>
        /// Binds the bar to any boss: its Health, a query for whether it has entered phase 2, and the labels.
        /// </summary>
        public void Bind(Health bossHealth, Func<bool> isPhase2, string title, string phase1Subtitle, string phase2Subtitle)
        {
            isPhase2Query = isPhase2;
            titleLabel = title;
            phase1Label = phase1Subtitle;
            phase2Label = phase2Subtitle;
            health = bossHealth;

            if (health != null)
            {
                health.OnHealthChanged += HandleHealthChanged;
                targetHealthRatio = (float)health.CurrentHP / Mathf.Max(1, health.maxHP);
                trailHealthRatio = targetHealthRatio;
            }

            BuildUI();
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnHealthChanged -= HandleHealthChanged;
            }

            if (rootUI != null)
            {
                Destroy(rootUI);
            }
        }

        private void HandleHealthChanged(Health h)
        {
            if (h == null) return;
            targetHealthRatio = Mathf.Clamp01((float)h.CurrentHP / Mathf.Max(1, h.maxHP));
        }

        private void BuildUI()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("BossUICanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            rootUI = new GameObject("BossHealthBarRoot");
            rootUI.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = rootUI.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -25f);
            rootRect.sizeDelta = new Vector2(480f, 60f);

            // Title Text
            GameObject titleObj = new GameObject("BossTitle");
            titleObj.transform.SetParent(rootUI.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, 0f);
            titleRect.sizeDelta = new Vector2(480f, 20f);

            titleText = titleObj.AddComponent<Text>();
            titleText.text = titleLabel;
            titleText.fontSize = 15;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.95f, 0.95f, 1f, 0.95f);
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Subtitle Text
            GameObject subObj = new GameObject("BossSubtitle");
            subObj.transform.SetParent(rootUI.transform, false);
            RectTransform subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -18f);
            subRect.sizeDelta = new Vector2(480f, 14f);

            subtitleText = subObj.AddComponent<Text>();
            subtitleText.text = phase1Label;
            subtitleText.fontSize = 11;
            subtitleText.alignment = TextAnchor.MiddleCenter;
            subtitleText.color = new Color(0.8f, 0.85f, 0.9f, 0.8f);
            subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Outer Frame
            GameObject frameObj = new GameObject("BossBarFrame");
            frameObj.transform.SetParent(rootUI.transform, false);
            RectTransform frameRect = frameObj.AddComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 0f);
            frameRect.anchorMax = new Vector2(0.5f, 0f);
            frameRect.pivot = new Vector2(0.5f, 0f);
            frameRect.anchoredPosition = new Vector2(0f, 4f);
            frameRect.sizeDelta = new Vector2(440f, 18f);

            Image frameImg = frameObj.AddComponent<Image>();
            frameImg.color = frameBorderColor;

            // Background
            GameObject bgObj = new GameObject("BossBarBG");
            bgObj.transform.SetParent(frameObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(2f, 2f);
            bgRect.offsetMax = new Vector2(-2f, -2f);

            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = backgroundColor;

            barContainer = bgRect;

            // Damage Trail
            GameObject trailObj = new GameObject("DamageTrail");
            trailObj.transform.SetParent(barContainer, false);
            trailRect = trailObj.AddComponent<RectTransform>();
            trailRect.anchorMin = Vector2.zero;
            trailRect.anchorMax = new Vector2(1f, 1f);
            trailRect.offsetMin = Vector2.zero;
            trailRect.offsetMax = Vector2.zero;

            trailImage = trailObj.AddComponent<Image>();
            trailImage.color = damageTrailColor;

            // Fill Bar
            GameObject fillObj = new GameObject("HealthFill");
            fillObj.transform.SetParent(barContainer, false);
            fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            fillImage = fillObj.AddComponent<Image>();
            fillImage.color = phase1FillColor;

            // 50% Phase Marker Notch
            this.frameRect = frameRect;
            defaultMarker = AddMarker(0.5f);
        }

        private GameObject AddMarker(float ratio)
        {
            GameObject markerObj = new GameObject("PhaseMarker");
            markerObj.transform.SetParent(frameRect, false);
            RectTransform markerRect = markerObj.AddComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(ratio, 0f);
            markerRect.anchorMax = new Vector2(ratio, 1f);
            markerRect.sizeDelta = new Vector2(2f, 4f);

            Image markerImg = markerObj.AddComponent<Image>();
            markerImg.color = new Color(1f, 1f, 1f, 0.9f);
            return markerObj;
        }

        /// <summary>Replaces the 50% notch with one notch per phase boundary (health ratios).</summary>
        public void SetPhaseMarkers(params float[] ratios)
        {
            if (frameRect == null) return;
            if (defaultMarker != null) Destroy(defaultMarker);
            foreach (float r in ratios) AddMarker(Mathf.Clamp01(r));
        }

        /// <summary>Overrides the phase subtitle (e.g. a boss breaking off), and keeps it there.</summary>
        public void ShowStatus(string text)
        {
            statusLocked = true;
            if (subtitleText != null) subtitleText.text = text;
        }

        /// <summary>Takes the bar down while the boss is still alive.</summary>
        public void Dismiss(float delay)
        {
            if (rootUI != null) Destroy(rootUI, delay);
        }

        private void Update()
        {
            if (health == null || !health.IsAlive)
            {
                if (rootUI != null && targetHealthRatio <= 0f)
                {
                    if (fillRect != null) fillRect.anchorMax = new Vector2(0f, 1f);
                    // Smoothly fade out upon defeat
                    Destroy(rootUI, 1.5f);
                }
                return;
            }

            // Lerp damage trail
            if (trailHealthRatio > targetHealthRatio)
            {
                trailHealthRatio = Mathf.MoveTowards(trailHealthRatio, targetHealthRatio, Time.deltaTime * 0.45f);
            }
            else
            {
                trailHealthRatio = targetHealthRatio;
            }

            if (fillRect != null)
            {
                fillRect.anchorMax = new Vector2(targetHealthRatio, 1f);
            }
            if (trailRect != null)
            {
                trailRect.anchorMax = new Vector2(trailHealthRatio, 1f);
            }

            // Phase 2 transition check
            if (isPhase2Query != null && isPhase2Query() && !isPhase2)
            {
                isPhase2 = true;
                if (fillImage != null) fillImage.color = phase2FillColor;
                if (subtitleText != null && !statusLocked)
                {
                    subtitleText.text = phase2Label;
                    subtitleText.color = new Color(1f, 0.35f, 0.35f, 1f);
                }
            }

            // Phase 2 warning pulse
            if (isPhase2 && subtitleText != null)
            {
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 8f);
                subtitleText.color = new Color(1f, 0.3f * pulse, 0.3f * pulse, 1f);
            }
        }
    }
}
