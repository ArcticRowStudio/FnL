using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// A boss whose fight can be put back to its starting state when the player dies, so the
    /// retry from the checkpoint is a fresh fight instead of a half-finished one.
    /// A defeated boss stays defeated; implementations ignore the call then.
    /// </summary>
    public interface IBossEncounter
    {
        void ResetEncounter();
    }

    public static class BossEncounters
    {
        /// <summary>Resets every live boss fight in the loaded scenes.</summary>
        public static void ResetAll()
        {
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
                if (mb is IBossEncounter boss) boss.ResetEncounter();
        }
    }
}
