using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Platformer.Cinematics
{
    /// <summary>
    /// Animates all ocean wave layers (distant horizon waves, rolling near swells,
    /// shimmering sunlight ripples, and ship wake foam) from Rightbgxbeam.psd.
    /// Creates an organic, multi-depth living ocean with zero distortion of the original art.
    /// </summary>
    [ExecuteAlways]
    public class OceanWaveEffect : MonoBehaviour
    {
        [Header("Horizon & Ocean Waves (Layer 3)")]
        [Tooltip("Distant horizon wave layer")]
        public SpriteRenderer waveOceanFar;
        [Tooltip("Foreground rolling wave layer")]
        public SpriteRenderer waveOceanNear;
        [Tooltip("Full single ocean wave layer (if not using Far/Near split)")]
        public SpriteRenderer waveOceanMain;

        [Header("Far Horizon Wave Motion")]
        public float farSpeed = 0.50f;
        public float farAmpX = 0.18f;
        public float farAmpY = 0.09f;

        [Header("Near Rolling Wave Motion")]
        public float nearSpeed = 0.80f;
        public float nearAmpX = 0.32f;
        public float nearAmpY = 0.18f;
        public float nearMinAlpha = 0.60f;
        public float nearMaxAlpha = 1.0f;

        [Header("Sunlight Shimmer Ripples (Layers 4 & 5)")]
        public SpriteRenderer waveLayerA;
        public SpriteRenderer waveLayerB;
        [Tooltip("Cycle frequency in Hz for ripple A and B crossfading")]
        public float shimmerSpeed = 0.60f;
        [Range(0.2f, 1.0f)]
        public float shimmerMaxAlpha = 1.0f;
        [Range(0.0f, 0.4f)]
        public float shimmerMinAlpha = 0.0f;
        public float shimmerAmpX = 0.22f;
        public float shimmerAmpY = 0.14f;

        [Header("Ship Foam & Wake (Layer 9)")]
        public SpriteRenderer waveLayerFoam;
        public float foamPulseSpeed = 1.6f;
        public float foamMinAlpha = 0.20f;
        public float foamMaxAlpha = 1.0f;
        public float foamAmpY = 0.12f;

        private Vector3 _origPosFar = Vector3.zero;
        private Vector3 _origPosNear = Vector3.zero;
        private Vector3 _origPosMain = Vector3.zero;
        private Vector3 _origPosA = Vector3.zero;
        private Vector3 _origPosB = Vector3.zero;
        private Vector3 _origPosFoam = Vector3.zero;
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
            // All wave children are centered at local (0, 0, 0)
            _origPosFar = Vector3.zero;
            _origPosNear = Vector3.zero;
            _origPosMain = Vector3.zero;
            _origPosA = Vector3.zero;
            _origPosB = Vector3.zero;
            _origPosFoam = Vector3.zero;
            _initialized = true;
        }

        public void RestoreOriginalPositions()
        {
            if (waveOceanFar != null) waveOceanFar.transform.localPosition = _origPosFar;
            if (waveOceanNear != null) waveOceanNear.transform.localPosition = _origPosNear;
            if (waveOceanMain != null) waveOceanMain.transform.localPosition = _origPosMain;
            if (waveLayerA != null) waveLayerA.transform.localPosition = _origPosA;
            if (waveLayerB != null) waveLayerB.transform.localPosition = _origPosB;
            if (waveLayerFoam != null) waveLayerFoam.transform.localPosition = _origPosFoam;
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (!Application.isPlaying && enabled && gameObject.activeInHierarchy)
            {
                AnimateWaves((float)EditorApplication.timeSinceStartup);
            }
        }
#endif

        private void Update()
        {
            if (Application.isPlaying)
            {
                AnimateWaves(Time.time);
            }
        }

        public void AnimateWaves(float t)
        {
            if (!_initialized) Initialize();

            // 1. Distant Horizon Waves (slow, subtle drift and bob)
            if (waveOceanFar != null)
            {
                float farX = Mathf.Sin(t * farSpeed) * farAmpX;
                float farY = Mathf.Cos(t * farSpeed * 1.25f) * farAmpY;
                waveOceanFar.transform.localPosition = _origPosFar + new Vector3(farX, farY, 0f);
            }

            // 2. Foreground Near Waves (gentle rolling swell and slight alpha breathing)
            if (waveOceanNear != null)
            {
                float nearX = Mathf.Sin(t * nearSpeed + 1.2f) * nearAmpX;
                float nearY = Mathf.Cos(t * nearSpeed * 1.35f + 0.8f) * nearAmpY;
                waveOceanNear.transform.localPosition = _origPosNear + new Vector3(nearX, nearY, 0f);

                float breath = (Mathf.Sin(t * nearSpeed * 0.8f) + 1f) * 0.5f;
                Color nc = waveOceanNear.color;
                nc.a = Mathf.Lerp(nearMinAlpha, nearMaxAlpha, breath);
                waveOceanNear.color = nc;
            }

            // 3. Optional Single Main Ocean Wave (if used instead of Far/Near split)
            if (waveOceanMain != null)
            {
                float mainX = Mathf.Sin(t * nearSpeed) * nearAmpX;
                float mainY = Mathf.Cos(t * nearSpeed * 1.2f) * nearAmpY;
                waveOceanMain.transform.localPosition = _origPosMain + new Vector3(mainX, mainY, 0f);
            }

            // 4. Sunlight Shimmer Crossfade (Ripples A and B)
            float shimmerPhase = Mathf.Sin(t * shimmerSpeed * Mathf.PI * 2f);
            float normA = (shimmerPhase + 1f) * 0.5f; // 0 to 1
            float normB = 1f - normA;                 // 1 to 0

            if (waveLayerA != null)
            {
                Color ca = waveLayerA.color;
                ca.a = Mathf.Lerp(shimmerMinAlpha, shimmerMaxAlpha, normA);
                waveLayerA.color = ca;

                float shX = Mathf.Sin(t * shimmerSpeed * 2.5f) * shimmerAmpX;
                float shY = Mathf.Cos(t * shimmerSpeed * 3.1f) * shimmerAmpY;
                waveLayerA.transform.localPosition = _origPosA + new Vector3(shX, shY, 0f);
            }

            if (waveLayerB != null)
            {
                Color cb = waveLayerB.color;
                cb.a = Mathf.Lerp(shimmerMinAlpha, shimmerMaxAlpha, normB);
                waveLayerB.color = cb;

                float shX = Mathf.Cos(t * shimmerSpeed * 2.2f + 1.5f) * (shimmerAmpX * 0.85f);
                float shY = Mathf.Sin(t * shimmerSpeed * 2.9f + 0.7f) * (shimmerAmpY * 0.85f);
                waveLayerB.transform.localPosition = _origPosB + new Vector3(shX, shY, 0f);
            }

            // 5. Ship Foam & Wake Pulse
            if (waveLayerFoam != null)
            {
                float foamPhase = (Mathf.Sin(t * foamPulseSpeed) + 1f) * 0.5f;
                Color fc = waveLayerFoam.color;
                fc.a = Mathf.Lerp(foamMinAlpha, foamMaxAlpha, foamPhase);
                waveLayerFoam.color = fc;

                float foamY = Mathf.Sin(t * (foamPulseSpeed * 0.75f)) * foamAmpY;
                waveLayerFoam.transform.localPosition = _origPosFoam + new Vector3(0f, foamY, 0f);
            }
        }
    }
}
