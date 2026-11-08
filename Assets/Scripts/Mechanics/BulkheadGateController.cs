using System.Collections;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Controls the massive industrial blast bulkhead gate and its dual laser locking mechanisms.
    /// Destroying both the left lock (via Projector 1) and right lock (via Projector 2)
    /// triggers the set-piece sequence: alarm, steam release, screen shake, and hydraulic split-opening.
    /// </summary>
    public class BulkheadGateController : MonoBehaviour
    {
        [Header("Door Transforms")]
        [Tooltip("Upper blast door slab that slides upward into the ceiling girder")]
        public Transform upperDoor;
        [Tooltip("Lower blast door slab that slides downward into the floor trench")]
        public Transform lowerDoor;
        [Tooltip("Solid blocker collider preventing player passage while gate is closed")]
        public Collider2D gateBlockerCollider;

        [Header("Dual Laser Lock Mechanisms")]
        public BulkheadLock leftLock;
        public BulkheadLock rightLock;

        [Header("Gate Animation Settings")]
        [Tooltip("Distance the upper and lower doors slide to fully open (units)")]
        public float slideDistance = 7.5f;
        [Tooltip("Time in seconds for the hydraulic doors to complete opening")]
        public float openDuration = 2.2f;

        [Header("Effects & Audio")]
        [Tooltip("Particle system for steam/spark release during gate opening")]
        public ParticleSystem steamParticles;
        [Tooltip("Audio clip for high pressure steam release when seal breaks")]
        public AudioClip steamReleaseAudio;
        [Tooltip("Audio clip for alarm siren / unlock sound")]
        public AudioClip unlockSirenAudio;
        [Tooltip("Audio clip for heavy hydraulic door grinding sound")]
        public AudioClip doorSlideAudio;

        [Header("Lock Requirements")]
        [Tooltip("If true, requires destroying both left and right locks to open. If false, destroying either lock opens the gate.")]
        public bool requireBothLocks = false;

        // State
        public bool IsGateOpen { get; private set; } = false;
        public bool IsOpening => isOpening;
        private bool isOpening = false;
        private Vector3 initialUpperPos;
        private Vector3 initialLowerPos;

        private void Awake()
        {
            if (upperDoor != null) initialUpperPos = upperDoor.localPosition;
            if (lowerDoor != null) initialLowerPos = lowerDoor.localPosition;

            if (leftLock != null) leftLock.Initialize(this, true);
            if (rightLock != null) rightLock.Initialize(this, false);
        }

        private void Start()
        {
            SetGateClosedInstant();
        }

        public void NotifyLockDamaged(BulkheadLock lockTarget, float currentHealth)
        {
            // Optional lock damage feedback
        }

        public void NotifyLockDestroyed(BulkheadLock lockTarget)
        {
            Debug.Log($"[BulkheadGate] Lock destroyed: {(lockTarget.isLeft ? "LEFT" : "RIGHT")}");

            bool leftDone = (leftLock == null || leftLock.isDestroyed);
            bool rightDone = (rightLock == null || rightLock.isDestroyed);

            bool shouldOpen = requireBothLocks ? (leftDone && rightDone) : (leftDone || rightDone);

            if (shouldOpen && !IsGateOpen && !isOpening)
            {
                StartCoroutine(OpenGateRoutine());
            }
        }

        public IEnumerator OpenGateRoutine()
        {
            isOpening = true;
            Debug.Log("[BulkheadGate] Both locks destroyed! Initiating hydraulic gate opening sequence!");

            // Trigger cinematic wide shot of the Bulkhead Gate
            if (Cinematics.LaserCinematicDirector.Instance != null)
            {
                Cinematics.LaserCinematicDirector.Instance.FocusBulkheadGate();
            }

            // 1. Pre-open alarm / explosive steam seal breach
            if (unlockSirenAudio != null)
            {
                AudioSource.PlayClipAtPoint(unlockSirenAudio, transform.position);
            }
            if (steamReleaseAudio != null)
            {
                AudioSource.PlayClipAtPoint(steamReleaseAudio, transform.position);
            }
            if (steamParticles != null)
            {
                var em = steamParticles.emission;
                em.enabled = true;
                steamParticles.Play(true);
            }

            yield return new WaitForSeconds(0.4f);

            if (doorSlideAudio != null)
            {
                AudioSource.PlayClipAtPoint(doorSlideAudio, transform.position);
            }

            // 2. Hydraulic Slide Open Animation
            float elapsed = 0f;
            Vector3 targetUpper = initialUpperPos + Vector3.up * slideDistance;
            Vector3 targetLower = initialLowerPos + Vector3.down * slideDistance;

            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / openDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                // Small industrial shudder / vibration
                float shudder = (t < 0.9f) ? Mathf.Sin(elapsed * 45f) * 0.04f * (1f - t) : 0f;

                if (upperDoor != null)
                {
                    upperDoor.localPosition = Vector3.Lerp(initialUpperPos, targetUpper, ease) + new Vector3(0f, shudder, 0f);
                }

                if (lowerDoor != null)
                {
                    lowerDoor.localPosition = Vector3.Lerp(initialLowerPos, targetLower, ease) + new Vector3(0f, -shudder, 0f);
                }

                // Disable solid blocker collider midway through opening
                if (t >= 0.5f && gateBlockerCollider != null && gateBlockerCollider.enabled)
                {
                    gateBlockerCollider.enabled = false;
                }

                yield return null;
            }

            if (upperDoor != null) upperDoor.localPosition = targetUpper;
            if (lowerDoor != null) lowerDoor.localPosition = targetLower;
            if (gateBlockerCollider != null) gateBlockerCollider.enabled = false;

            // Stop emitting fresh steam once doors are fully open; existing steam gently dissipates
            if (steamParticles != null)
            {
                var em = steamParticles.emission;
                em.enabled = false;
            }

            IsGateOpen = true;
            isOpening = false;
            Debug.Log("[BulkheadGate] Gate fully open! Central passageway unlocked!");

            // Hold wide shot briefly so player can see the fully open corridor
            yield return new WaitForSeconds(0.8f);

            if (Cinematics.LaserCinematicDirector.Instance != null)
            {
                Cinematics.LaserCinematicDirector.Instance.ReleaseBulkheadGate();
            }
        }

        public void SetGateClosedInstant()
        {
            IsGateOpen = false;
            isOpening = false;
            if (upperDoor != null) upperDoor.localPosition = initialUpperPos;
            if (lowerDoor != null) lowerDoor.localPosition = initialLowerPos;
            if (gateBlockerCollider != null) gateBlockerCollider.enabled = true;
            if (steamParticles != null)
            {
                var em = steamParticles.emission;
                em.enabled = false;
                steamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        [ContextMenu("Test Open Gate")]
        public void TestOpenGate()
        {
            if (leftLock != null) leftLock.DestroyLockInstant();
            if (rightLock != null) rightLock.DestroyLockInstant();
            if (Application.isPlaying)
            {
                StartCoroutine(OpenGateRoutine());
            }
            else
            {
                if (upperDoor != null) upperDoor.localPosition = initialUpperPos + Vector3.up * slideDistance;
                if (lowerDoor != null) lowerDoor.localPosition = initialLowerPos + Vector3.down * slideDistance;
                if (gateBlockerCollider != null) gateBlockerCollider.enabled = false;
                IsGateOpen = true;
            }
        }

        [ContextMenu("Test Reset Gate")]
        public void TestResetGate()
        {
            SetGateClosedInstant();
            if (leftLock != null) leftLock.ResetLockInstant();
            if (rightLock != null) rightLock.ResetLockInstant();
        }
    }
}
