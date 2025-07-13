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
                Debug.LogError("SteamAPI baþlatýlamadý!");
                return;
            }
            Debug.Log("Steam baþlatýldý: " + SteamFriends.GetPersonaName());
        }
        catch (System.Exception e)
        {
            Debug.LogError("Steam hatasý: " + e.Message);
        }
    }

    private void Update() => SteamAPI.RunCallbacks();

    private void OnApplicationQuit() => SteamAPI.Shutdown();
}
