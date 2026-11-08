using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Controls the continuous rotation of fan blades with speed adjustment,
    /// direction control, smooth spin up/down, editor preview, and sorting order management.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    public class FanSpinner : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The Transform of the rotating propeller blades. If not assigned, will look for a child named 'Blades' or 'Fan_Blades'.")]
        [SerializeField] private Transform bladesTransform;

        [Header("Rotation Settings")]
        [Tooltip("Rotation speed in degrees per second (e.g. 75 = slow atmospheric spin, 360 = fast).")]
        [SerializeField] private float rotationSpeed = 75f;

        [Tooltip("Reverse rotation direction (clockwise vs counter-clockwise).")]
        [SerializeField] private bool reverseDirection = false;

        [Tooltip("Whether the fan is currently active and spinning.")]
        [SerializeField] private bool isSpinning = true;

        [Header("Dynamics")]
        [Tooltip("Enable smooth spin up when turning on and spin down when turning off.")]
        [SerializeField] private bool smoothTransitions = true;

        [Tooltip("Acceleration / deceleration rate when starting or stopping.")]
        [SerializeField] private float acceleration = 2.0f;

        [Header("Editor Preview")]
        [Tooltip("Allow previewing rotation in the Editor Scene view without entering Play mode.")]
        [SerializeField] private bool previewInEditor = true;

        [Header("Rendering")]
        [Tooltip("Base sorting order. Back = order, Blades = order + 1, Front = order + 2.")]
        [SerializeField] private int baseSortingOrder = 0;

        [SerializeField] private SpriteRenderer backRenderer;
        [SerializeField] private SpriteRenderer bladesRenderer;
        [SerializeField] private SpriteRenderer frontRenderer;

        private float currentSpeedRatio = 1f;

        public float RotationSpeed
        {
            get => rotationSpeed;
            set => rotationSpeed = value;
        }

        public bool IsSpinning
        {
            get => isSpinning;
            set => isSpinning = value;
        }

        public bool ReverseDirection
        {
            get => reverseDirection;
            set => reverseDirection = value;
        }

        public int BaseSortingOrder
        {
            get => baseSortingOrder;
            set
            {
                baseSortingOrder = value;
                ApplySortingOrders();
            }
        }

        private void Reset()
        {
            AutoFindReferences();
            ApplySortingOrders();
        }

        private void OnValidate()
        {
            AutoFindReferences();
            ApplySortingOrders();
        }

        private void Awake()
        {
            AutoFindReferences();
            ApplySortingOrders();
            currentSpeedRatio = isSpinning ? 1f : 0f;
        }

        public void AutoFindReferences()
        {
            if (bladesTransform == null)
            {
                Transform found = transform.Find("Fan_Blades") ?? transform.Find("Blades");
                if (found != null) bladesTransform = found;
            }

            if (backRenderer == null)
            {
                Transform t = transform.Find("Fan_Back") ?? transform.Find("Back");
                if (t != null) backRenderer = t.GetComponent<SpriteRenderer>();
            }

            if (bladesRenderer == null && bladesTransform != null)
            {
                bladesRenderer = bladesTransform.GetComponent<SpriteRenderer>();
            }

            if (frontRenderer == null)
            {
                Transform t = transform.Find("Fan_Front") ?? transform.Find("Front");
                if (t != null) frontRenderer = t.GetComponent<SpriteRenderer>();
            }
        }

        public void ApplySortingOrders()
        {
            if (backRenderer != null) backRenderer.sortingOrder = baseSortingOrder;
            if (bladesRenderer != null) bladesRenderer.sortingOrder = baseSortingOrder + 1;
            if (frontRenderer != null) frontRenderer.sortingOrder = baseSortingOrder + 2;
        }

        private void Update()
        {
            if (!Application.isPlaying && !previewInEditor)
                return;

            Transform target = bladesTransform != null ? bladesTransform : transform;

            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

            if (smoothTransitions && Application.isPlaying)
            {
                float targetRatio = isSpinning ? 1f : 0f;
                currentSpeedRatio = Mathf.MoveTowards(currentSpeedRatio, targetRatio, acceleration * dt);
            }
            else
            {
                currentSpeedRatio = isSpinning ? 1f : 0f;
            }

            if (currentSpeedRatio > 0.0001f)
            {
                float dir = reverseDirection ? 1f : -1f;
                float angle = dir * rotationSpeed * currentSpeedRatio * dt;
                target.Rotate(0f, 0f, angle, Space.Self);
            }
        }

        /// <summary>
        /// Public method to toggle spinning state via UnityEvents or triggers.
        /// </summary>
        public void ToggleSpin()
        {
            isSpinning = !isSpinning;
        }
    }
}
