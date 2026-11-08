using System;
using System.Collections;
using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Sekiro-style Posture (체간) gauge for the player.
    /// Empty at 0, builds up on missed parries or heavy blocks.
    /// Reaching 100% causes a Guard Break (그로기 스턴).
    /// Perfect parries consume zero posture and recover posture instead!
    /// </summary>
    public class PlayerPosture : MonoBehaviour
    {
        [Header("Posture Settings")]
        public float maxPosture = 100f;
        public float decayPerSecond = 22f;
        public float decayDelay = 0.9f;

        [Header("Costs & Rewards")]
        public float missCost = 25f;
        public float successReward = 15f;
        public float guardBreakDuration = 1.25f;

        [Header("State")]
        public float currentPosture = 0f;
        public bool isBroken = false;

        public float Normalized => maxPosture > 0f ? Mathf.Clamp01(currentPosture / maxPosture) : 0f;
        public bool CanParry => !isBroken;

        public event Action<float, float> OnPostureChanged;
        public event Action OnGuardBreak;
        public event Action OnGuardBreakRecovered;

        private float decayTimer;
        private Coroutine breakCoroutine;
        private PlayerController player;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            currentPosture = 0f;
        }

        void Update()
        {
            if (isBroken) return;

            if (decayTimer > 0f)
            {
                decayTimer -= Time.deltaTime;
                return;
            }

            if (currentPosture > 0f)
            {
                currentPosture = Mathf.Max(0f, currentPosture - decayPerSecond * Time.deltaTime);
                OnPostureChanged?.Invoke(currentPosture, maxPosture);
            }
        }

        /// <summary>
        /// Called when the player whiffs a parry without contacting any bullet or enemy.
        /// </summary>
        public void OnParryMiss()
        {
            if (isBroken) return;
            AddPosture(missCost);
        }

        /// <summary>
        /// Called when a parry successfully deflects a bullet or attack.
        /// </summary>
        public void OnParrySuccess()
        {
            if (isBroken) return;

            // Successful parry rewards player by shedding accumulated posture!
            currentPosture = Mathf.Max(0f, currentPosture - successReward);
            decayTimer = decayDelay;
            OnPostureChanged?.Invoke(currentPosture, maxPosture);
        }

        public void AddPosture(float amount)
        {
            if (isBroken) return;

            currentPosture = Mathf.Min(maxPosture, currentPosture + amount);
            decayTimer = decayDelay;
            OnPostureChanged?.Invoke(currentPosture, maxPosture);

            if (currentPosture >= maxPosture)
            {
                TriggerGuardBreak();
            }
        }

        public void ResetPosture()
        {
            currentPosture = 0f;
            isBroken = false;
            decayTimer = 0f;
            OnPostureChanged?.Invoke(0f, maxPosture);
        }

        private void TriggerGuardBreak()
        {
            if (isBroken) return;

            isBroken = true;
            currentPosture = maxPosture;
            OnPostureChanged?.Invoke(currentPosture, maxPosture);
            OnGuardBreak?.Invoke();

            if (breakCoroutine != null) StopCoroutine(breakCoroutine);
            breakCoroutine = StartCoroutine(GuardBreakRoutine());
        }

        private IEnumerator GuardBreakRoutine()
        {
            // Visual & Audio impact for Guard Break
            CameraZoomController.TriggerShake(0.35f, 0.25f);
            ExpandingRing.Spawn(transform.position, new Color(1f, 0.2f, 0.1f), 2.2f, 0.3f);
            ProjectileDebris.SpawnSpark(transform.position, new Color(1f, 0.3f, 0.1f), 20, 10f);

            if (player != null && player.audioSource && player.ouchAudio)
            {
                player.audioSource.PlayOneShot(player.ouchAudio, 1f);
            }

            yield return new WaitForSeconds(guardBreakDuration);

            // Recover from Guard Break
            isBroken = false;
            currentPosture = 0f;
            decayTimer = 0f;
            OnPostureChanged?.Invoke(0f, maxPosture);
            OnGuardBreakRecovered?.Invoke();
        }
    }
}
