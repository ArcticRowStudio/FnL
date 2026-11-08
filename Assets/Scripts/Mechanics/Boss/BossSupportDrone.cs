using System.Collections;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Mechanics;
using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// Autonomous support escort drone deployed by the VTOL Main Boss.
    /// Provides defensive fire, formation escort, and kamikaze dive in Phase 2.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class BossSupportDrone : MonoBehaviour, IPlayerHittable
    {
        public enum DroneState
        {
            Spawning,
            FormationHover,
            Firing,
            KamikazeDive,
            Destroyed
        }

        [Header("Stats")]
        public int maxHealth = 6;
        public int currentHealth = 6;
        public float moveSpeed = 4.5f;
        public float diveSpeed = 12f;

        [Header("Sprites")]
        public SpriteRenderer spriteRenderer;
        public Sprite phase1Sprite;
        public Sprite phase2Sprite;

        [Header("Rig Parts (Tools/split-vtol-drone-parts.py)")]
        public Sprite fanLeftPhase1;
        public Sprite fanLeftPhase2;
        public Sprite fanRightPhase1;
        public Sprite fanRightPhase2;
        public Sprite gunLeftPhase1;
        public Sprite gunLeftPhase2;
        public Sprite gunRightPhase1;
        public Sprite gunRightPhase2;
        public Sprite antennaPhase1;
        public Sprite antennaPhase2;
        public Sprite[] leftFanFramesPhase1;
        public Sprite[] rightFanFramesPhase1;
        public Sprite[] leftFanFramesPhase2;
        public Sprite[] rightFanFramesPhase2;

        [Header("Rig Motion")]
        public float maxBank = 15f;
        public float maxFanSwivel = 30f;
        public float fanIdleFps = 30f;
        public float fanMaxFps = 56f;

        [Header("Combat")]
        public GameObject projectilePrefab;
        public Transform firePoint;
        public float fireCooldown = 3.0f;
        public int burstCount = 3;
        public float burstInterval = 0.18f;

        [Header("Targeting & Formation")]
        public Transform parentBoss;
        public Vector2 formationOffset = new Vector2(-3.5f, 2.0f);

        private Rigidbody2D _rb;
        private Collider2D _col;
        private Transform _playerTransform;
        private DroneState _state = DroneState.Spawning;
        private float _nextFireTime;
        private float _hoverSeed;
        private bool _isPhase2 = false;
        private Color _baseColor = Color.white;

        // Rig points from Tools/split-vtol-drone-parts.py, in drone units (art faces left, 330 PPU).
        static readonly Vector2 FanLeftJoint = new Vector2(-0.633f, 0.336f);
        static readonly Vector2 FanRightJoint = new Vector2(0.488f, -0.088f);
        static readonly Vector2 GunLeftJoint = new Vector2(-0.527f, -0.467f);
        static readonly Vector2 GunRightJoint = new Vector2(0.200f, -0.512f);
        static readonly Vector2 AntennaJoint = new Vector2(0.042f, 0.736f);
        static readonly Vector2 FanLeftCentre = new Vector2(-0.874f, 0.556f);
        static readonly Vector2 FanRightCentre = new Vector2(0.832f, -0.014f);
        static readonly Vector2 MuzzleLeft = new Vector2(-1.109f, -0.809f);
        static readonly Vector2 MuzzleRight = new Vector2(-0.503f, -1.042f);

        private Transform _fanL, _fanR, _gunL, _gunR, _antenna, _muzzleL, _muzzleR;
        private SpriteRenderer _fanLRenderer, _fanRRenderer, _gunLRenderer, _gunRRenderer, _antennaRenderer, _fanLFrames, _fanRFrames;
        private Vector2 _prevVelocity;
        private float _accelX;
        private float _bank, _bankVelocity;
        private float _swivel, _swivelVelocity;      // world degrees, + = counter-clockwise
        private float _antennaAngle, _antennaVelocity;
        private float _gunAngle;                       // + swings the barrels down
        private float _recoilL, _recoilR;
        private float _fanPhaseL, _fanPhaseR;
        private bool _fireLeftNext = true;

        void Awake()
        {
            CreateRig();
        }

        public void Init(Transform boss, Vector2 offset, bool isPhase2)
        {
            parentBoss = boss;
            formationOffset = offset;
            _isPhase2 = isPhase2;
            _hoverSeed = Random.Range(0f, 100f);

            ApplyPhaseSprites();
        }

        void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _rb.gravityScale = 0f;
            if (spriteRenderer != null) _baseColor = spriteRenderer.color;

            var player = FindFirstObjectByType<PlayerController>();
            if (player != null) _playerTransform = player.transform;

            _nextFireTime = Time.time + Random.Range(1.0f, 2.5f);
            StartCoroutine(SpawnSequence());
        }

        IEnumerator SpawnSequence()
        {
            _state = DroneState.Spawning;
            // Emerge downwards from boss bay
            Vector3 startPos = transform.position;
            Vector3 targetPos = (parentBoss != null) ? parentBoss.position + (Vector3)formationOffset : startPos + Vector3.left * 2f;

            float elapsed = 0f;
            while (elapsed < 0.8f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.8f;
                transform.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            _state = DroneState.FormationHover;
        }

        void Update()
        {
            if (_state == DroneState.Destroyed) return;
            UpdateRig();

            if (_state == DroneState.FormationHover)
            {
                UpdateFormationMovement();

                if (Time.time >= _nextFireTime && _playerTransform != null)
                {
                    StartCoroutine(FireBurstRoutine());
                }
            }
            else if (_state == DroneState.KamikazeDive)
            {
                UpdateKamikazeMovement();
            }
        }

        void UpdateFormationMovement()
        {
            Vector3 targetBase = (parentBoss != null) ? parentBoss.position + (Vector3)formationOffset : transform.position;
            // Sine-wave hover bobbing
            float hoverY = Mathf.Sin((Time.time + _hoverSeed) * 3f) * 0.4f;
            float hoverX = Mathf.Cos((Time.time + _hoverSeed) * 2f) * 0.25f;
            Vector2 targetPos = new Vector2(targetBase.x + hoverX, targetBase.y + hoverY);

            _rb.linearVelocity = (targetPos - _rb.position) * moveSpeed;

            // Face player
            if (_playerTransform != null)
            {
                bool faceLeft = _playerTransform.position.x < transform.position.x;
                if (Mathf.Abs(_playerTransform.position.x - transform.position.x) > 0.6f)
                    transform.localScale = new Vector3(faceLeft ? 1f : -1f, 1f, 1f);
            }
        }

        IEnumerator FireBurstRoutine()
        {
            _state = DroneState.Firing;
            _nextFireTime = Time.time + fireCooldown + Random.Range(0f, 1f);

            for (int i = 0; i < burstCount; i++)
            {
                if (_playerTransform == null || _state == DroneState.Destroyed) yield break;

                // Alternate guns; each shot leaves along its barrel, which is tracking the player.
                bool left = _fireLeftNext;
                _fireLeftNext = !_fireLeftNext;
                Transform gun = left ? _gunL : _gunR;
                Transform muzzle = left ? _muzzleL : _muzzleR;
                Vector2 spawnPos = muzzle != null ? (Vector2)muzzle.position
                    : firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position + Vector2.down * 0.5f;
                Vector2 dir = gun != null && muzzle != null
                    ? ((Vector2)muzzle.position - (Vector2)gun.position).normalized
                    : ((Vector2)_playerTransform.position - spawnPos).normalized;

                // Slight spread
                float spreadAngle = (i - 1) * 6f;
                dir = Quaternion.Euler(0, 0, spreadAngle) * dir;
                if (left) _recoilL = 0.09f; else _recoilR = 0.09f;
                _bankVelocity += (transform.localScale.x > 0f ? -1f : 1f) * 60f;

                SpawnBlasterBullet(spawnPos, dir);
                yield return new WaitForSeconds(burstInterval);
            }

            if (_state != DroneState.Destroyed && _state != DroneState.KamikazeDive)
            {
                _state = DroneState.FormationHover;
            }
        }

        void SpawnBlasterBullet(Vector2 pos, Vector2 dir)
        {
            Color col = _isPhase2 ? new Color(1f, 0.3f, 0.2f) : new Color(0.1f, 0.9f, 1f);
            var proj = BossProjectile.Create(pos, dir, 8.5f, col);
            proj.name = "Drone_Blaster";
            proj.damage = 1;
        }

        public void TriggerKamikazeDive(Transform player)
        {
            if (_state == DroneState.Destroyed) return;
            _playerTransform = player;
            _state = DroneState.KamikazeDive;

            _isPhase2 = true;
            ApplyPhaseSprites();
            StartCoroutine(KamikazeRoutine());
        }

        IEnumerator KamikazeRoutine()
        {
            // Back up slightly with warning alarm
            Vector2 startPos = _rb.position;
            Vector2 prepPos = startPos + Vector2.up * 1.5f;
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                _rb.position = Vector2.Lerp(startPos, prepPos, t / 0.4f);
                yield return null;
            }

            // Dive straight at player's predicted position
            Vector2 target = _playerTransform != null ? (Vector2)_playerTransform.position : _rb.position + Vector2.down * 8f;
            Vector2 diveDir = (target - _rb.position).normalized;

            float diveTimer = 0f;
            while (diveTimer < 2.5f)
            {
                diveTimer += Time.deltaTime;
                _rb.linearVelocity = diveDir * diveSpeed;

                // Spark trail
                ProjectileDebris.SpawnSpark(transform.position, new Color(1f, 0.2f, 0.1f), 2, 3f);
                yield return null;
            }

            Explode();
        }

        void UpdateKamikazeMovement()
        {
            // Handled in coroutine
        }

        public void TakeDamage(int damage)
        {
            if (_state == DroneState.Destroyed) return;

            currentHealth -= damage;
            StartCoroutine(DamageFlash());

            if (currentHealth <= 0)
            {
                Explode();
            }
        }

        public void OnPlayerHit(Vector2 hitPoint)
        {
            TakeDamage(1);
        }

        IEnumerator DamageFlash()
        {
            if (spriteRenderer != null)
            {
                // Over-bright, like the Quadpod's flash: plain white on a white tint shows nothing.
                // Restores the cached colour so overlapping flashes cannot leave it stuck bright.
                SetColor(new Color(2f, 2f, 2f, 1f));
                _bankVelocity += Random.Range(-120f, 120f);
                yield return new WaitForSeconds(0.08f);
                SetColor(_baseColor);
            }
        }

        #region Rig

        void CreateRig()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            int order = spriteRenderer != null ? spriteRenderer.sortingOrder : 14;
            _fanL = CreatePart("Fan_L", transform, FanLeftJoint, order - 3, out _fanLRenderer);
            _fanLFrames = CreateOverlay("FanSpin_L", _fanL, FanLeftCentre - FanLeftJoint, order - 2);
            _gunL = CreatePart("Gun_L", transform, GunLeftJoint, order - 1, out _gunLRenderer);
            _antenna = CreatePart("Antenna", transform, AntennaJoint, order + 1, out _antennaRenderer);
            _gunR = CreatePart("Gun_R", transform, GunRightJoint, order + 1, out _gunRRenderer);
            _fanR = CreatePart("Fan_R", transform, FanRightJoint, order + 2, out _fanRRenderer);
            _fanRFrames = CreateOverlay("FanSpin_R", _fanR, FanRightCentre - FanRightJoint, order + 3);
            _muzzleL = new GameObject("Muzzle_L").transform;
            _muzzleL.SetParent(_gunL, false);
            _muzzleL.localPosition = MuzzleLeft - GunLeftJoint;
            _muzzleR = new GameObject("Muzzle_R").transform;
            _muzzleR.SetParent(_gunR, false);
            _muzzleR.localPosition = MuzzleRight - GunRightJoint;
            _fanPhaseL = Random.value * 6f;
            _fanPhaseR = Random.value * 6f;
            ApplyPhaseSprites();
        }

        Transform CreatePart(string name, Transform parent, Vector2 joint, int order, out SpriteRenderer sr)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = joint;
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            if (spriteRenderer != null)
            {
                sr.sortingLayerID = spriteRenderer.sortingLayerID;
                sr.sharedMaterial = spriteRenderer.sharedMaterial;
            }
            return go.transform;
        }

        SpriteRenderer CreateOverlay(string name, Transform parent, Vector2 offset, int order)
        {
            CreatePart(name, parent, offset, order, out SpriteRenderer sr);
            return sr;
        }

        void ApplyPhaseSprites()
        {
            Set(spriteRenderer, _isPhase2 ? phase2Sprite : phase1Sprite);
            Set(_fanLRenderer, _isPhase2 ? fanLeftPhase2 : fanLeftPhase1);
            Set(_fanRRenderer, _isPhase2 ? fanRightPhase2 : fanRightPhase1);
            Set(_gunLRenderer, _isPhase2 ? gunLeftPhase2 : gunLeftPhase1);
            Set(_gunRRenderer, _isPhase2 ? gunRightPhase2 : gunRightPhase1);
            Set(_antennaRenderer, _isPhase2 ? antennaPhase2 : antennaPhase1);
        }

        static void Set(SpriteRenderer sr, Sprite sprite)
        {
            if (sr != null && sprite != null) sr.sprite = sprite;
        }

        void SetColor(Color c)
        {
            foreach (var sr in new[] { spriteRenderer, _fanLRenderer, _fanRRenderer, _gunLRenderer, _gunRRenderer, _antennaRenderer, _fanLFrames, _fanRFrames })
                if (sr != null) sr.color = c;
        }

        // Same idea as the boss, smaller and twitchier: the body banks into its motion, the fan
        // pods vector their thrust and overshoot, the antenna whips behind, the guns track the player.
        void UpdateRig()
        {
            if (_rb == null) return;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector2 v = _rb.linearVelocity;
            _accelX = Mathf.Lerp(_accelX, (v.x - _prevVelocity.x) / dt, Mathf.Clamp01(dt * 10f));
            _prevVelocity = v;
            float mirror = transform.localScale.x > 0f ? 1f : -1f;

            float bankTarget = Mathf.Clamp(-(v.x * 1.4f + _accelX * 0.25f), -maxBank, maxBank);
            Spring(ref _bank, ref _bankVelocity, bankTarget, 8f, 0.35f, dt);
            transform.rotation = Quaternion.Euler(0f, 0f, _bank);

            float swivelTarget = Mathf.Clamp(-(v.x * 3f + _accelX * 0.9f), -maxFanSwivel, maxFanSwivel) + _bank * 0.3f;
            Spring(ref _swivel, ref _swivelVelocity, swivelTarget, 11f, 0.4f, dt);
            float hunt = (Mathf.PerlinNoise(Time.time * 1.7f, _hoverSeed) - 0.5f) * 5f;
            if (_fanL != null) _fanL.localRotation = Quaternion.Euler(0f, 0f, (_swivel - _bank + hunt) * mirror);
            if (_fanR != null) _fanR.localRotation = Quaternion.Euler(0f, 0f, (_swivel - _bank - hunt) * mirror);

            // Antenna: a loose spring that lags the body's acceleration.
            Spring(ref _antennaAngle, ref _antennaVelocity, Mathf.Clamp(_accelX * 1.2f, -35f, 35f) * mirror, 7f, 0.15f, dt);
            if (_antenna != null) _antenna.localRotation = Quaternion.Euler(0f, 0f, _antennaAngle);

            // Guns: art barrels point along -x; aim at the player within their mount's reach.
            if (_playerTransform != null && _gunL != null)
            {
                Vector2 aim = (Vector2)_playerTransform.position - (Vector2)_gunL.position;
                Vector2 local = Quaternion.Euler(0f, 0f, -_bank) * aim;
                local.x *= mirror;
                float target = Mathf.Clamp(Mathf.Atan2(-local.y, -local.x) * Mathf.Rad2Deg, -20f, 45f);
                _gunAngle = Mathf.MoveTowardsAngle(_gunAngle, target, 160f * dt);
            }
            _recoilL = Mathf.MoveTowards(_recoilL, 0f, 0.8f * dt);
            _recoilR = Mathf.MoveTowards(_recoilR, 0f, 0.8f * dt);
            Quaternion gunRot = Quaternion.Euler(0f, 0f, _gunAngle);
            if (_gunL != null) { _gunL.localRotation = gunRot; _gunL.localPosition = (Vector3)GunLeftJoint + gunRot * Vector3.right * _recoilL; }
            if (_gunR != null) { _gunR.localRotation = gunRot; _gunR.localPosition = (Vector3)GunRightJoint + gunRot * Vector3.right * _recoilR; }

            // Fan spin follows thrust; flat out in the kamikaze dive.
            float thrust = _state == DroneState.KamikazeDive ? 1f
                : Mathf.Clamp01(0.25f + Mathf.Abs(_accelX) * 0.05f + Mathf.Max(0f, v.y) * 0.08f);
            float fps = Mathf.Lerp(fanIdleFps, fanMaxFps, thrust);
            _fanPhaseL += fps * dt;
            _fanPhaseR += fps * dt * 1.07f;
            Frame(_fanLFrames, _isPhase2 ? leftFanFramesPhase2 : leftFanFramesPhase1, _fanPhaseL);
            Frame(_fanRFrames, _isPhase2 ? rightFanFramesPhase2 : rightFanFramesPhase1, _fanPhaseR);
        }

        static void Spring(ref float x, ref float vel, float target, float w, float z, float dt)
        {
            int steps = Mathf.CeilToInt(dt / 0.005f);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                vel += (w * w * (target - x) - 2f * z * w * vel) * h;
                x += vel * h;
            }
        }

        static void Frame(SpriteRenderer sr, Sprite[] frames, float phase)
        {
            if (sr == null || frames == null || frames.Length == 0) return;
            sr.sprite = frames[(int)phase % frames.Length];
        }

        #endregion

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_state == DroneState.Destroyed) return;

            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                if (player.isParrying)
                {
                    player.OnParrySuccess(gameObject);
                    TakeDamage(4);
                    return;
                }

                if (!player.isInvincible)
                {
                    Vector2 knockback = (player.transform.position - transform.position).normalized * 5f;
                    player.TakeDamage(1, knockback);
                }

                if (_state == DroneState.KamikazeDive)
                {
                    Explode();
                }
            }
            else if (!other.isTrigger && _state == DroneState.KamikazeDive)
            {
                Explode();
            }
        }

        public void Explode()
        {
            if (_state == DroneState.Destroyed) return;
            _state = DroneState.Destroyed;

            CameraZoomController.TriggerShake(0.3f, 0.25f);
            ExpandingRing.Spawn(transform.position, _isPhase2 ? new Color(1f, 0.3f, 0.1f) : new Color(0.2f, 0.8f, 1f), 3f, 0.35f);
            ProjectileDebris.SpawnSpark(transform.position, new Color(1f, 0.5f, 0.1f), 18, 8f);

            Destroy(gameObject, 0.05f);
        }
    }
}
