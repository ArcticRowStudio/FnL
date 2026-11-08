using UnityEngine;

namespace Platformer.View
{
    /// <summary>
    /// Makes a windsock ride the wind: a slowly varying gust strength lifts it out straight when
    /// strong and lets it sag when weak, while it sways and its fabric flutters on top.
    /// The sprite's pivot must be where the sock hangs from the pole, and the sock drawn pointing
    /// along +X (downwind); the authored rotation and scale are the full-wind pose.
    /// </summary>
    public class WindsockFlutter : MonoBehaviour
    {
        [Header("Gusts")]
        [Tooltip("How fast the wind strength wanders (Perlin noise rate).")]
        public float gustRate = 0.18f;

        [Tooltip("Lowest wind strength (0..1); at 1 the sock never sags.")]
        [Range(0f, 1f)]
        public float minStrength = 0.35f;

        [Tooltip("How far the sock sags, in degrees, when the wind is at its weakest.")]
        public float sagAngle = 18f;

        [Header("Sway")]
        [Tooltip("Swing either side of the current angle, in degrees.")]
        public float swayAmplitude = 3f;
        public float swayPeriod = 2.6f;

        [Header("Flutter")]
        [Tooltip("How much the fabric ripples, as a fraction of its size.")]
        [Range(0f, 0.2f)]
        public float flutterAmount = 0.04f;
        public float flutterFrequency = 2.4f;

        [Tooltip("How much shorter the sock gets when the wind drops.")]
        [Range(0f, 0.5f)]
        public float slackShrink = 0.12f;

        private Quaternion _baseRotation;
        private Vector3 _baseScale;
        private float _seed;

        private void Awake()
        {
            // Play mode reload is off, so the authored pose is captured afresh every session.
            _baseRotation = transform.localRotation;
            _baseScale = transform.localScale;
            _seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float t = Time.time;
            float strength = Mathf.Lerp(minStrength, 1f, Mathf.PerlinNoise(_seed, t * gustRate));

            // Sagging turns the downwind tip downwards, i.e. clockwise
            float sag = -sagAngle * (1f - strength);
            float sway = swayAmplitude * (0.5f + 0.5f * strength)
                         * (0.7f * Mathf.Sin(2f * Mathf.PI * t / swayPeriod) + 0.3f * Mathf.Sin(2f * Mathf.PI * t / (swayPeriod * 0.43f) + 1.1f));
            transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, sag + sway);

            float ripple = flutterAmount * strength * Mathf.Sin(2f * Mathf.PI * flutterFrequency * t + 0.5f * Mathf.Sin(t * 1.7f));
            float length = 1f - slackShrink * (1f - strength);
            transform.localScale = new Vector3(_baseScale.x * length, _baseScale.y * (1f + ripple), _baseScale.z);
        }

        private void OnDisable()
        {
            transform.localRotation = _baseRotation;
            transform.localScale = _baseScale;
        }
    }
}
