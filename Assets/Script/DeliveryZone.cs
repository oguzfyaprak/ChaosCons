using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Managing;

namespace Game.Building
{
    public class DeliveryZone : NetworkBehaviour
    {
        [Header("Bina Ayarları")]
        [SerializeField] private int ownerPlayerID;
        [SerializeField] private Transform buildingRoot;
        [SerializeField] private GameObject[] buildingStages;
        [SerializeField] private float floorHeight = 1.5f;
        [SerializeField] private float sizeFactor = 1.0f; // Sabotaj süresine etki edecek faktör

        [Header("Tamamlama Efektleri")]
        [SerializeField] private ParticleSystem completionEffect;
        [SerializeField] private AudioClip completionSound;

        private readonly SyncVar<int> currentStage = new SyncVar<int>();
        private NetworkManager networkManager;

        private bool isDamaged = false;

        private void Awake()
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
                Debug.LogError("NetworkManager bulunamadı! DeliveryZone çalışmayacak.");

            currentStage.OnChange += OnStageChanged;
        }

        private void OnDestroy()
        {
            currentStage.OnChange -= OnStageChanged;
        }

        [Server]
        public void SetOwnerID(int id)
        {
            ownerPlayerID = id;
            Debug.Log($"DeliveryZone assigned to player ID {id}");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServerInitialized) return;
            if (!other.CompareTag("Item")) return;

            if (!other.TryGetComponent(out NetworkObject netObj)) return;
            if (!other.TryGetComponent(out ItemProperties itemProps)) return;

            Debug.Log($"DeliveryZone owner: {ownerPlayerID}, Item owner: {itemProps.ownerPlayerID.Value}");

            if (itemProps.ownerPlayerID.Value == ownerPlayerID)
            {
                AdvanceBuildingStage();

                if (netObj != null && netObj.IsSpawned)
                {
                    foreach (var comp in netObj.GetComponents<NetworkBehaviour>())
                        comp.enabled = false;

                    networkManager.ServerManager.Despawn(netObj, DespawnType.Destroy);
                    Debug.Log($"Item despawn edildi: {netObj.name}");
                }
            }
            else
            {
                Debug.Log($"Yanlış bölge: Item owner {itemProps.ownerPlayerID.Value} -> Zone owner {ownerPlayerID}");
            }
        }

        [Server]
        private void AdvanceBuildingStage()
        {
            if (currentStage.Value >= buildingStages.Length) return;

            currentStage.Value++;
            isDamaged = false; // Yeni katla birlikte onarılmış sayılır
            SpawnCurrentStage();

            if (currentStage.Value >= buildingStages.Length)
            {
                RpcCompletionEffects();
            }
        }

        [Server]
        private void SpawnCurrentStage()
        {
            Vector3 pos = buildingRoot.position + Vector3.up * ((currentStage.Value - 1) * floorHeight);
            GameObject stage = Instantiate(buildingStages[currentStage.Value - 1], pos, Quaternion.identity);

            if (stage.TryGetComponent(out NetworkObject netObj))
            {
                networkManager.ServerManager.Spawn(netObj);
                stage.name = $"Stage{currentStage.Value}";
            }
            else
            {
                Debug.LogError("NetworkObject eksik prefab!");
            }
        }

        private void OnStageChanged(int prev, int next, bool asServer)
        {
            if (asServer) return;
            Debug.Log($"Client stage değişti: {prev} -> {next}");
        }

        [ObserversRpc]
        private void RpcCompletionEffects()
        {
            Debug.Log("Completion effects played on all clients");

            if (completionEffect != null)
                Instantiate(completionEffect, transform.position, Quaternion.identity);

            if (completionSound != null)
                AudioSource.PlayClipAtPoint(completionSound, transform.position);
        }

        [Server]
        public void ApplySabotage()
        {
            if (currentStage.Value <= 0)
            {
                Debug.Log("No stage to sabotage.");
                return;
            }

            int lastIndex = currentStage.Value - 1;
            string lastStageName = $"Stage{lastIndex + 1}";

            Transform lastStage = buildingRoot.Find(lastStageName);
            if (lastStage != null && lastStage.TryGetComponent(out NetworkObject netObj))
            {
                currentStage.Value--;
                isDamaged = true;
                networkManager.ServerManager.Despawn(netObj, DespawnType.Destroy);
                Debug.Log($"Stage sabotaged and despawned: {lastStageName}");
            }
            else
            {
                Debug.LogWarning($"Sabotaj için stage bulunamadı: {lastStageName}");
            }
        }

        [Server]
        public void Repair()
        {
            isDamaged = false;
            Debug.Log("Bina onarıldı.");
        }

        public bool IsDamaged()
        {
            return isDamaged;
        }

        public float GetSizeFactor()
        {
            return sizeFactor;
        }

        public int GetOwnerID()
        {
            return ownerPlayerID;
        }

        public bool IsCompleted()
        {
            return currentStage.Value >= buildingStages.Length;
        }
    }
}
