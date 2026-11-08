using System.Collections;
using Platformer.Core;
using Platformer.Model;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

namespace Platformer.View
{
    /// <summary>
    /// A short cutaway: raises a Cinemachine camera over the follow camera, fires
    /// <see cref="onReveal"/> once the blend has arrived (so the thing it shows happens on
    /// screen), holds, then hands the view back. The player is held still for the length of it.
    /// Used for the basement power switch: the camera shows the Quadpod arena waking up.
    /// </summary>
    public class RevealCameraShot : MonoBehaviour
    {
        public CinemachineCamera shotCamera;
        public int activePriority = 50;
        public int inactivePriority = 0;
        [Tooltip("Seconds from Play until onReveal fires (cover the camera blend)")]
        public float leadTime = 1.1f;
        [Tooltip("Seconds the shot holds after onReveal")]
        public float holdTime = 1.8f;
        public bool lockPlayer = true;
        public UnityEvent onReveal;

        bool playing;

        void Awake()
        {
            playing = false;
            SetPriority(inactivePriority);
        }

        public void Play()
        {
            if (playing) return;
            StartCoroutine(Shot());
        }

        IEnumerator Shot()
        {
            playing = true;
            var player = Simulation.GetModel<PlatformerModel>().player;
            bool restoreControl = player != null && player.controlEnabled;
            if (lockPlayer && player != null) player.controlEnabled = false;

            SetPriority(activePriority);
            yield return new WaitForSeconds(leadTime);
            onReveal?.Invoke();
            yield return new WaitForSeconds(holdTime);
            SetPriority(inactivePriority);

            if (lockPlayer && player != null && restoreControl) player.controlEnabled = true;
            playing = false;
        }

        void SetPriority(int value)
        {
            if (shotCamera == null) return;
            var p = shotCamera.Priority;
            p.Value = value;
            shotCamera.Priority = p;
        }
    }
}
