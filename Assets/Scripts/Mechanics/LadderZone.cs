using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Interactive ladder zone that allows the player to climb up, down, hang stationary,
    /// jump off, or dismount onto platforms at the top or bottom.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [SelectionBase]
    public class LadderZone : MonoBehaviour
    {
        [Header("Ladder Settings")]
        [Tooltip("Total height of the ladder in world units.")]
        [Min(0.5f)]
        public float ladderHeight = 4.0f;

        [Tooltip("Climbing speed along the ladder (world units per second).")]
        public float climbSpeed = 3.5f;

        [Tooltip("How quickly the player aligns to the ladder center X when grabbing it.")]
        public float snapToCenterSpeed = 16.0f;

        [Tooltip("Whether the player can automatically dismount onto the floor above when climbing past the top rung.")]
        public bool allowTopDismount = true;

        [Tooltip("Vertical clearance given to the player when popping onto the top ledge.")]
        public float topDismountOffset = 0.25f;

        [Header("Rendering")]
        [Tooltip("Sorting order for ladder sprites (default 2, in front of platforms (1) and behind player (5)).")]
        public int sortingOrder = 2;

        [Header("Sprite")]
        [Tooltip("One 9-sliced sprite (top cap / tiling rungs / bottom feet), pivot at the bottom. Drawn on this object's own SpriteRenderer, so the ladder has no child parts.")]
        public Sprite ladderSprite;

        // The top cap pokes this far above TopY, matching the old separate TopCap child.
        private const float TopCapOverhang = 0.16f;

        private BoxCollider2D triggerCollider;
        private SpriteRenderer spriteRenderer;

        /// <summary>Climbable height in world units. ladderHeight is local, so a Scale-tool stretch counts too.</summary>
        public float WorldHeight => ladderHeight * Mathf.Abs(transform.lossyScale.y);
        public float TopY => transform.position.y + WorldHeight;
        public float BottomY => transform.position.y;
        public float CenterX => transform.position.x;

        private void Awake()
        {
            EnsureComponents();
        }

        private void Start()
        {
            EnsureComponents();
            RebuildVisualsAndCollider();
        }

        private void OnValidate()
        {
            if (ladderHeight < 0.5f) ladderHeight = 0.5f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= OnEditorRebuild;
            UnityEditor.EditorApplication.delayCall += OnEditorRebuild;
#endif
        }

#if UNITY_EDITOR
        private void OnEditorRebuild()
        {
            if (this == null) return;
            EnsureComponents();
            RebuildVisualsAndCollider();
        }
#endif

        private void Reset()
        {
            LoadDefaultSprites();
            EnsureComponents();
            RebuildVisualsAndCollider();
        }

        private void EnsureComponents()
        {
            if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }

            if (ladderSprite == null)
            {
                LoadDefaultSprites();
            }

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        private void LoadDefaultSprites()
        {
#if UNITY_EDITOR
            if (ladderSprite == null)
                ladderSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Pixel/Environment/Ladder/Ladder_Merged.png");
#endif
        }

        public void RebuildVisualsAndCollider()
        {
            // Configure Collider
            if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
                // Ladder trigger width 0.8m for responsive grabbing, height extends slightly past ends for easy grabbing and dismounting
                triggerCollider.size = new Vector2(0.8f, ladderHeight + 0.4f);
                triggerCollider.offset = new Vector2(0f, ladderHeight * 0.5f);
            }

            // Whole ladder is one tiled 9-slice sprite: the caps stay fixed, the rungs repeat.
            if (spriteRenderer != null && ladderSprite != null)
            {
                spriteRenderer.sprite = ladderSprite;
                spriteRenderer.drawMode = SpriteDrawMode.Tiled;
                spriteRenderer.tileMode = SpriteTileMode.Continuous;
                spriteRenderer.size = new Vector2(0.48f, ladderHeight + TopCapOverhang);
                spriteRenderer.sortingOrder = sortingOrder;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.8f, 0.9f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(CenterX, transform.position.y + WorldHeight * 0.5f, 0f), new Vector3(0.48f * Mathf.Abs(transform.lossyScale.x), WorldHeight, 0.1f));

            // Mark top and bottom dismount levels
            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(CenterX - 0.4f, TopY, 0f), new Vector3(CenterX + 0.4f, TopY, 0f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(CenterX - 0.4f, BottomY, 0f), new Vector3(CenterX + 0.4f, BottomY, 0f));
        }
    }
}
