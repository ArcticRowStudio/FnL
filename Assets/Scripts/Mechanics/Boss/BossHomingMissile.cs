using System.Collections;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Mechanics;
using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// Micro homing missile launched by VTOL Main Boss.
    /// Boosts upward/outward before locking onto and pursuing the player.
    /// Can be destroyed or parried by the player.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class BossHomingMissile : MonoBehaviour, IPlayerHittable
    {
        [Header("Movement")]
        public float launchSpeed = 8f;
        public float cruiseSpeed = 9.5f;
        public float turnRate = 220f;
        public float homingDuration = 3.5f;
        public float armingDelay = 0.35f;

        [Header("Damage & Effects")]
        public int damage = 1;
        public Color explosionColor = new Color(1f, 0.45f, 0.1f);
        public AudioClip launchSound;
        public AudioClip explodeSound;

        private Rigidbody2D _rb;
        private Transform _target;
        private float _spawnTime;
        private bool _isArmed = false;
        private bool _isDestroyed = false;
        private bool _isReflected = false;
        public bool IsReflected => _isReflected;

        public void Init(Vector2 initialDirection, Transform target)
        {
            _rb = GetComponent<Rigidbody2D>();
            _target = target;
            _spawnTime = Time.time;

            if (_rb != null)
            {
                _rb.gravityScale = 0f;
                _rb.linearVelocity = initialDirection.normalized * launchSpeed;
                float angle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        void Start()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            _spawnTime = Time.time;
            Destroy(gameObject, 6f); // Safety lifetime
        }

        public void Reflect(Vector2 dir)
        {
            if (_isReflected || _isDestroyed) return;
            _isReflected = true;
            _target = null;
            _spawnTime = Time.time;

            if (dir.sqrMagnitude < 0.001f) dir = -transform.right;
            dir = dir.normalized;

            float speed = cruiseSpeed * 1.6f;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (_rb != null)
            {
                _rb.linearVelocity = dir * speed;
            }

            explosionColor = new Color(0.2f, 0.95f, 1f);
            ProjectileDebris.SpawnSpark(transform.position, Color.cyan, 14, 8f);
            ExpandingRing.Spawn(transform.position, Color.cyan, 1.2f, 0.15f);
        }

        void Update()
        {
            if (_isDestroyed) return;

            if (_isReflected)
            {
                // Reflected missile flies straight with high velocity
                if (Random.value < 0.5f)
                {
                    Vector3 trailPos = transform.position - transform.right * 0.4f;
                    ProjectileDebris.SpawnSpark(trailPos, new Color(0.2f, 0.9f, 1f, 0.9f), 3, 3f);
                }
                return;
            }

            float age = Time.time - _spawnTime;
            if (age >= armingDelay)
            {
                _isArmed = true;
            }

            if (_isArmed && age < homingDuration && _target != null)
            {
                Vector2 currentDir = transform.right;
                Vector2 targetDir = ((Vector2)_target.position - _rb.position).normalized;

                float currentAngle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
                float targetAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;

                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnRate * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, 0f, newAngle);

                Vector2 moveDir = new Vector2(Mathf.Cos(newAngle * Mathf.Deg2Rad), Mathf.Sin(newAngle * Mathf.Deg2Rad));
                _rb.linearVelocity = moveDir * cruiseSpeed;
            }

            // Spawn smoke/spark trail periodically
            if (Random.value < 0.4f)
            {
                Vector3 trailPos = transform.position - transform.right * 0.4f;
                ProjectileDebris.SpawnSpark(trailPos, new Color(1f, 0.5f, 0.1f, 0.8f), 2, 2.5f);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_isDestroyed) return;

            // When reflected by parry, attack enemies and bosses
            if (_isReflected)
            {
                if (other.GetComponent<PlayerController>() != null || other.GetComponent<BossHomingMissile>() != null)
                    return;

                bool hit = false;
                var hittable = other.GetComponentInParent<IPlayerHittable>();
                if (hittable != null)
                {
                    hittable.OnPlayerHit(transform.position);
                    hit = true;
                }
                if (!hit)
                {
                    var vtol = other.GetComponentInParent<VtolBossAI>();
                    if (vtol != null) { vtol.TakeDamage(damage * 3); hit = true; }
                    var quad = other.GetComponentInParent<QuadpodBossAI>();
                    if (quad != null)
                    {
                        var quadHealth = quad.GetComponent<Health>();
                        if (quadHealth != null)
                        {
                            for (int i = 0; i < damage * 3; i++) quadHealth.Decrement();
                        }
                        hit = true;
                    }
                }
                if (!hit)
                {
                    var enemy = other.GetComponentInParent<EnemyController>();
                    if (enemy != null)
                    {
                        var health = enemy.GetComponent<Health>();
                        if (health != null) health.Decrement();
                        hit = true;
                    }
                }
                if (!hit && !other.isTrigger)
                {
                    hit = true;
                }

                if (hit) Explode();
                return;
            }

            // Ignore boss itself before reflection
            if (other.GetComponent<VtolBossAI>() != null || other.GetComponent<QuadpodBossAI>() != null)
                return;

            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                if (player.isParrying)
                {
                    Vector2 reflectDir = player.GetParryReflectDirection(transform.position);
                    Reflect(reflectDir);
                    player.OnParrySuccess(gameObject);
                    return;
                }
                else if (player.isGuarding)
                {
                    player.OnGuardBlock(transform.position);
                    Explode();
                    return;
                }

                if (!player.isInvincible)
                {
                    Vector2 knockback = (player.transform.position - transform.position).normalized * 4f;
                    player.TakeDamage(damage, knockback);
                }
                Explode();
            }
            else if (!other.isTrigger)
            {
                // Hit solid wall or obstacle
                Explode();
            }
        }

        public void OnPlayerHit(Vector2 hitPoint)
        {
            Explode();
        }

        public void Explode()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            CameraZoomController.TriggerShake(0.2f, 0.15f);
            ExpandingRing.Spawn(transform.position, explosionColor, 2.2f, 0.25f);
            ProjectileDebris.SpawnSpark(transform.position, explosionColor, 12, 6f);

            Destroy(gameObject);
        }
    }
}
