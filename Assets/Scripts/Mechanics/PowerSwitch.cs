using Platformer.Core;
using Platformer.Model;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Platformer.Mechanics
{
    /// <summary>
    /// A one-shot wall switch. Stand in its trigger and press E (Enter / gamepad West) to throw it:
    /// it lights up, shows a short label and raises <see cref="onActivated"/>, which the level wires
    /// to whatever it unlocks (a boss, barriers).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PowerSwitch : MonoBehaviour
    {
        [Header("Text")]
        public string promptText = "[E] RESTORE POWER";
        public string activatedText = "POWER RESTORED";

        [Header("Look")]
        public SpriteRenderer switchRenderer;
        public Sprite offSprite;
        public Sprite onSprite;
        public Color offTint = new Color(1f, 0.45f, 0.35f, 1f);
        public Color onTint = Color.white;
        public Light2D switchLight;
        public AudioClip activateAudio;

        [Header("Result")]
        public UnityEvent onActivated;

        public bool Activated { get; private set; }

        TextMeshPro prompt;
        bool playerInside;
        float pulse;

        void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
            Activated = false;
            playerInside = false;
            BuildPrompt();
            ApplyLook();
        }

        void BuildPrompt()
        {
            var existing = transform.Find("Prompt");
            if (existing != null) Destroy(existing.gameObject);
            var go = new GameObject("Prompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            prompt = go.AddComponent<TextMeshPro>();
            prompt.text = promptText;
            prompt.fontSize = 3.5f;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.color = new Color(1f, 0.85f, 0.4f, 1f);
            prompt.sortingOrder = 500;
            go.SetActive(false);
        }

        void ApplyLook()
        {
            if (switchRenderer != null)
            {
                var s = Activated ? onSprite : offSprite;
                if (s != null) switchRenderer.sprite = s;
                switchRenderer.color = Activated ? onTint : offTint;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null) playerInside = true;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null) playerInside = false;
        }

        void Update()
        {
            if (prompt != null) prompt.gameObject.SetActive(playerInside && !Activated);

            if (switchLight != null)
            {
                pulse += Time.deltaTime * 3f;
                switchLight.intensity = Activated ? 2.2f : 0.6f + 0.4f * Mathf.Sin(pulse);
            }

            if (Activated || !playerInside || Platformer.UI.GameMenuController.IsOpen) return;
            var player = Simulation.GetModel<PlatformerModel>().player;
            if (player == null || !player.controlEnabled) return;
            if (InteractPressed()) Activate();
        }

        static bool InteractPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            return pad != null && pad.buttonWest.wasPressedThisFrame;
        }

        /// <summary>Throws the switch. Does nothing the second time.</summary>
        public void Activate()
        {
            if (Activated) return;
            Activated = true;
            ApplyLook();
            var player = Simulation.GetModel<PlatformerModel>().player;
            if (activateAudio != null && player != null && player.audioSource != null)
                player.audioSource.PlayOneShot(activateAudio);
            CheckpointLabel.Spawn(transform.position + Vector3.up * 2.6f, activatedText, new Color(1f, 0.8f, 0.35f, 1f));
            CameraZoomController.TriggerShake(0.25f, 0.2f);
            onActivated?.Invoke();
        }
    }
}
