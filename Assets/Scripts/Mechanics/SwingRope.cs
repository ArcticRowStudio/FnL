using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Interactive multi-pivot pendulum swing rope (유연한 다중 피봇 진자 운동 로프).
    /// Provides energetic, fluid, and responsive pendulum swinging combined with multi-pivot wave flexibility.
    /// Supports:
    /// - Grabbing with 'F' key when near the rope.
    /// - Effortless, light A/D momentum pumping to quickly build up sweeping swing arcs.
    /// - Realistic multi-pivot bending, S-curve flex, and inertia whip across segments.
    /// - Climbing up/down with W/S.
    /// - Releasing with 'F' key (or Space) to fling across massive chasms and gaps with high momentum.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    public class SwingRope : MonoBehaviour
    {
        [Header("Multi-Pivot Rope Configuration")]
        [Tooltip("Number of segments/pivots along the rope. More pivots = smoother, more flexible rope curve.")]
        [Range(3, 16)]
        public int segmentCount = 8;

        [Tooltip("Total hanging length of the rope in world units.")]
        [Range(1.5f, 15.0f)]
        public float ropeLength = 5.0f;

        [Tooltip("Natural gravity driving the pendulum (m/s^2). 14~18 gives a light, majestic swing.")]
        public float gravity = 16.0f;

        [Tooltip("Air resistance damping when swinging freely (lower = swings longer and easier).")]
        [Range(0.01f, 0.4f)]
        public float damping = 0.10f;

        [Tooltip("Angular acceleration added when player pumps with A/D (deg/s^2). High value = light, effortless swing.")]
        public float pumpForce = 180.0f;

        [Tooltip("Braking deceleration when player presses against swing direction (deg/s^2).")]
        public float brakeForce = 130.0f;

        [Tooltip("Flexibility / responsiveness of rope segments to wave motion.")]
        [Range(10f, 60f)]
        public float flexResponsiveness = 35.0f;

        [Tooltip("Wave inertia whip factor (higher = more dynamic S-curve whip).")]
        [Range(0.01f, 0.10f)]
        public float whipLagMultiplier = 0.035f;

        [Tooltip("Climbing speed up and down the swing rope with W/S (m/s).")]
        public float climbSpeed = 3.2f;

        [Tooltip("Maximum allowed swing angle from vertical (degrees).")]
        [Range(30f, 88f)]
        public float maxAngle = 78.0f;

        [Tooltip("The player only hangs on and rides along: no swinging, pumping or climbing. " +
                 "Jumping off keeps the anchor's motion instead of the swing boost.")]
        public bool hangOnly = false;

        [Header("Jump Off / Release Boost (멀리 점프)")]
        [Tooltip("Multiplier applied to rope swing speed when jumping off.")]
        public float launchBoost = 1.75f;

        [Tooltip("Additional upward vertical velocity added when jumping off.")]
        public float extraJumpUpward = 6.5f;

        [Tooltip("Minimum horizontal launch speed guaranteed when jumping off.")]
        public float minLaunchSpeed = 5.5f;

        [Header("Grab & Interaction Range")]
        [Tooltip("Max distance to rope for player to grab with 'F' key.")]
        public float grabRadius = 3.5f;

        [Header("Rendering & Sprites")]
        public int sortingOrder = 2;
        [Tooltip("Drawn width of the rope segments in world units.")]
        public float ropeWidth = 0.16f;
        public Sprite middleSprite;
        public Sprite topAnchorSprite;
        public Sprite bottomStopperSprite;

        [Tooltip("Optional art that swings as one rigid piece about the anchor (e.g. a crane hook). " +
                 "When set, the rope stays straight and the segment, anchor and stopper sprites are hidden.")]
        public Transform rigidVisual;

        [Header("Current Runtime State")]
        [SerializeField] private float currentAngle = 0f; // degrees
        [SerializeField] private float angularVelocity = 0f; // deg/s
        [SerializeField] private float grabDistance = 3.8f; // distance from anchor along rope where player is holding
        private PlayerController attachedPlayer;
        private float grabCooldown = 0f;
        private float currentAttachDuration = 0f;

        // Anchor motion, so a rope hung from something moving (a crane trolley) feels its acceleration
        private Vector2 prevAnchor;
        private float anchorVelocityX;
        private float anchorAccelX;
        private bool hasAnchorSample;

        // Dynamic multi-segment angles and physical node positions
        private float[] segmentAngles;
        private float[] segmentAngleVels;
        private Vector2[] nodePositions;
        private Vector2[] prevPositions;
        private Vector2[] nodeVelocities;

        // Child components and transforms
        private Transform topAnchorTransform;
        private Transform pivotsParent;
        private Transform[] segmentPivotTransforms;
        private SpriteRenderer[] segmentRenderers;
        private Transform bottomStopperTransform;
        private SpriteRenderer bottomRenderer;
        private SpriteRenderer topRenderer;
        private BoxCollider2D triggerCollider;

        public Vector2 AnchorPosition => transform.position;
        public float CurrentAngle => currentAngle;
        public float AngularVelocity => angularVelocity;
        public bool HasPlayer => attachedPlayer != null;
        public float GrabDistance => grabDistance;
        public PlayerController AttachedPlayer => attachedPlayer;

        public float SegmentLength => ropeLength / Mathf.Max(1, segmentCount);

        private void Awake()
        {
            hasAnchorSample = false;
            anchorVelocityX = anchorAccelX = 0f;
            EnsureComponents();
            InitializeArraysIfEmpty();
        }

        private void Start()
        {
            EnsureComponents();
            InitializeArraysIfEmpty();
            RebuildVisualsAndCollider();
        }

        private void OnValidate()
        {
            if (segmentCount < 3) segmentCount = 3;
            if (ropeLength < 1.5f) ropeLength = 1.5f;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= OnEditorRebuild;
            UnityEditor.EditorApplication.delayCall += OnEditorRebuild;
#endif
        }

#if UNITY_EDITOR
        private void OnEditorRebuild()
        {
            if (this == null) return;
            EnsureComponents();
            InitializeArraysIfEmpty();
            RebuildVisualsAndCollider();
        }
#endif

        private void Reset()
        {
            LoadDefaultSprites();
            EnsureComponents();
            InitializeArraysIfEmpty();
            RebuildVisualsAndCollider();
        }

        private void LoadDefaultSprites()
        {
#if UNITY_EDITOR
            if (middleSprite == null)
                middleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Pixel/Environment/Rope/Rope_Middle.png");
            if (topAnchorSprite == null)
                topAnchorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Pixel/Environment/Rope/Rope_TopAnchor.png");
            if (bottomStopperSprite == null)
                bottomStopperSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Pixel/Environment/Rope/Rope_BottomStopper.png");
#endif
        }

        private void InitializeArraysIfEmpty()
        {
            int totalNodes = segmentCount + 1;
            if (nodePositions == null || nodePositions.Length != totalNodes || segmentAngles == null || segmentAngles.Length != segmentCount)
            {
                segmentAngles = new float[segmentCount];
                segmentAngleVels = new float[segmentCount];
                nodePositions = new Vector2[totalNodes];
                prevPositions = new Vector2[totalNodes];
                nodeVelocities = new Vector2[totalNodes];

                for (int i = 0; i < segmentCount; i++)
                {
                    segmentAngles[i] = currentAngle;
                    segmentAngleVels[i] = 0f;
                }

                ReconstructNodePositions();
                for (int i = 0; i <= segmentCount; i++)
                {
                    prevPositions[i] = nodePositions[i];
                    nodeVelocities[i] = Vector2.zero;
                }
            }
        }

        public void EnsureComponents()
        {
            if (middleSprite == null || topAnchorSprite == null || bottomStopperSprite == null)
            {
                LoadDefaultSprites();
            }

            var rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            triggerCollider = GetComponent<BoxCollider2D>();
            if (triggerCollider == null) triggerCollider = gameObject.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;

            topAnchorTransform = transform.Find("TopAnchor");
            if (topAnchorTransform == null)
            {
                var go = new GameObject("TopAnchor");
                go.transform.SetParent(transform, false);
                topAnchorTransform = go.transform;
            }
            topAnchorTransform.localPosition = new Vector3(0f, 0.08f, 0f);
            topRenderer = topAnchorTransform.GetComponent<SpriteRenderer>();
            if (topRenderer == null) topRenderer = topAnchorTransform.gameObject.AddComponent<SpriteRenderer>();

            Transform legacyPivot = transform.Find("RopePivot");
            if (legacyPivot != null)
            {
                DestroyImmediate(legacyPivot.gameObject);
            }

            pivotsParent = transform.Find("Pivots");
            if (pivotsParent == null)
            {
                var go = new GameObject("Pivots");
                go.transform.SetParent(transform, false);
                pivotsParent = go.transform;
            }
            pivotsParent.localPosition = Vector3.zero;

            EnsureSegmentPivots();

            bottomStopperTransform = transform.Find("BottomStopper");
            if (bottomStopperTransform == null)
            {
                var go = new GameObject("BottomStopper");
                go.transform.SetParent(transform, false);
                bottomStopperTransform = go.transform;
            }
            bottomRenderer = bottomStopperTransform.GetComponent<SpriteRenderer>();
            if (bottomRenderer == null) bottomRenderer = bottomStopperTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        private void EnsureSegmentPivots()
        {
            if (pivotsParent == null) return;

            while (pivotsParent.childCount > segmentCount)
            {
                DestroyImmediate(pivotsParent.GetChild(pivotsParent.childCount - 1).gameObject);
            }

            segmentPivotTransforms = new Transform[segmentCount];
            segmentRenderers = new SpriteRenderer[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                string pivotName = $"Pivot_{i}";
                Transform p = pivotsParent.Find(pivotName);
                if (p == null)
                {
                    var go = new GameObject(pivotName);
                    go.transform.SetParent(pivotsParent, false);
                    p = go.transform;
                }

                Transform spriteChild = p.Find("RopeSegmentSprite");
                if (spriteChild == null)
                {
                    var sGo = new GameObject("RopeSegmentSprite");
                    sGo.transform.SetParent(p, false);
                    spriteChild = sGo.transform;
                }

                var sr = spriteChild.GetComponent<SpriteRenderer>();
                if (sr == null) sr = spriteChild.gameObject.AddComponent<SpriteRenderer>();

                segmentPivotTransforms[i] = p;
                segmentRenderers[i] = sr;
            }
        }

        public void RebuildVisualsAndCollider()
        {
            EnsureComponents();
            InitializeArraysIfEmpty();

            float segLen = SegmentLength;

            if (triggerCollider != null)
            {
                triggerCollider.size = new Vector2(ropeLength * 1.8f, ropeLength + 1.5f);
                triggerCollider.offset = new Vector2(0f, -ropeLength * 0.5f);
            }

            if (topRenderer != null && topAnchorSprite != null)
            {
                topRenderer.sprite = topAnchorSprite;
                topRenderer.sortingOrder = sortingOrder + 2;
                topRenderer.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            }

            for (int i = 0; i < segmentCount; i++)
            {
                var sr = segmentRenderers[i];
                if (sr != null && middleSprite != null)
                {
                    sr.sprite = middleSprite;
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.tileMode = SpriteTileMode.Continuous;
                    sr.size = new Vector2(ropeWidth, segLen);
                    sr.transform.localPosition = new Vector3(0f, -segLen * 0.5f, 0f);
                    sr.sortingOrder = sortingOrder;
                }
            }

            if (bottomRenderer != null && bottomStopperSprite != null)
            {
                bottomRenderer.sprite = bottomStopperSprite;
                bottomRenderer.sortingOrder = sortingOrder + 1;
                bottomRenderer.transform.localPosition = new Vector3(0f, -ropeLength, 0f);
            }

            bool showRope = rigidVisual == null;
            if (topRenderer != null) topRenderer.enabled = showRope;
            if (bottomRenderer != null) bottomRenderer.enabled = showRope;
            if (segmentRenderers != null)
            {
                foreach (var sr in segmentRenderers)
                {
                    if (sr != null) sr.enabled = showRope;
                }
            }

            ReconstructNodePositions();
            UpdateVisualTransforms();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                InitializeArraysIfEmpty();
                for (int i = 0; i < segmentCount; i++)
                {
                    segmentAngles[i] = currentAngle;
                }
                ReconstructNodePositions();
                UpdateVisualTransforms();
                return;
            }

            if (grabCooldown > 0f)
            {
                grabCooldown -= Time.deltaTime;
            }

            SampleAnchorMotion(Time.deltaTime);
            float dt = Mathf.Min(Time.deltaTime, 0.025f);
            SimulateRopePhysics(dt);
            UpdateVisualTransforms();

            if (attachedPlayer != null)
            {
                UpdateAttachedPlayer(dt);
            }
        }

        private void SampleAnchorMotion(float frameDt)
        {
            Vector2 anchor = AnchorPosition;
            if (hasAnchorSample && frameDt > 0f)
            {
                float vx = (anchor.x - prevAnchor.x) / frameDt;
                anchorAccelX = (vx - anchorVelocityX) / frameDt;
                anchorVelocityX = vx;
            }
            else
            {
                anchorVelocityX = anchorAccelX = 0f;
                hasAnchorSample = true;
            }
            prevAnchor = anchor;
        }

        private void SimulateRopePhysics(float dt)
        {
            if (nodePositions == null || nodePositions.Length != segmentCount + 1 || segmentAngles == null)
            {
                InitializeArraysIfEmpty();
            }

            // 1. Primary Pendulum Physics Equation
            float thetaRad = currentAngle * Mathf.Deg2Rad;
            float effectiveLength = attachedPlayer != null ? grabDistance : ropeLength;
            effectiveLength = Mathf.Max(1.0f, effectiveLength);

            // Restoring torque from gravity: -(g / L) * sin(theta)
            float gravTorque = -(gravity / effectiveLength) * Mathf.Sin(thetaRad) * Mathf.Rad2Deg;

            // Air damping (player momentum helps sustain swing)
            float totalDamping = damping;
            if (attachedPlayer != null) totalDamping *= 0.65f;
            float dampTorque = -angularVelocity * totalDamping;

            // Player pump input (A / D)
            float inputTorque = 0f;
            if (attachedPlayer != null)
            {
                float inputX = attachedPlayer.MoveInput.x;
                if (Mathf.Abs(inputX) > 0.1f)
                {
                    // Pumping in direction of swing increases speed rapidly
                    bool pumpingWithSwing = (inputX > 0 && angularVelocity >= -5f) || (inputX < 0 && angularVelocity <= 5f);
                    if (pumpingWithSwing)
                    {
                        inputTorque = Mathf.Sign(inputX) * pumpForce;
                    }
                    else
                    {
                        // Holding opposite direction applies firm brake
                        inputTorque = Mathf.Sign(inputX) * brakeForce;
                    }
                }
            }

            // An accelerating anchor drags the rope the other way: -(a / L) * cos(theta)
            float carryTorque = -(anchorAccelX / effectiveLength) * Mathf.Cos(thetaRad) * Mathf.Rad2Deg;

            float alpha = gravTorque + dampTorque + inputTorque + carryTorque;
            angularVelocity += alpha * dt;
            currentAngle += angularVelocity * dt;

            // Limit to max swing angle with elastic bounce
            if (currentAngle > maxAngle)
            {
                currentAngle = maxAngle;
                angularVelocity = -Mathf.Abs(angularVelocity) * 0.35f;
            }
            else if (currentAngle < -maxAngle)
            {
                currentAngle = -maxAngle;
                angularVelocity = Mathf.Abs(angularVelocity) * 0.35f;
            }

            if (hangOnly)
            {
                currentAngle = 0f;
                angularVelocity = 0f;
            }

            // 2. Multi-Pivot Wave & Bending Simulation
            // Segments follow smoothly with subtle trailing wave, maintaining solid tension where player holds
            float s = grabDistance / SegmentLength;
            int playerSeg = attachedPlayer != null ? Mathf.Clamp(Mathf.FloorToInt(s), 0, segmentCount - 1) : -1;

            float flexDampingFactor = 10.0f;

            for (int i = 0; i < segmentCount; i++)
            {
                float targetAngle = currentAngle;

                if (rigidVisual != null)
                {
                    // Rigid art: the rope is one straight bar, so the grab point stays on the drawn piece
                    segmentAngles[i] = targetAngle;
                    segmentAngleVels[i] = 0f;
                    continue;
                }

                // Natural inertia lag along the tensioned upper rope
                float t = (i + 1) / (float)segmentCount;
                float waveLag = Mathf.Clamp(-angularVelocity * whipLagMultiplier * t, -16f, 16f);
                targetAngle += waveLag;

                // Free-swinging tail below player's grip has lively, playful whip
                if (attachedPlayer != null && i > playerSeg)
                {
                    float tailT = (i - playerSeg);
                    float tailWhip = Mathf.Clamp(-angularVelocity * 0.12f * tailT, -36f, 36f);
                    targetAngle += tailWhip;
                }

                float diff = targetAngle - segmentAngles[i];
                segmentAngleVels[i] += (diff * flexResponsiveness - segmentAngleVels[i] * flexDampingFactor) * dt;
                segmentAngles[i] += segmentAngleVels[i] * dt;
            }

            // 3. Reconstruct Node Positions & Compute Velocities
            for (int i = 0; i <= segmentCount; i++)
            {
                prevPositions[i] = nodePositions[i];
            }

            ReconstructNodePositions();

            for (int i = 0; i <= segmentCount; i++)
            {
                nodeVelocities[i] = (nodePositions[i] - prevPositions[i]) / Mathf.Max(0.0001f, dt);
            }
        }

        private void ReconstructNodePositions()
        {
            if (nodePositions == null || segmentAngles == null) return;

            Vector2 anchor = AnchorPosition;
            nodePositions[0] = anchor;

            float segLen = SegmentLength;
            for (int i = 0; i < segmentCount; i++)
            {
                float rad = segmentAngles[i] * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
                nodePositions[i + 1] = nodePositions[i] + dir * segLen;
            }
        }

        private void UpdateVisualTransforms()
        {
            if (nodePositions == null || segmentPivotTransforms == null) return;

            if (rigidVisual != null)
            {
                rigidVisual.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
            }

            for (int i = 0; i < segmentCount; i++)
            {
                if (i >= segmentPivotTransforms.Length || segmentPivotTransforms[i] == null) continue;

                Vector2 p0 = nodePositions[i];
                Vector2 p1 = nodePositions[i + 1];
                Vector2 dir = p1 - p0;
                float segDist = dir.magnitude;

                segmentPivotTransforms[i].position = new Vector3(p0.x, p0.y, transform.position.z);
                segmentPivotTransforms[i].rotation = Quaternion.Euler(0f, 0f, segmentAngles[i]);

                if (i < segmentRenderers.Length && segmentRenderers[i] != null)
                {
                    segmentRenderers[i].size = new Vector2(ropeWidth, segDist);
                    segmentRenderers[i].transform.localPosition = new Vector3(0f, -segDist * 0.5f, 0f);
                }
            }

            if (bottomStopperTransform != null && nodePositions.Length > segmentCount)
            {
                Vector2 lastNode = nodePositions[segmentCount];
                bottomStopperTransform.position = new Vector3(lastNode.x, lastNode.y, transform.position.z);
                if (segmentAngles != null && segmentAngles.Length > 0)
                {
                    bottomStopperTransform.rotation = Quaternion.Euler(0f, 0f, segmentAngles[segmentCount - 1]);
                }
            }
        }

        private void UpdateAttachedPlayer(float dt)
        {
            currentAttachDuration += dt;

            // Vertical climbing along rope with W / S
            float inputY = attachedPlayer.MoveInput.y;
            if (!hangOnly && Mathf.Abs(inputY) > 0.1f)
            {
                grabDistance -= inputY * climbSpeed * dt;
                grabDistance = Mathf.Clamp(grabDistance, 0.8f, ropeLength - 0.25f);
            }

            // Sync player position to rope grab point
            Vector3 grabPos = GetGrabWorldPosition();
            attachedPlayer.Teleport(new Vector3(grabPos.x, grabPos.y, attachedPlayer.transform.position.z));

            // Tilt player sprite smoothly along local rope tangent (pivot is directly at the hands)
            Vector2 tangent = GetGrabTangent();
            float ropeAngle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg + 90f;
            if (attachedPlayer.SpriteRenderer != null)
            {
                float inX = attachedPlayer.MoveInput.x;
                if (inX > 0.1f)
                {
                    attachedPlayer.SpriteRenderer.flipX = false;
                }
                else if (inX < -0.1f)
                {
                    attachedPlayer.SpriteRenderer.flipX = true;
                }

                // Tilt player sprite along the rope angle smoothly
                attachedPlayer.SpriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, ropeAngle * 0.75f);
            }

            // Check for release with 'F' key or 'Space' (Jump) - require brief grace period to prevent instant release on grab
            if (currentAttachDuration > 0.15f)
            {
                bool fPressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
                bool jumpPressed = attachedPlayer.JumpAction.WasPressedThisFrame();
                if (fPressed || jumpPressed)
                {
                    ReleasePlayerWithJump();
                }
            }
        }

        public Vector3 GetGrabWorldPosition()
        {
            if (nodePositions == null || nodePositions.Length <= segmentCount)
            {
                return AnchorPosition + Vector2.down * grabDistance;
            }

            float segLen = SegmentLength;
            float s = grabDistance / segLen;
            int seg = Mathf.Clamp(Mathf.FloorToInt(s), 0, segmentCount - 1);
            float u = s - seg;

            Vector2 p = Vector2.Lerp(nodePositions[seg], nodePositions[seg + 1], u);
            return new Vector3(p.x, p.y, transform.position.z);
        }

        public Vector2 GetGrabVelocity()
        {
            if (nodeVelocities == null || nodeVelocities.Length <= segmentCount)
            {
                return Vector2.zero;
            }

            float segLen = SegmentLength;
            float s = grabDistance / segLen;
            int seg = Mathf.Clamp(Mathf.FloorToInt(s), 0, segmentCount - 1);
            float u = s - seg;

            return Vector2.Lerp(nodeVelocities[seg], nodeVelocities[seg + 1], u);
        }

        public Vector2 GetGrabTangent()
        {
            if (nodePositions == null || nodePositions.Length <= segmentCount)
            {
                return Vector2.down;
            }

            float segLen = SegmentLength;
            float s = grabDistance / segLen;
            int seg = Mathf.Clamp(Mathf.FloorToInt(s), 0, segmentCount - 1);

            return (nodePositions[seg + 1] - nodePositions[seg]).normalized;
        }

        public bool IsPlayerInGrabRange(PlayerController player, float range = -1f)
        {
            if (player == null) return false;
            if (range <= 0f) range = grabRadius;

            Vector2 pPos = player.transform.position;

            if (nodePositions == null || nodePositions.Length <= segmentCount)
            {
                Vector2 top = AnchorPosition;
                Vector2 bot = top + Vector2.down * ropeLength;
                return DistancePointToSegment(pPos, top, bot) <= range;
            }

            for (int i = 0; i < segmentCount; i++)
            {
                float d = DistancePointToSegment(pPos, nodePositions[i], nodePositions[i + 1]);
                if (d <= range) return true;
            }

            return false;
        }

        public float GetNearestGrabDistance(Vector2 point)
        {
            if (nodePositions == null) return ropeLength * 0.7f;

            float minD = float.MaxValue;
            float bestDistance = ropeLength * 0.7f;
            float segLen = SegmentLength;

            for (int i = 0; i < segmentCount; i++)
            {
                float t;
                float d = DistancePointToSegmentWithT(point, nodePositions[i], nodePositions[i + 1], out t);
                if (d < minD)
                {
                    minD = d;
                    bestDistance = (i + t) * segLen;
                }
            }

            return Mathf.Clamp(bestDistance, 0.8f, ropeLength - 0.25f);
        }

        public bool TryAttachPlayer(PlayerController player)
        {
            if (attachedPlayer != null) return false;
            if (grabCooldown > 0f) return false;
            if (player == null) return false;

            attachedPlayer = player;
            currentAttachDuration = 0f;
            grabDistance = GetNearestGrabDistance(player.transform.position);

            // Transfer incoming player horizontal momentum directly into swing angular momentum!
            float incomingVx = player.Velocity.x;
            if (Mathf.Abs(incomingVx) > 0.5f)
            {
                float inducedOmega = (incomingVx / Mathf.Max(1.0f, grabDistance)) * Mathf.Rad2Deg * 0.85f;
                angularVelocity += inducedOmega;
            }

            player.StartSwinging(this);
            return true;
        }

        public void ReleasePlayerWithJump()
        {
            if (attachedPlayer == null) return;

            PlayerController player = attachedPlayer;
            attachedPlayer = null;
            grabCooldown = 0.4f;

            if (player.SpriteRenderer != null)
            {
                player.SpriteRenderer.transform.localRotation = Quaternion.identity;
            }

            Vector2 ropeVel = GetGrabVelocity();

            if (hangOnly)
            {
                // A plain hop off that keeps whatever the anchor (e.g. a crane trolley) was doing
                player.ReleaseSwing(new Vector2(ropeVel.x, extraJumpUpward));
                return;
            }

            // Tangential / horizontal launch speed with high boost
            float launchVx = ropeVel.x * launchBoost;

            // Upward boost
            float launchVy = Mathf.Max(0f, ropeVel.y * launchBoost) + extraJumpUpward;

            // Guarantee a strong minimum forward launch speed in the direction of swing
            if (Mathf.Abs(launchVx) < minLaunchSpeed)
            {
                float dir = Mathf.Sign(ropeVel.x);
                if (Mathf.Abs(dir) < 0.1f)
                {
                    dir = Mathf.Sign(angularVelocity);
                }
                if (Mathf.Abs(dir) < 0.1f)
                {
                    dir = (player.SpriteRenderer != null && player.SpriteRenderer.flipX) ? -1f : 1f;
                }
                launchVx = dir * minLaunchSpeed;
            }

            player.ReleaseSwing(new Vector2(launchVx, launchVy));
        }

        public void ForceDetach()
        {
            if (attachedPlayer == null) return;
            PlayerController player = attachedPlayer;
            attachedPlayer = null;
            grabCooldown = 0.3f;

            if (player.SpriteRenderer != null)
            {
                player.SpriteRenderer.transform.localRotation = Quaternion.identity;
            }

            player.ReleaseSwing(Vector2.zero);
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            float t;
            return DistancePointToSegmentWithT(point, a, b, out t);
        }

        private static float DistancePointToSegmentWithT(Vector2 point, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
            {
                t = 0f;
                return Vector2.Distance(point, a);
            }
            t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            Vector2 projection = a + t * ab;
            return Vector2.Distance(point, projection);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || Camera.main == null) return;

            var player = attachedPlayer != null ? attachedPlayer : Object.FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            bool isHolding = attachedPlayer != null;
            bool inRange = isHolding || IsPlayerInGrabRange(player, grabRadius);

            if (!inRange) return;

            Vector3 promptPos = isHolding ? GetGrabWorldPosition() + new Vector3(0f, 0.75f, 0f)
                                          : transform.position + new Vector3(0f, -ropeLength * 0.5f, 0f);

            Vector3 screenPos = Camera.main.WorldToScreenPoint(promptPos);
            if (screenPos.z <= 0) return;

            string text = !isHolding ? "[F] GRAB"
                        : hangOnly ? "[F / SPACE] DROP"
                        : "[F / SPACE] LET GO   [A/D] SWING";

            float width = isHolding ? (hangOnly ? 150f : 260f) : 100f;
            float height = 26f;
            Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

            GUI.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.88f);
            GUI.Box(rect, GUIContent.none);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = isHolding ? new Color(0.3f, 1f, 0.6f) : new Color(1f, 0.9f, 0.2f);
            GUI.Label(rect, text, style);
        }

        private void OnDrawGizmos()
        {
            Vector3 anchor = transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(anchor, 0.2f);

            if (nodePositions != null && nodePositions.Length > segmentCount)
            {
                Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
                for (int i = 0; i < segmentCount; i++)
                {
                    Gizmos.DrawLine(nodePositions[i], nodePositions[i + 1]);
                    Gizmos.DrawWireSphere(nodePositions[i + 1], 0.08f);
                }

                if (attachedPlayer != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(GetGrabWorldPosition(), 0.18f);
                }
            }
            else
            {
                Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.5f);
                Gizmos.DrawLine(anchor, anchor + Vector3.down * ropeLength);
            }
        }
    }
}
