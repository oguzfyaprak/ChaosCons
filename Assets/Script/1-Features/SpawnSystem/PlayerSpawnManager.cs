using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using Game.Player;
using Game.Utils;
using UnityEngine;

namespace Game
{
    public class DualStageSpawnManager : BaseMonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject lobbyAvatarPrefab;
        [SerializeField] private GameObject gamePlayerPrefab;

        [Header("Spawn (MainMap)")]
        [SerializeField] private string gameSceneName = "MainMap";
        [SerializeField] private string respawnTag = "RespawnPoint";

        private readonly Dictionary<int, NetworkObject> _lobbyAvatars = new(); // ClientId -> Lobby avatar
        private readonly List<Transform> _gameSpawnPoints = new();

        // Client sahnesi yüklenmeyi bitirdiğinde kontrol açmak için bekleyen PC’ler
        private readonly Dictionary<int, PlayerController> _pendingControllers = new(); // ClientId -> PC

        protected override void RegisterEvents()
        {
            PlayerConnectionManager.S_OnConnect += OnClientConnected;

            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnLoadEnd += OnSceneLoaded;
        }

        protected override void UnregisterEvents()
        {
            PlayerConnectionManager.S_OnConnect -= OnClientConnected;

            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnLoadEnd -= OnSceneLoaded;
        }

        private bool IsActiveScene(string sceneName)
            => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == sceneName;

        private bool IsLoadedScene(SceneLoadEndEventArgs e, string sceneName)
        {
            foreach (var s in e.LoadedScenes)
                if (s.name == sceneName) return true;
            return false;
        }

        /* === AŞAMA 1: LOBİDE BAĞLANAN OYUNCU === */
        private void OnClientConnected(NetworkConnection conn)
        {
            if (IsActiveScene(EScenes.MainMenu.ToString()))
            {
                SpawnLobbyAvatar(conn);
                return;
            }

            if (IsActiveScene(gameSceneName))
            {
                // Geç join ise burada spawn ETME.
                Debug.Log($"[DualStage] {conn.ClientId} joined while in {gameSceneName}. Waiting OnSceneLoaded...");
                return;
            }
        }

        private void SpawnLobbyAvatar(NetworkConnection conn)
        {
            int index = PlayerConnectionManager.Instance.AllClients.Count;
            Transform sp = SpawnCache.Instance.SpawnPoints[index % SpawnCache.Instance.SpawnPoints.Count()].transform;

            NetworkObject nob = InstanceFinder.NetworkManager.GetPooledInstantiated(lobbyAvatarPrefab, sp.position, sp.rotation, true);
            InstanceFinder.ServerManager.Spawn(nob, conn);

            if (nob.TryGetComponent(out PlayerController pc))
                pc.TargetForceLobbyState(conn); // input/CC kapalı

            _lobbyAvatars[conn.ClientId] = nob;

            Debug.Log($"[DualStage] LobbyAvatar spawned for {conn.ClientId} @ {sp.position}");
        }

        /* === AŞAMA 2: MAINMAP YÜKLENİNCE === */
        private void OnSceneLoaded(SceneLoadEndEventArgs args)
        {
            if (!IsLoadedScene(args, gameSceneName))
                return;
            if (!InstanceFinder.IsServerStarted)
                return;

            _gameSpawnPoints.Clear();
            foreach (var go in GameObject.FindGameObjectsWithTag(respawnTag))
                _gameSpawnPoints.Add(go.transform);

            foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            {
                if (_lobbyAvatars.TryGetValue(conn.ClientId, out var lob) && lob)
                    InstanceFinder.ServerManager.Despawn(lob);

                _lobbyAvatars.Remove(conn.ClientId);
                SpawnGamePlayer(conn, conn.ClientId);
            }
        }

        private readonly HashSet<int> _spawnedGame = new();
        private void SpawnGamePlayer(NetworkConnection owner, int pickIndex)
        {
            if (_spawnedGame.Contains(owner.ClientId))
            {
                Debug.LogWarning($"[DualStage] {owner.ClientId} zaten spawn edilmiş, atlıyorum.");
                return;
            }

            Transform sp = _gameSpawnPoints.Count > 0
                ? _gameSpawnPoints[pickIndex % _gameSpawnPoints.Count]
                : SpawnCache.Instance.SpawnPoints[pickIndex % SpawnCache.Instance.SpawnPoints.Count()].transform;

            var go = Instantiate(gamePlayerPrefab, sp.position, sp.rotation);
            InstanceFinder.ServerManager.Spawn(go, owner);
            _spawnedGame.Add(owner.ClientId);

            if (go.TryGetComponent<PlayerController>(out var pc))
            {
                owner.OnLoadedStartScenes -= OnOwnerLoadedStartScenes; // tek abonelik
                _pendingControllers[owner.ClientId] = pc;
                owner.OnLoadedStartScenes += OnOwnerLoadedStartScenes;
            }

            Debug.Log($"[DualStage] GamePlayer spawned for {owner.ClientId} @ {sp.position}");
        }

        // Client kendi start sahnelerini (bu oyun sahnesini) yüklemeyi bitirdiğinde tetiklenir
        private void OnOwnerLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            try
            {
                if (_pendingControllers.TryGetValue(conn.ClientId, out var pc) && pc != null)
                {
                    pc.TargetForceGameState(conn); // input ON, CC ON
                    Debug.Log($"[DualStage] TargetForceGameState sent → conn {conn.ClientId}");
                }
                else
                {
                    Debug.LogWarning($"[DualStage] Pending PC not found for conn {conn.ClientId}.");
                }
            }
            finally
            {
                _pendingControllers.Remove(conn.ClientId);
                conn.OnLoadedStartScenes -= OnOwnerLoadedStartScenes; // tek seferlik
            }
        }
    }
}
