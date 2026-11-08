using System.Collections;
using System.Collections.Generic;
using Platformer.Gameplay;
using UnityEngine;
using static Platformer.Core.Simulation;

namespace Platformer.Mechanics
{
    /// <summary>
    /// A simple controller for enemies. Provides movement control over a patrol path.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnemyController : MonoBehaviour
    {
        public PatrolPath path;
        public AudioClip ouch;

        internal PatrolPath.Mover mover;
        internal AnimationController control;
        internal Collider2D _collider;
        internal AudioSource _audio;
        SpriteRenderer spriteRenderer;

        public Bounds Bounds => _collider.bounds;

        protected virtual void Awake()
        {
            control = GetComponent<AnimationController>();
            _collider = GetComponent<Collider2D>();
            _audio = GetComponent<AudioSource>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            var player = collision.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                var ev = Schedule<PlayerEnemyCollision>();
                ev.player = player;
                ev.enemy = this;
            }
        }

        protected virtual void Update()
        {
            if (path != null)
            {
                if (mover == null) mover = path.CreateMover(control.maxSpeed * 0.5f);
                control.move.x = Mathf.Clamp(mover.Position.x - transform.position.x, -1, 1);
            }
        }

        /// <summary>
        /// Checks whether the enemy can receive damage from the given attacker and hit position.
        /// Allows specialized enemies like the Shield Robot to block attacks from the front.
        /// </summary>
        public virtual bool CanTakeDamage(Vector2 hitPosition, Vector2 attackerPosition)
        {
            return true;
        }

        /// <summary>
        /// True while the enemy plays its own death sequence. Weapons schedule EnemyDeath the moment
        /// HP reaches zero, which would otherwise destroy a boss before its defeat sequence runs.
        /// </summary>
        public virtual bool DefersDeath => false;

        /// <summary>
        /// Checks whether an overwatch round (IncomingRound) is stopped outright. The round comes out
        /// of the screen rather than across the level, so it has no side to judge the way
        /// CanTakeDamage judges an attacker's position.
        /// </summary>
        public virtual bool BlocksRangedHit(Vector2 hitPosition)
        {
            return false;
        }

        /// <summary>
        /// Called when an incoming attack was successfully deflected/blocked.
        /// </summary>
        public virtual void OnBlockedHit(Vector2 hitPosition, Vector2 attackerPosition)
        {
        }

        /// <summary>
        /// Called when the player stomps the enemy and it survives the hit.
        /// </summary>
        public virtual void OnStomped()
        {
        }

    }
}