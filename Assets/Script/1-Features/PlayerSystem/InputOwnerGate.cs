using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputOwnerGate : NetworkBehaviour
{
    [SerializeField] private PlayerInput playerInput;

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (playerInput) playerInput.enabled = IsOwner;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (playerInput) playerInput.enabled = false;
    }
}
