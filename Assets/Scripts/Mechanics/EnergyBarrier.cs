using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// A flickering red force field that blocks a doorway until <see cref="Open"/> is called.
    /// The look is generated at runtime (no art needed): a translucent sheet with brighter edges.
    /// Size it with the BoxCollider2D; the visual follows the collider.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class EnergyBarrier : MonoBehaviour
    {
        public Color color = new Color(1f, 0.18f, 0.12f, 0.55f);
        public float flickerSpeed = 9f;
        public int sortingOrder = 6;

        BoxCollider2D box;
        SpriteRenderer sheet;
        float fade = 1f;
        bool opening;
        static Sprite sharedSprite;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = false;
            box.enabled = true;
            IsOpen = false;
            opening = false;
            fade = 1f;
            BuildVisual();
        }

        static Sprite GetSprite()
        {
            if (sharedSprite != null) return sharedSprite;
            // 8 x 32: bright left/right edges, faint horizontal scan lines.
            var tex = new Texture2D(8, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 8; x++)
                {
                    float a = (x == 0 || x == 7) ? 1f : (y % 4 == 0 ? 0.55f : 0.3f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            // Full Rect mesh and 1 px side borders so Sliced draw mode stretches the middle only.
            sharedSprite = Sprite.Create(tex, new Rect(0, 0, 8, 32), new Vector2(0.5f, 0.5f), 8f, 0,
                SpriteMeshType.FullRect, new Vector4(1f, 0f, 1f, 0f));
            return sharedSprite;
        }

        void BuildVisual()
        {
            var old = transform.Find("BarrierVisual");
            if (old != null) Destroy(old.gameObject);
            var go = new GameObject("BarrierVisual");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = box.offset;
            sheet = go.AddComponent<SpriteRenderer>();
            sheet.sprite = GetSprite();
            sheet.drawMode = SpriteDrawMode.Sliced;
            sheet.size = box.size;
            sheet.sortingOrder = sortingOrder;
            sheet.color = color;
        }

        void Update()
        {
            if (sheet == null) return;
            if (opening)
            {
                fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime * 2.5f);
                if (fade <= 0f) { sheet.gameObject.SetActive(false); enabled = false; }
            }
            float flicker = 0.8f + 0.2f * Mathf.Sin(Time.time * flickerSpeed) * Mathf.Sin(Time.time * flickerSpeed * 0.37f);
            var c = color;
            c.a *= flicker * fade;
            sheet.color = c;
        }

        /// <summary>Drops the field: the collider goes at once, the glow fades out.</summary>
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            opening = true;
            box.enabled = false;
        }

        void OnDrawGizmos()
        {
            var b = GetComponent<BoxCollider2D>();
            if (b == null) return;
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.35f);
            Gizmos.DrawCube(transform.TransformPoint(b.offset), Vector3.Scale(b.size, transform.lossyScale));
        }
    }
}
