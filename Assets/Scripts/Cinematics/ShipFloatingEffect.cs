using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Platformer.Cinematics
{
    /// <summary>
    /// Simulates realistic maritime buoyancy and wave-induced motion for ships.
    /// Includes vertical heave (bobbing), pitch/roll rocking around the ship's
    /// center of buoyancy (waterline), and gentle horizontal surge.
    /// </summary>
    [ExecuteAlways]
    public class ShipFloatingEffect : MonoBehaviour
    {
        [Header("Center of Buoyancy (Waterline Pivot Offset)")]
        [Tooltip("Offset from sprite origin to the waterline pivot in local units")]
        public Vector2 waterlineOffset = new Vector2(8.02f, -4.93f);

        [Header("Heave Motion (Vertical Bobbing)")]
        [Tooltip("Bobbing frequency in rad/s")]
        public float heaveSpeed = 1.05f;
        [Tooltip("Vertical bobbing amplitude in world units")]
        public float heaveAmpY = 0.11f;
        [Tooltip("Horizontal surge / sway amplitude in world units")]
        public float swayAmpX = 0.045f;

        [Header("Pitch & Roll (Rocking)")]
        [Tooltip("Rocking frequency in rad/s")]
        public float pitchSpeed = 1.05f;
        [Tooltip("Maximum rocking angle in degrees")]
        public float pitchAngle = 1.15f;

        [Header("Phase Offset")]
        [Tooltip("Time phase offset in seconds for independent motion across multiple ships")]
        public float phaseOffset = 0.0f;

        private Vector3 _origPos = Vector3.zero;
        private Quaternion _origRot = Quaternion.identity;
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
            _origPos = Vector3.zero;
            _origRot = Quaternion.identity;
            _initialized = true;
        }

        public void RestoreOriginal()
        {
            transform.localPosition = _origPos;
            transform.localRotation = _origRot;
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (!Application.isPlaying && enabled && gameObject.activeInHierarchy)
            {
                AnimateShip((float)EditorApplication.timeSinceStartup);
            }
        }
#endif

        private void Update()
        {
            if (Application.isPlaying)
            {
                AnimateShip(Time.time);
            }
        }

        public void AnimateShip(float t)
        {
            if (!_initialized) Initialize();

            float time = t + phaseOffset;

            // 1. Vertical heave & horizontal surge with organic harmonic variation
            float dy = Mathf.Sin(time * heaveSpeed) * heaveAmpY 
                     + Mathf.Sin(time * heaveSpeed * 1.618f + 0.7f) * (heaveAmpY * 0.25f);
            float dx = Mathf.Cos(time * heaveSpeed * 0.8f + 0.3f) * swayAmpX;

            // 2. Angular rocking (pitch/roll) around the waterline
            float angle = Mathf.Sin(time * pitchSpeed + 1.2f) * pitchAngle;

            // Pivot point in parent local space
            Vector3 pivotPoint = _origPos + new Vector3(waterlineOffset.x, waterlineOffset.y, 0f);

            // Vector from pivot to origin
            Vector3 dirToOrigin = _origPos - pivotPoint;

            // Rotate around Z axis
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);
            Vector3 rotatedOffset = rot * dirToOrigin;

            // Apply transformed position and rotation
            transform.localPosition = pivotPoint + rotatedOffset + new Vector3(dx, dy, 0f);
            transform.localRotation = rot;
        }
    }
}
