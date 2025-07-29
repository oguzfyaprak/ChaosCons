using UnityEngine;
using TMPro;
using FishNet.Managing;
using FishNet.Transporting;
using FishySteamworks;
using Steamworks;
using System.Text;
using FishNet.Managing.Scened;
using UnityEngine.UI; // Button bile�eni i�in

public class LobbyManager : MonoBehaviour
{
    // --- MainMenuManager'dan gelen Referanslar ---
    // Bu alanlar SerializeField DE��LD�R, ��nk� MainMenuManager taraf�ndan runtime'da atanacaklar.
    private NetworkManager _networkManager;
    private FishySteamworks.FishySteamworks _steamworksTransport;

    private TMP_Text _lobbyIdText;
    private TMP_Text _playerListText;
    private GameObject _startGameButton;
    private GameObject _leaveLobbyButton;
    private GameObject _readyButton;
    private string _mainGameSceneName; // Ana oyun sahnesinin ad� (MainMenuManager'dan al�nacak)

    // Lobi Bilgisi
    private CSteamID _currentLobbyID; // Anl�k olarak ba�l� olunan lobi ID'si
    // MainMenuManager.staticLobbyID statik lobi ID'sini tutuyor.

    // Initialize fonksiyonu MainMenuManager taraf�ndan �a�r�lacak
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

        // UI buton olaylar�n� burada dinlemeye ba�la (�nceki hata d�zeltmeleri uyguland�)
        if (_startGameButton != null)
        {
            var button = _startGameButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_StartGame);
            else Debug.LogWarning("LobbyManager: StartGameButton �zerinde UnityEngine.UI.Button bile�eni bulunamad�.");
        }
        if (_leaveLobbyButton != null)
        {
            var button = _leaveLobbyButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_LeaveLobby);
            else Debug.LogWarning("LobbyManager: LeaveLobbyButton �zerinde UnityEngine.UI.Button bile�eni bulunamad�.");
        }
        if (_readyButton != null)
        {
            var button = _readyButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_Ready);
            else Debug.LogWarning("LobbyManager: ReadyButton �zerinde UnityEngine.UI.Button bile�eni bulunamad�.");
        }
    }

    // Lobi UI'�n� ba�latma (MainMenuManager taraf�ndan lobiye girildi�inde �a�r�l�r)
    public void InitializeLobbyUI(CSteamID lobbyID)
    {
        _currentLobbyID = lobbyID;
        if (_lobbyIdText != null)
        {
            _lobbyIdText.text = $"Lobi ID: {_currentLobbyID.m_SteamID}";
            GUIUtility.systemCopyBuffer = _currentLobbyID.m_SteamID.ToString(); // Lobi ID'yi panoya kopyala
            Debug.Log($"Lobi ID panoya kopyaland�: {_currentLobbyID.m_SteamID}");
        }
        UpdatePlayerList(); // Ba�lang��ta oyuncu listesini g�ncelle

        // Buton g�r�n�rl�klerini ayarla: Host ise Oyunu Ba�lat, Client ise Haz�r Ol
        if (SteamMatchmaking.GetLobbyOwner(_currentLobbyID) == SteamUser.GetSteamID()) // Host ise
        {
            if (_startGameButton != null) _startGameButton.SetActive(true);
            if (_readyButton != null) _readyButton.SetActive(false); // Host'un haz�r olmas� gerekmez
        }
        else // Client ise
        {
            if (_startGameButton != null) _startGameButton.SetActive(false);
            if (_readyButton != null) _readyButton.SetActive(true);
            UpdateReadyButtonState(); // Haz�r butonu durumunu g�ncelle
        }
    }

    public void OnClick_StartGame()
    {
        var localSteamId = SteamUser.GetSteamID();
        var lobbyOwner = SteamMatchmaking.GetLobbyOwner(_currentLobbyID);
        Debug.Log($"[StartGame] LocalSteamID: {localSteamId}, LobbyOwner: {lobbyOwner}");

        // Sadece lobi host'u ve sunucu aktifse oyunu ba�latabilir
        if (!_networkManager.IsServerStarted || lobbyOwner != localSteamId)
        {
            Debug.LogWarning("Sadece lobi host'u oyunu ba�latabilir!");
            return;
        }

        // T�m oyuncular haz�r m� kontrol et
        if (!AreAllPlayersReady())
        {
            Debug.LogWarning("T�m oyuncular haz�r de�il! Oyun ba�lat�lamaz.");
            return;
        }

        Debug.Log("Oyunu ba�lat�l�yor... Ana oyun sahnesine ge�iliyor.");

        // Lobide oyunun ba�lad���n� belirten bir veri ayarla
        SteamMatchmaking.SetLobbyData(_currentLobbyID, "GameStarted", "true");

        // Sunucu olarak ana oyun sahnesine ge�i�i FishNet ile yap
        // Client'lar MainMenuManager'�n LobbyDataUpdate callback'i ile bu veriyi g�r�p kendi sahnelerini y�kleyecek.
        MainMenuManager mainMenu = FindFirstObjectByType<MainMenuManager>(); // MainMenuManager'a eri�im
        if (mainMenu != null)
        {
            mainMenu.LoadMainGameScene();
        }
        else
        {
            Debug.LogError("LobbyManager: MainMenuManager bulunamad�! Sahne y�klenemedi.");
        }
    }

    public void OnClick_LeaveLobby()
    {
        Debug.Log("Lobiden ayr�l�yor...");

        if (_currentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(_currentLobbyID);
            Debug.Log($"Lobiden ayr�l�nd�: {_currentLobbyID}");
            MainMenuManager.staticLobbyID = CSteamID.Nil; // Statik lobi ID'sini s�f�rla
        }

        // Network ba�lant�lar�n� durdur (Host veya Client olmas�na g�re)
        if (_networkManager.IsServerStarted)
        {
            _networkManager.ServerManager.StopConnection(true);
        }
        else if (_networkManager.IsClientStarted)
        {
            _networkManager.ClientManager.StopConnection();
        }

        // Lobiden ayr�l�nca ana men�ye d�n��� MainMenuManager y�netecek.
        // Bu metot MainMenuManager'dan �a�r�ld���nda zaten MainMenuManager paneli de�i�tirecek.
    }

    public void OnClick_Ready()
    {
        if (!_currentLobbyID.IsValid()) return;

        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        string newReadyStatus = isCurrentlyReady ? "false" : "true";
        // CS1501 Hatas� D�zeltildi: SetLobbyMemberData 3 arg�man al�r.
        SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", newReadyStatus);

        Debug.Log($"Haz�r durumu g�ncellendi: {newReadyStatus}");
        UpdateReadyButtonState(); // Buton metnini g�ncelle
        UpdatePlayerList(); // Oyuncu listesini g�ncelle
    }

    // Haz�r butonu metnini ve durumunu g�nceller
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
            buttonText.text = isCurrentlyReady ? "Haz�r (Bekle)" : "Haz�r Ol";
        }
    }

    // --- Yard�mc� Fonksiyonlar ---
    // Oyuncu listesini g�nceller ve UI'a yans�t�r.
    public void UpdatePlayerList()
    {
        if (_playerListText == null)
        {
            Debug.LogError("LobbyManager: playerListText GameObject'i atanmam��! L�tfen Unity Inspector'da atay�n.");
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
                    // Kendi ad�n� �ekmek i�in ayr� kontrol
                    if (memberSteamID == SteamUser.GetSteamID())
                    {
                        personaName = SteamFriends.GetPersonaName();
                    }
                    else
                    {
                        personaName = SteamFriends.GetFriendPersonaName(memberSteamID);
                        if (string.IsNullOrEmpty(personaName) || personaName == "[unknown]")
                        {
                            personaName = "Y�kleniyor...";
                            SteamFriends.RequestUserInformation(memberSteamID, false);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[LobbyManager] Steam ad� al�namad�: {e.Message} - ID: {memberSteamID}");
                    personaName = "Bilinmeyen Oyuncu";
                }
            }
            else
            {
                Debug.LogWarning($"[LobbyManager] Ge�ersiz Steam ID bulundu: {memberSteamID}");
            }

            string hostIndicator = (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID)) ? " (Host)" : "";
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            string readyIndicator = (readyStatus == "true") ? " (Haz�r)" : " (Haz�r De�il)";

            sb.AppendLine($"- {personaName}{hostIndicator}{readyIndicator}");
        }

        _playerListText.text = sb.ToString();
    


    _playerListText.text = sb.ToString();
    }

    // T�m oyuncular�n haz�r olup olmad���n� kontrol eder.
    private bool AreAllPlayersReady()
    {
        if (!_currentLobbyID.IsValid()) return false;

        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        // E�er lobide sadece host varsa, oyunu direkt ba�latabilir (haz�r olmas�na gerek yok).
        if (numMembers <= 1) return true;

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            // Host oyuncunun haz�r olmas�na gerek yoktur, oyunu ba�latan odur.
            if (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID))
            {
                continue; // Host'u kontrol etme
            }

            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            if (readyStatus != "true")
            {
                return false; // Bir ki�i bile haz�r de�ilse false d�nd�r
            }
        }
        return true; // Herkes haz�r
    }
}