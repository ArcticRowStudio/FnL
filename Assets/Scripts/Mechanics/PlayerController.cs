using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Platformer.Gameplay;
using static Platformer.Core.Simulation;
using Platformer.Model;
using Platformer.Core;
using Platformer.UI;
using Platformer.View;
using UnityEngine.InputSystem;

namespace Platformer.Mechanics
{
    /// <summary>
    /// This is the main class used to implement control of the player.
    /// It is a superset of the AnimationController class, but is inlined to allow for any kind of customisation.
    /// </summary>
    public class PlayerController : KinematicObject
    {
        public AudioClip jumpAudio;
        public AudioClip respawnAudio;
        public AudioClip ouchAudio;

        /// <summary>
        /// Max horizontal speed of the player.
        /// </summary>
        public float maxSpeed = 7;
        /// <summary>
        /// Initial jump velocity at the start of a jump.
        /// </summary>
        public float jumpTakeOffSpeed = 7;

        public JumpState jumpState = JumpState.Grounded;
        private bool stopJump;

        [Header("Ranged Attack")]
        public GameObject projectilePrefab;
        public float fireRate = 0.5f;
        // Rounds available before a reload is needed.
        public int magazineSize = 8;
        // Seconds a reload takes.
        public float reloadTime = 1.2f;
        // Seconds between the shot and the impact at the designated point.
        public float sniperTravelTime = 0.18f;
        // Scale of the round as it enters the view, shrinking as it recedes into the scene.
        public float sniperEntryScale = 6f;
        // How far off a target the aim point may be and still hit it. A miss is a miss: the
        // crosshair has to be on the target (lock-on, the impact check and part aiming all use this).
        public float sniperImpactRadius = 0.1f;
        [Tooltip("TEMP testing: a sniper hit on a boss takes this share of its max HP (0.25 = phase change every ~2 hits). Set 0 for the normal 1 HP")]
        public float sniperBossDamageFraction = 0f;
        private float nextFireTime;
        private int ammoInMagazine;
        private Coroutine reloadCoroutine;
        private float reloadEndTime;
        private AmmoUI ammoUI;
        private AimReticleUI aimReticleUI;

        [Header("Melee Attack")]
        // Minimum delay between swings.
        public float meleeCooldown = 0.45f;
        // Delay before the hit lands, so the swing reads before it connects.
        public float meleeWindup = 0.15f;
        // The horizontal slash raises the sword and swings it round behind the body before the
        // blade comes through in front at 0.32 s, so it lands later than the thrust and jump slash.
        public float slashWindup = 0.32f;
        // How long a second click still counts as continuing the combo.
        public float meleeComboWindow = 0.9f;
        // Played on the horizontal slash, mirrored to whichever way the player is facing.
        public SpriteSequenceEffect slashEffect;
        // Where the arc starts, measured from the collider's centre and out along the facing.
        // The blade reaches 1.69 units out at full extension, so the arc has to be thrown
        // well clear of the body or it trails behind the sword instead of following it.
        public Vector2 slashEffectOffset = new Vector2(1.1f, 0f);
        // The swing animation spends its first frames winding up and carrying the sword round
        // behind the body. Firing the arc on the trigger put it in mid air before the blade
        // had moved, so it is held back until the blade actually sweeps through in front.
        public float slashEffectDelay = 0.32f;

        [Header("Respawn")]
        [Tooltip("Blue flame column played at the feet when the player respawns.")]
        public SpriteSequenceEffect spawnEffect;
        [Tooltip("Seconds into the spawn effect before the character becomes visible (the flame's peak covers the pop-in).")]
        public float spawnRevealDelay = 0.6f;
        private Coroutine spawnEffectRoutine;

        private float meleeCooldownTimer;
        private int meleeComboIndex;
        private float meleeComboExpiry;
        private readonly Collider2D[] meleeHitBuffer = new Collider2D[16];

        [Header("Parry & Guard Mechanics")]
        [Tooltip("Active parry frame window (seconds) where incoming projectiles are reflected.")]
        public float parryDuration = 0.22f;
        [Tooltip("Cooldown before another parry can be attempted (seconds).")]
        public float parryCooldown = 0.3f;
        public bool isParrying = false;
        public bool isGuarding = false;
        [Tooltip("Parry wind pressure sprite sequence effect prefab.")]
        public SpriteSequenceEffect parryWindEffect;
        [Tooltip("Offset for parry wind effect relative to character center.")]
        public Vector2 parryWindOffset = Vector2.zero;
        [Tooltip("Scale multiplier for the parry wind shockwave.")]
        public float parryWindScale = 1.0f;
        [Tooltip("Parry clash sparks particle effect prefab.")]
        public GameObject parrySparksPrefab;
        [Tooltip("Time scale a just parry eases back from once its hit-stop ends. 1 turns the slow motion off.")]
        [Range(0.05f, 1f)]
        public float parrySlowMotionScale = 0.35f;
        [Tooltip("Real seconds the just parry slow motion takes to ease back to full speed.")]
        public float parrySlowMotionDuration = 0.35f;
        [Tooltip("Speed a blocked hit shoves Row back at while guarding. It slides speed^2 / 50 units before stopping.")]
        public float guardPushSpeed = 5.5f;
        [Tooltip("Fraction of the guard push applied in the air.")]
        public float guardPushAirMultiplier = 0.5f;
        private readonly RaycastHit2D[] guardPushGroundHits = new RaycastHit2D[8];
        private float parryCooldownTimer = 0f;
        private Coroutine parryCoroutine;
        private Color originalColor;
        private PlayerPosture posture;
        private PlayerPostureUI postureUI;
        public PlayerPosture Posture => posture;
        private Sprite bracingShieldSprite;
        private Sprite bracingDustSprite;
        private Sprite impactDeflectionSprite;
        private Sprite impactDeflectionAirSprite;
        private Sprite guardBreakRecoverySprite;
        private Sprite parryImpactPose;
        private float parryImpactPoseUntil;

        // Real seconds the world freezes for on a successful parry.
        private const float parryHitStopDuration = 0.12f;

        // Centre of the flash in the Impact Deflection art, in sprite units from its pivot.
        private static readonly Vector2 ParryImpactOffset = new Vector2(0.0999f, -0.0904f);

        [Header("Warp Mechanics")]
        // How far a single warp teleports the player, in world units.
        public float warpDistance = 2.5f;
        // Stamina spent per warp. With the default gauge that is three warps back to back.
        public float warpCost = 30f;
        // Minimum delay between two warps.
        public float warpCooldown = 0.15f;
        [Tooltip("Warps allowed in mid-air before the player touches the ground, a ladder/rope or a swing rope again. Stops chained warps from flying over the level. 3 is the least the current level allows: the cannon catwalks sit ~9.5 above the cannon-level floor with no ladder, so 2 leaves both cannon buttons out of reach.")]
        public int maxAirWarps = 3;
        private int airWarpsUsed;
        public int AirWarpsLeft => Mathf.Max(0, maxAirWarps - airWarpsUsed);
        // Invincibility granted on arrival.
        public float warpInvincibility = 0.2f;
        public bool isInvincible = false;
        private float warpCooldownTimer = 0f;
        private Coroutine perfectDodgeCoroutine;
        private Coroutine warpInvincibilityCoroutine;
        private Coroutine warpDashCoroutine;
        private bool isWarpingDash = false;
        public float hurtInvincibilityDuration = 1.0f;
        // While on, hits still knock the player back and flash, but take no health. Falling into
        // a death zone still kills.
        public bool infiniteHealth = true;
        private Coroutine hurtInvincibilityCoroutine;
        private WarpStamina stamina;
        private WarpStaminaUI staminaUI;
        private PlayerHealthUI healthUI;
        private readonly Collider2D[] warpOverlapBuffer = new Collider2D[16];

        [Header("Climbing Mechanics")]
        public bool isClimbing = false;
        public bool IsClimbing => isClimbing;
        public bool IsClimbingLadder => isClimbing && currentLadder != null;
        public bool IsClimbingRope => isClimbing && currentRope != null;
        public bool IsRopeSliding { get; private set; }

        [Header("Swing Rope Mechanics")]
        public bool isSwinging = false;
        public bool IsSwinging => isSwinging;
        public SwingRope CurrentSwingRope { get; private set; }

        public Vector2 MoveInput => moveInput;
        public InputAction JumpAction => m_JumpAction;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public Vector2 Velocity => velocity;

        private LadderZone nearbyLadder;
        private RopeZone nearbyRope;
        private SwingRope nearbySwingRope;
        public SwingRope NearbySwingRope => nearbySwingRope;
        private float swingLaunchVelocityX = 0f;
        private float swingLaunchTimer = 0f;
        private LadderZone currentLadder;
        private RopeZone currentRope;
        private float climbCooldownTimer = 0f;

        /*internal new*/ public Collider2D collider2d;
        /*internal new*/ public AudioSource audioSource;
        public Health health;
        public bool controlEnabled = true;

        bool jump;
        Vector2 move;
        Vector2 moveInput;
        SpriteRenderer spriteRenderer;
        internal Animator animator;
        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        private InputAction m_MoveAction;
        private InputAction m_JumpAction;

        public Bounds Bounds => collider2d.bounds;

        /// <summary>
        /// The gauge warping is paid for out of.
        /// </summary>
        public WarpStamina Stamina => stamina;

        /// <summary>
        /// Is the player holding the aim key? Shooting is only possible while aiming.
        /// </summary>
        public bool IsAiming { get; private set; }

        /// <summary>
        /// Programmatic override for aiming (used for automated verification and cutscenes).
        /// </summary>
        public bool debugAimOverride = false;

        /// <summary>
        /// Fired when an off-screen sniper round is designated and fired.
        /// </summary>
        public event System.Action OnSniperShot;

        /// <summary>
        /// Rounds left in the magazine.
        /// </summary>
        public int Ammo => ammoInMagazine;

        public bool IsReloading => reloadCoroutine != null;

        /// <summary>
        /// How far along the current reload is, in the 0..1 range.
        /// </summary>
        public float ReloadProgress
        {
            get
            {
                if (!IsReloading || reloadTime <= 0f) return 1f;
                return Mathf.Clamp01(1f - (reloadEndTime - Time.time) / reloadTime);
            }
        }

        void Awake()
        {
            airWarpsUsed = 0;   // domain reload is off: the count would survive a stopped session
            health = GetComponent<Health>();
            audioSource = GetComponent<AudioSource>();
            collider2d = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            originalColor = spriteRenderer.color;
            animator = GetComponent<Animator>();
            LoadParrySprites();

            m_MoveAction = InputSystem.actions.FindAction("Player/Move");
            m_JumpAction = InputSystem.actions.FindAction("Player/Jump");

            m_MoveAction.Enable();
            m_JumpAction.Enable();

            stamina = GetComponent<WarpStamina>();
            if (stamina == null)
            {
                stamina = gameObject.AddComponent<WarpStamina>();
            }

            staminaUI = FindAnyObjectByType<WarpStaminaUI>();
            if (staminaUI == null)
            {
                staminaUI = new GameObject("WarpStaminaUI").AddComponent<WarpStaminaUI>();
            }
            staminaUI.Bind(stamina, warpCost);

            posture = GetComponent<PlayerPosture>();
            if (posture == null)
            {
                posture = gameObject.AddComponent<PlayerPosture>();
            }
            posture.OnGuardBreak += HandleGuardBreak;

            postureUI = FindAnyObjectByType<PlayerPostureUI>();
            if (postureUI == null)
            {
                postureUI = new GameObject("PlayerPostureUI").AddComponent<PlayerPostureUI>();
            }
            postureUI.Bind(posture);

            ammoInMagazine = magazineSize;

            ammoUI = FindAnyObjectByType<AmmoUI>();
            if (ammoUI == null)
            {
                ammoUI = new GameObject("AmmoUI").AddComponent<AmmoUI>();
            }
            ammoUI.Bind(this);

            aimReticleUI = FindAnyObjectByType<AimReticleUI>();
            if (aimReticleUI == null)
            {
                aimReticleUI = new GameObject("AimReticleUI").AddComponent<AimReticleUI>();
            }
            aimReticleUI.Bind(this);

            healthUI = FindAnyObjectByType<PlayerHealthUI>();
            if (healthUI == null)
            {
                healthUI = new GameObject("PlayerHealthUI").AddComponent<PlayerHealthUI>();
            }
            healthUI.Bind(health);
        }

        protected override void OnDisable()
        {
            // Reset timescale if player is destroyed or disabled during Witch Time
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            // Never leave the hardware cursor hidden behind us.
            SetAiming(false);
            base.OnDisable();
        }

        protected override void Update()
        {
            if (warpCooldownTimer > 0)
            {
                warpCooldownTimer -= Time.deltaTime;
            }
            if (meleeCooldownTimer > 0)
            {
                meleeCooldownTimer -= Time.deltaTime;
            }
            if (parryCooldownTimer > 0)
            {
                parryCooldownTimer -= Time.deltaTime;
            }
            if (climbCooldownTimer > 0)
            {
                climbCooldownTimer -= Time.deltaTime;
            }
            // Touching anything to stand or hang on gives the air warps back.
            if ((IsGrounded && !isWarpingDash) || isClimbing || isSwinging) airWarpsUsed = 0;

            if (controlEnabled)
            {
                moveInput = m_MoveAction.ReadValue<Vector2>();
                move.x = (isParrying || isGuarding) ? 0f : moveInput.x;

                // Latch onto nearby ladder or rope
                UpdateNearbyClimbables();

                if (!isClimbing && !isSwinging && !isWarpingDash && climbCooldownTimer <= 0)
                {
                    if (nearbyLadder != null)
                    {
                        // Pressing Up to climb up, or pressing Down when above the ladder's bottom to climb down
                        bool climbUp = moveInput.y > 0.2f && (body.position.y < nearbyLadder.TopY + 0.3f);
                        bool climbDown = moveInput.y < -0.2f && (body.position.y > nearbyLadder.BottomY + 0.1f);
                        if (climbUp || climbDown)
                        {
                            StartClimbingLadder(nearbyLadder);
                        }
                    }
                    else if (nearbyRope != null)
                    {
                        bool climbUp = moveInput.y > 0.2f && (body.position.y < nearbyRope.TopY);
                        bool climbDown = moveInput.y < -0.2f && (body.position.y > nearbyRope.BottomY + 0.1f);
                        bool midAirGrab = !IsGrounded && (moveInput.y > -0.1f || velocity.y < 0f);
                        if (climbUp || climbDown || midAirGrab)
                        {
                            StartClimbingRope(nearbyRope);
                        }
                    }
                }

                bool fJustPressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
                bool fHolding = fJustPressed || (Keyboard.current != null && Keyboard.current.fKey.isPressed);

                if (isClimbing)
                {
                    // Jump off ladder/rope with Space
                    if (m_JumpAction.WasPressedThisFrame() && !isParrying && !isGuarding)
                    {
                        JumpOffClimb();
                    }
                    // If player is grounded and moves horizontally (holding A or D), seamlessly step off
                    else if (IsGrounded && Mathf.Abs(moveInput.x) > 0.3f && Mathf.Abs(moveInput.y) < 0.2f)
                    {
                        StopClimbing();
                    }
                }
                else if (isSwinging)
                {
                    // Release with F or Space
                    if (fJustPressed || m_JumpAction.WasPressedThisFrame())
                    {
                        if (CurrentSwingRope != null)
                        {
                            CurrentSwingRope.ReleasePlayerWithJump();
                        }
                    }
                }
                else
                {
                    // Grab swing rope or climbing rope with F key or touching in mid-air
                    if (climbCooldownTimer <= 0)
                    {
                        if (fHolding)
                        {
                            if (nearbyRope != null)
                            {
                                StartClimbingRope(nearbyRope);
                            }
                            else
                            {
                                TryGrabNearbySwingRope();
                            }
                        }
                        else if (!IsGrounded && velocity.y < 2.0f)
                        {
                            // In mid-air near swing rope, grab automatically
                            TryGrabNearbySwingRope();
                        }
                    }

                    if (m_JumpAction.WasPressedThisFrame() && !isParrying && !isGuarding)
                    {
                        if (jumpState == JumpState.Grounded)
                        {
                            jumpState = JumpState.PrepareToJump;
                        }
                    }
                    else if (m_JumpAction.WasReleasedThisFrame())
                    {
                        stopJump = true;
                        Schedule<PlayerStopJump>().player = this;
                    }
                }

                // Ranged attack: hold Ctrl to raise the reticle, then left click to fire.
                bool ctrlPressed = Keyboard.current != null &&
                                  (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
                SetAiming(debugAimOverride || ctrlPressed);

                if (IsAiming)
                {
                    FaceMouseCursor();

                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        TryShoot();
            AtmospherePostProcessController.TriggerGunfireFlash();
                    }
                }
                else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    // Left click swings the sword. Holding Ctrl hands the click to the
                    // sniper instead, so the two never fire on the same press.
                    TryMelee();
                }

                // Manual reload.
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    StartReload();
                }

                // Warp input (Left Shift)
                if (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame)
                {
                    TryWarp();
                }

                // Guard & Parry input (Right Click or Q)
                bool guardPressed = (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) ||
                                    (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
                bool guardHeld = (Keyboard.current != null && Keyboard.current.qKey.isPressed) ||
                                 (Mouse.current != null && Mouse.current.rightButton.isPressed);

                if (guardPressed && !isParrying && !isGuarding && parryCooldownTimer <= 0 && (posture == null || posture.CanParry))
                {
                    if (parryCoroutine != null) StopCoroutine(parryCoroutine);
                    parryCoroutine = StartCoroutine(ParryGuardRoutine());
                }
                else if (!guardHeld && isGuarding && !isParrying)
                {
                    SetGuarding(false);
                }
            }
            else
            {
                moveInput = Vector2.zero;
                move.x = 0;
                SetAiming(false);
            }
            UpdateJumpState();
            base.Update();
        }

        void UpdateJumpState()
        {
            jump = false;
            switch (jumpState)
            {
                case JumpState.PrepareToJump:
                    jumpState = JumpState.Jumping;
                    jump = true;
                    stopJump = false;
                    break;
                case JumpState.Jumping:
                    if (!IsGrounded)
                    {
                        Schedule<PlayerJumped>().player = this;
                        jumpState = JumpState.InFlight;
                    }
                    break;
                case JumpState.InFlight:
                    if (IsGrounded)
                    {
                        Schedule<PlayerLanded>().player = this;
                        jumpState = JumpState.Landed;
                    }
                    break;
                case JumpState.Landed:
                    jumpState = JumpState.Grounded;
                    break;
            }
        }

        protected override void ComputeVelocity()
        {
            if (isWarpingDash)
            {
                suspendGravity = true;
                velocity = Vector2.zero;
                targetVelocity = Vector2.zero;
                return;
            }

            if (isSwinging)
            {
                suspendGravity = true;
                velocity = Vector2.zero;
                targetVelocity = Vector2.zero;
                move.x = 0;
                animator.SetBool("grounded", false);
                animator.SetBool("isClimbingRope", true);
                animator.SetBool("isClimbingLadder", false);
                animator.SetBool("isRopeSliding", false);
                animator.SetFloat("climbSpeed", 1f);
                animator.SetFloat("velocityX", 0f);
                return;
            }

            if (isClimbing)
            {
                suspendGravity = true;

                if (currentLadder != null)
                {
                    velocity.y = moveInput.y * currentLadder.climbSpeed;

                    // Smooth snap to ladder center (synchronizing both body.position and transform.position)
                    float targetX = currentLadder.CenterX;
                    float newX = Mathf.MoveTowards(body.position.x, targetX, currentLadder.snapToCenterSpeed * Time.deltaTime);
                    body.position = new Vector2(newX, body.position.y);
                    transform.position = new Vector3(newX, body.position.y, transform.position.z);

                    // Top climb & dismount logic:
                    if (moveInput.y > 0.1f)
                    {
                        // When player's feet or waist reach near the top of the ladder
                        if (body.position.y >= currentLadder.TopY - 0.4f)
                        {
                            if (currentLadder.allowTopDismount)
                            {
                                // Check if there is a walkable floor / platform right near the top of the ladder
                                var hit = Physics2D.Raycast(new Vector2(currentLadder.CenterX, currentLadder.TopY + 1.2f), Vector2.down, 1.8f);
                                if (hit.collider != null && !hit.collider.isTrigger && hit.normal.y > 0.5f)
                                {
                                    // Ledge / floor exists: safely pop player onto the floor!
                                    float floorY = hit.point.y;
                                    Vector2 dismountPos = new Vector2(currentLadder.CenterX, floorY + 1.0f);
                                    Teleport(dismountPos);
                                    StopClimbing();
                                    return;
                                }
                            }

                            // No platform to step onto (or free-standing ladder): safely clamp player at top rung!
                            if (body.position.y >= currentLadder.TopY - 0.15f)
                            {
                                body.position = new Vector2(body.position.x, currentLadder.TopY - 0.15f);
                                transform.position = new Vector3(body.position.x, currentLadder.TopY - 0.15f, transform.position.z);
                                velocity.y = 0f;
                            }
                        }
                    }

                    // Bottom dismount check: when standing on the ground and pressing Down
                    if (IsGrounded && moveInput.y < -0.1f)
                    {
                        StopClimbing();
                        return;
                    }

                    // Clamp to bottom of ladder if not grounded
                    if (body.position.y < currentLadder.BottomY && velocity.y < 0)
                    {
                        velocity.y = 0;
                        if (IsGrounded) StopClimbing();
                    }
                }
                else if (currentRope != null)
                {
                    if (currentRope.allowFastSlide && moveInput.y < -0.1f)
                    {
                        velocity.y = -currentRope.slideSpeed;
                        IsRopeSliding = true;
                    }
                    else if (moveInput.y > 0.1f)
                    {
                        velocity.y = currentRope.climbSpeed;
                        IsRopeSliding = false;
                    }
                    else
                    {
                        velocity.y = 0f;
                        IsRopeSliding = false;
                    }

                    // Smooth snap to rope center
                    float targetX = currentRope.CenterX;
                    float newX = Mathf.MoveTowards(body.position.x, targetX, currentRope.snapToCenterSpeed * Time.deltaTime);
                    body.position = new Vector2(newX, body.position.y);
                    transform.position = new Vector3(newX, body.position.y, transform.position.z);

                    // Bottom drop-off check: drop off when reaching bottom stopper
                    if (body.position.y <= currentRope.BottomY + 0.3f && velocity.y < 0)
                    {
                        StopClimbing();
                        return;
                    }
                    // Top stop check: cannot climb above top anchor
                    else if (body.position.y >= currentRope.TopY - 0.2f && velocity.y > 0)
                    {
                        velocity.y = 0f;
                    }
                }

                targetVelocity.x = 0;
                velocity.x = 0;
                move.x = 0;

                animator.SetBool("grounded", false);
                animator.SetBool("isClimbingLadder", IsClimbingLadder);
                animator.SetBool("isClimbingRope", IsClimbingRope);
                animator.SetBool("isRopeSliding", IsRopeSliding);
                animator.SetFloat("climbSpeed", Mathf.Abs(velocity.y) > 0.05f ? 1f : 0f);
                animator.SetFloat("velocityX", 0f);
                animator.SetFloat("velocityY", velocity.y);
                return;
            }

            animator.SetBool("isClimbingLadder", false);
            animator.SetBool("isClimbingRope", false);
            animator.SetBool("isRopeSliding", false);
            animator.SetFloat("climbSpeed", 0f);

            if (jump && IsGrounded)
            {
                velocity.y = jumpTakeOffSpeed * model.jumpModifier;
                jump = false;
            }
            else if (stopJump)
            {
                stopJump = false;
                if (velocity.y > 0)
                {
                    velocity.y = velocity.y * model.jumpDeceleration;
                }
            }

            if (!isParrying && !isGuarding)
            {
                if (move.x > 0.01f)
                    spriteRenderer.flipX = false;
                else if (move.x < -0.01f)
                    spriteRenderer.flipX = true;
            }

            if (!isParrying && !isGuarding)
            {
                animator.SetBool("grounded", IsGrounded);
                animator.SetFloat("velocityX", Mathf.Abs(velocity.x) / maxSpeed);
                // Drives the rise, apex and fall split of the jump animation.
                animator.SetFloat("velocityY", velocity.y);
            }

            if (swingLaunchTimer > 0f)
            {
                if (IsGrounded)
                {
                    swingLaunchTimer = 0f;
                    swingLaunchVelocityX = 0f;
                    targetVelocity = move * maxSpeed;
                }
                else
                {
                    swingLaunchTimer -= Time.deltaTime;
                    // Gradually blend high launch velocity with air drag
                    swingLaunchVelocityX = Mathf.MoveTowards(swingLaunchVelocityX, Mathf.Sign(swingLaunchVelocityX) * Mathf.Min(Mathf.Abs(swingLaunchVelocityX), maxSpeed), 4.5f * Time.deltaTime);
                    // Retain launch momentum while allowing air steering nudge
                    targetVelocity.x = swingLaunchVelocityX + move.x * 2.5f;
                    targetVelocity.y = 0f;
                }
            }
            else
            {
                targetVelocity = move * maxSpeed;
            }
        }

        private IEnumerator PerfectDodgeRoutine()
        {
            // Slow down time
            Time.timeScale = 0.2f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            // Wait for 1 real-time second
            yield return new WaitForSecondsRealtime(1.0f);

            // Restore time
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        public Vector2 GetParryReflectDirection(Vector3 projectilePos)
        {
            // Aim directly towards mouse cursor if valid
            if (Mouse.current != null && Camera.main != null)
            {
                Vector2 mouseScreen = Mouse.current.position.ReadValue();
                Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
                Vector2 toMouse = ((Vector2)mouseWorld - (Vector2)projectilePos);
                if (toMouse.sqrMagnitude > 0.05f)
                {
                    return toMouse.normalized;
                }
            }

            // Fallback: reflect forward in facing direction
            float facing = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
            return new Vector2(facing, 0.15f).normalized;
        }

        private bool DeflectNearbyProjectiles()
        {
            bool deflectedAny = false;
            // Generous proximity sweep so fast-traveling bullets don't slip through
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 1.8f);
            foreach (var hit in hits)
            {
                if (hit == null) continue;

                // 1. Deflect BossProjectile
                var proj = hit.GetComponent<Boss.BossProjectile>();
                if (proj != null && proj.isParriable && !proj.isReflected)
                {
                    Vector2 reflectDir = GetParryReflectDirection(proj.transform.position);
                    proj.Reflect(reflectDir);
                    OnParrySuccess(proj.gameObject);
                    deflectedAny = true;
                }

                // 2. Deflect BossHomingMissile
                var missile = hit.GetComponent<Boss.BossHomingMissile>();
                if (missile != null && !missile.IsReflected)
                {
                    Vector2 reflectDir = GetParryReflectDirection(missile.transform.position);
                    missile.Reflect(reflectDir);
                    OnParrySuccess(missile.gameObject);
                    deflectedAny = true;
                }
            }
            return deflectedAny;
        }

        private void SpawnParryWindEffect(Vector3? customOrigin = null)
        {
            if (parryWindEffect == null)
            {
                parryWindEffect = Resources.Load<SpriteSequenceEffect>("Vfx/ParryWindEffect");
            }

            if (parryWindEffect != null)
            {
                bool faceRight = spriteRenderer == null || !spriteRenderer.flipX;
                Vector2 center = collider2d != null ? (Vector2)collider2d.bounds.center : (Vector2)transform.position;
                Vector2 at = customOrigin.HasValue
                    ? (Vector2)customOrigin.Value
                    : center + new Vector2((faceRight ? 1f : -1f) * parryWindOffset.x, parryWindOffset.y);
                var instance = SpriteSequenceEffect.SpawnMirrored(parryWindEffect, at, faceRight);
                if (instance != null && !Mathf.Approximately(parryWindScale, 1.0f))
                {
                    instance.transform.localScale *= parryWindScale;
                }
                // Drawn behind Row so the wind frames the pose instead of covering it.
                if (instance != null && spriteRenderer != null)
                {
                    var windRenderer = instance.GetComponent<SpriteRenderer>();
                    windRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                    windRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
                }
            }
        }

        private void SpawnParrySparks(Vector3 impactPos, Vector2? reflectDir = null)
        {
            if (parrySparksPrefab == null)
            {
                parrySparksPrefab = Resources.Load<GameObject>("Vfx/ParrySparksEffect");
            }

            if (parrySparksPrefab != null)
            {
                Quaternion rot = Quaternion.identity;
                if (reflectDir.HasValue && reflectDir.Value.sqrMagnitude > 0.001f)
                {
                    float angle = Mathf.Atan2(reflectDir.Value.y, reflectDir.Value.x) * Mathf.Rad2Deg;
                    rot = Quaternion.Euler(0f, 0f, angle);
                }
                else
                {
                    float facing = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
                    rot = Quaternion.Euler(0f, 0f, facing > 0f ? 0f : 180f);
                }

                Instantiate(parrySparksPrefab, impactPos, rot);
            }
        }

        public void SetGuarding(bool active)
        {
            isGuarding = active;
            if (animator != null)
            {
                animator.SetBool("isGuarding", active);
            }
        }

        private IEnumerator ParryGuardRoutine()
        {
            isParrying = true;
            SetGuarding(true);
            parryCooldownTimer = parryCooldown;

            // Trigger character low-guard block animation (대검을 내려서 막는 모션)
            if (animator != null)
            {
                animator.Play("Player-Parry", 0, 0f);
            }

            Vector3 guardPos = collider2d != null ? (Vector3)collider2d.bounds.center : transform.position;
            // Subtle guard indicator on parry attempt (clean & non-intrusive)
            ExpandingRing.Spawn(guardPos, new Color(0.3f, 0.85f, 1f, 0.4f), 0.45f, 0.08f);

            // Immediately sweep any bullets already arriving
            bool hitBullet = DeflectNearbyProjectiles();
            if (!hitBullet && posture != null)
            {
                // Whiff posture on miss
                posture.OnParryMiss();
            }

            // Tight active parry frame window (0.22s) for reflection
            float elapsed = 0f;
            while (elapsed < parryDuration)
            {
                elapsed += Time.deltaTime;
                if (!isParrying) break; // Deflected a projectile successfully
                yield return null;
            }

            isParrying = false;

            // Continuous Guard: Holds the exact same lowered greatsword block pose as long as key/button is held
            while (posture == null || posture.CanParry)
            {
                bool guardHeld = (Keyboard.current != null && Keyboard.current.qKey.isPressed) ||
                                 (Mouse.current != null && Mouse.current.rightButton.isPressed);
                if (!guardHeld) break;
                yield return null;
            }

            SetGuarding(false);
        }

        public void OnGuardBlock(Vector3 impactPoint)
        {
            if (posture != null)
            {
                posture.AddPosture(16f);
            }

            // Spawn sand/dust puff at Row's feet upon blocking bullet
            SpawnBracingDust();
            PushBackFromBlock(impactPoint);

            // Visual feedback: Guard block sparks and yellow energy ring against the lowered greatsword
            ProjectileDebris.SpawnSpark(impactPoint, new Color(1f, 0.85f, 0.2f), 10, 6f);
            ProjectileDebris.SpawnSpark(impactPoint, Color.white, 5, 4f);
            ExpandingRing.Spawn(impactPoint, new Color(1f, 0.8f, 0.2f, 0.5f), 0.8f, 0.12f);

            // Slight camera shake
            CameraZoomController.TriggerShake(0.08f, 0.06f);
            AtmospherePostProcessController.TriggerGuardPulse();

            // Crisp metallic block sound feedback
            if (audioSource && jumpAudio)
            {
                float p = audioSource.pitch;
                audioSource.pitch = 1.35f;
                audioSource.PlayOneShot(jumpAudio, 0.65f);
                audioSource.pitch = p;
            }
        }

        /// <summary>
        /// A held guard gives ground: Row slides away from the blow. Only a guard does this; a just
        /// parry holds its ground. On the ground the slide stops short of a ledge.
        /// </summary>
        private void PushBackFromBlock(Vector3 impactPoint)
        {
            float away = impactPoint.x - transform.position.x;
            float direction = Mathf.Abs(away) > 0.05f
                ? -Mathf.Sign(away)
                : (spriteRenderer != null && spriteRenderer.flipX ? 1f : -1f);

            float speed = guardPushSpeed;
            if (!IsGrounded)
            {
                speed *= guardPushAirMultiplier;
            }
            else
            {
                // A push decays at a fixed rate, so its speed fixes how far it carries.
                float distance = speed * speed / (2f * ExternalPushDecay);
                speed = Mathf.Sqrt(2f * ExternalPushDecay * SupportedSlideDistance(direction, distance));
            }

            if (speed > 0f) Push(direction * speed);
        }

        /// <summary>
        /// How far Row can slide along the ground in a direction, up to a limit, before the feet
        /// would leave it.
        /// </summary>
        private float SupportedSlideDistance(float direction, float limit)
        {
            if (collider2d == null) return limit;

            const int steps = 4;
            var bounds = collider2d.bounds;
            float supported = 0f;
            for (int i = 1; i <= steps; i++)
            {
                float distance = limit * i / steps;
                var origin = new Vector2(bounds.center.x + direction * (bounds.extents.x + distance), bounds.min.y + 0.15f);
                int count = Physics2D.Raycast(origin, Vector2.down, contactFilter, guardPushGroundHits, 0.5f);
                bool ground = false;
                for (int h = 0; h < count; h++)
                {
                    if (guardPushGroundHits[h].collider != collider2d) ground = true;
                }
                if (!ground) break;
                supported = distance;
            }
            return supported;
        }

        public void OnParrySuccess(GameObject attacker)
        {
            // Prevent multiple parry triggers in the same frame
            if (!isParrying) return;
            
            isParrying = false; // End parry window
            bool guardHeld = (Keyboard.current != null && Keyboard.current.qKey.isPressed) ||
                             (Mouse.current != null && Mouse.current.rightButton.isPressed);
            SetGuarding(guardHeld);

            spriteRenderer.color = originalColor;

            // Shed accumulated posture on successful parry
            if (posture != null)
            {
                posture.OnParrySuccess();
            }

            // Grant brief invincibility to prevent immediate follow-up damage
            StartCoroutine(ParryInvincibilityRoutine());

            // The clash happens on the blade, wherever the attacker's own origin happens to be.
            // The impact pose draws its flash at the same point, so the sparks burst out of it.
            Vector3 impactPos = GetParryImpactPoint();
            Vector2 reflectDir = attacker != null
                ? GetParryReflectDirection(attacker.transform.position)
                : GetParryReflectDirection(impactPos);

            // Sparks run on unscaled time, so they fly while the hit-stop holds everything else.
            SpawnParrySparks(impactPos, reflectDir);

            // Camera shake on parry impact
            CameraZoomController.TriggerShake(0.18f, 0.12f);
            AtmospherePostProcessController.TriggerParryFlash();

            // Crisp "Clink / Parry" sound feedback
            if (audioSource && jumpAudio)
            {
                float p = audioSource.pitch;
                audioSource.pitch = 2.2f;
                audioSource.PlayOneShot(jumpAudio, 0.9f);
                audioSource.pitch = p;
            }

            // Dramatic visual: hold the Impact Deflection pose during the parry freeze. The ground
            // version has dust painted under the feet, so a parry in the air uses a clean one.
            parryImpactPose = IsGrounded || impactDeflectionAirSprite == null
                ? impactDeflectionSprite
                : impactDeflectionAirSprite;
            parryImpactPoseUntil = Time.unscaledTime + parryHitStopDuration;

            // The shockwave is the release of the blow, so it stays out of the freeze.
            StartCoroutine(ParryWindAfterFreezeRoutine(impactPos));

            // Hit-stop effect (momentary dramatic freeze-frame)
            if (perfectDodgeCoroutine != null) StopCoroutine(perfectDodgeCoroutine);
            perfectDodgeCoroutine = StartCoroutine(ParryTimeRoutine());
        }

        private IEnumerator ParryWindAfterFreezeRoutine(Vector3 impactPos)
        {
            yield return new WaitForSecondsRealtime(parryHitStopDuration);
            SpawnParryWindEffect(impactPos);
        }

        /// <summary>
        /// A just parry freezes, then eases back to full speed through a moment of slow motion.
        /// </summary>
        private IEnumerator ParryTimeRoutine()
        {
            yield return HitStopRoutine(0.05f, parryHitStopDuration);

            float elapsed = 0f;
            while (elapsed < parrySlowMotionDuration)
            {
                // A time scale of zero is the pause menu's, so the ease waits for it to end.
                if (Time.timeScale > 0f)
                {
                    float t = elapsed / parrySlowMotionDuration;
                    Time.timeScale = Mathf.Lerp(parrySlowMotionScale, 1f, t * t);
                    Time.fixedDeltaTime = 0.02f * Time.timeScale;
                    elapsed += Time.unscaledDeltaTime;
                }
                yield return null;
            }

            if (Time.timeScale > 0f) Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        /// <summary>
        /// World position of the flash drawn in the Impact Deflection pose.
        /// </summary>
        private Vector3 GetParryImpactPoint()
        {
            float facing = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
            Vector3 scale = transform.lossyScale;
            return transform.position + new Vector3(
                facing * ParryImpactOffset.x * Mathf.Abs(scale.x),
                ParryImpactOffset.y * Mathf.Abs(scale.y),
                0f);
        }

        // The animator writes the sprite after Update, so a pose set from gameplay code only
        // survives to the screen if it is written again here.
        private void LateUpdate()
        {
            if (parryImpactPose == null || spriteRenderer == null) return;

            if (Time.unscaledTime < parryImpactPoseUntil) spriteRenderer.sprite = parryImpactPose;
            else parryImpactPose = null;
        }

        private void HandleGuardBreak()
        {
            if (isParrying || isGuarding)
            {
                isParrying = false;
                SetGuarding(false);
                if (parryCoroutine != null) StopCoroutine(parryCoroutine);
                spriteRenderer.color = originalColor;
            }

            if (guardBreakRecoverySprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = guardBreakRecoverySprite;
            }
            else if (animator != null)
            {
                animator.SetTrigger("hurt");
            }
        }

        private IEnumerator ParryInvincibilityRoutine()
        {
            isInvincible = true;
            yield return new WaitForSeconds(0.25f);
            isInvincible = false;
        }

        private void SpawnBracingDust()
        {
            if (bracingDustSprite == null || !IsGrounded) return;
            bool flip = spriteRenderer != null && spriteRenderer.flipX;
            int order = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 1;
            string layer = spriteRenderer != null ? spriteRenderer.sortingLayerName : "Default";
            // The dust shares the player's canvas and pivot, so matching the scale puts it at the feet.
            Vector3 scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f);
            BracingDustEffect.Spawn(bracingDustSprite, transform.position, scale, flip, order, layer);
        }

        private void LoadParrySprites()
        {
            if (bracingShieldSprite == null)
                bracingShieldSprite = Resources.Load<Sprite>("ParrySprites/Bracing_Shield");
            if (bracingDustSprite == null)
                bracingDustSprite = Resources.Load<Sprite>("ParrySprites/Bracing_Dust");
            if (impactDeflectionSprite == null)
                impactDeflectionSprite = Resources.Load<Sprite>("ParrySprites/Impact_Deflection");
            if (impactDeflectionAirSprite == null)
                impactDeflectionAirSprite = Resources.Load<Sprite>("ParrySprites/Impact_Deflection_Air");
            if (guardBreakRecoverySprite == null)
                guardBreakRecoverySprite = Resources.Load<Sprite>("ParrySprites/Guard_Break_Recovery");
        }

        private IEnumerator HitStopRoutine(float slowTimeScale, float realDuration)
        {
            Time.timeScale = slowTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            yield return new WaitForSecondsRealtime(realDuration);

            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            if (isGuarding && bracingShieldSprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = bracingShieldSprite;
            }
        }

        /// <summary>
        /// Attempt an 8 directional warp, paying for it out of the stamina gauge.
        /// </summary>
        private void TryWarp()
        {
            if (warpCooldownTimer > 0f) return;

            if (stamina == null || !stamina.Has(warpCost))
            {
                OnWarpDenied();
                return;
            }

            bool airborne = !IsGrounded && !isClimbing && !isSwinging;
            if (airborne && airWarpsUsed >= maxAirWarps)
            {
                OnWarpDenied();
                return;
            }

            Vector2 direction = GetWarpDirection();
            Vector2 origin = body.position;

            if (!TryFindWarpDestination(origin, direction, out Vector2 destination))
            {
                // Every landing spot along the way is solid, so the warp is refused
                // without spending stamina.
                OnWarpDenied();
                return;
            }

            stamina.TrySpend(warpCost);
            warpCooldownTimer = warpCooldown;
            if (airborne) airWarpsUsed++;

            if (isClimbing)
            {
                StopClimbing();
            }

            // Anything crossed on the way still counts as a dodge.
            CheckWarpPassThrough(origin, destination);

            if (Mathf.Abs(direction.x) > 0.01f)
            {
                spriteRenderer.flipX = direction.x < 0f;
            }

            if (warpInvincibilityCoroutine != null) StopCoroutine(warpInvincibilityCoroutine);
            warpInvincibilityCoroutine = StartCoroutine(WarpInvincibilityRoutine());

            if (warpDashCoroutine != null) StopCoroutine(warpDashCoroutine);
            warpDashCoroutine = StartCoroutine(WarpDashTravelRoutine(origin, destination, direction));

            // Afterimages along the jumped path, plus a spark at both ends.
            WarpTrail trail = GetComponent<WarpTrail>();
            if (trail == null)
            {
                trail = gameObject.AddComponent<WarpTrail>();
            }
            trail.SpawnWarpTrail(origin, destination);
            AtmospherePostProcessController.TriggerWarpPulse();

            ProjectileDebris.SpawnSpark(origin, new Color(0.5f, 0.8f, 1f), 8, 5f);

            // Play warp sound (reusing jump sound with higher pitch)
            if (audioSource && jumpAudio)
            {
                float originalPitch = audioSource.pitch;
                audioSource.pitch = 1.8f;
                audioSource.PlayOneShot(jumpAudio, 0.6f);
                audioSource.pitch = originalPitch;
            }
        }

        private IEnumerator WarpDashTravelRoutine(Vector2 origin, Vector2 destination, Vector2 direction)
        {
            isWarpingDash = true;
            suspendGravity = true;
            skipPhysicsStep = true;
            velocity = Vector2.zero;
            targetVelocity = Vector2.zero;

            float dist = Vector2.Distance(origin, destination);
            float portalOffset = Mathf.Min(0.55f, dist * 0.25f);
            Vector2 entryPortalPos = origin + direction * portalOffset;
            Vector2 exitPortalPos = destination - direction * portalOffset;

            float enterDuration = 0.085f;
            float vanishDuration = 0.055f;
            float exitDuration = 0.065f;
            float totalTravel = enterDuration + vanishDuration + exitDuration;
            float elapsed = 0f;
            float z = transform.position.z;

            while (elapsed < totalTravel)
            {
                elapsed += Time.deltaTime;
                Vector2 pos;

                if (elapsed < enterDuration)
                {
                    // Phase 1: Dash forward from origin INTO the departure portal in front
                    float t = Mathf.Clamp01(elapsed / enterDuration);
                    float moveT = t < 0.22f ? 0f : (t - 0.22f) / 0.78f;
                    pos = Vector2.Lerp(origin, entryPortalPos, moveT * moveT);
                }
                else if (elapsed < enterDuration + vanishDuration)
                {
                    // Phase 2: Hidden inside dimensional space (warping from entry portal to exit portal)
                    float t = Mathf.Clamp01((elapsed - enterDuration) / vanishDuration);
                    pos = Vector2.Lerp(entryPortalPos, exitPortalPos, t);
                }
                else
                {
                    // Phase 3: Burst OUT from the arrival portal behind destination to destination
                    float t = Mathf.Clamp01((elapsed - enterDuration - vanishDuration) / exitDuration);
                    float easedOut = 1f - (1f - t) * (1f - t);
                    pos = Vector2.Lerp(exitPortalPos, destination, easedOut);
                }

                body.position = pos;
                transform.position = new Vector3(pos.x, pos.y, z);
                yield return null;
            }

            Teleport(new Vector3(destination.x, destination.y, z));
            transform.position = new Vector3(destination.x, destination.y, z);
            ProjectileDebris.SpawnSpark(destination, new Color(0.7f, 0.9f, 1f), 6, 4f);

            skipPhysicsStep = false;
            suspendGravity = false;
            isWarpingDash = false;
            // The up/down held to aim the warp must not latch onto a ladder or rope on arrival.
            climbCooldownTimer = Mathf.Max(climbCooldownTimer, 0.2f);

            if (direction.y > 0.1f)
            {
                velocity.y = 3.0f;
            }

            warpDashCoroutine = null;
        }

        /// <summary>
        /// Snaps the current move input to one of eight directions, falling back to the
        /// direction the player is facing when there is no input.
        /// </summary>
        private Vector2 GetWarpDirection()
        {
            float x = Mathf.Abs(moveInput.x) > 0.4f ? Mathf.Sign(moveInput.x) : 0f;
            float y = Mathf.Abs(moveInput.y) > 0.4f ? Mathf.Sign(moveInput.y) : 0f;

            if (x == 0f && y == 0f)
            {
                x = spriteRenderer.flipX ? -1f : 1f;
            }

            return new Vector2(x, y).normalized;
        }

        /// <summary>
        /// A warp may cross thin geometry, so only the landing spot has to be clear.
        /// Starts at full range and pulls back until an empty spot is found.
        /// </summary>
        private bool TryFindWarpDestination(Vector2 origin, Vector2 direction, out Vector2 destination)
        {
            const float stepBack = 0.25f;
            Vector2 colliderOffset = (Vector2)collider2d.bounds.center - body.position;
            Vector2 size = collider2d.bounds.size * 0.9f;

            for (float distance = warpDistance; distance >= stepBack; distance -= stepBack)
            {
                Vector2 candidate = origin + direction * distance;
                if (IsSpaceFree(candidate + colliderOffset, size))
                {
                    destination = candidate;
                    return true;
                }
            }

            destination = origin;
            return false;
        }

        private bool IsSpaceFree(Vector2 center, Vector2 size)
        {
            int count = Physics2D.OverlapBox(center, size, 0f, contactFilter, warpOverlapBuffer);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = warpOverlapBuffer[i];
                if (hit == null || hit.transform == transform) continue;
                // Enemies never block a warp, landing on top of one is the point.
                if (hit.GetComponentInParent<EnemyController>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Triggers the perfect dodge slow motion when the warp crossed an enemy.
        /// </summary>
        private void CheckWarpPassThrough(Vector2 origin, Vector2 destination)
        {
            Vector2 colliderOffset = (Vector2)collider2d.bounds.center - body.position;
            Vector2 extents = (Vector2)collider2d.bounds.extents;
            Vector2 from = origin + colliderOffset;
            Vector2 to = destination + colliderOffset;

            Collider2D[] hits = Physics2D.OverlapAreaAll(
                Vector2.Min(from, to) - extents,
                Vector2.Max(from, to) + extents);

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponentInParent<EnemyController>() == null) continue;

                if (perfectDodgeCoroutine != null) StopCoroutine(perfectDodgeCoroutine);
                perfectDodgeCoroutine = StartCoroutine(PerfectDodgeRoutine());
                return;
            }
        }

        private IEnumerator WarpInvincibilityRoutine()
        {
            isInvincible = true;
            yield return new WaitForSeconds(Mathf.Max(warpInvincibility, 0.28f));
            isInvincible = false;
        }

        /// <summary>
        /// Apply damage to the player, triggering hurt animation, knockback, and invulnerability frames.
        /// </summary>
        public void TakeDamage(int amount = 1, Vector2? knockback = null)
        {
            if (isInvincible || health == null || !health.IsAlive) return;

            if (isParrying) return;

            if (isGuarding)
            {
                OnGuardBlock(transform.position);
                return;
            }

            if (!infiniteHealth)
            {
                for (int i = 0; i < Mathf.Max(1, amount) && health.IsAlive; i++) health.Decrement();
                AtmospherePostProcessController.TriggerDamageFlash();
            }

            if (health.IsAlive)
            {
                if (isClimbing)
                {
                    StopClimbing();
                }

                if (audioSource && ouchAudio)
                    audioSource.PlayOneShot(ouchAudio);

                if (animator != null)
                    animator.SetTrigger("hurt");

                if (knockback.HasValue)
                {
                    Bounce(knockback.Value);
                }
                else
                {
                    Bounce(new Vector2(-Mathf.Sign(velocity.x != 0 ? velocity.x : 1f) * 3f, 4.5f));
                }

                if (hurtInvincibilityCoroutine != null) StopCoroutine(hurtInvincibilityCoroutine);
                hurtInvincibilityCoroutine = StartCoroutine(HurtInvincibilityRoutine());
            }
        }

        private IEnumerator HurtInvincibilityRoutine()
        {
            isInvincible = true;
            float elapsed = 0f;
            float interval = 0.1f;
            bool flicker = false;

            while (elapsed < hurtInvincibilityDuration)
            {
                elapsed += interval;
                flicker = !flicker;
                if (spriteRenderer != null)
                {
                    var c = spriteRenderer.color;
                    spriteRenderer.color = new Color(c.r, c.g, c.b, flicker ? 0.25f : 1.0f);
                }
                yield return new WaitForSeconds(interval);
            }

            ResetInvincibility();
        }

        /// <summary>
        /// Resets invincibility state and restores normal sprite alpha.
        /// </summary>
        public void ResetInvincibility()
        {
            if (hurtInvincibilityCoroutine != null)
            {
                StopCoroutine(hurtInvincibilityCoroutine);
                hurtInvincibilityCoroutine = null;
            }
            isInvincible = false;
            if (spriteRenderer != null)
            {
                var c = spriteRenderer.color;
                spriteRenderer.color = new Color(c.r, c.g, c.b, 1.0f);
            }
        }

        /// <summary>
        /// Feedback for a warp that could not happen: an empty gauge or a blocked exit.
        /// </summary>
        private void OnWarpDenied()
        {
            if (staminaUI != null) staminaUI.Flash();

            if (audioSource && jumpAudio)
            {
                float originalPitch = audioSource.pitch;
                audioSource.pitch = 0.6f;
                audioSource.PlayOneShot(jumpAudio, 0.25f);
                audioSource.pitch = originalPitch;
            }
        }

        /// <summary>
        /// Swings the sword. Consecutive clicks alternate between the horizontal slash and
        /// the thrust, so a held-down attack reads as a two hit combo.
        ///
        /// In the air that combo is dropped: both ground swings are built on footwork a jump
        /// does not have, so an airborne attack is always the same overhead swing.
        /// </summary>
        private void TryMelee()
        {
            if (meleeCooldownTimer > 0f) return;

            meleeCooldownTimer = meleeCooldown;

            if (!IsGrounded)
            {
                // Landing should open the ground combo from the top rather than resume
                // whichever half of it was next when the jump started.
                meleeComboIndex = 0;
                animator.SetTrigger("jumpSlash");
                StartCoroutine(MeleeHitRoutine(false, meleeWindup));
                return;
            }

            if (Time.time > meleeComboExpiry) meleeComboIndex = 0;
            meleeComboExpiry = Time.time + meleeComboWindow;

            bool thrust = meleeComboIndex % 2 == 1;
            meleeComboIndex++;

            animator.SetTrigger(thrust ? "pierce" : "slash");

            if (!thrust) StartCoroutine(SlashEffectRoutine());

            StartCoroutine(MeleeHitRoutine(thrust, thrust ? meleeWindup : slashWindup));
        }

        /// <summary>
        /// Throws the slash arc once the blade is actually sweeping. Facing is read after the
        /// wait, not before, so turning mid swing does not leave the arc pointing the old way.
        /// </summary>
        private IEnumerator SlashEffectRoutine()
        {
            if (slashEffectDelay > 0f) yield return new WaitForSeconds(slashEffectDelay);
            if (slashEffect == null) yield break;

            bool faceRight = !spriteRenderer.flipX;
            Vector2 centre = collider2d.bounds.center;
            Vector2 at = centre + new Vector2((faceRight ? 1f : -1f) * slashEffectOffset.x, slashEffectOffset.y);
            SpriteSequenceEffect.SpawnMirrored(slashEffect, at, faceRight);
        }

        /// <summary>
        /// Hides the character, plays <see cref="spawnEffect"/> at its feet and shows the
        /// character again once the flame has risen over it. Call right after teleporting.
        /// </summary>
        /// <param name="holdControl">Keep input off until the character shows (game start;
        /// a respawn already holds input through its own timer).</param>
        public void PlaySpawnEffect(bool holdControl = false)
        {
            if (spawnEffect == null) return;
            if (spawnEffectRoutine != null)
            {
                // Show the character again first, or the new run would record it as hidden.
                StopCoroutine(spawnEffectRoutine);
                RestoreSpawnHidden();
            }
            spawnEffectRoutine = StartCoroutine(SpawnEffectRoutine(holdControl));
        }

        private SpriteRenderer[] spawnHiddenRenderers;
        private bool[] spawnHiddenWasOn;

        private void RestoreSpawnHidden()
        {
            if (spawnHiddenRenderers == null) return;
            for (int i = 0; i < spawnHiddenRenderers.Length; i++)
                if (spawnHiddenRenderers[i] != null) spawnHiddenRenderers[i].enabled = spawnHiddenWasOn[i];
            spawnHiddenRenderers = null;
        }

        private IEnumerator SpawnEffectRoutine(bool holdControl)
        {
            bool restoreControl = holdControl && controlEnabled;
            if (restoreControl) controlEnabled = false;

            // Child renderers (shadow, weapon bits) go too; only the ones that were on come back.
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            var wasOn = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                wasOn[i] = renderers[i].enabled;
                renderers[i].enabled = false;
            }
            spawnHiddenRenderers = renderers;
            spawnHiddenWasOn = wasOn;

            // Teleport moves the rigidbody; the collider bounds follow after the physics step.
            yield return new WaitForFixedUpdate();
            Bounds body = collider2d.bounds;
            var feet = new Vector3(body.center.x, body.min.y, transform.position.z);
            var fx = Instantiate(spawnEffect, feet, Quaternion.identity);
            var fxRenderer = fx.GetComponent<SpriteRenderer>();
            if (fxRenderer != null && spriteRenderer != null)
            {
                fxRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                fxRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }

            yield return new WaitForSeconds(spawnRevealDelay);
            RestoreSpawnHidden();
            if (restoreControl) controlEnabled = true;
            spawnEffectRoutine = null;
        }

        private IEnumerator MeleeHitRoutine(bool thrust, float windup)
        {
            yield return new WaitForSeconds(windup);

            // The hit box is derived from the collider, so it keeps its proportions
            // whatever scale the character is at.
            Bounds body = collider2d.bounds;
            float facing = spriteRenderer.flipX ? -1f : 1f;
            // The thrust reaches further, the slash covers more height.
            Vector2 size = thrust
                ? new Vector2(body.size.x * 2.6f, body.size.y * 0.55f)
                : new Vector2(body.size.x * 1.9f, body.size.y * 0.9f);
            Vector2 center = (Vector2)body.center + new Vector2(facing * (body.size.x * 0.5f + size.x * 0.5f), 0f);

            // Enemies may use trigger colliders, so the sweep has to see those too.
            ContactFilter2D meleeFilter = ContactFilter2D.noFilter;
            meleeFilter.useTriggers = true;

            int count = Physics2D.OverlapBox(center, size, 0f, meleeFilter, meleeHitBuffer);
            bool connected = false;
            // An enemy built from several colliders (the mortar's body plus its ammo rack) is still
            // one target, and takes one hit per swing.
            var struck = new System.Collections.Generic.List<EnemyController>(count);
            var struckHittables = new System.Collections.Generic.List<IPlayerHittable>();
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = meleeHitBuffer[i];
                if (hit == null || hit.transform == transform) continue;

                EnemyController enemy = hit.GetComponentInParent<EnemyController>();
                // A hittable on the struck collider itself takes the hit, even when it hangs off an
                // enemy (a boss's breakable pods and ducts); otherwise only a free-standing one does.
                var hittable = hit.GetComponent<IPlayerHittable>();
                if (hittable == null && enemy == null) hittable = hit.GetComponentInParent<IPlayerHittable>();
                if (hittable != null)
                {
                    if (struckHittables.Contains(hittable)) continue;
                    struckHittables.Add(hittable);
                    connected = true;
                    ProjectileDebris.SpawnSpark(hit.bounds.center, new Color(1f, 0.35f, 0.15f), 10, 6f);
                    hittable.OnPlayerHit(hit.bounds.center);
                    continue;
                }
                if (enemy == null || struck.Contains(enemy)) continue;
                struck.Add(enemy);

                if (!enemy.CanTakeDamage(hit.bounds.center, transform.position))
                {
                    enemy.OnBlockedHit(hit.bounds.center, transform.position);
                    connected = true;
                    continue;
                }

                connected = true;
                ProjectileDebris.SpawnSpark(hit.bounds.center, new Color(1f, 0.35f, 0.15f), 10, 6f);
                ExpandingRing.Spawn(hit.bounds.center, new Color(1f, 0.9f, 0.7f, 0.8f), body.size.y * 3f, 0.18f);

                Health enemyHealth = enemy.GetComponent<Health>();
                if (enemyHealth != null)
                {
                    enemyHealth.Decrement();
                    if (!enemyHealth.IsAlive) Schedule<EnemyDeath>().enemy = enemy;
                }
                else
                {
                    Schedule<EnemyDeath>().enemy = enemy;
                }
            }

            PlayClick(connected ? 1.4f : 0.9f, connected ? 0.5f : 0.25f);

            if (connected)
            {
                if (perfectDodgeCoroutine != null) StopCoroutine(perfectDodgeCoroutine);
                perfectDodgeCoroutine = StartCoroutine(HitStopRoutine(0.15f, 0.06f));
            }
        }

        /// <summary>
        /// Raises or lowers the aim reticle. The hardware cursor is hidden while aiming so
        /// that the reticle reads as the crosshair.
        /// </summary>
        public void SetAiming(bool aiming)
        {
            if (IsAiming == aiming) return;

            IsAiming = aiming;
            Cursor.visible = !aiming;
        }

        /// <summary>
        /// Turns the player towards the cursor while aiming.
        /// </summary>
        private void FaceMouseCursor()
        {
            if (!TryGetMouseWorldPosition(out Vector3 mouseWorldPosition)) return;

            if (mouseWorldPosition.x > transform.position.x)
            {
                spriteRenderer.flipX = false;
            }
            else if (mouseWorldPosition.x < transform.position.x)
            {
                spriteRenderer.flipX = true;
            }
        }

        public bool TryGetAimWorldPosition(out Vector3 worldPosition, out EnemyController targetedEnemy)
        {
            worldPosition = transform.position;
            targetedEnemy = null;
            if (Camera.main == null || Mouse.current == null) return false;

            // Get mouse position in world space
            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector3 mouseScreenPosWithZ = new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, 0f);

            // Set projection depth exactly to the distance between the camera's Z plane and the player's Z plane.
            mouseScreenPosWithZ.z = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);

            worldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosWithZ);
            worldPosition.z = transform.position.z;

            // Lock only onto an enemy the crosshair is actually on (within sniperImpactRadius).
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPosition, sniperImpactRadius);
            float closestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                EnemyController enemy = hit.GetComponentInParent<EnemyController>();
                if (enemy != null)
                {
                    float dist = ((Vector2)hit.bounds.ClosestPoint(worldPosition) - (Vector2)worldPosition).sqrMagnitude;
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        targetedEnemy = enemy;
                    }
                }
            }

            return true;
        }

        private bool TryGetMouseWorldPosition(out Vector3 worldPosition)
        {
            return TryGetAimWorldPosition(out worldPosition, out _);
        }

        /// <summary>
        /// Fires a round if the magazine and the fire rate allow it. An empty magazine
        /// starts a reload instead.
        /// </summary>
        private void TryShoot()
        {
            if (IsReloading || Time.time < nextFireTime) return;

            if (ammoInMagazine <= 0)
            {
                StartReload();
                return;
            }

            Shoot();
            ammoInMagazine--;
            nextFireTime = Time.time + fireRate;
            OnSniperShot?.Invoke();

            if (ammoInMagazine <= 0) StartReload();
        }

        private void StartReload()
        {
            if (IsReloading || ammoInMagazine >= magazineSize) return;

            reloadCoroutine = StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            reloadEndTime = Time.time + reloadTime;

            // Low click as the magazine drops out.
            PlayClick(0.5f, 0.3f);

            while (Time.time < reloadEndTime)
            {
                yield return null;
            }

            ammoInMagazine = magazineSize;
            reloadCoroutine = null;

            // Higher click once the fresh magazine is seated.
            PlayClick(1.2f, 0.35f);
        }

        /// <summary>
        /// Tops the magazine up instantly, e.g. on respawn.
        /// </summary>
        public void RefillAmmo()
        {
            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
                reloadCoroutine = null;
            }
            ammoInMagazine = magazineSize;
        }

        private void PlayClick(float pitch, float volume)
        {
            if (!audioSource || !jumpAudio) return;

            float originalPitch = audioSource.pitch;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(jumpAudio, volume);
            audioSource.pitch = originalPitch;
        }

        void Shoot()
        {
            if (!TryGetAimWorldPosition(out Vector3 targetPosition, out EnemyController targetedEnemy)) return;

            // A hittable part right under the crosshair (a boss's pod or duct, an escort drone, a
            // missile) is what the player is aiming at: keep the exact point instead of snapping to
            // the centre of the enemy it hangs off.
            Collider2D aimedPart = FindAimedPart(targetPosition);

            // The round lands where the crosshair is; an enemy locked under it is still guaranteed the
            // hit if it moves during the short flight.
            // Launch supersonic AMR .50 BMG bullet from off-screen overwatch sniper into the designated point
            GameObject roundObject = new GameObject("AMR_SniperRound");
            IncomingRound round = roundObject.AddComponent<IncomingRound>();
            round.impactRadius = sniperImpactRadius;
            round.bossDamageFraction = sniperBossDamageFraction;
            round.Launch(targetPosition, targetedEnemy, aimedPart);
        }

        // The hittable collider under the aim point (within sniperImpactRadius), nearest first.
        private Collider2D FindAimedPart(Vector2 aim)
        {
            Collider2D best = null;
            float bestDist = float.MaxValue;
            foreach (var hit in Physics2D.OverlapCircleAll(aim, sniperImpactRadius))
            {
                if (hit == null || !hit.enabled || hit.GetComponent<IPlayerHittable>() == null) continue;
                float dist = hit.OverlapPoint(aim) ? 0f : ((Vector2)hit.bounds.ClosestPoint(aim) - aim).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = hit;
                }
            }
            return best;
        }

        #region Climbing Implementation

        public void StartClimbingLadder(LadderZone ladder)
        {
            if (ladder == null) return;
            isClimbing = true;
            currentLadder = ladder;
            currentRope = null;
            IsRopeSliding = false;
            suspendGravity = true;
            velocity = Vector2.zero;
            jumpState = JumpState.InFlight;
        }

        public void StartClimbingRope(RopeZone rope)
        {
            if (rope == null) return;
            isClimbing = true;
            currentRope = rope;
            currentLadder = null;
            IsRopeSliding = false;
            suspendGravity = true;
            velocity = Vector2.zero;
            jumpState = JumpState.InFlight;
        }

        public void StopClimbing()
        {
            if (!isClimbing) return;
            isClimbing = false;
            currentLadder = null;
            currentRope = null;
            IsRopeSliding = false;
            suspendGravity = false;
        }

        public void JumpOffClimb()
        {
            StopClimbing();
            climbCooldownTimer = 0.25f;

            if (Mathf.Abs(moveInput.x) > 0.1f)
            {
                velocity.x = Mathf.Sign(moveInput.x) * maxSpeed;
                velocity.y = jumpTakeOffSpeed * 0.9f;
                spriteRenderer.flipX = velocity.x < 0f;
            }
            else
            {
                velocity.y = jumpTakeOffSpeed * 0.7f;
            }

            jumpState = JumpState.InFlight;
            if (Application.isPlaying)
            {
                Schedule<PlayerJumped>().player = this;
            }
        }

        public void StartSwinging(SwingRope swingRope)
        {
            if (swingRope == null) return;
            StopClimbing();
            isSwinging = true;
            CurrentSwingRope = swingRope;
            nearbySwingRope = swingRope;
            suspendGravity = true;
            velocity = Vector2.zero;
            targetVelocity = Vector2.zero;
            swingLaunchTimer = 0f;
            swingLaunchVelocityX = 0f;
            jumpState = JumpState.InFlight;

            if (animator != null)
            {
                animator.SetBool("isClimbingRope", true);
                animator.SetBool("isClimbingLadder", false);
                animator.SetBool("isRopeSliding", false);
                animator.SetFloat("climbSpeed", 1f);
                animator.Play("Player-RopeClimb", 0, 0f);
            }
        }

        public void ReleaseSwing(Vector2 releaseVelocity)
        {
            if (!isSwinging) return;
            isSwinging = false;
            CurrentSwingRope = null;
            suspendGravity = false;
            climbCooldownTimer = 0.35f;
            velocity = releaseVelocity;
            swingLaunchVelocityX = releaseVelocity.x;
            swingLaunchTimer = 1.8f;

            if (Mathf.Abs(releaseVelocity.x) > 0.1f && spriteRenderer != null)
            {
                spriteRenderer.flipX = releaseVelocity.x < 0f;
            }
            jumpState = JumpState.InFlight;
            if (Application.isPlaying)
            {
                Schedule<PlayerJumped>().player = this;
            }
        }

        public void TryGrabNearbySwingRope()
        {
            if (nearbySwingRope != null && nearbySwingRope.IsPlayerInGrabRange(this))
            {
                nearbySwingRope.TryAttachPlayer(this);
                return;
            }

            var ropes = Object.FindObjectsByType<SwingRope>();
            for (int i = 0; i < ropes.Length; i++)
            {
                if (ropes[i].IsPlayerInGrabRange(this))
                {
                    if (ropes[i].TryAttachPlayer(this))
                    {
                        nearbySwingRope = ropes[i];
                        break;
                    }
                }
            }
        }

        // Same footprint as the probe in UpdateNearbyClimbables.
        private static readonly Vector2 ClimbProbeSize = new Vector2(1.2f, 1.8f);

        private bool IsTouchingZone(Component zone)
        {
            var c = zone != null ? zone.GetComponent<Collider2D>() : null;
            if (c == null) return false;
            Bounds b = c.bounds;
            Vector2 p = body.position;
            Vector2 half = ClimbProbeSize * 0.5f + new Vector2(0.1f, 0.1f);
            return p.x >= b.min.x - half.x && p.x <= b.max.x + half.x
                && p.y >= b.min.y - half.y && p.y <= b.max.y + half.y;
        }

        private void UpdateNearbyClimbables()
        {
            // These references are set by trigger callbacks and the probe below but only cleared in
            // OnTriggerExit2D. A warp moves the body without a physics step, so it can carry the
            // player out of a zone with no exit callback; the stale ladder then caught the up input
            // that aimed the warp and dragged the player back onto it. Drop any zone not touched now.
            if (nearbyLadder != null && !IsTouchingZone(nearbyLadder)) nearbyLadder = null;
            if (nearbyRope != null && !IsTouchingZone(nearbyRope)) nearbyRope = null;

            var hits = Physics2D.OverlapBoxAll(body.position, ClimbProbeSize, 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].isTrigger)
                {
                    if (nearbyLadder == null)
                    {
                        var l = hits[i].GetComponentInParent<LadderZone>();
                        if (l != null) nearbyLadder = l;
                    }
                    if (nearbyRope == null)
                    {
                        var r = hits[i].GetComponentInParent<RopeZone>();
                        if (r != null) nearbyRope = r;
                    }
                    if (nearbySwingRope == null || !nearbySwingRope.IsPlayerInGrabRange(this))
                    {
                        var s = hits[i].GetComponentInParent<SwingRope>();
                        if (s != null && s.IsPlayerInGrabRange(this))
                        {
                            nearbySwingRope = s;
                        }
                    }
                }
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var ladder = other.GetComponentInParent<LadderZone>();
            if (ladder != null) nearbyLadder = ladder;

            var rope = other.GetComponentInParent<RopeZone>();
            if (rope != null) nearbyRope = rope;

            var swing = other.GetComponentInParent<SwingRope>();
            if (swing != null) nearbySwingRope = swing;
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var ladder = other.GetComponentInParent<LadderZone>();
            if (ladder != null) nearbyLadder = ladder;

            var rope = other.GetComponentInParent<RopeZone>();
            if (rope != null) nearbyRope = rope;

            var swing = other.GetComponentInParent<SwingRope>();
            if (swing != null) nearbySwingRope = swing;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            // The dismount checks key off the zone being climbed, not nearbyLadder/nearbyRope, which
            // UpdateNearbyClimbables may already have cleared this frame.
            var ladder = other.GetComponentInParent<LadderZone>();
            if (ladder != null)
            {
                if (nearbyLadder == ladder) nearbyLadder = null;
                // Only dismount if we moved far horizontally away from the ladder
                if (currentLadder == ladder && Mathf.Abs(body.position.x - ladder.CenterX) > 0.8f)
                {
                    StopClimbing();
                }
            }

            var rope = other.GetComponentInParent<RopeZone>();
            if (rope != null)
            {
                if (nearbyRope == rope) nearbyRope = null;
                // Only dismount if we moved far horizontally away from the rope
                if (currentRope == rope && Mathf.Abs(body.position.x - rope.CenterX) > 0.8f)
                {
                    StopClimbing();
                }
            }

            var swing = other.GetComponentInParent<SwingRope>();
            if (swing != null && nearbySwingRope == swing)
            {
                if (!swing.IsPlayerInGrabRange(this, swing.grabRadius + 0.5f))
                {
                    nearbySwingRope = null;
                }
            }
        }

        protected override bool IgnoreCollision(RaycastHit2D hit)
        {
            if (isSwinging)
            {
                return true;
            }

            if (isClimbing)
            {
                // When climbing, side walls or edges should never block vertical climbing
                if (Mathf.Abs(hit.normal.x) > 0.3f)
                {
                    return true;
                }
                // If climbing upward, allow passing through ledge bottoms or platform edges
                if (velocity.y > 0)
                {
                    float topLimit = currentLadder != null ? (currentLadder.TopY + 0.5f) : (currentRope != null ? currentRope.TopY : float.MaxValue);
                    if (hit.point.y < topLimit)
                    {
                        return true;
                    }
                }
            }
            return base.IgnoreCollision(hit);
        }

        #endregion

        public enum JumpState
        {
            Grounded,
            PrepareToJump,
            Jumping,
            InFlight,
            Landed
        }
    }
}