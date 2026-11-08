using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Represents a laser target lock core on the Bulkhead Gate.
    /// Takes damage from the Laser Beam Projector and switches from Red to Green when destroyed,
    /// triggering dynamic scale pop, neon green light burst, sparks, and gate opening.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BulkheadLock : MonoBehaviour
    {
        [Header("Lock Configuration")]
        public bool isLeft = true;
        public float maxHealth = 60f;
        public float currentHealth;
        public bool isDestroyed = false;

        [Header("Visuals")]
        public SpriteRenderer lockRenderer;
        public Sprite lockedSprite;
        public Sprite unlockedSprite;
        public Light2D lockLight;

        [Header("Effects")]
        public ParticleSystem hitSparks;
        public ParticleSystem explosionFX;
        public AudioClip hitAudio;
        public AudioClip destroyAudio;

        private BulkheadGateController gateController;
        private Vector3 initialScale = Vector3.one;
        private Coroutine pulseLightCoroutine;

        private void Awake()
        {
            if (lockRenderer == null) lockRenderer = GetComponent<SpriteRenderer>();
            initialScale = transform.localScale;
            currentHealth = maxHealth;
            isDestroyed = false;

            EnsureLightExists();
            EnsureParticlesExist();
        }

        private void EnsureLightExists()
        {
            if (lockLight == null)
            {
                lockLight = GetComponentInChildren<Light2D>();
                if (lockLight == null)
                {
                    var lightObj = new GameObject("Lock_Light");
                    lightObj.transform.SetParent(transform, false);
                    lightObj.transform.localPosition = Vector3.zero;
                    lockLight = lightObj.AddComponent<Light2D>();
                    lockLight.lightType = Light2D.LightType.Point;
                    lockLight.pointLightOuterRadius = 3.5f;
                    lockLight.pointLightInnerRadius = 0.5f;
                }
            }

            if (lockLight != null)
            {
                lockLight.color = isDestroyed ? new Color(0.1f, 1f, 0.4f, 1f) : new Color(1f, 0.2f, 0.15f, 1f);
                lockLight.intensity = isDestroyed ? 2.5f : 1.5f;
                lockLight.enabled = true;
            }
        }

        private void EnsureParticlesExist()
        {
            if (explosionFX == null)
            {
                var fxObj = transform.Find("Explosion_FX");
                if (fxObj != null)
                {
                    explosionFX = fxObj.GetComponent<ParticleSystem>();
                }
            }
        }

        public void Initialize(BulkheadGateController controller, bool isLeftLock)
        {
            this.gateController = controller;
            this.isLeft = isLeftLock;
            ResetLockInstant();
        }

        public void TakeLaserDamage(float amount)
        {
            if (isDestroyed) return;

            currentHealth -= amount;

            if (hitSparks != null && !hitSparks.isPlaying)
            {
                hitSparks.Play();
            }

            // Pulse red light on hit
            if (lockLight != null && pulseLightCoroutine == null)
            {
                pulseLightCoroutine = StartCoroutine(PulseHitLight());
            }

            if (gateController != null)
            {
                gateController.NotifyLockDamaged(this, currentHealth);
            }

            if (currentHealth <= 0f)
            {
                DestroyLock();
            }
        }

        private IEnumerator PulseHitLight()
        {
            if (lockLight == null) yield break;
            float baseIntensity = 1.5f;
            lockLight.intensity = 3.5f;
            yield return new WaitForSeconds(0.08f);
            if (!isDestroyed && lockLight != null)
            {
                lockLight.intensity = baseIntensity;
            }
            pulseLightCoroutine = null;
        }

        public void DestroyLock()
        {
            if (isDestroyed) return;
            isDestroyed = true;
            currentHealth = 0f;

            if (lockRenderer != null && unlockedSprite != null)
            {
                lockRenderer.sprite = unlockedSprite;
            }

            // Light turns neon green and flashes
            if (lockLight != null)
            {
                lockLight.color = new Color(0.15f, 1f, 0.45f, 1f);
                lockLight.intensity = 5.5f;
                StartCoroutine(FadeLightAfterUnlock(2.2f));
            }

            // Mechanical scale punch pop
            StartCoroutine(UnlockScalePopRoutine());

            if (explosionFX != null)
            {
                explosionFX.Play();
            }

            if (destroyAudio != null)
            {
                AudioSource.PlayClipAtPoint(destroyAudio, transform.position);
            }

            // Screen shake
            CameraZoomController.TriggerShake(0.38f, 0.3f);

            Debug.Log($"[BulkheadLock] {(isLeft ? "LEFT" : "RIGHT")} Lock physically unlocked and popped open!");

            if (gateController != null)
            {
                gateController.NotifyLockDestroyed(this);
            }
        }

        private IEnumerator UnlockScalePopRoutine()
        {
            float elapsed = 0f;
            float duration = 0.35f;
            Vector3 peakScale = initialScale * 1.35f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Quick pop up then ease back
                float curve = Mathf.Sin(t * Mathf.PI);
                transform.localScale = Vector3.Lerp(initialScale, peakScale, curve);
                yield return null;
            }

            transform.localScale = initialScale;
        }

        private IEnumerator FadeLightAfterUnlock(float targetIntensity)
        {
            if (lockLight == null) yield break;
            yield return new WaitForSeconds(0.4f);
            float elapsed = 0f;
            float start = lockLight.intensity;
            while (elapsed < 0.6f)
            {
                elapsed += Time.deltaTime;
                lockLight.intensity = Mathf.Lerp(start, targetIntensity, elapsed / 0.6f);
                yield return null;
            }
            lockLight.intensity = targetIntensity;
        }

        public void DestroyLockInstant()
        {
            isDestroyed = true;
            currentHealth = 0f;
            if (lockRenderer != null && unlockedSprite != null)
            {
                lockRenderer.sprite = unlockedSprite;
            }
            if (lockLight != null)
            {
                lockLight.color = new Color(0.15f, 1f, 0.45f, 1f);
                lockLight.intensity = 2.2f;
            }
        }

        public void ResetLockInstant()
        {
            isDestroyed = false;
            currentHealth = maxHealth;
            transform.localScale = initialScale;
            if (lockRenderer != null && lockedSprite != null)
            {
                lockRenderer.sprite = lockedSprite;
            }
            if (lockLight != null)
            {
                lockLight.color = new Color(1f, 0.2f, 0.15f, 1f);
                lockLight.intensity = 1.5f;
            }
        }
    }
}
