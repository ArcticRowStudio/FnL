using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Gantry crane trolley that shuttles along its beam between two local X stops,
    /// easing in and out and pausing at each end. Its hook is a child SwingRope with a
    /// rigid visual, which picks up the trolley's acceleration and carries a grabbing player.
    /// Runs before other scripts so the hook and its rider follow the trolley in the same frame.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class CraneTrolley : MonoBehaviour
    {
        [Header("Travel (local X in the parent's space)")]
        [Tooltip("Left stop of the trolley, local X.")]
        public float leftX = 0f;

        [Tooltip("Right stop of the trolley, local X.")]
        public float rightX = 14.5f;

        [Tooltip("Average travel speed between the stops (world units per second).")]
        [Min(0.1f)]
        public float speed = 2.5f;

        [Tooltip("Seconds the trolley waits at each stop.")]
        [Min(0f)]
        public float pauseAtEnds = 1.5f;

        [Tooltip("Whether the first run heads to the right stop.")]
        public bool startTowardsRight = true;

        private float _from, _to, _t, _duration, _pause;

        private void Awake()
        {
            // Play mode reload is off, so every runtime field is reset here.
            float x = Mathf.Clamp(transform.localPosition.x, Mathf.Min(leftX, rightX), Mathf.Max(leftX, rightX));
            BeginRun(x, startTowardsRight ? rightX : leftX);
            _pause = 0f;
        }

        private void BeginRun(float from, float to)
        {
            _from = from;
            _to = to;
            _t = 0f;
            _duration = Mathf.Abs(to - from) / Mathf.Max(0.1f, speed);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_pause > 0f)
            {
                _pause -= dt;
                if (_pause <= 0f) BeginRun(_to, Mathf.Approximately(_to, rightX) ? leftX : rightX);
                return;
            }

            _t = _duration > 0f ? Mathf.Min(1f, _t + dt / _duration) : 1f;
            Vector3 p = transform.localPosition;
            p.x = Mathf.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, _t));
            transform.localPosition = p;
            if (_t >= 1f) _pause = Mathf.Max(0.0001f, pauseAtEnds);
        }

        private void OnDrawGizmosSelected()
        {
            Transform parent = transform.parent;
            Vector3 local = transform.localPosition;
            Vector3 a = new Vector3(leftX, local.y, local.z);
            Vector3 b = new Vector3(rightX, local.y, local.z);
            if (parent != null)
            {
                a = parent.TransformPoint(a);
                b = parent.TransformPoint(b);
            }

            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.25f);
            Gizmos.DrawWireSphere(b, 0.25f);
        }
    }
}
