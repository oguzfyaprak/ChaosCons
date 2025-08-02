using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Steamworks;
using FishNet.Connection;
using System.Collections;

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

        // LocalInstance null ise veya farklı ise ayarla
        if (IsOwner)
        {
            LocalInstance = this;
            if (SteamManager.Initialized)
            {
                StartCoroutine(DelayedSteamInfoSend());
            }
        }

        _lobbyManager = FindFirstObjectByType<LobbyManager>();
        _lobbyManager?.UpdatePlayerList();

        SteamName.OnChange += OnPlayerInfoChanged;
        IsReady.OnChange += OnPlayerInfoChanged;

        // Ek: SteamName ilk yüklendiğinde de OnPlayerInfoChanged çalıştır
        if (!string.IsNullOrEmpty(SteamName.Value))
            OnPlayerInfoChanged(SteamName.Value, SteamName.Value, false);
    }

    private IEnumerator DelayedSteamInfoSend()
    {
        yield return null;
        string name = SteamFriends.GetPersonaName();
        string id = SteamUser.GetSteamID().ToString();
        ServerSendSteamInfo(name, id);

        // Ek: 2 frame sonra tekrar lobby listesini güncelle
        yield return null;
        _lobbyManager?.UpdatePlayerList();
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        SteamName.OnChange -= OnPlayerInfoChanged;
        IsReady.OnChange -= OnPlayerInfoChanged;
        if (LocalInstance == this)
            LocalInstance = null; // Sahne geçişinde veya lobi çıkışında referansı temizle
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
