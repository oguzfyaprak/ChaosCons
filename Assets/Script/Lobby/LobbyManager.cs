using UnityEngine;
using TMPro;
using FishNet.Managing;
using FishNet.Transporting;
using FishySteamworks;
using Steamworks;
using System.Text;
using System.Collections.Generic;
using FishNet.Connection;
using UnityEngine.UI;
using System.Collections;

public class LobbyManager : MonoBehaviour
{
    private NetworkManager _networkManager;
    private FishySteamworks.FishySteamworks _steamworksTransport;
    private TMP_Text _lobbyIdText;
    private TMP_Text _playerListText;
    private GameObject _startGameButton;
    private GameObject _leaveLobbyButton;
    private GameObject _readyButton;
    private string _mainGameSceneName;
    [SerializeField] private GameObject lobbyPlayerInfoPrefab;

    private CSteamID _currentLobbyID;
    private Callback<LobbyDataUpdate_t> _lobbyDataUpdateCallback;
    private readonly Dictionary<int, LobbyPlayerInfo> _playerInfoMap = new();

    private void OnEnable()
    {
        _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);

        // Sahne geçişlerinde null pointer hatası almamak için LocalInstance referansı kontrol edilecek
        StartCoroutine(WaitAndUpdatePlayerList());
    }

    private IEnumerator WaitAndUpdatePlayerList()
    {
        // Oyuncu objeleri ve SyncVar’lar oturana kadar 2 frame bekleyip player listesini güncelle
        yield return null;
        yield return null;
        UpdatePlayerList();
    }

    private void OnDisable()
    {
        _lobbyDataUpdateCallback?.Dispose();

        if (_networkManager != null && _networkManager.ServerManager != null)
        {
            _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            conn.OnLoadedStartScenes += (connection, asServer) =>
            {
                if (lobbyPlayerInfoPrefab == null)
                {
                    Debug.LogError("LobbyPlayerInfo prefabı atanmamış!");
                    return;
                }

                var obj = Instantiate(lobbyPlayerInfoPrefab);
                _networkManager.ServerManager.Spawn(obj, conn);

                var info = obj.GetComponent<LobbyPlayerInfo>();
                if (info != null)
                {
                    _playerInfoMap[conn.ClientId] = info;
                    Debug.Log($"[LobbyManager] Oyuncu {conn.ClientId} lobiye katıldı.");
                }

                UpdatePlayerList();
            };
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            if (_playerInfoMap.Remove(conn.ClientId))
            {
                Debug.Log($"[LobbyManager] Oyuncu {conn.ClientId} ayrıldı.");
            }
            UpdatePlayerList();
        }
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback)
    {
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            UpdatePlayerList();
        }
    }

    public void Initialize(NetworkManager nm, FishySteamworks.FishySteamworks fst,
                          TMP_Text lobbyIdTxt, TMP_Text playerListTxt,
                          GameObject startGameBtn, GameObject leaveLobbyBtn,
                          GameObject readyBtn, string mainGameScene)
    {
        _networkManager = nm;
        _steamworksTransport = fst;
        _lobbyIdText = lobbyIdTxt;
        _playerListText = playerListTxt;
        _startGameButton = startGameBtn;
        _leaveLobbyButton = leaveLobbyBtn;
        _readyButton = readyBtn;
        _mainGameSceneName = mainGameScene;

        _startGameButton?.GetComponent<Button>()?.onClick.AddListener(OnClick_StartGame);
        _leaveLobbyButton?.GetComponent<Button>()?.onClick.AddListener(OnClick_LeaveLobby);
        _readyButton?.GetComponent<Button>()?.onClick.AddListener(OnClick_Ready);

        if (_networkManager.ServerManager != null)
        {
            _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }
    }

    public void InitializeLobbyUI(CSteamID lobbyID)
    {
        _currentLobbyID = lobbyID;
        if (_lobbyIdText != null)
            _lobbyIdText.text = $"Lobi ID: {_currentLobbyID.m_SteamID}";

        GUIUtility.systemCopyBuffer = _currentLobbyID.m_SteamID.ToString();

        CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(_currentLobbyID);
        bool isHost = (lobbyOwner == SteamUser.GetSteamID());

        _startGameButton?.SetActive(isHost);
        _readyButton?.SetActive(!isHost);

        UpdatePlayerList();
    }

    public void OnClick_StartGame()
    {
        if (!_networkManager.IsServerStarted) return;
        if (!AreAllPlayersReady())
        {
            Debug.LogWarning("Tüm oyuncular hazır değil!");
            return;
        }

        Debug.Log("[LobbyManager] Oyuna geçiliyor...");
        SteamMatchmaking.SetLobbyData(_currentLobbyID, "GameStarted", "true");

        var mainMenu = FindFirstObjectByType<MainMenuManager>();
        mainMenu?.LoadMainGameScene();
    }

    public void OnClick_LeaveLobby()
    {
        Debug.Log("[LobbyManager] Lobiden çıkılıyor.");
        if (_currentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(_currentLobbyID);
            MainMenuManager.staticLobbyID = CSteamID.Nil;
        }

        if (_networkManager.IsServerStarted)
            _networkManager.ServerManager.StopConnection(true);
        else if (_networkManager.IsClientStarted)
            _networkManager.ClientManager.StopConnection();
    }

    public void OnClick_Ready()
    {
        LobbyPlayerInfo.LocalInstance?.ServerToggleReadyStatus();
    }

    public void UpdatePlayerList()
    {
        if (_playerListText == null) return;

        StringBuilder sb = new StringBuilder("Oyuncular:\n");

        foreach (var info in _playerInfoMap.Values)
        {
            if (info == null)
            {
                sb.AppendLine("- Bağlantı bekleniyor...");
                continue;
            }
            string name = string.IsNullOrEmpty(info.SteamName.Value) ? "Yükleniyor..." : info.SteamName.Value;
            string readyStatus = info.IsReady.Value ? "<color=green>[HAZIR]</color>" : "<color=yellow>[HAZIR DEĞİL]</color>";
            sb.AppendLine($"- {name} {readyStatus}");
        }

        _playerListText.text = sb.ToString();
        UpdateReadyButtonState();
    }

    public void UpdateReadyButtonState()
    {
        if (_readyButton == null) return;
        if (LobbyPlayerInfo.LocalInstance == null) return;

        var text = _readyButton.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = LobbyPlayerInfo.LocalInstance.IsReady.Value ? "Hazır (İptal)" : "Hazır Ol";
        }
    }

    private bool AreAllPlayersReady()
    {
        if (_playerInfoMap.Count <= 1 && _networkManager.IsServerStarted) return true;

        foreach (var info in _playerInfoMap.Values)
        {
            if (info.Owner.IsHost) continue;
            if (!info.IsReady.Value) return false;
        }
        return true;
    }
}