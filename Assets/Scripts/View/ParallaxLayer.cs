using UnityEngine;

namespace Platformer.View
{
    /// <summary>
    /// Moves a transform with the main camera at a fraction of the camera's speed, so that
    /// branches of gameobjects standing at different notional depths drift apart as the
    /// camera travels. That drift is the strongest depth cue a flat 2D scene has.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        /// <summary>
        /// How much of the camera's movement this layer copies, per axis. 0 leaves the layer
        /// standing still in world space, so it slides past at full speed - the near
        /// foreground. 1 pins it to the camera so it never appears to move at all - the far
        /// distance. The values in between are what actually read as depth.
        ///
        /// Leave z at 0. Copying camera z would drag the layer along the axis that decides
        /// what draws in front of what, and in a 2D scene the camera never moves on it
        /// anyway, so there is nothing to gain.
        /// </summary>
        public Vector3 movementScale = new Vector3(0.25f, 0.25f, 0f);

        /// <summary>
        /// Anchor to <see cref="referenceCameraPosition"/> instead of wherever the camera is on
        /// the first frame. The layer is then laid out as seen from that fixed camera position,
        /// so it lands in the same place whichever spawn point the level starts from. A layer
        /// that has to line up with the scene, like a horizon behind the deck, needs this.
        /// </summary>
        public bool anchorToReference = false;

        /// <summary>
        /// The camera position the layer's authored position is meant to be seen from.
        /// Only used with <see cref="anchorToReference"/>.
        /// </summary>
        public Vector3 referenceCameraPosition;

        /// <summary>
        /// Where this layer was placed in the scene, and where the camera was looking when
        /// the level opened. Movement is applied as an offset from the pair.
        ///
        /// Scaling the camera position directly, as this did previously, threw the layer's
        /// authored position away on the first frame: whatever you positioned in the scene
        /// view snapped to the origin as soon as you pressed play, which made more than one
        /// parallax layer impossible to lay out.
        /// </summary>
        Vector3 startPosition;
        Vector3 startCameraPosition;

        bool anchored;

        Transform _camera;

        void Awake()
        {
            var main = Camera.main;
            if (main == null)
            {
                Debug.LogWarning($"{name}: no camera tagged MainCamera, parallax disabled.", this);
                enabled = false;
                return;
            }
            _camera = main.transform;
            startPosition = transform.position;
            // Play mode reload is off, so a flag left set by the last session must be cleared here.
            anchored = false;
        }

        void LateUpdate()
        {
            // The camera baseline is taken on the first LateUpdate rather than in Awake
            // because Cinemachine only snaps the camera onto its target once the first frame
            // is already running. Reading it any earlier anchors every layer to wherever the
            // camera happened to be authored, and the whole backdrop starts life shifted by
            // however far that first snap travelled.
            if (!anchored)
            {
                startCameraPosition = anchorToReference ? referenceCameraPosition : _camera.position;
                anchored = true;
            }

            var travelled = _camera.position - startCameraPosition;
            transform.position = startPosition + Vector3.Scale(travelled, movementScale);
        }
    }
}
