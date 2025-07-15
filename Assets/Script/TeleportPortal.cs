using UnityEngine;
using FishNet.Object;

public class TeleportPortal : NetworkBehaviour
{
    [SerializeField] private Transform teleportTarget;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServerInitialized) return;

        if (other.TryGetComponent(out TeleportHandler teleport))
        {
            teleport.TeleportTo(teleportTarget.position);
        }
    }
}
