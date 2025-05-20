using UnityEngine;

public class AdvancedMapBuilder : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject tilePrefab;
    public GameObject playerBasePrefab;
    public GameObject teleportPadPrefab;
    public GameObject centralDepotPrefab;
    public GameObject obstaclePrefab; // Yeni: Engel objeleri

    [Header("Map Settings")]
    public int mapSize = 20; // 15'ten 20'ye büyütüldü
    public int centralDepotSize = 2; // 3x3 yerine 2x2

    void Start()
    {
        BuildGrid();
        PlacePlayerBases();
        PlaceTeleportPads();
        PlaceCentralDepot();
        AddRandomObstacles(); // Yeni: Rastgele engeller
    }

    void BuildGrid()
    {
        for (int x = 0; x < mapSize; x++)
        {
            for (int y = 0; y < mapSize; y++)
            {
                Vector3 pos = new Vector3(x, 0f, y);
                Instantiate(tilePrefab, pos, Quaternion.identity, transform);
            }
        }
    }

    void PlacePlayerBases()
    {
        Vector2Int[] baseCoords = {
            new Vector2Int(2, 2), new Vector2Int(mapSize/2, 2), new Vector2Int(mapSize-3, 2),
            new Vector2Int(2, mapSize/2), new Vector2Int(2, mapSize-3),
            new Vector2Int(mapSize/2, mapSize-3), new Vector2Int(mapSize-3, mapSize-3),
            new Vector2Int(mapSize-3, mapSize/2)
        };

        foreach (var coord in baseCoords)
        {
            Vector3 pos = new Vector3(coord.x, 0.01f, coord.y);
            Instantiate(playerBasePrefab, pos, Quaternion.identity, transform);
        }
    }

    void PlaceTeleportPads()
    {
        Vector2Int[] teleportCoords = {
            new Vector2Int(5, mapSize/2), new Vector2Int(mapSize-6, mapSize/2),
            new Vector2Int(mapSize/2, 5), new Vector2Int(mapSize/2, mapSize-6)
        };

        foreach (var coord in teleportCoords)
        {
            Vector3 pos = new Vector3(coord.x, 0.01f, coord.y);
            Instantiate(teleportPadPrefab, pos, Quaternion.identity, transform);
        }
    }

    void PlaceCentralDepot()
    {
        int center = mapSize / 2;
        for (int x = center - centralDepotSize / 2; x <= center + centralDepotSize / 2; x++)
        {
            for (int y = center - centralDepotSize / 2; y <= center + centralDepotSize / 2; y++)
            {
                Vector3 pos = new Vector3(x, 0.01f, y);
                Instantiate(centralDepotPrefab, pos, Quaternion.identity, transform);
            }
        }
    }

    void AddRandomObstacles()
    {
        int obstacleCount = mapSize * 2; // Harita boyutuna göre engel sayýsý
        for (int i = 0; i < obstacleCount; i++)
        {
            int x = Random.Range(3, mapSize - 3);
            int y = Random.Range(3, mapSize - 3);

            // Merkez ve oyuncu üslerine çok yakýn olmamasýný kontrol et
            if (!IsNearPlayerBase(x, y) && !IsInCentralArea(x, y))
            {
                Vector3 pos = new Vector3(x, 0.02f, y);
                Instantiate(obstaclePrefab, pos, Quaternion.identity, transform);
            }
        }
    }

    bool IsNearPlayerBase(int x, int y)
    {
        Vector2Int[] baseCoords = {
            new Vector2Int(2, 2), new Vector2Int(mapSize/2, 2), new Vector2Int(mapSize-3, 2),
            new Vector2Int(2, mapSize/2), new Vector2Int(2, mapSize-3),
            new Vector2Int(mapSize/2, mapSize-3), new Vector2Int(mapSize-3, mapSize-3),
            new Vector2Int(mapSize-3, mapSize/2)
        };

        foreach (var coord in baseCoords)
        {
            if (Vector2Int.Distance(new Vector2Int(x, y), coord) < 4)
                return true;
        }
        return false;
    }

    bool IsInCentralArea(int x, int y)
    {
        int center = mapSize / 2;
        int areaSize = centralDepotSize + 4; // Merkez bölgesine yakýnlýk kontrolü
        return Mathf.Abs(x - center) <= areaSize / 2 && Mathf.Abs(y - center) <= areaSize / 2;
    }
}