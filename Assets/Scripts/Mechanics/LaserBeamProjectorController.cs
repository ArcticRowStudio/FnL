using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Platformer.Mechanics
{
    public enum ProjectorDeploymentMode
    {
        BreakActionTilt,    // Option A: Cannon head articulates at collar joint to charge & fire
        SideSwivel2D5,      // X-frame swivels to side in 2.5D perspective
        PetalBlossom4Part   // 4-petal articulated flower blossom deployment
    }

    /// <summary>
    /// Controls the Laser Beam Projector mechanical opening,
    /// core energy charge, high-power green laser beam emission, and fold-shut sequence.
    /// Supports Break-Action Tilt (Option A), 2.5D Side-Swivel, and 4-Petal Blossom modes.
    /// </summary>
    public class LaserBeamProjectorController : MonoBehaviour
    {
        [Header("Deployment Mode")]
        [Tooltip("Choose deployment mode: BreakActionTilt (Option A), SideSwivel2D5, or PetalBlossom4Part")]
        public ProjectorDeploymentMode deploymentMode = ProjectorDeploymentMode.BreakActionTilt;

        [Header("Break-Action Tilt Settings (Option A)")]
        [Tooltip("Transform of the articulated cannon head (barrel + X-frame + nozzle)")]
        public Transform cannonHead;
        [Tooltip("Tilt angle during energy charge (degrees, positive = tilts upward)")]
        public float chargeTiltAngle = 14f;
        [Tooltip("If true, fires along the tilted angle. If false, snaps level to 0 deg before firing horizontal beam.")]
        public bool fireWhileTilted = false;
        [Tooltip("Recoil vibration intensity during continuous laser emission")]
        public float recoilVibration = 0.35f;

        [Header("2.5D Side-Swivel Settings (Single X-Frame)")]
        [Tooltip("Transform of the single monolithic X-frame")]
        public Transform xFrame;
        [Tooltip("Target Y rotation angle (degrees) to swivel open sideways into depth")]
        public float openAngleY = 80f;
        [Tooltip("Target Z rotation angle (degrees) for optional slant during swivel")]
        public float openAngleZ = 0f;
        [Tooltip("Brightness factor when turned into depth (simulates 3D lighting falloff)")]
        [Range(0.4f, 1f)]
        public float sideShadingFactor = 0.75f;

        [Header("4-Part Blossom Petal Transforms (For Blossom Mode)")]
        public Transform topRearPetal;
        public Transform topFrontPetal;
        public Transform bottomRearPetal;
        public Transform bottomFrontPetal;
        public float openAngleTopRear = 20f;
        public float openAngleTopFront = 30f;
        public float openAngleBottomRear = -20f;
        public float openAngleBottomFront = -30f;

        // Legacy wing flap references for backwards compatibility
        [HideInInspector] public Transform topWing;
        [HideInInspector] public Transform bottomWing;

        [Header("Sequence Timings (Seconds)")]
        [Tooltip("Time taken for the frame/petals to smoothly swing open")]
        public float openDuration = 0.6f;
        [Tooltip("Time the green core charges before firing")]
        public float chargeDuration = 0.5f;
        [Tooltip("Duration of continuous laser beam emission")]
        public float fireDuration = 1.4f;
        [Tooltip("Time between laser cut-off and starting to fold shut")]
        public float cooldownDuration = 0.4f;
        [Tooltip("Time taken to smoothly return/fold shut")]
        public float closeDuration = 0.6f;
        [Tooltip("Idle delay before starting the next firing cycle (if autoLoop is true)")]
        public float loopInterval = 4.0f;

        [Header("Loop Control")]
        [Tooltip("If true, the projector continuously loops the firing cycle")]
        public bool autoLoop = true;
        [Tooltip("Initial delay before first firing")]
        public float initialDelay = 1.0f;

        [Header("Laser Beam References")]
        [Tooltip("Origin point of the laser at the green nozzle lens")]
        public Transform muzzlePoint;
        [Tooltip("Core LineRenderer for the intense inner beam")]
        public LineRenderer coreBeamRenderer;
        [Tooltip("Outer LineRenderer for the vibrant green energy aura")]
        public LineRenderer auraBeamRenderer;
        [Tooltip("Light2D attached to the nozzle core")]
        public Light2D muzzleLight;
        [Tooltip("Light2D illuminating the laser hit point")]
        public Light2D hitLight;
        [Tooltip("Particle system or visual effect at laser impact point")]
        public ParticleSystem impactSparks;

        [Header("Laser Beam Properties")]
        public float maxBeamDistance = 60f;
        public LayerMask obstacleLayerMask = ~0; // default all
        public float coreBeamWidth = 0.35f;
        public float auraBeamWidth = 0.95f;
        public Color coreColor = new Color(0.9f, 1f, 0.88f, 1f);
        public Color auraColor = new Color(0.15f, 1f, 0.35f, 0.65f);
        public Color lightColor = new Color(0.2f, 1f, 0.4f, 1f);
        public float maxLightIntensity = 3.5f;

        [Header("Damage Settings")]
        public bool dealDamage = true;
        public float damageInterval = 0.25f;

        [Header("Lock-On & Targeting Reticle")]
        [Tooltip("Enable pre-fire targeting laser line and lock-on reticle")]
        public bool enableLockOnIndicator = true;
        [Tooltip("Reticle Sprite to display (default: Reticle_White_Combined)")]
        public Sprite reticleSprite;
        [Tooltip("Transform of the Lock-On Reticle object")]
        public Transform reticleTransform;
        [Tooltip("SpriteRenderer for the reticle")]
        public SpriteRenderer reticleRenderer;
        [Tooltip("Thin guide laser LineRenderer showing trajectory during lock-on")]
        public LineRenderer guideBeamRenderer;
        [Tooltip("Default lock-on aiming color (e.g. Neon Green)")]
        public Color reticleAimColor = new Color(0.2f, 1f, 0.45f, 0.75f);
        [Tooltip("Locked / Player targeted alert color (e.g. Warning Red)")]
        public Color reticleLockedColor = new Color(1f, 0.25f, 0.15f, 0.95f);
        [Tooltip("Initial scale of reticle before clamping down")]
        public float initialReticleScale = 1.8f;
        [Tooltip("Final scale of reticle when locked on target")]
        public float lockedReticleScale = 0.85f;
        [Tooltip("Rotation degrees the reticle turns during lock-on")]
        public float reticleSpinAngle = 90f;
        [Tooltip("Guide laser line width")]
        public float guideBeamWidth = 0.04f;
        [Tooltip("If true, reticle switches to alert color when player is in line of fire")]
        public bool alertOnPlayerTarget = true;

        [Header("Reload Mechanism (Pneumatic & Cartridge)")]
        [Tooltip("Optional reload mechanism component for cartridge loading and pneumatic steam purge")]
        public LaserProjectorReloadMechanism reloadMechanism;

        [Header("Status Indicator Button")]
        [Tooltip("Animated green status / trigger button on the projector body housing")]
        public LaserProjectorButtonController greenButtonController;

        // Runtime state
        private bool isExecutingSequence = false;
        private Coroutine activeSequenceCoroutine;
        private float lastDamageTime = 0f;

        private void Start()
        {
            InitializeComponents();

            // Set to closed state initially
            SetClosedState();
            SetLaserActive(false);

            if (muzzleLight != null)
            {
                muzzleLight.color = lightColor;
                muzzleLight.intensity = 0.2f;
            }

            if (autoLoop)
            {
                StartCoroutine(AutoLoopRoutine());
            }
        }

        public void InitializeComponents()
        {
            // Auto-find Cannon_Head
            if (cannonHead == null)
            {
                cannonHead = transform.Find("Cannon_Head");
            }

            // Auto-find X_Frame
            if (xFrame == null)
            {
                xFrame = transform.Find("X_Frame");
            }

            // Auto-find 4 petal transforms if not manually assigned
            if (topRearPetal == null) topRearPetal = transform.Find("Petal_Top_Rear");
            if (topFrontPetal == null)
            {
                if (topRearPetal != null) topFrontPetal = topRearPetal.Find("Petal_Top_Front");
                if (topFrontPetal == null) topFrontPetal = transform.Find("Petal_Top_Front");
            }

            if (bottomRearPetal == null) bottomRearPetal = transform.Find("Petal_Bot_Rear");
            if (bottomFrontPetal == null)
            {
                if (bottomRearPetal != null) bottomFrontPetal = bottomRearPetal.Find("Petal_Bot_Front");
                if (bottomFrontPetal == null) bottomFrontPetal = transform.Find("Petal_Bot_Front");
            }

            if (muzzlePoint == null)
            {
                // Prefer child of cannonHead if present
                if (cannonHead != null)
                {
                    Transform mpInHead = cannonHead.Find("MuzzlePoint");
                    if (mpInHead != null) muzzlePoint = mpInHead;
                }
                if (muzzlePoint == null)
                {
                    Transform m = transform.Find("MuzzlePoint");
                    if (m != null) muzzlePoint = m;
                    else muzzlePoint = transform;
                }
            }

            if (reloadMechanism == null)
            {
                reloadMechanism = GetComponentInChildren<LaserProjectorReloadMechanism>();
            }

            if (greenButtonController == null)
            {
                greenButtonController = GetComponentInChildren<LaserProjectorButtonController>();
            }

            UpdateModeObjectsVisibility();
            SetupLineRenderers();
        }

        public void UpdateModeObjectsVisibility()
        {
            bool isBreakAction = (deploymentMode == ProjectorDeploymentMode.BreakActionTilt);
            bool isSideSwivel = (deploymentMode == ProjectorDeploymentMode.SideSwivel2D5);
            bool isBlossom = (deploymentMode == ProjectorDeploymentMode.PetalBlossom4Part);

            if (cannonHead != null)
            {
                cannonHead.gameObject.SetActive(isBreakAction);
            }

            if (xFrame != null)
            {
                xFrame.gameObject.SetActive(isSideSwivel);
            }

            if (topRearPetal != null)
            {
                topRearPetal.gameObject.SetActive(isBlossom);
            }

            if (bottomRearPetal != null)
            {
                bottomRearPetal.gameObject.SetActive(isBlossom);
            }
        }

        private void SetupLineRenderers()
        {
            if (coreBeamRenderer == null)
            {
                GameObject coreGo = new GameObject("CoreBeamRenderer");
                coreGo.transform.SetParent(muzzlePoint, false);
                coreBeamRenderer = coreGo.AddComponent<LineRenderer>();
                ConfigureRenderer(coreBeamRenderer, coreBeamWidth, coreColor, 2);
            }

            if (auraBeamRenderer == null)
            {
                GameObject auraGo = new GameObject("AuraBeamRenderer");
                auraGo.transform.SetParent(muzzlePoint, false);
                auraBeamRenderer = auraGo.AddComponent<LineRenderer>();
                ConfigureRenderer(auraBeamRenderer, auraBeamWidth, auraColor, 1);
            }

            if (guideBeamRenderer == null && muzzlePoint != null)
            {
                Transform guideT = muzzlePoint.Find("GuideBeamRenderer");
                if (guideT != null) guideBeamRenderer = guideT.GetComponent<LineRenderer>();
                if (guideBeamRenderer == null)
                {
                    GameObject guideGo = new GameObject("GuideBeamRenderer");
                    guideGo.transform.SetParent(muzzlePoint, false);
                    guideBeamRenderer = guideGo.AddComponent<LineRenderer>();
                }
                ConfigureRenderer(guideBeamRenderer, guideBeamWidth, reticleAimColor, 1);
            }

            if (reticleTransform == null)
            {
                Transform rT = transform.Find("Target_Reticle");
                if (rT != null)
                {
                    reticleTransform = rT;
                }
                else
                {
                    GameObject retGo = new GameObject("Target_Reticle");
                    retGo.transform.SetParent(transform, true);
                    reticleTransform = retGo.transform;
                }
            }

            if (reticleRenderer == null && reticleTransform != null)
            {
                reticleRenderer = reticleTransform.GetComponent<SpriteRenderer>();
                if (reticleRenderer == null)
                {
                    reticleRenderer = reticleTransform.gameObject.AddComponent<SpriteRenderer>();
                }
                SpriteRenderer parentSr = GetComponent<SpriteRenderer>();
                if (parentSr != null)
                {
                    reticleRenderer.sortingLayerID = parentSr.sortingLayerID;
                    reticleRenderer.sortingOrder = parentSr.sortingOrder + 12;
                }
            }

            if (reticleRenderer != null && reticleRenderer.sprite == null && reticleSprite != null)
            {
                reticleRenderer.sprite = reticleSprite;
            }
        }

        private void ConfigureRenderer(LineRenderer lr, float width, Color color, int sortingOrderOffset)
        {
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width * 1.05f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
            if (mat == null || mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                mat = new Material(Shader.Find("Sprites/Default"));
            }
            lr.material = mat;

            SpriteRenderer parentSr = GetComponent<SpriteRenderer>();
            if (parentSr != null)
            {
                lr.sortingLayerID = parentSr.sortingLayerID;
                lr.sortingOrder = parentSr.sortingOrder + sortingOrderOffset + 3;
            }

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) }
            );
            lr.colorGradient = gradient;
            lr.enabled = false;
        }

        private IEnumerator AutoLoopRoutine()
        {
            yield return new WaitForSeconds(initialDelay);

            while (autoLoop)
            {
                yield return StartCoroutine(FireSequenceRoutine());
                yield return new WaitForSeconds(loopInterval);
            }
        }

        private float GetEffectiveTiltAngle(float baseAngle)
        {
            return (transform.lossyScale.x < 0) ? -baseAngle : baseAngle;
        }

        /// <summary>
        /// Executes the complete firing sequence depending on deployment mode:
        /// Tilt / Open -> Charge Core -> Fire Laser Beam -> Cooldown -> Return / Close
        /// </summary>
        public IEnumerator FireSequenceRoutine()
        {
            if (isExecutingSequence) yield break;
            isExecutingSequence = true;

            UpdateModeObjectsVisibility();

            // Trigger Green Button tactile press
            if (greenButtonController != null)
            {
                greenButtonController.PressButton();
            }

            // 0. Pneumatic Cartridge / Heatsink Reload Phase
            if (reloadMechanism != null)
            {
                if (greenButtonController != null)
                {
                    greenButtonController.StartChargingStrobe();
                }

                if (Cinematics.LaserCinematicDirector.Instance != null)
                {
                    Cinematics.LaserCinematicDirector.Instance.FocusProjectorReload(this);
                }

                yield return StartCoroutine(reloadMechanism.PlayReloadSequenceRoutine());

                if (Cinematics.LaserCinematicDirector.Instance != null)
                {
                    Cinematics.LaserCinematicDirector.Instance.FocusProjectorFiring(this);
                }
            }
            else
            {
                if (Cinematics.LaserCinematicDirector.Instance != null)
                {
                    Cinematics.LaserCinematicDirector.Instance.FocusProjector(this);
                }
            }

            // 1. Articulation / Opening Phase
            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / openDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                if (deploymentMode == ProjectorDeploymentMode.BreakActionTilt)
                {
                    if (cannonHead != null)
                    {
                        float targetAngle = GetEffectiveTiltAngle(chargeTiltAngle);
                        float currentZ = Mathf.Lerp(0f, targetAngle, ease);
                        cannonHead.localRotation = Quaternion.Euler(0f, 0f, currentZ);
                    }
                }
                else if (deploymentMode == ProjectorDeploymentMode.SideSwivel2D5)
                {
                    SetSideSwivelProgress(ease);
                }
                else
                {
                    float curTR = Mathf.Lerp(0f, openAngleTopRear, ease);
                    float curTF = Mathf.Lerp(0f, openAngleTopFront, ease);
                    float curBR = Mathf.Lerp(0f, openAngleBottomRear, ease);
                    float curBF = Mathf.Lerp(0f, openAngleBottomFront, ease);
                    SetBlossomAngles(curTR, curTF, curBR, curBF);
                }

                // Guide laser & reticle acquire target
                UpdateLockOnIndicator(ease * 0.7f, isLocked: false, alphaMultiplier: ease);

                yield return null;
            }

            // If BreakAction and !fireWhileTilted: snap level to 0 deg before firing!
            if (deploymentMode == ProjectorDeploymentMode.BreakActionTilt && !fireWhileTilted && cannonHead != null)
            {
                float snapElapsed = 0f;
                float snapDuration = 0.1f;
                float startAngle = GetEffectiveTiltAngle(chargeTiltAngle);
                while (snapElapsed < snapDuration)
                {
                    snapElapsed += Time.deltaTime;
                    float st = Mathf.Clamp01(snapElapsed / snapDuration);
                    float currentZ = Mathf.Lerp(startAngle, 0f, st);
                    cannonHead.localRotation = Quaternion.Euler(0f, 0f, currentZ);
                    UpdateLockOnIndicator(0.85f, isLocked: false, alphaMultiplier: 1f);
                    yield return null;
                }
                cannonHead.localRotation = Quaternion.identity;
            }

            // 2. Charge Core Glow & Final Lock-On Clamping
            elapsed = 0f;
            while (elapsed < chargeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / chargeDuration);

                if (muzzleLight != null)
                {
                    float pulse = 0.5f + Mathf.PingPong(elapsed * 12f, 0.5f);
                    muzzleLight.intensity = Mathf.Lerp(0.2f, maxLightIntensity * 0.7f, t) * pulse;
                }

                // Clamps down to lockedReticleScale, and locks on (turns alert color)!
                UpdateLockOnIndicator(Mathf.Lerp(0.85f, 1.0f, t), isLocked: (t > 0.35f), alphaMultiplier: 1f);

                yield return null;
            }

            // 3. Fire Laser Beam
            if (guideBeamRenderer != null) guideBeamRenderer.enabled = false;

            SetLaserActive(true);
            if (greenButtonController != null)
            {
                greenButtonController.SetFiringOvercharge(true);
            }
            CameraZoomController.TriggerShake(0.28f, fireDuration);
            if (muzzleLight != null)
            {
                muzzleLight.intensity = maxLightIntensity;
            }

            float baseAngle = (deploymentMode == ProjectorDeploymentMode.BreakActionTilt && fireWhileTilted) ? GetEffectiveTiltAngle(chargeTiltAngle) : 0f;

            elapsed = 0f;
            while (elapsed < fireDuration)
            {
                elapsed += Time.deltaTime;

                // Heavy recoil vibration during firing
                if (deploymentMode == ProjectorDeploymentMode.BreakActionTilt && cannonHead != null)
                {
                    float jitter = Mathf.Sin(Time.time * 50f) * recoilVibration;
                    cannonHead.localRotation = Quaternion.Euler(0f, 0f, baseAngle + jitter);
                }

                UpdateLaserBeam();

                // Keep reticle active at hit point during blast
                if (enableLockOnIndicator && reticleTransform != null)
                {
                    Vector3 startPos = muzzlePoint != null ? muzzlePoint.position : transform.position;
                    Vector2 fireDir = GetMuzzleFireDirection();
                    Vector3 hitPos = CastProjectorRay(startPos, fireDir, out RaycastHit2D hit) ? (Vector3)hit.point : (startPos + (Vector3)(fireDir * maxBeamDistance));
                    SetReticleTransformWorld(hitPos, lockedReticleScale * (1f + 0.08f * Mathf.Sin(Time.time * 30f)), 0f);
                    if (reticleRenderer != null) reticleRenderer.color = reticleLockedColor;
                }

                float widthJitter = 1f + 0.08f * Mathf.Sin(Time.time * 40f);
                if (coreBeamRenderer != null) coreBeamRenderer.startWidth = coreBeamWidth * widthJitter;
                if (auraBeamRenderer != null) auraBeamRenderer.startWidth = auraBeamWidth * widthJitter;

                yield return null;
            }

            if (deploymentMode == ProjectorDeploymentMode.BreakActionTilt && cannonHead != null)
            {
                cannonHead.localRotation = Quaternion.Euler(0f, 0f, baseAngle);
            }

            // 4. Turn Off Laser & Cooldown
            SetLaserActive(false);
            if (greenButtonController != null)
            {
                greenButtonController.SetFiringOvercharge(false);
            }

            if (reloadMechanism != null)
            {
                StartCoroutine(reloadMechanism.PlayCooldownRoutine());
            }

            elapsed = 0f;
            while (elapsed < cooldownDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / cooldownDuration);
                if (muzzleLight != null)
                {
                    muzzleLight.intensity = Mathf.Lerp(maxLightIntensity * 0.5f, 0.2f, t);
                }

                // Reticle fade out
                if (reticleRenderer != null)
                {
                    Color c = reticleLockedColor;
                    c.a = Mathf.Lerp(reticleLockedColor.a, 0f, t);
                    reticleRenderer.color = c;
                }

                yield return null;
            }

            SetLockOnActive(false);

            if (muzzleLight != null)
            {
                muzzleLight.intensity = 0.2f;
            }

            if (greenButtonController != null)
            {
                greenButtonController.ReturnToStandby();
            }

            // 5. Close / Return Phase
            if (deploymentMode == ProjectorDeploymentMode.BreakActionTilt)
            {
                if (fireWhileTilted && cannonHead != null)
                {
                    elapsed = 0f;
                    float startAngle = GetEffectiveTiltAngle(chargeTiltAngle);
                    while (elapsed < closeDuration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / closeDuration);
                        float ease = Mathf.SmoothStep(0f, 1f, t);
                        float currentZ = Mathf.Lerp(startAngle, 0f, ease);
                        cannonHead.localRotation = Quaternion.Euler(0f, 0f, currentZ);
                        yield return null;
                    }
                    cannonHead.localRotation = Quaternion.identity;
                }
            }
            else if (deploymentMode == ProjectorDeploymentMode.SideSwivel2D5)
            {
                elapsed = 0f;
                while (elapsed < closeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / closeDuration);
                    float ease = Mathf.SmoothStep(0f, 1f, t);
                    SetSideSwivelProgress(1f - ease);
                    yield return null;
                }
                SetSideSwivelProgress(0f);
            }
            else
            {
                elapsed = 0f;
                while (elapsed < closeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / closeDuration);
                    float ease = Mathf.SmoothStep(0f, 1f, t);
                    float curTR = Mathf.Lerp(openAngleTopRear, 0f, ease);
                    float curTF = Mathf.Lerp(openAngleTopFront, 0f, ease);
                    float curBR = Mathf.Lerp(openAngleBottomRear, 0f, ease);
                    float curBF = Mathf.Lerp(openAngleBottomFront, 0f, ease);
                    SetBlossomAngles(curTR, curTF, curBR, curBF);
                    yield return null;
                }
                SetBlossomAngles(0f, 0f, 0f, 0f);
            }

            SetClosedState();
            isExecutingSequence = false;
        }

        private Vector2 GetMuzzleFireDirection()
        {
            if (muzzlePoint != null)
            {
                return (Vector2)(muzzlePoint.TransformPoint(Vector3.right) - muzzlePoint.position).normalized;
            }
            Vector3 td = transform.TransformPoint(Vector3.right) - transform.position;
            return (Vector2)td.normalized;
        }

        private bool CastProjectorRay(Vector3 startPos, Vector2 fireDirection, out RaycastHit2D validHit)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(startPos, fireDirection, maxBeamDistance, obstacleLayerMask);
            foreach (var h in hits)
            {
                if (h.collider != null)
                {
                    // Ignore self colliders
                    if (h.collider.transform.IsChildOf(transform)) continue;

                    // If trigger, only hit player or bulkhead lock
                    if (h.collider.isTrigger)
                    {
                        if (h.collider.GetComponent<PlayerController>() != null || h.collider.GetComponent<BulkheadLock>() != null)
                        {
                            validHit = h;
                            return true;
                        }
                        continue;
                    }

                    validHit = h;
                    return true;
                }
            }
            validHit = default;
            return false;
        }

        private void UpdateLaserBeam()
        {
            Vector3 startPos = muzzlePoint != null ? muzzlePoint.position : transform.position;
            Vector2 fireDirection = GetMuzzleFireDirection();

            Vector3 endPos;
            if (CastProjectorRay(startPos, fireDirection, out RaycastHit2D hit))
            {
                endPos = hit.point;
                if (hitLight != null)
                {
                    hitLight.transform.position = endPos;
                    hitLight.enabled = true;
                }
                if (impactSparks != null)
                {
                    impactSparks.transform.position = endPos;
                    if (!impactSparks.isPlaying) impactSparks.Play();
                }

                if (dealDamage && Time.time - lastDamageTime >= damageInterval)
                {
                    var player = hit.collider.GetComponent<PlayerController>();
                    if (player != null)
                    {
                        var health = hit.collider.GetComponent<Health>();
                        if (health != null)
                        {
                            health.Decrement();
                            lastDamageTime = Time.time;
                        }
                    }

                    var bLock = hit.collider.GetComponent<BulkheadLock>();
                    if (bLock == null) bLock = hit.collider.GetComponentInParent<BulkheadLock>();
                    if (bLock != null)
                    {
                        bLock.TakeLaserDamage(30f);
                        lastDamageTime = Time.time;
                    }
                }
            }
            else
            {
                endPos = startPos + (Vector3)(fireDirection * maxBeamDistance);
                if (hitLight != null) hitLight.enabled = false;
                if (impactSparks != null && impactSparks.isPlaying) impactSparks.Stop();
            }

            if (coreBeamRenderer != null)
            {
                coreBeamRenderer.SetPosition(0, startPos);
                coreBeamRenderer.SetPosition(1, endPos);
            }

            if (auraBeamRenderer != null)
            {
                auraBeamRenderer.SetPosition(0, startPos);
                auraBeamRenderer.SetPosition(1, endPos);
            }
        }

        private void SetLaserActive(bool active)
        {
            if (coreBeamRenderer != null) coreBeamRenderer.enabled = active;
            if (auraBeamRenderer != null) auraBeamRenderer.enabled = active;
            if (hitLight != null) hitLight.enabled = active;
            if (impactSparks != null)
            {
                if (active) impactSparks.Play();
                else impactSparks.Stop();
            }
        }

        private void SetReticleTransformWorld(Vector3 worldPos, float worldScale, float rotZ)
        {
            if (reticleTransform == null) return;
            reticleTransform.position = worldPos;
            Vector3 lossy = transform.lossyScale;
            float signX = Mathf.Sign(lossy.x);
            float signY = Mathf.Sign(lossy.y);
            float sx = (Mathf.Abs(lossy.x) > 0.0001f) ? (worldScale / Mathf.Abs(lossy.x)) * signX : worldScale;
            float sy = (Mathf.Abs(lossy.y) > 0.0001f) ? (worldScale / Mathf.Abs(lossy.y)) * signY : worldScale;
            reticleTransform.localScale = new Vector3(sx, sy, 1f);
            reticleTransform.rotation = Quaternion.Euler(0f, 0f, rotZ);
        }

        public void UpdateLockOnIndicator(float progress, bool isLocked, float alphaMultiplier = 1f)
        {
            if (!enableLockOnIndicator)
            {
                SetLockOnActive(false);
                return;
            }

            SetLockOnActive(true);

            Vector3 startPos = muzzlePoint != null ? muzzlePoint.position : transform.position;
            Vector2 fireDirection = GetMuzzleFireDirection();

            Vector3 targetPos;
            bool hitPlayer = false;
            if (CastProjectorRay(startPos, fireDirection, out RaycastHit2D hit))
            {
                targetPos = hit.point;
                hitPlayer = (hit.collider != null && hit.collider.GetComponent<PlayerController>() != null);
            }
            else
            {
                targetPos = startPos + (Vector3)(fireDirection * maxBeamDistance);
            }

            bool showAlert = isLocked || (alertOnPlayerTarget && hitPlayer);

            Color currentColor = showAlert ? reticleLockedColor : reticleAimColor;
            currentColor.a *= alphaMultiplier;

            // Guide beam line
            if (guideBeamRenderer != null)
            {
                guideBeamRenderer.enabled = true;
                guideBeamRenderer.SetPosition(0, startPos);
                guideBeamRenderer.SetPosition(1, targetPos);

                Color guideCol = currentColor;
                guideCol.a *= 0.65f;
                Gradient g = new Gradient();
                g.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(guideCol, 0f), new GradientColorKey(guideCol, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(guideCol.a * 0.3f, 0f), new GradientAlphaKey(guideCol.a, 1f) }
                );
                guideBeamRenderer.colorGradient = g;
                float widthFactor = showAlert ? 1.3f : 1f;
                guideBeamRenderer.startWidth = guideBeamWidth * widthFactor;
                guideBeamRenderer.endWidth = guideBeamWidth * widthFactor;
            }

            // Reticle
            if (reticleTransform != null)
            {
                float targetScale = Mathf.Lerp(initialReticleScale, lockedReticleScale, progress);
                if (isLocked)
                {
                    targetScale *= (1f + 0.12f * Mathf.Sin(Time.time * 25f));
                }
                float rotZ = (1f - progress) * reticleSpinAngle;

                SetReticleTransformWorld(targetPos, targetScale, rotZ);

                if (reticleRenderer != null)
                {
                    reticleRenderer.color = currentColor;
                }
            }
        }

        public void SetLockOnActive(bool active)
        {
            if (guideBeamRenderer != null) guideBeamRenderer.enabled = active;
            if (reticleTransform != null) reticleTransform.gameObject.SetActive(active);
        }

        public void SetSideSwivelProgress(float progress)
        {
            if (xFrame != null)
            {
                float currentY = Mathf.Lerp(0f, openAngleY, progress);
                float currentZ = Mathf.Lerp(0f, openAngleZ, progress);
                xFrame.localRotation = Quaternion.Euler(0f, currentY, currentZ);

                SpriteRenderer sr = xFrame.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    float brightness = Mathf.Lerp(1f, sideShadingFactor, progress);
                    sr.color = new Color(brightness, brightness, brightness, 1f);
                }
            }
        }

        public void SetBlossomAngles(float topRear, float topFront, float botRear, float botFront)
        {
            if (topRearPetal != null)
            {
                topRearPetal.localRotation = Quaternion.Euler(0f, 0f, topRear);
            }

            if (topFrontPetal != null)
            {
                topFrontPetal.localRotation = Quaternion.Euler(0f, 0f, topFront);
            }

            if (bottomRearPetal != null)
            {
                bottomRearPetal.localRotation = Quaternion.Euler(0f, 0f, botRear);
            }

            if (bottomFrontPetal != null)
            {
                bottomFrontPetal.localRotation = Quaternion.Euler(0f, 0f, botFront);
            }
        }

        public void SetOpenedState()
        {
            if (deploymentMode == ProjectorDeploymentMode.BreakActionTilt)
            {
                if (cannonHead != null) cannonHead.localRotation = Quaternion.Euler(0f, 0f, GetEffectiveTiltAngle(chargeTiltAngle));
            }
            else if (deploymentMode == ProjectorDeploymentMode.SideSwivel2D5)
            {
                SetSideSwivelProgress(1f);
            }
            else
            {
                SetBlossomAngles(openAngleTopRear, openAngleTopFront, openAngleBottomRear, openAngleBottomFront);
            }
        }

        public void SetClosedState()
        {
            if (deploymentMode == ProjectorDeploymentMode.BreakActionTilt)
            {
                if (cannonHead != null) cannonHead.localRotation = Quaternion.identity;
            }
            else if (deploymentMode == ProjectorDeploymentMode.SideSwivel2D5)
            {
                SetSideSwivelProgress(0f);
            }
            else
            {
                SetBlossomAngles(0f, 0f, 0f, 0f);
            }

            SetLockOnActive(false);

            if (reloadMechanism != null)
            {
                reloadMechanism.SetClosedIdleState();
            }

            if (greenButtonController != null)
            {
                greenButtonController.ReturnToStandby();
            }
        }

        [ContextMenu("Test Fire Sequence")]
        public void TestFireSequence()
        {
            if (Application.isPlaying)
            {
                StopAllCoroutines();
                isExecutingSequence = false;
                StartCoroutine(FireSequenceRoutine());
            }
            else
            {
                Debug.Log("[LaserBeamProjector] Test sequence can be run in Play mode or through scene manipulation.");
            }
        }

        [ContextMenu("Open / Tilt Projector (Editor Preview)")]
        public void OpenProjectorPreview()
        {
            InitializeComponents();
            SetOpenedState();
        }

        [ContextMenu("Close Projector (Editor Preview)")]
        public void CloseProjectorPreview()
        {
            InitializeComponents();
            SetClosedState();
        }

        [ContextMenu("Preview Lock-On (Editor Preview)")]
        public void PreviewLockOn()
        {
            InitializeComponents();
            UpdateLockOnIndicator(1f, isLocked: true, alphaMultiplier: 1f);
        }

        [ContextMenu("Hide Lock-On (Editor Preview)")]
        public void HideLockOnPreview()
        {
            SetLockOnActive(false);
        }
    }
}
