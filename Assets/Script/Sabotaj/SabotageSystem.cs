using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using Game.Building;

namespace Game.Player
{
    public class SabotageSystem : NetworkBehaviour
    {
        [SerializeField] private float sabotageDuration = 5f;
        [SerializeField] private KeyCode sabotageKey = KeyCode.Q;

        private bool isSabotaging = false;
        private float sabotageTimer = 0f;
        private DeliveryZone currentZone;

        private void Update()
        {
            if (!IsOwner) return;

            if (currentZone != null && Input.GetKeyDown(sabotageKey))
            {
                if (!isSabotaging && !currentZone.IsSabotageCooldownActive())
                {
                    StartSabotage();
                }
            }

            if (isSabotaging)
            {
                sabotageTimer += Time.deltaTime;
                if (sabotageTimer >= sabotageDuration)
                {
                    CompleteSabotage();
                }
            }
        }

        private void StartSabotage()
        {
            isSabotaging = true;
            sabotageTimer = 0f;
            Debug.Log("Sabotaj veya tamir başlatıldı...");
        }

        private void CompleteSabotage()
        {
            sabotageTimer = 0f;
            isSabotaging = false;

            if (currentZone != null)
            {
                NetworkObject zoneObj = currentZone.GetComponent<NetworkObject>();
                if (zoneObj != null)
                {
                    CmdSabotageOrRepair(zoneObj);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOwner) return;

            if (other.CompareTag("DeliveryZone") && other.TryGetComponent(out DeliveryZone zone))
            {
                currentZone = zone;
                Debug.Log("⚙️ Zone bulundu.");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsOwner) return;

            if (other.TryGetComponent(out DeliveryZone zone) && zone == currentZone)
            {
                currentZone = null;
                if (isSabotaging)
                {
                    sabotageTimer = 0f;
                    isSabotaging = false;
                    Debug.Log("🚪 Bölgeden çıkıldı, sabotaj iptal edildi.");
                }
            }
        }

        [ServerRpc]
        private void CmdSabotageOrRepair(NetworkObject zoneObj)
        {
            if (zoneObj == null) return;

            DeliveryZone zone = zoneObj.GetComponent<DeliveryZone>();
            if (zone == null) return;

            // Eğer hiç kat yoksa işlem yapma
            if (zone.GetCurrentStage() <= 0)
            {
                Debug.Log("❌ İşlem yapılamaz: bina henüz hiç kat çıkmamış.");
                return;
            }

            int playerID = GetComponent<PlayerController>().PlayerID;
            int zoneOwnerID = zone.GetOwnerID();

            if (playerID == zoneOwnerID)
            {
                // KENDİ BÖLGEN → TAMİR
                if (zone.IsDamaged())
                {
                    zone.Repair();
                    Debug.Log("✅ Tamir edildi.");
                }
                else
                {
                    Debug.Log("📦 Tamir gerekmedi.");
                }
            }
            else
            {
                // DÜŞMAN BÖLGE → SABOTAJ
                if (!zone.IsDamaged())
                {
                    zone.MarkDamaged();
                    Debug.Log("💢 Hasar verildi.");
                }
                else
                {
                    zone.ApplySabotage();
                    Debug.Log("💥 Kat eksildi.");
                }

                NetworkConnection conn = PlayerRegistry.GetConnectionByPlayerID(zoneOwnerID);
                if (conn != null)
                    TargetNotifySabotaged(conn);
            }
        }

        [TargetRpc]
        private void TargetNotifySabotaged(NetworkConnection conn)
        {
            Debug.Log("🚨 BİNANA SABOTAJ YAPILDI!");
        }
    }
}