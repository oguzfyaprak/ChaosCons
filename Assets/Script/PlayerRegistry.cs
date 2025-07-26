using System.Collections.Generic;
using FishNet.Connection;
using UnityEngine; // Debug.Log için

namespace Game.Player
{
    // PlayerRegistry statik bir sýnýf olarak kalacak
    public static class PlayerRegistry
    {
        private static readonly Dictionary<int, NetworkConnection> playerIDToConnection = new();
        private static readonly Dictionary<NetworkConnection, int> connectionToPlayerID = new();
        private static readonly Dictionary<int, string> playerIDToName = new(); // Oyuncu ID'si ve adýný tutmak için

        // Statik event'ler
        public static event System.Action<int, NetworkConnection> OnPlayerRegistered;
        public static event System.Action<NetworkConnection> OnPlayerDeregistered;

        public static void Initialize()
        {
            // Ýhtiyaç olursa burada bir defalýk baþlatma iþlemleri yapýlabilir.
            // Genellikle bu tür statik sýnýflar doðrudan metot çaðrýlarýyla kullanýlýr.
            Clear(); // Baþlangýçta temizlik yapmak iyi bir pratiktir.
            Debug.Log("[PlayerRegistry] Statik PlayerRegistry baþlatýldý/temizlendi.");
        }

        // Kayýt metodunu public tutalým
        public static void Register(int playerID, NetworkConnection conn, string playerName)
        {
            // Zaten kayýtlý mý kontrol et
            if (playerIDToConnection.ContainsKey(playerID))
            {
                Debug.LogWarning($"[PlayerRegistry] PlayerID {playerID} zaten kayýtlý. Tekrar kayýt yapýlmadý.");
                return;
            }
            if (connectionToPlayerID.ContainsKey(conn))
            {
                Debug.LogWarning($"[PlayerRegistry] Connection {conn.ClientId} zaten kayýtlý. Tekrar kayýt yapýlmadý.");
                return;
            }

            playerIDToConnection.Add(playerID, conn);
            connectionToPlayerID.Add(conn, playerID);
            playerIDToName.Add(playerID, playerName); // Oyuncu adýný da kaydet

            Debug.Log($"[PlayerRegistry] Registered PlayerID: {playerID}, Name: {playerName}, Connection: {conn.ClientId}");

            // Event'i tetikle
            OnPlayerRegistered?.Invoke(playerID, conn);
        }

        // Kayýt silme metodunu public tutalým
        public static void Deregister(NetworkConnection conn)
        {
            if (connectionToPlayerID.TryGetValue(conn, out int playerID))
            {
                playerIDToConnection.Remove(playerID);
                connectionToPlayerID.Remove(conn);
                playerIDToName.Remove(playerID); // Oyuncu adýný da sil

                Debug.Log($"[PlayerRegistry] Deregistered PlayerID: {playerID}, Connection: {conn.ClientId}");
                OnPlayerDeregistered?.Invoke(conn); // Event'i tetikle
            }
            else
            {
                Debug.LogWarning($"[PlayerRegistry] Connection {conn.ClientId} kayýtlý deðil, silinemedi.");
            }
        }

        public static NetworkConnection GetConnectionByPlayerID(int playerID)
        {
            playerIDToConnection.TryGetValue(playerID, out var conn);
            return conn;
        }

        public static int GetPlayerIDByConnection(NetworkConnection conn)
        {
            connectionToPlayerID.TryGetValue(conn, out var id);
            return id;
        }

        // YENÝ METOT: Oyuncu adýný ID'ye göre almak için
        public static string GetPlayerName(int playerID)
        {
            playerIDToName.TryGetValue(playerID, out var name);
            return name ?? "Bilinmeyen Oyuncu"; // Ad bulunamazsa varsayýlan deðer döndür
        }

        // YENÝ METOT: Tüm kayýtlarý temizlemek için (örneðin sahne deðiþtiðinde veya sunucu kapandýðýnda)
        public static void Clear()
        {
            playerIDToConnection.Clear();
            connectionToPlayerID.Clear();
            playerIDToName.Clear();
            Debug.Log("[PlayerRegistry] All registrations cleared.");
        }
    }
}