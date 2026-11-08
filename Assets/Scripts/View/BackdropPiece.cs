using System.Collections.Generic;
using UnityEngine;

namespace Platformer.View
{
    /// <summary>
    /// Cuts a pixel rectangle out of a large backdrop sprite so that part can be
    /// moved, hidden or drawn in front of other objects on its own.
    ///
    /// Hierarchy (built automatically):
    ///   backdrop_1_0
    ///     └ BackdropPiece        (this + SpriteMask that erases the region from the backdrop;
    ///         │                   stays pinned at the backdrop origin)
    ///         └ Visual           (the cut-out copy; move / hide / re-sort this one)
    ///
    /// The mask lives on this object rather than a child so it cannot be deleted or
    /// undone away on its own, which left the original showing behind a moved copy.
    ///
    /// Visual at localPosition (0,0) sits exactly where the pixels came from, so any
    /// position on it is purely an offset from the original spot.
    /// The cut sprite is rebuilt from the source texture on load and never saved as an
    /// asset, so the backdrop PNG is not touched.
    ///
    /// With affectCollision on, the level collision follows too: this object carries a
    /// Difference box over the region and Visual carries the level's shape inside it, both
    /// combined with the level polygon by a CompositeCollider2D added to the source.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteMask))]
    public class BackdropPiece : MonoBehaviour
    {
        [Tooltip("잘라낼 원본 배경 스프라이트 (보통 부모)")]
        public SpriteRenderer source;

        [Tooltip("원본 텍스처의 픽셀 영역. (0,0) = 텍스처 왼쪽 아래")]
        public RectInt pixelRect = new RectInt(0, 0, 64, 64);

        [Tooltip("켜면 원본 배경에서 이 영역을 지움. 끄면 원본은 그대로 두고 복사본만 그림")]
        public bool hideOriginal = true;

        [Tooltip("켜면 충돌도 조각을 따라감: 원래 자리 충돌은 지워지고, Visual 위치에 같은 모양의 충돌이 생김. Visual을 끄면 충돌도 사라짐")]
        public bool affectCollision = true;

        [HideInInspector] public SpriteRenderer visual;

        public SpriteMask hole => GetComponent<SpriteMask>();

        // Collision follows the art through a CompositeCollider2D on the source: the level
        // polygon merges in first, this piece's region box is subtracted, then the copy of
        // the level shape that rides on Visual merges back in wherever Visual now sits.
        const int LevelCompositeOrder = 0;
        const int HoleCompositeOrder = 10;
        const int PieceCompositeOrder = 20;

        BoxCollider2D HoleCollider => GetComponent<BoxCollider2D>();
        PolygonCollider2D PieceCollider => visual != null ? visual.GetComponent<PolygonCollider2D>() : null;

        Sprite cutSprite;

        void OnEnable()
        {
            Rebuild();
        }

        void OnDisable()
        {
            // The mask and hole collider share this GameObject, so disabling only the
            // component would leave the hole punched with no copy drawn over it.
            var mask = hole;
            if (mask != null) mask.enabled = false;
            var holeCol = HoleCollider;
            if (holeCol != null) holeCol.enabled = false;
            var pieceCol = PieceCollider;
            if (pieceCol != null) pieceCol.enabled = false;
            ReleaseSprite();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Assigning sprites inside OnValidate triggers SendMessage warnings.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled) Rebuild();
            };
        }
#endif

        // pixelRect is in texture pixels, not sprite pixels: the sprite rect can start on a
        // fractional pixel (backdrop_1_0 starts at y = 73.6), and cutting relative to it
        // would land every edge between texels.

        /// <summary>Texture pixel -> source local space.</summary>
        public Vector2 PixelToLocal(Vector2 texPx)
        {
            var sp = source.sprite;
            return (texPx - sp.rect.position - sp.pivot) / sp.pixelsPerUnit;
        }

        /// <summary>Source local space -> nearest texture pixel edge.</summary>
        public Vector2Int LocalToPixel(Vector2 local)
        {
            var sp = source.sprite;
            var px = local * sp.pixelsPerUnit + sp.pivot + sp.rect.position;
            return new Vector2Int(Mathf.RoundToInt(px.x), Mathf.RoundToInt(px.y));
        }

        /// <summary>Region in the source's local space, before any Visual offset.</summary>
        public Rect LocalRegion
        {
            get
            {
                if (source == null || source.sprite == null) return default;
                Vector2 min = PixelToLocal(pixelRect.min);
                return new Rect(min, (Vector2)pixelRect.size / source.sprite.pixelsPerUnit);
            }
        }

        public void Rebuild()
        {
            if (source == null && transform.parent != null)
                source = transform.parent.GetComponent<SpriteRenderer>();
            if (source == null || source.sprite == null) return;

            var sp = source.sprite;
            ClampRect(sp);
            EnsureChildren();

            ReleaseSprite();
            var texRect = new Rect(pixelRect.x, pixelRect.y, pixelRect.width, pixelRect.height);
            // Pivot at the source sprite's pivot, so localPosition 0 lands on the original pixels.
            Vector2 srcPivotTex = sp.rect.position + sp.pivot;
            var pivot = new Vector2((srcPivotTex.x - pixelRect.x) / pixelRect.width,
                                    (srcPivotTex.y - pixelRect.y) / pixelRect.height);
            cutSprite = Sprite.Create(sp.texture, texRect, pivot, sp.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cutSprite.name = $"{sp.name}_piece_{pixelRect.x}_{pixelRect.y}";
            cutSprite.hideFlags = HideFlags.DontSave;

            visual.sprite = cutSprite;
            visual.flipX = source.flipX;
            visual.flipY = source.flipY;

            var mask = hole;
            mask.sprite = cutSprite;
            mask.alphaCutoff = 0.01f;
            // Only erase the backdrop's own sorting slot, nothing in front of or behind it.
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = source.sortingLayerID;
            mask.backSortingLayerID = source.sortingLayerID;
            mask.frontSortingOrder = source.sortingOrder;
            mask.backSortingOrder = source.sortingOrder - 1;
            mask.enabled = hideOriginal;

            if (hideOriginal)
                source.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            // The collider shape is serialised with the scene; only re-cut it while editing.
            if (!Application.isPlaying) RebuildCollision();
            ApplyCollisionState();
        }

        void ApplyCollisionState()
        {
            var holeCol = HoleCollider;
            if (holeCol != null) holeCol.enabled = affectCollision && hideOriginal;
            var pieceCol = PieceCollider;
            if (pieceCol != null) pieceCol.enabled = affectCollision && pieceCol.pathCount > 0;
        }

        /// <summary>The level's own collider under the source, ignoring other pieces.</summary>
        public Collider2D FindLevelCollider()
        {
            if (source == null) return null;
            foreach (var col in source.GetComponentsInChildren<Collider2D>(true))
            {
                if (col.isTrigger || col is CompositeCollider2D) continue;
                if (col.GetComponentInParent<BackdropPiece>() != null) continue;
                return col;
            }
            return null;
        }

        void RebuildCollision()
        {
            var level = FindLevelCollider() as PolygonCollider2D;
            if (!affectCollision || level == null)
            {
                if (affectCollision && level == null)
                    Debug.LogWarning($"[BackdropPiece] {name}: no level PolygonCollider2D under {source.name}; collision is left alone.", this);
                return;
            }
            if (!EnsureLevelComposite(level)) return;

            var paths = LevelPathsInSourceSpace(level);
            Rect region = SnapToEdges(paths, LocalRegion, CollisionSnap);

            var holeCol = HoleCollider;
            if (holeCol == null) holeCol = gameObject.AddComponent<BoxCollider2D>();
            holeCol.compositeOperation = Collider2D.CompositeOperation.Difference;
            holeCol.compositeOrder = HoleCompositeOrder;
            holeCol.offset = region.center;
            holeCol.size = region.size;

            var pieceCol = PieceCollider;
            if (pieceCol == null) pieceCol = visual.gameObject.AddComponent<PolygonCollider2D>();
            pieceCol.compositeOperation = Collider2D.CompositeOperation.Merge;
            pieceCol.compositeOrder = PieceCompositeOrder;
            pieceCol.offset = Vector2.zero;
            // Visual's local space equals the source's at zero offset, so the shape is cut in
            // source space and simply rides along when Visual is moved.
            var rects = SolidRects(paths, region);
            pieceCol.pathCount = rects.Count;
            for (int i = 0; i < rects.Count; i++)
            {
                Rect r = rects[i];
                pieceCol.SetPath(i, new[]
                {
                    new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
                    new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax)
                });
            }
        }

        /// <summary>
        /// Puts a static body and a composite on the source so the level polygon and every
        /// piece's colliders are combined into one shape. Returns false if the level collider
        /// already belongs to some other body (e.g. rebuilt with its own composite).
        /// </summary>
        bool EnsureLevelComposite(Collider2D level)
        {
            var host = source.gameObject;
            var body = host.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = host.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
            }
            var composite = host.GetComponent<CompositeCollider2D>();
            if (composite == null)
            {
                composite = host.AddComponent<CompositeCollider2D>();
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            }
            if (level.attachedRigidbody != body)
            {
                Debug.LogWarning($"[BackdropPiece] {level.name} has its own Rigidbody2D, so pieces cannot cut it. " +
                                 "Bake it to a plain PolygonCollider2D first.", level);
                return false;
            }
            level.compositeOperation = Collider2D.CompositeOperation.Merge;
            level.compositeOrder = LevelCompositeOrder;
            return true;
        }

        List<Vector2[]> LevelPathsInSourceSpace(PolygonCollider2D level)
        {
            var paths = new List<Vector2[]>(level.pathCount);
            for (int i = 0; i < level.pathCount; i++)
            {
                var path = level.GetPath(i);
                for (int j = 0; j < path.Length; j++)
                {
                    Vector3 world = level.transform.TransformPoint(path[j] + level.offset);
                    path[j] = source.transform.InverseTransformPoint(world);
                }
                paths.Add(path);
            }
            return paths;
        }

        /// <summary>
        /// The solid part of the level inside <paramref name="region"/>, as rectangles.
        /// The level collision is built from boxes, so its edges are axis aligned: cutting the
        /// region on every vertex coordinate gives cells that are each fully solid or empty,
        /// and the result is exact. Any slanted edge adds a fine grid as a fallback.
        /// </summary>
        static List<Rect> SolidRects(List<Vector2[]> paths, Rect region)
        {
            const float eps = 1e-4f;
            var xs = new List<float> { region.xMin, region.xMax };
            var ys = new List<float> { region.yMin, region.yMax };
            bool slanted = false;
            foreach (var path in paths)
            {
                for (int i = 0; i < path.Length; i++)
                {
                    Vector2 a = path[i], b = path[(i + 1) % path.Length];
                    if (a.x > region.xMin && a.x < region.xMax) xs.Add(a.x);
                    if (a.y > region.yMin && a.y < region.yMax) ys.Add(a.y);
                    if (Mathf.Abs(a.x - b.x) > eps && Mathf.Abs(a.y - b.y) > eps) slanted = true;
                }
            }
            if (slanted)
            {
                for (float x = region.xMin; x < region.xMax; x += 0.05f) xs.Add(x);
                for (float y = region.yMin; y < region.yMax; y += 0.05f) ys.Add(y);
            }
            xs = SortedUnique(xs, eps);
            ys = SortedUnique(ys, eps);

            var done = new List<Rect>();
            var open = new Dictionary<Vector2, Rect>();   // (x0, x1) -> rect growing upward
            for (int j = 0; j < ys.Count - 1; j++)
            {
                float y0 = ys[j], y1 = ys[j + 1], cy = (y0 + y1) * 0.5f;
                var runs = new List<Vector2>();
                int start = -1;
                for (int i = 0; i <= xs.Count - 1; i++)
                {
                    bool solid = i < xs.Count - 1 && Inside(paths, new Vector2((xs[i] + xs[i + 1]) * 0.5f, cy));
                    if (solid && start < 0) start = i;
                    if (!solid && start >= 0) { runs.Add(new Vector2(xs[start], xs[i])); start = -1; }
                }

                var next = new Dictionary<Vector2, Rect>();
                foreach (var run in runs)
                {
                    if (open.TryGetValue(run, out Rect r))
                    {
                        r.yMax = y1;
                        next[run] = r;
                        open.Remove(run);
                    }
                    else next[run] = Rect.MinMaxRect(run.x, y0, run.y, y1);
                }
                done.AddRange(open.Values);
                open = next;
            }
            done.AddRange(open.Values);
            return done;
        }

        /// <summary>
        /// Collision was traced from the 15360 px source art, so its edges sit between the
        /// 2048 px texels the region is snapped to. Each side of the region moves out to a
        /// collision edge lying just beyond it, so the cut doesn't leave a sliver of the
        /// shape behind (e.g. the ledge's last 0.08 units stuck to the wall).
        /// </summary>
        const float CollisionSnap = 0.12f;

        static Rect SnapToEdges(List<Vector2[]> paths, Rect r, float tolerance)
        {
            float xMin = r.xMin, xMax = r.xMax, yMin = r.yMin, yMax = r.yMax;
            foreach (var path in paths)
            {
                foreach (var p in path)
                {
                    if (p.x < r.xMin && p.x >= r.xMin - tolerance && p.x < xMin) xMin = p.x;
                    if (p.x > r.xMax && p.x <= r.xMax + tolerance && p.x > xMax) xMax = p.x;
                    if (p.y < r.yMin && p.y >= r.yMin - tolerance && p.y < yMin) yMin = p.y;
                    if (p.y > r.yMax && p.y <= r.yMax + tolerance && p.y > yMax) yMax = p.y;
                }
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static List<float> SortedUnique(List<float> values, float eps)
        {
            values.Sort();
            var result = new List<float>(values.Count);
            foreach (float v in values)
                if (result.Count == 0 || v - result[result.Count - 1] > eps) result.Add(v);
            return result;
        }

        /// <summary>Even-odd test across all paths, so nested paths read as holes.</summary>
        static bool Inside(List<Vector2[]> paths, Vector2 p)
        {
            bool inside = false;
            foreach (var path in paths)
            {
                for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
                {
                    Vector2 a = path[i], b = path[j];
                    if ((a.y > p.y) != (b.y > p.y) &&
                        p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                        inside = !inside;
                }
            }
            return inside;
        }

        void ClampRect(Sprite sp)
        {
            // Rounded outward, so a cut can take the partial texel at a fractional sprite edge
            // (backdrop_1_0 ends at y = 73.6) instead of leaving a sliver of it behind.
            int minX = Mathf.Max(0, Mathf.FloorToInt(sp.rect.xMin)), maxX = Mathf.Min(sp.texture.width, Mathf.CeilToInt(sp.rect.xMax));
            int minY = Mathf.Max(0, Mathf.FloorToInt(sp.rect.yMin)), maxY = Mathf.Min(sp.texture.height, Mathf.CeilToInt(sp.rect.yMax));
            int x = Mathf.Clamp(pixelRect.x, minX, maxX - 1);
            int y = Mathf.Clamp(pixelRect.y, minY, maxY - 1);
            int w = Mathf.Clamp(pixelRect.width, 1, maxX - x);
            int h = Mathf.Clamp(pixelRect.height, 1, maxY - y);
            pixelRect = new RectInt(x, y, w, h);
        }

        void EnsureChildren()
        {
            if (visual == null)
            {
                var t = transform.Find("Visual");
                if (t == null)
                {
                    t = new GameObject("Visual").transform;
                    t.SetParent(transform, false);
                }
                visual = t.GetComponent<SpriteRenderer>();
                if (visual == null)
                {
                    visual = t.gameObject.AddComponent<SpriteRenderer>();
                    visual.sharedMaterial = source.sharedMaterial;
                    visual.color = source.color;
                    visual.sortingLayerID = source.sortingLayerID;
                    visual.sortingOrder = source.sortingOrder;
                }
            }
            if (hole == null) gameObject.AddComponent<SpriteMask>();

            // Older pieces kept the mask on a "Hole" child; a second mask there would
            // stay put while this one follows rect edits.
            var legacy = transform.Find("Hole");
            if (legacy != null && legacy.GetComponent<SpriteMask>() != null)
            {
                if (Application.isPlaying) Destroy(legacy.gameObject);
                else DestroyImmediate(legacy.gameObject);
            }
        }

        void ReleaseSprite()
        {
            if (cutSprite == null) return;
            if (Application.isPlaying) Destroy(cutSprite);
            else DestroyImmediate(cutSprite);
            cutSprite = null;
        }

        void OnDrawGizmosSelected()
        {
            if (source == null) return;
            var r = LocalRegion;
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            Gizmos.matrix = source.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(r.center, r.size);
            if (visual != null && visual.transform.localPosition != Vector3.zero)
            {
                Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
                Gizmos.DrawWireCube((Vector3)r.center + visual.transform.localPosition, r.size);
            }
        }
    }
}
