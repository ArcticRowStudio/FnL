using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Interactive button for the Freight Elevator.
    /// Can be used as an on-board cabin button (to send elevator up/down) or as a floor call button.
    /// Supports:
    /// - New Input System (E key, Enter, Gamepad West)
    /// - Mouse click directly on the button in 2D view
    /// - Optional touch/walk-in trigger
    /// - Physical press depress & elastic bounce animation
    /// - Click audio and LED illumination
    /// - Floating interaction prompt UI
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ElevatorButton : MonoBehaviour
    {
        public enum ButtonAction
        {
            ToggleFloor,        // If at bottom, go up; if at top, go down
            GoUp,               // Explicitly send up / call to top
            GoDown,             // Explicitly send down / call to bottom
            CallToCurrentFloor  // Call to the floor this station is on
        }

        [Header("Target Elevator")]
        [Tooltip("The elevator this button controls. If left empty, automatically finds the nearest FreightElevatorController in the scene!")]
        public FreightElevatorController elevator;

        [Header("Action")]
        public ButtonAction action = ButtonAction.ToggleFloor;
        [Tooltip("If action is CallToCurrentFloor, specify which floor this button represents")]
        public FreightElevatorController.FloorIndex designatedFloor = FreightElevatorController.FloorIndex.Bottom;

        [Header("Interaction Settings")]
        [Tooltip("Distance threshold for player interaction (world units)")]
        public float interactionDistance = 2.5f;
        [Tooltip("Trigger button automatically when player touches/enters collider")]
        public bool triggerOnTouch = false;

        [Header("Visual Feedback")]
        public SpriteRenderer buttonRenderer;
        public Light2D buttonLight;
        public Color idleLightColor = new Color(0.2f, 1.0f, 0.4f, 1.0f);     // Green
        public Color pressedLightColor = new Color(1.0f, 0.8f, 0.2f, 1.0f);  // Bright Amber
        public float pressDepressDistance = 0.06f;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip clickSound;

        [Header("Runtime Info")]
        [SerializeField] private bool isPlayerInRange = false;

        private Vector3 initialLocalPos;
        private Coroutine pressCoroutine;
        private Transform playerTransform;

        public bool IsPlayerInRange => isPlayerInRange;

        private void Awake()
        {
            initialLocalPos = transform.localPosition;
            if (buttonRenderer == null) buttonRenderer = GetComponent<SpriteRenderer>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (buttonLight == null) buttonLight = GetComponentInChildren<Light2D>();
        }

        private void Start()
        {
            // Auto-find nearest elevator if not assigned!
            if (elevator == null)
            {
                elevator = FindNearestElevator();
            }

            // Find player transform
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null) playerTransform = player.transform;

            UpdateLighting();
        }

        private FreightElevatorController FindNearestElevator()
        {
            var all = FindObjectsByType<FreightElevatorController>();
            if (all == null || all.Length == 0) return null;

            FreightElevatorController best = all[0];
            float bestDist = Vector3.Distance(transform.position, best.transform.position);

            for (int i = 1; i < all.Length; i++)
            {
                float d = Vector3.Distance(transform.position, all[i].transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = all[i];
                }
            }
            return best;
        }

        private void Update()
        {
            if (elevator == null)
            {
                elevator = FindNearestElevator();
            }

            // 1. Check if player is on board or near the button
            if (playerTransform == null)
            {
                var player = FindAnyObjectByType<PlayerController>();
                if (player != null) playerTransform = player.transform;
            }

            if (elevator != null && elevator.HasPassenger)
            {
                isPlayerInRange = true;
            }
            else if (playerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, playerTransform.position);
                isPlayerInRange = dist <= interactionDistance;
            }
            else
            {
                isPlayerInRange = false;
            }

            // 2. Check Input if player is in range
            if (isPlayerInRange && IsInteractInputPressed())
            {
                Press();
            }

            UpdateLighting();
        }

        private bool IsInteractInputPressed()
        {
            // Keyboard (New Input System). Space (jump) and W/Up (climb) are left out on
            // purpose: they pressed the button every time the player jumped on board.
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame ||
                    Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            // Gamepad (New Input System); South is jump, so the interact button is West.
            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonWest.wasPressedThisFrame)
                {
                    return true;
                }
            }

            // Fallback Legacy Input
            try
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private void OnMouseDown()
        {
            // Allows player to click on the button with mouse in Game view!
            Press();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger) return;
            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
            {
                isPlayerInRange = true;
                if (triggerOnTouch)
                {
                    Press();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
            {
                isPlayerInRange = false;
            }
        }

        [ContextMenu("Press Button")]
        public void Press()
        {
            if (elevator == null)
            {
                elevator = FindNearestElevator();
            }

            if (elevator == null)
            {
                Debug.LogWarning("[ElevatorButton] No FreightElevator found in scene to operate!");
                return;
            }

            // Visual tactile depression animation
            if (pressCoroutine != null) StopCoroutine(pressCoroutine);
            pressCoroutine = StartCoroutine(PressAnimationRoutine());

            // Audio
            if (clickSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(clickSound, 1.0f);
            }

            // Execute Elevator command
            switch (action)
            {
                case ButtonAction.ToggleFloor:
                    elevator.ToggleFloor();
                    break;
                case ButtonAction.GoUp:
                    elevator.CallElevatorToFloor(FreightElevatorController.FloorIndex.Top);
                    break;
                case ButtonAction.GoDown:
                    elevator.CallElevatorToFloor(FreightElevatorController.FloorIndex.Bottom);
                    break;
                case ButtonAction.CallToCurrentFloor:
                    elevator.CallElevatorToFloor(designatedFloor);
                    break;
            }
        }

        private IEnumerator PressAnimationRoutine()
        {
            // 1. Depress inward
            float elapsed = 0f;
            float duration = 0.06f;
            Vector3 targetPos = initialLocalPos + new Vector3(0f, -pressDepressDistance, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localPosition = Vector3.Lerp(initialLocalPos, targetPos, t);
                yield return null;
            }

            // 2. Rebound pop
            elapsed = 0f;
            float reboundDuration = 0.10f;
            while (elapsed < reboundDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / reboundDuration);
                transform.localPosition = Vector3.Lerp(targetPos, initialLocalPos, t);
                yield return null;
            }

            transform.localPosition = initialLocalPos;
            pressCoroutine = null;
        }

        private void UpdateLighting()
        {
            if (buttonLight == null) return;

            if (elevator != null && (elevator.State == FreightElevatorController.ElevatorState.Moving ||
                                     elevator.State == FreightElevatorController.ElevatorState.PassengerBoardedDelay))
            {
                buttonLight.color = pressedLightColor;
                buttonLight.intensity = 2.0f;
            }
            else
            {
                buttonLight.color = isPlayerInRange ? Color.white : idleLightColor;
                buttonLight.intensity = isPlayerInRange ? 1.5f : 0.8f;
            }
        }

        private void OnGUI()
        {
            // Draw floating prompt in Game view when player is in interaction range
            if (isPlayerInRange && Camera.main != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + new Vector3(0f, 0.6f, 0f));
                if (screenPos.z > 0)
                {
                    float width = 120f;
                    float height = 28f;
                    Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

                    string promptText = (action == ButtonAction.CallToCurrentFloor) ? "[E] 호출" : "[E] 승강기 작동";
                    
                    GUI.backgroundColor = new Color(0.1f, 0.15f, 0.2f, 0.85f);
                    GUI.Box(rect, GUIContent.none);

                    GUIStyle textStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        fontStyle = FontStyle.Bold
                    };
                    textStyle.normal.textColor = Color.yellow;
                    GUI.Label(rect, promptText, textStyle);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionDistance);
        }
    }
}
