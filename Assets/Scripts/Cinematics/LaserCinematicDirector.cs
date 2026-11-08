using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using Platformer.Mechanics;

namespace Platformer.Cinematics
{
    /// <summary>
    /// Directs cinematic wide-angle camera framing and screen shake during
    /// laser projector firing and the central bulkhead blast gate opening.
    /// Uses Cinemachine priority blending to seamlessly zoom out to wide shot
    /// and restore player follow camera when complete.
    /// </summary>
    public class LaserCinematicDirector : MonoBehaviour
    {
        public static LaserCinematicDirector Instance { get; private set; }

        [Header("Cinemachine Cameras")]
        [Tooltip("Focus shot on P1 reload mechanism (pistons, cartridge, steam)")]
        public CinemachineCamera projector1ReloadCamera;

        [Tooltip("Wide shot framing P1 (left cannon), player console, and left lock")]
        public CinemachineCamera projector1Camera;

        [Tooltip("Focus shot on P2 reload mechanism (pistons, cartridge, steam)")]
        public CinemachineCamera projector2ReloadCamera;

        [Tooltip("Wide shot framing P2 (right cannon), player console, and right lock")]
        public CinemachineCamera projector2Camera;

        [Tooltip("Wide shot framing the entire central bulkhead gate opening")]
        public CinemachineCamera bulkheadGateCamera;

        [Header("Priorities")]
        public int reloadPriority = 28;
        public int activePriority = 30;
        public int gatePriority = 35;
        public int inactivePriority = 0;

        [Header("Cinematic Shake Settings")]
        public float laserShakeIntensity = 0.28f;
        public float gateShakeIntensity = 0.35f;

        private CinemachineCamera _currentActiveCamera = null;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            ResetAllCameraPriorities();
        }

        private void ResetAllCameraPriorities()
        {
            SetCamPriority(projector1ReloadCamera, inactivePriority);
            SetCamPriority(projector1Camera, inactivePriority);
            SetCamPriority(projector2ReloadCamera, inactivePriority);
            SetCamPriority(projector2Camera, inactivePriority);
            SetCamPriority(bulkheadGateCamera, inactivePriority);
            _currentActiveCamera = null;
        }

        public void FocusProjectorReload(LaserBeamProjectorController projector)
        {
            if (projector == null) return;
            bool isLeft = projector.transform.position.x < 80f;
            CinemachineCamera reloadCam = isLeft ? projector1ReloadCamera : projector2ReloadCamera;

            if (reloadCam != null)
            {
                _currentActiveCamera = reloadCam;
                SetCamPriority(reloadCam, reloadPriority);
                Debug.Log($"[LaserCinematic] Focusing Reload camera on {(isLeft ? "P1 (Left)" : "P2 (Right)")} mechanism!");
            }
            else
            {
                FocusProjector(projector);
            }
        }

        public void FocusProjectorFiring(LaserBeamProjectorController projector)
        {
            if (projector == null) return;
            bool isLeft = projector.transform.position.x < 80f;
            CinemachineCamera wideCam = isLeft ? projector1Camera : projector2Camera;
            CinemachineCamera reloadCam = isLeft ? projector1ReloadCamera : projector2ReloadCamera;

            if (wideCam != null)
            {
                SetCamPriority(wideCam, activePriority);
                _currentActiveCamera = wideCam;
                Debug.Log($"[LaserCinematic] Transitioning to Wide Shot on {(isLeft ? "P1 (Left)" : "P2 (Right)")} for laser blast!");
            }

            if (reloadCam != null)
            {
                SetCamPriority(reloadCam, inactivePriority);
            }
        }

        public void FocusProjector(LaserBeamProjectorController projector)
        {
            if (projector == null) return;

            // P1 is on the left (x < 80), P2 is on the right (x > 80)
            bool isLeft = projector.transform.position.x < 80f;
            CinemachineCamera targetCam = isLeft ? projector1Camera : projector2Camera;

            if (targetCam != null)
            {
                _currentActiveCamera = targetCam;
                SetCamPriority(targetCam, activePriority);
                Debug.Log($"[LaserCinematic] Focusing Wide Shot on {(isLeft ? "P1 (Left)" : "P2 (Right)")} projector!");
            }
        }

        public void TriggerLaserFiringShake(float duration = 1.4f)
        {
            CameraZoomController.TriggerShake(laserShakeIntensity, duration);
        }

        public void ReleaseProjector()
        {
            SetCamPriority(projector1ReloadCamera, inactivePriority);
            SetCamPriority(projector1Camera, inactivePriority);
            SetCamPriority(projector2ReloadCamera, inactivePriority);
            SetCamPriority(projector2Camera, inactivePriority);

            if (_currentActiveCamera != bulkheadGateCamera && _currentActiveCamera != null)
            {
                _currentActiveCamera = null;
                Debug.Log("[LaserCinematic] Released projector cameras. Returning to player view.");
            }
        }

        public void FocusBulkheadGate()
        {
            // Release any active projector camera
            if (_currentActiveCamera != null && _currentActiveCamera != bulkheadGateCamera)
            {
                SetCamPriority(_currentActiveCamera, inactivePriority);
            }

            if (bulkheadGateCamera != null)
            {
                _currentActiveCamera = bulkheadGateCamera;
                SetCamPriority(bulkheadGateCamera, gatePriority);
                CameraZoomController.TriggerShake(gateShakeIntensity, 2.2f);
                Debug.Log("[LaserCinematic] Focusing Wide Shot on Bulkhead Gate opening!");
            }
        }

        public void ReleaseBulkheadGate()
        {
            if (bulkheadGateCamera != null)
            {
                SetCamPriority(bulkheadGateCamera, inactivePriority);
                if (_currentActiveCamera == bulkheadGateCamera)
                {
                    _currentActiveCamera = null;
                }
                Debug.Log("[LaserCinematic] Released Bulkhead Gate camera. Returning to player view.");
            }
        }

        private void SetCamPriority(CinemachineCamera cam, int priority)
        {
            if (cam != null)
            {
                var p = cam.Priority;
                p.Value = priority;
                cam.Priority = p;
            }
        }
    }
}
