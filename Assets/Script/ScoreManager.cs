using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using Game.Player; // PlayerRegistry'nin namespace'i

namespace Game.Score // Bu satır çok önemli!
{
    public struct PlayerScoreEntry : System.IEquatable<PlayerScoreEntry>
    {
        public int PlayerID;
        public string PlayerName;
        public int Kills;
        public int Deaths;
        public int Sabotages;
        public int Deliveries;
        public int ItemsPickedUp;
        public int TotalScore;

        public PlayerScoreEntry(int playerID, string playerName)
        {
            PlayerID = playerID;
            PlayerName = playerName;
            Kills = 0;
            Deaths = 0;
            Sabotages = 0;
            Deliveries = 0;
            ItemsPickedUp = 0;
            TotalScore = 0;
        }

        public bool Equals(PlayerScoreEntry other)
        {
            return PlayerID == other.PlayerID;
        }

        public override int GetHashCode()
        {
            return PlayerID.GetHashCode();
        }
    }

    public class ScoreManager : NetworkBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        private readonly SyncList<PlayerScoreEntry> _playerScores = new SyncList<PlayerScoreEntry>();

        [Header("Score Multipliers")]
        [SerializeField] private int killScore = 10;
        [SerializeField] private int deathPenalty = -5;
        [SerializeField] private int sabotageScore = 15;
        [SerializeField] private int deliveryScore = 20;
        [SerializeField] private int itemPickupScore = 1;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject); // Bu satırın YORUM SATIRI olduğundan emin olun!
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // YENİ EKLENDİ: Sunucu başladığında PlayerRegistry'i temizle
            PlayerRegistry.Initialize();

            // PlayerRegistry artık statik bir sınıf olduğu için Instance kullanmaya gerek yok.
            // Doğrudan sınıf adı üzerinden event'lere abone ol.
            PlayerRegistry.OnPlayerRegistered += PlayerRegistry_OnPlayerRegistered;
            PlayerRegistry.OnPlayerDeregistered += PlayerRegistry_OnPlayerDeregistered;

            // Diğer sistemlerle entegrasyon için event abonelikleri buraya gelecek:
            // HealthSystem.OnPlayerDied += OnPlayerDied; // HealthSystem'de ScoreManager.Instance.AddDeath çağrıldığı için buraya gerek kalmadı
            // PlayerItemHandler.OnItemPickedUp += OnItemPickedUp; 
            // SabotageSystem.OnSabotageCompleted += OnSabotageCompleted; 
            // DeliveryZone.OnDeliveryCompleted += OnDeliveryCompleted; 
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            // Sunucu durduğunda olay dinleyicilerini bırak
            PlayerRegistry.OnPlayerRegistered -= PlayerRegistry_OnPlayerRegistered;
            PlayerRegistry.OnPlayerDeregistered -= PlayerRegistry_OnPlayerDeregistered;

            // Diğer sistemlerle entegrasyon için event abonelikleri buraya gelecek:
            // HealthSystem.OnPlayerDied -= OnPlayerDied; 
            // PlayerItemHandler.OnItemPickedUp -= OnItemPickedUp; 
            // SabotageSystem.OnSabotageCompleted -= OnSabotageCompleted; 
            // DeliveryZone.OnDeliveryCompleted -= OnDeliveryCompleted; 
        }

        // --- Oyuncu Kayıt Olayları ---
        private void PlayerRegistry_OnPlayerRegistered(int playerID, NetworkConnection conn)
        {
            if (!IsServerInitialized) return;

            // YENİ EKLENDİ: Oyuncu zaten listede mi kontrol et
            if (_playerScores.Any(e => e.PlayerID == playerID))
            {
                Debug.LogWarning($"[SERVER] Player ID {playerID} zaten skor listesinde. Tekrar eklenmedi.");
                return;
            }

            string playerName = PlayerRegistry.GetPlayerName(playerID);
            PlayerScoreEntry newEntry = new PlayerScoreEntry(playerID, playerName);
            _playerScores.Add(newEntry);
            Debug.Log($"[SERVER] Registered new player for scores: ID {playerID}, Name: {newEntry.PlayerName}");
        }

        private void PlayerRegistry_OnPlayerDeregistered(NetworkConnection conn)
        {
            if (!IsServerInitialized) return;

            int deregisteringPlayerID = PlayerRegistry.GetPlayerIDByConnection(conn);
            if (deregisteringPlayerID != -1)
            {
                int index = _playerScores.FindIndex(e => e.PlayerID == deregisteringPlayerID);
                if (index != -1)
                {
                    _playerScores.RemoveAt(index);
                    Debug.Log($"[SERVER] Deregistered player scores: ID {deregisteringPlayerID}");
                }
            }
        }

        // --- Skor Güncelleme Metotları (ServerRpc ile çağrılacak) ---

        [ServerRpc(RequireOwnership = false)]
        public void AddKill(int killerPlayerID)
        {
            if (!IsServerInitialized) return;

            int index = _playerScores.FindIndex(e => e.PlayerID == killerPlayerID);
            if (index != -1)
            {
                PlayerScoreEntry entry = _playerScores[index];
                entry.Kills++;
                entry.TotalScore += killScore;
                _playerScores[index] = entry;
                Debug.Log($"[SERVER] Player {entry.PlayerName} (ID: {entry.PlayerID}) got a kill. New Total Score: {entry.TotalScore}");
            }
            else
            {
                Debug.LogWarning($"[SERVER] Killer PlayerID {killerPlayerID} not found in scores.");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddDeath(int victimPlayerID)
        {
            if (!IsServerInitialized) return;

            int index = _playerScores.FindIndex(e => e.PlayerID == victimPlayerID);
            if (index != -1)
            {
                PlayerScoreEntry entry = _playerScores[index];
                entry.Deaths++;
                entry.TotalScore += deathPenalty;
                _playerScores[index] = entry;
                Debug.Log($"[SERVER] Player {entry.PlayerName} (ID: {entry.PlayerID}) died. New Total Score: {entry.TotalScore}");
            }
            else
            {
                Debug.LogWarning($"[SERVER] Victim PlayerID {victimPlayerID} not found in scores.");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddSabotage(int sabotagerPlayerID)
        {
            if (!IsServerInitialized) return;

            int index = _playerScores.FindIndex(e => e.PlayerID == sabotagerPlayerID);
            if (index != -1)
            {
                PlayerScoreEntry entry = _playerScores[index];
                entry.Sabotages++;
                entry.TotalScore += sabotageScore;
                _playerScores[index] = entry;
                Debug.Log($"[SERVER] Player {entry.PlayerName} (ID: {entry.PlayerID}) completed a sabotage. New Total Score: {entry.TotalScore}");
            }
            else
            {
                Debug.LogWarning($"[SERVER] Sabotager PlayerID {sabotagerPlayerID} not found in scores.");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddDelivery(int delivererPlayerID)
        {
            if (!IsServerInitialized) return;

            int index = _playerScores.FindIndex(e => e.PlayerID == delivererPlayerID);
            if (index != -1)
            {
                PlayerScoreEntry entry = _playerScores[index];
                entry.Deliveries++;
                entry.TotalScore += deliveryScore;
                _playerScores[index] = entry;
                Debug.Log($"[SERVER] Player {entry.PlayerName} (ID: {entry.PlayerID}) completed a delivery. New Total Score: {entry.TotalScore}");
            }
            else
            {
                Debug.LogWarning($"[SERVER] Deliverer PlayerID {delivererPlayerID} not found in scores.");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddItemPickup(int pickerUpperPlayerID)
        {
            if (!IsServerInitialized) return;

            int index = _playerScores.FindIndex(e => e.PlayerID == pickerUpperPlayerID);
            if (index != -1)
            {
                PlayerScoreEntry entry = _playerScores[index];
                entry.ItemsPickedUp++;
                entry.TotalScore += itemPickupScore;
                _playerScores[index] = entry;
                Debug.Log($"[SERVER] Player {entry.PlayerName} (ID: {entry.PlayerID}) picked up an item. New Total Score: {entry.TotalScore}");
            }
            else
            {
                Debug.LogWarning($"[SERVER] Item picker-upper PlayerID {pickerUpperPlayerID} not found in scores.");
            }
        }

        // --- Skor Bilgilerini Alma Metotları ---

        public List<PlayerScoreEntry> GetScoresOrderedByTotalScore()
        {
            return _playerScores.OrderByDescending(e => e.TotalScore).ToList();
        }

        public PlayerScoreEntry? GetPlayerScore(int playerID)
        {
            PlayerScoreEntry? score = _playerScores.FirstOrDefault(e => e.PlayerID == playerID);
            if (score.HasValue && score.Value.PlayerID == playerID)
            {
                return score.Value;
            }
            return null;
        }

        public event SyncList<PlayerScoreEntry>.SyncListChanged ScoresChanged
        {
            add { _playerScores.OnChange += value; }
            remove { _playerScores.OnChange -= value; }
        }
    }
}