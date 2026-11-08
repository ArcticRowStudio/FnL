using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Platformer.Cinematics
{
    /// <summary>
    /// Animates a gentle, distant hot-air balloon drifting peacefully across the arctic sunset sky.
    /// Simulates horizontal wind drift, buoyant vertical bobbing, thermal air currents,
    /// natural pendulum tilt, and soft burner flame pulsing.
    /// </summary>
    [ExecuteAlways]
    public class HotAirBalloonFloat : MonoBehaviour
    {
        public enum DriftMode
        {
            [Tooltip("Drifts across the sky and wraps seamlessly from one side to the other with soft fading")]
            LoopAcrossSky,
            [Tooltip("Gently drifts back and forth around the base position with shifting thermal currents")]
            PingPong,
            [Tooltip("Continuous endless drift in wind direction without wrapping")]
            ContinuousDrift
        }

        [Header("Wind Drift Movement")]
        [Tooltip("Whether the balloon slowly drifts across the sky along the wind")]
        public bool enableWindDrift = true;

        [Tooltip("Movement pattern for wind drift")]
        public DriftMode driftMode = DriftMode.LoopAcrossSky;

        [Tooltip("Horizontal drift speed in units per second. Negative = West/Glacier, Positive = East/Base")]
        public float windDriftSpeed = -0.3f;

        [Tooltip("Sky bounds for Loop mode: left boundary (over distant glacier)")]
        public float minSkyX = -65f;

        [Tooltip("Sky bounds for Loop mode: right boundary (near base/horizon)")]
        public float maxSkyX = -15f;

        [Tooltip("Fade margin distance (units) near the sky bounds to smoothly dissolve in/out")]
        public float boundFadeMargin = 3.5f;

        [Tooltip("Half-distance to drift back and forth in PingPong mode")]
        public float pingPongDistance = 10f;

        [Header("Buoyancy & Thermal Currents")]
        [Tooltip("Base position in world space")]
        public Vector3 basePosition;

        [Tooltip("Vertical floating bobbing speed")]
        public float floatSpeed = 0.6f;

        [Tooltip("Vertical floating bobbing height amplitude")]
        public float floatAmplitude = 0.25f;

        [Tooltip("Gentle altitude variations caused by rising warm thermal currents")]
        public float thermalLiftAmplitude = 0.45f;

        [Tooltip("Speed of thermal updraft/downdraft cycle")]
        public float thermalLiftSpeed = 0.18f;

        [Tooltip("Horizontal micro-flutter sway amplitude")]
        public float swayAmplitude = 0.25f;

        [Tooltip("Horizontal micro-flutter sway speed")]
        public float swaySpeed = 0.4f;

        [Tooltip("Subtle pendulum tilt angle in degrees")]
        public float tiltAngle = 1.0f;

        [Tooltip("Tilt oscillation speed")]
        public float tiltSpeed = 0.45f;

        [Header("Burner Glow (Optional)")]
        [Tooltip("Child 2D light simulating the warm flame inside the balloon burner")]
        public Light2D burnerLight;

        [Tooltip("Burner flame flicker speed")]
        public float burnerPulseSpeed = 2.0f;

        [Tooltip("Burner light intensity range")]
        public float burnerMinIntensity = 0.6f;
        public float burnerMaxIntensity = 1.2f;

        [Header("Editor Preview")]
        [Tooltip("Toggle to preview floating animation in edit mode. Keep OFF to freely drag the balloon in Scene view or enter coordinates in Inspector!")]
        public bool previewInEditor = false;

        private SpriteRenderer _spriteRenderer;
        private bool _lastPreviewState = false;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (basePosition == Vector3.zero)
            {
                basePosition = transform.position;
            }
            if (burnerLight == null)
            {
                burnerLight = GetComponentInChildren<Light2D>();
            }
        }

        private void OnEnable()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (basePosition == Vector3.zero)
            {
                basePosition = transform.position;
            }
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
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && !previewInEditor && basePosition != Vector3.zero)
            {
                transform.position = basePosition;
                transform.rotation = Quaternion.identity;
                if (_spriteRenderer != null)
                {
                    Color c = _spriteRenderer.color;
                    c.a = 1f;
                    _spriteRenderer.color = c;
                }
            }
        }

        [ContextMenu("Capture Current Position As Base")]
        public void CaptureCurrentPosition()
        {
            basePosition = transform.position;
        }

        [ContextMenu("Reset To Base Position")]
        public void ResetPosition()
        {
            transform.position = basePosition;
            transform.rotation = Quaternion.identity;
            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.a = 1f;
                _spriteRenderer.color = c;
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                AnimateFloating(Time.time);
            }
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
                if (previewInEditor)
                {
                    _lastPreviewState = true;
                    AnimateFloating((float)EditorApplication.timeSinceStartup);
                }
                else
                {
                    if (_lastPreviewState)
                    {
                        _lastPreviewState = false;
                        ResetPosition();
                    }

                    // When preview is off, user can freely drag in Scene view or enter coordinates in Inspector!
                    if (transform.hasChanged)
                    {
                        basePosition = transform.position;
                        transform.hasChanged = false;
                    }
                }
            }
        }
#endif

        private void AnimateFloating(float time)
        {
            // 1. Horizontal position with wind drift
            float currentX = basePosition.x;
            float currentAlpha = 1.0f;

            if (enableWindDrift)
            {
                switch (driftMode)
                {
                    case DriftMode.LoopAcrossSky:
                        float width = Mathf.Max(1f, maxSkyX - minSkyX);
                        float normOffset = (basePosition.x - minSkyX) + windDriftSpeed * time;
                        currentX = Mathf.Repeat(normOffset, width) + minSkyX;

                        // Soft edge fading near sky bounds
                        if (boundFadeMargin > 0.05f)
                        {
                            float distMin = currentX - minSkyX;
                            float distMax = maxSkyX - currentX;
                            currentAlpha = Mathf.Clamp01(Mathf.Min(distMin, distMax) / boundFadeMargin);
                        }
                        break;

                    case DriftMode.PingPong:
                        float halfDist = Mathf.Max(1f, pingPongDistance);
                        float ppCycle = Mathf.Sin(time * (Mathf.Abs(windDriftSpeed) / halfDist));
                        currentX = basePosition.x + ppCycle * halfDist;
                        break;

                    case DriftMode.ContinuousDrift:
                        currentX = basePosition.x + windDriftSpeed * time;
                        break;
                }
            }

            // Micro sway
            float microSway = Mathf.Cos(time * swaySpeed) * swayAmplitude;
            currentX += microSway;

            // 2. Vertical position with bobbing and thermal lift
            float bobbing = Mathf.Sin(time * floatSpeed) * floatAmplitude;
            float thermal = Mathf.Sin(time * thermalLiftSpeed) * thermalLiftAmplitude;
            float currentY = basePosition.y + bobbing + thermal;

            transform.position = new Vector3(currentX, currentY, basePosition.z);

            // Apply alpha fade if looping
            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.a = currentAlpha;
                _spriteRenderer.color = c;
            }

            // 3. Subtle aerodynamic pendulum tilt
            float tiltOsc = Mathf.Sin(time * tiltSpeed) * tiltAngle;
            float windLean = enableWindDrift ? Mathf.Sign(windDriftSpeed) * -0.5f : 0f;
            transform.rotation = Quaternion.Euler(0f, 0f, tiltOsc + windLean);

            // 4. Burner flame pulse
            if (burnerLight != null)
            {
                float pulse = (Mathf.Sin(time * burnerPulseSpeed) + 1f) * 0.5f;
                float noise = Mathf.PerlinNoise(time * 3.5f, 0f) * 0.2f;
                burnerLight.intensity = Mathf.Lerp(burnerMinIntensity, burnerMaxIntensity, pulse + noise) * currentAlpha;
            }
        }
    }
}
