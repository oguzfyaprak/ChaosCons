using FishNet.Managing;
using Steamworks;
using System.Text;
using TMPro;
using UnityEngine;
using FishNet.Transporting;
using FishySteamworks;
using FishNet.Managing.Scened;
using UnityEngine.UI;
using System;
using System.Collections;

public class LobbyManager : MonoBehaviour
{
    // Konstante tanýmlarý
    private const string V = "Yükleniyor...";

    // --- MainMenuManager'dan gelen Referanslar ---
    private NetworkManager _networkManager;
    private FishySteamworks.FishySteamworks _steamworksTransport;

    // UI Referanslarý
    private TMP_Text _lobbyIdText;
    private TMP_Text _playerListText;
    private GameObject _startGameButton;
    private GameObject _leaveLobbyButton;
    private GameObject _readyButton;
    private string _mainGameSceneName; // Ana oyun sahnesinin adý (MainMenuManager'dan alýnacak)

    // Lobi Bilgisi
    private CSteamID _currentLobbyID; // Anlýk olarak baðlý olunan lobi ID'si

    // Initialize fonksiyonu MainMenuManager tarafýndan çaðrýlacak
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

        // UI buton olaylarýný burada dinlemeye baþla (önceki hata düzeltmeleri uygulandý)
        if (_startGameButton != null)
        {
            if (_startGameButton.TryGetComponent<Button>(out var button)) button.onClick.AddListener(OnClick_StartGame);
            else Debug.LogWarning("LobbyManager: StartGameButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
        else Debug.LogWarning("LobbyManager: _startGameButton referansý atanmamýþ!");

        if (_leaveLobbyButton != null)
        {
            if (_leaveLobbyButton.TryGetComponent<Button>(out var button)) button.onClick.AddListener(OnClick_LeaveLobby);
            else Debug.LogWarning("LobbyManager: LeaveLobbyButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
        else Debug.LogWarning("LobbyManager: _leaveLobbyButton referansý atanmamýþ!");

        if (_readyButton != null)
        {
            if (_readyButton.TryGetComponent<Button>(out var button)) button.onClick.AddListener(OnClick_Ready);
            else Debug.LogWarning("LobbyManager: ReadyButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
        else Debug.LogWarning("LobbyManager: _readyButton referansý atanmamýþ!");
    }

    // Lobi UI'ýný baþlatma (MainMenuManager tarafýndan lobiye girildiðinde çaðrýlýr)
    public void InitializeLobbyUI(CSteamID lobbyID)
    {
        _currentLobbyID = lobbyID;
        if (_lobbyIdText != null)
        {
            _lobbyIdText.text = $"Lobi ID: {_currentLobbyID.m_SteamID}";
            GUIUtility.systemCopyBuffer = _currentLobbyID.m_SteamID.ToString(); // Lobi ID'yi panoya kopyala
            Debug.Log($"Lobi ID panoya kopyalandý: {_currentLobbyID.m_SteamID}");
        }

        // Lobiye girer girmez tüm üyelerin bilgilerini talep et.
        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            // Oyuncunun bilgilerini önceden talep et. Bu, GetFriendPersonaName'in daha doðru çalýþmasýna yardýmcý olur.
            SteamFriends.RequestUserInformation(memberSteamID, false);
        }

        UpdatePlayerList(); // Baþlangýçta oyuncu listesini güncelle

        // Buton görünürlüklerini ayarla: Host ise Oyunu Baþlat, Client ise Hazýr Ol
        if (SteamMatchmaking.GetLobbyOwner(_currentLobbyID) == SteamUser.GetSteamID()) // Host ise
        {
            if (_startGameButton != null) _startGameButton.SetActive(true);
            if (_readyButton != null) _readyButton.SetActive(false); // Host'un hazýr olmasý gerekmez
        }
        else // Client ise
        {
            if (_startGameButton != null) _startGameButton.SetActive(false);
            if (_readyButton != null) _readyButton.SetActive(true);
            UpdateReadyButtonState(); // Hazýr butonu durumunu güncelle
        }
    }

    public void OnClick_StartGame()
    {
        var localSteamId = SteamUser.GetSteamID();
        var lobbyOwner = SteamMatchmaking.GetLobbyOwner(_currentLobbyID);
        Debug.Log($"[StartGame] LocalSteamID: {localSteamId}, LobbyOwner: {lobbyOwner}");

        // Sadece lobi host'u ve sunucu aktifse oyunu baþlatabilir
        if (!_networkManager.IsServerStarted || lobbyOwner != localSteamId)
        {
            Debug.LogWarning("Sadece lobi host'u oyunu baþlatabilir ve sunucu aktif olmalýdýr!");
            return;
        }

        // Tüm oyuncular hazýr mý kontrol et
        if (!AreAllPlayersReady())
        {
            Debug.LogWarning("Tüm oyuncular hazýr deðil! Oyun baþlatýlamaz.");
            return;
        }

        Debug.Log("Oyunu baþlatýlýyor... Ana oyun sahnesine geçiliyor.");

        // Lobide oyunun baþladýðýný belirten bir veri ayarla
        SteamMatchmaking.SetLobbyData(_currentLobbyID, "GameStarted", "true");

        // Sunucu olarak ana oyun sahnesine geçiþi FishNet ile yap
        // Client'lar MainMenuManager'ýn LobbyDataUpdate callback'i ile bu veriyi görüp kendi sahnelerini yükleyecek.
        MainMenuManager mainMenu = FindFirstObjectByType<MainMenuManager>(); // MainMenuManager'a eriþim
        if (mainMenu != null)
        {
            mainMenu.LoadMainGameScene();
        }
        else
        {
            Debug.LogError("LobbyManager: MainMenuManager bulunamadý! Sahne yüklenemedi.");
        }
    }

    public void OnClick_LeaveLobby()
    {
        if (_currentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(_currentLobbyID);
            MainMenuManager.staticLobbyID = CSteamID.Nil;
        }

        MainMenuManager mainMenu = FindFirstObjectByType<MainMenuManager>();
        if (mainMenu != null)
        {
            mainMenu.ReturnToMainMenuFromLobby();
        }
        else
        {
            Debug.LogError("MainMenuManager bulunamadý. Ana menüye dönemedi.");
        }
    }

    public void OnClick_Ready()
    {
        if (!_currentLobbyID.IsValid()) return;

        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        string newReadyStatus = isCurrentlyReady ? "false" : "true";
        SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", newReadyStatus);

        Debug.Log($"Hazýr durumu güncellendi: {newReadyStatus}");
    }

    // Hazýr butonu metnini ve durumunu günceller
    public void UpdateReadyButtonState()
    {
        if (_readyButton == null) return;

        if (!_currentLobbyID.IsValid())
        {
            TMP_Text buttonTextInvalid = _readyButton.GetComponentInChildren<TMP_Text>();
            if (buttonTextInvalid != null) buttonTextInvalid.text = "Geçersiz Lobi";
            _readyButton.SetActive(false); // Geçersiz lobi ise butonu kapat
            return;
        }

        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        TMP_Text buttonText = _readyButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = isCurrentlyReady ? "Hazýr (Bekle)" : "Hazýr Ol";
        }
    }

    // --- Yardýmcý Fonksiyonlar ---
    // Oyuncu listesini günceller ve UI'a yansýtýr.
    private Coroutine _refreshCoroutine;

    public void UpdatePlayerList()
    {
        if (_playerListText == null)
        {
            Debug.LogError("LobbyManager: _playerListText GameObject'i atanmamýþ! Lütfen Unity Inspector'da atayýn.");
            return; // Eðer UI elementý atanmamýþsa daha fazla devam etme
        }

        if (!_currentLobbyID.IsValid())
        {
            _playerListText.text = "Lobiye baðlý deðil.";
            return; // Geçerli bir lobi ID'si yoksa devam etme
        }

        // Bu KONTROL ÇOK ÖNEMLÝ: Steam API'si hazýr deðilse hemen çýk
        if (!SteamManager.Initialized)
        {
            _playerListText.text = "Steam baþlatýlamadý veya hazýr deðil.";
            Debug.LogWarning("SteamManager baþlatýlmamýþ veya henüz hazýr deðil. Oyuncu listesi çekilemiyor.");
            return; // Steam hazýr deðilse hemen çýk
        }

        StringBuilder sb = new("Oyuncular:\n");
        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        bool hasUnknownNames = false;

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            string personaName;

            if (!memberSteamID.IsValid())
            {
                Debug.LogWarning($"[LobbyManager] Geçersiz Steam ID bulundu: {memberSteamID}");
                personaName = "Geçersiz Oyuncu";
            }
            else
            {
                SteamFriends.RequestUserInformation(memberSteamID, true);

                try
                {
                    if (memberSteamID == SteamUser.GetSteamID())
                    {
                        personaName = SteamFriends.GetPersonaName(); // kendi adýn için daha güvenli
                    }
                    else
                    {
                        string fetchedName = SteamFriends.GetFriendPersonaName(memberSteamID);
                        if (string.IsNullOrEmpty(fetchedName) || fetchedName == "[unknown]")
                        {
                            personaName = V;
                            hasUnknownNames = true;
                        }
                        else
                        {
                            personaName = fetchedName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbyManager] Steam ID {memberSteamID} için PersonaName alýnamadý: {ex.Message}");
                    personaName = V;
                    hasUnknownNames = true;
                }
            }

            string hostIndicator = (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID)) ? " (Host)" : "";
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");

            string readyIndicator = "";
            if (hostIndicator != " (Host)")
            {
                readyIndicator = (readyStatus == "true") ? " (Hazýr)" : " (Hazýr Deðil)";
            }

            sb.AppendLine($"- {personaName}{hostIndicator}{readyIndicator}");
        }

        _playerListText.text = sb.ToString();

        if (hasUnknownNames)
        {
            if (_refreshCoroutine != null) StopCoroutine(_refreshCoroutine);
            _refreshCoroutine = StartCoroutine(RefreshPlayerListWithDelay());
        }
    }

    private IEnumerator RefreshPlayerListWithDelay()
    {
        yield return new WaitForSeconds(1.5f);
        UpdatePlayerList();
    }

    // Tüm oyuncularýn hazýr olup olmadýðýný kontrol eder.
    private bool AreAllPlayersReady()
    {
        if (!_currentLobbyID.IsValid()) return false;

        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        // Eðer lobide sadece host varsa, oyunu direkt baþlatabilir (hazýr olmasýna gerek yok).
        if (numMembers <= 1) return true;

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);

            // Host oyuncunun hazýr olmasýna gerek yoktur, oyunu baþlatan odur.
            if (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID))
            {
                continue; // Host'u kontrol etme
            }

            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            if (readyStatus != "true")
            {
                return false; // Bir kiþi bile hazýr deðilse false döndür
            }
        }
        return true; // Herkes hazýr
    }
}