using UnityEngine;

namespace Platformer.View
{
    /// <summary>
    /// A flock of distant drones crossing the sky in a V like migrating birds, then a pause, then a
    /// fresh flock with the arms of the V split differently.
    ///
    /// The drones are spawned as children at runtime (not saved), and move in this object's local
    /// space from <see cref="startX"/> to <see cref="endX"/>, so put this on (or under) a
    /// ParallaxLayer to set how far away the flock reads. The sprite must be drawn nose right; it
    /// is flipped to face the direction of flight. Each drone wanders and bobs a little on its own
    /// around its place in the V.
    /// </summary>
    public class DroneSwarm : MonoBehaviour
    {
        [Header("Look")]
        public Sprite droneSprite;
        public Material material;
        public string sortingLayerName = "Default";
        public int sortingOrder = 4;

        [Tooltip("Random size per drone, as a multiple of the sprite.")]
        public Vector2 scaleRange = new Vector2(0.9f, 1.3f);

        [Tooltip("Tint of the largest drones and of the smallest; smaller reads as further away.")]
        public Color nearTint = Color.white;
        public Color farTint = new Color(0.72f, 0.64f, 0.66f, 1f);

        [Header("Formation")]
        [Min(1)] public int count = 14;

        [Tooltip("Step from one drone to the next along each arm of the V, in local units: x back from the leader, y out to the side.")]
        public Vector2 vSpacing = new Vector2(1.6f, 0.45f);

        [Tooltip("Random nudge of each drone off its exact place in the V, in local units.")]
        [Min(0f)] public float slotJitter = 0.2f;

        [Tooltip("Fewest drones on the shorter arm, as a fraction of the followers; the split is random per flock.")]
        [Range(0f, 0.5f)] public float minArmShare = 0.35f;

        [Header("Flight")]
        [Tooltip("Local x where the leader appears and where the last drone has gone.")]
        public float startX = 34f;
        public float endX = -20f;
        public float speed = 1.4f;

        [Tooltip("Distance over which a drone fades in after the start line and out before the end.")]
        [Min(0.01f)] public float fadeDistance = 4f;

        [Tooltip("Each drone flies up to this fraction faster or slower than the flock.")]
        [Range(0f, 0.2f)] public float speedJitter = 0.005f;

        [Tooltip("Seconds between one flock leaving and the next appearing (random in range).")]
        public Vector2 waitRange = new Vector2(15f, 35f);

        [Tooltip("Seconds before the first flock appears.")]
        public float firstDelay = 3f;

        [Tooltip("How far each drone drifts around its slot, in local units.")]
        public float wander = 0.25f;
        public float wanderRate = 0.08f;
        public float bobAmount = 0.1f;
        public float bobFrequency = 0.9f;

        [Tooltip("Nose-down pitch into the direction of travel, in degrees.")]
        public float pitch = 6f;

        private struct Drone
        {
            public Transform transform;
            public SpriteRenderer renderer;
            public Vector2 slot;
            public float speedFactor;
            public float seed;
            public Color tint;
        }

        private Drone[] _drones;
        private float _travelled;
        private float _waitLeft;
        private bool _flying;

        private float Direction => endX < startX ? -1f : 1f;

        private void OnEnable()
        {
            if (droneSprite == null)
            {
                Debug.LogWarning($"{name}: no drone sprite, swarm disabled.", this);
                enabled = false;
                return;
            }

            _drones = new Drone[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Drone_" + i) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = droneSprite;
                if (material != null) sr.sharedMaterial = material;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder;
                sr.flipX = Direction < 0f;
                sr.enabled = false;
                _drones[i] = new Drone { transform = go.transform, renderer = sr };
            }

            // Play mode reload is off, so the flight state is reset here rather than by field initialisers.
            _flying = false;
            _waitLeft = firstDelay;
        }

        private void OnDisable()
        {
            if (_drones == null) return;
            foreach (var d in _drones)
                if (d.transform != null) Destroy(d.transform.gameObject);
            _drones = null;
        }

        private void Launch()
        {
            int followers = _drones.Length - 1;
            int minArm = Mathf.RoundToInt(followers * minArmShare);
            int upperArm = Random.Range(minArm, followers - minArm + 1);
            for (int i = 0; i < _drones.Length; i++)
            {
                ref var d = ref _drones[i];
                d.slot = VSlot(i, upperArm);
                d.speedFactor = 1f + Random.Range(-speedJitter, speedJitter);
                d.seed = Random.Range(0f, 1000f);

                float s = Random.Range(scaleRange.x, scaleRange.y);
                float depth = Mathf.InverseLerp(scaleRange.y, scaleRange.x, s);
                d.transform.localScale = new Vector3(s, s, 1f);
                d.tint = Color.Lerp(nearTint, farTint, depth);
            }
            _travelled = 0f;
            _flying = true;
        }

        // Slot 0 leads at the point of the V; the first upperArm followers trail back along the
        // upper arm, the rest along the lower one, each a step further back and out than the last.
        private Vector2 VSlot(int index, int upperArm)
        {
            if (index == 0) return Vector2.zero;
            bool upper = index <= upperArm;
            int rank = upper ? index : index - upperArm;
            var p = new Vector2(rank * vSpacing.x, (upper ? 1f : -1f) * rank * vSpacing.y);
            return p + Random.insideUnitCircle * slotJitter;
        }

        private void Update()
        {
            if (!_flying)
            {
                _waitLeft -= Time.deltaTime;
                if (_waitLeft <= 0f) Launch();
                return;
            }

            float dir = Direction;
            float t = Time.time;
            _travelled += speed * Time.deltaTime;
            bool anyLeft = false;

            for (int i = 0; i < _drones.Length; i++)
            {
                var d = _drones[i];
                float wx = wander * (Mathf.PerlinNoise(d.seed, t * wanderRate) - 0.5f) * 2f;
                float wy = 0.6f * wander * (Mathf.PerlinNoise(d.seed + 37.1f, t * wanderRate) - 0.5f) * 2f;
                float bob = bobAmount * Mathf.Sin(2f * Mathf.PI * bobFrequency * t + d.seed);

                float x = startX + dir * (_travelled * d.speedFactor - d.slot.x) + wx;
                d.transform.localPosition = new Vector3(x, d.slot.y + wy + bob, 0f);
                d.transform.localRotation = Quaternion.Euler(0f, 0f, -dir * pitch + 1.5f * Mathf.Sin(2f * Mathf.PI * bobFrequency * 0.5f * t + d.seed));

                // Hidden behind the start line and past the end, faded in and out near both
                float fromStart = dir * (x - startX);
                float toEnd = dir * (endX - x);
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(fromStart, toEnd) / fadeDistance));
                d.renderer.enabled = fade > 0f;
                var c = d.tint;
                c.a *= fade;
                d.renderer.color = c;
                if (toEnd > 0f) anyLeft = true;
            }

            if (!anyLeft)
            {
                _flying = false;
                _waitLeft = Random.Range(waitRange.x, waitRange.y);
            }
        }
    }
}
