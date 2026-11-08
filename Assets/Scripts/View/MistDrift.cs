using UnityEngine;

namespace Platformer.View
{
    /// <summary>
    /// Slides a mist sprite sideways and lets its opacity slowly swell and ebb.
    ///
    /// Tiled band (default): the offset wraps every <see cref="wrapWidth"/>, which must be one tile
    /// of the sprite's texture in world units: the tiled sprite looks the same after moving one
    /// tile, so the jump back is invisible.
    /// Single puff (<see cref="wrapInRange"/>): the sprite travels along local X between
    /// <see cref="minX"/> and <see cref="maxX"/>, fading out as it nears either end, and reappears
    /// at the other end while invisible.
    /// A speed of 0 leaves it in place and only breathes. Put it on the renderer's own object,
    /// under any ParallaxLayer, so the two motions add up.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class MistDrift : MonoBehaviour
    {
        [Tooltip("Sideways drift in world units per second (negative = to the left).")]
        public float speed = -0.3f;

        [Tooltip("One tile of the tiled texture in world units.")]
        [Min(0.01f)]
        public float wrapWidth = 32f;

        [Tooltip("How far the opacity swells and ebbs, as a fraction of the authored alpha.")]
        [Range(0f, 1f)]
        public float breatheAmount = 0.15f;

        [Tooltip("Breaths per second.")]
        public float breatheSpeed = 0.05f;

        [Tooltip("Shifts this layer's breathing so stacked layers don't swell together.")]
        public float breathePhase = 0f;

        [Header("Single Puff")]
        [Tooltip("Travel between Min X and Max X (local) instead of wrapping by one tile.")]
        public bool wrapInRange = false;
        public float minX = 0f;
        public float maxX = 100f;

        [Tooltip("Distance from either end over which the puff fades out.")]
        [Min(0.01f)]
        public float fadeDistance = 8f;

        private SpriteRenderer _renderer;
        private Vector3 _start;
        private float _baseAlpha;

        private void Awake()
        {
            // Play mode reload is off, so the authored values are captured afresh every session.
            _renderer = GetComponent<SpriteRenderer>();
            _start = transform.localPosition;
            _baseAlpha = _renderer.color.a;
        }

        private void Update()
        {
            float t = Time.time;
            float fade = 1f;
            if (wrapInRange && maxX > minX)
            {
                Vector3 p = _start;
                p.x = minX + Mathf.Repeat(_start.x - minX + t * speed, maxX - minX);
                transform.localPosition = p;
                float edge = Mathf.Min(p.x - minX, maxX - p.x);
                fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / fadeDistance));
            }
            else
            {
                transform.localPosition = _start + Vector3.right * Mathf.Repeat(t * speed, wrapWidth);
            }

            Color c = _renderer.color;
            c.a = fade * _baseAlpha * (1f + breatheAmount * Mathf.Sin((t * breatheSpeed + breathePhase) * 2f * Mathf.PI));
            _renderer.color = c;
        }

        private void OnDisable()
        {
            // Leave the authored state behind, not wherever the drift stopped.
            if (_renderer == null) return;
            transform.localPosition = _start;
            Color c = _renderer.color;
            c.a = _baseAlpha;
            _renderer.color = c;
        }
    }
}
