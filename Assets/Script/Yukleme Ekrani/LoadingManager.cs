using UnityEngine;
using UnityEngine.SceneManagement; // Sahne y�netimi i�in gerekli

public class LoadingManager : MonoBehaviour
{
    // Y�klenecek sahnenin ad�n� buraya yazaca��z (�rn: "MainMenu")
    public string sceneToLoad = "MainMenu";

    // Bir s�re sonra men�ye ge�mek i�in
    public float delayTime = 3f; // 3 saniye sonra men�ye ge�

    void Start()
    {
        // Belirli bir gecikmeden sonra SahneY�kle fonksiyonunu �a��r
        Invoke("LoadNextScene", delayTime);
    }

    void LoadNextScene()
    {
        // Belirtilen sahneyi asenkron olarak y�kle
        SceneManager.LoadScene(sceneToLoad);
    }
}