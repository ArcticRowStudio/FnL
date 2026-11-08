using Platformer.Core;
using Platformer.Model;
using TMPro;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// A trigger that moves the player's respawn point here when the player walks through it.
    /// The last checkpoint touched wins, so the route order does not matter.
    /// The respawn position is the optional <see cref="respawnPoint"/> child, or this transform.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("Where the player reappears. Leave empty to use this object's position.")]
        public Transform respawnPoint;

        [Tooltip("Optional sound played the first time this checkpoint becomes active.")]
        public AudioClip activateAudio;

        [Tooltip("Show a short floating 'CHECKPOINT' label when activated.")]
        public bool showLabel = true;

        public static Checkpoint Active { get; private set; }

        // Domain reload is off in this project, so statics survive between play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = null;

        PlatformerModel model;

        Transform SpawnTransform => respawnPoint != null ? respawnPoint : transform;

        void Awake()
        {
            model = Simulation.GetModel<PlatformerModel>();
            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (Active == this) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.health == null || !player.health.IsAlive) return;
            Activate(player);
        }

        void Activate(PlayerController player)
        {
            Active = this;
            model.spawnPoint = SpawnTransform;

            if (activateAudio != null && player.audioSource != null)
                player.audioSource.PlayOneShot(activateAudio);
            if (showLabel)
                CheckpointLabel.Spawn(player.transform.position + Vector3.up * 2.2f);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
            if (box != null)
                Gizmos.DrawCube(transform.TransformPoint(box.offset), Vector3.Scale(box.size, transform.lossyScale));
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 1f);
            var p = SpawnTransform.position;
            Gizmos.DrawWireSphere(p, 0.4f);
            Gizmos.DrawLine(p, p + Vector3.up * 1.5f);
        }
    }

    /// <summary>Placeholder feedback: a world-space label that rises and fades.</summary>
    class CheckpointLabel : MonoBehaviour
    {
        const float Duration = 1.4f;
        TextMeshPro text;
        float t;
        Vector3 start;

        public static void Spawn(Vector3 position) => Spawn(position, "CHECKPOINT", new Color(0.55f, 1f, 0.6f, 1f));

        public static void Spawn(Vector3 position, string message, Color color)
        {
            var go = new GameObject("FloatingLabel");
            go.transform.position = position;
            var label = go.AddComponent<CheckpointLabel>();
            label.text = go.AddComponent<TextMeshPro>();
            label.text.text = message;
            label.text.fontSize = 4f;
            label.text.alignment = TextAlignmentOptions.Center;
            label.text.color = color;
            label.text.sortingOrder = 500;
            label.start = position;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = t / Duration;
            transform.position = start + Vector3.up * (k * 0.8f);
            var c = text.color;
            c.a = 1f - Mathf.Clamp01((k - 0.5f) * 2f);
            text.color = c;
            if (t >= Duration) Destroy(gameObject);
        }
    }
}
