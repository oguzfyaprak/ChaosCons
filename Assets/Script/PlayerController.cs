using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
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
        [SerializeField] private float groundCheckDistance = 0.2f;

        [Header("Camera Settings")]
        [SerializeField] private Transform cameraHolder;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float maxPitchAngle = 80f;

        [Header("Item System")]
        [SerializeField] private Transform itemHoldPoint;
        [SerializeField] private float pickupDistance = 3f;
        [SerializeField] private float throwForce = 2f;
        [SerializeField] private float itemLerpSpeed = 10f;

        [Header("UI References")]
        [SerializeField] private GameObject winUI;

        [Header("Events")]
        public UnityEvent OnItemPickedUp;
        public UnityEvent OnItemDelivered;

        private NetworkObject heldItemNetObj;
        private float cameraPitch = 0f;
        private CharacterController characterController;
        private Vector2 moveInput;
        private Vector2 lookInput;
        private Vector3 velocity;
        private bool isJumping;
        private bool isSprinting;

        private readonly SyncVar<int> deliveredItemCount = new();
        private const int winItemCount = 5;

        public int PlayerID { get; private set; }
        public string PlayerName { get; private set; }
        public bool IsHoldingItem => heldItemNetObj != null;

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (base.IsOwner)
            {
                string nameFromPrefs = PlayerPrefs.GetString("PlayerName", "Player");
                int idFromPrefs = PlayerPrefs.GetInt("PlayerID", 0);
                SetPlayerInfoServerRpc(nameFromPrefs, idFromPrefs);
            }
            else
            {
                DisableCamera();
            }
        }
        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"[SERVER] Player {Owner.ClientId} assigned PlayerID: {PlayerID}");
        }

        [ServerRpc]
        private void SetPlayerInfoServerRpc(string name, int id)
        {
            SetPlayerInfoObserversRpc(name, id);
        }

        [ObserversRpc]
        private void SetPlayerInfoObserversRpc(string name, int id)
        {
            PlayerName = name;
            PlayerID = id;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void DisableCamera()
        {
            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam != null) cam.gameObject.SetActive(false);

            AudioListener audioListener = cameraHolder.GetComponentInChildren<AudioListener>();
            if (audioListener != null) audioListener.enabled = false;
        }

        private void Update()
        {
            if (!base.IsOwner) return;

            HandleMovement();
            HandleLook();
            HandleHeldItem();
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
            characterController.Move(velocity * Time.deltaTime);
        }

        private bool IsGrounded()
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.1f, Vector3.down);
            bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, 1.2f);
            Debug.DrawRay(ray.origin, ray.direction * 1.2f, hit ? Color.green : Color.red, 1f);
            return hit;
        }

        private void HandleLook()
        {
            transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);
            cameraPitch -= lookInput.y * mouseSensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch, -maxPitchAngle, maxPitchAngle);
            cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
        }

        private void HandleHeldItem()
        {
            if (!IsHoldingItem || itemHoldPoint == null) return;

            heldItemNetObj.transform.position = Vector3.Lerp(
                heldItemNetObj.transform.position,
                itemHoldPoint.position,
                itemLerpSpeed * Time.deltaTime);

            heldItemNetObj.transform.rotation = Quaternion.Lerp(
                heldItemNetObj.transform.rotation,
                itemHoldPoint.rotation,
                itemLerpSpeed * Time.deltaTime);
        }

        private void TryPickupItem()
        {
            if (IsHoldingItem) return;

            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam == null) return;

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            Debug.DrawRay(ray.origin, ray.direction * pickupDistance, Color.red, 1f); // Debug çizgisi

            if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance))
            {
                Debug.Log($"Raycast hit: {hit.collider.name}"); // Hangi objeye çarptığını logla

                if (hit.collider.TryGetComponent<NetworkObject>(out var itemNetObj))
                {
                    Debug.Log($"Found NetworkObject - Owner: {itemNetObj.Owner}, IsValid: {itemNetObj.Owner.IsValid}");

                    // Sahipsiz eşyaları al
                    if (!itemNetObj.Owner.IsValid)
                    {
                        Debug.Log("Attempting to pickup item");
                        StartCoroutine(PickupItemWithAnimation(itemNetObj));
                    }
                }
            }
        }

        private IEnumerator PickupItemWithAnimation(NetworkObject item)
        {
            PickupItemServerRpc(item);
            OnItemPickedUp?.Invoke();

            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 startPos = item.transform.position;
            Quaternion startRot = item.transform.rotation;

            while (elapsed < duration)
            {
                if (item == null) yield break;

                item.transform.position = Vector3.Lerp(startPos, itemHoldPoint.position, elapsed / duration);
                item.transform.rotation = Quaternion.Lerp(startRot, itemHoldPoint.rotation, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Son pozisyonu garantiye al
            if (item != null && itemHoldPoint != null)
            {
                item.transform.position = itemHoldPoint.position;
                item.transform.rotation = itemHoldPoint.rotation;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void PickupItemServerRpc(NetworkObject item)
        {
            if (item == null || item.Owner.IsValid) return;

            Debug.Log($"Server picking up item: {item.name}");

            // Item'ın orijinal kinematic durumunu kaydet
            if (item.TryGetComponent<Rigidbody>(out var rb))
            {
                if (!item.TryGetComponent<ItemProperties>(out var itemProps))
                {
                    itemProps = item.gameObject.AddComponent<ItemProperties>();
                }
                itemProps.OriginalKinematicState = rb.isKinematic;
                rb.isKinematic = true;
            }

            item.GiveOwnership(base.Owner);
            SetHeldItem(item);
        }

        private void TryDropItem()
        {
            if (!IsHoldingItem) return;
            DropItemServerRpc();
        }

        [ServerRpc]
        private void DropItemServerRpc()
        {
            if (!IsHoldingItem) return;

            if (heldItemNetObj.TryGetComponent<ItemProperties>(out var itemProps) &&
                heldItemNetObj.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = itemProps.OriginalKinematicState;
            }

            heldItemNetObj.RemoveOwnership();
            ClearHeldItem();
        }

        [ObserversRpc]
        private void SetHeldItem(NetworkObject item)
        {
            heldItemNetObj = item;
            if (IsHoldingItem)
            {
                heldItemNetObj.transform.SetParent(itemHoldPoint);
            }
        }

        [ObserversRpc]
        private void ClearHeldItem()
        {
            if (IsHoldingItem)
            {
                heldItemNetObj.transform.SetParent(null);
                heldItemNetObj = null;
            }
        }

        [Server]
        public void DeliverHeldItem()
        {
            if (!IsHoldingItem) return;

            heldItemNetObj.Despawn();
            ClearHeldItem();

            deliveredItemCount.Value++;
            OnItemDelivered?.Invoke();

            if (deliveredItemCount.Value >= winItemCount)
            {
                RpcShowWinMessage();
            }
        }

        [ObserversRpc]
        private void RpcShowWinMessage()
        {
            if (base.IsOwner && winUI != null)
            {
                winUI.SetActive(true);
            }
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (base.IsOwner) moveInput = context.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            if (base.IsOwner) lookInput = context.ReadValue<Vector2>();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (base.IsOwner && context.started && IsGrounded())
                isJumping = true;
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (!base.IsOwner || !context.started) return;

            if (!IsHoldingItem)
                TryPickupItem();
            else
                TryDropItem();
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (base.IsOwner)
                isSprinting = context.ReadValueAsButton();
        }
    }
}