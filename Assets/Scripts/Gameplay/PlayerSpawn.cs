using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the player is spawned after dying.
    /// </summary>
    public class PlayerSpawn : Simulation.Event<PlayerSpawn>
    {
        PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        public override void Execute()
        {
            var player = model.player;
            // Retrying from a checkpoint means a fresh boss fight, not the half-finished one.
            Platformer.Mechanics.Boss.BossEncounters.ResetAll();

            player.collider2d.enabled = true;
            player.controlEnabled = false;
            if (player.audioSource && player.respawnAudio)
                player.audioSource.PlayOneShot(player.respawnAudio);
            player.health.ResetHP();
            player.ResetInvincibility();
            if (player.Stamina != null) player.Stamina.Refill();
            player.RefillAmmo();
            player.Teleport(model.spawnPoint.transform.position);
            player.PlaySpawnEffect();
            player.jumpState = PlayerController.JumpState.Grounded;
            player.animator.SetBool("dead", false);
            model.virtualCamera.Follow = player.transform;
            model.virtualCamera.LookAt = player.transform;
            // Cut straight to the respawn point instead of panning across the map.
            model.virtualCamera.PreviousStateIsValid = false;
            Simulation.Schedule<EnablePlayerInput>(2f);
        }
    }
}