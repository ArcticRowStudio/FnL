using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Animates the heavy industrial reload sequence for the Laser Beam Projector:
    /// 1. Pneumatic Lift: Dual gas-pressure pistons extend, raising the armored breech hatch.
    /// 2. Cartridge Insertion: High-energy plasma cartridge slides into the breach chamber and locks into contacts.
    /// 3. Pneumatic Slam: Dual pistons slam down under pressure, clamping the hatch shut.
    /// 4. Gas Purge: High-pressure steam/gas blast discharges from lateral exhaust vents.
    /// 5. Heatsink Deployment: Radiator cooling fins extend and thermally charge before firing.
    /// </summary>
    public class LaserProjectorReloadMechanism : MonoBehaviour
    {
        [Header("Components & Transforms")]
        public Transform hatchTransform;
        public Transform cartridgeTransform;
        public Transform heatsinkTransform;
        public Transform pistonLeftRod;
        public Transform pistonRightRod;
        public SpriteRenderer cartridgeRenderer;
        public SpriteRenderer heatsinkRenderer;

        [Header("Effects & Lighting")]
        public ParticleSystem steamVentLeft;
        public ParticleSystem steamVentRight;
        [Tooltip("Hot wispy vapor that rolls out of the open chamber when hatch lifts")]
        public ParticleSystem chamberVaporVent;
        public Light2D cartridgeLight;
        public Light2D heatsinkLight;
        public AudioSource audioSource;

        [Header("Audio Clips")]
        public AudioClip pneumaticHissClip;
        public AudioClip cartridgeInsertClip;
        public AudioClip steamBlastClip;
        public AudioClip hatchSlamClip;

        [Header("Motion Settings (Local Coordinates)")]
        // Hatch
        public Vector3 hatchClosedPos = new Vector3(0f, 1.05f, 0f);
        public Vector3 hatchOpenPos = new Vector3(0f, 2.35f, 0f);
        public float hatchOpenTilt = -12f;

        // Piston Rods (relative to their cylinder parent)
        public Vector3 rodRetractedPos = new Vector3(0f, 0.2f, 0f);
        public Vector3 rodExtendedPos = new Vector3(0f, 1.45f, 0f);

        // Cartridge
        public Vector3 cartridgeStagedPos = new Vector3(0f, 3.4f, 0f);
        public Vector3 cartridgeLoadedPos = new Vector3(0f, 0.35f, 0f);

        // Heatsink
        public Vector3 heatsinkRetractedPos = new Vector3(-2.2f, 0.4f, 0f);
        public Vector3 heatsinkDeployedPos = new Vector3(-2.85f, 0.4f, 0f);

        [Header("Timings (Seconds)")]
        public float hatchOpenDuration = 0.40f;
        public float cartridgeInsertDuration = 0.45f;
        public float hatchCloseDuration = 0.18f;
        public float heatsinkDeployDuration = 0.30f;
        public float cooldownDuration = 0.8f;

        [Header("Glow & Intensity")]
        public float cartridgeMaxLight = 2.8f;
        public float cartridgeStandbyLight = 0.6f;
        public float heatsinkMaxLight = 2.0f;

        private Coroutine activeReloadCoroutine;
        private Vector3 initialBasePos;

        private void Awake()
        {
            initialBasePos = transform.localPosition;
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0.4f; // audible across wide shot
                    audioSource.minDistance = 5f;
                    audioSource.maxDistance = 60f;
                }
            }
        }

        private void Start()
        {
            SetClosedIdleState();
        }

        public void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        /// <summary>
        /// Resets the reload mechanism to ready/closed state
        /// </summary>
        public void SetClosedIdleState()
        {
            if (hatchTransform != null)
            {
                hatchTransform.localPosition = hatchClosedPos;
                hatchTransform.localRotation = Quaternion.identity;
            }

            if (pistonLeftRod != null) pistonLeftRod.localPosition = rodRetractedPos;
            if (pistonRightRod != null) pistonRightRod.localPosition = rodRetractedPos;

            if (cartridgeTransform != null)
            {
                cartridgeTransform.localPosition = cartridgeLoadedPos;
            }

            if (heatsinkTransform != null)
            {
                heatsinkTransform.localPosition = heatsinkRetractedPos;
            }

            if (cartridgeLight != null) cartridgeLight.intensity = cartridgeStandbyLight;
            if (heatsinkLight != null) heatsinkLight.intensity = 0.2f;

            if (chamberVaporVent != null) chamberVaporVent.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>
        /// Complete pneumatic reload and arming sequence:
        /// Lift Hatch -> Insert Cartridge -> Slam Shut & Gas Purge -> Deploy Heatsink
        /// </summary>
        public IEnumerator PlayReloadSequenceRoutine()
        {
            // 1. PNEUMATIC LIFT: Gas pistons push armored hatch open
            PlaySound(pneumaticHissClip, 0.9f);
            if (chamberVaporVent != null) chamberVaporVent.Play(true);

            // Hide/stage cartridge above the open breech
            if (cartridgeTransform != null)
            {
                cartridgeTransform.localPosition = cartridgeStagedPos;
                if (cartridgeRenderer != null)
                {
                    Color c = cartridgeRenderer.color;
                    c.a = 0f;
                    cartridgeRenderer.color = c;
                }
            }

            float elapsed = 0f;
            while (elapsed < hatchOpenDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hatchOpenDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                // Extend piston rods
                Vector3 curRod = Vector3.Lerp(rodRetractedPos, rodExtendedPos, ease);
                if (pistonLeftRod != null) pistonLeftRod.localPosition = curRod;
                if (pistonRightRod != null) pistonRightRod.localPosition = curRod;

                // Lift and tilt hatch
                if (hatchTransform != null)
                {
                    hatchTransform.localPosition = Vector3.Lerp(hatchClosedPos, hatchOpenPos, ease);
                    hatchTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, hatchOpenTilt, ease));
                }

                yield return null;
            }

            // 2. CARTRIDGE INJECTION: Slide into chamber socket
            elapsed = 0f;
            if (cartridgeRenderer != null)
            {
                Color c = cartridgeRenderer.color;
                c.a = 1f;
                cartridgeRenderer.color = c;
            }

            while (elapsed < cartridgeInsertDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / cartridgeInsertDuration);
                // Ease in with acceleration down the guide rails
                float ease = t * t;

                if (cartridgeTransform != null)
                {
                    cartridgeTransform.localPosition = Vector3.Lerp(cartridgeStagedPos, cartridgeLoadedPos, ease);
                }

                if (cartridgeLight != null)
                {
                    cartridgeLight.intensity = Mathf.Lerp(0f, cartridgeMaxLight * 0.7f, ease);
                }

                yield return null;
            }

            if (cartridgeTransform != null) cartridgeTransform.localPosition = cartridgeLoadedPos;

            // Cartridge seats into high-voltage contacts!
            PlaySound(cartridgeInsertClip, 1.0f);
            if (cartridgeLight != null)
            {
                cartridgeLight.intensity = cartridgeMaxLight * 1.4f;
            }

            // Micro-impact vibration on reload root
            yield return StartCoroutine(ShakeRootRoutine(0.04f, 0.08f));

            // 3. PNEUMATIC SLAM: Pistons power down, clamping hatch shut
            elapsed = 0f;
            while (elapsed < hatchCloseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hatchCloseDuration);
                // Fast aggressive slam curve
                float ease = t * t * t;

                Vector3 curRod = Vector3.Lerp(rodExtendedPos, rodRetractedPos, ease);
                if (pistonLeftRod != null) pistonLeftRod.localPosition = curRod;
                if (pistonRightRod != null) pistonRightRod.localPosition = curRod;

                if (hatchTransform != null)
                {
                    hatchTransform.localPosition = Vector3.Lerp(hatchOpenPos, hatchClosedPos, ease);
                    hatchTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(hatchOpenTilt, 0f, ease));
                }

                yield return null;
            }

            if (hatchTransform != null)
            {
                hatchTransform.localPosition = hatchClosedPos;
                hatchTransform.localRotation = Quaternion.identity;
            }
            if (pistonLeftRod != null) pistonLeftRod.localPosition = rodRetractedPos;
            if (pistonRightRod != null) pistonRightRod.localPosition = rodRetractedPos;

            if (chamberVaporVent != null) chamberVaporVent.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            // 4. GAS PURGE & SLAM IMPACT: Explosive steam blast!
            PlaySound(steamBlastClip, 1.0f);
            if (hatchSlamClip != null) PlaySound(hatchSlamClip, 0.85f);

            if (steamVentLeft != null) steamVentLeft.Play(true);
            if (steamVentRight != null) steamVentRight.Play(true);

            // Camera shake from the high-pressure slam
            CameraZoomController.TriggerShake(0.18f, 0.22f);
            yield return StartCoroutine(ShakeRootRoutine(0.06f, 0.12f));

            // 5. HEATSINK DEPLOYMENT: Expand radiator fins & thermal glow
            elapsed = 0f;
            while (elapsed < heatsinkDeployDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / heatsinkDeployDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                if (heatsinkTransform != null)
                {
                    heatsinkTransform.localPosition = Vector3.Lerp(heatsinkRetractedPos, heatsinkDeployedPos, ease);
                }

                if (heatsinkLight != null)
                {
                    heatsinkLight.intensity = Mathf.Lerp(0.2f, heatsinkMaxLight, ease);
                }

                yield return null;
            }

            if (heatsinkTransform != null) heatsinkTransform.localPosition = heatsinkDeployedPos;
        }

        /// <summary>
        /// Post-firing cooldown: Retracts heatsink fins and dims thermal lights
        /// </summary>
        public IEnumerator PlayCooldownRoutine()
        {
            float elapsed = 0f;
            Vector3 startHeatsink = heatsinkTransform != null ? heatsinkTransform.localPosition : heatsinkDeployedPos;

            while (elapsed < cooldownDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / cooldownDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                if (heatsinkTransform != null)
                {
                    heatsinkTransform.localPosition = Vector3.Lerp(startHeatsink, heatsinkRetractedPos, ease);
                }

                if (heatsinkLight != null)
                {
                    heatsinkLight.intensity = Mathf.Lerp(heatsinkMaxLight, 0.2f, ease);
                }

                if (cartridgeLight != null)
                {
                    cartridgeLight.intensity = Mathf.Lerp(cartridgeMaxLight, cartridgeStandbyLight, ease);
                }

                yield return null;
            }

            SetClosedIdleState();
        }

        private IEnumerator ShakeRootRoutine(float intensity, float duration)
        {
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float offsetX = Random.Range(-intensity, intensity);
                float offsetY = Random.Range(-intensity, intensity);
                transform.localPosition = initialBasePos + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }
            transform.localPosition = initialBasePos;
        }

        [ContextMenu("Preview Closed Idle")]
        public void PreviewClosedIdle()
        {
            SetClosedIdleState();
        }

        [ContextMenu("Preview Pneumatic Lift")]
        public void PreviewPneumaticLift()
        {
            if (hatchTransform != null)
            {
                hatchTransform.localPosition = hatchOpenPos;
                hatchTransform.localRotation = Quaternion.Euler(0f, 0f, hatchOpenTilt);
            }
            if (pistonLeftRod != null) pistonLeftRod.localPosition = rodExtendedPos;
            if (pistonRightRod != null) pistonRightRod.localPosition = rodExtendedPos;
            if (cartridgeTransform != null) cartridgeTransform.localPosition = cartridgeStagedPos;
            if (cartridgeRenderer != null) { Color c = cartridgeRenderer.color; c.a = 1f; cartridgeRenderer.color = c; }
            if (cartridgeLight != null) cartridgeLight.intensity = cartridgeMaxLight;
        }

        [ContextMenu("Preview Cartridge Inserted")]
        public void PreviewCartridgeInserted()
        {
            if (hatchTransform != null)
            {
                hatchTransform.localPosition = hatchOpenPos;
                hatchTransform.localRotation = Quaternion.Euler(0f, 0f, hatchOpenTilt);
            }
            if (pistonLeftRod != null) pistonLeftRod.localPosition = rodExtendedPos;
            if (pistonRightRod != null) pistonRightRod.localPosition = rodExtendedPos;
            if (cartridgeTransform != null) cartridgeTransform.localPosition = cartridgeLoadedPos;
            if (cartridgeRenderer != null) { Color c = cartridgeRenderer.color; c.a = 1f; cartridgeRenderer.color = c; }
            if (cartridgeLight != null) cartridgeLight.intensity = cartridgeMaxLight * 1.4f;
        }

        [ContextMenu("Preview Heatsink Deployed")]
        public void PreviewHeatsinkDeployed()
        {
            SetClosedIdleState();
            if (heatsinkTransform != null) heatsinkTransform.localPosition = heatsinkDeployedPos;
            if (heatsinkLight != null) heatsinkLight.intensity = heatsinkMaxLight;
            if (cartridgeLight != null) cartridgeLight.intensity = cartridgeMaxLight;
        }

        [ContextMenu("Test Reload Sequence")]
        public void TestReloadSequence()
        {
            if (Application.isPlaying)
            {
                StopAllCoroutines();
                StartCoroutine(PlayReloadSequenceRoutine());
            }
            else
            {
                Debug.Log("[LaserProjectorReload] Run in Play Mode to test full animated sequence.");
            }
        }
    }
}
