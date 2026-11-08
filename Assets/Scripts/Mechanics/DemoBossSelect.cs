using Platformer.Core;
using Platformer.Mechanics.Boss;
using Platformer.Model;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Jumps straight to a boss fight, for people evaluating the demo who will not play the whole
    /// route. Each jump unlocks what the route would have unlocked first, moves the player to the
    /// checkpoint before the fight and makes that checkpoint the respawn point.
    /// The title screen shows a BOSS SELECT button while this component is in the scene and
    /// <see cref="showInTitle"/> is on.
    /// </summary>
    public class DemoBossSelect : MonoBehaviour
    {
        public bool showInTitle = true;

        [Header("Quadpod")]
        public QuadpodBossAI quadpod;
        public PowerSwitch quadpodSwitch;
        public Checkpoint quadpodCheckpoint;

        [Header("Lour")]
        public LourBossAI lour;
        public Checkpoint lourCheckpoint;

        [Header("VTOL")]
        public VtolBossAI vtol;
        public RadomeShutterController radomeShutter;
        public Checkpoint vtolCheckpoint;

        public bool HasQuadpod => quadpod != null && quadpodCheckpoint != null;
        public bool HasLour => lour != null && lourCheckpoint != null;
        public bool HasVtol => vtol != null && vtolCheckpoint != null;

        public void JumpToQuadpod()
        {
            if (!HasQuadpod) return;
            if (quadpodSwitch != null && !quadpodSwitch.Activated) quadpodSwitch.Activate();
            else if (quadpod.IsLocked) quadpod.Unlock();
            PlaceAt(quadpodCheckpoint);
        }

        public void JumpToLour()
        {
            if (!HasLour) return;
            PlaceAt(lourCheckpoint);
        }

        public void JumpToVtol()
        {
            if (!HasVtol) return;
            PlaceAt(vtolCheckpoint);
            if (radomeShutter != null && !radomeShutter.IsOpen) radomeShutter.SetState(true);
            vtol.TriggerEncounterFromSwitch();
        }

        static void PlaceAt(Checkpoint checkpoint)
        {
            var model = Simulation.GetModel<PlatformerModel>();
            var player = model.player;
            if (player == null) return;
            Transform spot = checkpoint.respawnPoint != null ? checkpoint.respawnPoint : checkpoint.transform;
            model.spawnPoint = spot;
            player.Teleport(spot.position);
            if (player.health != null) player.health.ResetHP();
            if (model.virtualCamera != null)
            {
                model.virtualCamera.Follow = player.transform;
                model.virtualCamera.PreviousStateIsValid = false;
            }
        }
    }
}
