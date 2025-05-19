using UnityEngine;
using FishNet.Object;

public class DeliveryZone : NetworkBehaviour
{
    [Header("Bina Sistemi")]
    [SerializeField] private Transform buildingRoot;
    [SerializeField] private GameObject[] buildingStages;
    private int currentStage = 0;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[DeliveryZone] Trigger'a bir þey girdi: {other.name}");

        if (!base.IsServerInitialized)
        {
            Debug.LogWarning("[DeliveryZone] Sunucu initialized deðil!");
            return;
        }

        NetworkObject item = other.GetComponent<NetworkObject>();
        if (item == null)
        {
            Debug.LogWarning("[DeliveryZone] NetworkObject bulunamadý!");
            return;
        }

        Debug.Log($"[DeliveryZone] NetworkObject bulundu: {item.name}");

        if (!item.CompareTag("Item"))
        {
            Debug.LogWarning($"[DeliveryZone] Eþya tag'i 'Item' deðil! Tag: {item.tag}");
            return;
        }

        Debug.Log("[DeliveryZone] Tag 'Item' ile eþleþti, iþlem devam ediyor.");

        PlayerController player = item.Owner?.FirstObject?.GetComponent<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[DeliveryZone] Player bulunamadý (Owner null veya yanlýþ atanmýþ).");
            return;
        }

        Debug.Log($"[DeliveryZone] Player bulundu: {player.name}");

        player.DeliverHeldItem();

        Debug.Log("[DeliveryZone] DeliverHeldItem çaðrýldý, þimdi bina ilerletiliyor.");
        AdvanceBuildingStage();
    }

    [Server]
    private void AdvanceBuildingStage()
    {
        Debug.Log($"[AdvanceBuildingStage] Çaðrýldý! Þu anki kat: {currentStage}");

        if (currentStage >= buildingStages.Length)
        {
            Debug.LogWarning("[AdvanceBuildingStage] Tüm katlar tamamlandý.");
            return;
        }

        if (buildingRoot == null)
        {
            Debug.LogError("[AdvanceBuildingStage] BuildingRoot atanmamýþ!");
            return;
        }

        GameObject stagePrefab = buildingStages[currentStage];
        if (stagePrefab == null)
        {
            Debug.LogError($"[AdvanceBuildingStage] buildingStages[{currentStage}] prefab atanmadý!");
            return;
        }

        // Yeni: Her kat için yüksekliði hesapla (currentStage * kat yüksekliði)
        float yOffset = currentStage * 1.0f; // Her kat için 1 birim yukarý
        Vector3 spawnPosition = buildingRoot.position + new Vector3(0, yOffset, 0);

        Debug.Log($"[AdvanceBuildingStage] Kat prefabý instantiate ediliyor: {stagePrefab.name}, Pozisyon: {spawnPosition}");

        // Yeni pozisyonu kullan
        GameObject newStage = Instantiate(stagePrefab, spawnPosition, Quaternion.identity, buildingRoot);
        NetworkObject netObj = newStage.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            Spawn(netObj);
            Debug.Log("[AdvanceBuildingStage] NetworkObject baþarýyla spawn edildi.");
        }
        else
        {
            Debug.LogWarning("[AdvanceBuildingStage] Yeni kat prefabýnda NetworkObject component yok!");
        }

        currentStage++;

        if (currentStage >= buildingStages.Length)
        {
            Debug.Log("Bina tamamlandý!");
        }
    }
}
