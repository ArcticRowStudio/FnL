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
    /// Arachne-MK4 Heavy Quadpod Boss AI Controller.
    /// Pure 2D side-scrolling platformer boss faithful to custom tank-turret concept sketch.
    /// Features:
    /// - Massive 4-legged heavy mechanical walker chassis with custom walk animation
    /// - Upper tank turret assembly that rotates independently to track and aim at the player in real-time
    /// - Real-time aiming during ground combat AND while clinging 90-degrees to walls in Phase 2
    /// - Cannon muzzle-aligned bullet hell attacks: Gatling spread, Cluster mortar, Wall curtain, EMP ring
    /// - Mechanical recoil kickback on cannon firing
    /// - 2-phase escalation: Overclocked crimson plasma, faster tracking, wall triangle leaps
    /// </summary>
    [RequireComponent(typeof(Health), typeof(Collider2D))]
    public class QuadpodBossAI : EnemyController, IBossEncounter
    {
        public enum BossState
        {
            Intro,
            GroundCombat,
            PrepareJump,
            Leaping,
            WallCling,
            GroundSlam,
            Recovering,
            PhaseTransition,
            Defeated
        }

        [Header("Hierarchy & Turret Assembly")]
        public Transform turretPivot;
        public Transform muzzlePoint;
        public SpriteRenderer turretRenderer;
        public SpriteRenderer baseRenderer;
        public Transform radarPivot;
        public SpriteRenderer radarRenderer;

        [Header("Sprites & Phases")]
        public Sprite phase1Sprite; // legacy fallback
        public Sprite phase2Sprite; // legacy fallback
        public Sprite turretPhase1Sprite;
        public Sprite turretPhase2Sprite;
        public Sprite basePhase1Sprite;
        public Sprite basePhase2Sprite;
        public bool isPhase2 = false;
        public bool isInvulnerable = false;
        public Color phase2GlowColor = new Color(1f, 0.45f, 0.25f, 1f);

        [Header("Turret Tracking & Aiming")]
        public float turretAimSpeed = 180f;
        public float maxAimAngleGround = 60f;
        public bool isFacingLeft = true;

        [Header("Arena Bounds")]
        public float arenaLeft = 68.5f;
        public float arenaRight = 91.5f;
        public float arenaFloorY = -6.3f;
        public float wallClingHeight = 0.5f;

        [Header("Pilot")]
        [Tooltip("On: the pilot climbs out of the turret hatch during the defeat and runs off before the last blast")]
        public bool pilotEjectsOnDefeat = true;
        [Tooltip("The hatch on the turret art, in the turret sprite's local space as drawn facing left")]
        public Vector2 pilotHatchOffset = new Vector2(1.04f, 1.38f);
        [Tooltip("Hop Away: the pilot springs out of the hatch and bounds off to the far pillar. Eject Capsule: the turret's upper rear housing fires off with the pilot in it. Climb Out: the pilot scrambles out and runs")]
        public PilotExitStyle pilotExitStyle = PilotExitStyle.HopAway;

        [Header("Phase 2 Wall Climb")]
        [Tooltip("x of the climbable pillar faces either side of the arena, measured off the level collision.")]
        public float leftWallFaceX = 63.76f;
        public float rightWallFaceX = 94.96f;
        [Tooltip("Outer edge of the lip under each pillar. The leap stays beyond it until the body is above the lip.")]
        public float leftLedgeEdgeX = 66.72f;
        public float rightLedgeEdgeX = 93.12f;
        [Tooltip("Range the pivot crawls over on a pillar. The bottom keeps the sideways body above the lip, the top keeps it below the pillar top.")]
        public float wallClimbMinY = 6.7f;
        public float wallClimbMaxY = 12.7f;
        public float wallClimbSpeed = 2.6f;
        public float wallClimbDuration = 5.0f;
        public float wallClimbFireInterval = 1.1f;
        public float wallLeapDuration = 0.9f;

        [Header("Speeds & Physics")]
        public float groundSpeedPhase1 = 2.8f;
        public float groundSpeedPhase2 = 4.2f;
        public float jumpVelocityY = 13.5f;
        public float jumpVelocityX = 8.5f;
        public float slamSpeed = 22f;

        [Header("Combat Timers")]
        public float attackCooldownPhase1 = 2.2f;
        public float attackCooldownPhase2 = 1.2f;
        public float wallClingDurationPhase1 = 3.0f;
        public float wallClingDurationPhase2 = 1.8f;
        public float slamRecoveryPhase1 = 0.8f;
        public float slamRecoveryPhase2 = 0.35f;

        [Header("Audio Clips")]
        public AudioClip leapSound;
        public AudioClip landingSound;
        public AudioClip gatlingSound;
        public AudioClip sparkArcSound;
        public AudioClip phaseTransitionSound;

        [Header("State Tracking")]
        public BossState currentState = BossState.Intro;
        public bool autoEngage = true;

        [Header("Walk Cycle Animation")]
        public Sprite[] walkFramesPhase1;
        public Sprite[] walkFramesPhase2;
        public float walkAnimFps = 8f;

        [Header("Radar Animation & Scanner")]
        public Sprite[] radarSpinFramesPhase1;
        public Sprite[] radarSpinFramesPhase2;
        public float radarRps = 1.2f;

        private Health _bossHealth;
        private Rigidbody2D _rb;
        private Collider2D _col;
        private PlayerController _player;
        private BossHealthBarUI _healthBar;

        private float _nextActionTime = 0f;
        private float _stateTimer = 0f;
        private float _walkCycleProgress = 0f;
        private int _lastWalkFrame = -1;
        private float _radarSpinProgress = 0f;
        private int _lastRadarFrame = -1;
        private Vector2 _leapTarget;
        private bool _isAttachedToLeftWall = false;
        private int _patrolDir = 1;
        private int _consecutiveWallJumps = 0;
        private Vector3 _originalScale;
        private bool _hasLandedIntro = false;

        private BoxCollider2D _box;
        private Vector2 _boxSize;
        private Vector2 _boxOffset;
        private ContactFilter2D _groundFilter;
        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[8];
        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private float _recoilOffset = 0f;

        // Gap left between a pillar face and the sideways body clinging to it.
        private const float WallGap = 0.05f;
        private bool _onRealWall = false;
        private bool _scriptedMove = false;
        private bool _leapFromWall = false;
        private float _climbDir = 1f;
        private float _climbFireTimer = 0f;
        private bool _deathSequenceDone = false;
        private Vector3 _homePosition;
        private bool _barShown;
        private float _barHoldUntil;

        // The bar is up while the player is in the fight (the same range ground combat chases in,
        // with some slack before it drops) and for a few seconds after any hit, so long-range
        // sniper damage still shows.
        private const float BarShowDx = 22f, BarShowDy = 12f, BarHideDx = 27f, BarHideDy = 15f;
        private const float BarHoldAfterHit = 5f;

        // Hold off EnemyDeath (scheduled by whichever weapon lands the last hit) until the defeat
        // sequence has played; otherwise it destroys the boss 0.12 s into the explosions.
        public override bool DefersDeath => currentState == BossState.Defeated && !_deathSequenceDone;

        protected override void Awake()
        {
            base.Awake();
            ResetRuntimeState();

            _bossHealth = GetComponent<Health>();
            _col = GetComponent<Collider2D>();
            _originalScale = transform.localScale;
            _homePosition = transform.position;

            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 2.0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _box = _col as BoxCollider2D;
            if (_box != null)
            {
                _boxSize = _box.size;
                _boxOffset = _box.offset;
            }

            _groundFilter.useTriggers = false;
            _groundFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));

            if (_bossHealth != null)
            {
                // Without domain reload the handler from the last session is still attached.
                _bossHealth.OnHealthChanged -= HandleHealthChanged;
                _bossHealth.OnHealthChanged += HandleHealthChanged;
            }

            // Disable standard patrol AnimationController since Boss uses custom state machine & sprites
            if (control != null) control.enabled = false;
            var anim = GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            SetupHierarchy();
        }

        // Domain and scene reload are off in this project: entering play mode reuses this instance,
        // so private fields keep whatever the last session (stopped mid-leap, say) left in them, and
        // field initializers do not run again. Reset everything the fight changes here.
        private void ResetRuntimeState()
        {
            currentState = BossState.Intro;
            isPhase2 = false;
            isInvulnerable = false;
            _nextActionTime = 0f;
            _stateTimer = 0f;
            _walkCycleProgress = 0f;
            _lastWalkFrame = -1;
            _radarSpinProgress = 0f;
            _lastRadarFrame = -1;
            _leapTarget = Vector2.zero;
            _isAttachedToLeftWall = false;
            _patrolDir = 1;
            _consecutiveWallJumps = 0;
            _hasLandedIntro = false;
            _recoilOffset = 0f;
            _onRealWall = false;
            _scriptedMove = false;
            _leapFromWall = false;
            _climbDir = 1f;
            _climbFireTimer = 0f;
            _deathSequenceDone = false;
        }

        private void SetupHierarchy()
        {
            // Backward compatibility
            if (basePhase1Sprite == null && phase1Sprite != null) basePhase1Sprite = phase1Sprite;
            if (basePhase2Sprite == null && phase2Sprite != null) basePhase2Sprite = phase2Sprite;

            // 1. Lower Base Renderer
            if (baseRenderer == null)
            {
                var br = transform.Find("LowerBase");
                if (br != null) baseRenderer = br.GetComponent<SpriteRenderer>();
                if (baseRenderer == null)
                {
                    baseRenderer = GetComponent<SpriteRenderer>();
                    if (baseRenderer == null)
                    {
                        GameObject bObj = new GameObject("LowerBase");
                        bObj.transform.SetParent(transform, false);
                        bObj.transform.localPosition = Vector3.zero;
                        baseRenderer = bObj.AddComponent<SpriteRenderer>();
                    }
                }
            }
            if (baseRenderer != null)
            {
                baseRenderer.sortingOrder = 10;
                if (baseRenderer.sprite == null && basePhase1Sprite != null)
                {
                    baseRenderer.sprite = basePhase1Sprite;
                }
            }

            // 2. Turret Pivot
            if (turretPivot == null)
            {
                var tp = transform.Find("TurretPivot");
                if (tp != null) turretPivot = tp;
                else
                {
                    GameObject pObj = new GameObject("TurretPivot");
                    pObj.transform.SetParent(transform, false);
                    pObj.transform.localPosition = new Vector3(0f, 2.80f, 0f);
                    turretPivot = pObj.transform;
                }
            }

            // 3. Turret Renderer
            if (turretRenderer == null)
            {
                var tr = turretPivot.GetComponentInChildren<SpriteRenderer>();
                if (tr != null) turretRenderer = tr;
                else
                {
                    GameObject tSpriteObj = new GameObject("UpperTurret");
                    tSpriteObj.transform.SetParent(turretPivot, false);
                    tSpriteObj.transform.localPosition = Vector3.zero;
                    turretRenderer = tSpriteObj.AddComponent<SpriteRenderer>();
                }
            }
            if (turretRenderer != null)
            {
                turretRenderer.sortingOrder = 12;
                if (turretRenderer.sprite == null && turretPhase1Sprite != null)
                {
                    turretRenderer.sprite = turretPhase1Sprite;
                }
            }

            // 4. Muzzle Point
            if (muzzlePoint == null)
            {
                var mp = turretPivot.Find("MuzzlePoint");
                if (mp != null) muzzlePoint = mp;
                else
                {
                    GameObject mObj = new GameObject("MuzzlePoint");
                    mObj.transform.SetParent(turretPivot, false);
                    mObj.transform.localPosition = new Vector3(-3.092f, 0.454f, 0f);
                    muzzlePoint = mObj.transform;
                }
            }

            // 5. Radar Assembly
            if (radarPivot == null && turretPivot != null)
            {
                var rp = turretPivot.Find("RadarPivot");
                if (rp != null) radarPivot = rp;
                else
                {
                    GameObject rPivotObj = new GameObject("RadarPivot");
                    rPivotObj.transform.SetParent(turretPivot, false);
                    rPivotObj.transform.localPosition = new Vector3(isFacingLeft ? 1.150f : -1.150f, 1.377f, 0f);
                    radarPivot = rPivotObj.transform;
                }
            }

            if (radarRenderer == null && radarPivot != null)
            {
                radarRenderer = radarPivot.GetComponentInChildren<SpriteRenderer>();
                if (radarRenderer == null)
                {
                    GameObject rSpriteObj = new GameObject("RadarScanner");
                    rSpriteObj.transform.SetParent(radarPivot, false);
                    rSpriteObj.transform.localPosition = Vector3.zero;
                    radarRenderer = rSpriteObj.AddComponent<SpriteRenderer>();
                }
            }
            if (radarRenderer != null)
            {
                radarRenderer.sortingOrder = 13;
                if (radarRenderer.sprite == null && radarSpinFramesPhase1 != null && radarSpinFramesPhase1.Length > 0)
                {
                    radarRenderer.sprite = radarSpinFramesPhase1[0];
                }
            }

            ApplyFacing();
        }

        private void Start()
        {
            CreateHealthBar();

            // Start Intro
            currentState = BossState.Intro;
            _stateTimer = 0.5f;

            if (CheckGrounded())
            {
                CompleteIntroLanding();
            }
        }

        // Drops any bar an earlier play session (or an earlier attempt) left behind.
        private void CreateHealthBar()
        {
            if (_healthBar != null) Destroy(_healthBar.gameObject);
            GameObject barObj = new GameObject("QuadpodBoss_HealthBarUI");
            _healthBar = barObj.AddComponent<BossHealthBarUI>();
            _healthBar.Bind(this, _bossHealth);
            _barShown = false;
            _barHoldUntil = 0f;
            _healthBar.SetVisible(false, instant: true);
        }

        private void UpdateHealthBarVisibility()
        {
            if (_healthBar == null) return;
            bool show = _barShown;
            if (_player != null)
            {
                float dx = Mathf.Abs(_player.transform.position.x - transform.position.x);
                float dy = Mathf.Abs(_player.transform.position.y - transform.position.y);
                if (dx < BarShowDx && dy < BarShowDy) show = true;
                else if (dx > BarHideDx || dy > BarHideDy) show = false;
            }
            _barShown = show;
            _healthBar.SetVisible(show || Time.time < _barHoldUntil || currentState == BossState.Defeated);
        }

        /// <summary>
        /// Called when the player respawns. A damaged Quadpod goes back to its starting spot at
        /// full health and phase 1, already landed so the intro slam does not replay; a defeated
        /// one stays gone.
        /// </summary>
        public void ResetEncounter()
        {
            if (currentState == BossState.Defeated || _bossHealth == null) return;
            if (_bossHealth.CurrentHP >= _bossHealth.maxHP && !isPhase2) return;

            StopAllCoroutines();
            CameraZoomController.ClearFocus(_col);
            ReleaseFromWall();
            ResetRuntimeState();
            _bossHealth.ResetHP();
            StopAllCoroutines();   // ResetHP raised OnHealthChanged, which starts a damage flash

            if (baseRenderer != null)
            {
                if (basePhase1Sprite != null) baseRenderer.sprite = basePhase1Sprite;
                baseRenderer.color = Color.white;
            }
            if (turretRenderer != null)
            {
                if (turretPhase1Sprite != null) turretRenderer.sprite = turretPhase1Sprite;
                turretRenderer.color = Color.white;
            }
            if (radarRenderer != null)
            {
                if (radarSpinFramesPhase1 != null && radarSpinFramesPhase1.Length > 0)
                    radarRenderer.sprite = radarSpinFramesPhase1[0];
                radarRenderer.color = Color.white;
            }

            SetSquash(1f, 1f);
            if (_col != null) _col.enabled = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 2.0f;
            _rb.linearVelocity = Vector2.zero;
            _rb.rotation = 0f;
            _rb.position = _homePosition;
            transform.SetPositionAndRotation(_homePosition, Quaternion.identity);

            CreateHealthBar();

            _hasLandedIntro = true;
            currentState = BossState.GroundCombat;
            _nextActionTime = Time.time + 1.5f;
        }

        private void OnDestroy()
        {
            if (_bossHealth != null)
            {
                _bossHealth.OnHealthChanged -= HandleHealthChanged;
            }
            CameraZoomController.ClearFocus(_col);
        }

        private void HandleHealthChanged(Health h)
        {
            if (h == null || currentState == BossState.Defeated) return;

            if (h.CurrentHP < h.maxHP) _barHoldUntil = Time.time + BarHoldAfterHit;
            StartCoroutine(DamageFlashRoutine());

            if (!isPhase2 && h.CurrentHP <= (h.maxHP / 2))
            {
                TriggerPhaseTransition();
                return;
            }

            if (!h.IsAlive)
            {
                TriggerDefeat();
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            Color baseCol = isPhase2 ? phase2GlowColor : Color.white;
            if (baseRenderer != null) baseRenderer.color = new Color(2f, 2f, 2f, 1f);
            if (turretRenderer != null) turretRenderer.color = new Color(2f, 2f, 2f, 1f);
            yield return new WaitForSeconds(0.08f);
            if (baseRenderer != null) baseRenderer.color = baseCol;
            if (turretRenderer != null) turretRenderer.color = baseCol;
        }

        protected override void Update()
        {
            FindPlayer();
            UpdateHealthBarVisibility();

            if (currentState == BossState.Defeated) return;

            switch (currentState)
            {
                case BossState.Intro:
                    UpdateIntro();
                    break;

                case BossState.GroundCombat:
                    UpdateGroundCombat();
                    break;

                case BossState.PrepareJump:
                    break;

                case BossState.Leaping:
                    UpdateLeaping();
                    break;

                case BossState.WallCling:
                    UpdateWallCling();
                    break;

                case BossState.GroundSlam:
                    UpdateGroundSlam();
                    break;

                case BossState.Recovering:
                    UpdateRecovering();
                    break;

                case BossState.PhaseTransition:
                    break;
            }

            UpdateFacing();
            UpdateTurretAim();
            UpdateWalkAnimation();
            UpdateRadarAnimation();
            UpdateRecoil();
        }

        private void ApplyFacing()
        {
            if (baseRenderer != null)
            {
                baseRenderer.flipX = !isFacingLeft;
            }
            if (turretRenderer != null)
            {
                turretRenderer.flipX = !isFacingLeft;
            }
            if (radarPivot != null)
            {
                radarPivot.localPosition = new Vector3(isFacingLeft ? 1.150f : -1.150f, 1.377f, 0f);
            }
            if (muzzlePoint != null)
            {
                muzzlePoint.localPosition = new Vector3(isFacingLeft ? -3.092f : 3.092f, 0.454f, 0f);
            }
        }

        private void UpdateFacing()
        {
            if (currentState == BossState.GroundCombat)
            {
                if (_player != null)
                {
                    float dx = _player.transform.position.x - transform.position.x;
                    if (Mathf.Abs(dx) > 0.4f)
                    {
                        isFacingLeft = dx < 0f;
                    }
                }
                else
                {
                    isFacingLeft = _patrolDir < 0;
                }
            }
            else if (currentState == BossState.WallCling)
            {
                // Clinging to wall: face inward towards arena
                isFacingLeft = !_isAttachedToLeftWall;
            }

            ApplyFacing();
        }

        private void UpdateTurretAim()
        {
            if (turretPivot == null) return;
            if (currentState == BossState.Defeated) return;

            if (_player != null)
            {
                Vector2 turretPos = turretPivot.position;
                Vector2 targetPos = _player.transform.position;
                Vector2 aimDir = (targetPos - turretPos).normalized;

                if (aimDir.sqrMagnitude > 0.001f)
                {
                    float aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

                    if (currentState == BossState.WallCling)
                    {
                        // When attached to wall: wide aiming range to aim directly at player on arena floor
                        float diff = isFacingLeft ? Mathf.DeltaAngle(180f, aimAngle) : Mathf.DeltaAngle(0f, aimAngle);
                        diff = Mathf.Clamp(diff, -85f, 85f);
                        Quaternion targetRot = Quaternion.Euler(0f, 0f, diff);
                        turretPivot.rotation = Quaternion.RotateTowards(turretPivot.rotation, targetRot, turretAimSpeed * Time.deltaTime);
                    }
                    else
                    {
                        // Ground combat aiming
                        float diff = isFacingLeft ? Mathf.DeltaAngle(180f, aimAngle) : Mathf.DeltaAngle(0f, aimAngle);
                        diff = Mathf.Clamp(diff, -maxAimAngleGround, maxAimAngleGround);
                        Quaternion targetRot = Quaternion.Euler(0f, 0f, diff);
                        turretPivot.rotation = Quaternion.RotateTowards(turretPivot.rotation, targetRot, turretAimSpeed * Time.deltaTime);
                    }
                }
            }
            else
            {
                turretPivot.rotation = Quaternion.RotateTowards(turretPivot.rotation, Quaternion.identity, turretAimSpeed * 0.5f * Time.deltaTime);
            }
        }

        public void TriggerRecoil(float amount = 0.25f)
        {
            _recoilOffset = amount;
        }

        private void UpdateRecoil()
        {
            if (turretRenderer == null) return;

            if (_recoilOffset > 0.001f)
            {
                _recoilOffset = Mathf.MoveTowards(_recoilOffset, 0f, 1.8f * Time.deltaTime);
                float recoilX = isFacingLeft ? _recoilOffset : -_recoilOffset;
                turretRenderer.transform.localPosition = new Vector3(recoilX, 0f, 0f);
            }
            else
            {
                turretRenderer.transform.localPosition = Vector3.zero;
            }
        }

        private void UpdateWalkAnimation()
        {
            if (baseRenderer == null) return;

            Sprite[] frames = isPhase2 ? walkFramesPhase2 : walkFramesPhase1;
            if (frames == null || frames.Length == 0) return;

            bool crawling = currentState == BossState.WallCling && _onRealWall;
            if (currentState == BossState.GroundCombat || crawling)
            {
                float strideSpeed = crawling ? Mathf.Abs(_rb.linearVelocity.y) : Mathf.Abs(_rb.linearVelocity.x);
                if (strideSpeed > 0.15f)
                {
                    _walkCycleProgress += strideSpeed * Time.deltaTime * walkAnimFps * 0.4f;
                    int frameIndex = ((int)_walkCycleProgress) % frames.Length;

                    if (frameIndex != _lastWalkFrame)
                    {
                        _lastWalkFrame = frameIndex;
                        if (frames[frameIndex] != null)
                        {
                            baseRenderer.sprite = frames[frameIndex];
                        }

                        if (frameIndex == 1 || frameIndex == 3)
                        {
                            float footX = isFacingLeft ? -2.2f : 2.2f;
                            Vector3 footPos = transform.position + transform.rotation * new Vector3(footX, 0.1f, 0f);
                            ProjectileDebris.SpawnSpark(footPos, new Color(1f, 0.85f, 0.3f), 4, 2.5f);
                        }
                    }
                }
                else
                {
                    _lastWalkFrame = 0;
                    if (frames[0] != null) baseRenderer.sprite = frames[0];
                }
            }
            else if (currentState == BossState.Leaping)
            {
                if (frames.Length > 1 && frames[1] != null) baseRenderer.sprite = frames[1];
            }
            else if (currentState == BossState.Recovering || currentState == BossState.PrepareJump)
            {
                if (frames.Length > 2 && frames[2] != null) baseRenderer.sprite = frames[2];
            }
        }

        private void UpdateRadarAnimation()
        {
            if (radarRenderer == null) return;

            Sprite[] frames = isPhase2 ? radarSpinFramesPhase2 : radarSpinFramesPhase1;
            if (frames == null || frames.Length == 0) return;

            float speedMult = isPhase2 ? 1.8f : 1.0f;
            // Accelerate spin when clinging to wall or firing EMP barrage
            if (currentState == BossState.WallCling) speedMult *= 1.5f;

            _radarSpinProgress += Time.deltaTime * radarRps * 8f * speedMult;
            int frameIndex = ((int)_radarSpinProgress) % frames.Length;

            if (frameIndex != _lastRadarFrame)
            {
                _lastRadarFrame = frameIndex;
                if (frames[frameIndex] != null)
                {
                    radarRenderer.sprite = frames[frameIndex];
                }
            }
        }

        private void FindPlayer()
        {
            if (_player == null)
            {
                var model = Simulation.GetModel<PlatformerModel>();
                if (model != null) _player = model.player;
                if (_player == null) _player = FindAnyObjectByType<PlayerController>();
            }
        }

        private bool CheckGrounded()
        {
            if (_rb == null) return true;

            // Sweep the body itself. A ray cast from inside the collider hits the boss first, so it
            // never saw the floor and grounding fell back on a hardcoded height.
            int count = _rb.Cast(Vector2.down, _groundFilter, _groundHits, 0.1f);
            for (int i = 0; i < count; i++)
            {
                var hit = _groundHits[i];
                bool isTerrain = hit.rigidbody == null || hit.rigidbody.bodyType == RigidbodyType2D.Static;
                if (isTerrain && hit.normal.y > 0.65f) return true;
            }
            return false;
        }

        #region State Updates

        private void UpdateIntro()
        {
            _stateTimer -= Time.deltaTime;

            if (!_hasLandedIntro && (CheckGrounded() || _stateTimer <= 0f))
            {
                CompleteIntroLanding();
            }
        }

        private void CompleteIntroLanding()
        {
            _hasLandedIntro = true;
            _rb.linearVelocity = Vector2.zero;
            _rb.gravityScale = 2.0f;

            OnLandImpact();

            currentState = BossState.GroundCombat;
            _nextActionTime = Time.time + 1.0f;
        }

        private void UpdateGroundCombat()
        {
            float moveSpeed = isPhase2 ? groundSpeedPhase2 : groundSpeedPhase1;
            bool playerNearby = false;
            float dirToPlayer = _patrolDir;

            if (_player != null)
            {
                float dx = _player.transform.position.x - transform.position.x;
                float dy = Mathf.Abs(_player.transform.position.y - transform.position.y);
                float dist = Mathf.Abs(dx);

                if (dist < 22f && dy < 12f)
                {
                    playerNearby = true;
                    dirToPlayer = Mathf.Sign(dx);

                    if (dist < 3.0f)
                    {
                        _rb.linearVelocity = new Vector2(-dirToPlayer * moveSpeed, _rb.linearVelocity.y);
                    }
                    else if (dist > 7.5f)
                    {
                        _rb.linearVelocity = new Vector2(dirToPlayer * moveSpeed, _rb.linearVelocity.y);
                    }
                    else
                    {
                        _rb.linearVelocity = new Vector2(dirToPlayer * moveSpeed * 0.5f, _rb.linearVelocity.y);
                    }
                }
            }

            if (!playerNearby)
            {
                if (transform.position.x <= arenaLeft + 1.5f)
                {
                    _patrolDir = 1;
                }
                else if (transform.position.x >= arenaRight - 1.5f)
                {
                    _patrolDir = -1;
                }

                _rb.linearVelocity = new Vector2(_patrolDir * moveSpeed * 0.7f, _rb.linearVelocity.y);
            }

            ClampArenaPosition();

            if (Time.time >= _nextActionTime)
            {
                DecideGroundAction(playerNearby, dirToPlayer);
            }
        }

        private void DecideGroundAction(bool playerNearby, float dirX)
        {
            float rand = Random.value;

            if (rand < 0.40f)
            {
                StartCoroutine(ExecuteGatlingPattern(dirX));
                _nextActionTime = Time.time + (isPhase2 ? attackCooldownPhase2 : attackCooldownPhase1);
            }
            else if (rand < 0.70f)
            {
                ExecuteClusterMortar();
                _nextActionTime = Time.time + (isPhase2 ? attackCooldownPhase2 : attackCooldownPhase1);
            }
            else
            {
                if (Random.value < 0.65f)
                {
                    StartCoroutine(isPhase2 ? PrepareAndClimbWall() : PrepareAndJumpToWall());
                }
                else
                {
                    StartCoroutine(PrepareAndLeapSlam());
                }
            }
        }

        private void UpdateLeaping()
        {
            // A coroutine is flying the boss onto a pillar.
            if (_scriptedMove) return;

            if (_leapTarget.y >= wallClingHeight - 0.5f)
            {
                bool reachedWallX = (_isAttachedToLeftWall && transform.position.x <= arenaLeft + 1.0f) ||
                                   (!_isAttachedToLeftWall && transform.position.x >= arenaRight - 1.0f);

                if (reachedWallX && transform.position.y >= wallClingHeight - 1.2f)
                {
                    AttachToWall();
                }
                else if (_rb.linearVelocity.y <= 0f && CheckGrounded())
                {
                    // Something solid cut the leap short and it came down without reaching the wall.
                    currentState = BossState.GroundCombat;
                    _nextActionTime = Time.time + 0.5f;
                }
            }
            else
            {
                // Off a pillar the boss starts above the lip, so hold the plunge until it is over the
                // target, or it comes straight down onto the lip instead of the arena floor.
                bool overTarget = !_leapFromWall || Mathf.Abs(transform.position.x - _leapTarget.x) < 0.75f;
                if (overTarget && (transform.position.y >= _leapTarget.y + 4.5f || _rb.linearVelocity.y < 0.1f))
                {
                    currentState = BossState.GroundSlam;
                    _rb.linearVelocity = new Vector2(0f, -slamSpeed);
                }
                else if (_rb.linearVelocity.y <= 0f && CheckGrounded())
                {
                    LandSlam();
                }
            }
        }

        private void UpdateWallCling()
        {
            if (_onRealWall)
            {
                // Crawl up and down the pillar, turning at either end of the climbable stretch.
                float y = _rb.position.y;
                if (y >= wallClimbMaxY) _climbDir = -1f;
                else if (y <= wallClimbMinY) _climbDir = 1f;
                _rb.linearVelocity = new Vector2(0f, _climbDir * wallClimbSpeed);

                _climbFireTimer -= Time.deltaTime;
                if (_climbFireTimer <= 0f)
                {
                    _climbFireTimer = wallClimbFireInterval;
                    StartCoroutine(ExecuteGatlingPattern(isFacingLeft ? -1f : 1f));
                }
            }
            else
            {
                _rb.linearVelocity = Vector2.zero;
            }

            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                DetachFromWall();
            }
        }

        private void UpdateGroundSlam()
        {
            // The solver kills the plunge when anything solid stops it. Landing on that too keeps the
            // slam from pressing forever into something that isn't level floor, like the player.
            bool blocked = _rb.linearVelocity.y > -1f;
            if (CheckGrounded() || blocked)
            {
                LandSlam();
                return;
            }

            _rb.linearVelocity = new Vector2(0f, -slamSpeed);
        }

        private void UpdateRecovering()
        {
            _rb.linearVelocity = Vector2.zero;
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                currentState = BossState.GroundCombat;
                _nextActionTime = Time.time + 0.5f;
            }
        }

        #endregion

        #region Actions & Coroutines

        private IEnumerator ExecuteGatlingPattern(float dirX)
        {
            int bursts = isPhase2 ? 6 : 4;
            int countPerBurst = isPhase2 ? 5 : 3;
            float spread = isPhase2 ? 26f : 18f;
            float bulletSpeed = isPhase2 ? 13f : 9.5f;
            Color bulletColor = isPhase2 ? new Color(1f, 0.25f, 0.1f) : new Color(1f, 0.8f, 0.2f);

            for (int b = 0; b < bursts; b++)
            {
                if (currentState != BossState.GroundCombat && currentState != BossState.WallCling) yield break;

                Vector2 muzzlePos = muzzlePoint != null ? (Vector2)muzzlePoint.position : (turretPivot != null ? (Vector2)turretPivot.position : (Vector2)transform.position);
                Vector2 fireDir;
                if (_player != null)
                {
                    fireDir = ((Vector2)_player.transform.position - muzzlePos).normalized;
                }
                else if (muzzlePoint != null && turretPivot != null)
                {
                    fireDir = ((Vector2)muzzlePoint.position - (Vector2)turretPivot.position).normalized;
                }
                else
                {
                    fireDir = isFacingLeft ? Vector2.left : Vector2.right;
                }

                BossBulletHellEmitter.FireGatlingFan(muzzlePos, fireDir, countPerBurst, spread, bulletSpeed, bulletColor);
                TriggerRecoil(0.22f);

                if (_audio != null && gatlingSound != null)
                {
                    _audio.pitch = Random.Range(0.95f, 1.15f);
                    _audio.PlayOneShot(gatlingSound, 0.6f);
                }

                yield return new WaitForSeconds(0.12f);
            }
        }

        private void ExecuteClusterMortar()
        {
            Vector2 muzzlePos = muzzlePoint != null ? (Vector2)muzzlePoint.position : (Vector2)turretPivot.position;
            Vector2 target = _player != null ? (Vector2)_player.transform.position : (Vector2)transform.position + new Vector2((isFacingLeft ? -1f : 1f) * 8f, 0f);
            Color shellColor = isPhase2 ? new Color(1f, 0.2f, 0.1f) : new Color(1f, 0.6f, 0.15f);

            BossBulletHellEmitter.FireClusterMortar(muzzlePos, target, timeToTarget: 1.1f, shellColor);
            TriggerRecoil(0.35f);

            if (_audio != null && leapSound != null)
            {
                _audio.pitch = 0.7f;
                _audio.PlayOneShot(leapSound, 0.8f);
            }
        }

        private IEnumerator PrepareAndJumpToWall()
        {
            currentState = BossState.PrepareJump;
            _rb.linearVelocity = Vector2.zero;

            SetSquash(1.25f, 0.75f);
            ProjectileDebris.SpawnSpark(transform.position, Color.yellow, 8, 4f);

            yield return new WaitForSeconds(0.25f);

            SetSquash(1f, 1f);

            if (_player != null)
            {
                _isAttachedToLeftWall = _player.transform.position.x > (arenaLeft + arenaRight) * 0.5f;
            }
            else
            {
                _isAttachedToLeftWall = !_isAttachedToLeftWall;
            }

            float targetX = _isAttachedToLeftWall ? arenaLeft + 1.0f : arenaRight - 1.0f;
            _leapTarget = new Vector2(targetX, wallClingHeight);

            currentState = BossState.Leaping;
            _rb.gravityScale = 1.4f;

            float dx = _leapTarget.x - transform.position.x;
            float dy = _leapTarget.y - transform.position.y;
            float timeToApex = 0.8f;
            float vx = dx / timeToApex;
            float vy = (dy + 0.5f * 1.4f * 9.81f * timeToApex * timeToApex) / timeToApex;

            _rb.linearVelocity = new Vector2(vx, vy);

            if (_audio != null && leapSound != null)
            {
                _audio.pitch = 1.0f;
                _audio.PlayOneShot(leapSound);
            }
        }

        private IEnumerator PrepareAndClimbWall()
        {
            currentState = BossState.PrepareJump;
            _rb.linearVelocity = Vector2.zero;

            // Start widening the shot during the crouch: the flight alone is too short for the
            // camera to catch up, and the boss leaves the frame on the way up.
            CameraZoomController.SetFocus(_col);

            SetSquash(1.25f, 0.75f);
            ProjectileDebris.SpawnSpark(transform.position, Color.yellow, 8, 4f);

            yield return new WaitForSeconds(0.25f);

            SetSquash(1f, 1f);
            if (currentState != BossState.PrepareJump) yield break;

            // Take the pillar on the far side from the player, like the phase 1 wall jump.
            _isAttachedToLeftWall = _player != null
                ? _player.transform.position.x > (arenaLeft + arenaRight) * 0.5f
                : !_isAttachedToLeftWall;

            if (_audio != null && leapSound != null)
            {
                _audio.pitch = 0.9f;
                _audio.PlayOneShot(leapSound);
            }

            currentState = BossState.Leaping;
            yield return LeapOntoPillar(_rb.position, _rb.rotation, 0.55f);
            if (currentState != BossState.Leaping) yield break;

            AttachToRealWall();
        }

        // Flies the boss onto the pillar picked by _isAttachedToLeftWall. The body is kinematic for
        // the whole flight: the path is scripted, and turning onto the wall would otherwise drag the
        // box through the lip under the pillar.
        // turnFrom is how far along the flight the body starts turning onto the wall. Leaving the
        // floor it has to stay upright until it is above the lip; leaving a pillar it already is.
        private IEnumerator LeapOntoPillar(Vector2 start, float startAngle, float turnFrom)
        {
            _scriptedMove = true;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;
            CameraZoomController.SetFocus(_col);

            bool left = _isAttachedToLeftWall;
            float halfWidth = HalfWidth();
            Vector2 end = new Vector2(left ? leftWallFaceX + WallGap : rightWallFaceX - WallGap,
                                      Mathf.Clamp(start.y, wallClimbMinY, wallClimbMaxY));

            // Rise beyond the lip's outer edge until the body is above it, then swing in to the face.
            float clearX = left ? leftLedgeEdgeX + halfWidth + 0.3f : rightLedgeEdgeX - halfWidth - 0.3f;
            Vector2 control = new Vector2(clearX, Mathf.Max(start.y, end.y) + 5f);
            float endAngle = left ? -90f : 90f;

            for (float t = 0f; t < 1f && currentState == BossState.Leaping;)
            {
                t = Mathf.Min(1f, t + Time.fixedDeltaTime / wallLeapDuration);
                float u = 1f - t;
                Vector2 p = u * u * start + 2f * u * t * control + t * t * end;
                float turn = Mathf.InverseLerp(turnFrom, 1f, t);
                _rb.MovePosition(p);
                // Set directly: the body keeps FreezeRotation, which MoveRotation may not get past.
                _rb.rotation = Mathf.LerpAngle(startAngle, endAngle, turn);
                yield return new WaitForFixedUpdate();
            }

            _scriptedMove = false;
        }

        private void AttachToRealWall()
        {
            currentState = BossState.WallCling;
            _onRealWall = true;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;
            _rb.rotation = _isAttachedToLeftWall ? -90f : 90f;

            isFacingLeft = !_isAttachedToLeftWall;
            ApplyFacing();

            Vector2 contact = _rb.position;
            ExpandingRing.Spawn(contact, Color.white, 1.8f, 0.18f);
            ProjectileDebris.SpawnSpark(contact, new Color(0.9f, 0.9f, 0.6f), 14, 6f);
            CameraZoomController.TriggerShake(0.25f, 0.15f);

            _climbDir = 1f;
            _climbFireTimer = 0.4f;
            _stateTimer = wallClimbDuration;
        }

        private void DetachFromRealWall()
        {
            if (_consecutiveWallJumps < 1 && Random.value < 0.55f)
            {
                _consecutiveWallJumps++;
                StartCoroutine(PillarToPillarRoutine());
                return;
            }

            _consecutiveWallJumps = 0;
            _onRealWall = false;

            // Step out from the pillar before standing up, so the upright box clears its face.
            float halfWidth = HalfWidth();
            float x = _isAttachedToLeftWall ? leftWallFaceX + halfWidth + 0.1f : rightWallFaceX - halfWidth - 0.1f;
            _rb.position = new Vector2(x, _rb.position.y);
            ReleaseFromWall();

            _leapFromWall = true;
            StartCoroutine(PrepareAndLeapSlam());
        }

        private IEnumerator PillarToPillarRoutine()
        {
            currentState = BossState.Leaping;
            Vector2 start = _rb.position;
            float startAngle = _rb.rotation;
            _isAttachedToLeftWall = !_isAttachedToLeftWall;

            Vector2 spiralOrigin = muzzlePoint != null ? (Vector2)muzzlePoint.position : (Vector2)transform.position;
            StartCoroutine(BossBulletHellEmitter.FireSpiralRoutine(spiralOrigin, totalBullets: 14, duration: 0.75f, rotations: 1.5f, speed: 7.5f, Color.magenta));

            yield return LeapOntoPillar(start, startAngle, 0f);
            if (currentState != BossState.Leaping) yield break;

            AttachToRealWall();
        }

        private float HalfWidth()
        {
            return _box != null ? _boxSize.x * 0.5f * Mathf.Abs(_originalScale.x) : _col.bounds.extents.x;
        }

        private void AttachToWall()
        {
            currentState = BossState.WallCling;

            // Hanging on the wall is scripted, so take the body out of the simulation until it lets
            // go: a dynamic body snapped and turned into place gets shoved around by whatever it overlaps.
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;

            float wallX = _isAttachedToLeftWall ? arenaLeft + 0.8f : arenaRight - 0.8f;
            Vector2 clingPos = new Vector2(wallX, wallClingHeight);
            _rb.position = clingPos;
            _rb.rotation = _isAttachedToLeftWall ? -90f : 90f;

            isFacingLeft = !_isAttachedToLeftWall;
            ApplyFacing();

            // The transform only catches up with the body on the next physics step.
            ExpandingRing.Spawn(clingPos, Color.white, 1.8f, 0.18f);
            ProjectileDebris.SpawnSpark(clingPos, new Color(0.9f, 0.9f, 0.6f), 14, 6f);

            _stateTimer = isPhase2 ? wallClingDurationPhase2 : wallClingDurationPhase1;

            StartCoroutine(ExecuteWallBarrageRoutine());
        }

        private IEnumerator ExecuteWallBarrageRoutine()
        {
            yield return new WaitForSeconds(0.35f);

            if (currentState != BossState.WallCling) yield break;

            // Action 1: Aimed turret burst down at player!
            StartCoroutine(ExecuteGatlingPattern(isFacingLeft ? -1f : 1f));

            yield return new WaitForSeconds(0.55f);

            if (currentState != BossState.WallCling) yield break;

            // Action 2: Wall Curtain Fire (downward bullet rain)
            float arenaWidth = Mathf.Abs(arenaRight - arenaLeft);
            float safeGapX = _player != null ? _player.transform.position.x : (arenaLeft + arenaRight) * 0.5f;
            Color curtainColor = isPhase2 ? new Color(1f, 0.3f, 0.1f) : new Color(1f, 0.7f, 0.2f);

            Vector2 curtainOrigin = turretPivot != null ? (Vector2)turretPivot.position : (Vector2)transform.position + Vector2.up * 2f;
            BossBulletHellEmitter.FireWallCurtain(curtainOrigin, arenaWidth, count: 14, fallSpeed: 7.5f, curtainColor, safeGapX);

            yield return new WaitForSeconds(0.6f);

            if (currentState != BossState.WallCling) yield break;

            // Action 3: Arc EMP Ring from antenna/radar
            Vector2 antennaPos = radarPivot != null ? (Vector2)radarPivot.position : (turretPivot != null ? (Vector2)turretPivot.TransformPoint(new Vector3(1.15f, 2.3f, 0f)) : (Vector2)transform.position + Vector2.up * 4f);
            Color ringColor = isPhase2 ? new Color(0.95f, 0.2f, 1f) : new Color(0.3f, 0.85f, 1f);
            BossBulletHellEmitter.FireArcRing(antennaPos, count: isPhase2 ? 18 : 12, speed: 7f, ringColor);

            if (_audio != null && sparkArcSound != null)
            {
                _audio.pitch = 1.1f;
                _audio.PlayOneShot(sparkArcSound);
            }
        }

        private void DetachFromWall()
        {
            if (_onRealWall)
            {
                DetachFromRealWall();
                return;
            }

            ReleaseFromWall();

            if (isPhase2 && _consecutiveWallJumps < 1 && Random.value < 0.55f)
            {
                _consecutiveWallJumps++;
                StartCoroutine(TriangleWallJumpRoutine());
                return;
            }

            _consecutiveWallJumps = 0;
            StartCoroutine(PrepareAndLeapSlam());
        }

        private IEnumerator TriangleWallJumpRoutine()
        {
            currentState = BossState.Leaping;
            _rb.gravityScale = 1.0f;

            _isAttachedToLeftWall = !_isAttachedToLeftWall;
            float targetX = _isAttachedToLeftWall ? arenaLeft + 0.8f : arenaRight - 0.8f;
            _leapTarget = new Vector2(targetX, wallClingHeight);

            float dx = _leapTarget.x - transform.position.x;
            float dy = _leapTarget.y - transform.position.y;
            float time = 0.85f;
            _rb.linearVelocity = new Vector2(dx / time, (dy + 0.5f * 9.81f * time * time) / time);

            Vector2 spiralOrigin = muzzlePoint != null ? (Vector2)muzzlePoint.position : (Vector2)transform.position;
            StartCoroutine(BossBulletHellEmitter.FireSpiralRoutine(spiralOrigin, totalBullets: 14, duration: 0.75f, rotations: 1.5f, speed: 7.5f, Color.magenta));

            yield return null;
        }

        private IEnumerator PrepareAndLeapSlam()
        {
            currentState = BossState.PrepareJump;
            _rb.linearVelocity = Vector2.zero;
            _rb.rotation = 0f;

            float targetX = _player != null ? _player.transform.position.x : (arenaLeft + arenaRight) * 0.5f;
            targetX = Mathf.Clamp(targetX, arenaLeft + 2.0f, arenaRight - 2.0f);
            _leapTarget = new Vector2(targetX, arenaFloorY);

            SetSquash(1.3f, 0.7f);
            yield return new WaitForSeconds(0.2f);
            SetSquash(1f, 1f);

            currentState = BossState.Leaping;
            _rb.gravityScale = 2.2f;

            float dx = targetX - transform.position.x;
            float highApexY = arenaFloorY + 8.5f;
            float dy = highApexY - transform.position.y;

            float vy = Mathf.Sqrt(Mathf.Max(12f, 2f * 2.2f * 9.81f * Mathf.Max(2f, dy)));
            // Off a pillar the target can be most of the arena away, and the floor leap's rate
            // crosses that at over 40 units a second. The fall from the wall is long, so spread
            // the distance over it instead.
            float vx = _leapFromWall ? dx / 1.1f : dx * 1.8f;

            _rb.linearVelocity = new Vector2(vx, vy);

            if (_audio != null && leapSound != null)
            {
                _audio.pitch = 1.15f;
                _audio.PlayOneShot(leapSound);
            }
        }

        private void OnLandImpact()
        {
            CameraZoomController.TriggerShake(isPhase2 ? 0.45f : 0.35f, 0.22f);

            Color slamColor = isPhase2 ? new Color(1f, 0.3f, 0.1f) : Color.white;
            ExpandingRing.Spawn(transform.position + Vector3.up * 0.5f, slamColor, 2.8f, 0.25f);
            ProjectileDebris.SpawnSpark(transform.position + Vector3.up * 0.5f, slamColor, 20, 8f);

            float swSpeed = isPhase2 ? 10.0f : 7.5f;
            BossShockwave.Create(transform.position + Vector3.up * 0.2f, -1f, swSpeed, slamColor, isElectric: isPhase2);
            BossShockwave.Create(transform.position + Vector3.up * 0.2f, 1f, swSpeed, slamColor, isElectric: isPhase2);

            if (_audio != null && landingSound != null)
            {
                _audio.pitch = isPhase2 ? 0.85f : 1.0f;
                _audio.PlayOneShot(landingSound);
            }
        }

        #endregion

        #region Phase 2 Transition & Defeat

        private void TriggerPhaseTransition()
        {
            isPhase2 = true;
            currentState = BossState.PhaseTransition;
            StartCoroutine(PhaseTransitionRoutine());
        }

        private IEnumerator PhaseTransitionRoutine()
        {
            isInvulnerable = true;
            // Can start mid wall cling, which leaves the body kinematic and turned sideways.
            ReleaseFromWall();
            _rb.linearVelocity = Vector2.zero;
            _rb.gravityScale = 0f;

            Vector2 centerPos = new Vector2((arenaLeft + arenaRight) * 0.5f, arenaFloorY + 1.5f);
            _rb.position = centerPos;

            CameraZoomController.TriggerShake(0.5f, 0.8f);

            if (_audio != null && phaseTransitionSound != null)
            {
                _audio.PlayOneShot(phaseTransitionSound);
            }

            ProjectileDebris.SpawnSpark((Vector3)centerPos + Vector3.up * 2f, new Color(1f, 0.5f, 0.1f), 28, 10f);
            ExpandingRing.Spawn((Vector3)centerPos + Vector3.up * 2f, new Color(1f, 0.2f, 0.1f, 0.9f), 3.5f, 0.4f);

            // Swap to Phase 2 sprites
            if (basePhase2Sprite != null && baseRenderer != null)
            {
                baseRenderer.sprite = basePhase2Sprite;
            }
            if (turretPhase2Sprite != null && turretRenderer != null)
            {
                turretRenderer.sprite = turretPhase2Sprite;
            }
            if (radarSpinFramesPhase2 != null && radarSpinFramesPhase2.Length > 0 && radarRenderer != null)
            {
                radarRenderer.sprite = radarSpinFramesPhase2[0];
            }
            if (baseRenderer != null) baseRenderer.color = phase2GlowColor;
            if (turretRenderer != null) turretRenderer.color = phase2GlowColor;
            if (radarRenderer != null) radarRenderer.color = phase2GlowColor;

            yield return new WaitForSeconds(1.0f);

            BossBulletHellEmitter.FireArcRing(transform.position + Vector3.up * 2.5f, count: 20, speed: 8.0f, Color.red);

            isInvulnerable = false;
            _rb.gravityScale = 2.0f;

            currentState = BossState.GroundCombat;
            _nextActionTime = Time.time + 0.5f;
        }

        private void TriggerDefeat()
        {
            currentState = BossState.Defeated;
            StartCoroutine(DefeatSequenceRoutine());
        }

        private IEnumerator DefeatSequenceRoutine()
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.gravityScale = 0f;
            if (_col != null) _col.enabled = false;

            for (int i = 0; i < 8; i++)
            {
                Vector2 randomOffset = new Vector2(Random.Range(-1.8f, 1.8f), Random.Range(0.5f, 3.5f));
                Vector3 expPos = transform.position + (Vector3)randomOffset;

                CameraZoomController.TriggerShake(0.25f, 0.1f);
                ExpandingRing.Spawn(expPos, new Color(1f, Random.Range(0.3f, 0.8f), 0.1f), 2.2f, 0.2f);
                ProjectileDebris.SpawnSpark(expPos, Color.yellow, 14, 7f);

                if (_audio != null && ouch != null)
                {
                    _audio.pitch = Random.Range(0.6f, 1.4f);
                    _audio.PlayOneShot(ouch, 0.8f);
                }

                yield return new WaitForSeconds(0.22f);

                // Halfway through, the pilot bails out.
                if (i == 3 && pilotEjectsOnDefeat)
                {
                    if (pilotExitStyle == PilotExitStyle.EjectCapsule) LaunchCockpit();
                    else SpawnPilot();
                }
            }

            CameraZoomController.TriggerShake(0.6f, 0.4f);
            ExpandingRing.Spawn(transform.position + Vector3.up * 2f, Color.white, 4.5f, 0.45f);
            ProjectileDebris.SpawnSpark(transform.position + Vector3.up * 2f, new Color(1f, 0.4f, 0.1f), 35, 12f);

            _deathSequenceDone = true;
            var ev = Schedule<EnemyDeath>();
            ev.enemy = this;

            Destroy(gameObject, 0.1f);
        }

        // Centre of the turret's upper rear housing in turret-sprite units as drawn (facing left),
        // printed by Tools/cut-eject-capsules.py.
        private static readonly Vector2 CapsuleCentre = new Vector2(2.555f, 1.171f);

        // The housing fires off with the pilot in it, away from the player; the turret is left
        // scorched where it tore away.
        private void LaunchCockpit()
        {
            if (turretRenderer == null) return;
            Transform turret = turretRenderer.transform;
            string phase = turretRenderer.sprite != null && turretRenderer.sprite == turretPhase2Sprite ? "P2" : "P1";
            // flipX mirrors the art about the sprite pivot, which a transform point doesn't follow.
            Vector2 local = new Vector2(isFacingLeft ? CapsuleCentre.x : -CapsuleCentre.x, CapsuleCentre.y);

            int dir = 1;
            if (_player != null)
            {
                float away = transform.position.x - _player.transform.position.x;
                if (Mathf.Abs(away) > 0.5f) dir = away > 0f ? 1 : -1;
            }
            EjectionCapsule.Launch(Resources.Load<Sprite>("BossPilot/Capsule_Quadpod_" + phase),
                turret.TransformPoint(local), turret.rotation, turret.lossyScale, turretRenderer.flipX,
                turretRenderer.sortingLayerID, turretRenderer.sortingOrder + 2, dir);

            var torn = Resources.Load<Sprite>("BossPilot/Quadpod_Ejected_" + phase);
            if (torn != null) turretRenderer.sprite = torn;
        }

        // Out of the turret hatch, away from the player, through the gap past the pillar on that side.
        private void SpawnPilot()
        {
            Transform turret = turretRenderer != null ? turretRenderer.transform : transform;
            // flipX mirrors the art about the sprite pivot, which a transform point doesn't follow.
            Vector2 local = new Vector2(isFacingLeft ? pilotHatchOffset.x : -pilotHatchOffset.x, pilotHatchOffset.y);
            Vector2 hatch = turret.TransformPoint(local);

            ExpandingRing.Spawn(hatch, Color.white, 2f, 0.2f);
            ProjectileDebris.SpawnSpark(hatch, new Color(1f, 0.8f, 0.4f), 14, 7f);

            int dir = 1;
            if (_player != null)
            {
                float away = hatch.x - _player.transform.position.x;
                if (Mathf.Abs(away) > 0.5f) dir = away > 0f ? 1 : -1;
            }
            float exitX = dir > 0 ? rightWallFaceX : leftWallFaceX;
            int layer = turretRenderer != null ? turretRenderer.sortingLayerID : 0;
            int order = (turretRenderer != null ? turretRenderer.sortingOrder : 12) + 2;
            BossPilotEscape.Spawn(hatch, arenaFloorY, dir, exitX, false, layer, order, pilotExitStyle == PilotExitStyle.HopAway);
        }

        private void ClampArenaPosition()
        {
            // Through the body, never the transform: a transform write skips collision entirely.
            // Height is left to the level floor.
            Vector2 pos = _rb.position;
            float clampedX = Mathf.Clamp(pos.x, arenaLeft + 1.5f, arenaRight - 1.5f);
            if (clampedX == pos.x) return;

            _rb.position = new Vector2(clampedX, pos.y);
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        }

        private void LandSlam()
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.gravityScale = 2.0f;
            _rb.rotation = 0f;
            _leapFromWall = false;
            CameraZoomController.ClearFocus(_col);

            OnLandImpact();

            currentState = BossState.Recovering;
            _stateTimer = isPhase2 ? slamRecoveryPhase2 : slamRecoveryPhase1;
        }

        private void ReleaseFromWall()
        {
            // Stand upright before rejoining the simulation, so the sideways box never collides.
            _rb.rotation = 0f;
            _rb.bodyType = RigidbodyType2D.Dynamic;

            // Upright, the box can clip the ledges beside the cling point. Step clear of them here
            // instead of letting the solver shove the boss out mid-leap.
            _overlaps.Clear();
            _col.Overlap(_groundFilter, _overlaps);
            foreach (var other in _overlaps)
            {
                if (other.attachedRigidbody != null && other.attachedRigidbody.bodyType != RigidbodyType2D.Static) continue;
                ColliderDistance2D d = _col.Distance(other);
                if (d.isOverlapped) _rb.position += d.normal * d.distance;
            }
        }

        // Squashes the art but keeps the collider its normal size, so a crouch next to a wall
        // doesn't swell the box into it.
        private void SetSquash(float x, float y)
        {
            transform.localScale = new Vector3(_originalScale.x * x, _originalScale.y * y, _originalScale.z);
            if (_box == null) return;
            _box.size = new Vector2(_boxSize.x / x, _boxSize.y / y);
            _box.offset = new Vector2(_boxOffset.x / x, _boxOffset.y / y);
        }

        #endregion

        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            if (currentState == BossState.Defeated) return;

            var player = collision.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                if (isInvulnerable) return;

                bool willHurtEnemy = player.Bounds.min.y >= Bounds.center.y + 0.5f;
                if (willHurtEnemy)
                {
                    if (_bossHealth != null)
                    {
                        _bossHealth.Decrement();
                        player.Bounce(8);
                    }
                }
                else
                {
                    if (player.isParrying)
                    {
                        player.OnParrySuccess(gameObject);
                    }
                    else if (!player.isInvincible)
                    {
                        Vector2 dir = (player.transform.position - transform.position).normalized;
                        Vector2 knockback = new Vector2(Mathf.Sign(dir.x != 0 ? dir.x : 1f) * 4.5f, 5f);
                        player.TakeDamage(1, knockback);
                    }
                }
            }
            else
            {
                if (currentState == BossState.Intro && !_hasLandedIntro)
                {
                    CompleteIntroLanding();
                }
                else if (currentState == BossState.GroundSlam)
                {
                    LandSlam();
                }
            }
        }
    }
}
