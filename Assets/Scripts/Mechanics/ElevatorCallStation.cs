using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Interactive Floor Call Station for the Freight Elevator.
    /// Can be placed at any floor. When player approaches and presses interaction key (E or Up/Down),
    /// or enters trigger, summons the elevator to this station.
    /// Supports:
    /// - New Input System (E key, W/Up key, Gamepad)
    /// - Direct mouse click
    /// - Auto-find nearest elevator if not assigned in Inspector
    /// - Floating interaction prompt UI
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ElevatorCallStation : MonoBehaviour
    {
        [Tooltip("Target elevator. If left empty, automatically searches for the nearest FreightElevator in the scene!")]
        public FreightElevatorController elevator;
        public FreightElevatorController.FloorIndex designatedFloor;

        [Header("Call Button Visuals")]
        public SpriteRenderer buttonRenderer;
        public Light2D callButtonLight;
        public Color standbyColor = new Color(0.3f, 0.7f, 1.0f, 1.0f);
        public Color activeCallColor = new Color(1.0f, 0.75f, 0.1f, 1.0f);

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip callPressSound;

        [Header("Interaction Settings")]
        public KeyCode interactKey = KeyCode.E;
        public bool triggerOnTouch = false;
        public float interactionDistance = 2.5f;

        [Header("Runtime Info")]
        [SerializeField] private bool playerInRange = false;

        private Transform playerTransform;

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (buttonRenderer == null) buttonRenderer = GetComponent<SpriteRenderer>();
            if (callButtonLight == null) callButtonLight = GetComponentInChildren<Light2D>();
        }

        private void Start()
        {
            if (elevator == null)
            {
                elevator = FindNearestElevator();
            }

            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) playerTransform = pc.transform;

            UpdateButtonState();
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger) return;
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
            {
                playerInRange = true;
                if (triggerOnTouch)
                {
                    CallElevator();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
            {
                playerInRange = false;
            }
        }

        private void Update()
        {
            // Proximity check fallback
            if (playerTransform == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }

            if (playerTransform != null)
            {
                float d = Vector2.Distance(transform.position, playerTransform.position);
                playerInRange = d <= interactionDistance;
            }

            // Check Input
            if (playerInRange && IsInteractPressed())
            {
                CallElevator();
            }

            UpdateButtonState();
        }

        private bool IsInteractPressed()
        {
            // 1. New Input System Keyboard
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame ||
                    Keyboard.current.wKey.wasPressedThisFrame ||
                    Keyboard.current.upArrowKey.wasPressedThisFrame ||
                    Keyboard.current.spaceKey.wasPressedThisFrame ||
                    Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            // 2. Gamepad
            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                    Gamepad.current.dpad.up.wasPressedThisFrame)
                {
                    return true;
                }
            }

            // 3. Fallback Legacy
            try
            {
                if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private void OnMouseDown()
        {
            CallElevator();
        }

        [ContextMenu("Call Elevator")]
        public void CallElevator()
        {
            if (elevator == null)
            {
                elevator = FindNearestElevator();
            }

            if (elevator == null)
            {
                Debug.LogWarning("[ElevatorCallStation] No FreightElevator found in scene!");
                return;
            }

            if (callPressSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(callPressSound, 1.0f);
            }

            elevator.CallElevatorToFloor(designatedFloor);
            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            if (elevator == null) return;

            bool isCalledHere = (elevator.State == FreightElevatorController.ElevatorState.Moving || 
                                 elevator.State == FreightElevatorController.ElevatorState.PassengerBoardedDelay) &&
                                 elevator.CurrentFloor != designatedFloor;

            Color targetColor = isCalledHere ? activeCallColor : standbyColor;

            if (callButtonLight != null)
            {
                callButtonLight.color = targetColor;
                callButtonLight.intensity = isCalledHere ? 2.0f : (playerInRange ? 1.5f : 0.8f);
            }

            if (buttonRenderer != null)
            {
                buttonRenderer.color = isCalledHere ? Color.white : (playerInRange ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f));
            }
        }

        private void OnGUI()
        {
            if (playerInRange && Camera.main != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + new Vector3(0f, 0.9f, 0f));
                if (screenPos.z > 0)
                {
                    float width = 110f;
                    float height = 26f;
                    Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

                    GUI.backgroundColor = new Color(0.1f, 0.15f, 0.2f, 0.85f);
                    GUI.Box(rect, GUIContent.none);

                    GUIStyle textStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        fontStyle = FontStyle.Bold
                    };
                    textStyle.normal.textColor = Color.yellow;
                    GUI.Label(rect, "[E] 승강기 호출", textStyle);
                }
            }
        }
    }
}
