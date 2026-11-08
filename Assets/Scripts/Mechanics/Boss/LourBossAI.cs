using System;
using System.Collections;
using System.Collections.Generic;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Model;
using Platformer.View;
using UnityEngine;
using static Platformer.Core.Simulation;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// Lour - Spec-Ops Recon Sniper Mid-Boss AI.
    /// Equipped with high-caliber anti-material sniper rifle, tactical dash,
    /// evasive backstep grenade throws, and dynamic platform relocation.
    /// 
    /// Combat Design:
    /// - Tactical Distance Keeping: Maintains optimal sniper engagement range (7 - 12m).
    /// - Active Real-Time Aiming: Faces player and pitches sniper rifle directly towards target.
    /// - Red Laser Telegraph: Precision laser sight tracks player with lead before firing.
    /// - High-Velocity Sniper Round: Devastating direct bullet, parriable by the player.
    /// - Evasive Backstep Grenade: Leaps backwards and lobs cluster grenade when pressed.
    /// - Tactical Dash: Low-friction slide to disengage or cross arenas.
    /// - Phase 2 Overclock (< 50% HP): Red goggle flare, double-burst snipes, twin grenades.
    /// - Defeat / Tactical Retreat: Smokescreen fadeout for reappearance on future flagship galleon.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(Collider2D))]
    public class LourBossAI : EnemyController, IPlayerHittable
    {
        public enum BossState
        {
            Intro,
            Idle,
            Walk,
            Sprint,
            Dash,
            Aiming,
            Firing,
            BackstepGrenade,
            Jump,
            Stunned,
            Defeated
        }

        [Header("Boss State")]
        public BossState currentState = BossState.Intro;
        public bool isPhase2 = false;
        public bool isFacingLeft = true;

        [Header("Sprites - Animation Frames")]
        public Sprite[] idleFrames;
        public Sprite[] walkFrames;
        public Sprite[] runFrames;
        public Sprite[] dashFrames;
        public Sprite[] jumpFrames;
        public Sprite[] fireFrames;
        public Sprite[] throwFrames;
        public Sprite grenadeSprite;

        [Header("Combat & Ranges")]
        public float detectionRadius = 22f;
        public float minEngageDistance = 6.5f;
        public float maxEngageDistance = 12.5f;
        public float walkSpeed = 3.2f;
        public float sprintSpeed = 6.5f;
        public float dashSpeed = 15f;
        public float dashDuration = 0.35f;

        [Header("Sniper Parameters")]
        public float aimDurationPhase1 = 0.95f;
        public float aimDurationPhase2 = 0.55f;
        public float sniperBulletSpeed = 32f;
        public int sniperDamage = 1;
        public float sniperCooldown = 2.8f;
        public Transform muzzlePoint;

        [Header("Grenade Parameters")]
        public float grenadeCooldown = 4.2f;
        public float grenadeThrowSpeed = 8.5f;
        public int grenadeClusterCount = 5;

        [Header("Audio Clips")]
        public AudioClip shootSound;
        public AudioClip aimBeepSound;
        public AudioClip grenadeThrowSound;
        public AudioClip dashSound;
        public AudioClip hurtSound;
        public AudioClip retreatSound;

        [Header("VFX & Visuals")]
        public LineRenderer aimLaser;
        public Color laserAimColor = new Color(1f, 0.1f, 0.1f, 0.65f);
        public Color laserLockColor = new Color(1f, 0.2f, 0.2f, 1f);

        // Internal references
        private Health _health;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Collider2D _col;
        private Transform _playerTransform;
        private BossHealthBarUI _healthBar;
        private TrailRenderer _dashTrail;

        // Combat timers
        private float _sniperTimer = 1.0f;
        private float _grenadeTimer = 2.5f;
        private float _animTimer = 0f;
        private int _currentFrame = 0;
        private bool _isActionLocked = false;
        private bool _isInvincible = false;

        protected override void Awake()
        {
            base.Awake();
            _health = GetComponent<Health>();
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            _col = GetComponent<Collider2D>();

            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody2D>();
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.freezeRotation = true;
                _rb.gravityScale = 2.5f;
            }

            // Procedural Laser LineRenderer setup
            if (aimLaser == null)
            {
                GameObject laserObj = new GameObject("AimLaser");
                laserObj.transform.SetParent(transform);
                laserObj.transform.localPosition = Vector3.zero;
                aimLaser = laserObj.AddComponent<LineRenderer>();
                aimLaser.startWidth = 0.05f;
                aimLaser.endWidth = 0.02f;
                aimLaser.material = new Material(Shader.Find("Sprites/Default"));
                aimLaser.startColor = laserAimColor;
                aimLaser.endColor = laserAimColor;
                aimLaser.positionCount = 2;
                aimLaser.enabled = false;
            }

            // Procedural Dash Trail setup
            _dashTrail = gameObject.AddComponent<TrailRenderer>();
            _dashTrail.time = 0.18f;
            _dashTrail.startWidth = 1.2f;
            _dashTrail.endWidth = 0.1f;
            _dashTrail.material = new Material(Shader.Find("Sprites/Default"));
            _dashTrail.startColor = new Color(0.9f, 0.2f, 0.2f, 0.6f);
            _dashTrail.endColor = new Color(0.9f, 0.2f, 0.2f, 0.0f);
            _dashTrail.enabled = false;

            // Muzzle point offset aligned to rifle barrel
            if (muzzlePoint == null)
            {
                GameObject muz = new GameObject("MuzzlePoint");
                muz.transform.SetParent(transform);
                muz.transform.localPosition = new Vector3(-0.72f, 1.32f, 0f);
                muzzlePoint = muz.transform;
            }

            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
            }
        }

        private void Start()
        {
            var model = Simulation.GetModel<PlatformerModel>();
            if (model != null && model.player != null)
            {
                _playerTransform = model.player.transform;
            }

            // Initialize Boss Health Bar
            GameObject barObj = new GameObject("Lour_HealthBarUI");
            _healthBar = barObj.AddComponent<BossHealthBarUI>();
            _healthBar.Bind(_health, () => isPhase2,
                "LOUR // SPEC-OPS RECON SNIPER",
                "PHASE 1: TACTICAL RECON",
                "PHASE 2: OVERCLOCKED ACCELERATION [CRITICAL]");
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnHealthChanged -= HandleHealthChanged;
            if (_healthBar != null) Destroy(_healthBar.gameObject);
        }

        private void HandleHealthChanged(Health h)
        {
            if (!isPhase2 && h.CurrentHP <= h.maxHP * 0.5f)
            {
                isPhase2 = true;
                StartCoroutine(PhaseTransitionRoutine());
            }

            if (!h.IsAlive && currentState != BossState.Defeated)
            {
                StartCoroutine(DefeatRoutine());
            }
        }

        private IEnumerator PhaseTransitionRoutine()
        {
            _isActionLocked = true;
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            
            // Dramatic pause with red danger pulse
            for (int i = 0; i < 4; i++)
            {
                _sr.color = Color.red;
                yield return new WaitForSeconds(0.08f);
                _sr.color = Color.white;
                yield return new WaitForSeconds(0.08f);
            }
            
            _sr.color = new Color(1.0f, 0.85f, 0.85f);
            _isActionLocked = false;
        }

        protected override void Update()
        {
            if (currentState == BossState.Defeated) return;

            // Find player fallback
            if (_playerTransform == null)
            {
                var model = Simulation.GetModel<PlatformerModel>();
                if (model != null && model.player != null) _playerTransform = model.player.transform;
                return;
            }

            float distToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
            float xDiff = _playerTransform.position.x - transform.position.x;

            // Cooldowns
            _sniperTimer -= Time.deltaTime;
            _grenadeTimer -= Time.deltaTime;

            if (_isActionLocked)
            {
                UpdateAnimation();
                return;
            }

            // Facing logic: Face player in neutral / roaming states
            if (currentState != BossState.Dash && currentState != BossState.BackstepGrenade)
            {
                isFacingLeft = (xDiff < 0);
                UpdateFacing();
            }

            // AI State Decision Tree
            if (distToPlayer > detectionRadius)
            {
                currentState = BossState.Idle;
                _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            }
            else
            {
                // Defensive backstep grenade if player gets too close
                if (distToPlayer < 3.8f && _grenadeTimer <= 0f)
                {
                    StartCoroutine(BackstepGrenadeRoutine());
                    return;
                }

                // Defensive dash if player presses even closer
                if (distToPlayer < 2.5f)
                {
                    StartCoroutine(TacticalDashRoutine(isFacingLeft ? 1f : -1f));
                    return;
                }

                // Sniper shot if ready
                if (_sniperTimer <= 0f && distToPlayer >= minEngageDistance * 0.7f)
                {
                    StartCoroutine(SniperShotRoutine());
                    return;
                }

                // Distance management: Walk or Sprint
                if (distToPlayer < minEngageDistance)
                {
                    // Move away from player
                    float retreatDir = isFacingLeft ? 1f : -1f;
                    _rb.linearVelocity = new Vector2(retreatDir * walkSpeed, _rb.linearVelocity.y);
                    currentState = BossState.Walk;
                }
                else if (distToPlayer > maxEngageDistance)
                {
                    // Sprint towards player
                    float advanceDir = isFacingLeft ? -1f : 1f;
                    _rb.linearVelocity = new Vector2(advanceDir * (isPhase2 ? sprintSpeed * 1.25f : sprintSpeed), _rb.linearVelocity.y);
                    currentState = BossState.Sprint;
                }
                else
                {
                    // In sweet spot: Idle/Alert
                    _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
                    currentState = BossState.Idle;
                }
            }

            UpdateAnimation();
        }

        private void UpdateFacing()
        {
            // The raw sprites face RIGHT. So when isFacingLeft is true, flipX must be true!
            _sr.flipX = isFacingLeft;
            
            // Adjust muzzle point local position
            if (muzzlePoint != null)
            {
                float xOffset = isFacingLeft ? -0.72f : 0.72f;
                muzzlePoint.localPosition = new Vector3(xOffset, 1.32f, 0f);
            }
        }

        #region Actions & Coroutines

        private IEnumerator SniperShotRoutine()
        {
            _isActionLocked = true;
            currentState = BossState.Aiming;
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);

            float aimDuration = isPhase2 ? aimDurationPhase2 : aimDurationPhase1;
            float elapsed = 0f;

            if (aimLaser != null)
            {
                aimLaser.enabled = true;
                aimLaser.startColor = laserAimColor;
                aimLaser.endColor = laserAimColor;
            }

            if (aimBeepSound != null && _audio != null) _audio.PlayOneShot(aimBeepSound, 0.6f);

            Vector3 lockedTarget = _playerTransform.position + Vector3.up * 0.45f;

            // Tracking phase: Actively turn and pitch rifle directly at player
            while (elapsed < aimDuration)
            {
                elapsed += Time.deltaTime;
                bool isLockPhase = (elapsed >= aimDuration - 0.16f);

                // Continuously track player's horizontal position until the final lock-on tick
                if (!isLockPhase && _playerTransform != null)
                {
                    float currentXDiff = _playerTransform.position.x - transform.position.x;
                    isFacingLeft = (currentXDiff < 0);
                    UpdateFacing();
                    lockedTarget = _playerTransform.position + Vector3.up * 0.45f;
                }

                // Calculate aim pitch angle towards player
                Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 1.32f;
                Vector3 target = isLockPhase ? lockedTarget : (_playerTransform.position + Vector3.up * 0.45f);
                Vector2 aimDir = (target - origin).normalized;

                float pitchAngle = Mathf.Atan2(aimDir.y, Mathf.Abs(aimDir.x)) * Mathf.Rad2Deg;
                pitchAngle = Mathf.Clamp(pitchAngle, -32f, 28f);

                // Smoothly pitch character transform towards player
                transform.rotation = Quaternion.Euler(0, 0, isFacingLeft ? -pitchAngle : pitchAngle);

                // Flash lock-on color right before firing
                if (isLockPhase && aimLaser != null)
                {
                    aimLaser.startColor = laserLockColor;
                    aimLaser.endColor = laserLockColor;
                }

                if (aimLaser != null)
                {
                    origin = muzzlePoint != null ? muzzlePoint.position : transform.position;
                    aimLaser.SetPosition(0, origin);
                    aimLaser.SetPosition(1, target);
                }

                yield return null;
            }

            if (aimLaser != null) aimLaser.enabled = false;

            // Fire Frame
            currentState = BossState.Firing;
            _currentFrame = 1; // Muzzle flash frame
            if (fireFrames != null && fireFrames.Length > 1) _sr.sprite = fireFrames[1];

            // Spawn projectile traveling straight towards locked target
            Vector3 fireOrigin = muzzlePoint != null ? muzzlePoint.position : transform.position;
            Vector2 finalDir = (lockedTarget - fireOrigin).normalized;
            SpawnSniperBullet(finalDir);

            if (shootSound != null && _audio != null) _audio.PlayOneShot(shootSound, 0.95f);

            // Recoil kickback
            float kickDir = isFacingLeft ? 3.5f : -3.5f;
            _rb.linearVelocity = new Vector2(kickDir, 1.5f);

            yield return new WaitForSeconds(0.22f);

            // Phase 2 Double-Shot chance
            if (isPhase2 && UnityEngine.Random.value < 0.65f)
            {
                yield return new WaitForSeconds(0.18f);
                fireOrigin = muzzlePoint != null ? muzzlePoint.position : transform.position;
                if (_playerTransform != null) lockedTarget = _playerTransform.position + Vector3.up * 0.45f;
                finalDir = (lockedTarget - fireOrigin).normalized;
                SpawnSniperBullet(finalDir);
                if (shootSound != null && _audio != null) _audio.PlayOneShot(shootSound, 0.95f);
                _rb.linearVelocity = new Vector2(kickDir * 0.8f, 1f);
                yield return new WaitForSeconds(0.2f);
            }

            // Smoothly reset tilt rotation back to vertical
            float resetT = 0f;
            Quaternion startRot = transform.rotation;
            while (resetT < 0.12f)
            {
                resetT += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, Quaternion.identity, resetT / 0.12f);
                yield return null;
            }
            transform.rotation = Quaternion.identity;

            _sniperTimer = isPhase2 ? sniperCooldown * 0.6f : sniperCooldown;
            _isActionLocked = false;
        }

        private void SpawnSniperBullet(Vector2 dir)
        {
            Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position;
            if (dir.sqrMagnitude < 0.001f) dir = isFacingLeft ? Vector2.left : Vector2.right;

            GameObject bulletObj = new GameObject("Lour_SniperBullet");
            bulletObj.transform.position = origin;

            var proj = bulletObj.AddComponent<BossProjectile>();
            proj.Init(dir, sniperBulletSpeed, new Color(1.0f, 0.25f, 0.15f), parriable: true, gravity: false);
            proj.damage = sniperDamage;

            // Visual bullet trail
            var trail = bulletObj.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.startWidth = 0.22f;
                trail.endWidth = 0.04f;
                trail.time = 0.2f;
                trail.startColor = new Color(1f, 0.85f, 0.3f, 0.9f);
                trail.endColor = new Color(1f, 0.1f, 0.0f, 0.0f);
            }
        }

        private IEnumerator BackstepGrenadeRoutine()
        {
            _isActionLocked = true;
            currentState = BossState.BackstepGrenade;

            // Frame 1: Prep
            _currentFrame = 0;
            if (throwFrames != null && throwFrames.Length > 0) _sr.sprite = throwFrames[0];
            yield return new WaitForSeconds(0.1f);

            // Frame 2: Airborne backward leap
            float leapDir = isFacingLeft ? 1f : -1f; // leap backwards
            _rb.linearVelocity = new Vector2(leapDir * 8.5f, 6.5f);
            _currentFrame = 1;
            if (throwFrames != null && throwFrames.Length > 1) _sr.sprite = throwFrames[1];

            yield return new WaitForSeconds(0.15f);

            // Frame 3: Hurling grenade
            _currentFrame = 2;
            if (throwFrames != null && throwFrames.Length > 2) _sr.sprite = throwFrames[2];
            SpawnGrenade();
            if (grenadeThrowSound != null && _audio != null) _audio.PlayOneShot(grenadeThrowSound, 0.85f);

            // Phase 2 bonus secondary grenade
            if (isPhase2)
            {
                yield return new WaitForSeconds(0.12f);
                SpawnGrenade(arcMultiplier: 1.25f);
            }

            yield return new WaitForSeconds(0.25f);

            // Frame 4: Slide landing
            _currentFrame = 3;
            if (throwFrames != null && throwFrames.Length > 3) _sr.sprite = throwFrames[3];
            yield return new WaitForSeconds(0.18f);

            _grenadeTimer = isPhase2 ? grenadeCooldown * 0.65f : grenadeCooldown;
            _isActionLocked = false;
        }

        private void SpawnGrenade(float arcMultiplier = 1.0f)
        {
            Vector3 origin = transform.position + Vector3.up * 0.6f;
            float throwDir = isFacingLeft ? -1f : 1f;
            Vector2 launchVel = new Vector2(throwDir * grenadeThrowSpeed * arcMultiplier, 5.5f * arcMultiplier);

            GameObject grenadeObj = new GameObject("Lour_TacticalGrenade");
            grenadeObj.transform.position = origin;

            var sr = grenadeObj.AddComponent<SpriteRenderer>();
            sr.sprite = grenadeSprite;
            sr.sortingOrder = _sr.sortingOrder + 1;

            var proj = grenadeObj.AddComponent<BossProjectile>();
            proj.Init(launchVel.normalized, launchVel.magnitude, new Color(0.3f, 0.95f, 0.3f), parriable: true, gravity: true, gravScale: 1.8f);
            proj.ConfigureAsCluster(grenadeClusterCount, fragmentSpeed: 7.5f);
            proj.lifeTime = 1.4f;
        }

        private IEnumerator TacticalDashRoutine(float direction)
        {
            _isActionLocked = true;
            _isInvincible = true;
            currentState = BossState.Dash;

            if (_dashTrail != null) _dashTrail.enabled = true;
            if (dashSound != null && _audio != null) _audio.PlayOneShot(dashSound, 0.8f);

            _rb.linearVelocity = new Vector2(direction * dashSpeed, 0f);

            float t = 0f;
            while (t < dashDuration)
            {
                t += Time.deltaTime;
                int frameIdx = Mathf.FloorToInt((t / dashDuration) * (dashFrames != null ? dashFrames.Length : 1));
                if (dashFrames != null && frameIdx < dashFrames.Length) _sr.sprite = dashFrames[frameIdx];
                yield return null;
            }

            if (_dashTrail != null) _dashTrail.enabled = false;
            _rb.linearVelocity = new Vector2(direction * 2f, _rb.linearVelocity.y);
            _isInvincible = false;
            _isActionLocked = false;
        }

        private IEnumerator DefeatRoutine()
        {
            currentState = BossState.Defeated;
            _isActionLocked = true;
            _rb.linearVelocity = Vector2.zero;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            if (_col != null) _col.enabled = false;
            if (aimLaser != null) aimLaser.enabled = false;
            transform.rotation = Quaternion.identity;

            if (retreatSound != null && _audio != null) _audio.PlayOneShot(retreatSound, 1f);

            // Smoke / retreat flash sequence
            for (int i = 0; i < 6; i++)
            {
                _sr.color = new Color(1f, 1f, 1f, 0.3f);
                yield return new WaitForSeconds(0.08f);
                _sr.color = Color.white;
                yield return new WaitForSeconds(0.08f);
            }

            // Drop victory rewards (Tokens/Health)
            var tokenPrefab = Resources.Load<GameObject>("Prefabs/Token");
            if (tokenPrefab != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    Instantiate(tokenPrefab, transform.position + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 1f, 0), Quaternion.identity);
                }
            }

            // Fade out
            float fade = 1f;
            while (fade > 0f)
            {
                fade -= Time.deltaTime * 2.5f;
                _sr.color = new Color(1f, 1f, 1f, fade);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        #endregion

        #region Animation Controller

        private void UpdateAnimation()
        {
            _animTimer += Time.deltaTime;

            switch (currentState)
            {
                case BossState.Idle:
                    AnimateCycle(idleFrames, 0.14f);
                    break;
                case BossState.Walk:
                    AnimateCycle(walkFrames, 0.12f);
                    break;
                case BossState.Sprint:
                    AnimateCycle(runFrames, 0.09f);
                    break;
                case BossState.Aiming:
                    if (fireFrames != null && fireFrames.Length > 0) _sr.sprite = fireFrames[0];
                    break;
                case BossState.Firing:
                    if (fireFrames != null && fireFrames.Length > 1) _sr.sprite = fireFrames[1];
                    break;
                case BossState.BackstepGrenade:
                    // Handled inside coroutine
                    break;
                case BossState.Dash:
                    // Handled inside coroutine
                    break;
                case BossState.Jump:
                    AnimateCycle(jumpFrames, 0.12f);
                    break;
            }
        }

        private void AnimateCycle(Sprite[] frames, float frameDuration)
        {
            if (frames == null || frames.Length == 0) return;

            if (_animTimer >= frameDuration)
            {
                _animTimer = 0f;
                _currentFrame = (_currentFrame + 1) % frames.Length;
                _sr.sprite = frames[_currentFrame];
            }
        }

        #endregion

        #region Damage & Hit Interface

        public void OnPlayerHit(Vector2 hitPoint)
        {
            if (currentState == BossState.Defeated || _isInvincible) return;

            _health.Decrement();
            StartCoroutine(FlashHurtRoutine());

            if (hurtSound != null && _audio != null) _audio.PlayOneShot(hurtSound, 0.8f);

            // Reactive evasive maneuver: chance to dash or backstep away
            float evadeChance = isPhase2 ? 0.7f : 0.45f;
            if (!_isActionLocked && UnityEngine.Random.value < evadeChance)
            {
                float xDiff = _playerTransform != null ? _playerTransform.position.x - transform.position.x : 1f;
                float evadeDir = xDiff > 0 ? -1f : 1f;
                if (UnityEngine.Random.value < 0.5f && _grenadeTimer <= 0.5f)
                {
                    StartCoroutine(BackstepGrenadeRoutine());
                }
                else
                {
                    StartCoroutine(TacticalDashRoutine(evadeDir));
                }
            }
        }

        private IEnumerator FlashHurtRoutine()
        {
            Color orig = isPhase2 ? new Color(1.0f, 0.85f, 0.85f) : Color.white;
            _sr.color = Color.red;
            yield return new WaitForSeconds(0.07f);
            _sr.color = orig;
        }

        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            var player = collision.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                var ev = Schedule<PlayerEnemyCollision>();
                ev.player = player;
                ev.enemy = this;
            }
        }

        #endregion
    }
}
