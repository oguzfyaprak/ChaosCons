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
    // Konstante tan�mlar�
    private const string V = "Y�kleniyor...";

    // --- MainMenuManager'dan gelen Referanslar ---
    private NetworkManager _networkManager;
    private FishySteamworks.FishySteamworks _steamworksTransport;

    // UI Referanslar�
    private TMP_Text _lobbyIdText;
    private TMP_Text _playerListText;
    private GameObject _startGameButton;
    private GameObject _leaveLobbyButton;
    private GameObject _readyButton;
    private string _mainGameSceneName; // Ana oyun sahnesinin ad� (MainMenuManager'dan al�nacak)

    // Lobi Bilgisi
    private CSteamID _currentLobbyID; // Anl�k olarak ba�l� olunan lobi ID'si

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
            if (_startGameButton.TryGetComponent<Button>(out var button)) button.onClick.AddListener(OnClick_StartGame);
            else Debug.LogWarning("LobbyManager: StartGameButton �zerinde UnityEngine.UI.Button bile�eni bulunamad�.");
        }
        else Debug.LogWarning("LobbyManager: _startGameButton referans� atanmam��!");

        if (_leaveLobbyButton != null)
        {
            if (_leaveLobbyButton.TryGetComponent<Button>(out var button)) button.onClick.AddListener(OnClick_LeaveLobby);
            else Debug.LogWarning("LobbyManager: LeaveLobbyButton �zerinde UnityEngine.UI.Button bile�eni bulunamad�.");
        }
        else Debug.LogWarning("LobbyManager: _leaveLobbyButton referans� atanmam��!");

        if (_readyButton != null)
        {
            if (_readyButton.TryGetComponent<Button>(out var button)) button.onClick.AddListener(OnClick_Ready);
            else Debug.LogWarning("LobbyManager: ReadyButton �zerinde UnityEngine.UI.Button bile�eni bulunamad�.");
        }
        else Debug.LogWarning("LobbyManager: _readyButton referans� atanmam��!");
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

        // Lobiye girer girmez t�m �yelerin bilgilerini talep et.
        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            // Oyuncunun bilgilerini �nceden talep et. Bu, GetFriendPersonaName'in daha do�ru �al��mas�na yard�mc� olur.
            SteamFriends.RequestUserInformation(memberSteamID, false);
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
            Debug.LogWarning("Sadece lobi host'u oyunu ba�latabilir ve sunucu aktif olmal�d�r!");
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
            Debug.LogError("MainMenuManager bulunamad�. Ana men�ye d�nemedi.");
        }
    }

    public void OnClick_Ready()
    {
        if (!_currentLobbyID.IsValid()) return;

        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        string newReadyStatus = isCurrentlyReady ? "false" : "true";
        SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", newReadyStatus);

        Debug.Log($"Haz�r durumu g�ncellendi: {newReadyStatus}");
    }

    // Haz�r butonu metnini ve durumunu g�nceller
    public void UpdateReadyButtonState()
    {
        if (_readyButton == null) return;

        if (!_currentLobbyID.IsValid())
        {
            TMP_Text buttonTextInvalid = _readyButton.GetComponentInChildren<TMP_Text>();
            if (buttonTextInvalid != null) buttonTextInvalid.text = "Ge�ersiz Lobi";
            _readyButton.SetActive(false); // Ge�ersiz lobi ise butonu kapat
            return;
        }

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
    private Coroutine _refreshCoroutine;

    public void UpdatePlayerList()
    {
        if (_playerListText == null)
        {
            Debug.LogError("LobbyManager: _playerListText GameObject'i atanmam��! L�tfen Unity Inspector'da atay�n.");
            return; // E�er UI element� atanmam��sa daha fazla devam etme
        }

        if (!_currentLobbyID.IsValid())
        {
            _playerListText.text = "Lobiye ba�l� de�il.";
            return; // Ge�erli bir lobi ID'si yoksa devam etme
        }

        // Bu KONTROL �OK �NEML�: Steam API'si haz�r de�ilse hemen ��k
        if (!SteamManager.Initialized)
        {
            _playerListText.text = "Steam ba�lat�lamad� veya haz�r de�il.";
            Debug.LogWarning("SteamManager ba�lat�lmam�� veya hen�z haz�r de�il. Oyuncu listesi �ekilemiyor.");
            return; // Steam haz�r de�ilse hemen ��k
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
                Debug.LogWarning($"[LobbyManager] Ge�ersiz Steam ID bulundu: {memberSteamID}");
                personaName = "Ge�ersiz Oyuncu";
            }
            else
            {
                SteamFriends.RequestUserInformation(memberSteamID, true);

                try
                {
                    if (memberSteamID == SteamUser.GetSteamID())
                    {
                        personaName = SteamFriends.GetPersonaName(); // kendi ad�n i�in daha g�venli
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
                    Debug.LogWarning($"[LobbyManager] Steam ID {memberSteamID} i�in PersonaName al�namad�: {ex.Message}");
                    personaName = V;
                    hasUnknownNames = true;
                }
            }

            string hostIndicator = (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID)) ? " (Host)" : "";
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");

            string readyIndicator = "";
            if (hostIndicator != " (Host)")
            {
                readyIndicator = (readyStatus == "true") ? " (Haz�r)" : " (Haz�r De�il)";
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