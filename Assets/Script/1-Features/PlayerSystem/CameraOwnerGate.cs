using FishNet.Object;
using UnityEngine;

public class CameraOwnerGate : NetworkBehaviour
{
    [SerializeField] private GameObject cameraRig; // Cinemachine/Camera + AudioListener kökü

    public override void OnStartClient()
    {
        base.OnStartClient();
        SetLocal(IsOwner);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        SetLocal(false);
    }

    private void SetLocal(bool isLocal)
    {
        if (cameraRig) cameraRig.SetActive(isLocal);

        var al = cameraRig ? cameraRig.GetComponentInChildren<AudioListener>(true) : null;
        if (al) al.enabled = isLocal; // 2 AudioListener uyarýsýný bitirir
    }
}
