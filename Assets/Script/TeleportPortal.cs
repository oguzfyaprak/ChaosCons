using UnityEngine;
using FishNet.Object;
using Game.Player;

public class TeleportPortal : NetworkBehaviour
{
    [SerializeField] private Transform teleportTarget;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServerInitialized) return;

        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.Teleport(teleportTarget.position);
            }
        }
    }
}
