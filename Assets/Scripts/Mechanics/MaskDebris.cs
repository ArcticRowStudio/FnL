using System.Collections;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Physical detached mask shard that pops off and spins when the RabbitSpider is damaged.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D))]
    public class MaskDebris : MonoBehaviour
    {
        [Tooltip("Lifetime of the mask debris before fading out.")]
        public float lifetime = 1.2f;

        private SpriteRenderer _renderer;
        private Rigidbody2D _rb;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
        }

        public void Launch(Vector2 initialVelocity, float torque)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 1.5f;
            _rb.linearVelocity = initialVelocity;
            _rb.angularVelocity = torque;
            StartCoroutine(FadeAndDestroyRoutine());
        }

        private IEnumerator FadeAndDestroyRoutine()
        {
            yield return new WaitForSeconds(lifetime * 0.5f);
            float elapsed = 0f;
            float fadeDuration = lifetime * 0.5f;
            Color startColor = _renderer != null ? _renderer.color : Color.white;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                if (_renderer != null)
                {
                    float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                    _renderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                }
                yield return null;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
