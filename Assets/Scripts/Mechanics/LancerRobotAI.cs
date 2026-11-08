using System.Collections;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Model;
using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Fast-moving Spearman / Lancer Robot AI Controller.
    /// A high-speed biomechanical crawler equipped with a heavy pneumatic piledriver lance.
    ///
    /// Combat Design:
    /// - Patrol / Stalk: Predatory low crawler stride, tilts lance towards player.
    /// - Telegraph / Pneumatic Charge: Pulls lance back, compresses coil spring with electric arcs and steam hiss.
    /// - Dash Thrust: Explosive supersonic ground dash forward with pneumatic piston extension.
    /// - Counterplay: Jump over the thrust, parry the lance, or strike during the post-thrust recovery stall.
    /// </summary>
    [SelectionBase]
    [RequireComponent(typeof(Health))]
    public class LancerRobotAI : EnemyController
    {
        public enum LancerState
        {
            Patrol,
            Stalking,
            Charging,
            DashThrust,
            Recovery,
            Stunned
        }

        [Header("Current State")]
        public LancerState currentState = LancerState.Patrol;
        public bool isFacingLeft = true;

        [Header("Movement & Range")]
        public float patrolSpeed = 2.4f;
        public float stalkSpeed = 3.2f;
        public float detectionRadius = 11.0f;
        public float attackRange = 7.5f;
        public float minAttackDistance = 1.6f;
        public float verticalDetectLimit = 3.5f;
        public float selfPatrolDistance = 4.0f;
        public float ledgeCheckDepth = 0.8f;

        [Header("Dash Thrust Attack")]
        public float chargeDuration = 0.5f;
        public float dashSpeed = 12.0f;
        public float dashMaxDuration = 0.42f;
        public float recoveryDuration = 1.0f;
        public float attackCooldown = 2.2f;
        public int thrustDamage = 1;
        public float thrustReach = 1.2f;
        public float lancePistonThrustOffset = 0.7f;

        [Header("Stun & Vulnerability")]
        public float stunDuration = 2.0f;

        [Header("Gait Feel")]
        [Tooltip("Cycle advance per world unit travelled. Higher means quicker, shorter steps. " +
                 "Step length works out to pi / this, so 3.4 gives a 0.92 unit step.")]
        public float stridePerUnit = 3.4f;
        [Tooltip("1 means the planted foot tracks the ground exactly. Above 1 exaggerates the " +
                 "swing, which starts to look like sliding.")]
        public float strideExaggeration = 1.15f;
        [Tooltip("Upper limit on the swing, in degrees.")]
        public float maxStrideAngle = 44.0f;
        [Tooltip("Swing/stance asymmetry. 0 is a plain sine; higher throws the leg forward " +
                 "quickly and holds the stance longer, which is what reads as walking.")]
        public float strideSkew = 0.35f;
        [Tooltip("Stride swing in degrees during the dash thrust.")]
        public float dashStrideAngle = 46.0f;
        [Tooltip("Upper limit on step rate, so the dash does not blur the legs.")]
        public float maxCadence = 20.0f;
        [Tooltip("How far the body rises and falls on each step.")]
        public float bodyBounce = 0.10f;
        [Tooltip("How far the swinging leg lifts clear of the ground.")]
        public float footLift = 0.08f;
        [Tooltip("Forward lean in degrees while running.")]
        public float runLean = 5.0f;
        [Tooltip("Forward lean in degrees during the dash thrust.")]
        public float dashLean = 9.0f;
        public bool footDust = true;
        public Color dustColor = new Color(0.85f, 0.8f, 0.7f, 1f);

        [Header("Articulated Visuals")]
        public Transform visualRoot;
        public Transform bodyTransform;
        public Transform hipFront;
        public Transform kneeFront;
        public Transform hipRear;
        public Transform kneeRear;
        public Transform lancePivot;
        public Transform lanceTransform;
        public Transform lanceTip;
        public Transform exhaustVent;
        public Transform ankleFront;
        public Transform footFront;
        public Transform ankleRear;
        public Transform footRear;
        public SpriteRenderer bodyRenderer;
        public SpriteRenderer thighFrontRenderer;
        public SpriteRenderer shinFrontRenderer;
        public SpriteRenderer footFrontRenderer;
        public SpriteRenderer thighRearRenderer;
        public SpriteRenderer shinRearRenderer;
        public SpriteRenderer footRearRenderer;
        public SpriteRenderer lanceRenderer;

        // Backward compatibility accessors
        public Transform legFrontPivot => hipFront;
        public Transform legRearPivot => hipRear;
        public SpriteRenderer legFrontRenderer => thighFrontRenderer;
        public SpriteRenderer legRearRenderer => thighRearRenderer;

        [Header("Audio")]
        public AudioClip chargeSound;
        public AudioClip thrustSound;
        public AudioClip hitSound;
        public AudioClip steamSound;

        private Health _health;
        private Rigidbody2D _rb;
        private PlayerController _player;

        private float _spawnX;
        private int _patrolDir = -1;
        private float _turnCooldown = 0f;
        private float _nextAttackTime = 0f;
        private bool _isAttacking = false;
        private float _moveX = 0f;
        private float _fallSpeed = 0f;
        private float _currentLanceAngle = 0f;
        private float _walkCycle = 0f;
        private float _stepSpeed = 0f;
        private int _lastStepIndex = 0;

        // 3-Bone Inverse Kinematics constants (calibrated to the modular sprite artwork)
        public const float L1 = 0.6905f; // Thigh length (Hip to Knee)
        public const float L2 = 0.6414f; // Shin length (Knee to Ankle)
        public const float Theta1_0 = -2.8273f; // Initial thigh angle in sprite (-162.0 deg)
        public const float Theta2_0 = -2.0690f; // Initial shin angle in sprite (-118.53 deg)
        public const float GroundAnkleY = -1.005f; // Ankle ground level relative to Hip in VisualRoot
        public const float FootFlatAngle = 26.0f; // World angle in VisualRoot where foot sole is 100% horizontal flat
        public const float NeutralAnkleX = -0.04f; // Neutral ankle X relative to hip

        // Backward compatibility
        public const float GroundY = GroundAnkleY;
        public const float NeutralFootX = NeutralAnkleX;

        private Vector2 _currentFrontAnkle = new Vector2(NeutralAnkleX - 0.20f, GroundAnkleY);
        private Vector2 _currentRearAnkle = new Vector2(NeutralAnkleX + 0.20f, GroundAnkleY);
        private float _currentFrontFootPitch = 0f;
        private float _currentRearFootPitch = 0f;

        private Vector3 _initialBodyPos;
        private Vector3 _initialLanceLocalPos;
        private Vector3 _lanceThrustOffset = Vector3.zero;
        private Vector3 _lanceRecoilOffset = Vector3.zero;

        private ContactFilter2D _terrainFilter;
        private readonly RaycastHit2D[] _castHits = new RaycastHit2D[8];
        private readonly Collider2D[] _hitColliders = new Collider2D[8];

        private const float Skin = 0.02f;
        private static readonly Color ChargeTint = new Color(1.2f, 1.1f, 0.8f, 1f);
        private static readonly Color StunTint = new Color(0.5f, 0.8f, 1f, 0.75f);

        protected override void Awake()
        {
            base.Awake();

            currentState = LancerState.Patrol;
            _isAttacking = false;
            _moveX = 0f;
            _fallSpeed = 0f;
            _spawnX = transform.position.x;
            _nextAttackTime = Time.time + 0.5f;

            _health = GetComponent<Health>();
            if (_health == null) _health = gameObject.AddComponent<Health>();
            _health.maxHP = 4;
            _health.ResetHP();

            if (_health != null)
            {
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
            ResetRigLocalTransforms();

            if (control != null) control.enabled = false;
        }

        protected virtual void OnEnable()
        {
            _spawnX = transform.position.x;
            _moveX = 0f;
            _fallSpeed = 0f;
            _patrolDir = isFacingLeft ? -1 : 1;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (!Application.isPlaying)
            {
                InitModularParts();
                ResetRigLocalTransforms();
            }
        }
#endif

        [ContextMenu("Reset Modular Rig Transforms")]
        public void ResetRigLocalTransforms()
        {
            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
            }
            if (bodyTransform != null)
            {
                bodyTransform.localPosition = Vector3.zero;
                bodyTransform.localRotation = Quaternion.identity;
                _initialBodyPos = Vector3.zero;
            }
            if (lancePivot != null)
            {
                lancePivot.localPosition = Vector3.zero;
                lancePivot.localRotation = Quaternion.identity;
            }
            if (lanceTransform != null)
            {
                lanceTransform.localPosition = Vector3.zero;
                lanceTransform.localRotation = Quaternion.identity;
                _initialLanceLocalPos = Vector3.zero;
            }

            // Front Leg Rig
            if (hipFront != null)
            {
                hipFront.localPosition = new Vector3(0.505f, 1.158f, 0f);
                var tFront = hipFront.Find("Thigh_Front");
                if (tFront != null) { tFront.localPosition = new Vector3(-0.505f, -1.158f, 0f); tFront.localRotation = Quaternion.identity; }
            }
            if (kneeFront != null)
            {
                kneeFront.localPosition = new Vector3(-0.6567f, -0.2133f, 0f);
                var sFront = kneeFront.Find("Shin_Front");
                if (sFront != null) { sFront.localPosition = new Vector3(0.1517f, -0.945f, 0f); sFront.localRotation = Quaternion.identity; }
            }
            if (ankleFront != null)
            {
                ankleFront.localPosition = new Vector3(-0.3067f, -0.5633f, 0f);
                var fFront = ankleFront.Find("Foot_Front");
                if (fFront != null) { fFront.localPosition = new Vector3(0.4583f, -0.3817f, 0f); fFront.localRotation = Quaternion.identity; }
            }

            // Rear Leg Rig
            if (hipRear != null)
            {
                hipRear.localPosition = new Vector3(0.505f, 1.158f, 0f);
                var tRear = hipRear.Find("Thigh_Back");
                if (tRear != null) { tRear.localPosition = new Vector3(-0.505f, -1.158f, 0f); tRear.localRotation = Quaternion.identity; }
            }
            if (kneeRear != null)
            {
                kneeRear.localPosition = new Vector3(-0.6567f, -0.2133f, 0f);
                var sRear = kneeRear.Find("Shin_Back");
                if (sRear != null) { sRear.localPosition = new Vector3(0.1517f, -0.945f, 0f); sRear.localRotation = Quaternion.identity; }
            }
            if (ankleRear != null)
            {
                ankleRear.localPosition = new Vector3(-0.3067f, -0.5633f, 0f);
                var fRear = ankleRear.Find("Foot_Back");
                if (fRear != null) { fRear.localPosition = new Vector3(0.4583f, -0.3817f, 0f); fRear.localRotation = Quaternion.identity; }
            }
        }

        private void InitModularParts()
        {
            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            Transform parent = visualRoot != null ? visualRoot : transform;

            if (bodyTransform == null) bodyTransform = parent.Find("Body");
            
            // Front Leg Rig
            if (hipFront == null) hipFront = parent.Find("Hip_Front");
            if (hipFront != null)
            {
                if (kneeFront == null) kneeFront = hipFront.Find("Knee_Front");
                if (thighFrontRenderer == null)
                {
                    var tObj = hipFront.Find("Thigh_Front");
                    thighFrontRenderer = tObj != null ? tObj.GetComponent<SpriteRenderer>() : hipFront.GetComponentInChildren<SpriteRenderer>();
                }
                if (kneeFront != null)
                {
                    if (shinFrontRenderer == null)
                    {
                        var sObj = kneeFront.Find("Shin_Front");
                        shinFrontRenderer = sObj != null ? sObj.GetComponent<SpriteRenderer>() : null;
                    }
                    if (ankleFront == null) ankleFront = kneeFront.Find("Ankle_Front");
                    if (ankleFront != null)
                    {
                        if (footFront == null) footFront = ankleFront.Find("Foot_Front");
                        if (footFrontRenderer == null)
                        {
                            footFrontRenderer = footFront != null ? footFront.GetComponent<SpriteRenderer>() : ankleFront.GetComponentInChildren<SpriteRenderer>();
                        }
                    }
                }
            }

            // Rear Leg Rig
            if (hipRear == null) hipRear = parent.Find("Hip_Back");
            if (hipRear != null)
            {
                if (kneeRear == null) kneeRear = hipRear.Find("Knee_Back");
                if (thighRearRenderer == null)
                {
                    var tObj = hipRear.Find("Thigh_Back");
                    thighRearRenderer = tObj != null ? tObj.GetComponent<SpriteRenderer>() : hipRear.GetComponentInChildren<SpriteRenderer>();
                }
                if (kneeRear != null)
                {
                    if (shinRearRenderer == null)
                    {
                        var sObj = kneeRear.Find("Shin_Back");
                        shinRearRenderer = sObj != null ? sObj.GetComponent<SpriteRenderer>() : null;
                    }
                    if (ankleRear == null) ankleRear = kneeRear.Find("Ankle_Back");
                    if (ankleRear != null)
                    {
                        if (footRear == null) footRear = ankleRear.Find("Foot_Back");
                        if (footRearRenderer == null)
                        {
                            footRearRenderer = footRear != null ? footRear.GetComponent<SpriteRenderer>() : ankleRear.GetComponentInChildren<SpriteRenderer>();
                        }
                    }
                }
            }

            // Lance Rig
            if (lancePivot == null)
            {
                lancePivot = bodyTransform != null ? bodyTransform.Find("LancePivot") : parent.Find("LancePivot");
            }
            if (lanceTransform == null && lancePivot != null) lanceTransform = lancePivot.Find("Lance");
            if (lanceTransform == null && lancePivot != null) lanceTransform = lancePivot; // Fallback

            if (lanceTip == null && lanceTransform != null) lanceTip = lanceTransform.Find("LanceTip");
            if (exhaustVent == null && lanceTransform != null) exhaustVent = lanceTransform.Find("ExhaustVent");

            if (bodyTransform != null)
            {
                _initialBodyPos = bodyTransform.localPosition;
                if (bodyRenderer == null) bodyRenderer = bodyTransform.GetComponent<SpriteRenderer>();
            }

            if (lanceTransform != null)
            {
                _initialLanceLocalPos = lanceTransform.localPosition;
                if (lanceRenderer == null) lanceRenderer = lanceTransform.GetComponentInChildren<SpriteRenderer>();
            }

            // Set default rear leg shading
            Color rearTint = new Color(0.68f, 0.68f, 0.72f, 1f);
            if (thighRearRenderer != null) thighRearRenderer.color = rearTint;
            if (shinRearRenderer != null) shinRearRenderer.color = rearTint;
            if (footRearRenderer != null) footRearRenderer.color = rearTint;

            _currentFrontAnkle = new Vector2(NeutralAnkleX - 0.20f, GroundAnkleY);
            _currentRearAnkle = new Vector2(NeutralAnkleX + 0.20f, GroundAnkleY);
            _currentFrontFootPitch = 0f;
            _currentRearFootPitch = 0f;

            UpdateSpriteOrientation();
        }

        private void HandleHealthChanged(Health h)
        {
            if (h == null || !h.IsAlive)
            {
                ExplodeAndDie();
                return;
            }

            StartCoroutine(DamageFlashRoutine());
            if (_audio != null && ouch != null)
            {
                _audio.pitch = 1.15f;
                _audio.PlayOneShot(ouch);
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            SetRenderersColor(new Color(2f, 0.4f, 0.4f, 1f));
            yield return new WaitForSeconds(0.08f);
            SetRenderersColor(Color.white);
        }

        private void SetRenderersColor(Color c)
        {
            if (bodyRenderer != null) bodyRenderer.color = c;
            if (lanceRenderer != null) lanceRenderer.color = c;
            if (thighFrontRenderer != null) thighFrontRenderer.color = c;
            if (shinFrontRenderer != null) shinFrontRenderer.color = c;
            if (footFrontRenderer != null) footFrontRenderer.color = c;
            Color rearTint = new Color(0.68f, 0.68f, 0.72f, 1f);
            if (thighRearRenderer != null) thighRearRenderer.color = c * rearTint;
            if (shinRearRenderer != null) shinRearRenderer.color = c * rearTint;
            if (footRearRenderer != null) footRearRenderer.color = c * rearTint;
        }

        protected override void Update()
        {
            if (_health != null && !_health.IsAlive)
            {
                _moveX = 0f;
                return;
            }

            if (_player == null)
            {
                var model = Simulation.GetModel<PlatformerModel>();
                if (model != null) _player = model.player;
                if (_player == null) _player = FindAnyObjectByType<PlayerController>();
            }

            UpdateArticulatedMotion();

            if (_isAttacking || currentState == LancerState.Stunned) return;

            UpdateAI();
        }

        private void UpdateArticulatedMotion()
        {
            float dt = Time.deltaTime;

            // Smoothly damp lance piston recoil/thrust back to center
            _lanceThrustOffset = Vector3.Lerp(_lanceThrustOffset, Vector3.zero, dt * 6.0f);
            _lanceRecoilOffset = Vector3.Lerp(_lanceRecoilOffset, Vector3.zero, dt * 8.0f);

            if (lanceTransform != null)
            {
                lanceTransform.localPosition = _initialLanceLocalPos + _lanceThrustOffset + _lanceRecoilOffset;
            }

            // Gait. The cycle advances with distance covered rather than with time, so the feet
            // never slide and a robot pressed against a wall stops stepping.
            bool moving = _stepSpeed > 0.15f;
            bool dashing = currentState == LancerState.DashThrust;
            float legLerpSpeed;
            float bounce = 0f;
            float lean = 0f;

            Vector2 targetFrontAnkle;
            Vector2 targetRearAnkle;
            float targetFrontFootPitch = 0f;
            float targetRearFootPitch = 0f;

            if (currentState == LancerState.Charging)
            {
                // Bracing crouch stance: front leg lunged forward, rear leg extended back
                targetFrontAnkle = new Vector2(NeutralAnkleX - 0.28f, GroundAnkleY);
                targetRearAnkle = new Vector2(NeutralAnkleX + 0.32f, GroundAnkleY + 0.05f);
                targetFrontFootPitch = 0f;
                targetRearFootPitch = 0f;
                legLerpSpeed = 14.0f;
            }
            else if (currentState == LancerState.Stunned)
            {
                float twitch = Mathf.Sin(Time.time * 28.0f) * 0.04f;
                targetFrontAnkle = new Vector2(NeutralAnkleX - 0.08f + twitch, GroundAnkleY);
                targetRearAnkle = new Vector2(NeutralAnkleX + 0.08f - twitch, GroundAnkleY);
                targetFrontFootPitch = 0f;
                targetRearFootPitch = 0f;
                legLerpSpeed = 16.0f;
            }
            else if (moving)
            {
                legLerpSpeed = dashing ? 24.0f : 18.0f;
                float pace = Mathf.InverseLerp(patrolSpeed * 0.6f, stalkSpeed, _stepSpeed);
                float stepReach = dashing ? 0.32f : Mathf.Lerp(0.18f, 0.26f, pace);
                float currentLift = dashing ? 0.18f : Mathf.Lerp(0.08f, 0.14f, pace);

                // Alternating stride phases: Front leg at _walkCycle, Rear leg at _walkCycle + PI
                targetFrontAnkle = ComputeGaitAnklePos(_walkCycle, stepReach, currentLift, out targetFrontFootPitch);
                targetRearAnkle = ComputeGaitAnklePos(_walkCycle + Mathf.PI, stepReach, currentLift, out targetRearFootPitch);

                float rate = Mathf.Cos(_walkCycle);
                bounce = Mathf.Abs(rate) * bodyBounce * (dashing ? 1.35f : 1.0f);
                lean = (dashing ? dashLean : runLean) * Mathf.Max(pace, dashing ? 1f : 0.35f);
            }
            else
            {
                // Idle combat stance: rock-solid stance with both feet planted flat on the ground
                targetFrontAnkle = new Vector2(NeutralAnkleX - 0.20f, GroundAnkleY);
                targetRearAnkle = new Vector2(NeutralAnkleX + 0.20f, GroundAnkleY);
                targetFrontFootPitch = 0f;
                targetRearFootPitch = 0f;
                legLerpSpeed = 8.0f;
            }

            _currentFrontAnkle = Vector2.Lerp(_currentFrontAnkle, targetFrontAnkle, dt * legLerpSpeed);
            _currentRearAnkle = Vector2.Lerp(_currentRearAnkle, targetRearAnkle, dt * legLerpSpeed);
            _currentFrontFootPitch = Mathf.Lerp(_currentFrontFootPitch, targetFrontFootPitch, dt * legLerpSpeed);
            _currentRearFootPitch = Mathf.Lerp(_currentRearFootPitch, targetRearFootPitch, dt * legLerpSpeed);

            if (Solve2BoneIK(_currentFrontAnkle, out float fThigh, out float fKnee))
            {
                if (hipFront != null) hipFront.localRotation = Quaternion.Euler(0, 0, fThigh);
                if (kneeFront != null) kneeFront.localRotation = Quaternion.Euler(0, 0, fKnee);
                float fShinWorld = fThigh + fKnee;
                float fAnkleLocal = (FootFlatAngle + _currentFrontFootPitch) - fShinWorld;
                if (ankleFront != null) ankleFront.localRotation = Quaternion.Euler(0, 0, fAnkleLocal);
            }

            if (Solve2BoneIK(_currentRearAnkle, out float rThigh, out float rKnee))
            {
                if (hipRear != null) hipRear.localRotation = Quaternion.Euler(0, 0, rThigh);
                if (kneeRear != null) kneeRear.localRotation = Quaternion.Euler(0, 0, rKnee);
                float rShinWorld = rThigh + rKnee;
                float rAnkleLocal = (FootFlatAngle + _currentRearFootPitch) - rShinWorld;
                if (ankleRear != null) ankleRear.localRotation = Quaternion.Euler(0, 0, rAnkleLocal);
            }

            // Dust at each footfall
            if (moving && footDust)
            {
                int contact = Mathf.FloorToInt((_walkCycle + Mathf.PI * 0.5f) / Mathf.PI);
                if (contact != _lastStepIndex)
                {
                    _lastStepIndex = contact;
                    Transform activeAnkle = (contact & 1) == 1 ? ankleFront : ankleRear;
                    if (activeAnkle != null)
                    {
                        Vector3 footPos = activeAnkle.position + Vector3.down * 0.15f;
                        ProjectileDebris.SpawnSpark(footPos, dustColor, 3, 1.6f, 0.05f, 0.11f);
                    }
                }
            }

            // Body suspension: bounce with the stride, lean into the run.
            if (bodyTransform != null)
            {
                Vector3 bodyOffset;
                if (currentState == LancerState.Charging)
                {
                    // Crouch down while loading pneumatic spring
                    bodyOffset = new Vector3(0, -0.05f, 0);
                }
                else if (moving)
                {
                    bodyOffset = new Vector3(0, bounce, 0);
                }
                else
                {
                    // Idle breathing
                    bodyOffset = new Vector3(0, Mathf.Sin(Time.time * 2.5f) * 0.015f, 0);
                }
                bodyTransform.localPosition = Vector3.Lerp(bodyTransform.localPosition, _initialBodyPos + bodyOffset, dt * 14.0f);
                // Local Z, so the mirrored VisualRoot leans it forward whichever way it faces.
                bodyTransform.localRotation = Quaternion.Lerp(bodyTransform.localRotation, Quaternion.Euler(0, 0, lean), dt * 10.0f);
            }

            // Lance angle: stays dead-straight horizontal for direct linear jousting thrusts
            if (lancePivot != null)
            {
                if (currentState == LancerState.Charging || currentState == LancerState.DashThrust)
                {
                    lancePivot.localRotation = Quaternion.identity;
                }
                else if (PlayerTargetable())
                {
                    // Slight subtle micro-tilt (max +-5 deg) to track player height while keeping spear primarily horizontal
                    Vector3 playerTarget = _player.transform.position + Vector3.up * 0.5f;
                    Vector3 diff = playerTarget - lancePivot.position;
                    float sign = isFacingLeft ? -1f : 1f;
                    float targetDeg = Mathf.Atan2(diff.y, diff.x * sign) * Mathf.Rad2Deg;
                    targetDeg = Mathf.Clamp(targetDeg, -6f, 6f);
                    _currentLanceAngle = Mathf.Lerp(_currentLanceAngle, targetDeg, dt * 6.0f);
                    lancePivot.localRotation = Quaternion.Euler(0, 0, _currentLanceAngle);
                }
                else
                {
                    lancePivot.localRotation = Quaternion.identity;
                }
            }
        }

        public static bool Solve2BoneIK(Vector2 ankleTarget, out float thighAngle, out float kneeLocalAngle)
        {
            float d = ankleTarget.magnitude;
            d = Mathf.Clamp(d, 0.05f, L1 + L2 - 0.005f);
            float cosAlpha = Mathf.Clamp((L1 * L1 + d * d - L2 * L2) / (2f * L1 * d), -1f, 1f);
            float alpha = Mathf.Acos(cosAlpha);
            float phi = Mathf.Atan2(ankleTarget.y, ankleTarget.x);

            // Negative alpha selects the forward-pointing knee bend configuration
            float theta1 = phi - alpha;
            Vector2 knee = new Vector2(Mathf.Cos(theta1), Mathf.Sin(theta1)) * L1;
            float theta2 = Mathf.Atan2(ankleTarget.y - knee.y, ankleTarget.x - knee.x);

            float rotThigh = (theta1 - Theta1_0) * Mathf.Rad2Deg;
            float rotShin = (theta2 - Theta2_0) * Mathf.Rad2Deg;

            thighAngle = rotThigh;
            kneeLocalAngle = rotShin - rotThigh;
            return true;
        }

        private Vector2 ComputeGaitAnklePos(float phase, float reach, float lift, out float footPitch)
        {
            float p = Mathf.Repeat(phase, Mathf.PI * 2f);
            float ax, ay;
            if (p < Mathf.PI)
            {
                // Stance: Foot planted flat on ground, travelling backward relative to hip
                float u = p / Mathf.PI;
                ax = (NeutralAnkleX - reach) + u * (2f * reach);
                ay = GroundAnkleY;
                footPitch = 0f; // 100% flat
            }
            else
            {
                // Swing: Foot in air, stepping forward with knee bent
                float u = (p - Mathf.PI) / Mathf.PI;
                ax = (NeutralAnkleX + reach) - u * (2f * reach);
                ay = GroundAnkleY + Mathf.Sin(u * Mathf.PI) * lift;
                footPitch = -12f * Mathf.Sin(u * Mathf.PI);
            }
            return new Vector2(ax, ay);
        }

        private bool PlayerTargetable()
        {
            return _player != null && _player.gameObject.activeInHierarchy && _player.health != null && _player.health.IsAlive;
        }

        private void UpdateAI()
        {
            if (!PlayerTargetable())
            {
                HandlePatrol();
                return;
            }

            Vector2 myPos = transform.position;
            Vector2 playerPos = _player.transform.position;
            float diffX = playerPos.x - myPos.x;
            float absDx = Mathf.Abs(diffX);
            float absDy = Mathf.Abs(playerPos.y - myPos.y);

            if (absDx > detectionRadius || absDy > verticalDetectLimit)
            {
                HandlePatrol();
                return;
            }

            // Face the player
            if (absDx > 0.3f)
            {
                bool shouldFaceLeft = diffX < 0;
                if (shouldFaceLeft != isFacingLeft)
                {
                    isFacingLeft = shouldFaceLeft;
                    UpdateSpriteOrientation();
                }
            }

            // Check attack initiation
            if (absDx <= attackRange && absDx >= minAttackDistance && Time.time >= _nextAttackTime)
            {
                StartCoroutine(ExecuteDashThrustSequence());
                return;
            }

            // Stalk / Advance towards player
            currentState = LancerState.Stalking;
            float dir = isFacingLeft ? -1f : 1f;

            if (HasGroundAhead(dir))
            {
                _moveX = dir * stalkSpeed;
            }
            else
            {
                _moveX = 0f; // Don't fall off ledges while stalking
            }
        }

        private void HandlePatrol()
        {
            currentState = LancerState.Patrol;

            if (Time.time < _turnCooldown)
            {
                _moveX = _patrolDir * patrolSpeed;
                return;
            }

            // Stationary Sentry Mode:
            // When selfPatrolDistance <= 0.05f, stand firm in place as a sentinel guard
            if (selfPatrolDistance <= 0.05f)
            {
                _moveX = 0f;
                return;
            }

            float currentX = transform.position.x;
            if (_patrolDir > 0 && currentX >= _spawnX + selfPatrolDistance)
            {
                _patrolDir = -1;
                _turnCooldown = Time.time + 0.35f;
            }
            else if (_patrolDir < 0 && currentX <= _spawnX - selfPatrolDistance)
            {
                _patrolDir = 1;
                _turnCooldown = Time.time + 0.35f;
            }

            // Check ground ahead
            bool groundAhead = HasGroundAhead(_patrolDir);
            if (!groundAhead)
            {
                bool groundBehind = HasGroundAhead(-_patrolDir);
                if (groundBehind)
                {
                    _patrolDir = -_patrolDir;
                    _turnCooldown = Time.time + 0.35f;
                }
                else
                {
                    // Tiny platform / isolated ledge: nowhere to walk safely, hold position
                    _moveX = 0f;
                    return;
                }
            }

            bool shouldFaceLeft = _patrolDir < 0;
            if (shouldFaceLeft != isFacingLeft)
            {
                isFacingLeft = shouldFaceLeft;
                UpdateSpriteOrientation();
            }

            _moveX = _patrolDir * patrolSpeed;
        }

        private void UpdateSpriteOrientation()
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = new Vector3(isFacingLeft ? 1f : -1f, 1f, 1f);
            }
            if (_collider is BoxCollider2D box)
            {
                box.offset = new Vector2(isFacingLeft ? 0.3f : -0.3f, box.offset.y);
            }
        }

        private IEnumerator ExecuteDashThrustSequence()
        {
            _isAttacking = true;
            _moveX = 0f;
            currentState = LancerState.Charging;

            float dir = isFacingLeft ? -1f : 1f;

            // 1. Telegraph / Windup (Spring Compression)
            SetRenderersColor(ChargeTint);
            // Retract lance straight backward horizontally
            _lanceThrustOffset = new Vector3(0.42f, 0, 0);

            if (_audio != null && chargeSound != null)
            {
                _audio.pitch = 1.2f;
                _audio.PlayOneShot(chargeSound);
            }

            // Electric sparks and horizontal targeting trace at lance tip
            Vector3 tipPos = lanceTip != null ? lanceTip.position : transform.position + Vector3.left * (dir * 1.5f) + Vector3.up * 1.35f;
            ExpandingRing.Spawn(tipPos, new Color(0.2f, 0.7f, 1f, 0.85f), 1.2f, 0.2f);
            ProjectileDebris.SpawnSpark(tipPos, new Color(0.3f, 0.85f, 1f), 8, 4f);

            yield return new WaitForSeconds(chargeDuration);

            // 2. Dash Thrust (Explosive supersonic straight lunge)
            currentState = LancerState.DashThrust;
            SetRenderersColor(Color.white);

            // Extend lance piston forward violently in dead-straight horizontal line
            _lanceThrustOffset = new Vector3(-lancePistonThrustOffset, 0, 0);

            if (_audio != null && thrustSound != null)
            {
                _audio.pitch = Random.Range(0.95f, 1.1f);
                _audio.PlayOneShot(thrustSound);
            }

            // Exhaust steam blast at rear
            Vector3 ventPos = exhaustVent != null ? exhaustVent.position : transform.position + Vector3.right * (dir * 1.0f) + Vector3.up * 1.5f;
            ExpandingRing.Spawn(ventPos, new Color(0.9f, 0.9f, 0.9f, 0.7f), 1.8f, 0.25f);
            ProjectileDebris.SpawnSpark(ventPos, new Color(1f, 0.6f, 0.2f), 10, 6f);
            CameraZoomController.TriggerShake(0.25f, 0.12f);

            float elapsed = 0f;
            bool hitPlayer = false;

            while (elapsed < dashMaxDuration)
            {
                elapsed += Time.deltaTime;
                _moveX = dir * dashSpeed;

                // Check spear thrust collision with player
                if (!hitPlayer && TryLanceThrustHit(dir))
                {
                    hitPlayer = true;
                    break;
                }

                // Stop immediately if we reach the edge of the ground / cliff
                if (!HasGroundAhead(dir))
                {
                    break;
                }

                yield return null;
            }

            _moveX = 0f;

            // 3. Recovery / Cooling Stall (Weak Point Window)
            currentState = LancerState.Recovery;
            if (_audio != null && steamSound != null)
            {
                _audio.pitch = 0.9f;
                _audio.PlayOneShot(steamSound);
            }

            // Steam hiss puff from lance
            ExpandingRing.Spawn(tipPos, new Color(0.95f, 0.95f, 0.95f, 0.5f), 1.0f, 0.4f);

            yield return new WaitForSeconds(recoveryDuration);

            _nextAttackTime = Time.time + attackCooldown;
            _isAttacking = false;
            currentState = LancerState.Patrol;
        }

        private bool TryLanceThrustHit(float dir)
        {
            Bounds b = _collider.bounds;
            Vector2 checkCenter = new Vector2(dir > 0f ? b.max.x + thrustReach * 0.5f : b.min.x - thrustReach * 0.5f, b.center.y + 0.3f);
            Vector2 checkSize = new Vector2(thrustReach, 1.2f);

            int count = Physics2D.OverlapBox(checkCenter, checkSize, 0f, ContactFilter2D.noFilter, _hitColliders);
            for (int i = 0; i < count; i++)
            {
                var player = _hitColliders[i].GetComponentInParent<PlayerController>();
                if (player == null || player.health == null || !player.health.IsAlive) continue;

                // Parry check
                if (player.isParrying)
                {
                    player.OnParrySuccess(gameObject);
                    TriggerStun();
                    return true;
                }
                else if (player.isGuarding)
                {
                    Vector3 blockPoint = lanceTip != null ? lanceTip.position : player.transform.position;
                    player.OnGuardBlock(blockPoint);
                    return true;
                }

                if (player.isInvincible) return false;

                // Melee thrust impale hit
                Vector3 hitPoint = lanceTip != null ? lanceTip.position : player.transform.position;
                ProjectileDebris.SpawnSpark(hitPoint, new Color(1f, 0.3f, 0.1f), 14, 8f);
                ExpandingRing.Spawn(hitPoint, new Color(1f, 0.5f, 0.2f, 0.9f), 1.6f, 0.18f);

                player.TakeDamage(thrustDamage, new Vector2(dir * 7.5f, 5.0f));
                return true;
            }

            return false;
        }

        public void TriggerStun()
        {
            if (_health != null && !_health.IsAlive) return;

            StopAllCoroutines();
            StartCoroutine(StunRoutine());
        }

        private IEnumerator StunRoutine()
        {
            _isAttacking = false;
            currentState = LancerState.Stunned;
            _moveX = 0f;

            Vector3 sensorPos = lancePivot != null ? lancePivot.position : transform.position + Vector3.up * 1.2f;
            ProjectileDebris.SpawnSpark(sensorPos, new Color(0.2f, 0.9f, 1f), 18, 7f);
            ExpandingRing.Spawn(sensorPos, new Color(0.3f, 0.85f, 1f, 0.85f), 1.8f, 0.25f);

            SetRenderersColor(StunTint);

            yield return new WaitForSeconds(stunDuration);

            SetRenderersColor(Color.white);
            _nextAttackTime = Time.time + 1.0f;
            currentState = LancerState.Patrol;
        }

        public override void OnStomped()
        {
            TriggerStun();
        }

        public override bool CanTakeDamage(Vector2 hitPosition, Vector2 attackerPosition)
        {
            return true;
        }

        private void ExplodeAndDie()
        {
            StopAllCoroutines();
            _isAttacking = false;
            _moveX = 0f;

            Vector3 center = transform.position + Vector3.up * 1.0f;
            ExpandingRing.Spawn(center, new Color(1f, 0.5f, 0.1f), 2.8f, 0.4f);
            ProjectileDebris.SpawnSpark(center, new Color(1f, 0.8f, 0.2f), 20, 9f);

            StartCoroutine(DeathFadeRoutine());
        }

        private IEnumerator DeathFadeRoutine()
        {
            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, elapsed / 0.5f);
                SetRenderersColor(new Color(1f, 1f, 1f, a));
                yield return null;
            }

            Destroy(gameObject);
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
                else
                {
                    _moveX = 0f;
                }
            }

            if (delta != Vector2.zero)
            {
                _rb.MovePosition(_rb.position + delta);
            }

            // Ground speed and stride cadence both come from the distance actually covered.
            float instantSpeed = Mathf.Abs(delta.x) / Mathf.Max(0.0001f, dt);
            _stepSpeed = Mathf.Lerp(_stepSpeed, instantSpeed, 0.4f);
            _walkCycle += Mathf.Min(Mathf.Abs(delta.x) * stridePerUnit, maxCadence * dt);
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

        private float SweepDistance(Vector2 dir, float distance)
        {
            float allowed = distance;
            float feetY = _collider.bounds.min.y;
            int count = _rb.Cast(dir, _terrainFilter, _castHits, distance + Skin);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _castHits[i];
                if (Vector2.Dot(hit.normal, dir) > -0.5f) continue;
                if (dir.y == 0f && hit.point.y < feetY + 0.05f) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - Skin));
            }
            return allowed;
        }

        private bool HasGroundAhead(float dirX)
        {
            if (_collider == null) return true;
            Bounds b = _collider.bounds;
            Vector2 origin = new Vector2(dirX > 0f ? b.max.x + 0.1f : b.min.x - 0.1f, b.min.y + 0.1f);
            return Physics2D.Raycast(origin, Vector2.down, _terrainFilter, _castHits, ledgeCheckDepth + 0.1f) > 0;
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 pos = transform.position;
            float spawn = Application.isPlaying ? _spawnX : pos.x;

            // Detection and attack radii
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.4f);
            Gizmos.DrawWireSphere(pos + Vector3.up * 1.1f, detectionRadius);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(pos + Vector3.up * 1.1f, attackRange);

            // Patrol corridor visualization
            if (selfPatrolDistance > 0.05f)
            {
                Gizmos.color = Color.cyan;
                Vector3 leftBound = new Vector3(spawn - selfPatrolDistance, pos.y + 0.1f, pos.z);
                Vector3 rightBound = new Vector3(spawn + selfPatrolDistance, pos.y + 0.1f, pos.z);
                Gizmos.DrawLine(leftBound, rightBound);
                Gizmos.DrawWireCube(leftBound, new Vector3(0.2f, 0.5f, 0.1f));
                Gizmos.DrawWireCube(rightBound, new Vector3(0.2f, 0.5f, 0.1f));
            }
            else
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(pos + Vector3.up * 0.1f, new Vector3(0.6f, 0.2f, 0.2f));
            }

            // Ledge check ray
            if (_collider == null) _collider = GetComponent<Collider2D>();
            if (_collider != null)
            {
                float dir = isFacingLeft ? -1f : 1f;
                Bounds b = _collider.bounds;
                Vector2 origin = new Vector2(dir > 0f ? b.max.x + 0.1f : b.min.x - 0.1f, b.min.y + 0.1f);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(origin, origin + Vector2.down * (ledgeCheckDepth + 0.1f));
            }
        }
    }
}
