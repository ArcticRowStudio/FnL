using System.Collections;
using Platformer.Core;
using static Platformer.Core.Simulation;
using Platformer.Gameplay;
using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// Bullet hell projectile fired by the Quadpod Boss.
    /// Supports linear velocities, arcing cluster shells, electric spark orbs,
    /// dynamic trail rendering, player damage, and parry deflection.
    /// </summary>
    public class BossProjectile : MonoBehaviour
    {
        [Header("Ballistics")]
        public float speed = 10f;
        public int damage = 1;
        public float lifeTime = 5f;
        public Vector2 velocity;
        public bool useGravity = false;
        public float gravityScale = 1.2f;

        [Header("Properties")]
        public bool isParriable = true;
        public bool isClusterShell = false;
        public int clusterFragments = 8;
        public float clusterFragmentSpeed = 7f;
        public Color projectileColor = new Color(1f, 0.75f, 0.2f);

        [Header("Parry & Reflection")]
        public bool isReflected = false;
        public int reflectedDamage = 3;

        private Rigidbody2D rb;
        private CircleCollider2D col;
        private SpriteRenderer sr;
        private TrailRenderer trail;
        private bool hasDetonated = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = useGravity ? gravityScale : 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            col = GetComponent<CircleCollider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.18f;

            SetupVisuals();
        }

        private void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        private void SetupVisuals()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = VfxSprites.Glow;
            sr.color = projectileColor;
            sr.sortingOrder = 55;

            // Procedural glowing trail
            trail = gameObject.GetComponent<TrailRenderer>();
            if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = isClusterShell ? 0.25f : 0.14f;
            trail.endWidth = 0f;
            trail.numCornerVertices = 5;
            trail.material = new Material(Shader.Find("Sprites/Default"));

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(projectileColor, 0.4f),
                    new GradientColorKey(new Color(projectileColor.r * 0.5f, projectileColor.g * 0.2f, projectileColor.b * 0.2f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            trail.colorGradient = gradient;
        }

        public void Init(Vector2 dir, float spd, Color col, bool parriable = true, bool gravity = false, float gravScale = 1.2f)
        {
            projectileColor = col;
            speed = spd;
            velocity = dir.normalized * speed;
            isParriable = parriable;
            useGravity = gravity;
            gravityScale = gravScale;

            if (rb != null)
            {
                rb.gravityScale = useGravity ? gravityScale : 0f;
                rb.linearVelocity = velocity;
            }

            if (sr != null) sr.color = projectileColor;
            SetupVisuals();
        }

        public void ConfigureAsCluster(int fragments = 8, float fragmentSpeed = 7f)
        {
            isClusterShell = true;
            clusterFragments = fragments;
            clusterFragmentSpeed = fragmentSpeed;
            transform.localScale = Vector3.one * 1.6f;
            if (col != null) col.radius = 0.28f;
        }

        public void Reflect(Vector2 reflectDir)
        {
            if (isReflected) return;
            isReflected = true;
            hasDetonated = false;

            if (reflectDir.sqrMagnitude < 0.001f)
            {
                reflectDir = -velocity.normalized;
                if (reflectDir.sqrMagnitude < 0.001f) reflectDir = Vector2.right;
            }
            reflectDir = reflectDir.normalized;

            speed = Mathf.Max(speed * 1.5f, 18f);
            velocity = reflectDir * speed;
            useGravity = false;

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = velocity;
                float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            // Visual upgrade: Brilliant Electric Cyan / Blue
            projectileColor = new Color(0.2f, 0.95f, 1f, 1f);
            if (sr != null) sr.color = projectileColor;

            if (trail != null)
            {
                trail.startWidth = 0.22f;
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(Color.white, 0.0f),
                        new GradientColorKey(new Color(0.2f, 0.95f, 1f), 0.35f),
                        new GradientColorKey(new Color(0f, 0.4f, 1f), 1.0f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(0.0f, 1.0f)
                    }
                );
                trail.colorGradient = gradient;
            }

            // Refresh lifetime for reflected travel
            CancelInvoke();
            Destroy(gameObject, 3.5f);
        }

        private void FixedUpdate()
        {
            if (rb != null)
            {
                if (!useGravity)
                {
                    rb.linearVelocity = velocity;
                }
                else
                {
                    // Rotate projectile to face trajectory while arcing
                    if (rb.linearVelocity.sqrMagnitude > 0.05f)
                    {
                        float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
                        transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    }
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasDetonated) return;

            // Reflected behavior: hits enemies, boss parts, or obstacles!
            if (isReflected)
            {
                // Reflected bullets ignore the player and other projectiles
                if (other.GetComponent<PlayerController>() != null || other.GetComponent<BossProjectile>() != null)
                {
                    return;
                }

                bool hitTarget = false;

                // 1. Check IPlayerHittable (Boss parts, drones, missiles, etc.)
                var hittable = other.GetComponentInParent<IPlayerHittable>();
                if (hittable != null)
                {
                    ProjectileDebris.SpawnSpark(transform.position, Color.cyan, 14, 8f);
                    ExpandingRing.Spawn(transform.position, Color.cyan, 1.2f, 0.15f);
                    hittable.OnPlayerHit(transform.position);
                    hitTarget = true;
                }

                // 2. Direct hit on Bosses
                if (!hitTarget)
                {
                    var vtol = other.GetComponentInParent<VtolBossAI>();
                    if (vtol != null)
                    {
                        vtol.TakeDamage(reflectedDamage);
                        ProjectileDebris.SpawnSpark(transform.position, Color.cyan, 14, 8f);
                        hitTarget = true;
                    }
                    var quad = other.GetComponentInParent<QuadpodBossAI>();
                    if (quad != null)
                    {
                        var quadHealth = quad.GetComponent<Health>();
                        if (quadHealth != null)
                        {
                            for (int i = 0; i < reflectedDamage; i++) quadHealth.Decrement();
                        }
                        ProjectileDebris.SpawnSpark(transform.position, Color.cyan, 14, 8f);
                        hitTarget = true;
                    }
                }

                // 3. Regular enemies
                if (!hitTarget)
                {
                    var enemy = other.GetComponentInParent<EnemyController>();
                    if (enemy != null)
                    {
                        if (enemy.CanTakeDamage(transform.position, transform.position - (Vector3)velocity))
                        {
                            var enemyHealth = enemy.GetComponent<Health>();
                            if (enemyHealth != null)
                            {
                                for (int i = 0; i < reflectedDamage; i++) enemyHealth.Decrement();
                                if (!enemyHealth.IsAlive) Schedule<EnemyDeath>().enemy = enemy;
                            }
                            else
                            {
                                Schedule<EnemyDeath>().enemy = enemy;
                            }
                        }
                        else
                        {
                            enemy.OnBlockedHit(transform.position, transform.position - (Vector3)velocity);
                        }
                        ProjectileDebris.SpawnSpark(transform.position, Color.cyan, 12, 7f);
                        hitTarget = true;
                    }
                }

                // 4. Hit terrain or solid obstacle
                if (!hitTarget && !other.isTrigger)
                {
                    ProjectileDebris.SpawnSpark(transform.position, projectileColor, 8, 5f);
                    hitTarget = true;
                }

                if (hitTarget)
                {
                    hasDetonated = true;
                    Destroy(gameObject);
                    return;
                }

                return;
            }

            // Normal bullet logic:
            // Enemy fire passes through every enemy, the shooter included
            if (other.GetComponentInParent<EnemyController>() != null || other.GetComponent<BossProjectile>() != null)
            {
                return;
            }

            // Player collision
            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                if (player.isParrying)
                {
                    if (isParriable)
                    {
                        // Successful parry & reflect!
                        Vector2 reflectDir = player.GetParryReflectDirection(transform.position);
                        Reflect(reflectDir);
                        player.OnParrySuccess(gameObject);
                        ProjectileDebris.SpawnSpark(transform.position, Color.cyan, 16, 9f);
                        ExpandingRing.Spawn(transform.position, Color.cyan, 1.4f, 0.2f);
                        return;
                    }
                }
                else if (player.isGuarding)
                {
                    // Continuous guard blocks damage, damages posture, sparks & destroys projectile (no reflect)
                    player.OnGuardBlock(transform.position);
                    hasDetonated = true;
                    ProjectileDebris.SpawnSpark(transform.position, Color.yellow, 12, 6f);
                    ExpandingRing.Spawn(transform.position, new Color(1f, 0.85f, 0.3f, 0.6f), 0.9f, 0.12f);
                    Destroy(gameObject);
                    return;
                }

                if (player.isInvincible)
                {
                    // Dashed / warped through without taking damage
                    return;
                }

                // Apply damage to player
                hasDetonated = true;
                Vector2 knockDir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
                if (knockDir.sqrMagnitude < 0.01f) knockDir = Vector2.up;
                player.TakeDamage(damage, knockDir * 4f);
                ProjectileDebris.SpawnSpark(transform.position, projectileColor, 8, 6f);
                Destroy(gameObject);
                return;
            }

            // Terrain / Obstacle collision
            if (!other.isTrigger)
            {
                hasDetonated = true;
                if (isClusterShell)
                {
                    DetonateCluster();
                }
                else
                {
                    ProjectileDebris.SpawnSpark(transform.position, projectileColor, 6, 4f);
                }
                Destroy(gameObject);
            }
        }

        private void DetonateCluster()
        {
            ExpandingRing.Spawn(transform.position, projectileColor, 1.8f, 0.25f);
            ProjectileDebris.SpawnSpark(transform.position, projectileColor, 18, 7.5f);

            // Burst into radial fragments
            float angleStep = 360f / clusterFragments;
            for (int i = 0; i < clusterFragments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // Upward bias if hitting floor
                if (dir.y < -0.1f) dir.y = -dir.y * 0.5f;
                dir.Normalize();

                Create(transform.position, dir, clusterFragmentSpeed, projectileColor, isParriable: true);
            }
        }

        /// <summary>
        /// Factory method to spawn and initialize a boss projectile procedurally.
        /// </summary>
        public static BossProjectile Create(Vector2 position, Vector2 direction, float speed, Color color, bool isParriable = true, bool useGravity = false, float gravityScale = 1.2f)
        {
            GameObject go = new GameObject("BossProjectile");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.9f;

            var proj = go.AddComponent<BossProjectile>();
            proj.Init(direction, speed, color, isParriable, useGravity, gravityScale);
            return proj;
        }
    }
}
