using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Object;
using FishNet.Object.Synchronizing;
using TMPro;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 6f;
    [SerializeField] private float jumpSpeed = 6f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Camera Settings")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxPitchAngle = 80f;
    private float cameraPitch = 0f;

    [Header("Item System")]
    [SerializeField] private Transform itemHoldPoint;
    [SerializeField] private float pickupDistance = 3f;
    [SerializeField] private float throwForce = 2f;
    private NetworkObject heldItemNetObj;
    private GameObject heldItem => heldItemNetObj != null ? heldItemNetObj.gameObject : null;
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;

    private CharacterController characterController;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private bool isJumping;

    private int deliveredItemCount = 0;
    private const int winItemCount = 5;

    public string PlayerName { get; private set; } // Normal deðiþken, SyncVar deðil!

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (base.IsOwner)
        {
            string nameFromPrefs = PlayerPrefs.GetString("PlayerName", "Player");
            SetPlayerNameServerRpc(nameFromPrefs);
        }
    }

    [ServerRpc]
    private void SetPlayerNameServerRpc(string name)
    {
        SetPlayerNameObserversRpc(name); // Server aldýktan sonra tüm clientlara gönderiyor
    }

    [ObserversRpc]
    private void SetPlayerNameObserversRpc(string name)
    {
        PlayerName = name;
    }


    #region Initialization
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (!base.Owner.IsLocalClient)
        {
            DisableCamera();
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (cameraHolder == null)
        {
            Debug.LogError("CameraHolder is not assigned!", this);
            enabled = false;
            return;
        }

        if (itemHoldPoint == null)
        {
            Debug.LogError("ItemHoldPoint is not assigned!", this);
        }
    }

    private void DisableCamera()
    {
        if (cameraHolder != null)
        {
            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam != null) cam.gameObject.SetActive(false);

            AudioListener audioListener = cameraHolder.GetComponentInChildren<AudioListener>();
            if (audioListener != null) audioListener.enabled = false;
        }
    }
    #endregion

    private void Update()
    {
        if (!base.IsOwner) return;

        HandleMovement();
        HandleLook();
        HandleHeldItem();
    }

    #region Movement
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
        cameraPitch = Mathf.Clamp(cameraPitch, -maxPitchAngle, maxPitchAngle);
        cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
    }
    #endregion

    #region Item Handling
    private void HandleHeldItem()
    {
        if (heldItem == null || itemHoldPoint == null) return;

        if (heldItem.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
        }

        Vector3 targetPos = itemHoldPoint.position;
        Quaternion targetRot = itemHoldPoint.rotation;

        if (Vector3.Distance(lastSentPosition, targetPos) > 0.01f ||
            Quaternion.Angle(lastSentRotation, targetRot) > 1f)
        {
            heldItem.transform.position = targetPos;
            heldItem.transform.rotation = targetRot;

            lastSentPosition = targetPos;
            lastSentRotation = targetRot;
        }
    }

    private void TryPickupItem()
    {
        if (heldItem != null) return;

        Camera cam = cameraHolder.GetComponentInChildren<Camera>();
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance))
        {
            NetworkObject itemNetObj = hit.collider.GetComponent<NetworkObject>();
            if (itemNetObj != null && !itemNetObj.Owner.IsValid)
            {
                PickupItemServerRpc(itemNetObj);
            }
        }
    }

    private void TryDropItem()
    {
        if (heldItem == null) return;
        DropItemServerRpc();
    }

    [ServerRpc]
    private void PickupItemServerRpc(NetworkObject item)
    {
        if (item == null || item.Owner.IsValid) return;

        // Store original kinematic state
        if (item.TryGetComponent<ItemProperties>(out var itemProps))
        {
            itemProps.OriginalKinematicState = item.GetComponent<Rigidbody>().isKinematic;
        }

        item.GiveOwnership(base.Owner);
        SetHeldItem(item);
    }

    [ServerRpc]
    private void DropItemServerRpc()
    {
        if (heldItemNetObj == null) return;

        // Restore original kinematic state
        if (heldItemNetObj.TryGetComponent<ItemProperties>(out var itemProps))
        {
            if (heldItemNetObj.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = itemProps.OriginalKinematicState;
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = transform.forward * throwForce;
                }
            }
        }

        heldItemNetObj.RemoveOwnership();
        ClearHeldItem();
    }

    [ObserversRpc]
    private void SetHeldItem(NetworkObject item)
    {
        heldItemNetObj = item;

        if (heldItem != null && itemHoldPoint != null)
        {
            heldItem.transform.SetParent(itemHoldPoint);
            heldItem.transform.localPosition = Vector3.zero;
            heldItem.transform.localRotation = Quaternion.identity;
        }
    }

    [ObserversRpc]
    private void ClearHeldItem()
    {
        if (heldItem != null)
        {
            heldItem.transform.SetParent(null);
        }
        heldItemNetObj = null;
    }

    public bool HasHeldItem() => heldItem != null;

    [ServerRpc]
    public void DeliverHeldItem()
    {
        if (heldItemNetObj == null) return;

        heldItemNetObj.Despawn();
        ClearHeldItem();

        deliveredItemCount++;

        Debug.Log($"[PlayerController] Teslim edilen item sayýsý: {deliveredItemCount}");

        if (deliveredItemCount >= winItemCount)
        {
            RpcShowWinMessage();
        }
    }

    [ObserversRpc]
    private void RpcShowWinMessage()
    {
        if (!base.IsOwner) return; 

        Debug.Log(" Kazandýn! ");

        
    }


    #endregion

    #region Input Handling
    public void OnMove(InputAction.CallbackContext context)
    {
        if (!base.IsOwner) return;
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (!base.IsOwner) return;
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!base.IsOwner || !context.started) return;
        isJumping = true;
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!base.IsOwner || !context.started) return;

        if (heldItem == null)
        {
            TryPickupItem();
        }
        else
        {
            TryDropItem();
        }
    }
    #endregion
}

// Helper class for item properties
public class ItemProperties : MonoBehaviour
{
    [HideInInspector] public bool OriginalKinematicState;
}