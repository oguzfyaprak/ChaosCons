using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game.Player; // Eğer PlayerController burada ise
// using FishNet.Example.Scened; // Artık gerekli değilse kaldırabilirsin

namespace Game.Building
{
    public class DeliveryZone : NetworkBehaviour
    {
        [Header("Bina Ayarları")]
        [SerializeField] private int ownerPlayerID;
        [SerializeField] private Transform buildingRoot;
        [SerializeField] private GameObject[] buildingStages;
        [SerializeField] private float floorHeight = 1.5f;

        [Header("Tamamlama Efektleri")]
        [SerializeField] private ParticleSystem completionEffect;
        [SerializeField] private AudioClip completionSound;

        private readonly SyncVar<int> currentStage = new(); //  SyncVar yerine SyncVar<int>

        [Server]
        public void SetOwnerID(int id)
        {
            ownerPlayerID = id;
            Debug.Log($"DeliveryZone assigned to player ID {id}");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsValidCollision(other)) return;

            NetworkObject item = other.GetComponent<NetworkObject>();

            // Oyuncuyu çarpan collider'ın parent'larından bul
            PlayerController player = other.GetComponentInParent<PlayerController>();

            if (player != null && player.PlayerID == ownerPlayerID && player.IsHoldingItem)
            {
                player.DeliverHeldItem();
                AdvanceBuildingStage();
            }
        }

        private bool IsValidCollision(Collider other)
        {
            if (!base.IsServerInitialized) return false;
            if (!other.CompareTag("Item")) return false;
            if (!other.TryGetComponent<NetworkObject>(out var netObj)) return false;
            return netObj.Owner != null;
        }

        [Server]
        private void AdvanceBuildingStage()
        {
            if (currentStage.Value >= buildingStages.Length) return;
            if (buildingRoot == null) return;

            GameObject stagePrefab = buildingStages[currentStage.Value];
            if (stagePrefab == null) return;

            Vector3 spawnPosition = buildingRoot.position + Vector3.up * (currentStage.Value * floorHeight);
            GameObject newStage = Instantiate(stagePrefab, spawnPosition, Quaternion.identity, buildingRoot);

            if (newStage.TryGetComponent<NetworkObject>(out var netObj))
            {
                Spawn(netObj);
            }

            currentStage.Value++;

            if (currentStage.Value >= buildingStages.Length)
            {
                RpcPlayCompletionEffects();
            }
        }

        [ObserversRpc]
        private void RpcPlayCompletionEffects()
        {
            if (completionEffect != null)
                Instantiate(completionEffect, transform.position, Quaternion.identity);

            if (completionSound != null)
                AudioSource.PlayClipAtPoint(completionSound, transform.position);
        }
    }
}