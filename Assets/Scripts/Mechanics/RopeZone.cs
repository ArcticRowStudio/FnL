using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Interactive hanging rope zone that allows the player to climb up, hang stationary,
    /// slide down rapidly with friction particles, and jump off.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider2D))]
    [SelectionBase]
    public class RopeZone : MonoBehaviour
    {
        [Header("Rope Settings")]
        [Tooltip("Total hanging length of the rope in world units.")]
        [Min(0.5f)]
        public float ropeLength = 5.0f;

        [Tooltip("Standard climbing speed along the rope (world units per second).")]
        public float climbSpeed = 3.0f;

        [Tooltip("Fast slide descent speed when holding Down/S.")]
        public float slideSpeed = 8.0f;

        [Tooltip("Whether pressing Down triggers fast sliding down the rope.")]
        public bool allowFastSlide = true;

        [Tooltip("How quickly the player aligns to the rope center X when grabbing it.")]
        public float snapToCenterSpeed = 16.0f;

        [Header("Rendering")]
        [Tooltip("Sorting order for rope sprites (default 2, in front of platforms (1) and behind player (5)).")]
        public int sortingOrder = 2;

        [Header("Sprites")]
        public Sprite middleSprite;
        public Sprite topAnchorSprite;
        public Sprite bottomStopperSprite;

        private BoxCollider2D triggerCollider;
        private SpriteRenderer middleRenderer;
        private SpriteRenderer topRenderer;
        private SpriteRenderer bottomRenderer;

        public float TopY => transform.position.y;
        public float BottomY => transform.position.y - ropeLength;
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
            if (ropeLength < 0.5f) ropeLength = 0.5f;
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

            if (middleSprite == null || topAnchorSprite == null || bottomStopperSprite == null)
            {
                LoadDefaultSprites();
            }

            middleRenderer = EnsureChildRenderer("RopeMiddle", middleSprite, 0);
            topRenderer = EnsureChildRenderer("TopAnchor", topAnchorSprite, 1);
            bottomRenderer = EnsureChildRenderer("BottomStopper", bottomStopperSprite, 1);
        }

        private void LoadDefaultSprites()
        {
#if UNITY_EDITOR
            if (middleSprite == null)
                middleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Pixel/Environment/Rope/Rope_Middle.png");
            if (topAnchorSprite == null)
                topAnchorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Pixel/Environment/Rope/Rope_TopAnchor.png");
            if (bottomStopperSprite == null)
                bottomStopperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Pixel/Environment/Rope/Rope_BottomStopper.png");
#endif
        }

        private SpriteRenderer EnsureChildRenderer(string childName, Sprite sprite, int orderOffset)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                GameObject go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr == null) sr = child.gameObject.AddComponent<SpriteRenderer>();

            if (sprite != null) sr.sprite = sprite;
            sr.sortingOrder = orderOffset;
            return sr;
        }

        public void RebuildVisualsAndCollider()
        {
            if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
                // Rope trigger width 0.8m for responsive grabbing, height matching ropeLength
                triggerCollider.size = new Vector2(0.8f, ropeLength);
                triggerCollider.offset = new Vector2(0f, -ropeLength * 0.5f);
            }

            // Middle braided cable (Tiled)
            if (middleRenderer != null && middleSprite != null)
            {
                middleRenderer.sprite = middleSprite;
                middleRenderer.drawMode = SpriteDrawMode.Tiled;
                middleRenderer.tileMode = SpriteTileMode.Continuous;
                middleRenderer.size = new Vector2(0.16f, ropeLength);
                middleRenderer.transform.localPosition = new Vector3(0f, -ropeLength * 0.5f, 0f);
                middleRenderer.sortingOrder = sortingOrder;
            }

            // Top Anchor bracket
            if (topRenderer != null && topAnchorSprite != null)
            {
                topRenderer.sprite = topAnchorSprite;
                topRenderer.drawMode = SpriteDrawMode.Simple;
                topRenderer.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                topRenderer.sortingOrder = sortingOrder + 1;
            }

            // Bottom Stopper
            if (bottomRenderer != null && bottomStopperSprite != null)
            {
                bottomRenderer.sprite = bottomStopperSprite;
                bottomRenderer.drawMode = SpriteDrawMode.Simple;
                bottomRenderer.transform.localPosition = new Vector3(0f, -ropeLength, 0f);
                bottomRenderer.sortingOrder = sortingOrder + 1;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1.0f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(CenterX, transform.position.y - ropeLength * 0.5f, 0f), new Vector3(0.4f, ropeLength, 0.1f));

            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(CenterX - 0.3f, TopY, 0f), new Vector3(CenterX + 0.3f, TopY, 0f));
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(CenterX - 0.3f, BottomY, 0f), new Vector3(CenterX + 0.3f, BottomY, 0f));
        }
    }
}
