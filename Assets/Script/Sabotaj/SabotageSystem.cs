using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using Game.Building;

namespace Game.Player
{
    public class SabotageSystem : NetworkBehaviour
    {
        [SerializeField] private float sabotageDuration = 5f;

        private bool isSabotaging = false;
        private float sabotageTimer = 0f;

        private DeliveryZone currentNearbyZone;

        private void Update()
        {
            if (!IsOwner) return;

            if (currentNearbyZone != null && !isSabotaging)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    Debug.Log("⌨️ Q tuşuna basıldı, sabotaj başlatılıyor.");
                    sabotageTimer = 0f;
                    isSabotaging = true;
                }
            }

            if (isSabotaging)
            {
                sabotageTimer += Time.deltaTime;
                Debug.Log($"⏳ Sabotaj süresi: {sabotageTimer:F2}");

                if (sabotageTimer >= sabotageDuration)
                {
                    sabotageTimer = 0f;
                    isSabotaging = false;

                    if (currentNearbyZone != null)
                    {
                        int targetID = currentNearbyZone.GetOwnerID();
                        Debug.Log($"🎯 Sabotaj tamamlandı. Hedef PlayerID: {targetID}");
                        CmdApplySabotage(targetID);
                        currentNearbyZone = null;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOwner) return;

            Debug.Log($"🚪 Trigger Enter: {other.name}");

            if (other.CompareTag("DeliveryZone") &&
                other.TryGetComponent(out DeliveryZone zone))
            {
                int myID = GetComponent<PlayerController>().PlayerID;
                int zoneID = zone.GetOwnerID();
                Debug.Log($"🔍 Trigger ZoneID: {zoneID} | MyID: {myID}");

                if (zoneID != myID && zone.IsCompleted() && !zone.IsDamaged())
                {
                    currentNearbyZone = zone;
                    Debug.Log($"✅ Sabotaj yapılabilir bölgeye girdin. Hedef ID: {zoneID}");
                }
                else
                {
                    Debug.Log("⛔️ Bu bölge sana ait ya da zaten hasarlı.");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsOwner) return;

            if (other.TryGetComponent(out DeliveryZone zone) && zone == currentNearbyZone)
            {
                currentNearbyZone = null;
                Debug.Log("📤 Sabotaj bölgesinden çıktın.");
            }
        }

        [ServerRpc]
        private void CmdApplySabotage(int targetID)
        {
            Debug.Log($"🛠️ [SERVER] CmdApplySabotage çağrıldı. TargetID: {targetID}");

            DeliveryZone[] allZones = FindObjectsByType<DeliveryZone>(FindObjectsSortMode.None);
            foreach (var zone in allZones)
            {
                if (zone.GetOwnerID() == targetID && zone.IsCompleted() && !zone.IsDamaged())
                {
                    zone.ApplySabotage();
                    Debug.Log($"💣 Sabotaj uygulandı. PlayerID: {targetID}");

                    NetworkConnection conn = PlayerRegistry.GetConnectionByPlayerID(targetID);
                    if (conn != null)
                        TargetNotifySabotaged(conn);
                    else
                        Debug.LogWarning("⚠️ PlayerConnection bulunamadı!");
                }
            }
        }

        [TargetRpc]
        private void TargetNotifySabotaged(NetworkConnection conn)
        {
            Debug.Log("📢 [CLIENT] Binana sabotaj yapıldı!");
        }
    }
}
