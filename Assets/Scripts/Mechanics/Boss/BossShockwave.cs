using Platformer.Gameplay;
using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// A traveling ground shockwave produced by the Quadpod Boss's heavy jump slam.
    /// Traverses horizontally across the terrain, forcing the player to jump or warp-dodge.
    /// </summary>
    public class BossShockwave : MonoBehaviour
    {
        public float speed = 7f;
        public float directionX = 1f;
        public float lifeTime = 1.8f;
        public int damage = 1;
        public Color waveColor = new Color(1f, 0.5f, 0.1f);
        public bool isElectric = false;

        private float elapsed = 0f;
        private SpriteRenderer sr;
        private BoxCollider2D col;

        private void Awake()
        {
            col = GetComponent<BoxCollider2D>();
            if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.5f, 0.8f);
            col.offset = new Vector2(0f, 0.4f);

            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = VfxSprites.Ring;
            sr.sortingOrder = 52;

            transform.localScale = new Vector3(0.6f, 1.2f, 1f);
        }

        public void Init(float dirX, float spd, Color colr, bool electric = false)
        {
            directionX = Mathf.Sign(dirX);
            speed = spd;
            waveColor = colr;
            isElectric = electric;

            if (sr != null) sr.color = waveColor;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= lifeTime)
            {
                Destroy(gameObject);
                return;
            }

            // Move along ground
            transform.Translate(Vector3.right * directionX * speed * Time.deltaTime, Space.World);

            // Stick to ground surface
            RaycastHit2D hit = Physics2D.Raycast(transform.position + Vector3.up * 0.5f, Vector2.down, 1.5f);
            if (hit.collider != null && !hit.collider.isTrigger && hit.collider.GetComponentInParent<EnemyController>() == null)
            {
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            }

            // Emitting trail sparks
            if (Random.value < 0.45f)
            {
                Vector3 sparkPos = transform.position + Vector3.up * Random.Range(0.1f, 0.5f);
                Color c = isElectric ? new Color(0.4f, 0.8f, 1f) : waveColor;
                ProjectileDebris.SpawnSpark(sparkPos, c, 2, 3f);
            }

            // Fade out near end of life
            float alpha = Mathf.Clamp01(1f - (elapsed / lifeTime));
            if (sr != null)
            {
                Color current = waveColor;
                current.a = alpha;
                sr.color = current;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Any boss can raise a shockwave from under itself; none should snag on it.
            if (other.GetComponentInParent<EnemyController>() != null) return;

            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                if (player.isParrying)
                {
                    player.OnParrySuccess(gameObject);
                    ProjectileDebris.SpawnSpark(transform.position, Color.cyan, 12, 8f);
                    Destroy(gameObject);
                    return;
                }

                if (player.isInvincible) return;

                // Player got hit by shockwave
                Vector2 knockback = new Vector2(directionX * 4f, 4.5f);
                player.TakeDamage(damage, knockback);
                ProjectileDebris.SpawnSpark(transform.position, waveColor, 8, 6f);
            }
        }

        public static BossShockwave Create(Vector2 position, float dirX, float speed, Color color, bool isElectric = false)
        {
            GameObject go = new GameObject("BossShockwave");
            go.transform.position = position;
            var sw = go.AddComponent<BossShockwave>();
            sw.Init(dirX, speed, color, isElectric);
            return sw;
        }
    }
}
