using UnityEngine;

namespace Platformer.View
{
    /// <summary>
    /// Turns a part slowly back and forth about its pivot, like a dish antenna scanning the sky.
    /// Two sine waves at unrelated rates are mixed so the sweep never settles into an obvious loop.
    /// Set the sprite's pivot where the part is mounted; the authored rotation is the centre of the sweep.
    /// </summary>
    public class SlowScan : MonoBehaviour
    {
        [Tooltip("Largest swing either side of the authored rotation, in degrees.")]
        [Range(0f, 45f)]
        public float amplitude = 4f;

        [Tooltip("Seconds for one full back-and-forth of the main sweep.")]
        [Min(0.1f)]
        public float period = 14f;

        [Tooltip("Offsets the sweep so several scanners don't move in step.")]
        public float phase = 0f;

        private Quaternion _base;

        private void Awake()
        {
            // Play mode reload is off, so the authored rotation is captured afresh every session.
            _base = transform.localRotation;
        }

        private void Update()
        {
            float w = 2f * Mathf.PI / period;
            float t = Time.time;
            float sweep = 0.75f * Mathf.Sin(w * t + phase) + 0.25f * Mathf.Sin(2.37f * w * t + 1.3f + phase);
            transform.localRotation = _base * Quaternion.Euler(0f, 0f, amplitude * sweep);
        }

        private void OnDisable()
        {
            // Leave the authored pose behind rather than wherever the sweep stopped.
            transform.localRotation = _base;
        }
    }
}
