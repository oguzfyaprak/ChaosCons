using UnityEngine;
using TMPro;
using FishNet.Managing;
using FishNet.Transporting;
using FishySteamworks;
using Steamworks;
using System.Text;
using FishNet.Managing.Scened;
using UnityEngine.UI; // Button bileþeni için

public class LobbyManager : MonoBehaviour
{
    // --- MainMenuManager'dan gelen Referanslar ---
    // Bu alanlar SerializeField DEÐÝLDÝR, çünkü MainMenuManager tarafýndan runtime'da atanacaklar.
    private NetworkManager _networkManager;
    private FishySteamworks.FishySteamworks _steamworksTransport;

    private TMP_Text _lobbyIdText;
    private TMP_Text _playerListText;
    private GameObject _startGameButton;
    private GameObject _leaveLobbyButton;
    private GameObject _readyButton;
    private string _mainGameSceneName; // Ana oyun sahnesinin adý (MainMenuManager'dan alýnacak)

    // Lobi Bilgisi
    private CSteamID _currentLobbyID; // Anlýk olarak baðlý olunan lobi ID'si
    // MainMenuManager.staticLobbyID statik lobi ID'sini tutuyor.

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
            var button = _startGameButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_StartGame);
            else Debug.LogWarning("LobbyManager: StartGameButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
        if (_leaveLobbyButton != null)
        {
            var button = _leaveLobbyButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_LeaveLobby);
            else Debug.LogWarning("LobbyManager: LeaveLobbyButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
        if (_readyButton != null)
        {
            var button = _readyButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_Ready);
            else Debug.LogWarning("LobbyManager: ReadyButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
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
            Debug.LogWarning("Sadece lobi host'u oyunu baþlatabilir!");
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
        Debug.Log("Lobiden ayrýlýyor...");

        if (_currentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(_currentLobbyID);
            Debug.Log($"Lobiden ayrýlýndý: {_currentLobbyID}");
            MainMenuManager.staticLobbyID = CSteamID.Nil; // Statik lobi ID'sini sýfýrla
        }

        // Network baðlantýlarýný durdur (Host veya Client olmasýna göre)
        if (_networkManager.IsServerStarted)
        {
            _networkManager.ServerManager.StopConnection(true);
        }
        else if (_networkManager.IsClientStarted)
        {
            _networkManager.ClientManager.StopConnection();
        }

        // Lobiden ayrýlýnca ana menüye dönüþü MainMenuManager yönetecek.
        // Bu metot MainMenuManager'dan çaðrýldýðýnda zaten MainMenuManager paneli deðiþtirecek.
    }

    public void OnClick_Ready()
    {
        if (!_currentLobbyID.IsValid()) return;

        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        string newReadyStatus = isCurrentlyReady ? "false" : "true";
        SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", newReadyStatus);

        Debug.Log($"Hazýr durumu güncellendi: {newReadyStatus}");
        // Not: Lobi verisi güncellendiðinde Steam'in OnLobbyDataUpdate callback'i tetiklenir.
        // MainMenuManager bu callback'i dinleyip UpdatePlayerList ve UpdateReadyButtonState'i çaðýracaktýr.
        // Bu yüzden burada tekrar çaðýrmaya gerek yoktur, çifte çaðrýyý engeller.
    }

    // Hazýr butonu metnini ve durumunu günceller
    public void UpdateReadyButtonState()
    {
        if (_readyButton == null) return;

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

        StringBuilder sb = new StringBuilder("Oyuncular:\n");
        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            string personaName = "Yükleniyor...";

            if (!memberSteamID.IsValid() || memberSteamID.m_SteamID == 0)
            {
                Debug.LogWarning($"[LobbyManager] Geçersiz Steam ID bulundu: {memberSteamID.m_SteamID}");
                personaName = "Geçersiz Oyuncu";
            }
            else
            {
                if (memberSteamID == SteamUser.GetSteamID())
                {
                    // Kendi adını doğrudan al
                    personaName = SteamFriends.GetPersonaName();
                }
                else
                {
                    try
                    {
                        // Bilgi hazır mı kontrol et (false: sadece kontrol)
                        bool isLoaded = SteamFriends.RequestUserInformation(memberSteamID, false);

                        if (!isLoaded)
                        {
                            // Değilse yüklenmesini iste (true: bilgi istendi)
                            SteamFriends.RequestUserInformation(memberSteamID, true);
                            personaName = "Yükleniyor...";
                            Debug.Log($"[LobbyManager] Oyuncu adı henüz hazır değil, bilgi istendi. ID: {memberSteamID}");
                        }
                        else
                        {
                            personaName = SteamFriends.GetFriendPersonaName(memberSteamID);
                            if (string.IsNullOrEmpty(personaName))
                            {
                                personaName = "Yükleniyor...";
                                Debug.Log($"[LobbyManager] Oyuncu adı boş geldi, bekleniyor. ID: {memberSteamID}");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        // ex.ToString() bize hatanın hangi satırda, hangi fonksiyonda
                        // ve hangi dosyanın içinde olduğunu tam olarak gösterir.
                        Debug.LogWarning($"[LobbyManager] Steam adı alınamadı. ID: {memberSteamID}\nTam Hata: {ex.ToString()}");
                        personaName = "Bilinmeyen Oyuncu";
                    }
                }
            }

            // Host oyuncu mu?
            string hostIndicator = (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID)) ? " (Host)" : "";

            // Hazır durumu
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            string readyIndicator = "";

            if (hostIndicator != " (Host)")
            {
                readyIndicator = (readyStatus == "true") ? " (Hazır)" : " (Hazır Değil)";
            }

            sb.AppendLine($"- {personaName}{hostIndicator}{readyIndicator}");
        }

        _playerListText.text = sb.ToString();
        UpdateReadyButtonState(); // Kendi hazır ol butonunu da güncelle
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
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i); // Hata düzeltildi: _currentLobyID -> _currentLobbyID

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