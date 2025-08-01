using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Steamworks;

public class PlayerSteamInfo : NetworkBehaviour
{
    public readonly SyncVar<string> SteamName = new();
    public readonly SyncVar<string> SteamId = new();
    public readonly SyncVar<bool> IsReady = new();

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner && SteamManager.Initialized)
        {
            string name = SteamFriends.GetPersonaName();
            string id = SteamUser.GetSteamID().ToString();
            ServerSendSteamInfo(name, id);
        }

        // SteamName de�i�ti�inde otomatik g�ncelle
        SteamName.OnChange += (oldVal, newVal, asServer) =>
        {
            var lobby = FindFirstObjectByType<LobbyManager>();
            lobby?.UpdatePlayerList();
        };
    }

    [ServerRpc]
    private void ServerSendSteamInfo(string name, string id)
    {
        SteamName.Value = name;
        SteamId.Value = id;
        IsReady.Value = false; // Ba�lang��ta haz�r de�il
    }

    [ServerRpc]
    public void ServerToggleReadyStatus()
    {
        IsReady.Value = !IsReady.Value;
    }
}
