using System.Collections.Generic;
using Platformer.Gameplay;
using Platformer.Mechanics;
using Platformer.Core;
using Platformer.View;
using UnityEngine;
using static Platformer.Core.Simulation;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Handles movement and collision of the player's projectiles.
    /// Includes visual upgrades: TrailRenderer and Impact Spark particles.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public float speed = 15f;
        public float lifeTime = 2f;
        public Vector2 direction = Vector2.right;

        // Set on the first hit. Destroy waits for the end of the frame, so a shot entering two
        // colliders in one step (a boss part and the hull under it) would otherwise land twice.
        private bool spent;

        void Awake()
        {
            // Ensure we have a Rigidbody2D to detect collisions with static environment (Terrain)
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Add a TrailRenderer procedurally for a premium glowing tail effect!
            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = 0.15f;
            trail.endWidth = 0f;
            trail.numCornerVertices = 5;

            // Set trail material and color
            trail.material = new Material(Shader.Find("Sprites/Default"));
            
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.yellow, 0.0f), 
                    new GradientColorKey(new Color(1f, 0.5f, 0f), 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1.0f, 0.0f), 
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            trail.colorGradient = gradient;
        }

        void Start()
        {
            // Auto destroy after lifeTime seconds
            Destroy(gameObject, lifeTime);
        }

        void Update()
        {
            // Move projectile forward in the set direction
            transform.Translate(direction * speed * Time.deltaTime, Space.World);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (spent || other.GetComponent<PlayerController>() != null) return;

            // Drones and missiles are triggers, so check for them before triggers are skipped.
            var hittable = other.GetComponentInParent<IPlayerHittable>();
            if (hittable != null)
            {
                ProjectileDebris.SpawnSpark(transform.position, new Color(1f, 0.2f, 0.1f), 10, 6f);
                hittable.OnPlayerHit(transform.position);
                spent = true;
                Destroy(gameObject);
                return;
            }

            // Prevent colliding with other triggers
            if (other.isTrigger)
            {
                return;
            }

            // Check if we hit an enemy
            var enemy = other.GetComponent<EnemyController>();
            if (enemy != null)
            {
                if (!enemy.CanTakeDamage(transform.position, transform.position - (Vector3)direction))
                {
                    enemy.OnBlockedHit(transform.position, transform.position - (Vector3)direction);
                    spent = true;
                    Destroy(gameObject);
                    return;
                }

                // Spawn impact sparks (Red/Orange blood-splat style sparks!)
                ProjectileDebris.SpawnSpark(transform.position, new Color(1f, 0.2f, 0.1f), 10, 6f);

                var enemyHealth = enemy.GetComponent<Health>();
                if (enemyHealth != null)
                {
                    enemyHealth.Decrement();
                    if (!enemyHealth.IsAlive)
                    {
                        Schedule<EnemyDeath>().enemy = enemy;
                    }
                }
                else
                {
                    Schedule<EnemyDeath>().enemy = enemy;
                }
            }
            else
            {
                // Spawn impact sparks for obstacles (Grey/Yellow dust sparks!)
                ProjectileDebris.SpawnSpark(transform.position, new Color(0.9f, 0.9f, 0.7f), 6, 4f);
            }

            // Destroy the projectile when it hits anything solid (ground, wall, enemy)
            spent = true;
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Procedural particle system for projectile shoot and hit sparks.
    /// Pooled: engines, smoke and impacts spawn hundreds a second during a boss fight, so a spent
    /// particle is parked and reused instead of destroyed.
    /// </summary>
    public class ProjectileDebris : MonoBehaviour
    {
        public Vector2 velocity;
        public float fadeSpeed = 3f;
        private SpriteRenderer sr;

        const int MaxPooled = 600;
        static readonly Stack<ProjectileDebris> Pool = new Stack<ProjectileDebris>();

        // Domain reload is off in this project, so statics outlive a play session; the parked
        // particles do not (they are destroyed on Stop or scene change). Start each session empty.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPool()
        {
            Pool.Clear();
        }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            transform.Translate(velocity * Time.deltaTime);
            
            // Shrink and fade
            transform.localScale -= Vector3.one * fadeSpeed * Time.deltaTime;
            if (transform.localScale.x <= 0f)
            {
                Release();
            }
        }

        void Release()
        {
            if (Pool.Count >= MaxPooled)
            {
                Destroy(gameObject);
                return;
            }
            gameObject.SetActive(false);
            Pool.Push(this);
        }

        /// <summary>
        /// One particle: drifts at the given velocity and shrinks away at fadeSpeed scale per
        /// second. Reuses a parked particle when there is one.
        /// </summary>
        public static ProjectileDebris Spawn(Vector3 position, Color color, float scale, Vector2 velocity, float fadeSpeed, int sortingOrder = 140)
        {
            ProjectileDebris debris = null;
            // Entries can be destroyed objects (scene change); those compare equal to null.
            while (debris == null && Pool.Count > 0) debris = Pool.Pop();
            if (debris == null)
            {
                var go = new GameObject("SparkParticle");
                go.AddComponent<SpriteRenderer>().sprite = VfxSprites.Glow;
                debris = go.AddComponent<ProjectileDebris>();
            }

            Transform t = debris.transform;
            t.SetPositionAndRotation(position, Quaternion.identity);
            t.localScale = Vector3.one * scale;
            debris.sr.color = color;
            debris.sr.sortingOrder = sortingOrder;
            debris.velocity = velocity;
            debris.fadeSpeed = fadeSpeed;
            debris.gameObject.SetActive(true);
            return debris;
        }

        /// <summary>
        /// Spawns a burst of beautiful procedural circular sparks.
        /// </summary>
        public static void SpawnSpark(Vector3 position, Color color, int count = 8, float speed = 5f, float minScale = 0.12f, float maxScale = 0.25f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float rSpeed = Random.Range(speed * 0.3f, speed);
                Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rSpeed;
                Spawn(position, color, Random.Range(minScale, maxScale), velocity, Random.Range(1.5f, 3f));
            }
        }
    }
}
