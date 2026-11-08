using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Platformer.Cinematics
{
    /// <summary>
    /// Controls the atmospheric snowfall and chilly wind gust effects on the Left Veranda (Glacier side).
    /// Features:
    /// - Triple-layer depth:
    ///     1. Foreground flakes fluttering in the cold draft in front of the player.
    ///     2. Background distant flakes swirling near the glacier cliffs.
    ///     3. Whispering wind spindrift (fine high-speed snow dust streaks carried by glacial air currents).
    /// - Dynamic wind gusts: periodic swelling and lulling of freezing winds blowing from sea to glacier.
    /// - Dynamic emission & velocity fading synchronized with veranda entry/exit and outdoor lighting.
    /// - Editor preview support for instant visual feedback.
    /// </summary>
    [ExecuteAlways]
    public class GlacierSnowController : MonoBehaviour
    {
        [Header("Particle Systems")]
        [Tooltip("Foreground snow drifting in front of the player and deck railings")]
        public ParticleSystem foregroundSnow;
        [Tooltip("Background snow drifting between the distant glacier and the deck")]
        public ParticleSystem backgroundSnow;
        [Tooltip("Fine cold wind spindrift streaks blowing rapidly across the deck")]
        public ParticleSystem windDriftSnow;

        [Header("Emission Rates (Max Capacity)")]
        [Range(1f, 30f)]
        public float maxForegroundRate = 10f;
        [Range(1f, 50f)]
        public float maxBackgroundRate = 22f;
        [Range(1f, 40f)]
        public float maxWindDriftRate = 18f;

        [Header("Chilly Glacial Wind & Gust Dynamics")]
        [Tooltip("Base horizontal drift speed towards the glacier (-X = West)")]
        public float baseWindSpeed = -2.8f;
        [Tooltip("Peak additional wind speed during cold gusts")]
        public float gustStrength = -3.4f;
        [Tooltip("Frequency of wind gust oscillations")]
        public float gustFrequency = 0.40f;
        [Tooltip("Base flutter turbulence strength")]
        public float baseTurbulence = 0.55f;
        [Tooltip("Extra flutter turbulence added during wind gusts")]
        public float gustTurbulence = 0.45f;

        [Header("Outdoor Transition & Fade Sync")]
        [Tooltip("Optional light reference (Veranda_Left_Afterglow) to synchronize snow fade with entering veranda")]
        public UnityEngine.Rendering.Universal.Light2D referenceAfterglowLight;
        public float maxReferenceIntensity = 1.75f;

        [Header("Editor Preview")]
        [Tooltip("Keep snowfall active in Editor Scene View without entering Play mode")]
        public bool previewInEditor = true;

        [Header("Manual Master Controls")]
        [Range(0f, 1f)]
        public float manualIntensity = 1.0f;
        public bool useManualIntensity = false;

        private ParticleSystem.EmissionModule _fgEmission;
        private ParticleSystem.EmissionModule _bgEmission;
        private ParticleSystem.EmissionModule _wdEmission;

        private ParticleSystem.VelocityOverLifetimeModule _fgVel;
        private ParticleSystem.VelocityOverLifetimeModule _bgVel;
        private ParticleSystem.VelocityOverLifetimeModule _wdVel;

        private ParticleSystem.NoiseModule _fgNoise;
        private ParticleSystem.NoiseModule _bgNoise;

        private bool _modulesInitialized = false;

        private void Awake()
        {
            InitModules();
        }

        private void OnEnable()
        {
            InitModules();
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

        private void InitModules()
        {
            if (foregroundSnow != null)
            {
                _fgEmission = foregroundSnow.emission;
                _fgVel = foregroundSnow.velocityOverLifetime;
                _fgNoise = foregroundSnow.noise;
            }
            if (backgroundSnow != null)
            {
                _bgEmission = backgroundSnow.emission;
                _bgVel = backgroundSnow.velocityOverLifetime;
                _bgNoise = backgroundSnow.noise;
            }
            if (windDriftSnow != null)
            {
                _wdEmission = windDriftSnow.emission;
                _wdVel = windDriftSnow.velocityOverLifetime;
            }
            _modulesInitialized = true;
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (this == null)
            {
                EditorApplication.update -= EditorUpdate;
                return;
            }
            if (!Application.isPlaying && enabled && gameObject != null && gameObject.activeInHierarchy)
            {
                UpdateSnowSimulation((float)EditorApplication.timeSinceStartup);
            }
        }
#endif

        private void Update()
        {
            if (Application.isPlaying)
            {
                UpdateSnowSimulation(Time.time);
            }
        }

        public void UpdateSnowSimulation(float time)
        {
            if (!_modulesInitialized) InitModules();

            float ratio = 1.0f;

            if (useManualIntensity)
            {
                ratio = manualIntensity;
            }
            else if (Application.isPlaying && referenceAfterglowLight != null)
            {
                ratio = Mathf.Clamp01(referenceAfterglowLight.intensity / Mathf.Max(0.01f, maxReferenceIntensity));
            }
            else if (!Application.isPlaying && !previewInEditor && referenceAfterglowLight != null)
            {
                ratio = Mathf.Clamp01(referenceAfterglowLight.intensity / Mathf.Max(0.01f, maxReferenceIntensity));
            }

            // 1. Calculate dynamic wind gust waveform (organic multi-harmonic gusts)
            float gustPhase1 = Mathf.Sin(time * gustFrequency);
            float gustPhase2 = Mathf.Sin(time * gustFrequency * 2.14f + 0.85f);
            float gustNoise = Mathf.PerlinNoise(time * 0.42f, 13.7f);
            float gustWave = Mathf.Clamp01((gustPhase1 * 0.5f + gustPhase2 * 0.25f + gustNoise * 0.35f));

            float currentWind = (baseWindSpeed + gustWave * gustStrength) * ratio;
            float currentTurbulence = (baseTurbulence + gustWave * gustTurbulence);

            // 2. Foreground Snow (fluttering snowflakes swept by gusts)
            if (foregroundSnow != null)
            {
                _fgEmission = foregroundSnow.emission;
                _fgEmission.rateOverTime = maxForegroundRate * ratio;

                _fgVel = foregroundSnow.velocityOverLifetime;
                _fgVel.enabled = true;
                _fgVel.x = new ParticleSystem.MinMaxCurve(currentWind * 0.75f, currentWind * 1.25f);

                _fgNoise = foregroundSnow.noise;
                _fgNoise.strength = currentTurbulence;

                if (ratio > 0.01f && !foregroundSnow.isPlaying)
                    foregroundSnow.Play();
                else if (ratio <= 0.001f && foregroundSnow.isPlaying)
                    foregroundSnow.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            // 3. Background Snow (distant drifting curtain)
            if (backgroundSnow != null)
            {
                _bgEmission = backgroundSnow.emission;
                _bgEmission.rateOverTime = maxBackgroundRate * ratio;

                _bgVel = backgroundSnow.velocityOverLifetime;
                _bgVel.enabled = true;
                _bgVel.x = new ParticleSystem.MinMaxCurve(currentWind * 0.65f, currentWind * 1.05f);

                _bgNoise = backgroundSnow.noise;
                _bgNoise.strength = currentTurbulence * 0.75f;

                if (ratio > 0.01f && !backgroundSnow.isPlaying)
                    backgroundSnow.Play();
                else if (ratio <= 0.001f && backgroundSnow.isPlaying)
                    backgroundSnow.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            // 4. Wind Drift Spindrift Layer (fast-sweeping icy micro-streaks)
            if (windDriftSnow != null)
            {
                _wdEmission = windDriftSnow.emission;
                _wdEmission.rateOverTime = maxWindDriftRate * ratio;

                _wdVel = windDriftSnow.velocityOverLifetime;
                _wdVel.enabled = true;
                _wdVel.x = new ParticleSystem.MinMaxCurve(currentWind * 1.2f, currentWind * 1.8f);

                if (ratio > 0.01f && !windDriftSnow.isPlaying)
                    windDriftSnow.Play();
                else if (ratio <= 0.001f && windDriftSnow.isPlaying)
                    windDriftSnow.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void SetSnowActive(bool active)
        {
            useManualIntensity = true;
            manualIntensity = active ? 1.0f : 0.0f;
            UpdateSnowSimulation(Time.time);
        }
    }
}
