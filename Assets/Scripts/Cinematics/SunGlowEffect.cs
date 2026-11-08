using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Platformer.Cinematics
{
    /// <summary>
    /// Animates the sun light source and corona glow in the veranda ocean vista.
    /// Creates a natural, atmospheric solar breathing effect with subtle
    /// luminance pulsation and warm corona expansion.
    /// </summary>
    [ExecuteAlways]
    public class SunGlowEffect : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("Point light source illuminating the scene")]
        public Light2D sunLight;
        [Tooltip("Visual corona / halo sprite renderer")]
        public SpriteRenderer coronaRenderer;

        [Header("Light Intensity Pulsation")]
        [Tooltip("Base light intensity")]
        public float baseIntensity = 2.0f;
        [Tooltip("Pulsation amplitude for light intensity")]
        public float intensityPulseAmp = 0.25f;
        [Tooltip("Pulsation speed in rad/s")]
        public float pulseSpeed = 0.85f;

        [Header("Corona Glow Scale & Alpha Breathing")]
        [Tooltip("Base scale of the corona sprite")]
        public float baseScale = 1.15f;
        [Tooltip("Scale breathing amplitude")]
        public float scalePulseAmp = 0.06f;
        [Tooltip("Base alpha of the corona sprite")]
        [Range(0.1f, 1.0f)]
        public float baseAlpha = 0.90f;
        [Tooltip("Alpha breathing amplitude")]
        [Range(0.0f, 0.4f)]
        public float alphaPulseAmp = 0.10f;

        private Vector3 _originalScale = Vector3.one;
        private Color _originalCoronaColor = Color.white;
        private float _originalLightIntensity = 2.0f;
        private bool _initialized = false;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif
            RestoreOriginal();
        }

        public void Initialize()
        {
            if (coronaRenderer != null)
            {
                _originalScale = coronaRenderer.transform.localScale;
                _originalCoronaColor = coronaRenderer.color;
            }
            if (sunLight != null)
            {
                _originalLightIntensity = sunLight.intensity;
            }
            _initialized = true;
        }

        public void RestoreOriginal()
        {
            if (coronaRenderer != null)
            {
                coronaRenderer.transform.localScale = _originalScale;
                coronaRenderer.color = _originalCoronaColor;
            }
            if (sunLight != null)
            {
                sunLight.intensity = _originalLightIntensity;
            }
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (!Application.isPlaying && enabled && gameObject.activeInHierarchy)
            {
                AnimateSun((float)EditorApplication.timeSinceStartup);
            }
        }
#endif

        private void Update()
        {
            if (Application.isPlaying)
            {
                AnimateSun(Time.time);
            }
        }

        public void AnimateSun(float t)
        {
            if (!_initialized) Initialize();

            // Organic multi-harmonic breathing
            float phase1 = Mathf.Sin(t * pulseSpeed);
            float phase2 = Mathf.Sin(t * pulseSpeed * 1.618f + 1.2f);
            float combinedPhase = (phase1 * 0.7f + phase2 * 0.3f);

            // 1. Animate 2D Light intensity
            if (sunLight != null)
            {
                sunLight.intensity = baseIntensity + combinedPhase * intensityPulseAmp;
            }

            // 2. Animate Corona sprite scale and alpha
            if (coronaRenderer != null)
            {
                float currentScale = baseScale + combinedPhase * scalePulseAmp;
                coronaRenderer.transform.localScale = new Vector3(currentScale, currentScale, 1.0f);

                Color c = _originalCoronaColor;
                c.a = Mathf.Clamp01(baseAlpha + combinedPhase * alphaPulseAmp);
                coronaRenderer.color = c;
            }
        }
    }
}
