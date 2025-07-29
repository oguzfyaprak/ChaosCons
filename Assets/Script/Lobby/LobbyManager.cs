using UnityEngine;
using TMPro;
using FishNet.Managing;
using FishNet.Transporting;
using FishySteamworks;
using Steamworks;
using System.Text;
using FishNet.Managing.Scened;
using UnityEngine.UI;

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

    private CSteamID _currentLobbyID;

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

        if (_startGameButton != null)
        {
            var button = _startGameButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_StartGame);
            else Debug.LogWarning("LobbyManager: StartGameButton üzerinde Button bileþeni yok.");
        }
        if (_leaveLobbyButton != null)
        {
            var button = _leaveLobbyButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_LeaveLobby);
            else Debug.LogWarning("LobbyManager: LeaveLobbyButton üzerinde Button bileþeni yok.");
        }
        if (_readyButton != null)
        {
            var button = _readyButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_Ready);
            else Debug.LogWarning("LobbyManager: ReadyButton üzerinde Button bileþeni yok.");
        }
    }

    public void InitializeLobbyUI(CSteamID lobbyID)
    {
        _currentLobbyID = lobbyID;
        if (_lobbyIdText != null)
        {
            _lobbyIdText.text = $"Lobi ID: {_currentLobbyID.m_SteamID}";
            GUIUtility.systemCopyBuffer = _currentLobbyID.m_SteamID.ToString();
            Debug.Log($"Lobi ID panoya kopyalandý: {_currentLobbyID.m_SteamID}");
        }
        UpdatePlayerList();

        if (SteamMatchmaking.GetLobbyOwner(_currentLobbyID) == SteamUser.GetSteamID())
        {
            if (_startGameButton != null) _startGameButton.SetActive(true);
            if (_readyButton != null) _readyButton.SetActive(false);
        }
        else
        {
            if (_startGameButton != null) _startGameButton.SetActive(false);
            if (_readyButton != null) _readyButton.SetActive(true);
            UpdateReadyButtonState();
        }
    }

    public void OnClick_StartGame()
    {
        var localSteamId = SteamUser.GetSteamID();
        var lobbyOwner = SteamMatchmaking.GetLobbyOwner(_currentLobbyID);

        if (!_networkManager.IsServerStarted || lobbyOwner != localSteamId)
        {
            Debug.LogWarning("Sadece host oyunu baþlatabilir.");
            return;
        }

        if (!AreAllPlayersReady())
        {
            Debug.LogWarning("Tüm oyuncular hazýr deðil.");
            return;
        }

        SteamMatchmaking.SetLobbyData(_currentLobbyID, "GameStarted", "true");

        MainMenuManager mainMenu = FindFirstObjectByType<MainMenuManager>();
        if (mainMenu != null)
        {
            mainMenu.LoadMainGameScene();
        }
        else
        {
            Debug.LogError("MainMenuManager bulunamadý.");
        }
    }

    public void OnClick_LeaveLobby()
    {
        if (_currentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(_currentLobbyID);
            MainMenuManager.staticLobbyID = CSteamID.Nil;
        }

        if (_networkManager.IsServerStarted)
        {
            _networkManager.ServerManager.StopConnection(true);
        }
        else if (_networkManager.IsClientStarted)
        {
            _networkManager.ClientManager.StopConnection();
        }
    }

    public void OnClick_Ready()
    {
        if (!_currentLobbyID.IsValid()) return;

        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        string newReadyStatus = isCurrentlyReady ? "false" : "true";
        SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", newReadyStatus);

        UpdateReadyButtonState();
        UpdatePlayerList();
    }

    public void UpdateReadyButtonState()
    {
        if (_readyButton == null) return;

        Button button = _readyButton.GetComponent<Button>();
        if (button == null) return;

        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        TMP_Text buttonText = _readyButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = isCurrentlyReady ? "Hazýr (Bekle)" : "Hazýr Ol";
        }
    }

    public void UpdatePlayerList()
    {
        if (_playerListText == null)
        {
            Debug.LogError("playerListText eksik.");
            return;
        }

        if (!_currentLobbyID.IsValid()) return;

        StringBuilder sb = new StringBuilder("Oyuncular:\n");
        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            string personaName = "Bilinmeyen Oyuncu";

            if (memberSteamID.IsValid())
            {
                try
                {
                    if (memberSteamID == SteamUser.GetSteamID())
                    {
                        personaName = SteamFriends.GetPersonaName();
                    }
                    else
                    {
                        personaName = SteamFriends.GetFriendPersonaName(memberSteamID);
                        if (string.IsNullOrEmpty(personaName) || personaName == "[unknown]")
                        {
                            personaName = "Yükleniyor...";
                            SteamFriends.RequestUserInformation(memberSteamID, false);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[LobbyManager] Steam adý alýnamadý: {e.Message} - ID: {memberSteamID}");
                }
            }
            else
            {
                Debug.LogWarning($"[LobbyManager] Geçersiz Steam ID: {memberSteamID}");
            }

            string hostIndicator = (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID)) ? " (Host)" : "";
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            string readyIndicator = (readyStatus == "true") ? " (Hazýr)" : " (Hazýr Deðil)";

            sb.AppendLine($"- {personaName}{hostIndicator}{readyIndicator}");
        }

        _playerListText.text = sb.ToString();
    }

    private bool AreAllPlayersReady()
    {
        if (!_currentLobbyID.IsValid()) return false;

        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        if (numMembers <= 1) return true;

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            if (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID)) continue;

            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            if (readyStatus != "true") return false;
        }
        return true;
    }
}