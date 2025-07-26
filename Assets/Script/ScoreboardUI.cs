using UnityEngine;
using TMPro;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq; // OrderByDescending için
using FishNet.Connection;
using Game.Player; // PlayerRegistry için
using Game.Score; // ScoreManager ve PlayerScoreEntry için gerekli
using FishNet.Object.Synchronizing; // SyncListOperation için bu using direktifi önemli!

public class ScoreboardUI : NetworkBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerEntryPrefab;

    private Dictionary<int, TextMeshProUGUI> _playerEntries = new Dictionary<int, TextMeshProUGUI>();
    private Canvas _mainCanvas; // Canvas bileþenini tutmak için

    private void Awake()
    {
        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(false);
        }
        _mainCanvas = GetComponent<Canvas>(); // Bu objenin Canvas bileþenini al
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ScoresChanged += ScoreManager_ScoresChanged;
            Debug.Log("[ScoreboardUI] ScoreManager.ScoresChanged event'ine abone olundu.");

            // YENÝ EKLENDÝ: UI'ý baþlangýçta bir kez güncelleyin
            UpdateScoreboardUI();
        }
        else
        {
            Debug.LogError("[ScoreboardUI] ScoreManager.Instance bulunamadý! Lütfen ScoreManager'ýn sahnede olduðundan emin olun.");
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
            Debug.Log("[ScoreboardUI] ScoreManager.ScoresChanged event'inden abonelik kaldýrýldý.");
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
                Debug.LogWarning("[ScoreboardUI] Kendi PlayerController'ýnýz bulunamadý veya sahibi deðil.");
            }
        }
    }

    private void SetupCanvasRenderCamera()
    {
        if (_mainCanvas == null)
        {
            Debug.LogError("[ScoreboardUI] Canvas bileþeni bulunamadý! ScoreboardUI scripti bir Canvas objesi üzerinde olmalý.");
            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("[ScoreboardUI] 'MainCamera' tag'ine sahip kamera bulunamadý. Sahnedeki diðer kameralarý arýyorum.");
            mainCamera = GameObject.FindAnyObjectByType<Camera>();
        }

        if (mainCamera != null)
        {
            _mainCanvas.worldCamera = mainCamera;
            Debug.Log($"[ScoreboardUI] Canvas Render Camera olarak '{mainCamera.name}' atandý.");
        }
        else
        {
            Debug.LogError("[ScoreboardUI] Sahneden geçerli bir kamera bulunamadý! UI düzgün görüntülenmeyebilir.");
        }
    }

    private void ScoreManager_ScoresChanged(SyncListOperation op, int index, PlayerScoreEntry oldEntry, PlayerScoreEntry newEntry, bool asServer)
    {
        UpdateScoreboardUI();
        Debug.Log($"[ScoreboardUI] ScoresChanged event alýndý: {op}, Index: {index}, Old: {oldEntry.PlayerID}, New: {newEntry.PlayerID}");
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
        Debug.Log("[ScoreboardUI] Scoreboard UI güncellendi.");
    }
}