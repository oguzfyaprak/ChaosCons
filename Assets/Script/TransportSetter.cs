using FishNet;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using UnityEngine;

public class TransportSetter : MonoBehaviour
{
    [SerializeField] private FishySteamworks.FishySteamworks fishyTransport;

    private void Awake()
    {
        TransportManager tm = InstanceFinder.NetworkManager.GetComponent<TransportManager>();
        tm.Transport = fishyTransport;
    }
}