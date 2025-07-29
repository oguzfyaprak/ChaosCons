using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;
using System;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerItemHandler : NetworkBehaviour
{
    [Header("References")]
    public Transform itemHoldPoint; // El veya tutma noktası
    [SerializeField] private Camera playerCamera;

    [Header("Settings")]
    [SerializeField] private LayerMask pickupLayerMask;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float dropHeightOffset = 0.3f;
    [SerializeField] private float throwForce = 1.5f;
    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float itemDropDelay = 0.1f;

    private GameObject heldItem = null;
    private NetworkObject itemHoldPointNetObj;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
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

        Ray ray = new(playerCamera.transform.position, playerCamera.transform.forward);
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

        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        SetCollidersEnabled(item.gameObject, false);
        heldItem = item.gameObject;

        // 🔄 FishNet parenting (NetworkObject → NetworkObject)
        item.SetParent(this.NetworkObject);

        // 🔧 Görsel hizalama
        heldItem.transform.localPosition = itemHoldPoint.localPosition;
        heldItem.transform.localRotation = itemHoldPoint.localRotation;

        SetHeldItemObserversRpc(item);
    }

    [TargetRpc]
    private void RejectPickupClientRpc(NetworkConnection conn)
    {
        if (conn is null)
            throw new ArgumentNullException(nameof(conn));

        Debug.Log("Bu eşyayı alamazsınız!");
    }

    [ObserversRpc]
    private void SetHeldItemObserversRpc(NetworkObject item)
    {
        if (item == null) return;

        heldItem = item.gameObject;

        // 🔄 Multiplayer parenting
        item.SetParent(this.NetworkObject);

        // 🔧 Görsel hizalama
        heldItem.transform.localPosition = itemHoldPoint.localPosition;
        heldItem.transform.localRotation = itemHoldPoint.localRotation;

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
        if (!heldItem.TryGetComponent<NetworkObject>(out var netObj)) return;

        netObj.SetParent((NetworkObject)null);

        Vector3 dropPos = CalculateDropPosition();
        heldItem.transform.position = dropPos;

        SetCollidersEnabled(heldItem, true);

        if (heldItem.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = transform.forward * throwForce;
        }

        netObj.RemoveOwnership();
        StartCoroutine(ClearHeldItemAfterDelay(itemDropDelay));
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

            if (heldItem.TryGetComponent<NetworkObject>(out var netObj))
                netObj.SetParent((NetworkObject)null);

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
