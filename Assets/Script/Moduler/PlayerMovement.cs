using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    [RequireComponent(typeof(PlayerCore), typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float speed = 6f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float jumpSpeed = 6f;
        [SerializeField] private float gravity = -9.81f;

        private PlayerCore playerCore;
        private Vector2 moveInput;
        private Vector3 velocity;
        private bool isJumping;
        private bool isSprinting;

        private void Awake()
        {
            playerCore = GetComponent<PlayerCore>();
        }

        private void Update()
        {
            if (!playerCore.IsOwner) return;
            HandleMovement();
        }

        private void HandleMovement()
        {
            if (IsGrounded())
            {
                Vector3 move = transform.TransformDirection(new Vector3(moveInput.x, 0, moveInput.y));
                float currentSpeed = isSprinting ? speed * sprintMultiplier : speed;
                velocity = move * currentSpeed;

                if (isJumping)
                {
                    velocity.y = jumpSpeed;
                    isJumping = false;
                }
            }

            velocity.y += gravity * Time.deltaTime;
            playerCore.CharacterController.Move(velocity * Time.deltaTime);
        }

        private bool IsGrounded()
        {
            Ray ray = new(transform.position + Vector3.up * 0.1f, Vector3.down);
            bool hit = Physics.Raycast(ray, hitInfo: out RaycastHit hitInfo, 1.2f);
            Debug.DrawRay(ray.origin, ray.direction * 1.2f, hit ? Color.green : Color.red, 1f);
            return hit;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (playerCore.IsOwner)
                moveInput = context.ReadValue<Vector2>();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (playerCore.IsOwner && context.started && IsGrounded())
                isJumping = true;
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (playerCore.IsOwner)
                isSprinting = context.ReadValueAsButton();
        }
    }
}
