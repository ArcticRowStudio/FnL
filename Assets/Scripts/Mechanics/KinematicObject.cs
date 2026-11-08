using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Implements game physics for some in game entity.
    /// </summary>
    public class KinematicObject : MonoBehaviour
    {
        /// <summary>
        /// The minimum normal (dot product) considered suitable for the entity sit on.
        /// </summary>
        public float minGroundNormalY = .65f;

        /// <summary>
        /// A custom gravity coefficient applied to this entity.
        /// </summary>
        public float gravityModifier = 1f;

        /// <summary>
        /// Ledges no taller than this are climbed rather than walked into.
        ///
        /// The horizontal cast in <see cref="PerformMovement"/> treats anything steeper than
        /// <see cref="minGroundNormalY"/> as a wall and cancels all horizontal speed, so
        /// without this the decorative bumps in the level art, which run up to about 0.12
        /// units, stop the entity dead mid stride. Set to zero to go back to that.
        /// </summary>
        public float maxStepHeight = 0.2f;

        /// <summary>
        /// The current velocity of the entity.
        /// </summary>
        public Vector2 velocity;

        /// <summary>
        /// Sideways shove from the world (a boss's rotor wash), added on top of the walking speed,
        /// which otherwise overwrites velocity.x every step. It dies away by itself; see Push.
        /// </summary>
        public Vector2 externalPush;

        /// <summary>
        /// Is the entity currently sitting on a surface?
        /// </summary>
        /// <value></value>
        public bool IsGrounded { get; private set; }

        protected Vector2 targetVelocity;
        protected Vector2 groundNormal;
        protected Rigidbody2D body;
        protected ContactFilter2D contactFilter;
        protected RaycastHit2D[] hitBuffer = new RaycastHit2D[16];
        protected List<Collider2D> overlapBuffer = new List<Collider2D>();
        protected bool suspendGravity = false;
        protected bool skipPhysicsStep = false;

        protected const float minMoveDistance = 0.001f;
        protected const float shellRadius = 0.01f;

        /// <summary>
        /// How far the entity may sit inside level geometry before it is pushed back out. Resting
        /// contact already reads as about 0.012 of overlap, so anything tighter shoves entities off
        /// the ground they stand on.
        /// </summary>
        protected const float penetrationTolerance = 0.02f;

        /// <summary>
        /// How fast a push from <see cref="Push"/> dies away, in units per second squared.
        /// </summary>
        protected const float ExternalPushDecay = 25f;


        /// <summary>
        /// Bounce the object's vertical velocity.
        /// </summary>
        /// <param name="value"></param>
        public void Bounce(float value)
        {
            velocity.y = value;
        }

        /// <summary>
        /// Bounce the objects velocity in a direction.
        /// </summary>
        /// <param name="dir"></param>
        public void Bounce(Vector2 dir)
        {
            velocity.y = dir.y;
            velocity.x = dir.x;
        }

        /// <summary>
        /// Shoves the object sideways; call it every frame the push should last. The stronger of
        /// overlapping pushes wins.
        /// </summary>
        public void Push(float x)
        {
            if (Mathf.Abs(x) > Mathf.Abs(externalPush.x)) externalPush.x = x;
        }

        /// <summary>
        /// Teleport to some position.
        /// </summary>
        /// <param name="position"></param>
        public void Teleport(Vector3 position)
        {
            body.position = position;
            velocity *= 0;
            body.linearVelocity *= 0;
        }

        protected virtual void OnEnable()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        protected virtual void OnDisable()
        {
            body.bodyType = RigidbodyType2D.Dynamic;
        }

        protected virtual void Start()
        {
            contactFilter.useTriggers = false;
            contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            contactFilter.useLayerMask = true;
        }

        protected virtual void Update()
        {
            targetVelocity = Vector2.zero;
            ComputeVelocity();
        }

        protected virtual void ComputeVelocity()
        {

        }

        protected virtual void FixedUpdate()
        {
            if (skipPhysicsStep) return;

            Depenetrate();

            if (!suspendGravity)
            {
                //if already falling, fall faster than the jump speed, otherwise use normal gravity.
                if (velocity.y < 0)
                    velocity += gravityModifier * Physics2D.gravity * Time.deltaTime;
                else
                    velocity += Physics2D.gravity * Time.deltaTime;
            }

            velocity.x = targetVelocity.x + externalPush.x;
            externalPush = Vector2.MoveTowards(externalPush, Vector2.zero, ExternalPushDecay * Time.deltaTime);

            // Whether we were standing on something going into this step: the step over
            // check below needs it, and IsGrounded is about to be recomputed from scratch.
            var wasGrounded = IsGrounded;

            IsGrounded = false;

            var deltaPosition = velocity * Time.deltaTime;

            var moveAlongGround = new Vector2(groundNormal.y, -groundNormal.x);

            var move = moveAlongGround * deltaPosition.x;

            if (wasGrounded) StepOverLedge(move);

            PerformMovement(move, false);

            move = Vector2.up * deltaPosition.y;

            PerformMovement(move, true);

        }

        /// <summary>
        /// Pushes the entity back out of level geometry it has ended up inside.
        ///
        /// <see cref="StartedInside"/> stops movement from driving the entity deeper, but nothing
        /// in the cast based movement ever moves it back out. A rabbit spider dropping into the
        /// gap between the small platform and the pillar left of the boss arena, which is 0.005
        /// narrower than its capsule, wedges in far enough to need this.
        /// Only static colliders count: separating from other bodies is not this entity's job.
        /// </summary>
        void Depenetrate()
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                overlapBuffer.Clear();
                body.Overlap(contactFilter, overlapBuffer);
                var moved = false;
                foreach (var other in overlapBuffer)
                {
                    if (other.attachedRigidbody != null && other.attachedRigidbody.bodyType != RigidbodyType2D.Static) continue;
                    var separation = body.Distance(other);
                    if (!separation.isValid || !separation.isOverlapped || -separation.distance <= penetrationTolerance) continue;
                    body.position += separation.normal * separation.distance;
                    moved = true;
                }
                if (!moved) break;
            }
        }

        /// <summary>
        /// Is this hit a collider the entity already overlaps, and is not moving any further into?
        ///
        /// A cast that starts inside a collider reports it at distance zero, with a normal that has
        /// little to do with the direction of travel: falling beside a wall reports the wall.
        /// Treated as a block, zero minus the shell radius is a negative distance, so the entity
        /// steps backwards, which for a move away from the wall means deeper into it. It then does
        /// the same every step, rising up inside the wall while its fall speed keeps building,
        /// which is how enemies sank into the level after jumping.
        /// </summary>
        static bool StartedInside(RaycastHit2D hit, Vector2 move)
        {
            // Not quite zero, so a wall normal that is a hair off horizontal still counts as
            // parallel to a fall.
            return hit.distance <= 0f && Vector2.Dot(move.normalized, hit.normal) > -0.1f;
        }

        /// <summary>
        /// Is something in the way of this move that we cannot simply walk up?
        /// </summary>
        bool BlockedBy(Vector2 move)
        {
            var count = body.Cast(move, contactFilter, hitBuffer, move.magnitude + shellRadius);
            for (var i = 0; i < count; i++)
            {
                if (IgnoreCollision(hitBuffer[i]) || StartedInside(hitBuffer[i], move)) continue;
                if (hitBuffer[i].normal.y > minGroundNormalY) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Lifts the entity onto a low ledge in front of it so the horizontal move that
        /// follows runs along the top instead of into the face.
        ///
        /// This tries lifts and keeps the first one that actually frees the path, rather than
        /// measuring the ledge and rising by that much. Measuring does not survive contact
        /// with the level: its outline rounds off corners, so the height read in front of the
        /// entity is not the height needed to clear the contact, and rising by it leaves the
        /// entity still blocked, dropping back down and trying again forever.
        /// </summary>
        void StepOverLedge(Vector2 move)
        {
            if (maxStepHeight <= 0f) return;
            if (move.magnitude <= minMoveDistance) return;
            if (!BlockedBy(move)) return;

            var original = body.position;
            const int attempts = 4;

            for (var i = 1; i <= attempts; i++)
            {
                var lift = maxStepHeight * i / attempts;

                // Room to rise? Only a surface facing down is a ceiling: the ground we stand
                // on can report itself at zero distance.
                body.position = original;
                var overhead = body.Cast(Vector2.up, contactFilter, hitBuffer, lift + shellRadius);
                var blocked = false;
                for (var j = 0; j < overhead; j++)
                {
                    if (IgnoreCollision(hitBuffer[j])) continue;
                    if (hitBuffer[j].normal.y >= 0f) continue;
                    blocked = true;
                    break;
                }
                if (blocked) break;

                body.position = original + Vector2.up * lift;
                if (BlockedBy(move)) continue;

                // Cleared: keep the lift and let the caller move along the top.
                groundNormal = Vector2.up;
                IsGrounded = true;
                return;
            }

            // A wall, or an enemy: too tall to be a step, so stay put and let
            // PerformMovement stop us against it.
            body.position = original;
        }

        void PerformMovement(Vector2 move, bool yMovement)
        {
            var distance = move.magnitude;

            if (distance > minMoveDistance)
            {
                //check if we hit anything in current direction of travel
                var count = body.Cast(move, contactFilter, hitBuffer, distance + shellRadius);
                for (var i = 0; i < count; i++)
                {
                    if (IgnoreCollision(hitBuffer[i]) || StartedInside(hitBuffer[i], move))
                        continue;

                    var currentNormal = hitBuffer[i].normal;

                    //is this surface flat enough to land on?
                    if (currentNormal.y > minGroundNormalY)
                    {
                        IsGrounded = true;
                        // if moving up, change the groundNormal to new surface normal.
                        if (yMovement)
                        {
                            groundNormal = currentNormal;
                            currentNormal.x = 0;
                        }
                    }
                    if (IsGrounded)
                    {
                        //how much of our velocity aligns with surface normal?
                        var projection = Vector2.Dot(velocity, currentNormal);
                        if (projection < 0)
                        {
                            //slower velocity if moving against the normal (up a hill).
                            velocity = velocity - projection * currentNormal;
                        }
                    }
                    else
                    {
                        //We are airborne, but hit something, so cancel vertical up and horizontal velocity.
                        velocity.x *= 0;
                        velocity.y = Mathf.Min(velocity.y, 0);
                    }
                    //remove shellDistance from actual move distance.
                    var modifiedDistance = hitBuffer[i].distance - shellRadius;
                    distance = modifiedDistance < distance ? modifiedDistance : distance;
                }
            }
            body.position = body.position + move.normalized * distance;
        }

        protected virtual bool IgnoreCollision(RaycastHit2D hit)
        {
            return false;
        }
    }
}