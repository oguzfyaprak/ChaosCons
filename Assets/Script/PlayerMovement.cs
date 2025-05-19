using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Hareket Ayarlarý")]
    public float speed = 6f;
    public float jumpSpeed = 6f;
    public float gravity = -9.81f;

    [Header("Kamera Ayarlarý")]
    public Transform cameraHolder;
    public float mouseSensitivity = 2f;
    private float cameraPitch = 0f;

    private CharacterController characterController;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private bool isJumping;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (!base.Owner.IsLocalClient && cameraHolder != null)
        {
            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam != null)
                cam.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!base.Owner.IsLocalClient) return;

        HandleMovement();
        HandleLook();
    }

    private void HandleMovement()
    {
        if (characterController.isGrounded)
        {
            Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
            move = transform.TransformDirection(move);
            velocity = move * speed;

            if (isJumping)
            {
                velocity.y = jumpSpeed;
                isJumping = false;
            }
        }

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleLook()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);
        cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!base.Owner.IsLocalClient) return;
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (!base.Owner.IsLocalClient) return;
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!base.Owner.IsLocalClient) return;
        if (context.started || context.performed)
        {
            isJumping = true;
        }
    }
}
