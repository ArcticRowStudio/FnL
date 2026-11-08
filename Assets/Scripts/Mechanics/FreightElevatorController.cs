using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Open-Cage Industrial Freight Elevator / Cargo Lift (화물승강기 / 개방형 화물 엘리베이터).
    /// Features:
    /// - Automatic Passenger Detection (Player steps onto platform -> safety bar lowers -> departs).
    /// - Interactive on-board Button / Key press (E, W/Up, Space, or mouse click) to operate.
    /// - Smooth KinematicObject & Rigidbody2D carrier physics (zero-jitter, keeps player grounded).
    /// - Articulated yellow/black hazard safety bar (lowers on departure, raises on arrival).
    /// - Rotating / pulsing amber industrial warning beacon during transit.
    /// - Dynamic suspension cables & counterweight system.
    /// - Mechanical arrival bounce with pneumatic air release hiss and steam blast.
    /// - Multi-station / 2-station operation with floor call button support.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class FreightElevatorController : MonoBehaviour
    {
        public enum ElevatorState
        {
            AtFloor,                // Parked at a floor, safety bar open
            PassengerBoardedDelay,  // Player boarded, safety bar lowering, pre-departure pause
            Moving,                 // In motion towards destination
            ArrivedSettling,        // Arrival bounce, pneumatic hiss, opening safety bar
            CooldownWait            // Brief pause before next cycle can begin
        }

        public enum FloorIndex
        {
            Bottom = 0,
            Top = 1
        }

        [Header("Elevator Motion Settings")]
        [Tooltip("Vertical travel distance from Bottom floor to Top floor in world units")]
        public float travelHeight = 12.0f;
        [Tooltip("Max vertical travel speed in units per second")]
        public float moveSpeed = 3.8f;
        [Tooltip("Acceleration / deceleration smoothing (higher = sharper, lower = smoother)")]
        [Range(1f, 10f)]
        public float accelerationRamp = 3.2f;
        [Tooltip("Pause duration after passenger boarding before elevator starts moving (seconds)")]
        public float preDepartureDelay = 0.75f;
        [Tooltip("Cooldown after arriving before the elevator can depart again (seconds)")]
        public float floorArrivalCooldown = 1.0f;

        [Header("Operation Mode")]
        [Tooltip("켜면 플레이어가 올라타자마자 자동 출발. 끄면 E 키나 조작 패널 버튼을 눌러야 출발")]
        public bool autoDepartOnBoarding = false;
        [Tooltip("Allow pressing E while inside to start elevator")]
        public bool allowKeyboardInteraction = true;

        [Header("Starting Station")]
        public FloorIndex currentFloor = FloorIndex.Bottom;

        [Header("Elevator Dimensions & Customization (엘리베이터 규격 및 줄 길이)")]
        [Tooltip("화물 엘리베이터의 가로 폭(넓이) (단위: 월드 유닛, 기본 4.2m)")]
        [Range(2.0f, 15.0f)]
        public float elevatorWidth = 4.2f;

        [Tooltip("엘리베이터가 최하층(출발층)에 있을 때 상부 천장 앵커까지 연결되는 총 케이블 길이 (단위: 월드 유닛, 기본 16m). 가이드 레일도 이 앵커(천장)까지만 그려짐")]
        [Range(4.0f, 60.0f)]
        public float cableLength = 16.0f;

        [Header("Side Exit (옆으로 내리기)")]
        [Tooltip("안전바(문)가 완전히 열려 있는 동안 좌우 난간 충돌을 꺼서 옆으로 걸어 나갈 수 있게 함")]
        public bool openSidesWhenDoorOpen = true;

        [Header("Visual Components")]
        [Tooltip("Main moving elevator cabin frame (parent of deck, mesh, railings, bar)")]
        public Transform cabinTransform;
        [Tooltip("Front safety barrier bar that lowers when moving and raises when stopped")]
        public Transform safetyBarTransform;
        [Tooltip("Local Y position of the safety bar when fully open (raised)")]
        public float safetyBarOpenY = 2.4f;
        [Tooltip("Local Y position of the safety bar when fully closed (lowered)")]
        public float safetyBarClosedY = 0.95f;
        [Tooltip("Speed of safety bar motion")]
        public float safetyBarSpeed = 4.0f;

        [Header("Cables & Counterweight")]
        [Tooltip("Optional counterweight moving inversely along side track")]
        public Transform counterweightTransform;
        [Tooltip("Suspension cable renderers (will be stretched/tiled dynamically)")]
        public SpriteRenderer[] cableRenderers;
        [Tooltip("Fixed world Y position where cables attach at the top of the shaft/ceiling")]
        public float ceilingCableAnchorY = 20.0f;

        [Header("Warning Lighting & Visuals")]
        [Tooltip("Amber hazard warning beacon light")]
        public Light2D beaconLight;
        [Tooltip("SpriteRenderer for the amber beacon")]
        public SpriteRenderer beaconRenderer;
        [Tooltip("Amber warning strobe intensity when active")]
        public float beaconMaxIntensity = 2.5f;
        [Tooltip("Amber warning strobe frequency")]
        public float beaconStrobeSpeed = 12.0f;
        [Tooltip("Status indicator light on the control panel")]
        public Light2D statusPanelLight;
        public Color readyStatusColor = new Color(0.2f, 1.0f, 0.4f, 1.0f);     // Green
        public Color movingStatusColor = new Color(1.0f, 0.7f, 0.1f, 1.0f);    // Amber
        public Color lockedStatusColor = new Color(1.0f, 0.2f, 0.2f, 1.0f);    // Red

        [Header("Passenger Detection")]
        [Tooltip("Trigger collider that detects player boarding inside the elevator cage")]
        public Collider2D cabinPassengerTrigger;
        [Tooltip("Platform solid collider the player stands on")]
        public Collider2D platformDeckCollider;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip safetyBarLowerSound;
        public AudioClip safetyBarRaiseSound;
        public AudioClip motorHumLoopSound;
        public AudioClip pneumaticArrivalHissSound;
        public AudioClip arrivalThumpSound;

        [Header("Particle Effects")]
        public ParticleSystem departureSteamPuff;
        public ParticleSystem arrivalSteamPuff;

        // Runtime state variables
        [Header("Debug / Runtime Info")]
        [SerializeField] private ElevatorState state = ElevatorState.AtFloor;
        [SerializeField] private FloorIndex targetFloor = FloorIndex.Top;
        [SerializeField] private bool hasPassenger = false;

        private Vector3 initialCabinLocalPos;
        private Vector3 bottomCabinWorldPos;
        private Vector3 topCabinWorldPos;
        private float currentTravelProgress = 0f; // 0 = Bottom, 1 = Top
        private float targetTravelProgress = 0f;
        private float currentVelocity = 0f;
        private float stateTimer = 0f;
        private float targetSafetyBarY;
        private float currentSafetyBarY;

        // Passenger tracking
        private readonly HashSet<Collider2D> insideCabinColliders = new HashSet<Collider2D>();
        private readonly List<KinematicObject> activeKinematicPassengers = new List<KinematicObject>();
        private readonly List<Rigidbody2D> activeRigidbodyPassengers = new List<Rigidbody2D>();
        private readonly Collider2D[] overlapBuffer = new Collider2D[16];
        private Vector3 previousCabinWorldPos;
        private Collider2D[] sideRailingColliders = new Collider2D[0];

        public ElevatorState State => state;
        public FloorIndex CurrentFloor => currentFloor;
        public bool HasPassenger => hasPassenger;

        private void Awake()
        {
            if (cabinTransform == null) cabinTransform = transform;
            initialCabinLocalPos = cabinTransform.localPosition;

            // Compute bottom and top world positions based on travelHeight
            if (currentFloor == FloorIndex.Bottom)
            {
                bottomCabinWorldPos = cabinTransform.position;
                topCabinWorldPos = bottomCabinWorldPos + new Vector3(0f, travelHeight, 0f);
                currentTravelProgress = 0f;
                targetTravelProgress = 0f;
            }
            else
            {
                topCabinWorldPos = cabinTransform.position;
                bottomCabinWorldPos = topCabinWorldPos - new Vector3(0f, travelHeight, 0f);
                currentTravelProgress = 1f;
                targetTravelProgress = 1f;
            }

            previousCabinWorldPos = cabinTransform.position;
            currentSafetyBarY = safetyBarOpenY;
            targetSafetyBarY = safetyBarOpenY;

            if (safetyBarTransform != null)
            {
                Vector3 barPos = safetyBarTransform.localPosition;
                barPos.y = currentSafetyBarY;
                safetyBarTransform.localPosition = barPos;
            }

            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            CacheSideRailingColliders();

            if (cableLength < travelHeight) cableLength = travelHeight;
            ceilingCableAnchorY = bottomCabinWorldPos.y + 3.2f + cableLength;

            ApplyDimensions();
        }

        private void Start()
        {
            SetState(ElevatorState.AtFloor);
            UpdateStatusLighting();
            UpdateCablesAndCounterweight();
        }

        // Called by ElevatorCabinTrigger on child PassengerCabinTrigger
        public void NotifyTriggerEnter(Collider2D other)
        {
            OnTriggerEnter2D(other);
        }

        public void NotifyTriggerExit(Collider2D other)
        {
            OnTriggerExit2D(other);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger) return;
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null || other.GetComponentInParent<KinematicObject>() != null)
            {
                insideCabinColliders.Add(other);
                UpdatePassengerStatus();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (insideCabinColliders.Remove(other))
            {
                UpdatePassengerStatus();
            }
        }

        /// <summary>
        /// Active overlap check that runs in FixedUpdate to ensure passengers are detected
        /// even without relying on Unity trigger propagation matrix.
        /// </summary>
        private void CheckActivePassengerOverlap()
        {
            if (cabinTransform == null) return;
            // The whole cage, scale included. A fixed 3.8 x 2.8 box was narrower than a scaled
            // cabin, so a passenger walking to either side stopped being carried and the deck rose into them.
            Vector2 center, size;
            if (cabinPassengerTrigger != null)
            {
                Bounds tb = cabinPassengerTrigger.bounds;
                center = tb.center;
                size = tb.size;
            }
            else
            {
                Vector3 scale = cabinTransform.lossyScale;
                center = (Vector2)cabinTransform.position + new Vector2(0f, 1.4f * Mathf.Abs(scale.y));
                size = new Vector2((elevatorWidth - 0.4f) * Mathf.Abs(scale.x), 2.8f * Mathf.Abs(scale.y));
            }
            Bounds cabinBounds = new Bounds(center, size);

            // 1. Remove colliders that are destroyed, inactive, or no longer overlap cabin bounds
            insideCabinColliders.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy || !cabinBounds.Intersects(c.bounds));

            // 2. Discover active colliders inside cabin
            var cols = Physics2D.OverlapBoxAll(center, size, 0f);
            if (cols != null)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    var col = cols[i];
                    if (col == null || col.isTrigger) continue;
                    if (col.CompareTag("Player") || col.GetComponentInParent<PlayerController>() != null || col.GetComponentInParent<KinematicObject>() != null)
                    {
                        insideCabinColliders.Add(col);
                    }
                }
            }

            UpdatePassengerStatus();
        }

        private void UpdatePassengerStatus()
        {
            // Clean up any destroyed objects
            insideCabinColliders.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);

            bool previousHasPassenger = hasPassenger;
            hasPassenger = insideCabinColliders.Count > 0;

            activeKinematicPassengers.Clear();
            activeRigidbodyPassengers.Clear();

            foreach (var col in insideCabinColliders)
            {
                var ko = col.GetComponentInParent<KinematicObject>();
                if (ko != null && !activeKinematicPassengers.Contains(ko))
                {
                    activeKinematicPassengers.Add(ko);
                }

                var rb = col.attachedRigidbody;
                if (rb != null && rb.bodyType != RigidbodyType2D.Static && !activeRigidbodyPassengers.Contains(rb))
                {
                    activeRigidbodyPassengers.Add(rb);
                }
            }

            // Auto-departure if enabled
            if (!previousHasPassenger && hasPassenger && state == ElevatorState.AtFloor && autoDepartOnBoarding)
            {
                TriggerBoardedSequence();
            }
        }

        /// <summary>
        /// Toggles floor between Bottom and Top.
        /// </summary>
        public void ToggleFloor()
        {
            if (state != ElevatorState.AtFloor) return;
            targetFloor = (currentFloor == FloorIndex.Bottom) ? FloorIndex.Top : FloorIndex.Bottom;
            targetTravelProgress = (targetFloor == FloorIndex.Top) ? 1.0f : 0.0f;

            targetSafetyBarY = safetyBarClosedY;
            if (safetyBarLowerSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(safetyBarLowerSound, 0.9f);
            }

            SetState(ElevatorState.PassengerBoardedDelay);
        }

        public void GoUp()
        {
            CallElevatorToFloor(FloorIndex.Top);
        }

        public void GoDown()
        {
            CallElevatorToFloor(FloorIndex.Bottom);
        }

        /// <summary>
        /// Initiates the departure sequence after player steps onto the elevator platform.
        /// </summary>
        public void TriggerBoardedSequence()
        {
            ToggleFloor();
        }

        /// <summary>
        /// External call command (e.g. from a call button on a floor station).
        /// </summary>
        public void CallElevatorToFloor(FloorIndex destination)
        {
            if (state == ElevatorState.Moving) return;
            if (currentFloor == destination && state == ElevatorState.AtFloor)
            {
                // Already at destination floor; if player is on board, toggle to other floor
                if (hasPassenger)
                {
                    ToggleFloor();
                }
                return;
            }

            targetFloor = destination;
            targetTravelProgress = (targetFloor == FloorIndex.Top) ? 1.0f : 0.0f;

            // If currently open, close safety bar first
            targetSafetyBarY = safetyBarClosedY;
            SetState(ElevatorState.PassengerBoardedDelay);
        }

        private void SetState(ElevatorState newState)
        {
            state = newState;
            stateTimer = 0f;

            switch (state)
            {
                case ElevatorState.AtFloor:
                    targetSafetyBarY = safetyBarOpenY;
                    currentVelocity = 0f;
                    if (audioSource != null && audioSource.isPlaying && audioSource.clip == motorHumLoopSound)
                    {
                        audioSource.Stop();
                    }
                    break;

                case ElevatorState.PassengerBoardedDelay:
                    targetSafetyBarY = safetyBarClosedY;
                    break;

                case ElevatorState.Moving:
                    if (departureSteamPuff != null) departureSteamPuff.Play();
                    if (motorHumLoopSound != null && audioSource != null)
                    {
                        audioSource.clip = motorHumLoopSound;
                        audioSource.loop = true;
                        audioSource.volume = 0.7f;
                        audioSource.Play();
                    }
                    break;

                case ElevatorState.ArrivedSettling:
                    currentFloor = targetFloor;
                    currentVelocity = 0f;
                    if (audioSource != null && audioSource.clip == motorHumLoopSound)
                    {
                        audioSource.Stop();
                    }
                    if (pneumaticArrivalHissSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(pneumaticArrivalHissSound, 0.85f);
                    }
                    if (arrivalThumpSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(arrivalThumpSound, 0.75f);
                    }
                    if (arrivalSteamPuff != null) arrivalSteamPuff.Play();

                    // Open safety bar
                    targetSafetyBarY = safetyBarOpenY;
                    if (safetyBarRaiseSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(safetyBarRaiseSound, 0.8f);
                    }
                    break;

                case ElevatorState.CooldownWait:
                    break;
            }

            UpdateStatusLighting();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            stateTimer += dt;

            // Animate safety bar position
            if (!Mathf.Approximately(currentSafetyBarY, targetSafetyBarY))
            {
                currentSafetyBarY = Mathf.MoveTowards(currentSafetyBarY, targetSafetyBarY, safetyBarSpeed * dt);
                if (safetyBarTransform != null)
                {
                    Vector3 bp = safetyBarTransform.localPosition;
                    bp.y = currentSafetyBarY;
                    safetyBarTransform.localPosition = bp;
                }
            }

            UpdateSideExits();

            // Animate warning beacon strobe light when active
            AnimateWarningBeacon(dt);

            // In-cabin keyboard interaction. E only: Space is jump and W/Up climb, and
            // either one used to send the elevator off whenever the player jumped inside it.
            if (allowKeyboardInteraction && hasPassenger && state == ElevatorState.AtFloor)
            {
                bool interact = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
                try
                {
                    if (Input.GetKeyDown(KeyCode.E)) interact = true;
                }
                catch { }

                if (interact)
                {
                    ToggleFloor();
                }
            }

            switch (state)
            {
                case ElevatorState.PassengerBoardedDelay:
                    // Wait for safety bar to lower and pre-departure delay
                    bool barReady = (safetyBarTransform == null) || (Mathf.Abs(currentSafetyBarY - targetSafetyBarY) < 0.05f);
                    if (stateTimer >= preDepartureDelay && barReady)
                    {
                        SetState(ElevatorState.Moving);
                    }
                    else if (stateTimer >= preDepartureDelay + 1.0f)
                    {
                        SetState(ElevatorState.Moving);
                    }
                    break;

                case ElevatorState.Moving:
                    // Handled in FixedUpdate for rock-solid physics
                    break;

                case ElevatorState.ArrivedSettling:
                    if (stateTimer >= 0.45f)
                    {
                        SetState(ElevatorState.CooldownWait);
                    }
                    break;

                case ElevatorState.CooldownWait:
                    if (stateTimer >= floorArrivalCooldown)
                    {
                        // Check if player has stepped off
                        if (!hasPassenger)
                        {
                            SetState(ElevatorState.AtFloor);
                        }
                        else
                        {
                            // If player is still inside after cooldown + 2 seconds, ready to return if re-triggered
                            if (stateTimer >= floorArrivalCooldown + 2.5f)
                            {
                                SetState(ElevatorState.AtFloor);
                            }
                        }
                    }
                    break;
            }

            UpdateCablesAndCounterweight();
        }

        private void FixedUpdate()
        {
            // Active check for passengers
            CheckActivePassengerOverlap();

            if (state == ElevatorState.Moving)
            {
                // Smooth movement towards targetTravelProgress
                float dir = Mathf.Sign(targetTravelProgress - currentTravelProgress);

                // Smooth acceleration & deceleration curve
                float desiredSpeed = (moveSpeed / Mathf.Max(travelHeight, 0.01f));
                currentVelocity = Mathf.MoveTowards(currentVelocity, desiredSpeed * dir, accelerationRamp * Time.fixedDeltaTime);

                // Deceleration zone check
                float distToTarget = Mathf.Abs(targetTravelProgress - currentTravelProgress);
                float decelDist = 0.15f; // last 15% of travel decelerates
                if (distToTarget < decelDist)
                {
                    float factor = Mathf.Clamp01(distToTarget / decelDist);
                    currentVelocity = dir * Mathf.Max(desiredSpeed * factor, 0.1f * desiredSpeed);
                }

                currentTravelProgress += currentVelocity * Time.fixedDeltaTime;

                // Check arrival
                bool arrived = false;
                if (dir > 0 && currentTravelProgress >= targetTravelProgress)
                {
                    currentTravelProgress = targetTravelProgress;
                    arrived = true;
                }
                else if (dir < 0 && currentTravelProgress <= targetTravelProgress)
                {
                    currentTravelProgress = targetTravelProgress;
                    arrived = true;
                }

                // Update physical cabin position
                Vector3 newCabinWorldPos = Vector3.Lerp(bottomCabinWorldPos, topCabinWorldPos, currentTravelProgress);
                Vector3 deltaMove = PlaceCabin(newCabinWorldPos);

                // Move passengers with the elevator
                MovePassengers(deltaMove);

                previousCabinWorldPos = newCabinWorldPos;

                if (arrived)
                {
                    SetState(ElevatorState.ArrivedSettling);
                }
            }
            else if (state == ElevatorState.ArrivedSettling)
            {
                // Subtle mechanical settling bounce
                float bounce = Mathf.Sin(stateTimer * 28f) * Mathf.Exp(-stateTimer * 8f) * 0.04f;
                Vector3 basePos = (currentFloor == FloorIndex.Top) ? topCabinWorldPos : bottomCabinWorldPos;
                Vector3 bouncedPos = basePos + new Vector3(0f, bounce, 0f);
                Vector3 deltaMove = PlaceCabin(bouncedPos);

                MovePassengers(deltaMove);
                previousCabinWorldPos = bouncedPos;
            }
            else
            {
                previousCabinWorldPos = cabinTransform.position;
            }
        }

        /// <summary>
        /// Moves the cabin and pushes the move into the physics world at once, so the deck and
        /// railings are already where they belong when a passenger casts against them this step.
        /// Returns how far the cabin moved.
        /// </summary>
        private Vector3 PlaceCabin(Vector3 worldPos)
        {
            Vector3 deltaMove = worldPos - cabinTransform.position;
            cabinTransform.position = worldPos;
            Physics2D.SyncTransforms();
            return deltaMove;
        }

        /// <summary>
        /// Displaces all boarded passengers (including KinematicObject player) smoothly with the elevator.
        /// </summary>
        private void MovePassengers(Vector3 deltaMove)
        {
            if (deltaMove.sqrMagnitude < 0.0000001f) return;

            // 1. Kinematic Objects (e.g. PlayerController)
            for (int i = activeKinematicPassengers.Count - 1; i >= 0; i--)
            {
                var ko = activeKinematicPassengers[i];
                if (ko == null || !ko.gameObject.activeInHierarchy)
                {
                    activeKinematicPassengers.RemoveAt(i);
                    continue;
                }

                // Move passenger by deltaMove. Only the body: writing the transform as well left a
                // stale copy that the next physics sync pushed back onto the body, erasing whatever
                // the passenger moved by itself this step, so the player could not walk while riding.
                var rb = ko.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.position += (Vector2)deltaMove;
                }
                else
                {
                    ko.transform.position += deltaMove;
                }

                // If elevator is moving downwards, counteract gravity separation to keep player firmly grounded
                if (deltaMove.y < 0f && ko.velocity.y < 0f)
                {
                    ko.velocity.y = 0f;
                }
            }

            // 2. Dynamic Rigidbody2D objects
            for (int i = activeRigidbodyPassengers.Count - 1; i >= 0; i--)
            {
                var rb = activeRigidbodyPassengers[i];
                if (rb == null || !rb.gameObject.activeInHierarchy)
                {
                    activeRigidbodyPassengers.RemoveAt(i);
                    continue;
                }

                if (rb.bodyType == RigidbodyType2D.Dynamic)
                {
                    rb.position += (Vector2)deltaMove;
                }
            }
        }

        private void CacheSideRailingColliders()
        {
            var list = new List<Collider2D>();
            Transform cabin = cabinTransform != null ? cabinTransform : transform.Find("Cabin");
            if (cabin != null)
            {
                foreach (var name in new[] { "SideRailing_Left", "SideRailing_Right" })
                {
                    Transform rail = cabin.Find(name);
                    if (rail == null) continue;
                    foreach (var col in rail.GetComponents<Collider2D>())
                        if (!col.isTrigger) list.Add(col);
                }
            }
            sideRailingColliders = list.ToArray();
        }

        /// <summary>
        /// The side railings are only solid while the safety bar is down; once it is fully
        /// raised at a floor the passenger can walk straight off either side.
        /// </summary>
        private void UpdateSideExits()
        {
            bool doorOpen = openSidesWhenDoorOpen
                && state != ElevatorState.Moving
                && state != ElevatorState.PassengerBoardedDelay
                && Mathf.Abs(currentSafetyBarY - safetyBarOpenY) < 0.05f;

            for (int i = 0; i < sideRailingColliders.Length; i++)
            {
                var col = sideRailingColliders[i];
                if (col != null && col.enabled == doorOpen) col.enabled = !doorOpen;
            }
        }

        private void AnimateWarningBeacon(float dt)
        {
            if (state == ElevatorState.Moving || state == ElevatorState.PassengerBoardedDelay)
            {
                // Pulsing rotating hazard amber strobe
                float wave = Mathf.PingPong(Time.time * beaconStrobeSpeed, 1.0f);
                float strobe = Mathf.Pow(wave, 3f); // sharp flash
                float intensity = Mathf.Lerp(0.4f, beaconMaxIntensity, strobe);

                if (beaconLight != null)
                {
                    beaconLight.enabled = true;
                    beaconLight.intensity = intensity;
                    beaconLight.color = new Color(1.0f, 0.65f, 0.1f, 1.0f);
                }

                if (beaconRenderer != null)
                {
                    beaconRenderer.color = Color.Lerp(new Color(0.7f, 0.45f, 0.1f, 1f), Color.white, strobe);
                }
            }
            else
            {
                // Gentle standby glow when parked
                if (beaconLight != null)
                {
                    beaconLight.intensity = Mathf.Lerp(beaconLight.intensity, 0.3f, dt * 5f);
                }
                if (beaconRenderer != null)
                {
                    beaconRenderer.color = new Color(0.6f, 0.4f, 0.1f, 0.85f);
                }
            }
        }

        private void UpdateStatusLighting()
        {
            if (statusPanelLight == null) return;

            switch (state)
            {
                case ElevatorState.AtFloor:
                    statusPanelLight.color = readyStatusColor;
                    statusPanelLight.intensity = 1.0f;
                    break;
                case ElevatorState.PassengerBoardedDelay:
                case ElevatorState.ArrivedSettling:
                    statusPanelLight.color = movingStatusColor;
                    statusPanelLight.intensity = 1.5f;
                    break;
                case ElevatorState.Moving:
                    statusPanelLight.color = lockedStatusColor;
                    statusPanelLight.intensity = 1.8f;
                    break;
            }
        }

        /// <summary>
        /// Dynamically updates the elevator width, cable length, guide rails, and all child colliders.
        /// Can be called in Edit Mode or Runtime.
        /// </summary>
        [ContextMenu("Apply Dimensions")]
        public void ApplyDimensions()
        {
            if (travelHeight < 1.0f) travelHeight = 1.0f;
            if (elevatorWidth < 1.5f) elevatorWidth = 1.5f;
            if (cableLength < travelHeight) cableLength = travelHeight;

            float halfWidth = elevatorWidth * 0.5f;
            Transform cabin = cabinTransform != null ? cabinTransform : transform.Find("Cabin");

            if (cabin != null)
            {
                // 1. Platform Deck
                Transform deck = cabin.Find("Platform_Deck");
                if (deck != null)
                {
                    var sr = deck.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.drawMode = SpriteDrawMode.Sliced;
                        sr.size = new Vector2(elevatorWidth, 0.6f);
                    }

                    var col = deck.GetComponent<BoxCollider2D>();
                    if (col != null)
                    {
                        col.size = new Vector2(elevatorWidth, 0.25f);
                        col.offset = new Vector2(0f, -0.125f);
                    }
                }

                // 2. BackMesh
                Transform mesh = cabin.Find("BackMesh");
                if (mesh != null)
                {
                    var sr = mesh.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.drawMode = SpriteDrawMode.Tiled;
                        sr.size = new Vector2(elevatorWidth, 3.2f);
                    }
                }

                // 3. Side Railings
                Transform leftRail = cabin.Find("SideRailing_Left");
                if (leftRail != null)
                {
                    leftRail.localPosition = new Vector3(-halfWidth, 0f, 0f);
                }

                Transform rightRail = cabin.Find("SideRailing_Right");
                if (rightRail != null)
                {
                    rightRail.localPosition = new Vector3(halfWidth, 0f, 0f);
                }

                // 4. TopFrame
                Transform topFrame = cabin.Find("TopFrame");
                if (topFrame != null)
                {
                    topFrame.localPosition = new Vector3(0f, 3.2f, 0f);
                    var sr = topFrame.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.drawMode = SpriteDrawMode.Sliced;
                        sr.size = new Vector2(elevatorWidth + 0.2f, 0.7f);
                    }
                }

                // 5. Safety Bar
                Transform bar = safetyBarTransform != null ? safetyBarTransform : cabin.Find("SafetyBar");
                if (bar != null)
                {
                    var sr = bar.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.drawMode = SpriteDrawMode.Sliced;
                        sr.size = new Vector2(elevatorWidth - 0.4f, 0.36f);
                    }
                    // The bar is a front gate drawn over the passenger (order 7), not a wall. Its
                    // collider spanned the whole cage, and lowered for the ride it sat across the
                    // passenger's waist, wedging them in place so they could not walk while moving.
                    // The side railings already keep the passenger in.
                    foreach (var col in bar.GetComponents<Collider2D>())
                    {
                        col.enabled = false;
                    }
                }

                // 6. Control Panel
                Transform panel = cabin.Find("ControlPanel");
                if (panel != null)
                {
                    panel.localPosition = new Vector3(halfWidth - 0.25f, 1.25f, 0f);
                }

                // 7. Suspension Cables
                float cableSpacing = elevatorWidth * 0.52f;
                Transform cableL = cabin.Find("SuspensionCable_Left");
                if (cableL != null)
                {
                    cableL.localPosition = new Vector3(-cableSpacing * 0.5f, 3.4f, 0f);
                }
                Transform cableR = cabin.Find("SuspensionCable_Right");
                if (cableR != null)
                {
                    cableR.localPosition = new Vector3(cableSpacing * 0.5f, 3.4f, 0f);
                }

                // Refresh cableRenderers array if needed
                if (cableRenderers == null || cableRenderers.Length < 2 || cableRenderers[0] == null)
                {
                    var srL = cableL != null ? cableL.GetComponent<SpriteRenderer>() : null;
                    var srR = cableR != null ? cableR.GetComponent<SpriteRenderer>() : null;
                    if (srL != null && srR != null)
                    {
                        cableRenderers = new SpriteRenderer[] { srL, srR };
                    }
                }

                // 8. Passenger Detection Trigger
                Transform trigger = cabin.Find("PassengerCabinTrigger");
                if (trigger != null)
                {
                    var col = trigger.GetComponent<BoxCollider2D>();
                    if (col != null)
                    {
                        col.size = new Vector2(Mathf.Max(1.0f, elevatorWidth - 0.6f), 2.6f);
                        col.offset = new Vector2(0f, 1.35f);
                    }
                }
            }

            // 9. Guide Rails - from the bottom floor up to the ceiling anchor, not past it
            float totalTrackHeight = 3.2f + cableLength;
            Transform railLeft = transform.Find("GuideRail_Left");
            if (railLeft != null)
            {
                railLeft.localPosition = new Vector3(-halfWidth - 0.05f, 0f, 0f);
                var sr = railLeft.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.size = new Vector2(0.32f, totalTrackHeight);
                }
            }

            Transform railRight = transform.Find("GuideRail_Right");
            if (railRight != null)
            {
                railRight.localPosition = new Vector3(halfWidth + 0.05f, 0f, 0f);
                var sr = railRight.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.size = new Vector2(0.32f, totalTrackHeight);
                }
            }

            // 10. Counterweight
            Transform cw = counterweightTransform != null ? counterweightTransform : transform.Find("Counterweight");
            if (cw != null)
            {
                Vector3 cwPos = cw.localPosition;
                cwPos.x = halfWidth + 0.35f;
                cw.localPosition = cwPos;
            }

            // 11. Calculate Ceiling Anchor Y
            Vector3 baseWorldPos = Application.isPlaying ? bottomCabinWorldPos : transform.position;
            ceilingCableAnchorY = baseWorldPos.y + 3.2f + cableLength;

            // 12. Update cables and counterweight visual
            UpdateCablesAndCounterweight();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (travelHeight < 1.0f) travelHeight = 1.0f;
            if (elevatorWidth < 1.5f) elevatorWidth = 1.5f;
            if (cableLength < travelHeight) cableLength = travelHeight;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    ApplyDimensions();
                }
            };
        }
#endif

        public void UpdateCablesAndCounterweight()
        {
            float halfWidth = elevatorWidth * 0.5f;
            Vector3 bPos = Application.isPlaying ? bottomCabinWorldPos : transform.position;

            // 1. Update counterweight position (inversely proportional to travel progress)
            if (counterweightTransform != null)
            {
                float cwProgress = 1.0f - currentTravelProgress;
                float cwX = halfWidth + 0.35f;
                Vector3 cwBottom = bPos + new Vector3(cwX, travelHeight, 0f);
                Vector3 cwTop = bPos + new Vector3(cwX, 0f, 0f);
                counterweightTransform.position = Vector3.Lerp(cwBottom, cwTop, cwProgress);
            }

            // 2. Update suspension cables length
            if (cableRenderers != null && cableRenderers.Length > 0)
            {
                float anchorY = Application.isPlaying ? ceilingCableAnchorY : (bPos.y + 3.2f + cableLength);

                foreach (var cable in cableRenderers)
                {
                    if (cable != null)
                    {
                        // Measured from the cable's own attach point (above the top frame), so
                        // the cable ends exactly at the anchor instead of overshooting it.
                        Vector2 size = cable.size;
                        size.y = Mathf.Max(0f, anchorY - cable.transform.position.y);
                        cable.size = size;
                    }
                }
            }
        }

        /// <summary>
        /// Reads the level collision around the shaft: the top stop becomes the floor level
        /// beside the shaft near the current top stop, and the cable anchor (and guide rail
        /// top) becomes the ceiling right above that floor. Returns false if nothing was found.
        /// </summary>
        public bool FitToMap(out string report)
        {
            float bottomY = Application.isPlaying ? bottomCabinWorldPos.y : transform.position.y;
            float centerX = Application.isPlaying ? bottomCabinWorldPos.x : transform.position.x;
            float halfWorldWidth = elevatorWidth * 0.5f * Mathf.Abs(transform.lossyScale.x);
            float guessTopY = bottomY + travelHeight;

            // 1. Landing: floor surface just outside either side of the cabin.
            float landingY = float.NegativeInfinity;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 origin = new Vector2(centerX + side * (halfWorldWidth + 0.6f), guessTopY + 3f);
                if (IsLevelSolid(origin)) continue;
                if (RaycastLevel(origin, Vector2.down, 6f, out float hitY)) landingY = Mathf.Max(landingY, hitY);
            }
            if (float.IsNegativeInfinity(landingY))
            {
                report = $"현재 최상층(y {guessTopY:0.##}) 근처 ±3 안에서 양옆 바닥을 찾지 못했습니다. Travel Height를 대략 맞춘 뒤 다시 누르세요.";
                return false;
            }

            // 2. Ceiling straight above the landing, through the middle of the shaft.
            if (!RaycastLevel(new Vector2(centerX, landingY + 0.1f), Vector2.up, 30f, out float ceilingY))
            {
                report = $"최상층 바닥(y {landingY:0.##}) 위로 천장을 찾지 못했습니다.";
                return false;
            }

            travelHeight = landingY - bottomY;
            cableLength = Mathf.Max(travelHeight, ceilingY - (bottomY + 3.2f));
            ApplyDimensions();
            report = $"최상층 바닥 y {landingY:0.###} -> Travel Height {travelHeight:0.###} / 천장 y {ceilingY:0.###} -> Cable Length {cableLength:0.###}";
            return true;
        }

        private bool IsLevelSolid(Vector2 point)
        {
            foreach (var col in Physics2D.OverlapPointAll(point))
                if (IsLevelCollider(col)) return true;
            return false;
        }

        private bool RaycastLevel(Vector2 origin, Vector2 dir, float distance, out float hitY)
        {
            hitY = 0f;
            float best = float.PositiveInfinity;
            foreach (var hit in Physics2D.RaycastAll(origin, dir, distance))
            {
                if (!IsLevelCollider(hit.collider) || hit.distance <= 0f || hit.distance >= best) continue;
                best = hit.distance;
                hitY = hit.point.y;
            }
            return !float.IsPositiveInfinity(best);
        }

        private bool IsLevelCollider(Collider2D col)
        {
            if (col == null || col.isTrigger) return false;
            if (col.transform.IsChildOf(transform)) return false;
            var rb = col.attachedRigidbody;
            return rb == null || rb.bodyType == RigidbodyType2D.Static;
        }

        private void OnDrawGizmos()
        {
            Vector3 bPos = Application.isPlaying ? bottomCabinWorldPos : transform.position;
            Vector3 tPos = bPos + new Vector3(0f, travelHeight, 0f);
            float halfWidth = elevatorWidth * 0.5f;

            // Shaft guide rails
            Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.5f);
            Gizmos.DrawLine(bPos + new Vector3(-halfWidth, 0f, 0f), tPos + new Vector3(-halfWidth, 3.2f, 0f));
            Gizmos.DrawLine(bPos + new Vector3(halfWidth, 0f, 0f), tPos + new Vector3(halfWidth, 3.2f, 0f));

            // Bottom floor platform
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(bPos + new Vector3(0f, -0.125f, 0f), new Vector3(elevatorWidth, 0.25f, 0f));

            // Top floor platform
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(tPos + new Vector3(0f, -0.125f, 0f), new Vector3(elevatorWidth, 0.25f, 0f));

            // Ceiling Cable Anchor
            float anchorY = Application.isPlaying ? ceilingCableAnchorY : (bPos.y + 3.2f + cableLength);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(tPos + new Vector3(0f, 3.2f, 0f), new Vector3(bPos.x, anchorY, 0f));
            Gizmos.DrawWireSphere(new Vector3(bPos.x, anchorY, 0f), 0.3f);

            // Travel path line
            Gizmos.color = Color.white;
            Gizmos.DrawLine(bPos, tPos);
        }

        [ContextMenu("Test Board / Toggle Elevator")]
        public void TestBoard() => ToggleFloor();

        [ContextMenu("Test Call To Top")]
        public void TestCallTop() => CallElevatorToFloor(FloorIndex.Top);

        [ContextMenu("Test Call To Bottom")]
        public void TestCallBottom() => CallElevatorToFloor(FloorIndex.Bottom);
    }
}
