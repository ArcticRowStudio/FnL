using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Platformer.Cinematics
{
    /// <summary>
    /// Controls the cinematic whale breaching and tail fluking animation in the arctic bay.
    /// Manages frame-by-frame sprite playback, submerging intervals, sunset breach lighting, and wide-shot cinematic triggers.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class WhaleAnimationController : MonoBehaviour
    {
        [Header("Animation Frames")]
        [Tooltip("The sequential animation frames of the whale breaching and fluking")]
        public Sprite[] frames;

        [Header("Playback Settings")]
        [Tooltip("Playback speed in frames per second (~10.5 fps produces a balanced, graceful breach)")]
        [Range(3f, 30f)]
        public float fps = 10.5f;

        [Tooltip("Whether to automatically surface and breach at intervals")]
        public bool loopWithInterval = true;

        [Tooltip("Time (seconds) submerged between repeat breaches")]
        public float interval = 7.0f;

        [Tooltip("Hide the sprite renderer while submerged underwater waiting for the next breach")]
        public bool hideWhenSubmerged = true;

        [Header("Sunset Breach Lighting")]
        [Tooltip("Optional 2D point light that casts a warm golden sunset glow on the whale and water while breaching")]
        public Light2D breachLight;

        [Tooltip("Peak light intensity reached when the tail is fully raised")]
        public float maxLightIntensity = 1.2f;

        [Header("Cinematic Triggers")]
        [Tooltip("If true, automatically breach when the player approaches the veranda wide shot")]
        public bool triggerOnWideShot = true;

        [Header("Editor Preview")]
        [Tooltip("Check to preview repeat animation in editor (with 7s interval). Leave OFF when moving/dragging the whale in Scene view!")]
        public bool previewInEditor = false;

        private SpriteRenderer _spriteRenderer;
        private int _currentFrame = 0;
        private bool _isPlayingBreach = false;
        private float _timer = 0f;
        private float _submergedTimer = 0f;
        private double _lastEditorTime = 0.0;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (breachLight == null)
            {
                breachLight = GetComponentInChildren<Light2D>();
            }
        }

        private void OnEnable()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (breachLight == null)
            {
                breachLight = GetComponentInChildren<Light2D>();
            }
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
            _lastEditorTime = EditorApplication.timeSinceStartup;
#endif
            if (Application.isPlaying)
            {
                // Start first breach after 1.5s so the player can witness it quickly
                TriggerBreach();
            }
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (_isPlayingBreach)
            {
                AdvanceBreachAnimation(Time.deltaTime);
            }
            else if (loopWithInterval)
            {
                _submergedTimer += Time.deltaTime;
                if (_submergedTimer >= interval)
                {
                    _submergedTimer = 0f;
                    TriggerBreach();
                }
            }
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (this == null)
            {
                EditorApplication.update -= EditorUpdate;
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float dt = (float)(currentTime - _lastEditorTime);
            _lastEditorTime = currentTime;

            if (!Application.isPlaying && previewInEditor && frames != null && frames.Length > 0)
            {
                if (_isPlayingBreach)
                {
                    AdvanceBreachAnimation(dt);
                }
                else if (loopWithInterval)
                {
                    _submergedTimer += dt;
                    if (_submergedTimer >= interval)
                    {
                        _submergedTimer = 0f;
                        TriggerBreach();
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Triggers a full breach and fluking sequence.
        /// </summary>
        [ContextMenu("Trigger Breach Now")]
        public void TriggerBreach()
        {
            _currentFrame = 0;
            _timer = 0f;
            _isPlayingBreach = true;

            if (_spriteRenderer != null && frames != null && frames.Length > 0)
            {
                _spriteRenderer.enabled = true;
                _spriteRenderer.sprite = frames[0];
            }

            UpdateLightIntensity();
        }

        /// <summary>
        /// Submerges the whale and resets to idle calm water.
        /// </summary>
        [ContextMenu("Reset / Submerge")]
        public void ResetSubmerged()
        {
            _isPlayingBreach = false;
            _currentFrame = 0;
            _timer = 0f;
            _submergedTimer = 0f;

            if (_spriteRenderer != null)
            {
                if (hideWhenSubmerged)
                {
                    _spriteRenderer.enabled = false;
                }
                else if (frames != null && frames.Length > 0)
                {
                    _spriteRenderer.sprite = frames[0];
                }
            }

            UpdateLightIntensity();
        }

        private void AdvanceBreachAnimation(float dt)
        {
            if (frames == null || frames.Length == 0) return;

            _timer += dt;
            float frameDuration = 1f / Mathf.Max(1f, fps);

            if (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _currentFrame++;

                if (_currentFrame < frames.Length)
                {
                    if (_spriteRenderer != null)
                    {
                        _spriteRenderer.enabled = true;
                        _spriteRenderer.sprite = frames[_currentFrame];
                    }
                    UpdateLightIntensity();
                }
                else
                {
                    // Breach complete! Submerge back into deep water
                    ResetSubmerged();
                }
            }
        }

        /// <summary>
        /// Updates the 2D point light intensity smoothly matching the breach height curve.
        /// </summary>
        private void UpdateLightIntensity()
        {
            if (breachLight == null) return;

            if (!_isPlayingBreach || frames == null || frames.Length == 0)
            {
                breachLight.intensity = 0f;
                return;
            }

            float lightFactor = 0f;
            if (_currentFrame <= 4)
            {
                lightFactor = Mathf.Lerp(0.2f, 0.65f, (float)_currentFrame / 4f);
            }
            else if (_currentFrame <= 9)
            {
                lightFactor = Mathf.Lerp(0.65f, 1.0f, (float)(_currentFrame - 4) / 5f);
            }
            else if (_currentFrame <= 20)
            {
                lightFactor = 1.0f;
            }
            else if (_currentFrame <= 25)
            {
                lightFactor = Mathf.Lerp(1.0f, 0.2f, (float)(_currentFrame - 20) / 5f);
            }
            else
            {
                lightFactor = Mathf.Lerp(0.2f, 0f, (float)(_currentFrame - 25) / 12f);
            }

            breachLight.intensity = lightFactor * maxLightIntensity;
        }

        /// <summary>
        /// Call to set preview frame in editor
        /// </summary>
        public void SetPreviewFrame(int index)
        {
            if (frames != null && index >= 0 && index < frames.Length)
            {
                _currentFrame = index;
                _isPlayingBreach = true;
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.enabled = true;
                    _spriteRenderer.sprite = frames[index];
                }
                UpdateLightIntensity();
            }
        }
    }
}
