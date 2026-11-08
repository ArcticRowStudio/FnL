using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// Trigger hitbox on a breakable boss part (a missile pod, a duct). Player weapons find it
    /// through IPlayerHittable on the struck collider and the hit goes to the part, not the hull.
    /// </summary>
    public class BossPartHitbox : MonoBehaviour, IPlayerHittable
    {
        public System.Action<Vector2> onHit;
        public bool acceptsHits = true;

        public void OnPlayerHit(Vector2 hitPoint)
        {
            if (acceptsHits) onHit?.Invoke(hitPoint);
        }
    }
}
