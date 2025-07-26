using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using FishNet.Component.Transforming;
using FishNet.Connection;
using Game.Player;
using Game.Score; // ScoreManager için gerekli

public class HealthSystem : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float invincibilityDuration = 1.5f;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip deathSound;

    private readonly SyncVar<int> _currentHealth = new();
    private readonly SyncVar<bool> _isDead = new();
    private readonly SyncVar<Vector3> _initialSpawnPosition = new();

    private bool _isInvincible;
    private AudioSource _audioSource;
    private PlayerController _playerController;
    private NetworkTransform _networkTransform; // NetworkTransform referansı
    private CharacterController _characterController; // CharacterController referansı

    public delegate void HealthChangedDelegate(int newHealth, int maxHealth);
    public event HealthChangedDelegate OnHealthChangedEvent;

    public delegate void DeathDelegate(int attackerId);
    public event DeathDelegate OnDeathEvent; // Ölüm olayını dışarıya bildirmek için

    public bool IsDead => _isDead.Value;

    private void Awake()
    {
        _currentHealth.OnChange += OnHealthChanged;
        _isDead.OnChange += OnIsDeadChanged;
    }

    private void OnDestroy()
    {
        _currentHealth.OnChange -= OnHealthChanged;
        _isDead.OnChange -= OnIsDeadChanged;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _playerController = GetComponent<PlayerController>();
        if (_playerController == null)
        {
            Debug.LogWarning("PlayerController component not found. Player velocity reset and movement disabling features might not work.");
        }

        _networkTransform = GetComponent<NetworkTransform>();
        if (_networkTransform == null) Debug.LogError("NetworkTransform component not found on player!");
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null) Debug.LogError("CharacterController component not found on player!");

        if (IsServerInitialized)
        {
            _currentHealth.Value = maxHealth;
            _isDead.Value = false;
            // İlk spawn pozisyonunu sadece sunucu başlatıldığında bir kez al
            // Bu, objenin sahneye yerleştirildiği yer veya spawn edildiği yer olabilir
            _initialSpawnPosition.Value = transform.position;
            Debug.Log($"[SERVER] Initialized HealthSystem for {gameObject.name}. Spawn Pos: {_initialSpawnPosition.Value}");
        }
    }

    /// <summary>
    /// Oyuncuya hasar verir. Sadece sunucuda çağrılır.
    /// </summary>
    /// <param name="amount">Hasar miktarı.</param>
    /// <param name="attackerConn">Hasarı veren oyuncunun NetworkConnection'ı. Null olabilir (örn. çevre hasarı).</param>
    [ServerRpc(RequireOwnership = false)] // RequireOwnership=false, çağrıyı yapan objenin sahibi olmak zorunda değil
    public void ServerTakeDamage(int amount, NetworkConnection attackerConn) // RPC olarak tanımlandı
    {
        if (!IsServerInitialized) return; // Sadece sunucuda çalış

        if (_isInvincible || _isDead.Value) return;

        _currentHealth.Value = Mathf.Max(0, _currentHealth.Value - amount);
        Debug.Log($"[SERVER] {gameObject.name} (Owner: {Owner.ClientId}) took {amount} damage from {(attackerConn != null ? attackerConn.ClientId.ToString() : "Environment")}. HP now: {_currentHealth.Value}");

        if (_currentHealth.Value <= 0)
        {
            HandleDeath(attackerConn); // NetworkConnection'ı HandleDeath'e ilet
        }
        else
        {
            StartCoroutine(InvincibilityCoroutine());
            PlayHurtEffectObserversRpc();
        }
    }

    /// <summary>
    /// Oyuncunun ölümünü yönetir. Sadece sunucuda çağrılır.
    /// </summary>
    /// <param name="killerConn">Oyuncuyu öldüren kişinin NetworkConnection'ı. Null olabilir (örn. çevre ölümü).</param>
    [Server] // Bu metot bir RPC değil, Server'daki TakeDamage tarafından çağrılıyor
    private void HandleDeath(NetworkConnection killerConn)
    {
        if (_isDead.Value) return;

        _isDead.Value = true;
        Debug.Log("[SERVER] Player died. Setting _isDead to true.");

        // Kendi ölüm skorunu güncelle (AddDeath)
        if (ScoreManager.Instance != null)
        {
            int playerID = PlayerRegistry.GetPlayerIDByConnection(base.Owner);
            ScoreManager.Instance.AddDeath(playerID);
        }
        else
        {
            Debug.LogWarning("[SERVER] ScoreManager.Instance is null for death update.");
        }

        // Öldürme skorunu güncelle (AddKill)
        if (killerConn != null && killerConn != base.Owner && ScoreManager.Instance != null)
        {
            int killerID = PlayerRegistry.GetPlayerIDByConnection(killerConn);
            ScoreManager.Instance.AddKill(killerID);
        }
        else if (killerConn == null)
        {
            Debug.LogWarning("[SERVER] Player died but killer connection was null (Environment kill?). Kill score not updated.");
            OnDeathEvent?.Invoke(-1); // Çevre ölümü ise -1 ile bildir
        }
        else if (killerConn == base.Owner)
        {
            Debug.LogWarning($"[SERVER] Player (ID: {base.Owner.ClientId}) killed themselves. No kill score awarded.");
            OnDeathEvent?.Invoke(base.Owner.ClientId); // Kendi kendini öldürdüyse kendi ID'sini bildir
        }
        else
        {
            Debug.LogWarning("[SERVER] ScoreManager.Instance is null for kill update.");
        }

        PlayDeathEffectObserversRpc();

        // Ölen oyuncunun istemcisine hareketi durdurma komutu gönder
        TargetSetPlayerMovementRpc(Owner, false);

        // Respawn gecikmesini başlat
        StartCoroutine(RespawnAfterDelay(respawnDelay));
    }

    private IEnumerator InvincibilityCoroutine()
    {
        _isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        _isInvincible = false;
    }

    private IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Respawn();
    }

    /// <summary>
    /// Oyuncuyu yeniden doğurur. Sadece sunucuda çağrılır.
    /// </summary>
    [Server]
    public void Respawn()
    {
        Debug.Log($"[SERVER] Respawning player (ID: {Owner.ClientId}) to initial spawn position: {_initialSpawnPosition.Value}");

        _currentHealth.Value = maxHealth;
        _isInvincible = false;
        _isDead.Value = false;

        // Respawn işlemi için NetworkTransform'u geçici olarak devre dışı bırak
        if (_networkTransform != null) _networkTransform.enabled = false;
        // CharacterController'ı da geçici olarak devre dışı bırak
        if (_characterController != null) _characterController.enabled = false;

        // Sunucuda karakterin pozisyonunu ayarla
        transform.SetPositionAndRotation(_initialSpawnPosition.Value, Quaternion.identity);

        if (_playerController != null)
            _playerController.ResetVelocity();

        // Oyuncunun kendi istemcisine doğru spawn pozisyonunu doğrudan ilet
        TargetTeleportToSpawn(Owner, _initialSpawnPosition.Value);

        // NetworkTransform'ı ve CharacterController'ı biraz sonra etkinleştir
        StartCoroutine(ReEnableMovementComponentsAfterTeleport(0.1f));

        // Oyuncunun kendi istemcisine hareketi etkinleştirme komutu gönder
        TargetSetPlayerMovementRpc(Owner, true);
    }

    /// <summary>
    /// Sunucu tarafında NetworkTransform ve CharacterController'ı yeniden etkinleştirmek için Coroutine.
    /// </summary>
    private IEnumerator ReEnableMovementComponentsAfterTeleport(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (IsServerInitialized)
        {
            if (_networkTransform != null) _networkTransform.enabled = true;
            if (_characterController != null) _characterController.enabled = true;
            Debug.Log("[SERVER] NetworkTransform and CharacterController re-enabled after teleport on server.");
        }
    }

    /// <summary>
    /// Oyuncuyu iyileştirir. Sadece sunucuda çağrılır.
    /// </summary>
    /// <param name="amount">İyileşme miktarı.</param>
    [Server]
    public void Heal(int amount)
    {
        if (_isDead.Value) return;

        _currentHealth.Value = Mathf.Min(maxHealth, _currentHealth.Value + amount);
        Debug.Log($"[SERVER] Player (ID: {Owner.ClientId}) healed {amount}. New HP: {_currentHealth.Value}");
    }

    /// <summary>
    /// Sağlık değeri değiştiğinde çağrılan SyncVar olayı. Hem sunucuda hem istemcide tetiklenir.
    /// </summary>
    private void OnHealthChanged(int previous, int current, bool asServer)
    {
        OnHealthChangedEvent?.Invoke(current, maxHealth);
        Debug.Log($"[{(asServer ? "SERVER" : "CLIENT")}] HP: {previous} → {current} for Player (ID: {Owner.ClientId})");
    }

    /// <summary>
    /// _isDead durumu değiştiğinde çağrılan SyncVar olayı. Hem sunucuda hem istemcide tetiklenir.
    /// </summary>
    private void OnIsDeadChanged(bool previous, bool current, bool asServer)
    {
        Debug.Log($"[{(asServer ? "SERVER" : "CLIENT")}] _isDead changed: {previous} → {current} for Player (ID: {Owner.ClientId})");

        if (IsOwner) // Sadece kendi oyuncumuz için bu mantığı uygula
        {
            if (current) // Eğer oyuncu öldüyse
            {
                Debug.Log($"[CLIENT {Owner.ClientId}] Kendi oyuncusu öldü. Hareket durduruluyor.");
                _playerController?.SetMovementEnabled(false);
            }
            else // Eğer oyuncu yeniden doğduysa
            {
                Debug.Log($"[CLIENT {Owner.ClientId}] Kendi oyuncusu yeniden doğdu. Hareket etkinleştiriliyor.");
                StartCoroutine(ClientReEnableMovementAfterRespawn(0.5f)); // Biraz daha uzun gecikme
            }
        }
    }

    /// <summary>
    /// Sunucudan hedef istemciye pozisyonu doğrudan ışınlamak için TargetRpc.
    /// </summary>
    /// <param name="conn">Hedef bağlantı.</param>
    /// <param name="pos">Işınlanacak pozisyon.</param>
    [TargetRpc]
    private void TargetTeleportToSpawn(NetworkConnection conn, Vector3 pos)
    {
        Debug.Log($"[CLIENT {conn.ClientId}] TargetTeleportToSpawn RPC received. Teleporting to: {pos}");

        // İstemci tarafında da NetworkTransform ve CharacterController'ı geçici olarak devre dışı bırak
        if (_networkTransform != null) _networkTransform.enabled = false;
        if (_characterController != null) _characterController.enabled = false;

        transform.SetPositionAndRotation(pos, Quaternion.identity);

        // İstemci tarafında da bileşenleri biraz sonra tekrar etkinleştir
        StartCoroutine(ClientReEnableMovementComponentsAfterTeleport(0.1f));
    }

    /// <summary>
    /// İstemci tarafında NetworkTransform ve CharacterController'ı yeniden etkinleştirmek için Coroutine.
    /// </summary>
    private IEnumerator ClientReEnableMovementComponentsAfterTeleport(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (IsOwner) // Sadece kendi objemiz için
        {
            if (_networkTransform != null) _networkTransform.enabled = true;
            if (_characterController != null) _characterController.enabled = true;
            Debug.Log($"[CLIENT {Owner.ClientId}] NetworkTransform and CharacterController re-enabled after teleport on client.");
        }
    }

    /// <summary>
    /// Oyuncunun hareketini etkinleştirmek/devre dışı bırakmak için TargetRpc.
    /// </summary>
    /// <param name="conn">Hedef bağlantı.</param>
    /// <param name="enabled">Hareketin etkin olup olmayacağı.</param>
    [TargetRpc]
    private void TargetSetPlayerMovementRpc(NetworkConnection conn, bool enabled)
    {
        if (_playerController != null)
        {
            _playerController.SetMovementEnabled(enabled);
            Debug.Log($"[CLIENT {conn.ClientId}] TargetSetPlayerMovementRpc received. Movement set to: {enabled}");
        }
    }

    /// <summary>
    /// İstemci tarafında yeniden doğduktan sonra hareketin etkinleştirilmesi için gecikmeli Coroutine.
    /// </summary>
    private IEnumerator ClientReEnableMovementAfterRespawn(float delay)
    {
        Debug.Log($"[CLIENT {Owner.ClientId}] ClientReEnableMovementAfterRespawn coroutine started. Waiting for {delay}s.");
        yield return new WaitForSeconds(delay);
        Debug.Log($"[CLIENT {Owner.ClientId}] ClientReEnableMovementAfterRespawn coroutine finished waiting. Re-enabling movement.");
        _playerController?.SetMovementEnabled(true);
    }

    /// <summary>
    /// Hasar sesi ve efekti oynatmak için ObserversRpc.
    /// </summary>
    [ObserversRpc]
    private void PlayHurtEffectObserversRpc()
    {
        if (hurtSound != null)
            _audioSource.PlayOneShot(hurtSound);
    }

    /// <summary>
    /// Ölüm sesi ve efekti oynatmak için ObserversRpc.
    /// </summary>
    [ObserversRpc]
    private void PlayDeathEffectObserversRpc()
    {
        if (deathSound != null)
            _audioSource.PlayOneShot(deathSound);

        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
    }
}