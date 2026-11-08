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
    /// Floor-mounted heavy tripod defense turret.
    /// Anchored to metal floor grating with 3 hydraulic outriggers.
    /// Features:
    /// - Dual heavy cannon breach firing alternating heavy rounds.
    /// - Weak Point (Rear Ammo Drum): Attacking from behind detonates the ammo drum,
    ///   triggering a massive critical explosion that destroys the turret and damages nearby enemies!
    /// - Armored Front: Frontal armor absorbs direct hits (requires multiple strikes).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class FloorTurretAI : EnemyController
    {
        public enum TurretState
        {
            Idle,
            Tracking,
            Firing,
            Reloading
        }

        [Header("Targeting & Range")]
        public TurretState currentState = TurretState.Idle;
        public bool isFacingLeft = true;
        public float detectionRadius = 12.0f;
        public float fireDelay = 0.6f;
        public float reloadDuration = 2.2f;

        [Header("Dual Heavy Cannons")]
        public int burstCount = 4;
        public float shotInterval = 0.15f;
        public float bulletSpeed = 12.0f;
        public int bulletDamage = 1;
        public Color cannonBulletColor = new Color(1f, 0.6f, 0.15f);

        [Header("Ammo Drum Weak Point")]
        public float ammoDrumExplosionRadius = 4.0f;
        public AudioClip ammoDrumDetonateSound;

        [Header("Transforms & Visuals")]
        public Transform muzzlePoint;
        public SpriteRenderer turretRenderer;
        public LineRenderer aimLaser;

        [Header("Modular Articulated Parts")]
        public Transform baseTransform;
        public Transform turretHeadPivot;
        public SpriteRenderer baseRenderer;
        public SpriteRenderer headRenderer;

        [Header("Audio")]
        public AudioClip fireSound;
        public AudioClip reloadSound;

        private Health _health;
        private PlayerController _player;
        private bool _isFiring = false;
        private bool _isDetonated = false;

        private Vector3 _initialHeadLocalPos;
        private Vector3 _headRecoilOffset = Vector3.zero;
        private float _currentPitchAngle = 0f;

        protected override void Awake()
        {
            base.Awake();

            // Enter Play Mode Options keep domain and scene reload off, so these objects are
            // never recreated between sessions. Stopping play mid-action would otherwise leave
            // the flags below set, and this enemy would never act again in any later session.
            currentState = TurretState.Idle;
            _isFiring = false;
            _isDetonated = false;
            _headRecoilOffset = Vector3.zero;
            _currentPitchAngle = 0f;
            _player = null;
            if (aimLaser != null) aimLaser.enabled = false;
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

            InitModularParts();
            SetupLaser();

            if (control != null) control.enabled = false;
        }

        private void InitModularParts()
        {
            if (baseTransform == null) baseTransform = transform.Find("Base");
            if (turretHeadPivot == null) turretHeadPivot = transform.Find("TurretHeadPivot");

            if (baseTransform != null && baseRenderer == null)
                baseRenderer = baseTransform.GetComponentInChildren<SpriteRenderer>();

            if (turretHeadPivot != null)
            {
                _initialHeadLocalPos = turretHeadPivot.localPosition;
                if (headRenderer == null) headRenderer = turretHeadPivot.GetComponentInChildren<SpriteRenderer>();
                if (muzzlePoint == null)
                {
                    muzzlePoint = turretHeadPivot.Find("MuzzlePoint");
                    if (muzzlePoint == null)
                    {
                        GameObject mObj = new GameObject("MuzzlePoint");
                        mObj.transform.SetParent(turretHeadPivot, false);
                        mObj.transform.localPosition = new Vector3(1.670f, 0.445f, 0f);
                        muzzlePoint = mObj.transform;
                    }
                }
            }

            if (turretRenderer == null) turretRenderer = GetComponent<SpriteRenderer>();
        }

        private void SetupLaser()
        {
            if (aimLaser == null)
            {
                aimLaser = GetComponent<LineRenderer>();
                if (aimLaser == null) aimLaser = gameObject.AddComponent<LineRenderer>();
            }

            aimLaser.material = new Material(Shader.Find("Sprites/Default"));
            aimLaser.startWidth = 0.035f;
            aimLaser.endWidth = 0.035f;
            aimLaser.startColor = new Color(1f, 0.2f, 0.05f, 0.6f);
            aimLaser.endColor = new Color(1f, 0.4f, 0.1f, 0.8f);
            aimLaser.positionCount = 2;
            aimLaser.enabled = false;
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
            if (h == null) return;

            StartCoroutine(DamageFlashRoutine());
            if (_audio != null && ouch != null)
            {
                _audio.pitch = 1.1f;
                _audio.PlayOneShot(ouch);
            }

            if (!h.IsAlive && !_isDetonated)
            {
                StandardExplosion();
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            Color flash = new Color(1.7f, 0.6f, 0.6f, 1f);
            SetRenderersColor(flash);
            yield return new WaitForSeconds(0.1f);
            SetRenderersColor(Color.white);
        }

        private void SetRenderersColor(Color c)
        {
            if (turretRenderer != null) turretRenderer.color = c;
            if (baseRenderer != null) baseRenderer.color = c;
            if (headRenderer != null) headRenderer.color = c;
        }

        protected override void Update()
        {
            if (_health != null && !_health.IsAlive) return;

            if (_player == null)
            {
                var model = Simulation.GetModel<PlatformerModel>();
                if (model != null) _player = model.player;
                if (_player == null) _player = FindAnyObjectByType<PlayerController>();
            }

            UpdateArticulatedMotion();

            if (_isFiring) return;

            CheckTargeting();
        }

        private void UpdateArticulatedMotion()
        {
            // Head recoil spring damping return
            _headRecoilOffset = Vector3.Lerp(_headRecoilOffset, Vector3.zero, Time.deltaTime * 12f);
            if (turretHeadPivot != null)
            {
                turretHeadPivot.localPosition = _initialHeadLocalPos + _headRecoilOffset;
            }

            if (turretHeadPivot != null && currentState != TurretState.Firing && !_isDetonated)
            {
                if (_player != null && _player.gameObject.activeInHierarchy && _player.health != null && _player.health.IsAlive)
                {
                    Vector3 targetPos = _player.transform.position + Vector3.up * 0.4f;
                    Vector3 diff = targetPos - turretHeadPivot.position;
                    float sign = isFacingLeft ? -1f : 1f;
                    float targetPitch = Mathf.Atan2(diff.y, Mathf.Abs(diff.x)) * Mathf.Rad2Deg;
                    targetPitch = Mathf.Clamp(targetPitch, -20f, 30f);
                    _currentPitchAngle = Mathf.Lerp(_currentPitchAngle, targetPitch, Time.deltaTime * 6f);
                    turretHeadPivot.localRotation = Quaternion.Euler(0, 0, sign * _currentPitchAngle);
                }
                else
                {
                    float idlePitch = Mathf.Sin(Time.time * 1.5f) * 4f;
                    turretHeadPivot.localRotation = Quaternion.Euler(0, 0, idlePitch);
                }
            }
        }

        private void CheckTargeting()
        {
            if (_player == null || !_player.gameObject.activeInHierarchy || _player.health == null || !_player.health.IsAlive)
            {
                if (aimLaser != null) aimLaser.enabled = false;
                currentState = TurretState.Idle;
                return;
            }

            Vector3 myPos = transform.position;
            Vector3 playerPos = _player.transform.position + Vector3.up * 0.4f;
            float dist = Vector2.Distance(myPos, playerPos);

            if (dist <= detectionRadius)
            {
                float diffX = playerPos.x - myPos.x;
                if (Mathf.Abs(diffX) > 0.3f)
                {
                    UpdateFacing(diffX < 0);
                }

                StartCoroutine(FireSequenceRoutine(playerPos));
            }
            else
            {
                if (aimLaser != null) aimLaser.enabled = false;
                currentState = TurretState.Idle;
            }
        }

        private void UpdateFacing(bool facingLeft)
        {
            isFacingLeft = facingLeft;
            if (turretHeadPivot != null)
            {
                // Invert X scale on head pivot so cannons face left or right
                turretHeadPivot.localScale = new Vector3(isFacingLeft ? -1f : 1f, 1f, 1f);
            }
            if (turretRenderer != null)
            {
                turretRenderer.flipX = false;
            }
        }

        private IEnumerator FireSequenceRoutine(Vector3 targetPos)
        {
            _isFiring = true;
            currentState = TurretState.Tracking;

            // Telegraph Laser
            if (aimLaser != null)
            {
                aimLaser.enabled = true;
                Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 0.8f;
                aimLaser.SetPosition(0, origin);
                aimLaser.SetPosition(1, targetPos);
            }

            yield return new WaitForSeconds(fireDelay);

            if (aimLaser != null) aimLaser.enabled = false;

            // Firing burst
            currentState = TurretState.Firing;
            Vector3 fireOrigin = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 0.8f;
            Vector3 dir = (targetPos - fireOrigin).normalized;

            for (int i = 0; i < burstCount; i++)
            {
                fireOrigin = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 0.8f;
                FireCannon(fireOrigin, dir);
                yield return new WaitForSeconds(shotInterval);
            }

            // Reload / Venting
            currentState = TurretState.Reloading;
            if (_audio != null && reloadSound != null)
            {
                _audio.pitch = 1.0f;
                _audio.PlayOneShot(reloadSound, 0.7f);
            }

            yield return new WaitForSeconds(reloadDuration);

            _isFiring = false;
            currentState = TurretState.Idle;
        }

        private void FireCannon(Vector3 origin, Vector3 dir)
        {
            // Cannon breach recoil kickback
            _headRecoilOffset += (isFacingLeft ? Vector3.right : Vector3.left) * 0.16f;

            GameObject bulletObj = new GameObject("FloorTurretBullet");
            bulletObj.transform.position = origin;

            var proj = bulletObj.AddComponent<Boss.BossProjectile>();
            proj.velocity = dir * bulletSpeed;
            proj.damage = bulletDamage;
            proj.projectileColor = cannonBulletColor;
            proj.isParriable = true;
            proj.lifeTime = 3.0f;

            // Recoil & Muzzle flash
            ExpandingRing.Spawn(origin, cannonBulletColor, 0.9f, 0.12f);
            ProjectileDebris.SpawnSpark(origin, new Color(1f, 0.7f, 0.2f), 10, 6f);
            CameraZoomController.TriggerShake(0.12f, 0.05f);

            if (_audio != null && fireSound != null)
            {
                _audio.pitch = Random.Range(0.85f, 1.05f);
                _audio.PlayOneShot(fireSound, 0.9f);
            }
        }

        /// <summary>
        /// Intercepts damage to check for rear Ammo Drum critical detonation!
        /// If attacked from behind, triggers instant Ammo Drum explosion.
        /// </summary>
        public override bool CanTakeDamage(Vector2 hitPosition, Vector2 attackerPosition)
        {
            float attackDir = attackerPosition.x - transform.position.x;
            bool attackedFromBehind = (isFacingLeft && attackDir > 0) || (!isFacingLeft && attackDir < 0);

            if (attackedFromBehind && !_isDetonated)
            {
                ExplodeAmmoDrum();
                return false; // Handled by instant detonation
            }

            return true;
        }

        public void ExplodeAmmoDrum()
        {
            if (_isDetonated) return;
            _isDetonated = true;

            if (aimLaser != null) aimLaser.enabled = false;

            if (turretHeadPivot != null)
            {
                turretHeadPivot.localRotation = Quaternion.Euler(0, 0, isFacingLeft ? -60f : 60f);
                turretHeadPivot.localPosition += Vector3.up * 0.4f;
            }

            Vector3 drumPos = transform.position + (isFacingLeft ? Vector3.right : Vector3.left) * 0.75f + Vector3.up * 1.35f;

            // Massive Screen Shake
            CameraZoomController.TriggerShake(0.55f, 0.28f);

            // Audio: heavy detonation
            if (_audio != null)
            {
                AudioClip snd = ammoDrumDetonateSound != null ? ammoDrumDetonateSound : ouch;
                if (snd != null)
                {
                    _audio.pitch = 0.65f;
                    _audio.PlayOneShot(snd, 1.2f);
                }
            }

            // VFX: multi-layered firestorm explosion
            ExpandingRing.Spawn(drumPos, new Color(1f, 0.4f, 0.1f, 0.95f), 4.5f, 0.35f);
            ExpandingRing.Spawn(drumPos, new Color(1f, 0.9f, 0.3f, 0.9f), 2.8f, 0.22f);
            ProjectileDebris.SpawnSpark(drumPos, new Color(1f, 0.3f, 0.1f), 28, 12f);
            ProjectileDebris.SpawnSpark(drumPos, new Color(1f, 0.85f, 0.3f), 20, 9f);

            // Splash damage to nearby enemies!
            var nearbyColliders = Physics2D.OverlapCircleAll(drumPos, ammoDrumExplosionRadius);
            for (int i = 0; i < nearbyColliders.Length; i++)
            {
                var col = nearbyColliders[i];
                if (col == null || col.transform == transform) continue;

                var otherEnemy = col.GetComponentInParent<EnemyController>();
                if (otherEnemy != null && otherEnemy != this)
                {
                    var h = otherEnemy.GetComponent<Health>();
                    if (h != null)
                    {
                        h.Decrement();
                        if (!h.IsAlive) Schedule<EnemyDeath>().enemy = otherEnemy;
                    }
                }
            }

            // Destroy this turret
            Schedule<EnemyDeath>().enemy = this;
        }

        private void StandardExplosion()
        {
            if (aimLaser != null) aimLaser.enabled = false;
            if (turretHeadPivot != null)
            {
                turretHeadPivot.localRotation = Quaternion.Euler(0, 0, isFacingLeft ? -45f : 45f);
            }
            CameraZoomController.TriggerShake(0.3f, 0.12f);
            ProjectileDebris.SpawnSpark(transform.position, new Color(1f, 0.5f, 0.1f), 16, 7f);
            ExpandingRing.Spawn(transform.position, new Color(1f, 0.7f, 0.3f, 0.85f), 2.5f, 0.2f);
        }
    }
}
