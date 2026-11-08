using UnityEngine;
using Unity.Cinemachine;

namespace Platformer.Cinematics
{
    /// <summary>
    /// Dynamically controls the Cinemachine camera lens zoom and target offset
    /// as the player walks across the Right Veranda bridge/observation deck.
    /// When walking towards the tip of the bridge (overlooking the sea),
    /// the camera smoothly draws back into an expansive wide shot revealing the fleet of ships!
    /// Purely camera-driven with no artwork alteration.
    /// </summary>
    [ExecuteAlways]
    public class VerandaBridgeCameraController : MonoBehaviour
    {
        [Header("Player Reference")]
        public Transform playerTransform;

        [Header("Bridge Walk Range (World X)")]
        [Tooltip("World X coordinate where wide-angle zoom begins (e.g. midway on deck)")]
        public float bridgeStartX = 236.0f;
        [Tooltip("World X coordinate at the tip of the observation deck")]
        public float bridgeEndX = 262.0f;

        [Header("Standard Framing (Near Door)")]
        public float normalOrthoSize = 9.50f;
        public Vector3 normalTargetOffset = new Vector3(5.0f, 1.8f, 0f);

        [Header("Wide Shot Framing (At Bridge Tip)")]
        [Tooltip("Wide-angle lens size to reveal full ocean, sky, and tall ships")]
        public float wideOrthoSize = 14.50f;
        [Tooltip("Shifts camera focus eastward towards the ships on the horizon")]
        public Vector3 wideTargetOffset = new Vector3(10.0f, 0.5f, 0f);

        [Header("Smooth Damping")]
        public float smoothSpeed = 6.0f;

        private CinemachineCamera _cinemachineCamera;
        private CinemachinePositionComposer _positionComposer;

        private float _currentOrthoSize;
        private Vector3 _currentTargetOffset;

        private void Awake()
        {
            _cinemachineCamera = GetComponent<CinemachineCamera>();
            _positionComposer = GetComponent<CinemachinePositionComposer>();
            _currentOrthoSize = normalOrthoSize;
            _currentTargetOffset = normalTargetOffset;
            FindPlayer();
        }

        private void OnEnable()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            if (playerTransform == null || playerTransform.GetComponent<Platformer.Mechanics.PlayerController>() == null)
            {
                var pc = FindAnyObjectByType<Platformer.Mechanics.PlayerController>();
                if (pc != null)
                {
                    playerTransform = pc.transform;
                }
                else
                {
                    var p = GameObject.Find("Player");
                    if (p != null) playerTransform = p.transform;
                }
            }
        }

        private void Update()
        {
            if (_cinemachineCamera == null) _cinemachineCamera = GetComponent<CinemachineCamera>();
            if (_positionComposer == null) _positionComposer = GetComponent<CinemachinePositionComposer>();

            if (playerTransform == null || playerTransform.GetComponent<Platformer.Mechanics.PlayerController>() == null)
            {
                FindPlayer();
                if (playerTransform == null) return;
            }

            // Calculate normalized progress t along the bridge deck [0.0 = door, 1.0 = bridge tip]
            float playerX = playerTransform.position.x;
            float range = bridgeEndX - bridgeStartX;
            float t = Mathf.Abs(range) > 0.001f ? Mathf.Clamp01((playerX - bridgeStartX) / range) : 0f;

            // Smoothstep curve for elegant cinematic acceleration and deceleration
            float smoothT = t * t * (3f - 2f * t);

            float targetOrtho = Mathf.Lerp(normalOrthoSize, wideOrthoSize, smoothT);
            Vector3 targetOffset = Vector3.Lerp(normalTargetOffset, wideTargetOffset, smoothT);

            float dt = Application.isPlaying ? Time.deltaTime : 0.02f;
            _currentOrthoSize = Mathf.MoveTowards(_currentOrthoSize, targetOrtho, dt * smoothSpeed);
            _currentTargetOffset = Vector3.MoveTowards(_currentTargetOffset, targetOffset, dt * (smoothSpeed * 1.5f));

            // Apply to Cinemachine Lens
            if (_cinemachineCamera != null)
            {
                var lens = _cinemachineCamera.Lens;
                lens.OrthographicSize = _currentOrthoSize;
                _cinemachineCamera.Lens = lens;
            }

            // Apply to Position Composer
            if (_positionComposer != null)
            {
                _positionComposer.TargetOffset = _currentTargetOffset;
            }

            // Direct sync to Camera.main if this virtual camera is currently active
            if (_cinemachineCamera != null && _cinemachineCamera.Priority.Value > 0)
            {
                Platformer.Mechanics.CameraZoomController.IsCinematicActive = true;
                if (Camera.main != null)
                {
                    var ppc = Camera.main.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
                    if (ppc != null && ppc.enabled) ppc.enabled = false;
                    Camera.main.orthographicSize = _currentOrthoSize;
                }
            }
        }
    }
}
