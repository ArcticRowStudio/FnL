using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Platformer.Mechanics
{
    /// <summary>
    /// Controls 2D camera zoom levels dynamically while maintaining pixel perfection.
    /// Supports mouse scroll wheel and keyboard shortcuts (+ / - / bracket keys).
    /// </summary>
    public class CameraZoomController : MonoBehaviour
    {
        [System.Serializable]
        public struct ZoomLevel
        {
            public string label;
            public int refWidth;
            public int refHeight;

            public ZoomLevel(string label, int w, int h)
            {
                this.label = label;
                this.refWidth = w;
                this.refHeight = h;
            }
        }

        [Header("Zoom Presets (from Far to Close)")]
        public ZoomLevel[] zoomLevels = new ZoomLevel[]
        {
            new ZoomLevel("Far (Original)", 384, 216),    // 1.0x (ortho ~6.75)
            new ZoomLevel("Medium-Far", 320, 180),        // 1.2x (ortho ~5.625)
            new ZoomLevel("Medium (Recommended)", 240, 135), // 1.6x (ortho ~4.22)
            new ZoomLevel("Close", 192, 108)             // 2.0x (ortho ~3.375)
        };

        [Tooltip("Index of the default zoom level (0 = Far, 2 = Medium/Recommended, 3 = Close)")]
        public int currentLevelIndex = 2; // Default to Medium (240x135)

        [Header("Input Settings")]
        public bool enableMouseScroll = true;
        public bool enableKeyboardShortcuts = true;

        [Header("ADS Sniper Aim Settings")]
        public bool enableAimZoom = true;
        [Tooltip("Magnification factor when looking down the AMR scope (0.85 = 15% closer)")]
        public float aimZoomRatio = 0.85f;
        [Tooltip("Subtle camera offset towards mouse cursor while aiming (0 = stay strictly locked to player)")]
        public float aimLookaheadWeight = 0f;
        public float aimTransitionSpeed = 8f;

        [Header("Focus Framing")]
        [Tooltip("Largest orthographic size the camera widens to when framing the player together with a focus target, such as a boss up a wall")]
        public float maxFocusOrthoSize = 12.5f;
        [Tooltip("World units kept clear around the player and the focus target")]
        public float focusMargin = 1.5f;
        [Tooltip("How fast framing blends in and out, in blends per second")]
        public float focusBlendSpeed = 3f;
        [Tooltip("A focus target that is not engaged (see SetFocusEngaged) is let go once fitting it with the player would need this much more ortho size than the cap, and taken back once it fits again")]
        public float focusReleaseSlack = 3f;

        private PixelPerfectCamera pixelPerfectCamera;
        private CinemachineCamera virtualCamera;
        private CinemachinePositionComposer positionComposer;
        private Camera unityCamera;
        private PlayerController player;

        private Vector3 baseTargetOffset = new Vector3(0f, 0.4f, 0f);
        private float currentAimZoom = 1f;
        private Vector3 currentLookaheadOffset = Vector3.zero;

        private static CameraZoomController instance;
        private float shakeIntensity = 0f;
        private float shakeDuration = 0f;
        private float shakeTimer = 0f;

        private Collider2D focusTarget;
        private Bounds lastFocusBounds;
        private float focusBlend = 0f;
        private float focusMaxOrtho = 0f;
        private bool focusEngaged = true;
        private bool focusReleased;

        /// <summary>
        /// Widens and recentres the camera to keep target in shot alongside the player, until
        /// <see cref="ClearFocus"/> is called with the same target or the target is disabled.
        /// <paramref name="maxOrthoSize"/> overrides maxFocusOrthoSize for this target (0 keeps it),
        /// e.g. a boss whose arena needs a wider shot.
        /// </summary>
        public static void SetFocus(Collider2D target, float maxOrthoSize = 0f)
        {
            if (instance == null || target == null) return;
            instance.focusTarget = target;
            instance.focusMaxOrtho = maxOrthoSize;
            instance.focusEngaged = true;
            instance.focusReleased = false;
        }

        /// <summary>
        /// Engaged (the default), the focus target is always framed with the player, even when
        /// that cuts it off at the edge. Not engaged - a boss flying off between attacks, say - it
        /// is let go once it no longer fits in the widest allowed shot, and the camera settles back
        /// on the player until it comes back in range.
        /// </summary>
        public static void SetFocusEngaged(Collider2D target, bool engaged)
        {
            if (instance != null && instance.focusTarget == target) instance.focusEngaged = engaged;
        }

        /// <summary>The current focus target is in shot right now (not let go, not blended out).</summary>
        public static bool IsFraming(Collider2D target)
        {
            return instance != null && instance.focusTarget == target && instance.focusBlend > 0.5f && !instance.focusReleased;
        }

        public static void ClearFocus(Collider2D target)
        {
            if (instance != null && instance.focusTarget == target) instance.focusTarget = null;
        }

        public static void TriggerShake(float intensity = 0.35f, float duration = 0.15f)
        {
            if (instance != null)
            {
                instance.shakeIntensity = Mathf.Max(instance.shakeIntensity, intensity);
                instance.shakeDuration = duration;
                instance.shakeTimer = duration;
            }
        }

        private void Awake()
        {
            instance = this;
            // Static, and play mode skips the domain reload: a session stopped mid-cinematic
            // would otherwise start the next one with pixel perfect off and zoom input dead.
            IsCinematicActive = false;
            unityCamera = GetComponent<Camera>();
            if (unityCamera == null) unityCamera = Camera.main;

            if (unityCamera != null)
            {
                pixelPerfectCamera = unityCamera.GetComponent<PixelPerfectCamera>();
            }

            var vcamObj = GameObject.Find("CM vcam1");
            if (vcamObj != null)
            {
                virtualCamera = vcamObj.GetComponent<CinemachineCamera>();
                positionComposer = vcamObj.GetComponent<CinemachinePositionComposer>();
                if (positionComposer != null)
                {
                    baseTargetOffset = positionComposer.TargetOffset;
                }
            }

            ApplyZoom(currentLevelIndex, false);
        }

        public static bool IsCinematicActive = false;

        private void Update()
        {
            if (IsCinematicActive)
            {
                if (pixelPerfectCamera != null && pixelPerfectCamera.enabled)
                {
                    pixelPerfectCamera.enabled = false;
                }
                return;
            }

            HandleInput();
            UpdateAimZoomAndLookahead();
        }

        private void HandleInput()
        {
            float scrollDelta = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                scrollDelta = Mouse.current.scroll.ReadValue().y;
            }
#else
            scrollDelta = Input.mouseScrollDelta.y;
#endif

            if (enableMouseScroll && Mathf.Abs(scrollDelta) > 0.05f)
            {
                if (scrollDelta > 0f)
                {
                    // Scroll up = Zoom IN (closer)
                    ZoomIn();
                }
                else
                {
                    // Scroll down = Zoom OUT (farther)
                    ZoomOut();
                }
            }

            if (enableKeyboardShortcuts)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.equalsKey.wasPressedThisFrame || Keyboard.current.numpadPlusKey.wasPressedThisFrame || Keyboard.current.rightBracketKey.wasPressedThisFrame)
                    {
                        ZoomIn();
                    }
                    else if (Keyboard.current.minusKey.wasPressedThisFrame || Keyboard.current.numpadMinusKey.wasPressedThisFrame || Keyboard.current.leftBracketKey.wasPressedThisFrame)
                    {
                        ZoomOut();
                    }
                }
#else
                if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.RightBracket))
                {
                    ZoomIn();
                }
                else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.LeftBracket))
                {
                    ZoomOut();
                }
#endif
            }
        }

        public void ZoomIn()
        {
            if (currentLevelIndex < zoomLevels.Length - 1)
            {
                SetZoomIndex(currentLevelIndex + 1);
            }
        }

        public void ZoomOut()
        {
            if (currentLevelIndex > 0)
            {
                SetZoomIndex(currentLevelIndex - 1);
            }
        }

        public void SetZoomIndex(int newIndex)
        {
            if (zoomLevels == null || zoomLevels.Length == 0) return;
            currentLevelIndex = Mathf.Clamp(newIndex, 0, zoomLevels.Length - 1);
            ApplyZoom(currentLevelIndex, true);
        }

        private void ApplyZoom(int index, bool logMessage)
        {
            if (zoomLevels == null || index < 0 || index >= zoomLevels.Length) return;
            var preset = zoomLevels[index];

            if (pixelPerfectCamera != null)
            {
                pixelPerfectCamera.refResolutionX = preset.refWidth;
                pixelPerfectCamera.refResolutionY = preset.refHeight;
            }

            // Sync Cinemachine Lens OrthographicSize
            float ppu = pixelPerfectCamera != null ? (float)pixelPerfectCamera.assetsPPU : 16f;
            float orthoSize = preset.refHeight / (2f * ppu);

            if (virtualCamera != null)
            {
                var lens = virtualCamera.Lens;
                lens.OrthographicSize = orthoSize;
                virtualCamera.Lens = lens;
            }

            if (unityCamera != null)
            {
                unityCamera.orthographicSize = orthoSize;
            }

            if (logMessage)
            {
                Debug.Log($"[CameraZoom] Zoom set to {preset.label} ({preset.refWidth}x{preset.refHeight}, Ortho: {orthoSize:F2})");
            }
        }

        private void UpdateAimZoomAndLookahead()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            bool isAiming = enableAimZoom && player != null && player.IsAiming;
            float targetZoom = isAiming ? aimZoomRatio : 1f;
            currentAimZoom = Mathf.MoveTowards(currentAimZoom, targetZoom, Time.deltaTime * aimTransitionSpeed);

            Vector3 targetLookahead = Vector3.zero;
            if (isAiming && Camera.main != null && player != null)
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                {
                    Vector2 mouseScreen = Mouse.current.position.ReadValue();
                    float zDepth = Mathf.Abs(Camera.main.transform.position.z - player.transform.position.z);
                    Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, zDepth));
                    Vector3 dir = mouseWorld - player.transform.position;
                    targetLookahead = Vector3.ClampMagnitude(dir, 4.0f) * aimLookaheadWeight;
                }
#endif
            }

            currentLookaheadOffset = Vector3.MoveTowards(currentLookaheadOffset, targetLookahead, Time.deltaTime * (aimTransitionSpeed * 0.8f));

            Vector3 shakeOffset = Vector3.zero;
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.deltaTime;
                float damp = Mathf.Clamp01(shakeTimer / (shakeDuration > 0.0001f ? shakeDuration : 0.0001f));
                shakeOffset = new Vector3(
                    (Mathf.PerlinNoise(Time.time * 45f, 0f) - 0.5f) * 2f * shakeIntensity * damp,
                    (Mathf.PerlinNoise(0f, Time.time * 45f) - 0.5f) * 2f * shakeIntensity * damp,
                    0f
                );
            }

            // Frame the player and the focus target together: recentre on both and widen to fit.
            bool hasFocus = focusTarget != null && focusTarget.isActiveAndEnabled && player != null;
            if (hasFocus)
            {
                // A target that isn't engaged is let go while it is out of reach, with some slack
                // either way so it doesn't flicker in and out at the limit.
                Bounds wanted = focusTarget.bounds;
                wanted.Encapsulate(player.transform.position + baseTargetOffset);
                float aspectNow = unityCamera != null ? unityCamera.aspect : 16f / 9f;
                float need = Mathf.Max(wanted.extents.y, wanted.extents.x / aspectNow) + focusMargin;
                float capNow = focusMaxOrtho > 0f ? focusMaxOrtho : maxFocusOrthoSize;
                if (focusEngaged || need <= capNow) focusReleased = false;
                else if (need > capNow + focusReleaseSlack) focusReleased = true;
                hasFocus = !focusReleased;
            }
            if (hasFocus) lastFocusBounds = focusTarget.bounds;
            focusBlend = Mathf.MoveTowards(focusBlend, hasFocus ? 1f : 0f, Time.deltaTime * focusBlendSpeed);

            Vector3 focusOffset = Vector3.zero;
            float focusOrtho = 0f;
            if (focusBlend > 0f && player != null)
            {
                Vector3 playerPoint = player.transform.position + baseTargetOffset;
                Bounds shot = lastFocusBounds;
                shot.Encapsulate(playerPoint);
                float aspect = unityCamera != null ? unityCamera.aspect : 16f / 9f;
                float cap = focusMaxOrtho > 0f ? focusMaxOrtho : maxFocusOrthoSize;
                focusOrtho = Mathf.Min(cap, Mathf.Max(shot.extents.y, shot.extents.x / aspect) + focusMargin);

                // Player first: when the two don't fit in the widest allowed shot, centring between
                // them cuts both off. Slide the centre toward the player instead, so only the
                // target is cut and the player is never pushed off screen.
                float halfWidth = Mathf.Max(0f, focusOrtho * aspect - focusMargin);
                float halfHeight = Mathf.Max(0f, focusOrtho - focusMargin);
                Vector3 centre = shot.center;
                centre.x = Mathf.Clamp(centre.x, playerPoint.x - halfWidth, playerPoint.x + halfWidth);
                centre.y = Mathf.Clamp(centre.y, playerPoint.y - halfHeight, playerPoint.y + halfHeight);
                focusOffset = (centre - playerPoint) * focusBlend;
                focusOffset.z = 0f;
            }

            if (positionComposer != null)
            {
                positionComposer.TargetOffset = baseTargetOffset + currentLookaheadOffset + shakeOffset + focusOffset;
            }

            // Apply smooth ADS magnification to Cinemachine camera lens
            if (virtualCamera != null && zoomLevels != null && currentLevelIndex >= 0 && currentLevelIndex < zoomLevels.Length)
            {
                var preset = zoomLevels[currentLevelIndex];
                float ppu = pixelPerfectCamera != null ? (float)pixelPerfectCamera.assetsPPU : 16f;
                float baseOrtho = preset.refHeight / (2f * ppu);

                float ortho = baseOrtho * currentAimZoom;
                if (focusBlend > 0f) ortho = Mathf.Lerp(ortho, Mathf.Max(ortho, focusOrtho), focusBlend);

                var lens = virtualCamera.Lens;
                lens.OrthographicSize = ortho;
                virtualCamera.Lens = lens;

                if (unityCamera != null)
                {
                    unityCamera.orthographicSize = ortho;
                }
            }

            if (pixelPerfectCamera != null)
            {
                // While aiming with ADS zoom, temporarily let Cinemachine cleanly control
                // orthographic size so Camera.main.ScreenToWorldPoint remains 100% mathematically exact.
                // Focus framing needs the same freedom, since its size is not one of the presets.
                bool freeZoom = (isAiming && currentAimZoom < 0.999f) || focusBlend > 0f;
                if (freeZoom && pixelPerfectCamera.enabled)
                {
                    pixelPerfectCamera.enabled = false;
                }
                else if (!freeZoom && currentAimZoom >= 0.999f && !pixelPerfectCamera.enabled)
                {
                    pixelPerfectCamera.enabled = true;
                }
            }
        }

        private void OnDestroy()
        {
            IsCinematicActive = false;
            if (instance == this) instance = null;
        }
    }
}
