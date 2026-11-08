using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Platformer.Cinematics
{
    /// <summary>
    /// Animates background sky clouds with organic multi-harmonic wind drift,
    /// atmospheric heave, thermodynamic scale breathing, and subtle camera parallax.
    /// High clouds drift majestically with the upper winds across the sky,
    /// while low sunset clouds gently breathe and heave, maintaining solar alignment.
    /// </summary>
    [ExecuteAlways]
    public class SkyCloudEffect : MonoBehaviour
    {
        [Header("Cloud Renderers")]
        [Tooltip("High-altitude upper storm clouds")]
        public SpriteRenderer cloudsHigh;
        [Tooltip("Low-altitude warm sunset cumulus clouds")]
        public SpriteRenderer cloudsLow;
        [Tooltip("Full cloud layer (if not using High/Low split)")]
        public SpriteRenderer cloudsMain;

        [Header("High Cloud Wind Drift (Upper Sky)")]
        public float highSpeed = 0.12f;
        public float highAmpX = 1.10f;
        public float highAmpY = 0.08f;

        [Header("Low Cloud Atmosphere (Sunset Horizon)")]
        public float lowSpeed = 0.08f;
        public float lowAmpX = 0.04f;
        public float lowAmpY = 0.04f;

        [Header("Atmospheric Breathing")]
        public bool enableBreathing = true;
        public float breatheSpeed = 0.10f;
        public float breatheScaleAmount = 0.012f;
        public float minAlpha = 0.94f;
        public float maxAlpha = 1.0f;

        [Header("Camera Parallax")]
        public bool enableParallax = true;
        public Transform referenceCamera;
        public float highParallaxFactor = 0.025f;
        public float lowParallaxFactor = 0.005f;
        public float baseCameraX = 245.0f;

        private Vector3 _origPosHigh = Vector3.zero;
        private Vector3 _origPosLow = Vector3.zero;
        private Vector3 _origPosMain = Vector3.zero;
        private Vector3 _origScaleHigh = Vector3.one;
        private Vector3 _origScaleLow = Vector3.one;
        private Vector3 _origScaleMain = Vector3.one;
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
            RestoreOriginalPositions();
        }

        public void Initialize()
        {
            _origPosHigh = Vector3.zero;
            _origPosLow = Vector3.zero;
            _origPosMain = Vector3.zero;

            if (cloudsHigh != null) _origScaleHigh = Vector3.one;
            if (cloudsLow != null) _origScaleLow = Vector3.one;
            if (cloudsMain != null) _origScaleMain = Vector3.one;

            if (referenceCamera == null && Camera.main != null)
            {
                referenceCamera = Camera.main.transform;
            }

            _initialized = true;
        }

        public void RestoreOriginalPositions()
        {
            if (cloudsHigh != null)
            {
                cloudsHigh.transform.localPosition = _origPosHigh;
                cloudsHigh.transform.localScale = _origScaleHigh;
            }
            if (cloudsLow != null)
            {
                cloudsLow.transform.localPosition = _origPosLow;
                cloudsLow.transform.localScale = _origScaleLow;
            }
            if (cloudsMain != null)
            {
                cloudsMain.transform.localPosition = _origPosMain;
                cloudsMain.transform.localScale = _origScaleMain;
            }
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (!Application.isPlaying && enabled && gameObject.activeInHierarchy)
            {
                AnimateClouds((float)EditorApplication.timeSinceStartup);
            }
        }
#endif

        private void Update()
        {
            if (Application.isPlaying)
            {
                AnimateClouds(Time.time);
            }
        }

        public void AnimateClouds(float t)
        {
            if (!_initialized) Initialize();

            // Camera parallax along bridge
            float camDelta = 0f;
            if (enableParallax && referenceCamera != null)
            {
                camDelta = referenceCamera.position.x - baseCameraX;
            }

            // Breathing pulse
            float breatheScale = 1.0f;
            float breatheAlpha = 1.0f;
            if (enableBreathing)
            {
                float bPhase = (Mathf.Sin(t * breatheSpeed) + 1f) * 0.5f;
                breatheScale = 1.0f + (bPhase - 0.5f) * breatheScaleAmount;
                breatheAlpha = Mathf.Lerp(minAlpha, maxAlpha, bPhase);
            }

            // 1. High Altitude Clouds (wide drifting sweep across upper sky)
            if (cloudsHigh != null)
            {
                float hx = Mathf.Sin(t * highSpeed) * highAmpX 
                         + Mathf.Sin(t * highSpeed * 1.618f + 0.9f) * (highAmpX * 0.35f);
                float hy = Mathf.Cos(t * highSpeed * 0.75f) * highAmpY;
                float pHigh = camDelta * -highParallaxFactor;

                cloudsHigh.transform.localPosition = _origPosHigh + new Vector3(hx + pHigh, hy, 0f);
                cloudsHigh.transform.localScale = _origScaleHigh * breatheScale;

                Color hc = cloudsHigh.color;
                hc.a = breatheAlpha;
                cloudsHigh.color = hc;
            }

            // 2. Low Altitude Sunset Clouds (gentle heave, preserves solar alignment)
            if (cloudsLow != null)
            {
                float lx = Mathf.Sin(t * lowSpeed) * lowAmpX;
                float ly = Mathf.Cos(t * lowSpeed * 0.85f) * lowAmpY;
                float pLow = camDelta * -lowParallaxFactor;

                cloudsLow.transform.localPosition = _origPosLow + new Vector3(lx + pLow, ly, 0f);
                cloudsLow.transform.localScale = _origScaleLow * (1.0f + (1.0f - breatheScale) * 0.5f);

                Color lc = cloudsLow.color;
                lc.a = breatheAlpha;
                cloudsLow.color = lc;
            }

            // 3. Optional Single Main Clouds
            if (cloudsMain != null)
            {
                float mx = Mathf.Sin(t * highSpeed) * 0.6f;
                float my = Mathf.Cos(t * highSpeed * 0.8f) * 0.05f;

                cloudsMain.transform.localPosition = _origPosMain + new Vector3(mx, my, 0f);
                cloudsMain.transform.localScale = _origScaleMain * breatheScale;

                Color mc = cloudsMain.color;
                mc.a = breatheAlpha;
                cloudsMain.color = mc;
            }
        }
    }
}
