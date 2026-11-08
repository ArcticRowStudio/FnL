using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Platformer.View
{
    /// <summary>
    /// The basement starts in a blackout: its ceiling lamps run as dim, slowly pulsing red
    /// emergency lights. <see cref="RestorePower"/> (wired to the basement PowerSwitch) brings
    /// them back one after another from the switch end, each stuttering a few times before it
    /// holds, as old fluorescent tubes do.
    /// Only the lamps listed in <see cref="lamps"/> are touched; the global light and the
    /// player's light are left to whatever else drives them.
    /// </summary>
    public class BasementPowerGrid : MonoBehaviour
    {
        [Tooltip("Ceiling lamp lights, in any order; the power-on runs from the one nearest restoreOrigin outward")]
        public Light2D[] lamps;
        [Tooltip("Lamp sprites to darken during the blackout (optional, same length as lamps or empty)")]
        public SpriteRenderer[] lampSprites;
        [Tooltip("Where the power comes back from (the switch)")]
        public Transform restoreOrigin;
        public bool startInBlackout = true;

        [Header("Emergency look")]
        public Color emergencyColor = new Color(1f, 0.16f, 0.1f, 1f);
        // Lamps sit 13-16 apart: the radius has to bridge the gaps or enemies between lamps vanish.
        public float emergencyIntensity = 0.7f;
        public float emergencyRadius = 9.5f;
        public float emergencyPulseSpeed = 1.4f;
        public Color darkSpriteTint = new Color(0.35f, 0.3f, 0.3f, 1f);

        [Header("Power-on")]
        public float delayBetweenLamps = 0.14f;
        public int stutters = 3;
        public AudioClip lampOnClip;
        [Range(0f, 1f)] public float lampOnVolume = 0.5f;

        public bool PowerOn { get; private set; }

        struct LampState { public float intensity, radius; public Color color; public Color spriteColor; }
        LampState[] original;
        bool[] lit;
        float pulseTime;

        void Awake()
        {
            original = new LampState[lamps.Length];
            lit = new bool[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
            {
                var l = lamps[i];
                if (l == null) continue;
                original[i] = new LampState
                {
                    intensity = l.intensity,
                    radius = l.pointLightOuterRadius,
                    color = l.color,
                    spriteColor = SpriteAt(i) != null ? SpriteAt(i).color : Color.white,
                };
            }
            PowerOn = !startInBlackout;
            for (int i = 0; i < lamps.Length; i++)
            {
                lit[i] = PowerOn;
                if (PowerOn) ApplyLit(i); else ApplyEmergency(i, 1f);
            }
        }

        SpriteRenderer SpriteAt(int i) => lampSprites != null && i < lampSprites.Length ? lampSprites[i] : null;

        void Update()
        {
            pulseTime += Time.deltaTime * emergencyPulseSpeed;
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lit[i] || lamps[i] == null) continue;
                // Each lamp pulses a little out of step so the corridor does not breathe in unison.
                float pulse = 0.65f + 0.35f * Mathf.Sin(pulseTime * Mathf.PI * 2f + i * 1.7f);
                ApplyEmergency(i, pulse);
            }
        }

        void ApplyEmergency(int i, float pulse)
        {
            var l = lamps[i];
            l.color = emergencyColor;
            l.intensity = emergencyIntensity * pulse;
            l.pointLightOuterRadius = emergencyRadius;
            var s = SpriteAt(i);
            if (s != null) s.color = darkSpriteTint;
        }

        void ApplyLit(int i)
        {
            var l = lamps[i];
            l.color = original[i].color;
            l.intensity = original[i].intensity;
            l.pointLightOuterRadius = original[i].radius;
            var s = SpriteAt(i);
            if (s != null) s.color = original[i].spriteColor;
        }

        /// <summary>Brings the lamps back, nearest to the switch first. Does nothing twice.</summary>
        public void RestorePower()
        {
            if (PowerOn) return;
            PowerOn = true;
            StartCoroutine(PowerOnSequence());
        }

        IEnumerator PowerOnSequence()
        {
            var order = new List<int>();
            for (int i = 0; i < lamps.Length; i++) if (lamps[i] != null) order.Add(i);
            Vector3 origin = restoreOrigin != null ? restoreOrigin.position : transform.position;
            order.Sort((a, b) => Mathf.Abs(lamps[a].transform.position.x - origin.x)
                .CompareTo(Mathf.Abs(lamps[b].transform.position.x - origin.x)));

            foreach (int i in order)
            {
                StartCoroutine(StutterOn(i));
                yield return new WaitForSeconds(delayBetweenLamps);
            }
        }

        IEnumerator StutterOn(int i)
        {
            for (int s = 0; s < stutters; s++)
            {
                ApplyLit(i);
                lamps[i].intensity = original[i].intensity * Random.Range(0.4f, 1f);
                lit[i] = true;
                yield return new WaitForSeconds(Random.Range(0.03f, 0.07f));
                lamps[i].intensity = 0f;
                yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
            }
            ApplyLit(i);
            if (lampOnClip != null) AudioSource.PlayClipAtPoint(lampOnClip, lamps[i].transform.position, lampOnVolume);
        }
    }
}
