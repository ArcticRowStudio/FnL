using System.Collections;
using System.Collections.Generic;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Model;
using Platformer.View;
using Platformer.Mechanics.Boss;
using UnityEngine;
using static Platformer.Core.Simulation;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Heavy Crawler Mortar Artillery Robot AI Controller.
    /// Pure flat 2D side-scrolling profile with articulated high-angle mortar cannon.
    ///
    /// Features:
    /// - Ballistic Arcing Fire: lobs a shell onto the floor under the player, over any cover lower than the arc.
    /// - Ground Telegraph Warning: Projects a pulsing red danger marker on the floor before impact.
    /// - Holds fire when terrain blocks the arc, so it never shells its own ceiling or the next room.
    /// - Articulated Motion and Recoil: Smooth cannon elevation tracking and heavy hydraulic recoil kickback.
    /// - Cluster Splash Detonation: Shell explodes on impact into radial high-velocity shrapnel.
    /// - Rear Ammo Rack Weak Point: a melee hit from behind triggers a catastrophic ammo cook-off!
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class MortarRobotAI : EnemyController
    {
        public enum MortarState
        {
            Idle,
            Aiming,
            Firing,
            Reloading
        }

        [Header("Targeting and Range")]
        public MortarState currentState = MortarState.Idle;
        public bool isFacingRight = true;
        [Tooltip("Horizontal reach.")]
        public float detectionRadius = 16.0f;
        [Tooltip("Players nearer than this horizontally are too close to shell.")]
        public float minFiringDistance = 3.5f;
        [Tooltip("Players further above or below than this are out of reach.")]
        public float verticalDetectLimit = 6.0f;
        public float aimDuration = 1.2f;
        public float reloadDuration = 2.6f;

        [Header("Mortar Ballistics")]
        public float shellFlightTime = 1.3f;
        public int directDamage = 2;
        public int clusterFragments = 6;
        public float fragmentSpeed = 6.0f;
        public Color shellColor = new Color(1f, 0.45f, 0.1f);
        public float cannonMinElevation = 35.0f;
        public float cannonMaxElevation = 75.0f;

        [Header("Ammo Rack Cook-off")]
        public float rackBlastRadius = 4.0f;
        [Tooltip("Only melee from behind sets the rack off, so the player is always inside the blast.")]
        public bool rackBlastHurtsPlayer = false;

        [Header("Articulated Transforms and Visuals")]
        public Transform baseTransform;
        public Transform cannonPivot;
        public Transform muzzlePoint;
        public Transform weakPoint;
        public SpriteRenderer baseRenderer;
        public SpriteRenderer cannonRenderer;

        [Header("Recoil Dynamics")]
        public float recoilDistance = 0.35f;
        public float recoilRecoverSpeed = 8.0f;

        [Header("Audio")]
        public AudioClip aimWarningSound;
        public AudioClip mortarFireSound;
        public AudioClip ammoDetonateSound;
        public AudioClip reloadSound;

        // FireClusterMortar launches its shell at gravity scale 1.2; the arc check has to agree.
        private const float ShellGravityScale = 1.2f;
        private const int ArcSamples = 12;
        // The arc is meant to end on the floor, so terrain this close to the target doesn't block it.
        private const float LandingTolerance = 0.75f;
        private const float GroundProbeDepth = 12f;
        // Multipliers on the natural flight time: flatter and steeper lobs to try when one is blocked.
        private static readonly float[] ArcCandidates = { 1f, 1.15f, 1.3f, 1.45f, 0.85f, 0.7f };
        private const float MinFlightTime = 0.8f;
        private const float MaxFlightTime = 2.2f;
        // Searching arcs costs a handful of linecasts, so don't redo it every frame.
        private const float TargetCheckInterval = 0.2f;

        private Health _health;
        private PlayerController _player;
        private bool _isAttacking = false;
        private bool _isDetonated = false;

        private Vector3 _initialCannonLocalPos;
        private Vector3 _cannonRecoilOffset = Vector3.zero;
        private float _currentElevationAngle = 55.0f;
        private GameObject _telegraphObject;
        private LineRenderer _telegraphCircle;

        private ContactFilter2D _terrainFilter;
        private readonly RaycastHit2D[] _rayHits = new RaycastHit2D[8];
        private float _nextTargetCheck = 0f;

        protected override void Awake()
        {
            base.Awake();

            // Enter Play Mode Options keep domain and scene reload off, so these objects are
            // never recreated between sessions. Stopping play mid-action would otherwise leave
            // the flags below set, and this enemy would never act again in any later session.
            currentState = MortarState.Idle;
            _isAttacking = false;
            _isDetonated = false;
            _cannonRecoilOffset = Vector3.zero;
            _player = null;
            _nextTargetCheck = 0f;
            _health = GetComponent<Health>();
            if (_health == null) _health = gameObject.AddComponent<Health>();
            _health.maxHP = 5;
            _health.ResetHP();

            if (_health != null)
            {
                // Detach first: with scene reload off this component is reused between sessions,
                // so a plain += stacks another handler on every Play.
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.OnHealthChanged += HandleHealthChanged;
            }

            // Shells are spawned on the default layer, so the arc is checked against what they hit.
            _terrainFilter.useTriggers = false;
            _terrainFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(0));

            InitModularParts();
            SetupTelegraph();

            if (control != null) control.enabled = false;
        }

        private void InitModularParts()
        {
            if (baseTransform == null) baseTransform = transform.Find("Base");
            if (cannonPivot == null) cannonPivot = transform.Find("CannonPivot");
            if (muzzlePoint == null && cannonPivot != null) muzzlePoint = cannonPivot.Find("MuzzlePoint");
            if (weakPoint == null) weakPoint = transform.Find("WeakPoint");

            if (baseTransform != null && baseRenderer == null)
                baseRenderer = baseTransform.GetComponent<SpriteRenderer>();

            if (cannonPivot != null)
            {
                if (cannonRenderer == null)
                    cannonRenderer = cannonPivot.GetComponentInChildren<SpriteRenderer>();
                _initialCannonLocalPos = cannonPivot.localPosition;
            }

            // By default sprite faces right
            UpdateFacing(isFacingRight);
        }

        private void SetupTelegraph()
        {
            if (_telegraphObject == null)
            {
                _telegraphObject = new GameObject("GroundTelegraph");
                _telegraphObject.transform.SetParent(transform, false);

                _telegraphCircle = _telegraphObject.AddComponent<LineRenderer>();
                _telegraphCircle.useWorldSpace = true;
                _telegraphCircle.loop = true;
                _telegraphCircle.positionCount = 36;
                _telegraphCircle.startWidth = 0.08f;
                _telegraphCircle.endWidth = 0.08f;

                Material lineMat = new Material(Shader.Find("Sprites/Default"));
                _telegraphCircle.material = lineMat;
                _telegraphCircle.startColor = new Color(1f, 0.2f, 0.1f, 0.85f);
                _telegraphCircle.endColor = new Color(1f, 0.2f, 0.1f, 0.85f);
                _telegraphCircle.sortingOrder = 30;
                _telegraphCircle.enabled = false;
            }
        }

        private void HandleHealthChanged(Health h)
        {
            if (_isDetonated) return;

            StartCoroutine(DamageFlashRoutine());

            if (muzzlePoint != null)
            {
                ProjectileDebris.SpawnSpark(muzzlePoint.position, new Color(1f, 0.8f, 0.3f), 4, 4f);
            }

            if (_audio != null && ouch != null)
            {
                _audio.pitch = 0.95f;
                _audio.PlayOneShot(ouch);
            }

            if (!h.IsAlive && !_isDetonated)
            {
                StandardExplosion();
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            Color flash = new Color(1.8f, 0.7f, 0.7f, 1f);
            SetRenderersColor(flash);
            yield return new WaitForSeconds(0.1f);
            SetRenderersColor(Color.white);
        }

        private void SetRenderersColor(Color c)
        {
            if (baseRenderer != null) baseRenderer.color = c;
            if (cannonRenderer != null) cannonRenderer.color = c;
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

            if (_isAttacking) return;

            CheckTargeting();
        }

        private void UpdateArticulatedMotion()
        {
            // Dampen recoil offset smoothly
            _cannonRecoilOffset = Vector3.Lerp(_cannonRecoilOffset, Vector3.zero, Time.deltaTime * recoilRecoverSpeed);
            if (cannonPivot != null)
            {
                cannonPivot.localPosition = _initialCannonLocalPos + _cannonRecoilOffset;
                // Elevation rotation: local Z rotation
                // Note: Sprite faces Right (0 deg is horizontal right).
                // Default sprite angle in artwork is ~55 deg.
                // Rotation relative to base:
                float visualAngle = _currentElevationAngle - 55.0f;
                cannonPivot.localRotation = Quaternion.Euler(0, 0, visualAngle);
            }
        }

        private bool PlayerTargetable()
        {
            return _player != null && _player.gameObject.activeInHierarchy && _player.health != null && _player.health.IsAlive;
        }

        private void CheckTargeting()
        {
            if (Time.time < _nextTargetCheck) return;
            _nextTargetCheck = Time.time + TargetCheckInterval;

            if (!PlayerTargetable())
            {
                GoIdle();
                return;
            }

            Vector2 myPos = transform.position;
            Vector2 playerPos = _player.transform.position;
            float diffX = playerPos.x - myPos.x;
            float absDx = Mathf.Abs(diffX);
            float absDy = Mathf.Abs(playerPos.y - myPos.y);

            if (absDx > detectionRadius || absDx < minFiringDistance || absDy > verticalDetectLimit)
            {
                GoIdle();
                return;
            }

            // Face first: flipping moves the muzzle the arc is checked from.
            if (absDx > 0.5f)
            {
                UpdateFacing(diffX > 0);
            }

            Vector2 target = GroundUnderPlayer();
            if (!TryFindFiringSolution(target, out float flightTime))
            {
                // Every arc from here is blocked, by a wall beside the muzzle or by the ceiling.
                GoIdle();
                return;
            }

            StartCoroutine(MortarAttackSequenceRoutine(target, flightTime));
        }

        private void GoIdle()
        {
            HideTelegraph();
            currentState = MortarState.Idle;
        }

        public void UpdateFacing(bool faceRight)
        {
            isFacingRight = faceRight;
            Vector3 s = transform.localScale;
            s.x = isFacingRight ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
            transform.localScale = s;
        }

        private Vector2 MuzzlePosition()
        {
            return muzzlePoint != null ? (Vector2)muzzlePoint.position : (Vector2)transform.position + Vector2.up * 2f;
        }

        // This robot is placed in cover, so one flight time is not enough: the flat arc clips a wall
        // right beside the muzzle while a steeper one clears it, and a steeper one still hits the
        // ceiling. Walk a ladder of arcs and take the first that gets through.
        private bool TryFindFiringSolution(Vector2 target, out float flightTime)
        {
            Vector2 origin = MuzzlePosition();
            float baseTime = FlightTimeTo(target);
            for (int i = 0; i < ArcCandidates.Length; i++)
            {
                float candidate = Mathf.Clamp(baseTime * ArcCandidates[i], MinFlightTime, MaxFlightTime);
                if (ArcIsClear(origin, target, candidate))
                {
                    flightTime = candidate;
                    return true;
                }
            }
            flightTime = baseTime;
            return false;
        }

        private float FlightTimeTo(Vector2 target)
        {
            // Dynamic flight time based on distance
            float dist = Mathf.Abs(target.x - MuzzlePosition().x);
            return Mathf.Clamp(shellFlightTime * (dist / 8.0f), 1.0f, 1.8f);
        }

        private static float ShellGravity => Mathf.Abs(Physics2D.gravity.y * ShellGravityScale);

        private static Vector2 LaunchVelocity(Vector2 origin, Vector2 target, float flightTime)
        {
            float g = ShellGravity;
            return new Vector2((target.x - origin.x) / flightTime,
                               (target.y - origin.y + 0.5f * g * flightTime * flightTime) / flightTime);
        }

        // The floor under the player's feet. The player's transform sits well above them, so aiming
        // at it floats the warning ring in the air and lands the shell beyond it.
        private Vector2 GroundUnderPlayer()
        {
            Bounds b = _player.Bounds;
            Vector2 feet = new Vector2(b.center.x, b.min.y);

            int count = Physics2D.Raycast(feet + Vector2.up * 0.1f, Vector2.down, _terrainFilter, _rayHits, GroundProbeDepth);
            for (int i = 0; i < count; i++)
            {
                if (IsTerrain(_rayHits[i].collider)) return _rayHits[i].point;
            }
            return feet;
        }

        private bool ArcIsClear(Vector2 origin, Vector2 target, float flightTime)
        {
            Vector2 velocity = LaunchVelocity(origin, target, flightTime);
            float g = ShellGravity;
            Vector2 previous = origin;

            for (int step = 1; step <= ArcSamples; step++)
            {
                float t = flightTime * step / ArcSamples;
                Vector2 point = origin + velocity * t + Vector2.down * (0.5f * g * t * t);

                int count = Physics2D.Linecast(previous, point, _terrainFilter, _rayHits);
                for (int i = 0; i < count; i++)
                {
                    if (!IsTerrain(_rayHits[i].collider)) continue;
                    if (Vector2.Distance(_rayHits[i].point, target) > LandingTolerance) return false;
                }
                previous = point;
            }
            return true;
        }

        // Level geometry has no body or a static one; the player, enemies and the mortar itself move.
        private static bool IsTerrain(Collider2D col)
        {
            return col.attachedRigidbody == null || col.attachedRigidbody.bodyType == RigidbodyType2D.Static;
        }

        private IEnumerator MortarAttackSequenceRoutine(Vector2 targetPos, float flightTime)
        {
            _isAttacking = true;
            currentState = MortarState.Aiming;

            // 1. Raise the cannon to roughly the launch angle
            Vector2 velocity = LaunchVelocity(MuzzlePosition(), targetPos, flightTime);
            float launchAngleDeg = Mathf.Atan2(velocity.y, Mathf.Abs(velocity.x)) * Mathf.Rad2Deg;
            _currentElevationAngle = Mathf.Clamp(launchAngleDeg, cannonMinElevation, cannonMaxElevation);

            // 2. Play warning telemetry sound
            if (_audio != null && aimWarningSound != null)
            {
                _audio.pitch = 1.0f;
                _audio.PlayOneShot(aimWarningSound);
            }

            // 3. Telegraph pulse on the floor
            float elapsed = 0f;
            if (_telegraphCircle != null) _telegraphCircle.enabled = true;

            while (elapsed < aimDuration)
            {
                if (_isDetonated || (_health != null && !_health.IsAlive))
                {
                    HideTelegraph();
                    _isAttacking = false;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float progress = elapsed / aimDuration;

                // Telegraph ring radius pulses and contracts
                float radius = Mathf.Lerp(1.6f, 0.9f, progress) + Mathf.Sin(progress * 25f) * 0.1f;
                UpdateTelegraphGeometry(targetPos, radius);

                yield return null;
            }

            HideTelegraph();

            // 4. FIRE!
            currentState = MortarState.Firing;
            ExecuteMortarLaunch(targetPos, flightTime);

            // 5. Reload and cooldown
            currentState = MortarState.Reloading;
            if (_audio != null && reloadSound != null)
            {
                _audio.PlayOneShot(reloadSound);
            }

            yield return new WaitForSeconds(reloadDuration);

            _isAttacking = false;
            currentState = MortarState.Idle;
        }

        private void UpdateTelegraphGeometry(Vector2 center, float radius)
        {
            if (_telegraphCircle == null || !_telegraphCircle.enabled) return;

            int count = _telegraphCircle.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                float x = center.x + Mathf.Cos(angle) * radius;
                float y = center.y + Mathf.Sin(angle) * (radius * 0.35f); // Elliptical ground perspective
                _telegraphCircle.SetPosition(i, new Vector3(x, y, 0));
            }
        }

        private void HideTelegraph()
        {
            if (_telegraphCircle != null)
            {
                _telegraphCircle.enabled = false;
            }
        }

        private void ExecuteMortarLaunch(Vector2 targetPos, float flightTime)
        {
            Vector2 launchOrigin = MuzzlePosition();

            // Audio and Screen Feedback
            if (_audio != null && mortarFireSound != null)
            {
                _audio.pitch = Random.Range(0.88f, 1.05f);
                _audio.PlayOneShot(mortarFireSound);
            }

            // Heavy Muzzle FX
            ExpandingRing.Spawn(launchOrigin, shellColor, 1.5f, 0.22f);
            ProjectileDebris.SpawnSpark(launchOrigin, new Color(1f, 0.8f, 0.3f), 10, 7f);

            // Recoil kick along barrel axis
            if (cannonPivot != null)
            {
                // In local space, barrel points at _currentElevationAngle
                float rad = _currentElevationAngle * Mathf.Deg2Rad;
                Vector3 recoilDir = new Vector3(-Mathf.Cos(rad), -Mathf.Sin(rad), 0) * recoilDistance;
                _cannonRecoilOffset = recoilDir;
            }

            // Fire ballistic cluster mortar shell
            var shell = BossBulletHellEmitter.FireClusterMortar(launchOrigin, targetPos, flightTime, shellColor, clusterFragments, fragmentSpeed);
            shell.damage = directDamage;
        }

        public override bool CanTakeDamage(Vector2 hitPosition, Vector2 attackerPosition)
        {
            float attackDir = attackerPosition.x - transform.position.x;
            bool attackedFromBehind = isFacingRight ? attackDir < 0f : attackDir > 0f;

            if (attackedFromBehind && !_isDetonated)
            {
                DetonateAmmoRack();
                return false; // The cook-off has already destroyed it
            }

            return true;
        }

        /// <summary>
        /// Triggered when the rear autoloader ammo rack is attacked!
        /// Triggers a massive catastrophic secondary explosion that detonates the robot!
        /// </summary>
        public void DetonateAmmoRack()
        {
            if (_isDetonated) return;
            _isDetonated = true;
            StopAllCoroutines();
            HideTelegraph();

            Vector3 ammoPos = weakPoint != null ? weakPoint.position : transform.position + new Vector3(isFacingRight ? -1.18f : 1.18f, 1.19f, 0);

            if (_audio != null && ammoDetonateSound != null)
            {
                _audio.pitch = 0.8f;
                _audio.PlayOneShot(ammoDetonateSound);
            }

            // Triple expanding rings for massive critical feedback
            ExpandingRing.Spawn(ammoPos, new Color(1f, 0.9f, 0.2f), 3.5f, 0.45f);
            ExpandingRing.Spawn(ammoPos, new Color(1f, 0.3f, 0.1f), 2.2f, 0.3f);
            ProjectileDebris.SpawnSpark(ammoPos, new Color(1f, 0.6f, 0.1f), 20, 10f);
            CameraZoomController.TriggerShake(0.4f, 0.2f);

            // Area blast. Each target is struck once however many colliders it has, and the player
            // goes through TakeDamage so invincibility and knockback still apply.
            var struck = new List<EnemyController>();
            bool playerStruck = false;
            foreach (var col in Physics2D.OverlapCircleAll(ammoPos, rackBlastRadius))
            {
                var player = col.GetComponentInParent<PlayerController>();
                if (player != null)
                {
                    if (rackBlastHurtsPlayer && !playerStruck)
                    {
                        playerStruck = true;
                        float away = player.transform.position.x - ammoPos.x;
                        player.TakeDamage(1, new Vector2((away < 0f ? -1f : 1f) * 5f, 5f));
                    }
                    continue;
                }

                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy == null || enemy == this || struck.Contains(enemy)) continue;
                struck.Add(enemy);

                // Reaching zero schedules the enemy's death through HealthIsZero.
                var h = enemy.GetComponent<Health>();
                if (h != null && h.IsAlive)
                {
                    h.Decrement();
                }
            }

            // Immediately destroy/die
            if (_health != null)
            {
                _health.Die();
            }

            StandardExplosion();
        }

        private void StandardExplosion()
        {
            _isDetonated = true;
            HideTelegraph();

            Vector3 center = transform.position + Vector3.up * 1.3f;
            ExpandingRing.Spawn(center, new Color(1f, 0.5f, 0.1f), 2.5f, 0.35f);
            ProjectileDebris.SpawnSpark(center, new Color(1f, 0.8f, 0.2f), 15, 8f);

            // Fade out and destroy
            StartCoroutine(DeathFadeRoutine());
        }

        private IEnumerator DeathFadeRoutine()
        {
            float elapsed = 0f;
            Color initBase = baseRenderer != null ? baseRenderer.color : Color.white;
            Color initCannon = cannonRenderer != null ? cannonRenderer.color : Color.white;

            while (elapsed < 0.6f)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, elapsed / 0.6f);
                if (baseRenderer != null) baseRenderer.color = new Color(initBase.r, initBase.g, initBase.b, a);
                if (cannonRenderer != null) cannonRenderer.color = new Color(initCannon.r, initCannon.g, initCannon.b, a);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
            }
            HideTelegraph();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(detectionRadius * 2f, verticalDetectLimit * 2f, 0f));
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(minFiringDistance * 2f, verticalDetectLimit * 2f, 0f));

            if (weakPoint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(weakPoint.position, rackBlastRadius);
            }
        }

    }
}
