using UnityEngine;
using UnityEngine.SceneManagement; // Sahne yönetimi için gerekli

public class LoadingManager : MonoBehaviour
{
    // Yüklenecek sahnenin adýný buraya yazacaðýz (Örn: "MainMenu")
    public string sceneToLoad = "MainMenu";

    // Bir süre sonra menüye geçmek için
    public float delayTime = 3f; // 3 saniye sonra menüye geç

    void Start()
    {
        // Belirli bir gecikmeden sonra SahneYükle fonksiyonunu çaðýr
        Invoke("LoadNextScene", delayTime);
    }

    void LoadNextScene()
    {
        // Belirtilen sahneyi asenkron olarak yükle
        SceneManager.LoadScene(sceneToLoad);
    }
}