using UnityEngine;

namespace Platformer.View
{
    /// <summary>
    /// Plays a sprite sequence once and removes itself.
    ///
    /// A one shot effect does not need an Animator: no state machine, no controller asset,
    /// and the caller can vary the speed per spawn.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteSequenceEffect : MonoBehaviour
    {
        public Sprite[] frames;
        public float framesPerSecond = 24f;

        /// <summary>
        /// Scales the whole effect. Kept separate from the transform so a prefab can be
        /// dropped in at scale one and still be tuned from the caller.
        /// </summary>
        public float sizeMultiplier = 1f;

        [Header("Sweep")]
        /// <summary>
        /// Angle the effect starts at, in degrees.
        /// </summary>
        public float startDegrees;

        /// <summary>
        /// How far the effect turns over its lifetime. The sprite turns about its own pivot,
        /// so an arc whose pivot sits on the character sweeps around the body rather than
        /// hanging in front of it: the difference between a still slash and a swung one.
        /// Zero leaves the effect unturned.
        /// </summary>
        public float sweepDegrees;

        SpriteRenderer spriteRenderer;
        float elapsed;

        /// <summary>
        /// Flipped to -1 when the effect is mirrored, so a sweep that reads as an overhand
        /// swing facing right reads the same way facing left.
        /// </summary>
        float sweepSign = 1f;

        float Duration => frames == null || frames.Length == 0 || framesPerSecond <= 0f
            ? 0f
            : frames.Length / framesPerSecond;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            elapsed = 0f;
            if (frames != null && frames.Length > 0) spriteRenderer.sprite = frames[0];
            if (!Mathf.Approximately(sizeMultiplier, 1f)) transform.localScale *= sizeMultiplier;
            ApplySweep(0f);
        }

        void Update()
        {
            if (frames == null || frames.Length == 0) { Destroy(gameObject); return; }

            elapsed += Time.deltaTime;
            var index = Mathf.FloorToInt(elapsed * framesPerSecond);
            if (index >= frames.Length) { Destroy(gameObject); return; }

            spriteRenderer.sprite = frames[index];

            var duration = Duration;
            ApplySweep(duration <= 0f ? 0f : Mathf.Clamp01(elapsed / duration));
        }

        void ApplySweep(float t)
        {
            if (Mathf.Approximately(startDegrees, 0f) && Mathf.Approximately(sweepDegrees, 0f)) return;
            transform.localRotation = Quaternion.Euler(0f, 0f, sweepSign * (startDegrees + sweepDegrees * t));
        }

        /// <summary>
        /// Drops a copy of <paramref name="prefab"/> at a point, turned to face
        /// <paramref name="direction"/>. Returns null when there is no prefab set, so callers
        /// can leave the field empty and simply get no effect.
        /// </summary>
        public static SpriteSequenceEffect Spawn(SpriteSequenceEffect prefab, Vector3 position, Vector2 direction)
        {
            if (prefab == null) return null;

            var rotation = Quaternion.identity;
            if (direction.sqrMagnitude > 0.0001f)
            {
                // The art is drawn pointing right, so the angle to that axis is the rotation.
                rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
            return Instantiate(prefab, position, rotation);
        }

        /// <summary>
        /// Drops a copy of <paramref name="prefab"/> at a point, mirrored when facing left.
        ///
        /// Use this rather than <see cref="Spawn"/> for anything with a clear up and down: a
        /// slash arc turned through 180 degrees comes out upside down, while mirroring it
        /// about the pivot keeps the sweep reading the same way round.
        /// </summary>
        public static SpriteSequenceEffect SpawnMirrored(SpriteSequenceEffect prefab, Vector3 position, bool faceRight)
        {
            if (prefab == null) return null;

            var instance = Instantiate(prefab, position, Quaternion.identity);
            var renderer = instance.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.flipX = !faceRight;

            // Mirroring the sprite reverses which way a turn reads, so the sweep is negated
            // to keep the swing going the same way round the character.
            instance.sweepSign = faceRight ? 1f : -1f;
            instance.ApplySweep(0f);
            return instance;
        }
    }
}
