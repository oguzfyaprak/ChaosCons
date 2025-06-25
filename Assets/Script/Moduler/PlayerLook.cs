using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    public class PlayerLook : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private Transform cameraHolder;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float maxPitchAngle = 80f;

        private PlayerCore playerCore;
        private Vector2 lookInput;
        private float cameraPitch = 0f;

        private void Awake()
        {
            playerCore = GetComponent<PlayerCore>();

            if (!playerCore.IsOwner)
            {
                Camera cam = cameraHolder.GetComponentInChildren<Camera>();
                if (cam != null) cam.gameObject.SetActive(false);

                AudioListener audioListener = cameraHolder.GetComponentInChildren<AudioListener>();
                if (audioListener != null) audioListener.enabled = false;
            }
        }

        private void Update()
        {
            if (!playerCore.IsOwner) return;
            HandleLook();
        }

        private void HandleLook()
        {
            transform.Rotate(lookInput.x * mouseSensitivity * Vector3.up);
            cameraPitch -= lookInput.y * mouseSensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch, -maxPitchAngle, maxPitchAngle);
            cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            if (playerCore.IsOwner) lookInput = context.ReadValue<Vector2>();
        }
    }
}