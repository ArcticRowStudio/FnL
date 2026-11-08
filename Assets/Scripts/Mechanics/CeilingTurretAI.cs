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
    /// Ceiling-mounted industrial defense sentry turret.
    /// Clamped to ceiling H-beams or bulkheads.
    /// Features:
    /// - 180-degree downward player tracking.
    /// - LineRenderer based targeting laser with warning lock-on telegraph.
    /// - 3-round rapid autocannon burst fire.
    /// - Steam venting cooldown period.
    /// - Explosive destruction on defeat.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class CeilingTurretAI : EnemyController
    {
        public enum TurretState
        {
            Idle,
            Aiming,
            Locked,
            Firing,
            Cooling
        }

        [Header("Targeting & Range")]
        public TurretState currentState = TurretState.Idle;
        public float detectionRadius = 11.0f;
        public float aimDuration = 0.85f;
        public float lockDuration = 0.30f;
        public float cooldownDuration = 2.0f;

        [Header("Autocannon Ballistics")]
        public int burstRounds = 3;
        public float burstInterval = 0.12f;
        public float bulletSpeed = 11.0f;
        public int bulletDamage = 1;
        public Color bulletColor = new Color(1f, 0.45f, 0.1f);

        [Header("Transforms & Laser")]
        public Transform muzzlePoint;
        public LineRenderer laserLine;
        public SpriteRenderer turretRenderer;

        [Header("Modular Articulated Parts")]
        public Transform mountTransform;
        public Transform turretHeadPivot;
        public SpriteRenderer mountRenderer;
        public SpriteRenderer headRenderer;

        [Header("Audio")]
        public AudioClip laserChargeSound;
        public AudioClip fireSound;
        public AudioClip steamVentSound;

        private Health _health;
        private PlayerController _player;
        private bool _isBusy = false;
        private Vector3 _lockedAimDir = Vector3.down;

        private Vector3 _initialHeadLocalPos;
        private Vector3 _headRecoilOffset = Vector3.zero;

        protected override void Awake()
        {
            base.Awake();

            // Enter Play Mode Options keep domain and scene reload off, so these objects are
            // never recreated between sessions. Stopping play mid-action would otherwise leave
            // the flags below set, and this enemy would never act again in any later session.
            currentState = TurretState.Idle;
            _isBusy = false;
            _lockedAimDir = Vector3.down;
            _headRecoilOffset = Vector3.zero;
            _player = null;
            if (laserLine != null) laserLine.enabled = false;
            _health = GetComponent<Health>();
            if (_health == null) _health = gameObject.AddComponent<Health>();
            _health.maxHP = 3;
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
            if (mountTransform == null) mountTransform = transform.Find("Mount");
            if (turretHeadPivot == null) turretHeadPivot = transform.Find("TurretHeadPivot");

            if (mountTransform != null && mountRenderer == null)
                mountRenderer = mountTransform.GetComponentInChildren<SpriteRenderer>();

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
                        mObj.transform.localPosition = new Vector3(1.903f, -1.318f, 0f);
                        muzzlePoint = mObj.transform;
                    }
                }
            }

            if (turretRenderer == null) turretRenderer = GetComponent<SpriteRenderer>();
        }

        private void SetupLaser()
        {
            if (laserLine == null)
            {
                laserLine = GetComponent<LineRenderer>();
                if (laserLine == null) laserLine = gameObject.AddComponent<LineRenderer>();
            }

            laserLine.material = new Material(Shader.Find("Sprites/Default"));
            laserLine.startWidth = 0.04f;
            laserLine.endWidth = 0.04f;
            laserLine.startColor = new Color(1f, 0.1f, 0.1f, 0.7f);
            laserLine.endColor = new Color(1f, 0.3f, 0.1f, 0.9f);
            laserLine.positionCount = 2;
            laserLine.enabled = false;
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
                _audio.pitch = 1.3f;
                _audio.PlayOneShot(ouch);
            }

            if (!h.IsAlive)
            {
                TriggerExplosion();
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            Color flash = new Color(1.8f, 0.5f, 0.5f, 1f);
            SetRenderersColor(flash);
            yield return new WaitForSeconds(0.1f);
            SetRenderersColor(Color.white);
        }

        private void SetRenderersColor(Color c)
        {
            if (turretRenderer != null) turretRenderer.color = c;
            if (mountRenderer != null) mountRenderer.color = c;
            if (headRenderer != null) headRenderer.color = c;
        }

        private void TriggerExplosion()
        {
            if (laserLine != null) laserLine.enabled = false;
            if (turretHeadPivot != null)
            {
                turretHeadPivot.localRotation = Quaternion.Euler(0, 0, Random.Range(-120f, -60f));
            }
            CameraZoomController.TriggerShake(0.35f, 0.15f);
            ProjectileDebris.SpawnSpark(transform.position, new Color(1f, 0.4f, 0.1f), 20, 8f);
            ProjectileDebris.SpawnSpark(transform.position, new Color(1f, 0.9f, 0.4f), 14, 6f);
            ExpandingRing.Spawn(transform.position, new Color(1f, 0.6f, 0.2f, 0.9f), 2.8f, 0.25f);
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

            if (_isBusy) return;

            CheckPlayerInSight();
        }

        private void UpdateArticulatedMotion()
        {
            // Head recoil spring damping return
            _headRecoilOffset = Vector3.Lerp(_headRecoilOffset, Vector3.zero, Time.deltaTime * 14f);
            if (turretHeadPivot != null)
            {
                turretHeadPivot.localPosition = _initialHeadLocalPos + _headRecoilOffset;
            }

            // Idle sweep when not aiming or locked
            if (currentState == TurretState.Idle && turretHeadPivot != null)
            {
                float idleSweep = Mathf.Sin(Time.time * 1.4f) * 35f - 90f;
                turretHeadPivot.localRotation = Quaternion.Slerp(turretHeadPivot.localRotation, Quaternion.Euler(0, 0, idleSweep), Time.deltaTime * 3.5f);
            }
            else if (currentState == TurretState.Cooling && turretHeadPivot != null)
            {
                // Head rests pointing down-forward while venting steam
                turretHeadPivot.localRotation = Quaternion.Slerp(turretHeadPivot.localRotation, Quaternion.Euler(0, 0, -90f), Time.deltaTime * 5f);
            }
        }

        private void CheckPlayerInSight()
        {
            if (_player == null || !_player.gameObject.activeInHierarchy || _player.health == null || !_player.health.IsAlive)
            {
                if (laserLine != null) laserLine.enabled = false;
                currentState = TurretState.Idle;
                return;
            }

            Vector3 myPos = transform.position;
            Vector3 playerPos = _player.transform.position + Vector3.up * 0.4f;
            float dist = Vector2.Distance(myPos, playerPos);

            // Must be within radius and strictly below the ceiling mount
            if (dist <= detectionRadius && playerPos.y < myPos.y + 0.5f)
            {
                StartCoroutine(CombatCycleRoutine());
            }
            else
            {
                if (laserLine != null) laserLine.enabled = false;
                currentState = TurretState.Idle;
            }
        }

        private IEnumerator CombatCycleRoutine()
        {
            _isBusy = true;
            currentState = TurretState.Aiming;

            if (laserLine != null)
            {
                laserLine.enabled = true;
                laserLine.startWidth = 0.035f;
                laserLine.endWidth = 0.035f;
                laserLine.startColor = new Color(1f, 0.15f, 0.15f, 0.65f);
                laserLine.endColor = new Color(1f, 0.3f, 0.15f, 0.85f);
            }

            if (_audio != null && laserChargeSound != null)
            {
                _audio.pitch = 1.2f;
                _audio.PlayOneShot(laserChargeSound, 0.7f);
            }

            // Phase 1: Realtime aiming tracking player
            float elapsed = 0f;
            Vector3 startPos = muzzlePoint != null ? muzzlePoint.position : (turretHeadPivot != null ? turretHeadPivot.position : transform.position);

            while (elapsed < aimDuration)
            {
                elapsed += Time.deltaTime;
                if (_player == null) break;

                startPos = muzzlePoint != null ? muzzlePoint.position : (turretHeadPivot != null ? turretHeadPivot.position : transform.position);
                Vector3 targetPos = _player.transform.position + Vector3.up * 0.4f;
                _lockedAimDir = (targetPos - startPos).normalized;

                // Smoothly orient turret head pivot to aim at player
                if (turretHeadPivot != null)
                {
                    float targetDeg = Mathf.Atan2(_lockedAimDir.y, _lockedAimDir.x) * Mathf.Rad2Deg;
                    // Clamp downward arc (-170 to -10)
                    if (targetDeg > 0) targetDeg = targetDeg > 90 ? -170f : -10f;
                    targetDeg = Mathf.Clamp(targetDeg, -170f, -10f);
                    turretHeadPivot.rotation = Quaternion.Slerp(turretHeadPivot.rotation, Quaternion.Euler(0, 0, targetDeg), Time.deltaTime * 10f);
                }

                if (laserLine != null)
                {
                    startPos = muzzlePoint != null ? muzzlePoint.position : (turretHeadPivot != null ? turretHeadPivot.position : transform.position);
                    laserLine.SetPosition(0, startPos);
                    laserLine.SetPosition(1, startPos + _lockedAimDir * 18f);
                }

                yield return null;
            }

            // Phase 2: Lock angle (telegraph flash)
            currentState = TurretState.Locked;
            if (laserLine != null)
            {
                laserLine.startWidth = 0.07f;
                laserLine.endWidth = 0.07f;
                laserLine.startColor = new Color(1f, 0.9f, 0.3f, 0.95f);
                laserLine.endColor = new Color(1f, 0.4f, 0.1f, 0.95f);
            }

            yield return new WaitForSeconds(lockDuration);

            if (laserLine != null) laserLine.enabled = false;

            // Phase 3: Rapid Burst Fire
            currentState = TurretState.Firing;
            for (int i = 0; i < burstRounds; i++)
            {
                FireBullet(_lockedAimDir);
                yield return new WaitForSeconds(burstInterval);
            }

            // Phase 4: Cooling & Steam Venting
            currentState = TurretState.Cooling;
            Vector3 ventPos = transform.position + Vector3.down * 0.4f;
            ProjectileDebris.SpawnSpark(ventPos, new Color(0.9f, 0.9f, 0.95f), 8, 3f);

            if (_audio != null && steamVentSound != null)
            {
                _audio.pitch = 1.1f;
                _audio.PlayOneShot(steamVentSound, 0.5f);
            }

            yield return new WaitForSeconds(cooldownDuration);

            _isBusy = false;
            currentState = TurretState.Idle;
        }

        private void FireBullet(Vector3 direction)
        {
            Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + direction * 0.5f;

            // Recoil kickback on turret head
            _headRecoilOffset += -direction * 0.18f;

            GameObject bulletObj = new GameObject("TurretBullet");
            bulletObj.transform.position = spawnPos;

            var proj = bulletObj.AddComponent<Boss.BossProjectile>();
            proj.velocity = direction * bulletSpeed;
            proj.damage = bulletDamage;
            proj.projectileColor = bulletColor;
            proj.isParriable = true;
            proj.lifeTime = 3.5f;

            // Muzzle flash
            ExpandingRing.Spawn(spawnPos, bulletColor, 0.8f, 0.12f);
            ProjectileDebris.SpawnSpark(spawnPos, new Color(1f, 0.8f, 0.3f), 8, 5f);

            if (_audio != null && fireSound != null)
            {
                _audio.pitch = Random.Range(1.1f, 1.3f);
                _audio.PlayOneShot(fireSound, 0.8f);
            }
        }
    }
}
