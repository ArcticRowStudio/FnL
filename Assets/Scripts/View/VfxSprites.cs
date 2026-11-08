using System.Collections.Generic;
using UnityEngine;

namespace Platformer.View
{
    /// <summary>
    /// Sprites for the procedural effects, generated once at runtime. A SpriteRenderer draws
    /// nothing at all without a sprite, and the project ships no effect art, so the shapes
    /// are built from code instead.
    /// </summary>
    public static class VfxSprites
    {
        /// <summary>
        /// Texture size in pixels. Kept small so the shapes read as drawn pixels rather than
        /// as a smooth gradient sitting on top of pixel art.
        /// </summary>
        const int Resolution = 16;

        /// <summary>
        /// Number of alpha steps. Hand drawn pixel art uses a handful of flat values, so the
        /// falloff is quantised instead of continuous.
        /// </summary>
        const int AlphaSteps = 3;

        static Sprite glow;
        static Sprite ring;

        /// <summary>
        /// A soft round dot, bright in the middle and fading out to the edge.
        /// </summary>
        public static Sprite Glow => glow != null ? glow : glow = Create(false);

        /// <summary>
        /// A hollow ring, for shockwaves.
        /// </summary>
        public static Sprite Ring => ring != null ? ring : ring = Create(true);

        static Sprite Create(bool hollow)
        {
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[Resolution * Resolution];
            var half = Resolution * 0.5f;

            for (var y = 0; y < Resolution; y++)
            {
                for (var x = 0; x < Resolution; x++)
                {
                    // Distance from the centre, normalised so the edge of the texture is 1.
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha;
                    if (hollow)
                    {
                        // A band around 0.78 of the radius, feathered on both sides.
                        alpha = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.78f) / 0.28f);
                    }
                    else
                    {
                        alpha = Mathf.Clamp01(1f - distance);
                        alpha *= alpha; // Squared falloff reads as a glow rather than a disc.
                    }

                    // Snap to a few flat steps: continuous alpha is the tell of a filter
                    // effect rather than drawn pixels.
                    alpha = Mathf.Round(alpha * AlphaSteps) / AlphaSteps;

                    pixels[y * Resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // Pixels per unit equal to the resolution, so the sprite is 1 world unit wide.
            return Sprite.Create(texture, new Rect(0f, 0f, Resolution, Resolution),
                new Vector2(0.5f, 0.5f), Resolution);
        }
    }

    /// <summary>
    /// A ring that expands and fades, then removes itself. Used for impacts.
    /// </summary>
    public class ExpandingRing : MonoBehaviour
    {
        public float duration = 0.25f;
        public float startScale = 0.3f;
        public float endScale = 2f;

        float elapsed;
        SpriteRenderer spriteRenderer;
        Color startColor;

        // Pooled like ProjectileDebris: spent rings are parked and reused.
        const int MaxPooled = 64;
        static readonly Stack<ExpandingRing> Pool = new Stack<ExpandingRing>();

        // Domain reload is off, so the static pool would otherwise carry destroyed rings over.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPool()
        {
            Pool.Clear();
        }

        /// <summary>
        /// Spawns a shockwave ring at a world position.
        /// </summary>
        public static ExpandingRing Spawn(Vector3 position, Color color, float endScale = 2f,
            float duration = 0.25f)
        {
            ExpandingRing ring = null;
            while (ring == null && Pool.Count > 0) ring = Pool.Pop();
            if (ring == null)
            {
                var go = new GameObject("ImpactRing");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = VfxSprites.Ring;
                sr.sortingOrder = 50;
                ring = go.AddComponent<ExpandingRing>();
            }

            ring.transform.SetPositionAndRotation(position, Quaternion.identity);
            ring.spriteRenderer.color = color;
            ring.startScale = 0.3f;
            ring.endScale = endScale;
            ring.duration = duration;
            ring.elapsed = 0f;
            ring.startColor = color;
            ring.transform.localScale = Vector3.one * ring.startScale;
            ring.gameObject.SetActive(true);
            return ring;
        }

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            var t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

            // Fast expansion that eases out, fading as it grows.
            var eased = 1f - (1f - t) * (1f - t);
            var scale = Mathf.Lerp(startScale, endScale, eased);
            transform.localScale = new Vector3(scale, scale, 1f);
            spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b,
                startColor.a * (1f - t));

            if (t >= 1f)
            {
                if (Pool.Count >= MaxPooled) Destroy(gameObject);
                else
                {
                    gameObject.SetActive(false);
                    Pool.Push(this);
                }
            }
        }
    }
}
