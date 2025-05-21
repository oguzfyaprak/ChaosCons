using UnityEngine;


public class MapBuilder : MonoBehaviour
{
    [Header("Harita Ayarlar�")]
    public int mapSize = 100;

    [Header("Prefablar")]
    public GameObject binaPrefab;
    public GameObject depoPrefab;

    [Header("Zemin")]
    public GameObject zeminPrefab;

    private Vector3[] binaPozisyonlari;

    void Start()
    {
        if (!Application.isPlaying) return;
        OlusturHarita();
    }

    void OlusturHarita()
    {
        OlusturZemin();
        OlusturBinaAlanlari();
        OlusturDepo();
    }

    void OlusturZemin()
    {
        if (zeminPrefab == null)
        {
            Debug.LogWarning("Zemin prefab� atanmad�!");
            return;
        }

       

        GameObject zemin = Instantiate(zeminPrefab, Vector3.zero, Quaternion.identity, this.transform);

        // 1 birim = 1 metre olacak �ekilde ayarl�yorsan bu scale uygundur
        zemin.transform.localScale = new Vector3(mapSize / 10f, 1, mapSize / 10f);
        zemin.transform.position = new Vector3(mapSize / 2f, 0, mapSize / 2f); // Ortalanm�� �ekilde b�rak�yoruz ama scale tam mapSize olacak
        zemin.name = "MapGround";
    }


    void OlusturBinaAlanlari()
    {
        float y = 0.5f;
        float m = mapSize;

        binaPozisyonlari = new Vector3[]
        {
            // 4 k��e
            new(0, y, 0),             // Sol alt k��e
            new(0, y, m),             // Sol �st k��e
            new(m, y, 0),             // Sa� alt k��e
            new(m, y, m),             // Sa� �st k��e

            // 4 kenar ortas� (kenar�n ortas�nda ama en u�ta olacak �ekilde)
            new(m / 2f, y, 0),        // Alt kenar ortas�
            new(m / 2f, y, m),        // �st kenar ortas�
            new(0, y, m / 2f),        // Sol kenar ortas�
            new(m, y, m / 2f)         // Sa� kenar ortas�
        };

        for (int i = 0; i < binaPozisyonlari.Length; i++)
        {
            GameObject bina = Instantiate(binaPrefab, binaPozisyonlari[i], Quaternion.identity, this.transform);
            bina.name = $"Bina_{i + 1}";
        }
    }

    void OlusturDepo()
    {
        Vector3 merkezPozisyon = new(mapSize / 2f, 0.5f, mapSize / 2f);
        Instantiate(depoPrefab, merkezPozisyon, Quaternion.identity, this.transform).name = "Depo_Merkez";
    }
}
