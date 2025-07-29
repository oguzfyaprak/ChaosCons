using UnityEngine;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Connection;
using FishNet.Transporting;

public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private List<Transform> spawnPoints = new List<Transform>();
    private NetworkManager networkManager;

    // Sahnenin sunucu tarafında yüklenip yüklenmediğini ve spawn'a hazır olup olmadığını takip eder.
    private bool _sceneReady = false;

    private void Awake()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("❌ NetworkManager bulunamadı!");
            return;
        }

        // Sadece sunucu tarafında gerçekleşen olaylara abone oluyoruz.
        // Oyuncu spawn etme sorumluluğu tamamen sunucudadır.
        networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;

        // FishNet'in kendi sahne yöneticisi olayına abone oluyoruz. Bu, Unity'nin OnSceneLoaded olayından daha güvenilirdir.
        networkManager.SceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
            networkManager.SceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;
        }
    }

    /// <summary>
    /// FishNet'in sahne yükleme işlemi sunucu tarafında tamamlandığında bu metod tetiklenir.
    /// </summary>
    private void SceneManager_OnLoadEnd(SceneLoadEndEventArgs args)
    {
        // Sadece sunucu tarafında çalışmasını sağlıyoruz. args.LoadedScenes boşsa bu client tarafıdır.
        if (args.LoadedScenes == null || args.LoadedScenes.Length == 0)
            return;

        // Sadece MainMap yüklendiğinde işlem yap.
        if (args.LoadedScenes[0].name == "MainMap")
        {
            Debug.Log("✅ Sunucu tarafında MainMap sahnesi yüklendi. Spawn noktaları aranıyor...");

            // Spawn noktalarını bul ve listeye ekle.
            spawnPoints.Clear();
            var foundPoints = GameObject.FindGameObjectsWithTag("RespawnPoint");
            foreach (var go in foundPoints)
                spawnPoints.Add(go.transform);

            Debug.Log($"✅ {spawnPoints.Count} adet RespawnPoint bulundu.");

            _sceneReady = true;

            // ÖNEMLİ: Sahne yüklendiğinde, Host'un kendi oyuncusunu hemen spawn etmeliyiz.
            // Host hem sunucu hem de istemcidir ve onun bağlantısı ClientManager üzerinden alınır.
            if (networkManager.IsServerStarted)
            {
                Debug.Log("✨ Sahne hazır ve sunucu çalışıyor. Host oyuncusu spawn ediliyor...");
                // ---------- HATA BURADAYDI, ŞİMDİ DÜZELTİLDİ ----------
                SpawnPlayer(networkManager.ClientManager.Connection);
                // --------------------------------------------------------
            }
        }
        else
        {
            _sceneReady = false;
        }
    }

    /// <summary>
    /// Uzaktaki bir istemci sunucuya bağlandığında tetiklenir.
    /// </summary>
    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        // Sadece yeni bir bağlantı kurulduğunda (Started) işlem yap.
        if (args.ConnectionState != RemoteConnectionState.Started)
            return;

        // Host'un kendisi bu olayla tetiklenmez, bu sadece remote client'lar içindir.
        // Eğer sahne hazırsa, yeni bağlanan istemci için hemen bir oyuncu spawn et.
        if (_sceneReady)
        {
            Debug.Log($"✨ Yeni istemci ({conn.ClientId}) bağlandı ve sahne hazır. Oyuncu spawn ediliyor...");
            SpawnPlayer(conn);
        }
        else
        {
            // Bu durum genellikle olmaz ama bir güvenlik önlemidir.
            // Eğer istemci bir şekilde sahne yüklenmeden bağlanırsa, sahne yüklendiğinde
            // tüm bağlı client'lar için spawn işlemi yapılabilir (bu kodda o kısım basitleştirildi).
            Debug.LogWarning($"⚠️ Client {conn.ClientId} bağlandı ama sahne henüz hazır değil. Spawn için beklenecek.");
        }
    }

    /// <summary>
    /// Belirtilen bağlantı (connection) için oyuncuyu spawn eder. Hem Host hem de Client'lar için bu metod kullanılır.
    /// </summary>
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

        // Basit bir spawn noktası seçme algoritması.
        int index = conn.ClientId % spawnPoints.Count;
        Transform spawnPoint = spawnPoints[index];

        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);

        // Sunucu, oluşturulan bu objeyi ağ üzerinde spawn eder ve sahipliğini 'conn' isimli bağlantıya verir.
        networkManager.ServerManager.Spawn(playerInstance, conn);

        Debug.Log($"✅ Oyuncu spawn edildi. ClientId: {conn.ClientId}, Konum: {spawnPoint.position}");
    }
}