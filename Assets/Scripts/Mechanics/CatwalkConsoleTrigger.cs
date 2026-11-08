using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Platformer.Mechanics
{
    /// <summary>
    /// Interactive console located on the Laser Projector's catwalk bridge.
    /// When the player stands near the console and presses [E], it triggers the laser cannon sequence
    /// to lock-on and fire at the Bulkhead Gate's lock mechanism.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CatwalkConsoleTrigger : MonoBehaviour
    {
        [Header("Target Projector")]
        public LaserBeamProjectorController targetProjector;

        [Header("Interaction Settings")]
        public KeyCode interactKey = KeyCode.E;
        [Tooltip("Optional prompt object (e.g. TextMeshPro or Sprite prompt)")]
        public GameObject promptObject;
        public TextMeshPro promptTextMesh;

        [Header("Audio & Effects")]
        public AudioClip consolePressAudio;
        public ParticleSystem activateSparks;

        private bool playerInRange = false;
        private bool isBusy = false;

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            if (promptObject != null) promptObject.SetActive(false);
            if (promptTextMesh != null) promptTextMesh.gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null)
            {
                playerInRange = true;
                UpdatePromptVisibility(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null)
            {
                playerInRange = false;
                UpdatePromptVisibility(false);
            }
        }

        private void Update()
        {
            if (playerInRange && !isBusy)
            {
                bool interactPressed = false;

#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    interactPressed = true;
                }
#else
                if (Input.GetKeyDown(interactKey))
                {
                    interactPressed = true;
                }
#endif

                if (interactPressed)
                {
                    ActivateConsole();
                }
            }
        }

        public void ActivateConsole()
        {
            if (isBusy || targetProjector == null) return;
            isBusy = true;
            UpdatePromptVisibility(false);

            if (consolePressAudio != null)
            {
                AudioSource.PlayClipAtPoint(consolePressAudio, transform.position);
            }

            if (activateSparks != null)
            {
                activateSparks.Play();
            }

            Debug.Log($"[CatwalkConsole] Player activated laser cannon: {targetProjector.gameObject.name}!");

            targetProjector.StopAllCoroutines();
            StartCoroutine(TriggerProjectorRoutine());
        }

        private System.Collections.IEnumerator TriggerProjectorRoutine()
        {
            yield return targetProjector.StartCoroutine(targetProjector.FireSequenceRoutine());
            
            // Brief hold so player can enjoy seeing the lock destroyed
            yield return new WaitForSeconds(0.4f);

            if (Cinematics.LaserCinematicDirector.Instance != null)
            {
                Cinematics.LaserCinematicDirector.Instance.ReleaseProjector();
            }

            isBusy = false;
            if (playerInRange)
            {
                UpdatePromptVisibility(true);
            }
        }

        private void UpdatePromptVisibility(bool visible)
        {
            if (promptObject != null) promptObject.SetActive(visible);
            if (promptTextMesh != null)
            {
                promptTextMesh.gameObject.SetActive(visible);
                if (visible) promptTextMesh.text = "[E] FIRE CANNON";
            }
        }
    }
}
