using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private float gameDurationInSeconds = 900f; // 15 dakika
    private readonly SyncVar<float> remainingTime = new SyncVar<float>();

    [SerializeField] private TextMeshProUGUI timerText; // UI'den bağlanmalı

    private bool timerRunning = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
        remainingTime.Value = gameDurationInSeconds;
        timerRunning = true;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        remainingTime.OnChange += OnTimeChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        remainingTime.OnChange -= OnTimeChanged;
    }

    private void Update()
    {
        if (!IsServerInitialized || !timerRunning) return;

        remainingTime.Value -= Time.deltaTime;

        if (remainingTime.Value <= 0f)
        {
            remainingTime.Value = 0f;
            timerRunning = false;
            EndGame();
        }
    }

    private void OnTimeChanged(float oldValue, float newValue, bool asServer)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(newValue / 60f);
            int seconds = Mathf.FloorToInt(newValue % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    [Server]
    private void EndGame()
    {
        Debug.Log("Süre bitti, oyun bitiyor...");

        // Sahne geçişi (Unity > Build Settings içinde bu sahne eklenmiş olmalı)
         // ← buraya kendi sonuç sahne adını yaz
    }
}
