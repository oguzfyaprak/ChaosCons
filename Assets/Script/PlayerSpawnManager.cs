using UnityEngine;
using UnityEngine.SceneManagement; // Unity'nin SceneManager'ı
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Transporting;
using System.Collections.Generic;
using FishNet.Managing.Scened; // FishNet'in SceneManager'ı
using System.Linq; // .Any() metodu için
using System.Collections; // Coroutine'ler için

public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private List<Transform> spawnPoints = new List<Transform>();
    private NetworkManager networkManager;

    // Host için oyuncunun zaten spawn edilip edilmediğini takip eden bayrak
    private bool _hostPlayerSpawned = false;
    // MainMap sahnesinin hazır olup olmadığını takip eden bayrak
    private bool _mainMapSceneReady = false;

    private void Awake()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("❌ NetworkManager bulunamadı!");
            return;
        }

        // Zaten abone olduğumuz olaylar:
        networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState; // Bu zaten genel bir olay

        // Eski sürümlerde olmayan OnServerStarted/OnClientStarted olaylarını kaldırıyoruz.
        // Bu olayların işlevselliğini ClientManager_OnClientConnectionState içinde birleştireceğiz.
        // networkManager.ServerManager.OnServerStarted += NetworkManager_OnServerStarted; // KALDIRILDI
        // networkManager.ClientManager.OnClientStarted += NetworkManager_OnClientStarted;   // KALDIRILDI
        // networkManager.ClientManager.OnClientStopped += NetworkManager_OnClientStopped;   // KALDIRILDI

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log("PlayerSpawnManager: Awake tamamlandı, olaylara abone olundu.");
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
            networkManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
            // Olay abonelikleri kaldırıldı.
            // networkManager.ServerManager.OnServerStarted -= NetworkManager_OnServerStarted; // KALDIRILDI
            // networkManager.ClientManager.OnClientStarted -= NetworkManager_OnClientStarted;   // KALDIRILDI
            // networkManager.ClientManager.OnClientStopped -= NetworkManager_OnClientStopped;   // KALDIRILDI
        }
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        Debug.Log("PlayerSpawnManager: OnDestroy tamamlandı, olay abonelikleri bırakıldı.");
    }

    // NetworkManager sunucu tarafı tamamen başladığında
    // Bu metodu doğrudan bir olaya abone etmek yerine,
    // ClientManager_OnClientConnectionState içinde IsServerStarted kontrolüyle kullanacağız.
    // private void NetworkManager_OnServerStarted() { } // KALDIRILDI

    // NetworkManager istemci tarafı tamamen başladığında
    // Bu metodu doğrudan bir olaya abone etmek yerine,
    // ClientManager_OnClientConnectionState içinde bağlantı durumuyla kullanacağız.
    // private void NetworkManager_OnClientStarted() { } // KALDIRILDI

    // NetworkManager istemci tarafı durduğunda
    // Bu metodu doğrudan bir olaya abone etmek yerine,
    // ClientManager_OnClientConnectionState içinde bağlantı durumuyla kullanacağız.
    // private void NetworkManager_OnClientStopped() { } // KALDIRILDI


    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMap")
        {
            var foundPoints = GameObject.FindGameObjectsWithTag("RespawnPoint");
            spawnPoints.Clear();
            foreach (var go in foundPoints)
                spawnPoints.Add(go.transform);

            Debug.Log($"✅ {spawnPoints.Count} adet RespawnPoint bulundu. MainMap Sahnesi Hazır.");
            _mainMapSceneReady = true; // Sahne hazır bayrağını işaretle
            CheckAndSpawnHost(); // Sahne hazır, şimdi ağ bağlantılarını bekleyebiliriz
        }
        else
        {
            _mainMapSceneReady = false; // Başka bir sahnedeysek sıfırla
            _hostPlayerSpawned = false; // Sahne değiştiğinde host spawn bayrağını sıfırla (önemli)
        }
    }

    // FishNet istemcisinin bağlantı durumu değiştiğinde çağrılır.
    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs args)
    {
        // İstemci BAŞLADIYSA: Host veya diğer istemciler için kontrol
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            Debug.Log($"PlayerSpawnManager: FishNet İstemcisi Başlatıldı. Client ID: {networkManager.ClientManager.Connection.ClientId}");
            CheckAndSpawnHost(); // İstemci başladı, şimdi diğer koşulları kontrol et
        }
        // İstemci DURDUYSA: Host spawn bayrağını sıfırla
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            _hostPlayerSpawned = false;
            Debug.Log("PlayerSpawnManager: FishNet İstemcisi Durduruldu, _hostPlayerSpawned sıfırlandı.");
        }
    }


    // Yeni: Hem sunucu, hem istemci, hem de sahne hazır olduğunda spawn'ı tetikleyen merkezi kontrol
    private void CheckAndSpawnHost()
    {
        // Sadece host için (ClientId 0) ve henüz spawn edilmemişse
        // ve hem sunucu hem de istemci aktifse ve sahne hazırsa
        if (networkManager.IsServerStarted &&
            networkManager.IsClientStarted && // Client'ın da başlatıldığını doğrula
            networkManager.ClientManager.Connection.ClientId == 0 && // Host'un client ID'si 0'dır
            _mainMapSceneReady &&
            !_hostPlayerSpawned)
        {
            Debug.Log("✨ Tüm koşullar sağlandı: Sunucu başlatıldı, İstemci başlatıldı, MainMap hazır. Host spawn ediliyor...");
            // Çok küçük bir gecikme ekleyelim, belki de hala bazı bileşenler kuruluyordur
            StartCoroutine(DelayedHostSpawn());
        }
        else
        {
            Debug.Log($"[CheckAndSpawnHost] Bekleniyor... Sunucu Başladı:{networkManager.IsServerStarted}, İstemci Başladı:{networkManager.IsClientStarted}, İstemci ID:{networkManager.ClientManager.Connection?.ClientId}, Sahne Hazır:{_mainMapSceneReady}, Spawn Edildi:{_hostPlayerSpawned}");
        }
    }

    // Remote clientlar bağlandığında (Sunucu tarafında çalışır)
    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Started)
            return;

        Debug.Log($"PlayerSpawnManager: ServerManager_OnRemoteConnectionState - Yeni istemci bağlandı: ClientId {conn.ClientId}");

        // Host dışındaki diğer istemciler için oyuncuyu spawn et
        // Sadece MainMap'teysak ve spawn noktaları hazırsa
        if (conn.ClientId != 0 && _mainMapSceneReady)
        {
            SpawnPlayer(conn);
        }
        else if (!_mainMapSceneReady)
        {
            Debug.LogWarning($"⚠️ Client {conn.ClientId} bağlandı ama MainMap henüz hazır değil. Spawn için beklenecek.");
        }
    }

    // Host oyuncusunu gecikmeli olarak spawn eden Coroutine
    private IEnumerator DelayedHostSpawn()
    {
        // Daha önce denediğimiz 0.2f saniye yetmediyse, bunu biraz artırabiliriz.
        // Amaç, tüm FishNet iç mekanizmalarının ve Unity'nin yaşam döngüsünün tam oturması.
        yield return new WaitForSeconds(0.5f); // 0.5 saniye bekleyelim

        TrySpawnLocalHost();
    }


    // Sadece Host (sunucu ve istemci aynı anda) için oyuncuyu spawn eder.
    private void TrySpawnLocalHost()
    {
        if (_hostPlayerSpawned)
        {
            Debug.Log("PlayerSpawnManager: Host oyuncusu zaten spawn edilmiş, tekrar spawn edilmiyor.");
            return;
        }

        NetworkConnection conn = networkManager.ClientManager.Connection;
        // Son bir kontrol: Bağlantı hala aktif mi ve host mu?
        if (conn != null && conn.ClientId == 0 && networkManager.IsServerStarted && conn.IsActive)
        {
            SpawnPlayer(conn);
            _hostPlayerSpawned = true; // Host oyuncusunun spawn edildiğini işaretle
            Debug.Log("PlayerSpawnManager: Host oyuncusu başarıyla spawn edildi.");
        }
        else
        {
            Debug.LogWarning("PlayerSpawnManager: Host oyuncusu spawn edilemedi. Koşullar sağlanmadı veya bağlantı aktif değil.");
        }
    }

    // Belirtilen bağlantı için bir oyuncu örneği oluşturup ağa spawn eder.
    private void SpawnPlayer(NetworkConnection conn)
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("⚠️ Oyuncu prefabı eksik. Oyuncu spawn edilemedi.");
            return;
        }
        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning("⚠️ Spawn noktası eksik. Oyuncu spawn edilemedi.");
            return;
        }

        int index = (int)(conn.ClientId % spawnPoints.Count);
        Transform spawnPoint = spawnPoints[index];

        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        // ÖNEMLİ: FishNet'in NetworkManager'ına playerPrefab'ın kayıtlı olduğundan emin olun!
        networkManager.ServerManager.Spawn(playerInstance, conn);
        Debug.Log($"✅ Oyuncu spawn edildi. ClientId: {conn.ClientId}, Konum: {spawnPoint.position}");
    }
}