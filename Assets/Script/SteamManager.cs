using UnityEngine;
using Steamworks;

public class SteamManager : MonoBehaviour
{
    private void Awake()
    {
        if (!SteamAPI.Init())
        {
            Debug.LogError("SteamAPI init failed!");
            Application.Quit();
        }
        else
        {
            Debug.Log("SteamAPI init success!");
        }
    }

    private void Update()
    {
        SteamAPI.RunCallbacks();
    }

    private void OnApplicationQuit()
    {
        SteamAPI.Shutdown();
    }
}
