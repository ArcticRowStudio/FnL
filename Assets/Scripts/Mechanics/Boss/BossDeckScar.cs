using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// A gouge left on the deck where a boss came down: a streak that glows hot and cools to soot,
    /// with patches of burning wreckage along it that flicker and smoke for a while, then die out.
    /// </summary>
    public class BossDeckScar : MonoBehaviour
    {
        public float coolTime = 3f;

        SpriteRenderer streak;
        float age;

        static readonly Color Hot = new Color(1f, 0.55f, 0.2f, 0.9f);
        static readonly Color Soot = new Color(0.06f, 0.05f, 0.05f, 0.75f);

        /// <param name="fromX">Where the skid started.</param>
        /// <param name="toX">Where it stopped.</param>
        /// <param name="deckY">Top of the deck surface.</param>
        /// <param name="fires">Burning patches spread along the scar.</param>
        public static BossDeckScar Spawn(float fromX, float toX, float deckY, int fires, float thickness = 0.35f)
        {
            float x0 = Mathf.Min(fromX, toX), x1 = Mathf.Max(fromX, toX);
            float length = Mathf.Max(1.2f, x1 - x0 + 2.5f);

            var go = new GameObject("BossDeckScar");
            // Just in front of the deck art (same sorting order, nearer the camera), behind the player.
            go.transform.position = new Vector3((x0 + x1) * 0.5f, deckY - thickness * 0.35f, -0.01f);
            var scar = go.AddComponent<BossDeckScar>();
            scar.streak = go.AddComponent<SpriteRenderer>();
            scar.streak.sprite = VfxSprites.Glow;
            scar.streak.sortingOrder = 3;
            scar.streak.color = Hot;
            go.transform.localScale = new Vector3(length, thickness, 1f);

            for (int i = 0; i < fires; i++)
            {
                float u = fires == 1 ? 0.5f : (float)i / (fires - 1);
                var fire = new GameObject("DeckFire").AddComponent<BossDeckFire>();
                fire.transform.position = new Vector3(Mathf.Lerp(x0, x1, u) + Random.Range(-0.6f, 0.6f), deckY + 0.05f, 0f);
                fire.lifetime = Random.Range(18f, 30f);
                fire.size = Random.Range(0.7f, 1.2f);
            }
            return scar;
        }

        void Update()
        {
            age += Time.deltaTime;
            streak.color = Color.Lerp(Hot, Soot, Mathf.Clamp01(age / coolTime));
        }
    }

    /// <summary>A patch of burning wreckage: flickering flame and a smoke column, fading out at the end.</summary>
    public class BossDeckFire : MonoBehaviour
    {
        public float lifetime = 24f;
        public float size = 1f;
        float age;

        void Update()
        {
            age += Time.deltaTime;
            float strength = Mathf.Clamp01((lifetime - age) / 4f);  // dies down over its last 4 s
            if (strength <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 p = transform.position;
            if (Random.value < 0.7f * strength)
            {
                Color flame = Color.Lerp(new Color(1f, 0.35f, 0.08f), new Color(1f, 0.85f, 0.3f), Random.value);
                ProjectileDebris.Spawn(p + new Vector3(Random.Range(-0.35f, 0.35f) * size, 0f, 0f), flame,
                    Random.Range(0.18f, 0.32f) * size, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(1.2f, 2.4f)), 0.9f, 5);
            }
            if (Random.value < 0.18f * strength)
            {
                ProjectileDebris.Spawn(p + new Vector3(Random.Range(-0.3f, 0.3f), 0.6f * size, 0f),
                    new Color(0.15f, 0.15f, 0.17f, 0.6f), Random.Range(0.35f, 0.55f) * size,
                    new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1.0f, 1.8f)), 0.35f, 5);
            }
        }
    }
}
