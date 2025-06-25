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

        [Header("Tamamlama Efektleri")]
        [SerializeField] private ParticleSystem completionEffect;
        [SerializeField] private AudioClip completionSound;

        private readonly SyncVar<int> currentStage = new SyncVar<int>();
        private NetworkManager networkManager;

        private void Awake()
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
                Debug.LogError(" NetworkManager bulunamadı! DeliveryZone çalışmayacak.");

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
            Debug.Log($" DeliveryZone assigned to player ID {id}");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServerInitialized) return;
            if (!other.CompareTag("Item")) return;

            if (!other.TryGetComponent(out NetworkObject netObj)) return;
            if (!other.TryGetComponent(out ItemProperties itemProps)) return;

            Debug.Log($" DeliveryZone owner: {ownerPlayerID}, Item owner: {itemProps.ownerPlayerID.Value}");

            if (itemProps.ownerPlayerID.Value == ownerPlayerID)
            {
                Debug.Log(" AdvanceBuildingStage çağrılıyor");
                AdvanceBuildingStage();

                if (netObj != null && netObj.IsSpawned)
                {
                    // Component'leri devre dışı bırak (gerekirse)
                    foreach (var comp in netObj.GetComponents<NetworkBehaviour>())
                        comp.enabled = false;

                    networkManager.ServerManager.Despawn(netObj, FishNet.Object.DespawnType.Destroy);
                    Debug.Log($" Item despawn edildi: {netObj.name}");
                }
                else
                {
                    Debug.LogWarning($" Despawn edilmek istenen obje zaten despawn edilmiş veya null: {netObj?.name}");
                }
            }
            else
            {
                Debug.Log($" Yanlış bölge: Item owner {itemProps.ownerPlayerID.Value} -> Zone owner {ownerPlayerID}");
            }
        }

        [Server]
        private void AdvanceBuildingStage()
        {
            if (currentStage.Value >= buildingStages.Length) return;

            currentStage.Value++;
            Debug.Log($" Bina stage ilerledi: {currentStage.Value}");

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
                Debug.Log($" Kat spawn edildi: {netObj.name}, ObjectID: {netObj.ObjectId}, IsGlobal: {netObj.IsGlobal}");
            }
            else
            {
                Debug.LogError(" NetworkObject eksik prefab!");
            }
        }

        private void OnStageChanged(int prev, int next, bool asServer)
        {
            if (asServer) return;

            Debug.Log($" Client stage değişti: {prev} -> {next}");
            // UI güncellemeleri, sesler, efektler burada yapılabilir
        }

        [ObserversRpc]
        private void RpcCompletionEffects()
        {
            Debug.Log(" Completion effects played on all clients");

            if (completionEffect != null)
                Instantiate(completionEffect, transform.position, Quaternion.identity);

            if (completionSound != null)
                AudioSource.PlayClipAtPoint(completionSound, transform.position);
        }
    }
}
