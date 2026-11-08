using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Something the player's weapons can strike that is not an EnemyController with a Health,
    /// such as a boss's escort drones and missiles. These use trigger colliders, which the
    /// projectile otherwise flies straight through.
    /// </summary>
    public interface IPlayerHittable
    {
        void OnPlayerHit(Vector2 hitPoint);
    }
}
