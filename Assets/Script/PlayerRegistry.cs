using System.Collections.Generic;
using FishNet.Connection;
using UnityEngine; // Debug.Log i�in

namespace Game.Player
{
    // PlayerRegistry statik bir s�n�f olarak kalacak
    public static class PlayerRegistry
    {
        private static readonly Dictionary<int, NetworkConnection> playerIDToConnection = new();
        private static readonly Dictionary<NetworkConnection, int> connectionToPlayerID = new();
        private static readonly Dictionary<int, string> playerIDToName = new(); // Oyuncu ID'si ve ad�n� tutmak i�in

        // Statik event'ler
        public static event System.Action<int, NetworkConnection> OnPlayerRegistered;
        public static event System.Action<NetworkConnection> OnPlayerDeregistered;

        public static void Initialize()
        {
            // �htiya� olursa burada bir defal�k ba�latma i�lemleri yap�labilir.
            // Genellikle bu t�r statik s�n�flar do�rudan metot �a�r�lar�yla kullan�l�r.
            Clear(); // Ba�lang��ta temizlik yapmak iyi bir pratiktir.
            Debug.Log("[PlayerRegistry] Statik PlayerRegistry ba�lat�ld�/temizlendi.");
        }

        // Kay�t metodunu public tutal�m
        public static void Register(int playerID, NetworkConnection conn, string playerName)
        {
            // Zaten kay�tl� m� kontrol et
            if (playerIDToConnection.ContainsKey(playerID))
            {
                Debug.LogWarning($"[PlayerRegistry] PlayerID {playerID} zaten kay�tl�. Tekrar kay�t yap�lmad�.");
                return;
            }
            if (connectionToPlayerID.ContainsKey(conn))
            {
                Debug.LogWarning($"[PlayerRegistry] Connection {conn.ClientId} zaten kay�tl�. Tekrar kay�t yap�lmad�.");
                return;
            }

            playerIDToConnection.Add(playerID, conn);
            connectionToPlayerID.Add(conn, playerID);
            playerIDToName.Add(playerID, playerName); // Oyuncu ad�n� da kaydet

            Debug.Log($"[PlayerRegistry] Registered PlayerID: {playerID}, Name: {playerName}, Connection: {conn.ClientId}");

            // Event'i tetikle
            OnPlayerRegistered?.Invoke(playerID, conn);
        }

        // Kay�t silme metodunu public tutal�m
        public static void Deregister(NetworkConnection conn)
        {
            if (connectionToPlayerID.TryGetValue(conn, out int playerID))
            {
                playerIDToConnection.Remove(playerID);
                connectionToPlayerID.Remove(conn);
                playerIDToName.Remove(playerID); // Oyuncu ad�n� da sil

                Debug.Log($"[PlayerRegistry] Deregistered PlayerID: {playerID}, Connection: {conn.ClientId}");
                OnPlayerDeregistered?.Invoke(conn); // Event'i tetikle
            }
            else
            {
                Debug.LogWarning($"[PlayerRegistry] Connection {conn.ClientId} kay�tl� de�il, silinemedi.");
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

        // YEN� METOT: Oyuncu ad�n� ID'ye g�re almak i�in
        public static string GetPlayerName(int playerID)
        {
            playerIDToName.TryGetValue(playerID, out var name);
            return name ?? "Bilinmeyen Oyuncu"; // Ad bulunamazsa varsay�lan de�er d�nd�r
        }

        // YEN� METOT: T�m kay�tlar� temizlemek i�in (�rne�in sahne de�i�ti�inde veya sunucu kapand���nda)
        public static void Clear()
        {
            playerIDToConnection.Clear();
            connectionToPlayerID.Clear();
            playerIDToName.Clear();
            Debug.Log("[PlayerRegistry] All registrations cleared.");
        }
    }
}