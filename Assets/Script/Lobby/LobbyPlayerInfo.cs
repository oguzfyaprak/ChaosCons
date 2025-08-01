using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Steamworks;
using FishNet.Connection;
using FishNet.Managing;

public class LobbyPlayerInfo : NetworkBehaviour
{
    public readonly SyncVar<string> SteamName = new();
    public readonly SyncVar<string> SteamId = new();
    public readonly SyncVar<bool> IsReady = new();

    public static LobbyPlayerInfo LocalInstance { get; private set; }
    private LobbyManager _lobbyManager;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            LocalInstance = this;

            if (SteamManager.Initialized)
            {
                string name = SteamFriends.GetPersonaName();
                string id = SteamUser.GetSteamID().ToString();
                ServerSendSteamInfo(name, id);
            }
        }

        _lobbyManager = FindFirstObjectByType<LobbyManager>();
        _lobbyManager?.UpdatePlayerList();

        SteamName.OnChange += OnPlayerInfoChanged;
        IsReady.OnChange += OnPlayerInfoChanged;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        SteamName.OnChange += OnPlayerInfoChanged;
        IsReady.OnChange += OnPlayerInfoChanged;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        SteamName.OnChange -= OnPlayerInfoChanged;
        IsReady.OnChange -= OnPlayerInfoChanged;
    }

    [ServerRpc]
    private void ServerSendSteamInfo(string name, string id)
    {
        SteamName.Value = name;
        SteamId.Value = id;
        IsReady.Value = false;
    }

    private void OnPlayerInfoChanged(string prev, string next, bool asServer)
    {
        _lobbyManager?.UpdatePlayerList();
    }

    private void OnPlayerInfoChanged(bool prev, bool next, bool asServer)
    {
        _lobbyManager?.UpdatePlayerList();
    }

    [ServerRpc]
    public void ServerToggleReadyStatus()
    {
        IsReady.Value = !IsReady.Value;
    }
}
