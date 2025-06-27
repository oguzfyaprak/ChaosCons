using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;


namespace Game.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float speed = 6f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float jumpSpeed = 6f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Camera Settings")]
        [SerializeField] private Transform cameraHolder;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float maxPitchAngle = 80f;

        [Header("Item Settings")]
        [SerializeField] private Transform itemHoldPoint;
        [SerializeField] private float pickupDistance = 3f;
        [SerializeField] private float itemLerpSpeed = 10f;

        private CharacterController characterController;
        private Vector2 moveInput;
        private Vector2 lookInput;
        private Vector3 velocity;
        private float cameraPitch;
        private bool isJumping;
        private bool isSprinting;
        private NetworkObject heldItem;
       

        public int PlayerID { get; private set; }
        private static int playerIdCounter = 0;

        public override void OnStartServer()
        {
            base.OnStartServer();
            PlayerID = playerIdCounter++;
            PlayerRegistry.Register(PlayerID, Owner); // ← burası önemli!
            Debug.Log($"[SERVER] Player {Owner.ClientId} assigned PlayerID: {PlayerID}");

        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                DisableCameraAndAudio();
            }
        }

        private void DisableCameraAndAudio()
        {
            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam != null)
                cam.gameObject.SetActive(false);

            AudioListener listener = cameraHolder.GetComponentInChildren<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }

        private void Update()
        {
            if (!IsOwner) return;

            HandleLook();
            HandleMovement();
            HandleHeldItemPosition();
        }

        private void HandleLook()
        {
            transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);
            cameraPitch -= lookInput.y * mouseSensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch, -maxPitchAngle, maxPitchAngle);
            cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            Vector3 move = transform.TransformDirection(new Vector3(moveInput.x, 0, moveInput.y));
            float currentSpeed = isSprinting ? speed * sprintMultiplier : speed;
            velocity.x = move.x * currentSpeed;
            velocity.z = move.z * currentSpeed;

            if (IsGrounded())
            {
                if (isJumping)
                {
                    velocity.y = jumpSpeed;
                    isJumping = false;
                }
            }

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        private bool IsGrounded()
        {
            return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 1.2f);
        }

        private void HandleHeldItemPosition()
        {
            if (heldItem == null) return;

            heldItem.transform.position = Vector3.Lerp(
                heldItem.transform.position,
                itemHoldPoint.position,
                itemLerpSpeed * Time.deltaTime);

            heldItem.transform.rotation = Quaternion.Lerp(
                heldItem.transform.rotation,
                itemHoldPoint.rotation,
                itemLerpSpeed * Time.deltaTime);
        }

        private void TryPickupItem()
        {
            if (heldItem != null) return;

            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance))
            {
                if (hit.collider.CompareTag("Item") &&
                    hit.collider.TryGetComponent<NetworkObject>(out var netObj) &&
                    !netObj.Owner.IsValid)
                {
                    PickupItemServerRpc(netObj);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void PickupItemServerRpc(NetworkObject item)
        {
            if (item == null || item.Owner.IsValid) return;

            item.GiveOwnership(Owner);

            if (item.TryGetComponent<ItemProperties>(out var props))
            {
                props.ownerPlayerID.Value = PlayerID;
                if (item.TryGetComponent<Rigidbody>(out var rb))
                {
                    props.OriginalKinematicState = rb.isKinematic;
                    rb.isKinematic = true;
                }
            }

            SetHeldItemObserversRpc(item);
        }

        [ObserversRpc]
        private void SetHeldItemObserversRpc(NetworkObject item)
        {
            heldItem = item;
            heldItem.transform.SetParent(itemHoldPoint);
        }

        private void TryDropItem()
        {
            if (heldItem == null) return;
            DropItemServerRpc();
        }

        [ServerRpc]
        private void DropItemServerRpc()
        {
            if (heldItem == null) return;

            if (heldItem.TryGetComponent<ItemProperties>(out var props) &&
                heldItem.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = props.OriginalKinematicState;
                props.ownerPlayerID.Value = -1;
            }

            heldItem.RemoveOwnership();
            ClearHeldItemObserversRpc();
        }

        [ObserversRpc]
        private void ClearHeldItemObserversRpc()
        {
            if (heldItem == null) return;
            heldItem.transform.SetParent(null);
            heldItem = null;
        }

        // INPUT HANDLERS
        public void OnMove(InputAction.CallbackContext context)
        {
            if (IsOwner) moveInput = context.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            if (IsOwner) lookInput = context.ReadValue<Vector2>();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (IsOwner && context.started && IsGrounded())
                isJumping = true;
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (IsOwner)
                isSprinting = context.ReadValueAsButton();
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (!IsOwner || !context.started) return;

            if (heldItem == null)
                TryPickupItem();
            else
                TryDropItem();
        }
    }
}
