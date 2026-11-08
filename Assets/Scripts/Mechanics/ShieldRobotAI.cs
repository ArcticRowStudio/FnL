using System.Collections;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Model;
using Platformer.View;
using UnityEngine;
using static Platformer.Core.Simulation;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Bulwark Shield Robot AI Controller.
    /// A biomechanical heavy shield crawler that serves as the vanguard shield wall.
    /// Features:
    /// - Blast shield: stops every overwatch round, and every melee hit from the front.
    /// - Slow to turn: it faces a player behind it only after turnDelay, the opening a warp buys.
    /// - Stomp stun: a stomp stuns it, and while stunned melee lands from either side.
    /// - Shield Bash Counter: Punishes players who linger in front too long with a forceful forward shield thrust and knockback.
    /// - Heavy hydraulic and metallic impact visual & sound feedback.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class ShieldRobotAI : EnemyController
    {
        public enum ShieldState
        {
            Patrol,
            Advance,
            Guarding,
            ShieldBash,
            Stunned
        }

        [Header("State & Stance")]
        public ShieldState currentState = ShieldState.Patrol;
        public bool isFacingLeft = true;
        public float moveSpeed = 1.2f;
        public float detectionRadius = 7.5f;
        public float guardDistance = 4.5f;
        [Tooltip("Seconds the player has to stay behind the robot before it turns around.")]
        public float turnDelay = 0.8f;
        [Tooltip("How far the floor may drop past the front edge before the robot refuses to walk on.")]
        public float ledgeCheckDepth = 0.6f;

        [Header("Shield Bash Counter")]
        public float bashTriggerDistance = 1.8f;
        public float bashWindup = 0.4f;
        public float bashLungeSpeed = 6.0f;
        public float bashDuration = 0.3f;
        public float bashCooldown = 3.0f;
        public int bashDamage = 1;

        [Header("Stun")]
        [Tooltip("Height above the feet where the stun sparks appear when there is no sensor pivot.")]
        public float topSensorHeight = 0.85f;
        public float stunDuration = 2.0f;

        [Header("Audio")]
        public AudioClip blockSound;
        public AudioClip bashSound;
        public AudioClip hydraulicSound;

        [Header("Visual References")]
        public SpriteRenderer robotRenderer;

        [Header("Modular Articulated Parts")]
        public Transform visualRoot;
        public Transform bodyTransform;
        public Transform sensorPivot;
        public Transform shieldPivot;
        public SpriteRenderer bodyRenderer;
        public SpriteRenderer sensorRenderer;
        public SpriteRenderer shieldRenderer;

        // Clearance kept between the body and whatever it walks into.
        private const float Skin = 0.02f;
        // How far past the shield face the bash connects.
        private const float BashReach = 0.5f;
        private static readonly Color BashWindupTint = new Color(1f, 0.9f, 0.7f, 1f);
        private static readonly Color StunTint = new Color(0.6f, 0.8f, 1f, 0.7f);

        private Health _health;
        private Rigidbody2D _rb;
        private PlayerController _player;
        private float _nextBashTime = 0f;
        private float _playerInMeleeTimer = 0f;
        private float _behindTimer = 0f;
        private bool _isPerformingAction = false;
        private Coroutine _bashRoutine;
        private Coroutine _stunRoutine;
        private Color _tint = Color.white;

        // Walking is swept against the level by hand: the body is kinematic, so nothing else
        // stops it at a wall or a drop.
        private float _moveX = 0f;
        private float _fallSpeed = 0f;
        private ContactFilter2D _terrainFilter;
        private readonly RaycastHit2D[] _castHits = new RaycastHit2D[8];
        private readonly Collider2D[] _bashHits = new Collider2D[8];

        private Vector3 _initialBodyPos;
        private Vector3 _initialShieldPos;
        private Vector3 _shieldRecoilOffset = Vector3.zero;
        private Vector3 _shieldBashOffset = Vector3.zero;
        private float _currentSensorAngle = 0f;

        protected override void Awake()
        {
            base.Awake();

            // Enter Play Mode Options keep domain and scene reload off, so these objects are
            // never recreated between sessions. Stopping play mid-action would otherwise leave
            // the flags below set, and this enemy would never act again in any later session.
            currentState = ShieldState.Patrol;
            _isPerformingAction = false;
            _bashRoutine = null;
            _stunRoutine = null;
            _moveX = 0f;
            _fallSpeed = 0f;
            _behindTimer = 0f;
            _playerInMeleeTimer = 0f;
            _nextBashTime = 0f;
            _player = null;
            _tint = Color.white;
            _health = GetComponent<Health>();
            if (_health == null) _health = gameObject.AddComponent<Health>();
            _health.maxHP = 4;
            _health.ResetHP();

            if (_health != null)
            {
                // Detach first: with scene reload off this component is reused between sessions,
                // so a plain += stacks another handler on every Play.
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.OnHealthChanged += HandleHealthChanged;
            }

            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.freezeRotation = true;

            _terrainFilter.useTriggers = false;
            _terrainFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));

            InitModularParts();

            // Turn off default AnimationController so ShieldRobot uses its custom AI kinematics
            if (control != null) control.enabled = false;
        }

        private void InitModularParts()
        {
            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            Transform searchParent = visualRoot != null ? visualRoot : transform;

            if (bodyTransform == null) bodyTransform = searchParent.Find("Body");
            if (sensorPivot == null) sensorPivot = searchParent.Find("SensorPivot");
            if (shieldPivot == null) shieldPivot = searchParent.Find("ShieldPivot");

            if (bodyTransform != null)
            {
                _initialBodyPos = bodyTransform.localPosition;
                if (bodyRenderer == null) bodyRenderer = bodyTransform.GetComponentInChildren<SpriteRenderer>();
            }

            if (shieldPivot != null)
            {
                _initialShieldPos = shieldPivot.localPosition;
                if (shieldRenderer == null) shieldRenderer = shieldPivot.GetComponentInChildren<SpriteRenderer>();
            }

            if (sensorPivot != null)
            {
                if (sensorRenderer == null) sensorRenderer = sensorPivot.GetComponentInChildren<SpriteRenderer>();
            }

            if (robotRenderer == null) robotRenderer = GetComponent<SpriteRenderer>();
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

            StartCoroutine(DamageFlashRoutine());
            if (_audio != null && ouch != null)
            {
                _audio.pitch = 0.9f;
                _audio.PlayOneShot(ouch);
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            Color flashColor = new Color(1f, 0.4f, 0.4f, 1f);
            SetRenderersColor(flashColor);
            yield return new WaitForSeconds(0.12f);
            SetRenderersColor(_tint);
        }

        private void SetTint(Color color)
        {
            _tint = color;
            SetRenderersColor(color);
        }

        private void SetRenderersColor(Color color)
        {
            if (robotRenderer != null) robotRenderer.color = color;
            if (bodyRenderer != null) bodyRenderer.color = color;
            if (sensorRenderer != null) sensorRenderer.color = color;
            if (shieldRenderer != null) shieldRenderer.color = color;
        }

        protected override void Update()
        {
            if (_health != null && !_health.IsAlive)
            {
                _moveX = 0f;
                return;
            }

            // Find player reference if missing
            if (_player == null)
            {
                var model = Simulation.GetModel<PlatformerModel>();
                if (model != null) _player = model.player;
                if (_player == null) _player = FindAnyObjectByType<PlayerController>();
            }

            UpdateArticulatedMotion();

            if (_isPerformingAction || currentState == ShieldState.Stunned) return;

            UpdateFacingAndTargeting();
        }

        private bool PlayerTargetable()
        {
            return _player != null && _player.gameObject.activeInHierarchy && _player.health != null && _player.health.IsAlive;
        }

        private void UpdateArticulatedMotion()
        {
            // 1. Suspension bobbing on crawler chassis
            if (bodyTransform != null)
            {
                float bobFreq = (currentState == ShieldState.Advance) ? 8.0f : 2.5f;
                float bobAmp = (currentState == ShieldState.Advance) ? 0.035f : 0.015f;
                float bob = Mathf.Sin(Time.time * bobFreq) * bobAmp;
                bodyTransform.localPosition = _initialBodyPos + new Vector3(0, bob, 0);
            }

            // 2. Sensor dynamic eye tracking & stun droop
            if (sensorPivot != null)
            {
                if (currentState == ShieldState.Stunned)
                {
                    // Droop limp downward when stunned
                    sensorPivot.localRotation = Quaternion.Slerp(sensorPivot.localRotation, Quaternion.Euler(0, 0, -40f), Time.deltaTime * 8f);
                }
                else if (PlayerTargetable())
                {
                    Vector3 targetPos = _player.transform.position + Vector3.up * 0.4f;
                    Vector3 diff = targetPos - sensorPivot.position;
                    float sign = isFacingLeft ? -1f : 1f;
                    float targetDeg = Mathf.Atan2(diff.y, diff.x * sign) * Mathf.Rad2Deg;
                    targetDeg = Mathf.Clamp(targetDeg, -25f, 35f);
                    _currentSensorAngle = Mathf.Lerp(_currentSensorAngle, targetDeg, Time.deltaTime * 6f);
                    sensorPivot.localRotation = Quaternion.Euler(0, 0, _currentSensorAngle);
                }
                else
                {
                    // Idle breathing tilt
                    float idleTilt = Mathf.Sin(Time.time * 1.8f) * 5f;
                    sensorPivot.localRotation = Quaternion.Euler(0, 0, idleTilt);
                }
            }

            // 3. Shield hydraulic positioning (spring damp return)
            if (shieldPivot != null)
            {
                _shieldRecoilOffset = Vector3.Lerp(_shieldRecoilOffset, Vector3.zero, Time.deltaTime * 10f);
                _shieldBashOffset = Vector3.Lerp(_shieldBashOffset, Vector3.zero, Time.deltaTime * 12f);

                Vector3 currentTarget = _initialShieldPos + _shieldRecoilOffset + _shieldBashOffset;
                shieldPivot.localPosition = currentTarget;
            }
        }

        private void UpdateFacingAndTargeting()
        {
            if (!PlayerTargetable())
            {
                EnterPatrol();
                return;
            }

            float diffX = _player.transform.position.x - transform.position.x;
            float absDistX = Mathf.Abs(diffX);
            float absDistY = Mathf.Abs(_player.transform.position.y - transform.position.y);

            if (absDistX > detectionRadius || absDistY > 3.5f)
            {
                EnterPatrol();
                return;
            }

            // Turn around only after the player has stayed behind for a while, so getting
            // behind the shield leaves a window to hit the unguarded back.
            bool playerBehind = absDistX > 0.3f && (diffX < 0f) != isFacingLeft;
            if (playerBehind)
            {
                _behindTimer += Time.deltaTime;
                if (_behindTimer >= turnDelay)
                {
                    TurnAround();
                    playerBehind = false;
                }
            }
            else
            {
                _behindTimer = 0f;
            }

            if (absDistX > guardDistance)
            {
                // Advance with the shield forward, but never backwards into the player.
                currentState = ShieldState.Advance;
                _playerInMeleeTimer = 0f;
                _moveX = playerBehind ? 0f : Mathf.Sign(diffX) * moveSpeed;
                return;
            }

            // In guard range: Lock down shield
            _moveX = 0f;
            if (currentState != ShieldState.Guarding)
            {
                EnterGuard();
            }

            // Check for Shield Bash trigger if player lingers in front
            if (!playerBehind && absDistX <= bashTriggerDistance)
            {
                _playerInMeleeTimer += Time.deltaTime;
                if (_playerInMeleeTimer >= 1.2f && Time.time >= _nextBashTime)
                {
                    _bashRoutine = StartCoroutine(ExecuteShieldBashRoutine());
                }
            }
            else
            {
                _playerInMeleeTimer = 0f;
            }
        }

        private void EnterPatrol()
        {
            currentState = ShieldState.Patrol;
            _moveX = 0f;
            _playerInMeleeTimer = 0f;
            _behindTimer = 0f;
        }

        private void TurnAround()
        {
            isFacingLeft = !isFacingLeft;
            UpdateSpriteOrientation();
            _behindTimer = 0f;
            _playerInMeleeTimer = 0f;
            _shieldRecoilOffset = new Vector3(0.12f, -0.03f, 0);

            if (_audio != null && hydraulicSound != null)
            {
                _audio.pitch = 0.9f;
                _audio.PlayOneShot(hydraulicSound);
            }
        }

        private void UpdateSpriteOrientation()
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = new Vector3(isFacingLeft ? 1f : -1f, 1f, 1f);
            }
            else if (robotRenderer != null)
            {
                robotRenderer.flipX = !isFacingLeft;
            }
        }

        private void EnterGuard()
        {
            currentState = ShieldState.Guarding;
            // Shield ground lockdown hydraulic stamp
            _shieldRecoilOffset = new Vector3(-0.08f, -0.05f, 0);

            Vector3 groundPos = transform.position + (isFacingLeft ? Vector3.left : Vector3.right) * 0.6f;
            ExpandingRing.Spawn(groundPos, new Color(0.8f, 0.85f, 0.95f, 0.8f), 1.2f, 0.15f);
            ProjectileDebris.SpawnSpark(groundPos, new Color(0.9f, 0.8f, 0.5f), 8, 4f);

            if (_audio != null && hydraulicSound != null)
            {
                _audio.pitch = 1.1f;
                _audio.PlayOneShot(hydraulicSound);
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null || _collider == null) return;

            float dt = Time.fixedDeltaTime;
            Vector2 delta = Vector2.zero;

            bool grounded = IsGrounded();
            if (grounded)
            {
                _fallSpeed = 0f;
            }
            else
            {
                _fallSpeed += Mathf.Abs(Physics2D.gravity.y) * _rb.gravityScale * dt;
                delta.y = -SweepDistance(Vector2.down, _fallSpeed * dt);
            }

            if (grounded && _moveX != 0f)
            {
                Vector2 dir = _moveX > 0f ? Vector2.right : Vector2.left;
                if (HasGroundAhead(dir.x))
                {
                    delta.x = dir.x * SweepDistance(dir, Mathf.Abs(_moveX) * dt);
                }
            }

            if (delta != Vector2.zero)
            {
                _rb.MovePosition(_rb.position + delta);
            }
        }

        private bool IsGrounded()
        {
            int count = _rb.Cast(Vector2.down, _terrainFilter, _castHits, 0.05f);
            for (int i = 0; i < count; i++)
            {
                if (_castHits[i].normal.y > 0.65f) return true;
            }
            return false;
        }

        // How far the body can travel along dir, up to distance, before it touches something.
        private float SweepDistance(Vector2 dir, float distance)
        {
            float allowed = distance;
            float feetY = _collider.bounds.min.y;
            int count = _rb.Cast(dir, _terrainFilter, _castHits, distance + Skin);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _castHits[i];
                // Only surfaces facing the motion block it, which skips the floor under a sideways move.
                if (Vector2.Dot(hit.normal, dir) > -0.5f) continue;
                // A sideways hit right at the feet is a seam between floor tiles, not a wall.
                if (dir.y == 0f && hit.point.y < feetY + 0.05f) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - Skin));
            }
            return allowed;
        }

        private bool HasGroundAhead(float dirX)
        {
            Bounds b = _collider.bounds;
            Vector2 origin = new Vector2(dirX > 0f ? b.max.x + 0.1f : b.min.x - 0.1f, b.min.y + 0.1f);
            return Physics2D.Raycast(origin, Vector2.down, _terrainFilter, _castHits, ledgeCheckDepth + 0.1f) > 0;
        }

        private IEnumerator ExecuteShieldBashRoutine()
        {
            _isPerformingAction = true;
            currentState = ShieldState.ShieldBash;
            _playerInMeleeTimer = 0f;
            _nextBashTime = Time.time + bashCooldown;
            _moveX = 0f;

            // Windup: pull back shield
            float dir = isFacingLeft ? -1f : 1f;
            SetTint(BashWindupTint);
            _shieldBashOffset = new Vector3(0.18f, 0.04f, 0);

            yield return new WaitForSeconds(bashWindup);

            // Audio & Thrust forward
            _shieldBashOffset = new Vector3(-0.55f, 0, 0);

            if (_audio != null && bashSound != null)
            {
                _audio.pitch = 1.0f;
                _audio.PlayOneShot(bashSound);
            }

            CameraZoomController.TriggerShake(0.2f, 0.1f);
            Vector3 bashPos = transform.position + Vector3.right * (dir * 0.8f);
            ExpandingRing.Spawn(bashPos, new Color(1f, 0.5f, 0.2f, 0.85f), 1.8f, 0.2f);
            ProjectileDebris.SpawnSpark(bashPos, new Color(1f, 0.6f, 0.2f), 14, 7f);

            // Lunge forward; FixedUpdate moves the body so walls and ledges still stop it.
            float elapsed = 0f;
            while (elapsed < bashDuration)
            {
                elapsed += Time.deltaTime;
                _moveX = dir * bashLungeSpeed;
                if (TryBashHit(dir)) break;
                yield return null;
            }

            _moveX = 0f;
            SetTint(Color.white);
            yield return new WaitForSeconds(0.3f);

            _isPerformingAction = false;
            currentState = ShieldState.Guarding;
            _bashRoutine = null;
        }

        private bool TryBashHit(float dir)
        {
            Bounds b = _collider.bounds;
            Vector2 center = new Vector2(dir > 0f ? b.max.x + BashReach * 0.5f : b.min.x - BashReach * 0.5f, b.center.y);
            Vector2 size = new Vector2(BashReach, b.size.y);

            int count = Physics2D.OverlapBox(center, size, 0f, ContactFilter2D.noFilter, _bashHits);
            for (int i = 0; i < count; i++)
            {
                var player = _bashHits[i].GetComponentInParent<PlayerController>();
                if (player == null || player.health == null || !player.health.IsAlive) continue;

                if (player.isParrying)
                {
                    player.OnParrySuccess(gameObject);
                    return true;
                }
                else if (player.isGuarding)
                {
                    player.OnGuardBlock(player.transform.position);
                    return true;
                }

                // Warping through the lunge doesn't end it.
                if (player.isInvincible) return false;

                player.TakeDamage(bashDamage, new Vector2(dir * 5.5f, 4.0f));
                return true;
            }
            return false;
        }

        private void CancelBash()
        {
            if (_bashRoutine != null)
            {
                StopCoroutine(_bashRoutine);
                _bashRoutine = null;
            }
            _isPerformingAction = false;
            _moveX = 0f;
            _shieldBashOffset = Vector3.zero;
        }

        public void TriggerStun()
        {
            if (currentState == ShieldState.Stunned) return;
            if (_health != null && !_health.IsAlive) return;

            // A stun lands mid-bash too, and must stop the lunge rather than wait for it to end.
            CancelBash();
            _stunRoutine = StartCoroutine(StunRoutine());
        }

        private IEnumerator StunRoutine()
        {
            currentState = ShieldState.Stunned;
            _moveX = 0f;
            _playerInMeleeTimer = 0f;
            _behindTimer = 0f;

            // Stun VFX: electric sparks around sensor
            Vector3 sensorPos = sensorPivot != null ? sensorPivot.position : transform.position + Vector3.up * topSensorHeight;
            ProjectileDebris.SpawnSpark(sensorPos, new Color(0.2f, 0.9f, 1f), 16, 6f);
            ExpandingRing.Spawn(sensorPos, new Color(0.4f, 0.9f, 1f, 0.9f), 1.6f, 0.25f);

            SetTint(StunTint);

            yield return new WaitForSeconds(stunDuration);

            SetTint(Color.white);
            currentState = ShieldState.Guarding;
            _stunRoutine = null;
        }

        public override void OnStomped()
        {
            TriggerStun();
        }

        public override bool CanTakeDamage(Vector2 hitPosition, Vector2 attackerPosition)
        {
            if (currentState == ShieldState.Stunned)
            {
                return true;
            }

            // The shield covers the front in every stance; only the back is open.
            float attackDir = attackerPosition.x - transform.position.x;
            bool attackedFromFront = isFacingLeft ? attackDir < 0f : attackDir > 0f;
            return !attackedFromFront;
        }

        public override bool BlocksRangedHit(Vector2 hitPosition)
        {
            // Overwatch rounds never get through; the robot is beaten up close.
            return true;
        }

        public override void OnBlockedHit(Vector2 hitPosition, Vector2 attackerPosition)
        {
            // Visual feedback: ricochet sparks
            ProjectileDebris.SpawnSpark(hitPosition, new Color(0.4f, 0.85f, 1f), 12, 7f);
            ExpandingRing.Spawn(hitPosition, new Color(0.6f, 0.85f, 1f, 0.75f), 1.0f, 0.12f);

            // Shield physical kickback into hydraulic suspension
            _shieldRecoilOffset += new Vector3(0.25f, 0, 0);

            // Camera recoil
            CameraZoomController.TriggerShake(0.14f, 0.06f);

            // Audio: metallic clink
            if (_audio != null)
            {
                AudioClip snd = blockSound != null ? blockSound : ouch;
                if (snd != null)
                {
                    float origPitch = _audio.pitch;
                    _audio.pitch = 2.2f;
                    _audio.PlayOneShot(snd, 1.0f);
                    _audio.pitch = origPitch;
                }
            }

            StartCoroutine(ShieldRecoilRoutine());
        }

        private IEnumerator ShieldRecoilRoutine()
        {
            SetRenderersColor(new Color(1.5f, 1.5f, 1.5f, 1f));
            yield return new WaitForSeconds(0.06f);
            SetRenderersColor(_tint);
        }

    }
}
