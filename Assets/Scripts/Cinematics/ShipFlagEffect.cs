using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Platformer.Cinematics
{
    /// <summary>
    /// Animates the flags on the tall ship in the ocean vista.
    /// Cycles through smooth wave frames matching the natural sea breeze.
    /// </summary>
    [ExecuteAlways]
    public class ShipFlagEffect : MonoBehaviour
    {
        public SpriteRenderer flagRenderer;
        public Sprite[] frames;
        [Tooltip("Animation playback speed in frames per second")]
        public float fps = 10f;

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (!Application.isPlaying && enabled && gameObject.activeInHierarchy)
            {
                UpdateFrame((float)EditorApplication.timeSinceStartup);
            }
        }
#endif

        private void Update()
        {
            if (Application.isPlaying)
            {
                UpdateFrame(Time.time);
            }
        }

        private void UpdateFrame(float t)
        {
            if (flagRenderer == null || frames == null || frames.Length == 0) return;
            int index = Mathf.FloorToInt(t * fps) % frames.Length;
            if (index < 0) index += frames.Length;
            if (index >= 0 && index < frames.Length)
            {
                flagRenderer.sprite = frames[index];
            }
        }
    }
}
