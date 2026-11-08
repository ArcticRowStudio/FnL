using UnityEngine;

namespace Platformer.View
{
    /// <summary>
    /// Lays a soft blob on the ground underneath an entity and shrinks and fades it as the
    /// entity rises away from that ground.
    ///
    /// Without one, a sprite drawn over a backdrop gives the eye nothing to place it with: a
    /// character standing on a ledge and a character falling past that ledge in front of it
    /// occupy the same flat picture. The blob is the only cue a 2D scene has for where the
    /// ground under a thing is, and how far above it the thing currently is.
    ///
    /// The renderer is created at runtime and parented to nothing, so it neither inherits the
    /// owner's flipping and scaling nor has to be built into the prefab.
    /// </summary>
    public class ContactShadow : MonoBehaviour
    {
        [Header("Look")]
        /// <summary>
        /// The blob. Any soft ellipse will do; it is tinted and scaled from here.
        /// </summary>
        public Sprite sprite;

        /// <summary>
        /// Tint applied to the sprite. The alpha is the alpha used when the entity is resting
        /// on the ground, and the height falloff fades out from it.
        /// </summary>
        public Color color = new Color(0f, 0f, 0f, 0.45f);

        /// <summary>
        /// World width of the blob when the entity is resting on the ground. Left at 0 it is
        /// taken from the collider, a little wider than the entity itself, which is what a
        /// shadow cast by anything other than a light directly overhead looks like.
        /// </summary>
        public float groundWidth = 0f;

        /// <summary>
        /// How much wider than the collider a derived <see cref="groundWidth"/> comes out.
        /// </summary>
        public float widthPadding = 1.35f;

        [Header("Falloff")]
        /// <summary>
        /// Height above the ground at which the blob has shrunk and faded all the way out.
        /// Keep it near the peak of a jump: any higher and the shadow stays visible through
        /// the whole arc and stops reading as height at all.
        /// </summary>
        public float fadeHeight = 3.5f;

        /// <summary>
        /// Fraction of <see cref="groundWidth"/> the blob has shrunk to at
        /// <see cref="fadeHeight"/>. Real shadows grow as the caster leaves the surface; this
        /// shrinks instead, because on screen a smaller, fainter blob reads as "further up"
        /// far more clearly than a larger one, which mostly reads as a bigger character.
        /// </summary>
        [Range(0.05f, 1f)] public float minWidthFraction = 0.45f;

        [Header("Ground search")]
        /// <summary>
        /// Take the ground layers from the physics collision matrix for the entity's own
        /// layer, the way <see cref="Platformer.Mechanics.KinematicObject"/> does. That makes
        /// the blob land on exactly the surfaces the entity can actually stand on, with no
        /// second list to keep in step, and it works before the project has named layers of
        /// its own. Turn it off to use <see cref="groundLayers"/> instead.
        /// </summary>
        public bool useCollisionMatrix = true;

        /// <summary>
        /// Which physics layers count as ground, when <see cref="useCollisionMatrix"/> is off.
        /// </summary>
        public LayerMask groundLayers = ~0;

        /// <summary>
        /// How far down to look before giving up and hiding the blob, so a character falling
        /// down a death pit does not keep a shadow pinned to the floor far below.
        /// </summary>
        public float maxDropDistance = 20f;

        /// <summary>
        /// Lifts the blob off the surface it landed on so the two are not co-planar.
        /// </summary>
        public float surfaceOffset = 0.02f;

        /// <summary>
        /// Tilt the blob to lie along a slope rather than staying level.
        /// </summary>
        public bool alignToSlope = true;

        [Header("Sorting")]
        /// <summary>
        /// Sorting layer for the blob. Left empty it copies the owner's renderer, which is
        /// what you want while the project still has only the one sorting layer.
        /// </summary>
        public string sortingLayer = "";

        /// <summary>
        /// Where the blob sits relative to the owner's sorting order. It has to land above
        /// the ground it is drawn on but below the entity casting it, so one step under the
        /// owner is the only value that works before a proper layer set is in place.
        /// </summary>
        public int sortingOrderOffset = -1;

        Collider2D ownerCollider;
        SpriteRenderer ownerRenderer;
        SpriteRenderer shadowRenderer;
        Transform shadow;

        ContactFilter2D filter;
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];

        /// <summary>
        /// The sprite's own world size, which is what turns a width in units into a scale.
        /// </summary>
        float spriteWorldWidth;
        float resolvedWidth;

        void Awake()
        {
            ownerCollider = GetComponent<Collider2D>();
            ownerRenderer = GetComponent<SpriteRenderer>();

            if (ownerCollider == null)
            {
                Debug.LogWarning($"{name}: ContactShadow needs a Collider2D to find the ground under. Disabled.", this);
                enabled = false;
                return;
            }
            if (sprite == null)
            {
                Debug.LogWarning($"{name}: ContactShadow has no sprite assigned. Disabled.", this);
                enabled = false;
                return;
            }

            spriteWorldWidth = sprite.bounds.size.x;

            filter.useTriggers = false;
            filter.SetLayerMask(useCollisionMatrix
                ? Physics2D.GetLayerCollisionMask(gameObject.layer)
                : groundLayers.value);
            filter.useLayerMask = true;
        }

        void OnEnable()
        {
            if (shadow != null)
            {
                shadowRenderer.enabled = true;
                return;
            }

            var go = new GameObject($"{name} contact shadow");
            shadow = go.transform;
            shadowRenderer = go.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = sprite;
            shadowRenderer.color = color;

            if (!string.IsNullOrEmpty(sortingLayer))
                shadowRenderer.sortingLayerName = sortingLayer;
            else if (ownerRenderer != null)
                shadowRenderer.sortingLayerID = ownerRenderer.sortingLayerID;

            shadowRenderer.sortingOrder = (ownerRenderer != null ? ownerRenderer.sortingOrder : 0) + sortingOrderOffset;
        }

        /// <summary>
        /// The collider is measured here rather than in Awake: a Collider2D only registers
        /// with the physics system when it is enabled, which happens after every component's
        /// Awake has run, and until it does its bounds come back empty. Deriving the width
        /// from that gives a shadow scaled to nothing.
        /// </summary>
        void Start()
        {
            resolvedWidth = groundWidth > 0f ? groundWidth : ownerCollider.bounds.size.x * widthPadding;
        }

        void OnDisable()
        {
            if (shadowRenderer != null) shadowRenderer.enabled = false;
        }

        void OnDestroy()
        {
            if (shadow != null) Destroy(shadow.gameObject);
        }

        /// <summary>
        /// Runs in LateUpdate so the blob is placed against the position physics and
        /// animation have already settled on this frame, rather than trailing it by one.
        /// </summary>
        void LateUpdate()
        {
            var bounds = ownerCollider.bounds;

            // Start at the very bottom of the collider, so the distance the ray reports is
            // already the height of the entity's feet above the ground, with nothing left to
            // subtract off it.
            var origin = new Vector2(bounds.center.x, bounds.min.y);

            if (!TryFindGround(origin, out var hit))
            {
                shadowRenderer.enabled = false;
                return;
            }

            var t = Mathf.Clamp01(hit.distance / fadeHeight);

            // Fully faded is the common case for anything sitting over a long drop, and a
            // transparent quad still costs a draw call, so stop before placing it.
            if (t >= 1f)
            {
                shadowRenderer.enabled = false;
                return;
            }

            shadowRenderer.enabled = true;

            var width = resolvedWidth * Mathf.Lerp(1f, minWidthFraction, t);

            var tint = color;
            tint.a = color.a * (1f - t);
            shadowRenderer.color = tint;

            var scale = width / spriteWorldWidth;
            shadow.localScale = new Vector3(scale, scale, 1f);

            var point = hit.point + hit.normal * surfaceOffset;
            shadow.position = new Vector3(point.x, point.y, transform.position.z);
            shadow.rotation = alignToSlope
                ? Quaternion.FromToRotation(Vector3.up, hit.normal)
                : Quaternion.identity;
        }

        /// <summary>
        /// Casts down for the nearest ground, skipping anything belonging to the entity
        /// itself. The self check is needed because the ray starts flush with the owner's own
        /// collider, and Physics2D reports a hit at zero distance on a collider a query
        /// begins inside of.
        /// </summary>
        bool TryFindGround(Vector2 origin, out RaycastHit2D ground)
        {
            ground = default;

            var count = Physics2D.Raycast(origin, Vector2.down, filter, hits, maxDropDistance);
            var nearest = float.PositiveInfinity;

            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                ground = hit;
            }

            return ground.collider != null;
        }
    }
}
