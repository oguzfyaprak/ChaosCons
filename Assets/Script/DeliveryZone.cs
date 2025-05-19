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
        Debug.Log($"[DeliveryZone] Trigger'a bir �ey girdi: {other.name}");

        if (!base.IsServerInitialized)
        {
            Debug.LogWarning("[DeliveryZone] Sunucu initialized de�il!");
            return;
        }

        NetworkObject item = other.GetComponent<NetworkObject>();
        if (item == null)
        {
            Debug.LogWarning("[DeliveryZone] NetworkObject bulunamad�!");
            return;
        }

        Debug.Log($"[DeliveryZone] NetworkObject bulundu: {item.name}");

        if (!item.CompareTag("Item"))
        {
            Debug.LogWarning($"[DeliveryZone] E�ya tag'i 'Item' de�il! Tag: {item.tag}");
            return;
        }

        Debug.Log("[DeliveryZone] Tag 'Item' ile e�le�ti, i�lem devam ediyor.");

        PlayerController player = item.Owner?.FirstObject?.GetComponent<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[DeliveryZone] Player bulunamad� (Owner null veya yanl�� atanm��).");
            return;
        }

        Debug.Log($"[DeliveryZone] Player bulundu: {player.name}");

        player.DeliverHeldItem();

        Debug.Log("[DeliveryZone] DeliverHeldItem �a�r�ld�, �imdi bina ilerletiliyor.");
        AdvanceBuildingStage();
    }

    [Server]
    private void AdvanceBuildingStage()
    {
        Debug.Log($"[AdvanceBuildingStage] �a�r�ld�! �u anki kat: {currentStage}");

        if (currentStage >= buildingStages.Length)
        {
            Debug.LogWarning("[AdvanceBuildingStage] T�m katlar tamamland�.");
            return;
        }

        if (buildingRoot == null)
        {
            Debug.LogError("[AdvanceBuildingStage] BuildingRoot atanmam��!");
            return;
        }

        GameObject stagePrefab = buildingStages[currentStage];
        if (stagePrefab == null)
        {
            Debug.LogError($"[AdvanceBuildingStage] buildingStages[{currentStage}] prefab atanmad�!");
            return;
        }

        // Yeni: Her kat i�in y�ksekli�i hesapla (currentStage * kat y�ksekli�i)
        float yOffset = currentStage * 1.0f; // Her kat i�in 1 birim yukar�
        Vector3 spawnPosition = buildingRoot.position + new Vector3(0, yOffset, 0);

        Debug.Log($"[AdvanceBuildingStage] Kat prefab� instantiate ediliyor: {stagePrefab.name}, Pozisyon: {spawnPosition}");

        // Yeni pozisyonu kullan
        GameObject newStage = Instantiate(stagePrefab, spawnPosition, Quaternion.identity, buildingRoot);
        NetworkObject netObj = newStage.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            Spawn(netObj);
            Debug.Log("[AdvanceBuildingStage] NetworkObject ba�ar�yla spawn edildi.");
        }
        else
        {
            Debug.LogWarning("[AdvanceBuildingStage] Yeni kat prefab�nda NetworkObject component yok!");
        }

        currentStage++;

        if (currentStage >= buildingStages.Length)
        {
            Debug.Log("Bina tamamland�!");
        }
    }
}
