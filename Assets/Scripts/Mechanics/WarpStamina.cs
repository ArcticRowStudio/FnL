using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// A regenerating stamina gauge. Warping spends it and regeneration pauses for a
    /// moment afterwards, so the gauge has to be managed during a fight.
    /// </summary>
    public class WarpStamina : MonoBehaviour
    {
        /// <summary>
        /// Size of the full gauge.
        /// </summary>
        public float maxStamina = 100f;

        /// <summary>
        /// Stamina restored per second once regeneration resumes.
        /// </summary>
        public float regenPerSecond = 25f;

        /// <summary>
        /// How long regeneration is paused after stamina is spent.
        /// </summary>
        public float regenDelay = 0.7f;

        /// <summary>
        /// Current amount of stamina in the gauge.
        /// </summary>
        public float Current { get; private set; }

        /// <summary>
        /// Current fill of the gauge, in the 0..1 range.
        /// </summary>
        public float Normalized => maxStamina > 0f ? Current / maxStamina : 0f;

        float regenTimer;

        /// <summary>
        /// Is there enough stamina left to pay the given cost?
        /// </summary>
        public bool Has(float amount) => Current >= amount;

        /// <summary>
        /// Pay the given cost, if the gauge can afford it. Returns false when it can't.
        /// </summary>
        public bool TrySpend(float amount)
        {
            if (!Has(amount)) return false;

            Current = Mathf.Max(0f, Current - amount);
            regenTimer = regenDelay;
            return true;
        }

        /// <summary>
        /// Refill the gauge instantly, e.g. on respawn.
        /// </summary>
        public void Refill()
        {
            Current = maxStamina;
            regenTimer = 0f;
        }

        void Awake()
        {
            Current = maxStamina;
        }

        void Update()
        {
            if (regenTimer > 0f)
            {
                regenTimer -= Time.deltaTime;
                return;
            }

            if (Current < maxStamina)
            {
                Current = Mathf.Min(maxStamina, Current + regenPerSecond * Time.deltaTime);
            }
        }
    }
}
