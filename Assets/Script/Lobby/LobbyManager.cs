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
    // Konstante tanımları
    private const string V = "Yükleniyor...";

    // --- MainMenuManager'dan gelen Referanslar ---
    private NetworkManager _networkManager;
    private FishySteamworks.FishySteamworks _steamworksTransport;

    // UI Referansları
    private TMP_Text _lobbyIdText;
    private TMP_Text _playerListText;
    private GameObject _startGameButton;
    private GameObject _leaveLobbyButton;
    private GameObject _readyButton;
    private string _mainGameSceneName; // Ana oyun sahnesinin adı (MainMenuManager'dan alınacak)

    // Lobi Bilgisi
    private CSteamID _currentLobbyID; // Anlık olarak bağlı olunan lobi ID'si

    // Initialize fonksiyonu MainMenuManager tarafından çağrılacak
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

        // UI buton olaylarını burada dinlemeye başla (önceki hata düzeltmeleri uygulandı)
        if (_startGameButton != null)
        {
            if (_startGameButton.TryGetComponent<Button>(out var button)) button.onClick.AddListener(OnClick_StartGame);
            else Debug.LogWarning("LobbyManager: StartGameButton üzerinde UnityEngine.UI.Button bileşeni bulunamadı.");
        }
        else Debug.LogWarning("LobbyManager: _startGameButton referansı atanmamış!");

        if (_leaveLobbyButton != null)
        {
            if (_leaveLobbyButton.TryGetComponent<Button>(out var button)) button.onClick.AddListener(OnClick_LeaveLobby);
            else Debug.LogWarning("LobbyManager: LeaveLobbyButton üzerinde UnityEngine.UI.Button bileşeni bulunamadı.");
        }
        else Debug.LogWarning("LobbyManager: _leaveLobbyButton referansı atanmamış!");

        if (_readyButton != null)
        {
            if (_readyButton.TryGetComponent<Button>(out var button)) button.onClick.AddListener(OnClick_Ready);
            else Debug.LogWarning("LobbyManager: ReadyButton üzerinde UnityEngine.UI.Button bileşeni bulunamadı.");
        }
        else Debug.LogWarning("LobbyManager: _readyButton referansı atanmamış!");
    }

    // Lobi UI'ını başlatma (MainMenuManager tarafından lobiye girildiğinde çağrılır)
    public void InitializeLobbyUI(CSteamID lobbyID)
    {
        _currentLobbyID = lobbyID;
        if (_lobbyIdText != null)
        {
            _lobbyIdText.text = $"Lobi ID: {_currentLobbyID.m_SteamID}";
            GUIUtility.systemCopyBuffer = _currentLobbyID.m_SteamID.ToString(); // Lobi ID'yi panoya kopyala
            Debug.Log($"Lobi ID panoya kopyalandı: {_currentLobbyID.m_SteamID}");
        }

        // Lobiye girer girmez tüm üyelerin bilgilerini talep et.
        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            // Oyuncunun bilgilerini önceden talep et. Bu, GetFriendPersonaName'in daha doğru çalışmasına yardımcı olur.
            SteamFriends.RequestUserInformation(memberSteamID, false);
        }

        UpdatePlayerList(); // Başlangıçta oyuncu listesini güncelle

        // Buton görünürlüklerini ayarla: Host ise Oyunu Başlat, Client ise Hazır Ol
        if (SteamMatchmaking.GetLobbyOwner(_currentLobbyID) == SteamUser.GetSteamID()) // Host ise
        {
            if (_startGameButton != null) _startGameButton.SetActive(true);
            if (_readyButton != null) _readyButton.SetActive(false); // Host'un hazır olması gerekmez
        }
        else // Client ise
        {
            if (_startGameButton != null) _startGameButton.SetActive(false);
            if (_readyButton != null) _readyButton.SetActive(true);
            UpdateReadyButtonState(); // Hazır butonu durumunu güncelle
        }
    }

    public void OnClick_StartGame()
    {
        var localSteamId = SteamUser.GetSteamID();
        var lobbyOwner = SteamMatchmaking.GetLobbyOwner(_currentLobbyID);
        Debug.Log($"[StartGame] LocalSteamID: {localSteamId}, LobbyOwner: {lobbyOwner}");

        // Sadece lobi host'u ve sunucu aktifse oyunu başlatabilir
        if (!_networkManager.IsServerStarted || lobbyOwner != localSteamId)
        {
            Debug.LogWarning("Sadece lobi host'u oyunu başlatabilir ve sunucu aktif olmalıdır!");
            return;
        }

        // Tüm oyuncular hazır mı kontrol et
        if (!AreAllPlayersReady())
        {
            Debug.LogWarning("Tüm oyuncular hazır değil! Oyun başlatılamaz.");
            return;
        }

        Debug.Log("Oyunu başlatılıyor... Ana oyun sahnesine geçiliyor.");

        // Lobide oyunun başladığını belirten bir veri ayarla
        SteamMatchmaking.SetLobbyData(_currentLobbyID, "GameStarted", "true");

        // Sunucu olarak ana oyun sahnesine geçişi FishNet ile yap
        // Client'lar MainMenuManager'ın LobbyDataUpdate callback'i ile bu veriyi görüp kendi sahnelerini yükleyecek.
        MainMenuManager mainMenu = FindFirstObjectByType<MainMenuManager>(); // MainMenuManager'a erişim
        if (mainMenu != null)
        {
            mainMenu.LoadMainGameScene();
        }
        else
        {
            Debug.LogError("LobbyManager: MainMenuManager bulunamadı! Sahne yüklenemedi.");
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
            Debug.LogError("MainMenuManager bulunamadı. Ana menüye dönemedi.");
        }
    }

    public void OnClick_Ready()
    {
        if (!_currentLobbyID.IsValid()) return;

        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        string newReadyStatus = isCurrentlyReady ? "false" : "true";
        SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", newReadyStatus);

        Debug.Log($"Hazır durumu güncellendi: {newReadyStatus}");
    }

    // Hazır butonu metnini ve durumunu günceller
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
            buttonText.text = isCurrentlyReady ? "Hazır (Bekle)" : "Hazır Ol";
        }
    }

    // --- Yardımcı Fonksiyonlar ---
    // Oyuncu listesini günceller ve UI'a yansıtır.
    private Coroutine _refreshCoroutine;

    public void UpdatePlayerList()
    {
        if (_playerListText == null)
        {
            Debug.LogError("LobbyManager: _playerListText GameObject'i atanmamış! Lütfen Unity Inspector'da atayın.");
            return;
        }

        if (!_currentLobbyID.IsValid())
        {
            _playerListText.text = "Lobiye bağlı değil.";
            return;
        }

        if (!SteamManager.Initialized)
        {
            _playerListText.text = "Steam başlatılamadı veya hazır değil.";
            Debug.LogWarning("SteamManager başlatılmamış veya henüz hazır değil. Oyuncu listesi çekilemiyor.");
            return;
        }

        StringBuilder sb = new("Oyuncular:\n");
        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            string personaName = "Player"; // 🔐 Varsayılan değer

            if (!memberSteamID.IsValid())
            {
                Debug.LogWarning($"[LobbyManager] Geçersiz Steam ID bulundu: {memberSteamID}");
                personaName = "Player";
            }
            else
            {
                SteamFriends.RequestUserInformation(memberSteamID, true);

                try
                {
                    if (memberSteamID == SteamUser.GetSteamID())
                    {
                        personaName = SteamFriends.GetPersonaName();
                    }
                    else
                    {
                        string fetchedName = SteamFriends.GetFriendPersonaName(memberSteamID);
                        if (!string.IsNullOrEmpty(fetchedName) && fetchedName != "[unknown]")
                            personaName = fetchedName;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[LobbyManager] Steam ID {memberSteamID} için isim alınamadı: {ex.Message}");
                    // personaName zaten "Player"
                }
            }

            string hostIndicator = (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID)) ? " (Host)" : "";
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");

            string readyIndicator = "";
            if (hostIndicator != " (Host)")
            {
                readyIndicator = (readyStatus == "true") ? " (Hazır)" : " (Hazır Değil)";
            }

            sb.AppendLine($"- {personaName}{hostIndicator}{readyIndicator}");
        }

        _playerListText.text = sb.ToString();
    }

    private IEnumerator RefreshPlayerListWithDelay()
    {
        yield return new WaitForSeconds(1.5f);
        UpdatePlayerList();
    }

    // Tüm oyuncuların hazır olup olmadığını kontrol eder.
    private bool AreAllPlayersReady()
    {
        if (!_currentLobbyID.IsValid()) return false;

        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        // Eğer lobide sadece host varsa, oyunu direkt başlatabilir (hazır olmasına gerek yok).
        if (numMembers <= 1) return true;

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);

            // Host oyuncunun hazır olmasına gerek yoktur, oyunu başlatan odur.
            if (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID))
            {
                continue; // Host'u kontrol etme
            }

            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            if (readyStatus != "true")
            {
                return false; // Bir kişi bile hazır değilse false döndür
            }
        }
        return true; // Herkes hazır
    }
}