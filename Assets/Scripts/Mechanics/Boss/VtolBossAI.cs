using System.Collections;
using System.Collections.Generic;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Model;
using Platformer.View;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using static Platformer.Core.Simulation;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// VTOL-99 "Heavy Striker" Main Boss AI Controller.
    /// Aerial piloted gunship mech with flight physics, banked tilting,
    /// homing missile salvos, support drone deployment, and screen-sweeping mega particle beam.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(Collider2D))]
    public partial class VtolBossAI : EnemyController
    {
        public enum BossState
        {
            Intro,
            Hovering,
            MissileSalvo,
            MegaBeamStrike,
            DeployDrones,
            CarpetBombing,
            PhaseTransition,
            Defeated,
            // Appended last so serialized states keep their values.
            Dormant,
            Swoop,
            GunRun,
            Escaping,
            Crashing,
            Grounded,
            GroundCannon,
            GroundBeam,
            LiftAttempt
        }

        [Header("Hierarchy & Renderers")]
        public SpriteRenderer fuselageRenderer;
        public Transform cannonPivot;
        public Transform cannonMuzzle;
        public Transform leftMissileHatch;
        public Transform rightMissileHatch;
        public Transform droneBay;

        [Header("Sprites & Phases")]
        public Sprite phase1Sprite;
        public Sprite phase2Sprite;
        public bool isPhase2 = false;
        public bool isInvulnerable = false;
        public Color phase2GlowColor = new Color(1f, 0.35f, 0.2f, 1f);

        [Header("Flight Physics & Banked Tilt")]
        public float maxFlightSpeed = 6.5f;
        public float flightAcceleration = 9f;
        public float repositionIntervalMin = 1.4f;
        public float repositionIntervalMax = 2.6f;
        public float hoverAltitude = 4.2f;
        public float maxTiltAngle = 10f;

        [Header("Attitude (self-balancing)")]
        [Tooltip("How quickly the airframe swings toward the attitude it wants, in rad/s")]
        public float attitudeFrequency = 6.5f;
        [Tooltip("Below 1 the airframe overshoots and rocks back to level, like a craft correcting itself")]
        public float attitudeDamping = 0.3f;
        [Tooltip("Lean, in degrees, per unit/s² of horizontal acceleration")]
        public float leanPerAccel = 0.35f;
        [Tooltip("Lean, in degrees, per unit/s of cruise speed (holding speed against drag)")]
        public float leanPerSpeed = 0.45f;
        [Tooltip("Gusty sway, in degrees, that the balance keeps correcting while hovering")]
        public float hoverWobble = 1.5f;

        [Header("Rig Parts (Tools/split-vtol-parts.py)")]
        public Sprite leftDuctPhase1;
        public Sprite leftDuctPhase2;
        public Sprite rightDuctPhase1;
        public Sprite rightDuctPhase2;
        public Sprite leftPodPhase1;
        public Sprite leftPodPhase2;
        public Sprite rightPodPhase1;
        public Sprite rightPodPhase2;
        public Sprite cannonPhase1;
        public Sprite cannonPhase2;

        [Header("Thrust Vectoring")]
        [Tooltip("Furthest the ducts swivel from vertical, in degrees")]
        public float ductMaxAngle = 35f;
        public float ductFrequency = 9f;
        [Tooltip("Below 1 the ducts overshoot and settle, hunting for balance")]
        public float ductDamping = 0.42f;
        [Tooltip("Duct swivel, in degrees, per unit/s² of horizontal acceleration")]
        public float ductPerAccel = 2.4f;
        [Tooltip("Duct swivel, in degrees, per unit/s of cruise speed")]
        public float ductPerSpeed = 2.2f;
        [Tooltip("How much the ducts hold vertical against the airframe's sway (1 = fully gimballed)")]
        public float ductGimbal = 0.7f;

        [Header("Breakable Parts")]
        public int podHealth = 8;
        public int ductHealth = 12;
        [Tooltip("Hull damage dealt when a pod is blown off or a duct is wrecked")]
        public int partBreakDamage = 3;
        [Tooltip("Lean, in degrees, toward the side of a wrecked duct")]
        public float damagedDuctLean = 9f;

        [Header("Chin Cannon")]
        public float cannonTurnSpeed = 110f;
        [Tooltip("Aim limits relative to the hull, degrees; positive swings the barrel down")]
        public float cannonMinAngle = -15f;
        public float cannonMaxAngle = 55f;

        [Header("Ducted Fans")]
        [Tooltip("Spin frames cut from the body art by Tools/build-vtol-fan-frames.py")]
        public Sprite[] leftFanFramesPhase1;
        public Sprite[] rightFanFramesPhase1;
        public Sprite[] leftFanFramesPhase2;
        public Sprite[] rightFanFramesPhase2;
        [Tooltip("Frames per second at idle thrust; each frame turns the blades 7.5 degrees")]
        public float fanIdleFps = 26f;
        public float fanMaxFps = 52f;
        public float hoverBobAmplitude = 0.4f;
        public float hoverBobFrequency = 2.4f;
        public bool isFacingLeft = true;

        [Header("Arena Bounds")]
        // The helideck: collider top y 75.17, x 123.3..165.4 (Helipad_Structure).
        public float arenaLeft = 123.5f;
        public float arenaRight = 165f;
        public float arenaFloorY = 75.17f;
        public float arenaCeilingY = 87f;

        [Header("Topside Roaming")]
        [Tooltip("Off: it keeps to the helideck (Arena Bounds) as before. The encounter trigger, the entrance and the phase 3 crash stay on the helideck either way")]
        public bool roamTopside = true;
        // The whole topside: the west wide shot (x -19) to just past the helideck's right edge.
        public float roamLeft = -19f;
        public float roamRight = 170f;
        [Tooltip("Highest a spot it hovers or fights from may be. Where an obstacle needs more (the X-band dome's crown), it only flies over")]
        public float roamHoverCeilingY = 100f;
        [Tooltip("Chance, each time it relocates near the player, of a fast pass over the player's head to a spot well out on the other side")]
        [Range(0f, 1f)] public float roamLegChance = 0.3f;
        [Tooltip("How far past the player a pass carries it")]
        public float roamLegMinDistance = 12f;
        public float roamLegMaxDistance = 26f;
        [Tooltip("Speed of passes, and of the chase when the player is out of attack range")]
        public float roamCruiseSpeed = 13f;
        [Tooltip("Seconds it holds at the end of a long leg before it comes back for the player")]
        public float roamLoiterMin = 1.2f;
        public float roamLoiterMax = 2.6f;
        [Tooltip("Close attacks start only within this horizontal distance of the player; farther out it closes in, firing missiles or dropping drones")]
        public float attackRange = 18f;
        [Tooltip("Missiles and drones still come from this far off")]
        public float longRangeAttackRange = 45f;
        [Tooltip("Half-width of the stretch a gun run or bombing run sweeps, centred on the player")]
        public float attackSpan = 20f;
        [Tooltip("Deck art to stay above, world units (scene bounds as of 2026-10-26). A dome follows an elliptical crown from the deck to its top instead of a flat box. Backdrop entries are skipped: it flies in front of them, as the player walks in front of them; untick to make one a hard obstacle again")]
        public RoamObstacle[] roamObstacles =
        {
            // Avoiding the radomes and cranes left a safe zone under each (the X-band dome alone
            // was 80 units of deck it could never fight over), so they are backdrop by default.
            new RoamObstacle { name = "West aux radome", left = -14.8f, right = 1.8f, top = 94.4f, backdrop = true },
            new RoamObstacle { name = "Jib crane", left = -3.1f, right = 6.1f, top = 89.1f, backdrop = true },
            new RoamObstacle { name = "Gantry crane", left = 14.7f, right = 35.3f, top = 91.2f, backdrop = true },
            new RoamObstacle { name = "Airlock sat radome", left = 22.6f, right = 39.2f, top = 94.8f, backdrop = true },
            new RoamObstacle { name = "X-band radome", left = 37.6f, right = 122.9f, top = 117.8f, dome = true, backdrop = true },
            new RoamObstacle { name = "Control tower cab", left = 122.4f, right = 136.1f, top = 95f },
        };
        [Tooltip("How far beside an obstacle the hull still counts as over it")]
        public float obstaclePadding = 3f;
        [Tooltip("Pivot height kept above an obstacle's top (the hull reaches 2.0 below the pivot)")]
        public float obstacleClearance = 3.2f;
        [Tooltip("How far ahead it looks for obstacles to climb over")]
        public float obstacleLookahead = 10f;

        [System.Serializable]
        public struct RoamObstacle
        {
            public string name;
            public float left;
            public float right;
            public float top;
            public bool dome;
            [Tooltip("Drawn behind the play plane: fly in front of it instead of over it")]
            public bool backdrop;
        }

        [Header("Phases")]
        [Tooltip("Health ratio where phase 2 (overdrive) starts")]
        public float phase2Threshold = 0.6f;
        [Tooltip("Health ratio where it loses lift and crash-lands on the deck for its last stand (phase 3)")]
        public float phase3Threshold = 0.3f;
        [Tooltip("On: at zero health the pilot bails out of the cockpit and runs off the helideck, and the empty gunship blows up. Takes priority over Escape On Defeat")]
        public bool pilotEjectsOnDefeat = true;
        [Tooltip("Hop Away: the pilot springs out and bounds across the helideck, diving off its seaward edge. Eject Capsule: the canopy block fires off with the pilot in it. Climb Out: the pilot scrambles out and runs")]
        public PilotExitStyle pilotExitStyle = PilotExitStyle.HopAway;
        [Tooltip("On (and Pilot Ejects off): at zero health the pilot limps away toward the next map (the ship, where it later crashes) instead of being destroyed")]
        public bool escapeOnDefeat = true;
        [Tooltip("Raised once the boss has flown off, e.g. to open the way to the next map")]
        public UnityEvent onEscaped;

        [Header("Phase 3: Crash Landing")]
        [Tooltip("Pivot height above the deck while it lies on its belly (the chin cannon rests on the deck)")]
        public float groundedPivotHeight = 2.4f;
        public float groundAttackCooldown = 1.6f;
        [Tooltip("Speed of the shove a sputtering duct gives it across the deck")]
        public float lurchSpeed = 3.2f;
        [Tooltip("Damage per hit on the exposed cockpit")]
        public int cockpitDamage = 2;

        [Header("Rotor Wash")]
        [Tooltip("Sideways speed the wash shoves a player standing right under the boss with")]
        public float downwashStrength = 6f;
        [Tooltip("How far to either side of the boss the wash reaches")]
        public float downwashReach = 3.5f;
        [Tooltip("How far below the boss the wash still pushes")]
        public float downwashDepth = 7.5f;

        [Header("Camera")]
        [Tooltip("How far the camera may widen to keep the boss and the player in one shot. 14 fits the whole helideck on a 16:9 screen")]
        public float cameraMaxOrthoSize = 14f;

        [Header("Entrance")]
        public string nameCardTitle = "VTOL-99 // HEAVY STRIKER";
        public string nameCardSubtitle = "AIRBORNE SIEGE GUNSHIP";
        public Color nameCardAccent = new Color(0.25f, 0.85f, 1f, 1f);

        [Header("Encounter Trigger")]
        [Tooltip("Stay hidden until the player steps onto the arena floor, instead of starting at scene load")]
        public bool waitForPlayer = true;
        [Tooltip("When true, the boss stays dormant until the RadomeLever is pulled to open the radome")]
        public bool waitForRadomeLever = true;
        [Tooltip("How far below the arena floor the player's feet may be and still count as on it")]
        public float activationFloorTolerance = 0.6f;

        [Header("Combat Timers & Parameters")]
        public float attackCooldownPhase1 = 2.2f;
        public float attackCooldownPhase2 = 1.3f;
        public int missileSalvoCountPhase1 = 6;
        public int missileSalvoCountPhase2 = 10;
        public float missileFireInterval = 0.22f;

        [Header("Swoop")]
        public float swoopDuration = 1.35f;
        [Tooltip("Boss pivot height above the deck at the bottom of the dive")]
        public float swoopLowClearance = 2.4f;

        [Header("Gun Run")]
        public float gunRunSpeed = 8f;
        public float gunRunFireInterval = 0.11f;
        public float gunRunBulletSpeed = 12f;

        [Header("Carpet Bombing")]
        public float bombInterval = 0.22f;

        [Header("Prefabs & Attacks")]
        public GameObject homingMissilePrefab;
        public GameObject supportDronePrefab;
        public GameObject megaBeamPrefab;
        public BossMegaBeam megaBeamInstance;

        [Header("Audio Clips")]
        public AudioClip jetEngineLoop;
        public AudioClip missileLaunchSound;
        public AudioClip laserChargeSound;
        public AudioClip laserFireSound;
        public AudioClip alarmSound;

        [Header("Live State")]
        public BossState currentState = BossState.Intro;

        private Rigidbody2D _rb;
        private Collider2D _col;
        private Health _bossHealth;
        private PlayerController _player;
        private float _stateTimer = 0f;
        private float _currentTilt = 0f;
        private Vector3 _originalScale;
        private int _maxHp = 32;
        private bool _hasTriggeredPhase2 = false;
        private bool _deathSequenceDone = false;
        private Color _fuselageBaseColor = Color.white;
        private List<BossSupportDrone> _activeDrones = new List<BossSupportDrone>();
        private Vector2 _hoverTarget;
        private float _nextRepositionTime;
        private BossState _lastAttack = BossState.Hovering;
        private Vector2 _scaleMul = Vector2.one;
        private Vector2 _prevVelocity;
        private float _smoothedAccelX;
        private float _tiltOverride;
        private float _tiltVelocity;
        private SpriteRenderer _leftFan;
        private SpriteRenderer _rightFan;
        private float _leftFanPhase;
        private float _rightFanPhase;
        private float _fanSpinScale = 1f;
        private Transform _ductL, _ductR, _podL, _podR, _cannon;
        private SpriteRenderer _ductLRenderer, _ductRRenderer, _podLRenderer, _podRRenderer, _cannonRenderer;
        private float _ductAngle;          // world angle both ducts swing to, + = counter-clockwise
        private float _ductVelocity;
        private float _ductOverride;       // held swivel an attack asks for, world degrees
        private float _podPitch;           // nose-up degrees, art space
        private float _podPitchVelocity;
        private float _podPitchTarget;
        private float _podRecoilL, _podRecoilR;
        private float _cannonAngle;        // degrees, + swings the barrel down
        private float _cannonRecoil;
        private bool _cannonFollowsBeam;
        private int _podLHp, _podRHp, _ductLHp, _ductRHp;
        private bool _podLBroken, _podRBroken, _ductLDamaged, _ductRDamaged;
        private Color _tintPodL = Color.white, _tintPodR = Color.white, _tintDuctL = Color.white, _tintDuctR = Color.white;
        private BossPartHitbox _podLHit, _podRHit, _ductLHit, _ductRHit;
        private bool _escaping;
        private float _depthScale = 1f;   // shrinks as it flies off into the distance
        private bool _far;                // behind the helipad, past the point of being in the fight
        private BossHealthBarUI _healthBar;
        private bool _isPhase3;
        private bool _groundLock;          // lying on the deck: height pinned, belly scraping
        private float _groundY;
        private float _lurchTimer;
        private BossPartHitbox _cockpitHit;
        private Collider2D _cockpitCollider;
        private SpriteRenderer _cockpitGlow;
        private float _sputterTimer = 1f;
        private float _sputterL = 1f, _sputterR = 1f;
        private bool _roamLeg;            // cruising to another stretch of the topside
        private float _roamLegEnd;
        private float _lastRoamLegTime;
        private bool _limpingHome;        // phase 3 begun away from the helideck: flying back to crash on it
        private BossOffscreenIndicator _offscreen;

        protected override void Awake()
        {
            base.Awake();
            ResetRuntimeState();
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _bossHealth = GetComponent<Health>();
            _originalScale = transform.localScale;

            if (_rb != null)
            {
                _rb.gravityScale = 0f;
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            SetupHierarchy();
            CreateRig();

            if (fuselageRenderer != null) _fuselageBaseColor = fuselageRenderer.color;
            if (_bossHealth != null)
            {
                // Without domain reload the handler from the last session is still attached.
                _bossHealth.OnHealthChanged -= HandleHealthChanged;
                _bossHealth.OnHealthChanged += HandleHealthChanged;
            }
        }

        // Domain and scene reload are off in this project: entering play mode reuses this instance,
        // so private fields keep whatever the last session (stopped mid-fight, say) left in them.
        // Field initializers do not run again; reset everything the fight changes here.
        void ResetRuntimeState()
        {
            currentState = BossState.Intro;
            isPhase2 = false;
            isInvulnerable = false;
            _stateTimer = 0f;
            _currentTilt = 0f;
            _tiltVelocity = 0f;
            _tiltOverride = 0f;
            _hasTriggeredPhase2 = false;
            _deathSequenceDone = false;
            _activeDrones.Clear();
            _nextRepositionTime = 0f;
            _lastAttack = BossState.Hovering;
            _scaleMul = Vector2.one;
            _prevVelocity = Vector2.zero;
            _smoothedAccelX = 0f;
            _fanSpinScale = 1f;
            _ductAngle = _ductVelocity = _ductOverride = 0f;
            _podPitch = _podPitchVelocity = _podPitchTarget = 0f;
            _podRecoilL = _podRecoilR = 0f;
            _cannonAngle = _cannonRecoil = 0f;
            _cannonFollowsBeam = false;
            _podLBroken = _podRBroken = _ductLDamaged = _ductRDamaged = false;
            _tintPodL = _tintPodR = _tintDuctL = _tintDuctR = Color.white;
            _escaping = false;
            _depthScale = 1f;
            _far = false;
            _isPhase3 = false;
            _groundLock = false;
            _lurchTimer = 0f;
            _sputterTimer = 1f;
            _sputterL = _sputterR = 1f;
            _roamLeg = false;
            _roamLegEnd = 0f;
            _lastRoamLegTime = -999f;
            _limpingHome = false;
        }

        void OnDestroy()
        {
            if (_healthBar != null) _healthBar.Dismiss(0f);
            if (_bossHealth != null) _bossHealth.OnHealthChanged -= HandleHealthChanged;
            CameraZoomController.ClearFocus(_col);
            // The beam was detached in SetupHierarchy, so it no longer dies with the boss.
            if (megaBeamInstance != null && megaBeamInstance.transform.parent != transform)
                Destroy(megaBeamInstance.gameObject);
        }

        void SetupHierarchy()
        {
            if (fuselageRenderer == null)
                fuselageRenderer = GetComponent<SpriteRenderer>();

            if (cannonPivot == null)
            {
                Transform foundCannon = transform.Find("CannonPivot");
                if (foundCannon != null) cannonPivot = foundCannon;
            }

            if (cannonMuzzle == null && cannonPivot != null)
            {
                Transform foundMuzzle = cannonPivot.Find("Muzzle");
                if (foundMuzzle != null) cannonMuzzle = foundMuzzle;
                else cannonMuzzle = cannonPivot;
            }

            // The chin cannon is painted into the hull, so the pivot never turns: the muzzle has to
            // stay on the drawn barrel.
            if (cannonPivot != null) cannonPivot.localRotation = Quaternion.identity;

            if (megaBeamInstance == null)
            {
                megaBeamInstance = GetComponentInChildren<BossMegaBeam>();
                if (megaBeamInstance == null)
                {
                    GameObject beamObj = new GameObject("MegaBeamSystem");
                    beamObj.transform.SetParent(transform, false);
                    megaBeamInstance = beamObj.AddComponent<BossMegaBeam>();
                }
            }

            // Detach the beam. As a child its trigger joins the boss's Rigidbody2D (so a melee swing
            // at the beam hits the boss), and the boss's negative X scale when facing right mirrors
            // the hitbox away from the drawn beam.
            if (megaBeamInstance != null && megaBeamInstance.transform.IsChildOf(transform))
                megaBeamInstance.transform.SetParent(null, true);
        }

        void Start()
        {
            _player = FindFirstObjectByType<PlayerController>();

            if (_bossHealth != null)
            {
                _maxHp = _bossHealth.maxHP > 0 ? _bossHealth.maxHP : 32;
            }

                        if (waitForPlayer || waitForRadomeLever) EnterDormant();
            else BeginEncounter();
        }

        /// <summary>
        /// Triggers the boss encounter externally (e.g., when the RadomeLever is pulled).
        /// </summary>
        public void TriggerEncounterFromSwitch()
        {
            if (currentState != BossState.Dormant) return;
            BeginEncounter();
        }

        // Hidden, untouchable and parked above the arena until the player arrives.
        void EnterDormant()
        {
            currentState = BossState.Dormant;
            isInvulnerable = true;
            transform.position = new Vector2((arenaLeft + arenaRight) * 0.5f, arenaCeilingY + 3f);
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            SetPresence(false);
        }

        void SetPresence(bool present)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = present;
            if (_col != null) _col.enabled = present;
        }

        bool PlayerIsOnArenaFloor()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerController>();
                if (_player == null) return false;
            }

            Vector2 feet = new Vector2(_player.transform.position.x, _player.Bounds.min.y);
            return feet.x >= arenaLeft && feet.x <= arenaRight
                && feet.y >= arenaFloorY - activationFloorTolerance && feet.y <= arenaCeilingY;
        }

        void BeginEncounter()
        {
            SetPresence(true);
            SetCockpitExposed(_isPhase3);  // SetPresence turned every renderer on, the weak-point glow too

            // Keep the boss in shot: it hovers well above the player and ranges the whole helideck.
            CameraZoomController.SetFocus(_col, cameraMaxOrthoSize);

            StartCoroutine(IntroSequence());
        }

        // Shown once the entrance is over, so it doesn't cover the name card.
        void BindHealthBar()
        {
            if (_bossHealth == null) return;
            // The bar sits on the boss itself, so its OnDestroy takes the UI down with the boss.
            var healthBar = GetComponent<BossHealthBarUI>();
            if (healthBar == null) healthBar = gameObject.AddComponent<BossHealthBarUI>();
            healthBar.Bind(_bossHealth, () => isPhase2,
                nameCardTitle, "PHASE 1: AIR SUPERIORITY", "PHASE 2: OVERDRIVE [CRITICAL]");
            healthBar.SetPhaseMarkers(phase2Threshold, phase3Threshold);
            _healthBar = healthBar;

            // It roams the whole topside: point at it whenever it is out of shot, red while it attacks.
            _offscreen = GetComponent<BossOffscreenIndicator>();
            if (_offscreen == null) _offscreen = gameObject.AddComponent<BossOffscreenIndicator>();
            _offscreen.Bind(transform, nameCardAccent, () => currentState != BossState.Hovering);
        }

        // The camera always keeps it in shot while it fights near the player; out on a long leg it
        // may be let go so the player isn't held at the screen edge.
        void UpdateCameraEngagement()
        {
            bool engaged = currentState != BossState.Hovering
                || (!_roamLeg && EnsurePlayer() && Mathf.Abs(_player.transform.position.x - _rb.position.x) <= attackRange);
            CameraZoomController.SetFocusEngaged(_col, engaged);
        }

        void RemoveOffscreenIndicator()
        {
            if (_offscreen != null) Destroy(_offscreen);
            _offscreen = null;
        }

        // Entrance: the boss has been holding below the helideck's open edge. Its wash blasts up
        // over the edge first, then it climbs into view as a dark silhouette behind the deck,
        // takes its colours as it clears the edge, rears up at the top while its name comes up,
        // and slides in over the deck.
        IEnumerator IntroSequence()
        {
            currentState = BossState.Intro;
            isInvulnerable = true;

            float edgeX = arenaRight;
            // Just inside the edge: the camera's confiner ends barely past the deck.
            Vector2 below = new Vector2(edgeX - 1f, arenaFloorY - 6f);
            Vector2 crest = new Vector2(edgeX - 3f, Mathf.Min(arenaCeilingY - 0.5f, arenaFloorY + hoverAltitude + 2.5f));
            SetPosition(below);
            // It climbs up through the deck's collider; solid, the physics would fling it out. A
            // trigger rather than disabled, so the camera (which drops disabled targets) keeps it.
            if (_col != null) _col.isTrigger = true;
            SetFacing(true);   // the deck, and the player, are to its left

            // Behind the deck (order 2-3) while it is below the edge.
            var group = gameObject.GetComponent<SortingGroup>();
            if (group == null) group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = 1;
            Color silhouette = new Color(0.12f, 0.12f, 0.15f, 1f);
            SetHullColor(silhouette);
            _fanSpinScale = 1.6f;

            // Warning: rumble, and the wash throwing grit up over the edge.
            float t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                CameraZoomController.TriggerShake(0.15f, 0.1f);
                if (Random.value < 0.8f)
                {
                    Vector3 lip = new Vector3(edgeX - Random.Range(0f, 3f), arenaFloorY + 0.1f, 0f);
                    ProjectileDebris.Spawn(lip, new Color(0.8f, 0.82f, 0.85f, 0.7f), Random.Range(0.12f, 0.26f),
                        new Vector2(Random.Range(-2.5f, 0.5f), Random.Range(3f, 6f)), 0.6f);
                }
                yield return null;
            }

            // Climb.
            bool cleared = false;
            float rise = 1.8f;
            t = 0f;
            while (t < rise)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / rise);
                SetPosition(Vector2.Lerp(below, crest, u));
                if (!cleared && _rb.position.y > arenaFloorY + 1.2f)
                {
                    cleared = true;
                    group.sortingOrder = 20;   // in front of the deck now
                }
                // The collider reaches 2.0 below the pivot: solid again once that clears the deck.
                if (_col != null && _col.isTrigger && _rb.position.y > arenaFloorY + 2.2f) _col.isTrigger = false;
                float reveal = Mathf.InverseLerp(arenaFloorY - 1f, arenaFloorY + 2.5f, _rb.position.y);
                SetHullColor(Color.Lerp(silhouette, _fuselageBaseColor, reveal));
                if (Random.value < 0.6f) SpawnDownwash(1);
                yield return null;
            }

            // Rear up at the top; the name comes up.
            if (_col != null) _col.isTrigger = false;
            SetHullColor(_fuselageBaseColor);
            Squash(0.85f, 1.15f);
            Rock(NoseUp(140f));
            CameraZoomController.TriggerShake(1.0f, 0.4f);
            ExpandingRing.Spawn(transform.position, nameCardAccent, 7f, 0.45f);
            BossNameCard.Show(nameCardTitle, nameCardSubtitle, nameCardAccent);
            _fanSpinScale = 1f;

            // Slide in over the deck, braking into a hover.
            Vector2 ready = new Vector2(Mathf.Lerp(arenaLeft, arenaRight, 0.65f), arenaFloorY + hoverAltitude);
            yield return StartCoroutine(FlyTo(ready, maxFlightSpeed, flightAcceleration, 0.8f, 2.4f));

            // Back to normal sorting so smoke and sparks draw over the hull again.
            Destroy(group);
            BindHealthBar();
            isInvulnerable = false;
            ReturnToHover(0.6f);
        }

        void SetPosition(Vector2 p)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.position = p;
            transform.position = p;
        }

        protected override void Update()
        {
            if (currentState == BossState.Defeated) return;

            if (currentState == BossState.Dormant)
            {
                if (!waitForRadomeLever && PlayerIsOnArenaFloor()) BeginEncounter();
                return;
            }

            if (currentState == BossState.Hovering)
            {
                UpdateHoverMovement();

                // Nothing starts mid-leg; from far off only the long-range attacks come.
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f && !_roamLeg && EnsurePlayer())
                {
                    float range = Mathf.Abs(_player.transform.position.x - _rb.position.x);
                    if (range <= attackRange) DecideNextAttack(false);
                    else if (range <= longRangeAttackRange) DecideNextAttack(true);
                }
            }
            else if (currentState == BossState.Grounded)
            {
                UpdateGrounded();
            }

            if (_groundLock) HoldOnDeck();
            UpdateCameraEngagement();
            UpdateCockpitGlow();
            UpdateEngineEffects();
            UpdateDownwashPush();
            ApplyBankedTilt();
            UpdateRig();
            UpdateFans();
            UpdateDamageEffects();
            ApplyFacing();
            // The intro climbs from below the deck and the escape leaves the bounds; clamping would snap them back.
            if (currentState != BossState.Intro && currentState != BossState.Escaping) ClampArenaBounds();
        }

        bool EnsurePlayer()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            return _player != null;
        }
    }
}
