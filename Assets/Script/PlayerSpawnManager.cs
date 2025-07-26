using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Connection;
using System.Collections.Generic;
using FishNet.Transporting;

public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private List<Transform> spawnPoints = new List<Transform>();
    private NetworkManager networkManager;

    private void Awake()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("❌ NetworkManager bulunamadı!");
            return;
        }

        networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (networkManager != null)
            networkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMap") // Sahne adını kontrol et
        {
            var foundPoints = GameObject.FindGameObjectsWithTag("RespawnPoint");
            spawnPoints.Clear();
            foreach (var go in foundPoints)
                spawnPoints.Add(go.transform);

            Debug.Log($"✅ {spawnPoints.Count} adet RespawnPoint bulundu.");
        }
    }

    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != FishNet.Transporting.RemoteConnectionState.Started)
            return;

        if (playerPrefab == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("⚠️ Oyuncu prefabı veya spawn noktası eksik.");
            return;
        }

        int index = (int)(conn.ClientId % spawnPoints.Count);
        Transform spawnPoint = spawnPoints[index];

        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        networkManager.ServerManager.Spawn(playerInstance, conn);
    }

    private void ServerManager_OnClientConnectionState(NetworkConnection conn, ClientConnectionStateArgs args)
    {
        if (args.ConnectionState != FishNet.Transporting.LocalConnectionState.Started)
            return;

        if (playerPrefab == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("⚠️ Oyuncu prefabı veya spawn noktası eksik.");
            return;
        }

        int index = (int)(conn.ClientId % spawnPoints.Count);
        Transform spawnPoint = spawnPoints[index];

        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        networkManager.ServerManager.Spawn(playerInstance, conn);
    }
}
