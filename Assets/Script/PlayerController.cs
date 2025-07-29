using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using FishNet.Connection;
using Game.Stats; // PlayerStats sınıfın varsa kalacak, yoksa bu satırı sil
using Game.Player; // PlayerRegistry'nin namespace'i

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

        [Header("Combat Settings")]
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private int attackDamage = 20;
        [SerializeField] private LayerMask hitMask;
        [SerializeField] private Animator animator;

        // PlayerRegistry.Register metodu için gerekli olan oyuncu adını tutan değişken
        // Bu değeri gelecekte bir UI'dan alabilirsiniz. Şimdilik sabit bir değer kullanacağız.
        [SerializeField] private string myPlayerName = "DefaultPlayer";

        private readonly SyncVar<float> syncedSpeed = new();

        private CharacterController characterController;
        private Vector2 moveInput;
        private Vector2 lookInput;
        private Vector3 velocity;
        private float cameraPitch;
        private bool isJumping;
        private bool isSprinting;
        private NetworkObject heldItem;
        private PlayerStats stats; // PlayerStats scriptini sağladığında aktif olacak

        private bool _movementEnabled = true;
        private float speedMultiplier = 1f;

        public int PlayerID { get; private set; }
        private static int playerIdCounter = 0;

        public override void OnStartServer()
        {
            base.OnStartServer();
            // PlayerID atamasını sadece sunucu tarafında yap
            PlayerID = playerIdCounter++;

            // PlayerRegistry.Register metoduna 3. parametre olarak playerName'i ekledik
            PlayerRegistry.Register(PlayerID, Owner, myPlayerName);
            Debug.Log($"[SERVER] Player {Owner.ClientId} assigned PlayerID: {PlayerID}, Name: {myPlayerName} on StartServer.");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            // Sunucuda durdurulduğunda (oyuncu bağlantısı kesildiğinde veya sahne değiştiğinde) kaydı sil
            // PlayerRegistry'nin Deregister metodu içinde null kontrolü var.
            PlayerRegistry.Deregister(Owner);
            Debug.Log($"[SERVER] Player {Owner.ClientId} deregistered on StopServer.");
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            stats = GetComponent<PlayerStats>();
            if (stats == null)
                Debug.LogError("❌ PlayerStats componenti bulunamadı! Lütfen PlayerStats scriptini sağlayın veya bu satırı silin.");
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

            if (!_movementEnabled)
            {
                moveInput = Vector2.zero;
                lookInput = Vector2.zero;
                velocity = Vector3.zero;
                isJumping = false;
                isSprinting = false;
                return;
            }

            HandleLook();
            HandleMovement();
            HandleHeldItemPosition();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryAttack();
            }

            float localSpeed = moveInput.magnitude;

            if (animator != null)
                animator.SetFloat("speed", localSpeed);

            if (IsServerInitialized)
                syncedSpeed.Value = localSpeed;
            else
                UpdateSpeedServerRpc(localSpeed);
        }

        private void LateUpdate()
        {
            if (IsOwner) return;

            if (animator != null)
                animator.SetFloat("speed", syncedSpeed.Value);
        }

        [ServerRpc]
        private void UpdateSpeedServerRpc(float speed)
        {
            syncedSpeed.Value = speed;
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
            if (!characterController.enabled)
                return;

            Vector3 move = transform.TransformDirection(new Vector3(moveInput.x, 0, moveInput.y));
            float currentSpeed = (isSprinting ? speed * sprintMultiplier : speed) * speedMultiplier;
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
            if (item == null) return;

            heldItem = item;

            // ✅ Network parenting (FishNet tarafından senkronize edilir)
            heldItem.SetParent(this.NetworkObject);

            // ✅ Görsel olarak hizala
            heldItem.transform.localPosition = itemHoldPoint.localPosition;
            heldItem.transform.localRotation = itemHoldPoint.localRotation;
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

            // ❗ Doğru parent kaldırımı
            if (heldItem.TryGetComponent<NetworkObject>(out var netObj))
                netObj.SetParent((NetworkObject)null);

            heldItem = null;
        }

        private void TryAttack()
        {
            Vector3 origin = cameraHolder.position;
            Vector3 direction = cameraHolder.forward;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, attackRange, hitMask))
            {
                Debug.Log("[CLIENT] Hit " + hit.collider.name);

                if (hit.collider.GetComponentInParent<HealthSystem>() is { } healthSystem)
                {
                    Debug.Log("[CLIENT] Trying to deal damage...");
                    DealDamageServerRpc(healthSystem.NetworkObject);
                }
            }
            else
            {
                Debug.Log("[CLIENT] Attack missed.");
            }

            if (animator != null)
                animator.SetTrigger("Punch");
        }

        [ServerRpc]
        private void DealDamageServerRpc(NetworkObject targetObj)
        {
            Debug.Log("[SERVER] DealDamageServerRpc called.");

            if (targetObj != null && targetObj.TryGetComponent(out HealthSystem healthSystem))
            {
                Debug.Log("[SERVER] Applying damage...");
                healthSystem.ServerTakeDamage(20, base.Owner); // ✅ doğru değişken ismi
            }
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (IsOwner && _movementEnabled) moveInput = context.ReadValue<Vector2>();
            else moveInput = Vector2.zero;
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            if (IsOwner && _movementEnabled) lookInput = context.ReadValue<Vector2>();
            else lookInput = Vector2.zero;
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (IsOwner && _movementEnabled && context.started && IsGrounded())
                isJumping = true;
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (IsOwner && _movementEnabled)
                isSprinting = context.ReadValueAsButton();
            else
                isSprinting = false;
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (!IsOwner || !_movementEnabled || !context.started) return;

            if (heldItem == null)
                TryPickupItem();
            else
                TryDropItem();
        }

        public void SetMovementEnabled(bool enabled)
        {
            Debug.Log($"[{(IsOwner ? "CLIENT" : "SERVER")}] PlayerController.SetMovementEnabled çağrıldı: {enabled}");
            _movementEnabled = enabled;

            if (!enabled)
            {
                moveInput = Vector2.zero;
                lookInput = Vector2.zero;
                velocity = Vector3.zero;
                isJumping = false;
                isSprinting = false;
                Debug.Log($"[{(IsOwner ? "CLIENT" : "SERVER")}] Hareket girdileri ve hız sıfırlandı.");
            }

            if (IsOwner)
            {
                Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !enabled;
            }

            Debug.Log($"[{(IsOwner ? "CLIENT" : "SERVER")}] Movement enabled state set to: {enabled}");
        }

        public void ResetVelocity()
        {
            velocity = Vector3.zero;
            Debug.Log($"[{(IsOwner ? "CLIENT" : "SERVER")}] Velocity sıfırlandı.");
        }

        // === YENİ: Speed Boost Yetenek Fonksiyonları ===
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = multiplier;
        }

        public void ResetSpeedMultiplier()
        {
            speedMultiplier = 1f;
        }
    }
}