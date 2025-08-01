using UnityEngine;
using TMPro;
using FishNet.Managing;
using FishNet.Transporting;
using FishySteamworks;
using Steamworks;
using System.Text;
using FishNet.Managing.Scened;
using UnityEngine.UI;
using System.Linq;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

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

    private Callback<PersonaStateChange_t> _personaStateChangeCallback;
    private Callback<LobbyDataUpdate_t> _lobbyDataUpdateCallback;

    private void OnEnable()
    {
        _personaStateChangeCallback = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
        _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
    }

    private void OnDisable()
    {
        _personaStateChangeCallback?.Dispose();
        _lobbyDataUpdateCallback?.Dispose();
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback)
    {
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            Debug.Log("[LobbyManager] → OnLobbyDataUpdate çağrıldı. Oyuncu listesi güncelleniyor.");
            UpdatePlayerList();
        }
    }

    private void OnPersonaStateChange(PersonaStateChange_t pCallback)
    {
        // Yalnızca mevcut lobiye ait oyuncu değişiklikleriyle ilgileniyoruz.
        if (_currentLobbyID.IsValid() && SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, 0) != CSteamID.Nil)
        {
            // PersonaState değiştiğinde oyuncu listesini yeniden güncelleyelim.
            // Bu, "Adı Yükleniyor..." yazısının yerini doğru adın almasını sağlar.
            Debug.Log($"[LobbyManager] → PersonaState değişti: {pCallback.m_ulSteamID}. Liste güncellenecek.");
            UpdatePlayerList();
        }
    }

    private bool IsMemberOfLobby(CSteamID steamID)
    {
        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        for (int i = 0; i < numMembers; i++)
        {
            if (SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i) == steamID)
                return true;
        }
        return false;
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
    }

    public void InitializeLobbyUI(CSteamID lobbyID)
    {
        // Lobi ID'sini ve UI referanslarını günceller.
        _currentLobbyID = lobbyID;
        MainMenuManager.staticLobbyID = lobbyID;

        // Lobi ID'sini ekrana yazar ve panoya kopyalar.
        // _lobbyIdText'in null olup olmadığını kontrol eder.
        if (_lobbyIdText != null)
        {
            _lobbyIdText.text = $"Lobi ID: {_currentLobbyID.m_SteamID}";
        }

        // Panoya kopyalama işlemini sadece geçerli bir lobi ID'si varsa yapar.
        if (_currentLobbyID.IsValid())
        {
            GUIUtility.systemCopyBuffer = _currentLobbyID.m_SteamID.ToString();
            Debug.Log($"[LobbyManager] → Lobi ID panoya kopyalandı: {_currentLobbyID.m_SteamID}");
        }

        // Oyuncu listesini ilk kez günceller.
        UpdatePlayerList();

        // Host'un kim olduğunu belirler.
        CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(_currentLobbyID);
        CSteamID localSteamID = SteamUser.GetSteamID();
        bool isHost = (lobbyOwner == localSteamID);

        Debug.Log($"[LobbyManager] → UI hazırlanıyor. Bu oyuncu host mu? {isHost}");

        // UI'ı host veya client durumuna göre ayarlar.
        if (_startGameButton != null)
        {
            _startGameButton.SetActive(isHost);
        }

        // Hazır butonu, host değilse görünür olur.
        if (_readyButton != null)
        {
            _readyButton.SetActive(!isHost);
            // Client ise hazır butonunun metnini günceller.
            if (!isHost)
            {
                UpdateReadyButtonState();
            }
        }
    }

    public void OnClick_StartGame()
    {
        var localSteamId = SteamUser.GetSteamID();
        var lobbyOwner = SteamMatchmaking.GetLobbyOwner(_currentLobbyID);

        if (!_networkManager.IsServerStarted || lobbyOwner != localSteamId)
        {
            Debug.LogWarning("Sadece host oyunu başlatabilir!");
            return;
        }

        if (!AreAllPlayersReady())
        {
            Debug.LogWarning("Tüm oyuncular hazır değil!");
            return;
        }

        Debug.Log("[LobbyManager] → Oyuna geçiliyor, sahne yükleniyor...");
        SteamMatchmaking.SetLobbyData(_currentLobbyID, "GameStarted", "true");

        var mainMenu = FindFirstObjectByType<MainMenuManager>();
        mainMenu?.LoadMainGameScene();
    }

    public void OnClick_LeaveLobby()
    {
        Debug.Log("[LobbyManager] → Lobiden çıkılıyor.");
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
        var localPlayer = Object.FindObjectsByType<PlayerSteamInfo>(FindObjectsSortMode.None)
                                .FirstOrDefault(p => p.IsOwner);
        if (localPlayer != null)
        {
            localPlayer.ServerToggleReadyStatus();
            Debug.Log("[LobbyManager] → Hazır durumu değiştirildi.");
        }
        else
        {
            Debug.LogWarning("[LobbyManager] → Owner player bulunamadı.");
        }
    }

    public void UpdateReadyButtonState()
    {
        if (_readyButton == null) return;

        string currentStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isReady = (currentStatus == "true");

        var text = _readyButton.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = isReady ? "Hazır (Bekle)" : "Hazır Ol";
    }

    public void UpdatePlayerList()
    {
        if (_playerListText == null) return;

        if (_currentLobbyID.IsValid())
        {
            Debug.Log("[LobbyManager] → UpdatePlayerList: Steam üzerinden veri çekiliyor.");
            UpdateLobbyPlayerList_Steam();
        }
        else if (UnitySceneManager.GetActiveScene().name == "MainMap")
        {
            Debug.Log("[LobbyManager] → UpdatePlayerList: Sahne MainMap, oyuncular prefab üzerinden listelenecek.");
            UpdateLobbyPlayerList_FromPlayers();
        }
        else
        {
            Debug.LogWarning("[LobbyManager] → Lobi ID geçersiz, sahne MainMap değil.");
            _playerListText.text = "Oyuncular yüklenemedi.";
        }
    }

    // LobbyManager.cs
    // Hata alınan yer: UpdateLobbyPlayerList_Steam() metodunun 240. satırı.
    // Bu metodu aşağıdaki gibi yeniden düzenleyin:

    private void UpdateLobbyPlayerList_Steam()
    {
        if (_currentLobbyID == CSteamID.Nil || !_currentLobbyID.IsValid())
        {
            if (_playerListText != null)
            {
                _playerListText.text = "Lobiye bağlı değil.";
            }
            return;
        }

        // UI referansının null olup olmadığını kontrol et
        if (_playerListText == null)
        {
            Debug.LogError("LobbyManager: PlayerListText referansı boş!");
            return;
        }

        string playerList = "Oyuncular:\n";
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);

            // Hata Giderme: SteamID geçerli mi kontrolü
            if (!memberID.IsValid())
            {
                Debug.LogWarning($"[LobbyManager] Geçersiz SteamID bulundu. İsim çekilemiyor.");
                continue;
            }

            // Hata Giderme: Oyuncu adının null olup olmadığını kontrol et
            // Steam'den veri henüz indirilmemiş olabilir. Bu durumda hata alırsınız.
            string playerName;

            // SteamFriends.GetFriendPersonaName, bazen null veya boş bir değer döndürebilir.
            // Bu durumu yakalamak için try-catch bloğu kullanabiliriz.
            // Ancak daha temiz bir çözüm, verinin hazır olup olmadığını kontrol etmektir.
            // Steam'in kendi kütüphanesinde bu konuda net bir metot yok, bu yüzden
            // en güvenli yol, döndürülen değerin geçerliliğini kontrol etmektir.

            try
            {
                playerName = SteamFriends.GetFriendPersonaName(memberID);
            }
            catch (System.NullReferenceException)
            {
                // Steam API'sinden ad çekilirken hata oluşursa
                playerName = "Adı Yükleniyor...";
            }

            if (string.IsNullOrEmpty(playerName))
            {
                // Eğer hala boşsa, geçici bir değer ata
                playerName = "Adı Yükleniyor...";
            }

            // Oyuncunun hazır durumunu kontrol et
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberID, "ReadyStatus") == "true" ? "[HAZIR]" : "[HAZIR DEĞİL]";

            playerList += $"- {playerName} {readyStatus}\n";
        }

        _playerListText.text = playerList;
    }
    private void UpdateLobbyPlayerList_FromPlayers()
    {
        var players = Object.FindObjectsByType<PlayerSteamInfo>(FindObjectsSortMode.None);
        if (players.Length == 0)
        {
            _playerListText.text = "Sahneye oyuncular yüklenmedi.";
            return;
        }

        StringBuilder sb = new StringBuilder("Oyuncular (Sahne içi):\n");
        foreach (var player in players)
        {
            string name = string.IsNullOrEmpty(player.SteamName.Value) ? "Yükleniyor..." : player.SteamName.Value;
            string ready = player.IsReady.Value ? "Hazır" : "Hazır Değil";
            sb.AppendLine($"- {name} ({ready})");
        }

        _playerListText.text = sb.ToString();
    }

    private bool AreAllPlayersReady()
    {
        var players = Object.FindObjectsByType<PlayerSteamInfo>(FindObjectsSortMode.None);
        if (players.Length <= 1) return true;

        foreach (var player in players)
        {
            if (player.IsServerStarted) continue;
            if (!player.IsReady.Value) return false;
        }

        return true;
    }
}
