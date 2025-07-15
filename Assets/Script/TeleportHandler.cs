using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Component.Transforming;
using System.Collections;

public class TeleportHandler : NetworkBehaviour
{
    private NetworkTransform _networkTransform;
    private CharacterController _characterController;

    private void Awake()
    {
        _networkTransform = GetComponent<NetworkTransform>();
        _characterController = GetComponent<CharacterController>();
    }

    [Server]
    public void TeleportTo(Vector3 destination)
    {
        if (_networkTransform != null) _networkTransform.enabled = false;
        if (_characterController != null) _characterController.enabled = false;

        transform.SetPositionAndRotation(destination, Quaternion.identity);
        TargetTeleportClientRpc(Owner, destination);

        StartCoroutine(ReEnableServerSide(0.1f));
    }

    [TargetRpc]
    private void TargetTeleportClientRpc(NetworkConnection conn, Vector3 destination)
    {
        if (_networkTransform != null) _networkTransform.enabled = false;
        if (_characterController != null) _characterController.enabled = false;

        transform.SetPositionAndRotation(destination, Quaternion.identity);

        StartCoroutine(ReEnableClientSide(0.1f));
    }

    private IEnumerator ReEnableServerSide(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_networkTransform != null) _networkTransform.enabled = true;
        if (_characterController != null) _characterController.enabled = true;
    }

    private IEnumerator ReEnableClientSide(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsOwner)
        {
            if (_networkTransform != null) _networkTransform.enabled = true;
            if (_characterController != null) _characterController.enabled = true;
        }
    }
}
