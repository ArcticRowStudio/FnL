using System.Collections;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Model;
using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Spider-Rabbit enemy AI controller.
    /// Handles autonomous patrol, high-speed player chase, and ferocious lunging jump attacks.
    /// Includes idle behaviors when undisturbed, and a 2-phase mask break & enrage system.
    /// </summary>
    public class RabbitSpiderAI : EnemyController
    {
        [Header("Health & Mask System")]
        [Tooltip("Hits taken before mask is knocked off.")]
        public int hitsToBreakMask = 2;

        [Tooltip("Is the mask currently broken?")]
        public bool isMaskBroken = false;

        [Tooltip("Prefab for detached mask debris.")]
        public GameObject maskDebrisPrefab;

        [Tooltip("Audio clip played when mask breaks.")]
        public AudioClip maskBreakSound;

        [Tooltip("Animator controller used while mask is intact.")]
        public RuntimeAnimatorController maskedController;

        [Tooltip("Animator controller used after mask breaks.")]
        public RuntimeAnimatorController unmaskedController;

        [Header("Detection Settings")]
        [Tooltip("Distance within which the spider spots the player.")]
        public float detectionRadius = 6.5f;

        [Tooltip("Max vertical difference to recognize player.")]
        public float verticalDetectLimit = 3.5f;

        [Header("Movement Speeds")]
        public float patrolSpeed = 1.8f;
        public float chaseSpeed = 3.8f;

        [Header("Jump Attack Settings")]
        [Tooltip("Distance to initiate the leap attack.")]
        public float jumpAttackRange = 3.5f;

        [Tooltip("Minimum distance to jump attack (won't jump if already on top of player).")]
        public float jumpMinRange = 1.2f;

        [Tooltip("Vertical takeoff speed for the jump attack.")]
        public float jumpVelocityY = 7.2f;

        [Tooltip("Horizontal forward speed while leaping.")]
        public float jumpVelocityX = 4.8f;

        [Tooltip("Cooldown in seconds between jump attacks.")]
        public float jumpCooldown = 2.5f;

        [Tooltip("Optional audio clip played when leaping.")]
        public AudioClip leapSound;

        [Header("Patrol & Idle Settings")]
        public float selfPatrolDistance = 4f;
        public bool checkLedges = true;
        [Tooltip("Minimum walking time before pausing to idle.")]
        public float minWalkTime = 2.5f;
        [Tooltip("Maximum walking time before pausing to idle.")]
        public float maxWalkTime = 5.0f;
        [Tooltip("Minimum pause duration.")]
        public float minIdlePause = 2.0f;
        [Tooltip("Maximum pause duration.")]
        public float maxIdlePause = 3.5f;

        [Header("Enraged / Aggressive Phase Settings")]
        public float enragedPatrolSpeed = 2.8f;
        public float enragedChaseSpeed = 5.6f;
        public float enragedJumpCooldown = 1.2f;
        public float enragedJumpVelocityY = 8.5f;
        public float enragedJumpVelocityX = 6.2f;
        public float enragedJumpAttackRange = 5.0f;
        public float enragedDetectionRadius = 9.5f;
        public float enragedVerticalDetectLimit = 5.0f;
        public Color enragedTint = new Color(1f, 0.88f, 0.88f, 1f);

        private float _spawnX;
        private int _patrolDirection = 1;
        private float _nextJumpTime = 0f;
        private bool _isLeaping = false;
        private PlayerController _cachedPlayer;
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private Health _health;
        private int _hitsTaken = 0;

        // Idle state tracking
        private bool _isIdling = false;
        private float _nextPatrolStateTime = 0f;

        protected override void Awake()
        {
            base.Awake();
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _health = GetComponent<Health>();
            _spawnX = transform.position.x;

            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
            }

            if (!isMaskBroken && maskedController != null && _animator != null)
            {
                _animator.runtimeAnimatorController = maskedController;
            }

            if (control != null)
            {
                control.maxSpeed = patrolSpeed;
            }
            _nextPatrolStateTime = Time.time + Random.Range(minWalkTime, maxWalkTime);
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void HandleHealthChanged(Health h)
        {
            if (h == null || !h.IsAlive) return;

            _hitsTaken = h.maxHP - h.CurrentHP;

            if (!isMaskBroken && _hitsTaken >= hitsToBreakMask)
            {
                BreakMask();
            }
            else if (!isMaskBroken)
            {
                // Mask hit feedback: spark and metallic impact sound
                if (_audio != null && ouch != null)
                {
                    _audio.pitch = 1.35f;
                    _audio.PlayOneShot(ouch);
                }
                Vector3 hitPos = transform.position + Vector3.up * 0.45f;
                ProjectileDebris.SpawnSpark(hitPos, new Color(1f, 0.9f, 0.5f), 8, 5f);
                StartCoroutine(DamageFlashRoutine());
            }
            else
            {
                // Hit in enraged phase
                StartCoroutine(DamageFlashRoutine());
            }
        }

        public void BreakMask()
        {
            if (isMaskBroken) return;
            isMaskBroken = true;

            // Visual: switch to unmasked animator controller
            if (unmaskedController != null && _animator != null)
            {
                _animator.runtimeAnimatorController = unmaskedController;
            }

            // Spawn detached mask debris popping off and spinning
            SpawnMaskDebris();

            // FX: dramatic sparks & expanding shockwave ring
            Vector3 headPos = transform.position + Vector3.up * 0.45f;
            ProjectileDebris.SpawnSpark(headPos, new Color(1f, 0.45f, 0.15f), 16, 7f);
            ExpandingRing.Spawn(headPos, new Color(1f, 0.7f, 0.4f, 0.85f), 1.6f, 0.22f);

            // Audio: mask shatter / scream
            if (_audio != null)
            {
                AudioClip soundToPlay = maskBreakSound != null ? maskBreakSound : ouch;
                if (soundToPlay != null)
                {
                    _audio.pitch = 0.82f;
                    _audio.PlayOneShot(soundToPlay);
                }
            }

            // Enrage AI combat stats
            patrolSpeed = enragedPatrolSpeed;
            chaseSpeed = enragedChaseSpeed;
            jumpCooldown = enragedJumpCooldown;
            jumpVelocityY = enragedJumpVelocityY;
            jumpVelocityX = enragedJumpVelocityX;
            jumpAttackRange = enragedJumpAttackRange;
            detectionRadius = enragedDetectionRadius;
            verticalDetectLimit = enragedVerticalDetectLimit;

            if (control != null)
            {
                control.maxSpeed = chaseSpeed;
            }

            // Cancel any idle pauses immediately
            _isIdling = false;
            _nextJumpTime = Time.time;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = enragedTint;
            }

            StartCoroutine(DamageFlashRoutine());
        }

        private void SpawnMaskDebris()
        {
            GameObject debrisObj = null;
            if (maskDebrisPrefab != null)
            {
                debrisObj = Instantiate(maskDebrisPrefab, transform.position + Vector3.up * 0.45f, Quaternion.identity);
            }
            else
            {
                debrisObj = new GameObject("MaskDebris");
                debrisObj.transform.position = transform.position + Vector3.up * 0.45f;
                var sr = debrisObj.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 10;
                var rb = debrisObj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 1.5f;
                debrisObj.AddComponent<MaskDebris>();
            }

            var debris = debrisObj.GetComponent<MaskDebris>();
            if (debris != null)
            {
                float dirX = _spriteRenderer != null && _spriteRenderer.flipX ? -1f : 1f;
                Vector2 vel = new Vector2(Random.Range(0.8f, 2.5f) * dirX, Random.Range(4.5f, 6.5f));
                float torque = Random.Range(200f, 400f) * -dirX;
                debris.Launch(vel, torque);
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            if (_spriteRenderer == null) yield break;
            Color origColor = isMaskBroken ? enragedTint : Color.white;
            _spriteRenderer.color = new Color(2f, 2f, 2f, 1f);
            yield return new WaitForSeconds(0.08f);
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = origColor;
            }
        }

        protected override void Update()
        {
            if (control == null || !control.enabled || (_health != null && !_health.IsAlive)) return;

            // Find player reference if needed
            if (_cachedPlayer == null)
            {
                var model = Simulation.GetModel<PlatformerModel>();
                if (model != null) _cachedPlayer = model.player;
                if (_cachedPlayer == null) _cachedPlayer = FindAnyObjectByType<PlayerController>();
            }

            // Check if player is alive and active
            bool playerTargetable = _cachedPlayer != null && 
                                    _cachedPlayer.gameObject.activeInHierarchy && 
                                    _cachedPlayer.health != null && 
                                    _cachedPlayer.health.IsAlive;

            Vector2 myPos = transform.position;
            bool seesPlayer = false;
            float distToPlayer = 999f;
            float dirToPlayer = 0f;

            if (playerTargetable)
            {
                Vector2 playerPos = _cachedPlayer.transform.position;
                float diffX = playerPos.x - myPos.x;
                float diffY = Mathf.Abs(playerPos.y - myPos.y);
                distToPlayer = Mathf.Abs(diffX);
                dirToPlayer = Mathf.Sign(diffX);

                if (distToPlayer <= detectionRadius && diffY <= verticalDetectLimit)
                {
                    seesPlayer = true;
                }
            }

            // Reset leap state once grounded again
            if (_isLeaping && control.IsGrounded)
            {
                _isLeaping = false;
            }

            if (seesPlayer)
            {
                // Cancel any idle immediately when player is sighted
                _isIdling = false;
                HandleCombat(distToPlayer, dirToPlayer);
            }
            else
            {
                // Normal Patrol & Idle Behavior
                HandlePatrol();
            }
        }

        private void HandleCombat(float distToPlayer, float dirToPlayer)
        {
            // Can initiate jump attack?
            if (!_isLeaping && control.IsGrounded && Time.time >= _nextJumpTime)
            {
                if (distToPlayer <= jumpAttackRange && distToPlayer >= jumpMinRange)
                {
                    ExecuteJumpAttack(dirToPlayer);
                    return;
                }
            }

            // While airborne from a leap, maintain forward momentum
            if (_isLeaping && !control.IsGrounded)
            {
                control.move.x = dirToPlayer;
                return;
            }

            // Standard ground chase
            control.maxSpeed = chaseSpeed;
            control.move.x = dirToPlayer;
        }

        private void ExecuteJumpAttack(float direction)
        {
            _isLeaping = true;
            _nextJumpTime = Time.time + jumpCooldown;

            // Apply takeoff velocities
            control.jumpTakeOffSpeed = jumpVelocityY;
            control.jump = true;
            control.maxSpeed = jumpVelocityX;
            control.move.x = direction;

            if (leapSound != null && _audio != null)
            {
                _audio.pitch = isMaskBroken ? 1.2f : 1.0f;
                _audio.PlayOneShot(leapSound);
            }
        }

        private void HandlePatrol()
        {
            if (_isLeaping) return;

            // Handle patrol paths if assigned
            if (path != null)
            {
                control.maxSpeed = patrolSpeed;
                if (mover == null) mover = path.CreateMover(control.maxSpeed * 0.5f);
                control.move.x = Mathf.Clamp(mover.Position.x - transform.position.x, -1f, 1f);
                return;
            }

            // In enraged mode, don't pause for playful idles
            if (!isMaskBroken)
            {
                // Patrol State Machine (Walk <-> Idle/Special Idle)
                if (_isIdling)
                {
                    control.move.x = 0;
                    if (Time.time >= _nextPatrolStateTime)
                    {
                        // Resume walking
                        _isIdling = false;
                        if (Random.value < 0.4f)
                        {
                            _patrolDirection = -_patrolDirection;
                        }
                        _nextPatrolStateTime = Time.time + Random.Range(minWalkTime, maxWalkTime);
                    }
                    return;
                }

                // Currently walking
                if (Time.time >= _nextPatrolStateTime && control.IsGrounded)
                {
                    // Enter idle state
                    _isIdling = true;
                    control.move.x = 0;
                    float idleDuration = Random.Range(minIdlePause, maxIdlePause);
                    _nextPatrolStateTime = Time.time + idleDuration;

                    // Chance to trigger a special idle animation
                    if (_animator != null && Random.value < 0.75f)
                    {
                        if (Random.value < 0.5f)
                        {
                            _animator.SetTrigger("specialIdleLegPlay");
                        }
                        else
                        {
                            _animator.SetTrigger("specialIdleFootTap");
                        }
                    }
                    return;
                }
            }

            // Autonomous self-patrol around spawn point
            control.maxSpeed = patrolSpeed;
            float currentX = transform.position.x;
            if (_patrolDirection > 0 && currentX >= _spawnX + selfPatrolDistance)
            {
                _patrolDirection = -1;
            }
            else if (_patrolDirection < 0 && currentX <= _spawnX - selfPatrolDistance)
            {
                _patrolDirection = 1;
            }

            // Ledge detection
            if (checkLedges && control.IsGrounded)
            {
                Vector2 rayOrigin = (Vector2)transform.position + new Vector2(_patrolDirection * 0.65f, 0f);
                RaycastHit2D groundHit = Physics2D.Raycast(rayOrigin, Vector2.down, 1.2f);
                if (groundHit.collider == null)
                {
                    _patrolDirection = -_patrolDirection;
                }
            }

            control.move.x = _patrolDirection;
        }

        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            base.OnCollisionEnter2D(collision);
        }
    }
}
