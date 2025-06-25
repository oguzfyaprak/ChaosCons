using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;

namespace Game.Player
{
    public class PlayerItemSystem : NetworkBehaviour
    {
        [Header("Item System")]
        [SerializeField] private Transform itemHoldPoint;
        [SerializeField] private float pickupDistance = 3f;
        [SerializeField] private float itemLerpSpeed = 10f;

        [Header("Events")]
        public UnityEvent OnItemPickedUp;
        public UnityEvent OnItemDelivered;

        private readonly SyncVar<int> deliveredItemCount = new();
        private const int winItemCount = 5;

        private PlayerCore playerCore;
        private NetworkObject heldItemNetObj;

        public bool IsHoldingItem => heldItemNetObj != null;

        private void Awake()
        {
            playerCore = GetComponent<PlayerCore>();
        }

        private void Update()
        {
            if (!playerCore.IsOwner) return;
            HandleHeldItem();
        }

        private void HandleHeldItem()
        {
            if (!IsHoldingItem || itemHoldPoint == null) return;

            
            heldItemNetObj.transform.SetPositionAndRotation(Vector3.Lerp(
                heldItemNetObj.transform.position,
                itemHoldPoint.position,
                itemLerpSpeed * Time.deltaTime), Quaternion.Lerp(
                heldItemNetObj.transform.rotation,
                itemHoldPoint.rotation,
                itemLerpSpeed * Time.deltaTime));
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (!playerCore.IsOwner || !context.started) return;

            if (!IsHoldingItem)
                TryPickupItem();
            else
                TryDropItem();
        }

        private void TryPickupItem()
        {
            if (IsHoldingItem) return;

            Camera cam = GetComponentInChildren<Camera>();
            if (cam == null) return;

            Ray ray = new(cam.transform.position, cam.transform.forward);
            Debug.DrawRay(ray.origin, ray.direction * pickupDistance, Color.red, 1f);

            if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance))
            {
                if (hit.collider.TryGetComponent<NetworkObject>(out var itemNetObj))
                {
                    if (!itemNetObj.Owner.IsValid)
                    {
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
            item.transform.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
            while (elapsed < duration)
            {
                if (item == null) yield break;

                item.transform.SetPositionAndRotation(Vector3.Lerp(startPos, itemHoldPoint.position, elapsed / duration), Quaternion.Lerp(startRot, itemHoldPoint.rotation, elapsed / duration));
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (item != null && itemHoldPoint != null)
            {
                item.transform.SetPositionAndRotation(itemHoldPoint.position, itemHoldPoint.rotation);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void PickupItemServerRpc(NetworkObject item)
        {
            if (item == null || item.Owner.IsValid) return;

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
            if (playerCore.IsOwner && TryGetComponent(out PlayerUI ui))
            {
                ui.ShowWinUI();
            }
        }
    }
}