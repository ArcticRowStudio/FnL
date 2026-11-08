using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using Platformer.UI;

namespace Platformer.Cinematics
{
    /// <summary>
    /// Manages the cinematic transition when entering the base's outdoor observation veranda.
    /// Handles Cinemachine camera priority blending, widescreen letterboxing, and outdoor sunlight.
    /// </summary>
    public class VerandaCinematicController : MonoBehaviour
    {
        [Header("Cinemachine Cameras")]
        [Tooltip("Dedicated panoramic camera for the outdoor veranda")]
        public CinemachineCamera verandaCamera;
        [Tooltip("Priority when player is inside the veranda trigger zone")]
        public int activePriority = 20;
        [Tooltip("Priority when player is outside the veranda trigger zone")]
        public int inactivePriority = 0;

        [Header("Letterbox UI")]
        public bool enableLetterbox = false;
        [Range(0.5f, 3.0f)]
        public float letterboxTransitionDuration = 1.8f;

        [Header("Atmospheric Outdoor Lighting")]
        public Light2D outdoorSunLight;
        public float targetSunIntensity = 1.8f;
        public float lightFadeDuration = 1.5f;

        private bool _isInsideVeranda = false;
        private Coroutine _lightFadeCoroutine;
        private float _initialLightIntensity = 0f;

        private void Awake()
        {
            if (outdoorSunLight != null)
            {
                _initialLightIntensity = outdoorSunLight.intensity;
            }

            if (verandaCamera != null)
            {
                var p = verandaCamera.Priority;
                p.Value = inactivePriority;
                verandaCamera.Priority = p;
            }
        }

        private void Start()
        {
            CheckPlayerOverlap();
        }

        private void CheckPlayerOverlap()
        {
            if (_isInsideVeranda) return;

            var col = GetComponent<Collider2D>();
            if (col == null) return;

            var player = FindAnyObjectByType<Platformer.Mechanics.PlayerController>();
            if (player != null)
            {
                var playerCol = player.GetComponent<Collider2D>();
                if (playerCol != null && col.IsTouching(playerCol))
                {
                    _isInsideVeranda = true;
                    EnterCinematic();
                }
                else if (col.OverlapPoint(player.transform.position))
                {
                    _isInsideVeranda = true;
                    EnterCinematic();
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsPlayer(other) && !_isInsideVeranda)
            {
                _isInsideVeranda = true;
                EnterCinematic();
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (IsPlayer(other) && !_isInsideVeranda)
            {
                _isInsideVeranda = true;
                EnterCinematic();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other) && _isInsideVeranda)
            {
                _isInsideVeranda = false;
                ExitCinematic();
            }
        }

        private bool IsPlayer(Collider2D col)
        {
            if (col == null) return false;
            return col.CompareTag("Player") || col.GetComponent<Platformer.Mechanics.PlayerController>() != null;
        }

        public void EnterCinematic()
        {
            Debug.Log("[VerandaCinematic] Player entered Veranda! Triggering cinematic panorama view.");

            // 1. Pause gameplay camera zoom controller & pixel perfect lock
            Platformer.Mechanics.CameraZoomController.IsCinematicActive = true;
            if (Camera.main != null)
            {
                var ppc = Camera.main.GetComponent<PixelPerfectCamera>();
                if (ppc != null) ppc.enabled = false;
            }

            // 2. Activate Cinemachine Veranda Camera
            if (verandaCamera != null)
            {
                if (verandaCamera.Target.TrackingTarget == null)
                {
                    var pObj = FindAnyObjectByType<Platformer.Mechanics.PlayerController>();
                    if (pObj != null) verandaCamera.Target.TrackingTarget = pObj.transform;
                }
                var p = verandaCamera.Priority;
                p.Value = activePriority;
                verandaCamera.Priority = p;
            }

            // 3. Animate Cinematic Letterbox
            if (enableLetterbox)
            {
                CinematicLetterboxUI.Show(true, letterboxTransitionDuration);
            }

            // 4. Fade in outdoor sunlight
            if (outdoorSunLight != null)
            {
                FadeLight(targetSunIntensity);
            }
        }

        public void ExitCinematic()
        {
            Debug.Log("[VerandaCinematic] Player exited Veranda. Returning to standard gameplay view.");

            // 1. Resume gameplay camera zoom controller & pixel perfect lock
            Platformer.Mechanics.CameraZoomController.IsCinematicActive = false;
            if (Camera.main != null)
            {
                var ppc = Camera.main.GetComponent<PixelPerfectCamera>();
                if (ppc != null) ppc.enabled = true;
            }

            // 2. Deactivate Cinemachine Veranda Camera
            if (verandaCamera != null)
            {
                var p = verandaCamera.Priority;
                p.Value = inactivePriority;
                verandaCamera.Priority = p;
            }

            // 3. Retract Cinematic Letterbox
            if (enableLetterbox)
            {
                CinematicLetterboxUI.Show(false, letterboxTransitionDuration);
            }

            // 4. Fade out outdoor sunlight
            if (outdoorSunLight != null)
            {
                FadeLight(_initialLightIntensity);
            }
        }

        private void FadeLight(float targetIntensity)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_lightFadeCoroutine != null)
            {
                StopCoroutine(_lightFadeCoroutine);
            }
            _lightFadeCoroutine = StartCoroutine(AnimateLight(targetIntensity));
        }

        private IEnumerator AnimateLight(float targetIntensity)
        {
            float start = outdoorSunLight.intensity;
            float elapsed = 0f;

            while (elapsed < lightFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / (lightFadeDuration > 0.001f ? lightFadeDuration : 0.001f));
                t = t * t * (3f - 2f * t);
                outdoorSunLight.intensity = Mathf.Lerp(start, targetIntensity, t);
                yield return null;
            }

            outdoorSunLight.intensity = targetIntensity;
            _lightFadeCoroutine = null;
        }
    }
}
