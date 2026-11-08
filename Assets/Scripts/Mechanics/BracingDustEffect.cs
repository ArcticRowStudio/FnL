using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Temporary sand/dust smoke effect spawned at Row's feet when blocking bullets in Bracing Shield stance.
    /// Fades out smoothly into full transparency and slightly billows before self-destructing.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BracingDustEffect : MonoBehaviour
    {
        [Tooltip("Lifetime of the dust puff before fully vanishing.")]
        public float duration = 0.42f;

        [Tooltip("Starting alpha of the dust puff.")]
        public float startAlpha = 0.95f;

        [Tooltip("Slight outward expansion as the dust billows, as a fraction of the spawn scale.")]
        public Vector3 scaleExpansion = new Vector3(0.06f, 0.04f, 0f);

        private SpriteRenderer spriteRenderer;
        private float elapsed = 0f;
        private Vector3 initialScale;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            initialScale = transform.localScale;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth quad ease-out fade to transparent
            float alpha = Mathf.Lerp(startAlpha, 0f, t * t);
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
            }

            // Subtle billowing expansion
            transform.localScale = initialScale + Vector3.Scale(initialScale, scaleExpansion) * Mathf.Sin(t * Mathf.PI * 0.5f);

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Spawns the bracing dust effect at the target position matching Row's orientation and sorting order.
        /// </summary>
        public static BracingDustEffect Spawn(Sprite dustSprite, Vector3 position, Vector3 scale, bool flipX, int sortingOrder, string sortingLayerName = "Default")
        {
            if (dustSprite == null) return null;

            GameObject go = new GameObject("BracingDustEffect");
            go.transform.position = position;
            go.transform.localScale = scale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dustSprite;
            sr.flipX = flipX;
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                sr.sortingLayerName = sortingLayerName;
            }
            sr.sortingOrder = sortingOrder;

            Color c = Color.white;
            c.a = 0.95f;
            sr.color = c;

            BracingDustEffect effect = go.AddComponent<BracingDustEffect>();
            return effect;
        }
    }
}
