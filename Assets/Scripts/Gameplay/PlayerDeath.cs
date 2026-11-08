using System.Collections;
using System.Collections.Generic;
using Platformer.Core;
using Platformer.Model;
using UnityEngine;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the player has died.
    /// </summary>
    /// <typeparam name="PlayerDeath"></typeparam>
    public class PlayerDeath : Simulation.Event<PlayerDeath>
    {
        PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        public override void Execute()
        {
            var player = model.player;
            if (player == null) return;
            if (!player.controlEnabled && player.animator != null && player.animator.GetBool("dead")) return;

            if (player.health != null && player.health.IsAlive)
                player.health.Die();

            if (model.virtualCamera != null)
            {
                model.virtualCamera.Follow = null;
                model.virtualCamera.LookAt = null;
            }
            player.controlEnabled = false;

            if (player.audioSource && player.ouchAudio)
                player.audioSource.PlayOneShot(player.ouchAudio);
            if (player.animator != null)
            {
                player.animator.SetTrigger("hurt");
                player.animator.SetBool("dead", true);
            }
            Simulation.Schedule<PlayerSpawn>(2);
        }
    }
}