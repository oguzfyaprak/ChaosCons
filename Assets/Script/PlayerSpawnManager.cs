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
    private bool _sceneReady = false;
    private HashSet<int> _spawnedPlayers = new HashSet<int>();

    private void Awake()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager bulunamadı!");
            return;
        }

        networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        networkManager.SceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
    }

    private void SceneManager_OnLoadEnd(SceneLoadEndEventArgs args)
    {
        if (args.LoadedScenes == null || args.LoadedScenes.Length == 0 ||
            args.LoadedScenes[0].name != "MainMap")
            return;

        spawnPoints.Clear();
        var foundPoints = GameObject.FindGameObjectsWithTag("RespawnPoint");
        if (foundPoints.Length == 0)
        {
            Debug.LogError("Sahnede RespawnPoint tag'li obje bulunamadı!");
            return;
        }

        foreach (var go in foundPoints)
            spawnPoints.Add(go.transform);

        _sceneReady = true;

        _spawnedPlayers.Clear(); // Sahne her yüklendiğinde spawn kayıtlarını temizle

        if (networkManager.IsServerStarted && networkManager.ClientManager.Connection != null)
        {
            SpawnPlayer(networkManager.ClientManager.Connection);
        }
    }

    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started && _sceneReady)
        {
            SpawnPlayer(conn);
        }
    }

    private void SpawnPlayer(NetworkConnection conn)
    {
        if (_spawnedPlayers.Contains(conn.ClientId))
        {
            Debug.LogWarning($"Client {conn.ClientId} zaten spawn edilmiş!");
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("Player prefabı atanmamış!");
            return;
        }

        if (spawnPoints.Count == 0)
        {
            Debug.LogError("Spawn noktası bulunamadı!");
            return;
        }

        int index = conn.ClientId % spawnPoints.Count;
        Transform spawnPoint = spawnPoints[index];

        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);

        try
        {
            networkManager.ServerManager.Spawn(playerInstance, conn);
            _spawnedPlayers.Add(conn.ClientId);
            Debug.Log($"Oyuncu spawn edildi. ClientId: {conn.ClientId}, Konum: {spawnPoint.position}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Oyuncu spawn edilemedi: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
            networkManager.SceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;
        }
    }
}