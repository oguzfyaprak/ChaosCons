using UnityEngine;
using UnityEngine.UI; // <-- Normal UI Text için gerekli
using FishNet.Object;
using FishNet.Connection;
using Game.Building;

namespace Game.Player
{
    public class SabotageSystem : NetworkBehaviour
    {
        [SerializeField] private float sabotageDuration = 5f;
        [SerializeField] private Text sabotageHintText; // <-- TMP yerine Text

        private bool isSabotaging = false;
        private float sabotageTimer = 0f;

        private DeliveryZone currentNearbyZone;

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsOwner) return;

            GameObject txtObj = GameObject.Find("SabotajText"); // Sahnedeki yazı objesinin ismi
            if (txtObj != null)
            {
                sabotageHintText = txtObj.GetComponent<Text>();
                Debug.Log(" sabotageHintText bağlandı.");
            }
            else
            {
                Debug.LogWarning(" sabotageHintText bulunamadı! Obje adı doğru mu?");
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (currentNearbyZone != null && !isSabotaging)
            {
                if (sabotageHintText != null)
                    sabotageHintText.gameObject.SetActive(true);

                if (Input.GetKeyDown(KeyCode.Q))
                {
                    sabotageTimer = 0f;
                    isSabotaging = true;
                    Debug.Log("[CLIENT] Sabotaj başlatıldı!");
                }
            }
            else
            {
                if (sabotageHintText != null)
                    sabotageHintText.gameObject.SetActive(false);
            }

            if (isSabotaging)
            {
                sabotageTimer += Time.deltaTime;

                if (sabotageTimer >= sabotageDuration)
                {
                    sabotageTimer = 0f;
                    isSabotaging = false;

                    if (currentNearbyZone != null)
                    {
                        int targetID = currentNearbyZone.GetOwnerID();
                        CmdApplySabotage(targetID);
                        currentNearbyZone = null;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOwner) return;

            if (other.CompareTag("SabotajZone") &&
                other.TryGetComponent(out DeliveryZone zone))
            {
                int myID = GetComponent<PlayerController>().PlayerID;
                int zoneID = zone.GetOwnerID();

                if (zoneID != myID && zone.IsCompleted())
                {
                    currentNearbyZone = zone;
                    Debug.Log($"[CLIENT] Sabotaj yapılabilir bölgeye girdin. Hedef ID: {zoneID}");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsOwner) return;

            if (other.TryGetComponent(out DeliveryZone zone) && zone == currentNearbyZone)
            {
                currentNearbyZone = null;
                if (sabotageHintText != null)
                    sabotageHintText.gameObject.SetActive(false);
                Debug.Log("[CLIENT] Sabotaj bölgesinden çıktın.");
            }
        }

        [ServerRpc]
        private void CmdApplySabotage(int targetID)
        {
            DeliveryZone[] allZones = FindObjectsByType<DeliveryZone>(FindObjectsSortMode.None);
            foreach (var zone in allZones)
            {
                if (zone.GetOwnerID() == targetID && zone.IsCompleted())
                {
                    zone.ApplySabotage();
                    Debug.Log($"[SERVER] Sabotaj uygulandı. Hedef PlayerID: {targetID}");

                    NetworkConnection conn = PlayerRegistry.GetConnectionByPlayerID(targetID);
                    if (conn != null)
                        TargetNotifySabotaged(conn);
                }
            }
        }

        [TargetRpc]
        private void TargetNotifySabotaged(NetworkConnection conn)
        {
            Debug.Log("[CLIENT] Binana sabotaj yapıldı!");
        }
    }
}
