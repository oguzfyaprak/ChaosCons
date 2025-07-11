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

        private NetworkManager networkManager;

        private readonly SyncVar<int> currentStage = new(0);
        private readonly SyncVar<bool> isDamaged = new(false);
        private readonly SyncVar<bool> isSabotageCooldown = new(false);

        private void Awake()
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
                Debug.LogError("❌ NetworkManager bulunamadı! DeliveryZone çalışmaz.");

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
            Debug.Log($"✅ DeliveryZone player ID atandı: {id}");
        }

        public int GetOwnerID() => ownerPlayerID;
        public bool IsDamaged() => isDamaged.Value;
        public bool IsCompleted() => currentStage.Value >= buildingStages.Length;
        public int GetCurrentStage() => currentStage.Value;
        public bool IsSabotageCooldownActive() => isSabotageCooldown.Value;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServerInitialized) return;
            if (!other.CompareTag("Item")) return;

            if (!other.TryGetComponent(out NetworkObject netObj)) return;
            if (!other.TryGetComponent(out ItemProperties itemProps)) return;

            if (itemProps.ownerPlayerID.Value == ownerPlayerID)
            {
                AdvanceBuildingStage();

                if (netObj.IsSpawned)
                {
                    foreach (var comp in netObj.GetComponents<NetworkBehaviour>())
                        comp.enabled = false;

                    networkManager.ServerManager.Despawn(netObj, DespawnType.Destroy);
                }
            }
        }

        [Server]
        private void AdvanceBuildingStage()
        {
            if (IsCompleted()) return;

            currentStage.Value++;
            isDamaged.Value = false;
            SpawnCurrentStage();

            if (IsCompleted())
                RpcCompletionEffects();
        }

        [Server]
        private void SpawnCurrentStage()
        {
            int index = currentStage.Value - 1;
            if (index < 0 || index >= buildingStages.Length) return;

            Vector3 pos = buildingRoot.position + Vector3.up * (index * floorHeight);
            GameObject stage = Instantiate(buildingStages[index], pos, Quaternion.identity);

            if (stage.TryGetComponent(out NetworkObject netObj))
            {
                stage.name = $"Stage{currentStage.Value}";
                networkManager.ServerManager.Spawn(netObj);
            }
        }

        private void OnStageChanged(int prev, int next, bool asServer)
        {
            if (asServer) return;
            Debug.Log($"📶 Client: Bina stage değişti → {prev} → {next}");
        }

        [ObserversRpc]
        private void RpcCompletionEffects()
        {
            Debug.Log("🎉 Bina tamamlandı! Efektler oynatılıyor.");
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
                Debug.Log("❌ Sabotaj yapılamaz: bina hiç kat çıkmamış.");
                return;
            }

            int lastIndex = currentStage.Value - 1;
            string lastStageName = $"Stage{currentStage.Value}";

            GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (var obj in allObjects)
            {
                if (obj.name == lastStageName && obj.transform.position.y == buildingRoot.position.y + (lastIndex * floorHeight))
                {
                    if (obj.TryGetComponent(out NetworkObject netObj))
                    {
                        currentStage.Value--;
                        isDamaged.Value = true;
                        isSabotageCooldown.Value = true;

                        // 5 saniye sonra cooldown'u kaldır
                        Invoke(nameof(ResetSabotageCooldown), 5f);

                        networkManager.ServerManager.Despawn(netObj, DespawnType.Destroy);
                        RpcSetDamageVisual(true);
                        Debug.Log($"💥 Kat silindi ve bina hasarlandı: {lastStageName}");
                        return;
                    }
                }
            }

            Debug.LogWarning($"❌ Sahnede sabotaj için uygun prefab bulunamadı: {lastStageName}");
        }

        [Server]
        private void ResetSabotageCooldown()
        {
            isSabotageCooldown.Value = false;
        }

        [Server]
        public void MarkDamaged()
        {
            isDamaged.Value = true;
            isSabotageCooldown.Value = true;
            Invoke(nameof(ResetSabotageCooldown), 5f);
            RpcSetDamageVisual(true);
            Debug.Log("⚠️ Bina hasarlı olarak işaretlendi.");
        }

        [Server]
        public void Repair()
        {
            if (!isDamaged.Value) return;

            isDamaged.Value = false;
            RpcSetDamageVisual(false);
            Debug.Log("🔧 Bina tamir edildi.");
        }

        [ObserversRpc]
        private void RpcSetDamageVisual(bool hasarliMi)
        {
            Debug.Log($"🎨 Görsel güncellendi → {(hasarliMi ? "HASARLI" : "SAĞLAM")}");
        }
    }
}