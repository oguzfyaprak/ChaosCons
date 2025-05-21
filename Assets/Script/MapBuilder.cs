using UnityEngine;


public class MapBuilder : MonoBehaviour
{
    [Header("Harita Ayarlarý")]
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
            Debug.LogWarning("Zemin prefabý atanmadý!");
            return;
        }

       

        GameObject zemin = Instantiate(zeminPrefab, Vector3.zero, Quaternion.identity, this.transform);

        // 1 birim = 1 metre olacak þekilde ayarlýyorsan bu scale uygundur
        zemin.transform.localScale = new Vector3(mapSize / 10f, 1, mapSize / 10f);
        zemin.transform.position = new Vector3(mapSize / 2f, 0, mapSize / 2f); // Ortalanmýþ þekilde býrakýyoruz ama scale tam mapSize olacak
        zemin.name = "MapGround";
    }


    void OlusturBinaAlanlari()
    {
        float y = 0.5f;
        float m = mapSize;

        binaPozisyonlari = new Vector3[]
        {
            // 4 köþe
            new(0, y, 0),             // Sol alt köþe
            new(0, y, m),             // Sol üst köþe
            new(m, y, 0),             // Sað alt köþe
            new(m, y, m),             // Sað üst köþe

            // 4 kenar ortasý (kenarýn ortasýnda ama en uçta olacak þekilde)
            new(m / 2f, y, 0),        // Alt kenar ortasý
            new(m / 2f, y, m),        // Üst kenar ortasý
            new(0, y, m / 2f),        // Sol kenar ortasý
            new(m, y, m / 2f)         // Sað kenar ortasý
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
