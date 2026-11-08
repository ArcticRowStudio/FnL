using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// A part blown off a boss: tumbles down trailing smoke and bursts when it reaches the floor.
    /// Runs on its own so the boss interrupting its coroutines cannot leave it hanging in the air.
    /// </summary>
    public class BossWreckage : MonoBehaviour
    {
        public float floorY;
        public float lifetime = 4f;
        public Color burstColor = new Color(1f, 0.5f, 0.15f);

        float _age;

        void Update()
        {
            _age += Time.deltaTime;

            if (Random.value < 0.6f)
                ProjectileDebris.SpawnSpark(transform.position, new Color(0.2f, 0.2f, 0.22f, 0.7f), 1, 1.2f, 0.25f, 0.45f);
            if (Random.value < 0.25f)
                ProjectileDebris.SpawnSpark(transform.position, burstColor, 1, 3f);

            if (transform.position.y <= floorY + 0.3f || _age >= lifetime) Burst();
        }

        void Burst()
        {
            CameraZoomController.TriggerShake(0.5f, 0.25f);
            ExpandingRing.Spawn(transform.position, burstColor, 3f, 0.3f);
            ProjectileDebris.SpawnSpark(transform.position, burstColor, 24, 9f);
            ProjectileDebris.SpawnSpark(transform.position, new Color(0.25f, 0.25f, 0.27f, 0.8f), 10, 3f, 0.3f, 0.55f);
            Destroy(gameObject);
        }
    }
}
