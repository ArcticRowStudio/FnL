using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Animates the illuminated green tactile button/switch on the lower housing of the Laser Beam Projector.
    /// States:
    /// 1. Standby: Calm rhythmic breathing glow indicating ready status.
    /// 2. Pressed: Physical tactile depress animation with mechanical click sound.
    /// 3. Arming / Charging Strobe: Rapid high-frequency pulsation during cartridge reload.
    /// 4. Firing Overcharge: Blinding white-green overcharge with electrical hum jitter during beam discharge.
    /// 5. Cooldown: Smooth fade back to idle breathing standby.
    /// </summary>
    [ExecuteAlways]
    public class LaserProjectorButtonController : MonoBehaviour
    {
        [Header("Renderers & Lighting")]
        public SpriteRenderer buttonRenderer;
        public Light2D buttonLight;
        public AudioSource audioSource;

        [Header("Sprites for States")]
        public Sprite standbySprite;
        public Sprite litSprite;
        public Sprite overchargeSprite;

        [Header("Audio")]
        public AudioClip clickAudio;

        [Header("Motion & Glow Settings")]
        public float breathingSpeed = 2.4f;
        public float standbyLightMin = 0.5f;
        public float standbyLightMax = 1.0f;
        public float pressedLightIntensity = 2.4f;
        public float firingLightIntensity = 3.8f;
        public float pressDepressDistance = 0.08f;

        public enum ButtonState
        {
            Standby,
            Pressed,
            ChargingStrobe,
            FiringOvercharge,
            Cooldown
        }

        [Header("Current State")]
        [SerializeField]
        private ButtonState currentState = ButtonState.Standby;
        public ButtonState State => currentState;

        private Vector3 initialLocalPos;
        private Vector3 initialScale;
        private Coroutine activeStateCoroutine;

        private void Awake()
        {
            initialLocalPos = transform.localPosition;
            initialScale = transform.localScale;

            if (buttonRenderer == null) buttonRenderer = GetComponent<SpriteRenderer>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            SetState(ButtonState.Standby);
        }

        private void Update()
        {
            // Update breathing glow during Standby state
            if (currentState == ButtonState.Standby)
            {
                float t = (Mathf.Sin(Time.time * breathingSpeed) + 1f) * 0.5f;
                if (buttonLight != null)
                {
                    buttonLight.intensity = Mathf.Lerp(standbyLightMin, standbyLightMax, t);
                }
                if (buttonRenderer != null)
                {
                    float alpha = Mathf.Lerp(0.85f, 1.0f, t);
                    Color c = buttonRenderer.color;
                    c.a = alpha;
                    buttonRenderer.color = c;
                }
            }
            else if (currentState == ButtonState.FiringOvercharge)
            {
                // Micro-jitter during firing beam overcharge
                if (buttonLight != null)
                {
                    buttonLight.intensity = firingLightIntensity + Random.Range(-0.35f, 0.35f);
                }
            }
        }

        public void SetState(ButtonState state)
        {
            currentState = state;
            if (activeStateCoroutine != null)
            {
                StopCoroutine(activeStateCoroutine);
                activeStateCoroutine = null;
            }

            switch (state)
            {
                case ButtonState.Standby:
                    activeStateCoroutine = StartCoroutine(StandbyRoutine());
                    break;
                case ButtonState.Pressed:
                    activeStateCoroutine = StartCoroutine(PressRoutine());
                    break;
                case ButtonState.ChargingStrobe:
                    activeStateCoroutine = StartCoroutine(ChargingStrobeRoutine());
                    break;
                case ButtonState.FiringOvercharge:
                    activeStateCoroutine = StartCoroutine(FiringOverchargeRoutine());
                    break;
                case ButtonState.Cooldown:
                    activeStateCoroutine = StartCoroutine(CooldownRoutine());
                    break;
            }
        }

        /// <summary>
        /// Triggered when the console is pressed by the player
        /// </summary>
        public void PressButton()
        {
            SetState(ButtonState.Pressed);
        }

        public void StartChargingStrobe()
        {
            SetState(ButtonState.ChargingStrobe);
        }

        public void SetFiringOvercharge(bool isFiring)
        {
            if (isFiring)
            {
                SetState(ButtonState.FiringOvercharge);
            }
            else
            {
                SetState(ButtonState.Cooldown);
            }
        }

        public void ReturnToStandby()
        {
            SetState(ButtonState.Standby);
        }

        private IEnumerator StandbyRoutine()
        {
            if (buttonRenderer != null && standbySprite != null)
            {
                buttonRenderer.sprite = standbySprite;
            }
            transform.localPosition = initialLocalPos;
            transform.localScale = initialScale;
            yield break;
        }

        private IEnumerator PressRoutine()
        {
            // 1. Play tactile mechanical click
            if (clickAudio != null && audioSource != null)
            {
                audioSource.PlayOneShot(clickAudio, 1.0f);
            }

            if (buttonRenderer != null && litSprite != null)
            {
                buttonRenderer.sprite = litSprite;
            }

            if (buttonLight != null)
            {
                buttonLight.intensity = pressedLightIntensity;
            }

            // 2. Physical push inward/down
            float elapsed = 0f;
            float depressDuration = 0.07f;
            Vector3 depressedPos = initialLocalPos + new Vector3(0f, -pressDepressDistance, 0f);
            Vector3 depressedScale = initialScale * 0.91f;

            while (elapsed < depressDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / depressDuration);
                transform.localPosition = Vector3.Lerp(initialLocalPos, depressedPos, t);
                transform.localScale = Vector3.Lerp(initialScale, depressedScale, t);
                yield return null;
            }

            // 3. Elastic rebound pop
            elapsed = 0f;
            float reboundDuration = 0.10f;
            Vector3 peakScale = initialScale * 1.06f;

            while (elapsed < reboundDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / reboundDuration);
                float curve = Mathf.Sin(t * Mathf.PI);
                transform.localPosition = Vector3.Lerp(depressedPos, initialLocalPos, t);
                transform.localScale = Vector3.Lerp(initialScale, peakScale, curve);
                yield return null;
            }

            transform.localPosition = initialLocalPos;
            transform.localScale = initialScale;
        }

        private IEnumerator ChargingStrobeRoutine()
        {
            float strobeInterval = 0.08f;
            bool toggle = false;

            while (currentState == ButtonState.ChargingStrobe)
            {
                toggle = !toggle;
                if (buttonRenderer != null)
                {
                    buttonRenderer.sprite = toggle ? litSprite : standbySprite;
                }
                if (buttonLight != null)
                {
                    buttonLight.intensity = toggle ? pressedLightIntensity * 1.3f : standbyLightMin;
                }
                yield return new WaitForSeconds(strobeInterval);
            }
        }

        private IEnumerator FiringOverchargeRoutine()
        {
            if (buttonRenderer != null && overchargeSprite != null)
            {
                buttonRenderer.sprite = overchargeSprite;
            }
            if (buttonLight != null)
            {
                buttonLight.intensity = firingLightIntensity;
            }

            // Continuous high-voltage hum vibration while firing
            while (currentState == ButtonState.FiringOvercharge)
            {
                float vibX = Random.Range(-0.015f, 0.015f);
                float vibY = Random.Range(-0.015f, 0.015f);
                transform.localPosition = initialLocalPos + new Vector3(vibX, vibY, 0f);
                yield return null;
            }
            transform.localPosition = initialLocalPos;
        }

        private IEnumerator CooldownRoutine()
        {
            float elapsed = 0f;
            float duration = 0.75f;
            float startLight = buttonLight != null ? buttonLight.intensity : firingLightIntensity;

            if (buttonRenderer != null && litSprite != null)
            {
                buttonRenderer.sprite = litSprite;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                if (buttonLight != null)
                {
                    buttonLight.intensity = Mathf.Lerp(startLight, standbyLightMax, ease);
                }

                yield return null;
            }

            SetState(ButtonState.Standby);
        }

        [ContextMenu("Test Press Button")]
        public void TestPress() => PressButton();

        [ContextMenu("Test Charging Strobe")]
        public void TestChargingStrobe() => StartChargingStrobe();

        [ContextMenu("Test Firing Overcharge")]
        public void TestFiringOvercharge() => SetFiringOvercharge(true);

        [ContextMenu("Test Return Standby")]
        public void TestReturnStandby() => ReturnToStandby();
    }
}
