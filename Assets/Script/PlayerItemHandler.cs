using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerItemHandler : NetworkBehaviour
{
    [Header("References")]
    public Transform itemHoldPoint;
    [SerializeField] private Camera playerCamera;

    [Header("Settings")]
    [SerializeField] private LayerMask pickupLayerMask;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float dropHeightOffset = 0.3f;
    [SerializeField] private float throwForce = 1.5f;
    [SerializeField] private float positionLerpSpeed = 15f;
    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float itemDropDelay = 0.1f;

    private GameObject heldItem = null;
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (heldItem != null)
            SyncHeldItemPosition();
    }

    private void SyncHeldItemPosition()
    {
        if (heldItem == null) return;

        heldItem.transform.position = Vector3.Lerp(
            heldItem.transform.position,
            itemHoldPoint.position,
            Time.deltaTime * positionLerpSpeed
        );

        heldItem.transform.rotation = Quaternion.Slerp(
            heldItem.transform.rotation,
            itemHoldPoint.rotation,
            Time.deltaTime * positionLerpSpeed
        );
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.started || !IsOwner) return;

        if (heldItem == null)
            TryPickupItem();
        else
            TryDropItem();
    }

    private void TryPickupItem()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask))
        {
            NetworkObject itemNetObj = hit.collider.GetComponent<NetworkObject>();
            if (itemNetObj != null && itemNetObj.CompareTag("Item"))
            {
                PickupItemServerRpc(itemNetObj);
            }
        }
    }

    private void TryDropItem()
    {
        if (heldItem != null)
            DropItemServerRpc();
    }

    [ServerRpc]
    private void PickupItemServerRpc(NetworkObject item)
    {
        if (item == null || heldItem != null) return;

        if (item.Owner != null && item.Owner != Owner)
        {
            RejectPickupClientRpc(Owner);
            return;
        }

        if (item.transform.parent != null)
        {
            RejectPickupClientRpc(Owner);
            return;
        }

        item.GiveOwnership(Owner);

        // Make it kinematic so it follows itemHoldPoint
        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        SetCollidersEnabled(item.gameObject, false);
        heldItem = item.gameObject;
        heldItem.transform.SetParent(itemHoldPoint);
        heldItem.transform.localPosition = Vector3.zero;
        heldItem.transform.localRotation = Quaternion.identity;

        SetHeldItemObserversRpc(item);
    }

    [TargetRpc]
    private void RejectPickupClientRpc(NetworkConnection conn)
    {
        Debug.Log("Bu eşyayı alamazsınız!");
    }

    [ObserversRpc]
    private void SetHeldItemObserversRpc(NetworkObject item)
    {
        if (item == null) return;

        heldItem = item.gameObject;
        heldItem.transform.SetParent(itemHoldPoint);
        heldItem.transform.localPosition = Vector3.zero;
        heldItem.transform.localRotation = Quaternion.identity;

        if (heldItem.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        SetCollidersEnabled(heldItem, false);
    }

    [ServerRpc]
    private void DropItemServerRpc()
    {
        if (heldItem == null) return;

        var netObj = heldItem.GetComponent<NetworkObject>();
        if (netObj == null) return;

        // 1. Detach from player
        heldItem.transform.SetParent(null);

        // 2. Move it slightly above ground
        Vector3 dropPos = CalculateDropPosition();
        heldItem.transform.position = dropPos;

        // 3. Enable colliders before physics
        SetCollidersEnabled(heldItem, true);

        // 4. Activate physics
        if (heldItem.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = transform.forward * throwForce;
        }

        // 5. Release network ownership
        netObj.RemoveOwnership();

        // 6. Clear server reference after delay
        StartCoroutine(ClearHeldItemAfterDelay(itemDropDelay));

        // 7. Notify clients to clear and restore
        ClearHeldItemObserversRpc();
    }

    private Vector3 CalculateDropPosition()
    {
        Vector3 startPos = itemHoldPoint.position;
        if (Physics.Raycast(startPos, Vector3.down, out RaycastHit hit, groundCheckDistance * 2f, groundLayer))
        {
            return hit.point + Vector3.up * dropHeightOffset;
        }
        return startPos + Vector3.up * dropHeightOffset;
    }

    private IEnumerator ClearHeldItemAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        heldItem = null;
    }

    [ObserversRpc]
    private void ClearHeldItemObserversRpc()
    {
        if (heldItem != null)
        {
            if (heldItem.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            SetCollidersEnabled(heldItem, true);
            heldItem.transform.SetParent(null);
            heldItem = null;
        }
    }

    private void SetCollidersEnabled(GameObject obj, bool enabled)
    {
        foreach (var col in obj.GetComponentsInChildren<Collider>())
            col.enabled = enabled;
    }

    private void OnDrawGizmosSelected()
    {
        if (itemHoldPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(itemHoldPoint.position, itemHoldPoint.position - Vector3.up * groundCheckDistance);
        }
    }
}
