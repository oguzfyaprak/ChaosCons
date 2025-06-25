using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class ItemProperties : NetworkBehaviour
{
    [HideInInspector]
    public bool OriginalKinematicState;

    public readonly SyncVar<int> ownerPlayerID = new SyncVar<int>(-1);

    private void Awake()
    {
        // Rigidbody'nin orijinal kinematic durumunu kaydet
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            OriginalKinematicState = rb.isKinematic;
        }
    }

    private void OnEnable()
    {
        ownerPlayerID.OnChange += OnOwnerIDChanged;
    }

    private void OnDisable()
    {
        ownerPlayerID.OnChange -= OnOwnerIDChanged;
    }

    private void OnOwnerIDChanged(int oldID, int newID, bool asServer)
    {
        Debug.Log($" Item ownerPlayerID changed: {oldID} -> {newID}");
    }
}
