using UnityEngine;
using Steamworks;

public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            if (!SteamAPI.Init())
            {
                Debug.LogError("SteamAPI ba�lat�lamad�!");
                return;
            }
            Debug.Log("Steam ba�lat�ld�: " + SteamFriends.GetPersonaName());
        }
        catch (System.Exception e)
        {
            Debug.LogError("Steam hatas�: " + e.Message);
        }
    }

    private void Update() => SteamAPI.RunCallbacks();

    private void OnApplicationQuit() => SteamAPI.Shutdown();
}
