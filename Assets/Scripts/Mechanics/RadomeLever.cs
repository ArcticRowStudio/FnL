using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Floor lever that opens the X-band radome. With the player in reach, the interact input
    /// (E / Enter / Gamepad West, same as the elevator buttons) swings the handle over and then
    /// starts the clamshell shutters. Pulling it back closes them again unless
    /// <see cref="canClose"/> is off.
    /// Opening cuts to <see cref="revealCamera"/>, a wide shot of the whole radome, and holds the
    /// player still until the shells have finished and the shot has been held.
    /// </summary>
    public class RadomeLever : MonoBehaviour
    {
        [Header("Target")]
        public RadomeShutterController shutter;

        [Header("Handle")]
        [Tooltip("Pivots at the bottom of the slot in the base")]
        public Transform handle;
        [Tooltip("Handle angle while the radome is closed (degrees, + leans left)")]
        public float closedAngle = 40f;
        [Tooltip("Handle angle once pulled (radome open)")]
        public float openAngle = -40f;
        public float pullDuration = 0.35f;

        [Header("Interaction")]
        [Tooltip("Distance threshold for player interaction (world units)")]
        public float interactionDistance = 2.5f;
        [Tooltip("Allow pulling the lever back to close the radome")]
        public bool canClose = true;

        [Header("Indicator")]
        public SpriteRenderer lamp;
        public Light2D lampLight;
        public Color closedColor = new Color(1.0f, 0.25f, 0.2f, 1.0f);  // Red
        public Color openColor = new Color(0.2f, 1.0f, 0.4f, 1.0f);     // Green

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip pullSound;

        [Header("Reveal Shot (opening only)")]
        [Tooltip("Wide shot framing the whole radome. Leave empty to open without a camera cut")]
        public CinemachineCamera revealCamera;
        public int revealPriority = 40;
        public int inactivePriority = 0;
        [Tooltip("Seconds the camera gets to pull out before the shells start moving")]
        public float shotLeadTime = 1.5f;
        [Tooltip("Seconds the open radome stays in shot before returning to the player")]
        public float shotHoldTime = 1.5f;
        [Tooltip("Freeze player input while the wide shot is up")]
        public bool lockPlayerDuringShot = true;

        [Header("Opening Shake (world units, sized for the wide shot)")]
        [Tooltip("Steady rumble while the shells swing")]
        public float rumbleAmplitude = 0.3f;
        [Tooltip("Kick when the shells unlock and again when they seat at the bottom")]
        public float joltAmplitude = 1.2f;
        [Tooltip("Seconds a jolt takes to die away")]
        public float joltDecay = 0.5f;
        public float shakeFrequency = 18f;

        [Header("Runtime Info")]
        [SerializeField] private bool isPlayerInRange = false;

        private bool isBusy;
        private PlayerController player;
        private Transform playerTransform;

        private void Awake()
        {
            // Play mode reuses component instances (domain/scene reload are off), so a lever
            // stopped mid-pull would otherwise stay busy in every later session.
            isBusy = false;
            isPlayerInRange = false;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player != null) playerTransform = player.transform;

            bool open = shutter != null && shutter.IsOpen;
            SetHandleAngle(open ? openAngle : closedAngle);
            UpdateLamp(open);
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                player = FindAnyObjectByType<PlayerController>();
                if (player != null) playerTransform = player.transform;
            }

            isPlayerInRange = playerTransform != null &&
                              Vector2.Distance(transform.position, playerTransform.position) <= interactionDistance;

            if (isPlayerInRange && CanPull && IsInteractInputPressed())
            {
                Pull();
            }
        }

        private bool CanPull => !isBusy && shutter != null && (canClose || !shutter.IsOpen);

        private bool IsInteractInputPressed()
        {
            if (Keyboard.current != null &&
                (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
            {
                return true;
            }

            // South is jump, so the interact button is West.
            if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        [ContextMenu("Pull Lever")]
        public void Pull()
        {
            if (!Application.isPlaying || !CanPull) return;
            StartCoroutine(PullRoutine(!shutter.IsOpen));
        }

        private IEnumerator PullRoutine(bool open)
        {
            isBusy = true;

            bool reveal = open && revealCamera != null;
            if (reveal) BeginRevealShot();

            if (pullSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(pullSound, 1.0f);
            }

            float from = handle != null ? Mathf.DeltaAngle(0f, handle.localEulerAngles.z) : 0f;
            float to = open ? openAngle : closedAngle;
            float elapsed = 0f;

            while (elapsed < pullDuration)
            {
                elapsed += Time.deltaTime;
                SetHandleAngle(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / pullDuration)));
                yield return null;
            }

            SetHandleAngle(to);
            UpdateLamp(open);

            if (reveal) yield return new WaitForSeconds(shotLeadTime);

            shutter.SetState(open);

            if (reveal)
            {
                yield return ShakeWhileOpening(revealCamera.transform);
                yield return new WaitForSeconds(Mathf.Max(0f, shotHoldTime - joltDecay));
                yield return EndRevealShot();
            }
            else if (open)
            {
                // No wide shot: shake the follow camera instead, at its own (close) scale.
                CameraZoomController.TriggerShake(0.25f, shutter.TransitionDuration);
            }

            if (open)
            {
                var boss = FindAnyObjectByType<Platformer.Mechanics.Boss.VtolBossAI>();
                if (boss != null)
                {
                    boss.TriggerEncounterFromSwitch();
                }
            }

            isBusy = false;
        }

        /// <summary>
        /// Rumbles the wide-shot camera for as long as the shells move, with a jolt as they
        /// unlock and another as they seat. The camera has no body component, so Cinemachine
        /// takes its pose straight from this transform.
        /// </summary>
        private IEnumerator ShakeWhileOpening(Transform cam)
        {
            // The seam lamps turn green first; the jolt belongs to the moment the shells move.
            while (shutter.IsUnlocking) yield return null;

            Vector3 basePos = cam.position;
            float sinceStart = 0f;

            while (shutter.IsMoving)
            {
                sinceStart += Time.deltaTime;
                ApplyShake(cam, basePos, rumbleAmplitude + joltAmplitude * JoltEnvelope(sinceStart));
                yield return null;
            }

            float sinceStop = 0f;
            while (sinceStop < joltDecay)
            {
                sinceStop += Time.deltaTime;
                ApplyShake(cam, basePos, joltAmplitude * JoltEnvelope(sinceStop));
                yield return null;
            }

            cam.position = basePos;
        }

        private float JoltEnvelope(float t)
        {
            float k = 1f - Mathf.Clamp01(t / Mathf.Max(joltDecay, 0.0001f));
            return k * k;
        }

        private void ApplyShake(Transform cam, Vector3 basePos, float amplitude)
        {
            float t = Time.time * shakeFrequency;
            var offset = new Vector3(Mathf.PerlinNoise(t, 0.37f) - 0.5f, Mathf.PerlinNoise(0.71f, t) - 0.5f, 0f);
            cam.position = basePos + offset * (2f * amplitude);
        }

        private void BeginRevealShot()
        {
            // The radome is far wider than any pixel perfect preset, so hand the lens to
            // Cinemachine for the duration, as the veranda cinematic does.
            CameraZoomController.IsCinematicActive = true;
            var ppc = Camera.main != null ? Camera.main.GetComponent<PixelPerfectCamera>() : null;
            if (ppc != null) ppc.enabled = false;

            SetRevealPriority(revealPriority);
            if (lockPlayerDuringShot && player != null) player.controlEnabled = false;
        }

        private IEnumerator EndRevealShot()
        {
            SetRevealPriority(inactivePriority);
            if (lockPlayerDuringShot && player != null) player.controlEnabled = true;

            // Pixel perfect would snap the lens back to the close preset mid-blend, so give the
            // camera back only once the blend home has finished.
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            yield return null;
            while (brain != null && brain.IsBlending) yield return null;

            CameraZoomController.IsCinematicActive = false;
        }

        private void SetRevealPriority(int priority)
        {
            var p = revealCamera.Priority;
            p.Value = priority;
            revealCamera.Priority = p;
        }

        private void SetHandleAngle(float angle)
        {
            if (handle != null) handle.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void UpdateLamp(bool open)
        {
            Color c = open ? openColor : closedColor;
            if (lamp != null) lamp.color = c;
            if (lampLight != null) lampLight.color = c;
        }

        private void OnGUI()
        {
            // Floating prompt in Game view, styled like ElevatorButton's.
            if (!isPlayerInRange || !CanPull || Camera.main == null) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + new Vector3(0f, 1.6f, 0f));
            if (screenPos.z <= 0) return;

            float width = 120f;
            float height = 28f;
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
            GUI.Label(rect, shutter.IsOpen ? "[E] 레버 올리기" : "[E] 레버 당기기", textStyle);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionDistance);
        }
    }
}
