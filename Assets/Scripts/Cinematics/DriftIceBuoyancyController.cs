using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Platformer.Cinematics
{
    /// <summary>
    /// Animates gentle floating, bobbing, and subtle tilting of drift ice floes in the glacial bay.
    /// Simulates natural ocean swell and buoyancy for both the ice chunk and its water reflection.
    /// </summary>
    [ExecuteAlways]
    public class DriftIceBuoyancyController : MonoBehaviour
    {
        [System.Serializable]
        public class DriftIceItem
        {
            public string name;
            public Transform iceTransform;
            public SpriteRenderer iceRenderer;
            public SpriteRenderer reflectionRenderer;
            [Tooltip("Underwater glowing cyan wake / trailing afterimage")]
            public SpriteRenderer wakeRenderer;
            [Tooltip("Base position of this drift ice piece before wave displacement")]
            public Vector3 baseLocalPosition;
            public float bobAmplitude = 0.06f;
            public float bobFrequency = 0.85f;
            public float tiltAmplitude = 0.6f;
            public float tiltFrequency = 0.65f;
            public float phaseOffset = 0f;

            [Header("Underwater Wake Animation")]
            public float wakeShimmerSpeed = 1.4f;
            public float wakeWaveSpread = 0.05f;
        }

        [Header("Drift Ice Items")]
        public DriftIceItem[] driftIceItems;

        [Header("Global Settings")]
        [Tooltip("Master speed multiplier for ocean swell")]
        public float globalSwellSpeed = 1.0f;
        [Tooltip("Master amplitude multiplier")]
        public float globalSwellScale = 1.0f;

        [Header("Editor Preview")]
        [Tooltip("Enable to preview buoyant floating animation in edit mode. When OFF, you can freely drag ice pieces in Scene view or type coordinates in Inspector!")]
        public bool previewInEditor = false;

        private bool _lastPreviewState = false;

        private void Awake()
        {
            CaptureCurrentPositionsAsBase();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
#endif
        }

        [ContextMenu("Capture Current Positions As Base")]
        public void CaptureCurrentPositionsAsBase()
        {
            if (driftIceItems == null) return;
            foreach (var item in driftIceItems)
            {
                if (item != null && item.iceTransform != null)
                {
                    item.baseLocalPosition = item.iceTransform.localPosition;
                }
            }
        }

        [ContextMenu("Reset Transforms To Base")]
        public void ResetToBasePositions()
        {
            if (driftIceItems == null) return;
            foreach (var item in driftIceItems)
            {
                if (item != null && item.iceTransform != null)
                {
                    item.iceTransform.localPosition = item.baseLocalPosition;
                    item.iceTransform.localRotation = Quaternion.identity;
                    if (item.reflectionRenderer != null)
                    {
                        item.reflectionRenderer.transform.localScale = Vector3.one;
                        item.reflectionRenderer.transform.localRotation = Quaternion.identity;
                    }
                }
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
                    AnimateBuoyancy((float)EditorApplication.timeSinceStartup);
                }
                else
                {
                    // If preview was just turned off, reset transforms to rest positions
                    if (_lastPreviewState)
                    {
                        _lastPreviewState = false;
                        ResetToBasePositions();
                    }

                    // When preview is off, user can freely drag in Scene view or enter coordinates in Inspector!
                    // Automatically capture modified positions as new baseLocalPosition
                    if (driftIceItems != null)
                    {
                        foreach (var item in driftIceItems)
                        {
                            if (item != null && item.iceTransform != null && item.iceTransform.hasChanged)
                            {
                                item.baseLocalPosition = item.iceTransform.localPosition;
                                item.iceTransform.hasChanged = false;
                            }
                        }
                    }
                }
            }
        }
#endif

        private void Update()
        {
            if (Application.isPlaying)
            {
                AnimateBuoyancy(Time.time);
            }
        }

        public void AnimateBuoyancy(float t)
        {
            if (driftIceItems == null) return;

            float time = t * globalSwellSpeed;

            for (int i = 0; i < driftIceItems.Length; i++)
            {
                var item = driftIceItems[i];
                if (item == null || item.iceTransform == null) continue;

                // 1. Vertical Heave (ocean swell bobbing)
                float heave = Mathf.Sin(time * item.bobFrequency + item.phaseOffset) * (item.bobAmplitude * globalSwellScale);
                heave += Mathf.Sin(time * item.bobFrequency * 1.618f + item.phaseOffset * 2f) * (item.bobAmplitude * globalSwellScale * 0.25f);

                // 2. Subtle Roll / Pitch Tilt
                float tilt = Mathf.Sin(time * item.tiltFrequency + item.phaseOffset * 1.3f) * (item.tiltAmplitude * globalSwellScale);

                // Apply to transform relative to baseLocalPosition
                item.iceTransform.localPosition = item.baseLocalPosition + new Vector3(0f, heave, 0f);
                item.iceTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);

                // 3. Water Reflection Refraction / Ripple Shimmer
                if (item.reflectionRenderer != null)
                {
                    float wave = Mathf.Sin(time * item.wakeShimmerSpeed + item.phaseOffset * 1.7f);
                    float swell = Mathf.Cos(time * item.wakeShimmerSpeed * 0.7f + item.phaseOffset);
                    
                    // Subtle horizontal water ripple smearing
                    float scaleX = 1.0f + wave * item.wakeWaveSpread;
                    float scaleY = 1.0f + swell * (item.wakeWaveSpread * 0.5f);
                    item.reflectionRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1.0f);

                    // Fluid wave lag for underwater reflection
                    float reflTilt = Mathf.Sin(time * item.tiltFrequency + item.phaseOffset * 1.3f - 0.2f) * (item.tiltAmplitude * 0.4f);
                    item.reflectionRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, reflTilt - tilt);

                    // Opacity breathing
                    Color rc = item.reflectionRenderer.color;
                    rc.a = Mathf.Clamp01(0.85f + wave * 0.08f);
                    item.reflectionRenderer.color = rc;
                }
            }
        }
    }
}
