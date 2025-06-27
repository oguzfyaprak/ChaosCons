using System.Collections.Generic;
using FishNet.Connection;

public static class PlayerRegistry
{
    private static readonly Dictionary<int, NetworkConnection> playerIDToConnection = new();
    private static readonly Dictionary<NetworkConnection, int> connectionToPlayerID = new();

    public static void Register(int playerID, NetworkConnection conn)
    {
        if (!playerIDToConnection.ContainsKey(playerID))
            playerIDToConnection.Add(playerID, conn);

        if (!connectionToPlayerID.ContainsKey(conn))
            connectionToPlayerID.Add(conn, playerID);
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

    public static void Clear()
    {
        playerIDToConnection.Clear();
        connectionToPlayerID.Clear();
    }
}
