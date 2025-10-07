// 10/7/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.InputSystem;

namespace TMPro.Examples
{
    public class CameraController : MonoBehaviour
    {
        public enum CameraModes { Follow, Isometric, Free }

        private Transform cameraTransform;
        private Transform dummyTarget;

        public Transform CameraTarget;

        public float FollowDistance = 30.0f;
        public float MaxFollowDistance = 100.0f;
        public float MinFollowDistance = 2.0f;

        public float ElevationAngle = 30.0f;
        public float MaxElevationAngle = 85.0f;
        public float MinElevationAngle = 0f;

        public float OrbitalAngle = 0f;

        public CameraModes CameraMode = CameraModes.Follow;

        public bool MovementSmoothing = true;
        public bool RotationSmoothing = false;
        private bool previousSmoothing;

        public float MovementSmoothingValue = 25f;
        public float RotationSmoothingValue = 5.0f;

        public float MoveSensitivity = 2.0f;

        private Vector3 currentVelocity = Vector3.zero;
        private Vector3 desiredPosition;
        private Vector2 mouseDelta;
        private float mouseWheel;

        private InputAction lookAction;
        private InputAction zoomAction;

        void Awake()
        {
            cameraTransform = transform;
            previousSmoothing = MovementSmoothing;

            // Initialize Input Actions
            var playerInput = GetComponent<PlayerInput>();
            lookAction = playerInput.actions["Look"];
            zoomAction = playerInput.actions["Zoom"];
        }

        void Start()
        {
            if (CameraTarget == null)
            {
                dummyTarget = new GameObject("Camera Target").transform;
                CameraTarget = dummyTarget;
            }

            // Lock the cursor to the center of the screen and hide it
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void LateUpdate()
        {
            GetPlayerInput();

            if (CameraTarget != null)
            {
                if (CameraMode == CameraModes.Isometric)
                {
                    desiredPosition = CameraTarget.position + Quaternion.Euler(ElevationAngle, OrbitalAngle, 0f) * new Vector3(0, 0, -FollowDistance);
                }
                else if (CameraMode == CameraModes.Follow)
                {
                    desiredPosition = CameraTarget.position + CameraTarget.TransformDirection(Quaternion.Euler(ElevationAngle, OrbitalAngle, 0f) * (new Vector3(0, 0, -FollowDistance)));
                }

                if (MovementSmoothing)
                {
                    cameraTransform.position = Vector3.SmoothDamp(cameraTransform.position, desiredPosition, ref currentVelocity, MovementSmoothingValue * Time.fixedDeltaTime);
                }
                else
                {
                    cameraTransform.position = desiredPosition;
                }

                if (RotationSmoothing)
                {
                    cameraTransform.rotation = Quaternion.Lerp(cameraTransform.rotation, Quaternion.LookRotation(CameraTarget.position - cameraTransform.position), RotationSmoothingValue * Time.deltaTime);
                }
                else
                {
                    cameraTransform.LookAt(CameraTarget);
                }
            }
        }

        void GetPlayerInput()
        {
            // Get mouse delta from the new Input System
            mouseDelta = lookAction.ReadValue<Vector2>();

            // Update Elevation and Orbital Angles
            ElevationAngle -= mouseDelta.y * MoveSensitivity * Time.deltaTime;
            ElevationAngle = Mathf.Clamp(ElevationAngle, MinElevationAngle, MaxElevationAngle);

            OrbitalAngle += mouseDelta.x * MoveSensitivity * Time.deltaTime;

            // Get zoom input
            mouseWheel = zoomAction.ReadValue<float>();
            FollowDistance -= mouseWheel * 5.0f;
            FollowDistance = Mathf.Clamp(FollowDistance, MinFollowDistance, MaxFollowDistance);
        }
    }
}