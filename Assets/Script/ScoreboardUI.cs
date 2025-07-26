using UnityEngine;
using TMPro;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq; // OrderByDescending i�in
using FishNet.Connection;
using Game.Player; // PlayerRegistry i�in
using Game.Score; // ScoreManager ve PlayerScoreEntry i�in gerekli
using FishNet.Object.Synchronizing; // SyncListOperation i�in bu using direktifi �nemli!

public class ScoreboardUI : NetworkBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerEntryPrefab;

    private Dictionary<int, TextMeshProUGUI> _playerEntries = new Dictionary<int, TextMeshProUGUI>();
    private Canvas _mainCanvas; // Canvas bile�enini tutmak i�in

    private void Awake()
    {
        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(false);
        }
        _mainCanvas = GetComponent<Canvas>(); // Bu objenin Canvas bile�enini al
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ScoresChanged += ScoreManager_ScoresChanged;
            Debug.Log("[ScoreboardUI] ScoreManager.ScoresChanged event'ine abone olundu.");

            // YEN� EKLEND�: UI'� ba�lang��ta bir kez g�ncelleyin
            UpdateScoreboardUI();
        }
        else
        {
            Debug.LogError("[ScoreboardUI] ScoreManager.Instance bulunamad�! L�tfen ScoreManager'�n sahnede oldu�undan emin olun.");
        }

        if (IsOwner)
        {
            SetupCanvasRenderCamera();
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ScoresChanged -= ScoreManager_ScoresChanged;
            Debug.Log("[ScoreboardUI] ScoreManager.ScoresChanged event'inden abonelik kald�r�ld�.");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleScoreboard();
        }
    }

    private void ToggleScoreboard()
    {
        if (scoreboardPanel == null) return;

        bool isActive = !scoreboardPanel.activeSelf;
        scoreboardPanel.SetActive(isActive);

        if (IsOwner)
        {
            Cursor.lockState = isActive ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isActive;

            PlayerController playerController = GameObject.FindAnyObjectByType<PlayerController>();
            if (playerController != null && playerController.IsOwner)
            {
                playerController.SetMovementEnabled(!isActive);
                Debug.Log($"[ScoreboardUI] Player movement {(isActive ? "disabled" : "enabled")}.");
            }
            else
            {
                Debug.LogWarning("[ScoreboardUI] Kendi PlayerController'�n�z bulunamad� veya sahibi de�il.");
            }
        }
    }

    private void SetupCanvasRenderCamera()
    {
        if (_mainCanvas == null)
        {
            Debug.LogError("[ScoreboardUI] Canvas bile�eni bulunamad�! ScoreboardUI scripti bir Canvas objesi �zerinde olmal�.");
            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("[ScoreboardUI] 'MainCamera' tag'ine sahip kamera bulunamad�. Sahnedeki di�er kameralar� ar�yorum.");
            mainCamera = GameObject.FindAnyObjectByType<Camera>();
        }

        if (mainCamera != null)
        {
            _mainCanvas.worldCamera = mainCamera;
            Debug.Log($"[ScoreboardUI] Canvas Render Camera olarak '{mainCamera.name}' atand�.");
        }
        else
        {
            Debug.LogError("[ScoreboardUI] Sahneden ge�erli bir kamera bulunamad�! UI d�zg�n g�r�nt�lenmeyebilir.");
        }
    }

    private void ScoreManager_ScoresChanged(SyncListOperation op, int index, PlayerScoreEntry oldEntry, PlayerScoreEntry newEntry, bool asServer)
    {
        UpdateScoreboardUI();
        Debug.Log($"[ScoreboardUI] ScoresChanged event al�nd�: {op}, Index: {index}, Old: {oldEntry.PlayerID}, New: {newEntry.PlayerID}");
    }

    private void UpdateScoreboardUI()
    {
        if (playerListContent == null || playerEntryPrefab == null || ScoreManager.Instance == null) return;

        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }
        _playerEntries.Clear();

        List<PlayerScoreEntry> currentScores = ScoreManager.Instance.GetScoresOrderedByTotalScore();

        foreach (var entry in currentScores)
        {
            GameObject entryObj = Instantiate(playerEntryPrefab, playerListContent);
            TextMeshProUGUI entryText = entryObj.GetComponent<TextMeshProUGUI>();

            if (entryText != null)
            {
                string playerName = PlayerRegistry.GetPlayerName(entry.PlayerID);
                if (string.IsNullOrEmpty(playerName) || playerName == "Bilinmeyen Oyuncu")
                {
                    playerName = entry.PlayerName;
                }

                entryText.text = $"ID: {entry.PlayerID} - {playerName} - K:{entry.Kills} D:{entry.Deaths} S:{entry.Sabotages} Del:{entry.Deliveries} P:{entry.ItemsPickedUp} - Skor: {entry.TotalScore}";
                _playerEntries.Add(entry.PlayerID, entryText);
            }
        }
        Debug.Log("[ScoreboardUI] Scoreboard UI g�ncellendi.");
    }
}