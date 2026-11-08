using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Platformer.Cinematics
{
    /// <summary>
    /// Animates the physical light scattering, crystalline glint, and rim reflections
    /// on the glacier ice cliffs caused by the low-angle eastern sunset afterglow.
    /// Simulates:
    /// 1. Eastern rim grazing highlight on the vertical cliff face facing the afterglow.
    /// 2. Crystalline apex glint/shimmer at the highest ice ridge point.
    /// 3. Deep cyan/turquoise subsurface scattering (SSS) glowing within the ancient ice core.
    /// 4. Water surface specular glint reflecting the illuminated cliff in the bay.
    /// </summary>
    [ExecuteAlways]
    public class GlacierIceScatteringEffect : MonoBehaviour
    {
        [Header("Primary Glacier Scattering Renderers")]
        [Tooltip("Grazing rim highlight on the eastern vertical cliff face")]
        public SpriteRenderer rimHighlightRenderer;
        [Tooltip("Crystalline glint at the cliff apex catching direct grazing rays")]
        public SpriteRenderer apexGlintRenderer;
        [Tooltip("Deep cyan/aquamarine subsurface scattering within the ice core")]
        public SpriteRenderer subsurfaceGlowRenderer;
        [Tooltip("Water surface specular reflection glint below the cliff")]
        public SpriteRenderer waterGlintRenderer;

        [Header("Second Glacier Scattering Renderers")]
        [Tooltip("Grazing rim highlight on the eastern vertical cliff face of the second glacier")]
        public SpriteRenderer secondRimHighlightRenderer;
        [Tooltip("Crystalline glint at the second glacier apex/edge")]
        public SpriteRenderer secondApexGlintRenderer;
        [Tooltip("Deep cyan subsurface scattering within the second glacier ice core")]
        public SpriteRenderer secondSubsurfaceGlowRenderer;
        [Tooltip("Water surface specular reflection glint below the second glacier")]
        public SpriteRenderer secondWaterGlintRenderer;

        [Header("Optional 2D Point Light")]
        public Light2D rimLight2D;

        [Header("Atmospheric Light Reference (Auto-Fade Sync)")]
        [Tooltip("Optional reference light (e.g. Veranda_Left_Afterglow) to synchronize fade-in/out with entering the veranda")]
        public Light2D referenceAfterglowLight;
        public float maxReferenceIntensity = 1.75f;

        [Header("Shimmer & Pulsation Settings")]
        [Tooltip("Primary breathing speed of rim reflection")]
        public float rimBreathingSpeed = 0.75f;
        [Tooltip("Twinkle frequency of crystalline apex glint")]
        public float apexTwinkleSpeed = 2.2f;
        [Tooltip("Slow mystical breathing of deep cyan subsurface light")]
        public float subsurfacePulseSpeed = 0.55f;
        [Tooltip("Water glint ripple frequency")]
        public float waterGlintSpeed = 1.1f;

        [Header("Base Colors & Target Alphas")]
        [Tooltip("Grazing rim reflection color on the eastern cliff face")]
        public Color rimColor = new Color(1.0f, 0.88f, 0.65f, 0.85f);
        [Tooltip("Crystalline apex sparkle color")]
        public Color apexColor = new Color(1.0f, 0.95f, 0.82f, 0.90f);
        [Tooltip("Deep ancient ice cyan subsurface scattering color")]
        public Color sssColor = new Color(0.22f, 0.85f, 1.0f, 0.40f);
        [Tooltip("Water reflection glint color below the cliff")]
        public Color waterColor = new Color(0.95f, 0.82f, 0.58f, 0.50f);
        [Tooltip("Rim point light intensity")]
        public float baseLightIntensity = 1.6f;

        [Header("Primary Glacier Base Transforms & Scales")]
        public Vector3 baseRimScale = new Vector3(0.38f, 5.2f, 1.0f);
        public Vector3 baseApexScale = new Vector3(0.45f, 0.45f, 1.0f);
        public Vector3 baseSSSScale = new Vector3(5.5f, 3.2f, 1.0f);
        public Vector3 baseWaterScale = new Vector3(0.45f, 4.2f, 1.0f);

        [Header("Second Glacier Base Transforms & Scales")]
        public Vector3 baseSecondRimScale = new Vector3(0.32f, 1.8f, 1.0f);
        public Vector3 baseSecondApexScale = new Vector3(0.35f, 0.35f, 1.0f);
        public Vector3 baseSecondSSSScale = new Vector3(4.0f, 1.8f, 1.0f);
        public Vector3 baseSecondWaterScale = new Vector3(0.32f, 2.2f, 1.0f);

        [Header("Editor Preview")]
        [Tooltip("Always show full scattering in Editor Scene View without needing Play Mode or active trigger")]
        public bool previewInEditor = true;

        private void Awake()
        {
            ValidateScales();
        }

        private void OnEnable()
        {
            ValidateScales();
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
#endif
        }

        private void ValidateScales()
        {
            if (rimHighlightRenderer != null && baseRimScale == Vector3.zero)
                baseRimScale = rimHighlightRenderer.transform.localScale;
            if (apexGlintRenderer != null && baseApexScale == Vector3.zero)
                baseApexScale = apexGlintRenderer.transform.localScale;
            if (subsurfaceGlowRenderer != null && baseSSSScale == Vector3.zero)
                baseSSSScale = subsurfaceGlowRenderer.transform.localScale;
            if (waterGlintRenderer != null && baseWaterScale == Vector3.zero)
                baseWaterScale = waterGlintRenderer.transform.localScale;

            if (secondRimHighlightRenderer != null && baseSecondRimScale == Vector3.zero)
                baseSecondRimScale = secondRimHighlightRenderer.transform.localScale;
            if (secondApexGlintRenderer != null && baseSecondApexScale == Vector3.zero)
                baseSecondApexScale = secondApexGlintRenderer.transform.localScale;
            if (secondSubsurfaceGlowRenderer != null && baseSecondSSSScale == Vector3.zero)
                baseSecondSSSScale = secondSubsurfaceGlowRenderer.transform.localScale;
            if (secondWaterGlintRenderer != null && baseSecondWaterScale == Vector3.zero)
                baseSecondWaterScale = secondWaterGlintRenderer.transform.localScale;
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (this == null)
            {
                EditorApplication.update -= EditorUpdate;
                return;
            }
            if (!Application.isPlaying && enabled && gameObject != null && gameObject.activeInHierarchy)
            {
                AnimateScattering((float)EditorApplication.timeSinceStartup);
            }
        }
#endif

        private void Update()
        {
            if (Application.isPlaying)
            {
                AnimateScattering(Time.time);
            }
        }

        public void AnimateScattering(float t)
        {
            // Determine outdoor fade ratio [0.0 = dark indoors, 1.0 = outdoors under afterglow]
            float fadeRatio = 1.0f;
            if (Application.isPlaying && referenceAfterglowLight != null)
            {
                fadeRatio = Mathf.Clamp01(referenceAfterglowLight.intensity / Mathf.Max(0.01f, maxReferenceIntensity));
            }
            else if (!Application.isPlaying && !previewInEditor && referenceAfterglowLight != null)
            {
                fadeRatio = Mathf.Clamp01(referenceAfterglowLight.intensity / Mathf.Max(0.01f, maxReferenceIntensity));
            }

            // --- PRIMARY GLACIER ANIMATION ---

            // 1. Primary Eastern Rim Highlight Animation (grazing reflection breathing)
            if (rimHighlightRenderer != null)
            {
                float p1 = Mathf.Sin(t * rimBreathingSpeed);
                float p2 = Mathf.Sin(t * rimBreathingSpeed * 1.73f + 0.8f);
                float rimPhase = (p1 * 0.7f + p2 * 0.3f);

                Color c = rimColor;
                c.a = Mathf.Clamp01((rimColor.a + rimPhase * 0.12f) * fadeRatio);
                rimHighlightRenderer.color = c;

                float scaleMod = 1.0f + rimPhase * 0.05f;
                rimHighlightRenderer.transform.localScale = new Vector3(baseRimScale.x * scaleMod, baseRimScale.y, 1.0f);
            }

            // 2. Primary Apex Crystalline Glint (sharp diamond sparkle)
            if (apexGlintRenderer != null)
            {
                float twinkle = Mathf.Sin(t * apexTwinkleSpeed) * Mathf.Cos(t * apexTwinkleSpeed * 1.414f);
                float twinkleFactor = Mathf.Clamp01(0.7f + twinkle * 0.4f);

                Color c = apexColor;
                c.a = Mathf.Clamp01(apexColor.a * twinkleFactor * fadeRatio);
                apexGlintRenderer.color = c;

                float scaleMod = 0.85f + twinkleFactor * 0.25f;
                apexGlintRenderer.transform.localScale = new Vector3(baseApexScale.x * scaleMod, baseApexScale.y * scaleMod, 1.0f);
            }

            // 3. Primary Subsurface Cyan Internal Luminescence (deep ancient ice breathing)
            if (subsurfaceGlowRenderer != null)
            {
                float sssPhase = Mathf.Sin(t * subsurfacePulseSpeed + 1.5f);

                Color c = sssColor;
                c.a = Mathf.Clamp01((sssColor.a + sssPhase * 0.08f) * fadeRatio);
                subsurfaceGlowRenderer.color = c;

                float scaleMod = 1.0f + sssPhase * 0.04f;
                subsurfaceGlowRenderer.transform.localScale = new Vector3(baseSSSScale.x * scaleMod, baseSSSScale.y * scaleMod, 1.0f);
            }

            // 4. Primary Water Surface Glint (gentle rolling specular ripple)
            if (waterGlintRenderer != null)
            {
                float waterPhase = Mathf.Sin(t * waterGlintSpeed + 2.1f);

                Color c = waterColor;
                c.a = Mathf.Clamp01((waterColor.a + waterPhase * 0.10f) * fadeRatio);
                waterGlintRenderer.color = c;

                float scaleMod = 1.0f + waterPhase * 0.06f;
                waterGlintRenderer.transform.localScale = new Vector3(baseWaterScale.x * scaleMod, baseWaterScale.y, 1.0f);
            }

            // --- SECOND GLACIER ANIMATION ---

            // 5. Second Eastern Rim Highlight Animation (grazing reflection breathing with organic phase offset)
            if (secondRimHighlightRenderer != null)
            {
                float p1 = Mathf.Sin((t + 0.65f) * rimBreathingSpeed);
                float p2 = Mathf.Sin((t + 0.65f) * rimBreathingSpeed * 1.73f + 0.8f);
                float rimPhase = (p1 * 0.7f + p2 * 0.3f);

                Color c = rimColor;
                c.a = Mathf.Clamp01((rimColor.a + rimPhase * 0.12f) * fadeRatio);
                secondRimHighlightRenderer.color = c;

                float scaleMod = 1.0f + rimPhase * 0.05f;
                secondRimHighlightRenderer.transform.localScale = new Vector3(baseSecondRimScale.x * scaleMod, baseSecondRimScale.y, 1.0f);
            }

            // 6. Second Apex Crystalline Glint (crystalline sparkle on plateau corner)
            if (secondApexGlintRenderer != null)
            {
                float twinkle = Mathf.Sin((t + 1.25f) * apexTwinkleSpeed) * Mathf.Cos((t + 1.25f) * apexTwinkleSpeed * 1.414f);
                float twinkleFactor = Mathf.Clamp01(0.7f + twinkle * 0.4f);

                Color c = apexColor;
                c.a = Mathf.Clamp01(apexColor.a * twinkleFactor * fadeRatio);
                secondApexGlintRenderer.color = c;

                float scaleMod = 0.85f + twinkleFactor * 0.25f;
                secondApexGlintRenderer.transform.localScale = new Vector3(baseSecondApexScale.x * scaleMod, baseSecondApexScale.y * scaleMod, 1.0f);
            }

            // 7. Second Subsurface Cyan Luminescence (shelf glacier body glow)
            if (secondSubsurfaceGlowRenderer != null)
            {
                float sssPhase = Mathf.Sin((t + 0.85f) * subsurfacePulseSpeed + 1.5f);

                Color c = sssColor;
                c.a = Mathf.Clamp01((sssColor.a + sssPhase * 0.08f) * fadeRatio);
                secondSubsurfaceGlowRenderer.color = c;

                float scaleMod = 1.0f + sssPhase * 0.04f;
                secondSubsurfaceGlowRenderer.transform.localScale = new Vector3(baseSecondSSSScale.x * scaleMod, baseSecondSSSScale.y * scaleMod, 1.0f);
            }

            // 8. Second Water Surface Glint (water ripple below second cliff)
            if (secondWaterGlintRenderer != null)
            {
                float waterPhase = Mathf.Sin((t + 0.75f) * waterGlintSpeed + 2.1f);

                Color c = waterColor;
                c.a = Mathf.Clamp01((waterColor.a + waterPhase * 0.10f) * fadeRatio);
                secondWaterGlintRenderer.color = c;

                float scaleMod = 1.0f + waterPhase * 0.06f;
                secondWaterGlintRenderer.transform.localScale = new Vector3(baseSecondWaterScale.x * scaleMod, baseSecondWaterScale.y, 1.0f);
            }

            // --- 2D POINT LIGHT ---
            if (rimLight2D != null)
            {
                float lightPhase = Mathf.Sin(t * rimBreathingSpeed);
                rimLight2D.intensity = (baseLightIntensity + lightPhase * 0.2f) * fadeRatio;
            }
        }
    }
}
