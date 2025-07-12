using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using FishNet.Component.Transforming; // NetworkTransform için gerekli
using FishNet.Connection;
using Game.Player;

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
    private NetworkTransform _networkTransform; // YENİ: NetworkTransform referansı
    private CharacterController _characterController; // YENİ: CharacterController referansı

    public delegate void HealthChangedDelegate(int newHealth, int maxHealth);
    public event HealthChangedDelegate OnHealthChangedEvent;

    public delegate void DeathDelegate(int attackerId);
    public event DeathDelegate OnDeathEvent;

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

        // YENİ: NetworkTransform ve CharacterController referanslarını al
        _networkTransform = GetComponent<NetworkTransform>();
        if (_networkTransform == null) Debug.LogError("NetworkTransform component not found on player!");
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null) Debug.LogError("CharacterController component not found on player!");

        if (IsServerInitialized)
        {
            _currentHealth.Value = maxHealth;
            _isDead.Value = false;
            _initialSpawnPosition.Value = transform.position;
            Debug.Log($"[SERVER] Initialized HealthSystem for {gameObject.name}. Spawn Pos: {_initialSpawnPosition.Value}");
        }
    }

    [Server]
    public void TakeDamage(int amount, int attackerId = -1)
    {
        if (_isInvincible || _isDead.Value) return;

        _currentHealth.Value = Mathf.Max(0, _currentHealth.Value - amount);
        Debug.Log($"[SERVER] {gameObject.name} took {amount} damage. HP now: {_currentHealth.Value}");

        if (_currentHealth.Value <= 0)
        {
            HandleDeath(attackerId);
        }
        else
        {
            StartCoroutine(InvincibilityCoroutine());
            PlayHurtEffectObserversRpc();
        }
    }

    [Server]
    private void HandleDeath(int attackerId)
    {
        if (_isDead.Value) return;

        _isDead.Value = true;
        Debug.Log("[SERVER] Player died. Setting _isDead to true.");

        OnDeathEvent?.Invoke(attackerId);
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

    [Server]
    public void Respawn()
    {
        Debug.Log("[SERVER] Respawning player to initial spawn position.");

        _currentHealth.Value = maxHealth;
        _isInvincible = false;
        _isDead.Value = false;

        // YENİ: Respawn işlemi için NetworkTransform'u geçici olarak devre dışı bırak
        // Bu, istemcinin kendi pozisyon güncellemesini durdurur ve sunucunun yetkisini güçlendirir.
        if (_networkTransform != null) _networkTransform.enabled = false;
        // YENİ: CharacterController'ı da geçici olarak devre dışı bırak
        if (_characterController != null) _characterController.enabled = false;

        // Sunucuda karakterin pozisyonunu ayarla
        transform.SetPositionAndRotation(_initialSpawnPosition.Value, Quaternion.identity);

        if (_playerController != null)
            _playerController.ResetVelocity();

        // Oyuncunun kendi istemcisine doğru spawn pozisyonunu doğrudan ilet
        // Bu RPC, istemcinin kendi pozisyonunu ayarlamasını sağlar.
        TargetTeleportToSpawn(Owner, _initialSpawnPosition.Value);

        // YENİ: NetworkTransform'ı ve CharacterController'ı biraz sonra etkinleştir
        // Bu, istemcinin pozisyonu alıp stabilize etmesine zaman tanır.
        StartCoroutine(ReEnableMovementComponentsAfterTeleport(0.1f)); // Kısa bir gecikme

        // Oyuncunun kendi istemcisine hareketi etkinleştirme komutu gönder
        TargetSetPlayerMovementRpc(Owner, true);
    }

    // YENİ COROUTINE: NetworkTransform ve CharacterController'ı yeniden etkinleştirmek için
    private IEnumerator ReEnableMovementComponentsAfterTeleport(float delay)
    {
        yield return new WaitForSeconds(delay); // Kısa bir gecikme bekle

        if (IsServerInitialized) // Sadece sunucu tarafında bu bileşenleri tekrar etkinleştir
        {
            if (_networkTransform != null) _networkTransform.enabled = true;
            if (_characterController != null) _characterController.enabled = true;
            Debug.Log("[SERVER] NetworkTransform and CharacterController re-enabled after teleport.");
        }
    }


    [Server]
    public void Heal(int amount)
    {
        if (_isDead.Value) return;

        _currentHealth.Value = Mathf.Min(maxHealth, _currentHealth.Value + amount);
        Debug.Log($"[SERVER] Player healed {amount}. New HP: {_currentHealth.Value}");
    }

    private void OnHealthChanged(int previous, int current, bool asServer)
    {
        OnHealthChangedEvent?.Invoke(current, maxHealth);
        Debug.Log($"[{(asServer ? "SERVER" : "CLIENT")}] HP: {previous} → {current}");
    }

    private void OnIsDeadChanged(bool previous, bool current, bool asServer)
    {
        Debug.Log($"[{(asServer ? "SERVER" : "CLIENT")}] _isDead changed: {previous} → {current}");

        if (IsOwner)
        {
            if (current)
            {
                Debug.Log($"[CLIENT {Owner.ClientId}] Kendi oyuncusu öldü. Hareket durduruluyor.");
                _playerController?.SetMovementEnabled(false);
            }
            else
            {
                Debug.Log($"[CLIENT {Owner.ClientId}] Kendi oyuncusu yeniden doğdu. Hareket etkinleştiriliyor.");
                // Client tarafında hareketi etkinleştirme gecikmesini artırabiliriz
                StartCoroutine(ClientReEnableMovementAfterRespawn(0.5f)); // Biraz daha uzun gecikme
            }
        }
    }

    // TargetRpc: Sunucudan hedef istemciye pozisyonu doğrudan ışınlamak için
    [TargetRpc]
    private void TargetTeleportToSpawn(NetworkConnection conn, Vector3 pos)
    {
        Debug.Log($"[CLIENT {conn.ClientId}] TargetTeleportToSpawn RPC received. Teleporting to: {pos}");

        // YENİ: İstemci tarafında da NetworkTransform ve CharacterController'ı geçici olarak devre dışı bırak
        if (_networkTransform != null) _networkTransform.enabled = false;
        if (_characterController != null) _characterController.enabled = false;

        transform.SetPositionAndRotation(pos, Quaternion.identity);

        // YENİ: İstemci tarafında da bileşenleri biraz sonra tekrar etkinleştir
        StartCoroutine(ClientReEnableMovementComponentsAfterTeleport(0.1f)); // Aynı kısa gecikme
    }

    // YENİ COROUTINE: İstemci tarafında NetworkTransform ve CharacterController'ı yeniden etkinleştirmek için
    private IEnumerator ClientReEnableMovementComponentsAfterTeleport(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (IsOwner) // Sadece sahip olan istemci (kendisi) için
        {
            if (_networkTransform != null) _networkTransform.enabled = true;
            if (_characterController != null) _characterController.enabled = true;
            Debug.Log($"[CLIENT {Owner.ClientId}] NetworkTransform and CharacterController re-enabled after teleport on client.");
        }
    }


    [TargetRpc]
    private void TargetSetPlayerMovementRpc(NetworkConnection conn, bool enabled)
    {
        if (_playerController != null)
        {
            _playerController.SetMovementEnabled(enabled);
            Debug.Log($"[CLIENT {conn.ClientId}] TargetSetPlayerMovementRpc received. Movement set to: {enabled}");
        }
    }

    private IEnumerator ClientReEnableMovementAfterRespawn(float delay)
    {
        Debug.Log($"[CLIENT {Owner.ClientId}] ClientReEnableMovementAfterRespawn coroutine started. Waiting for {delay}s.");
        yield return new WaitForSeconds(delay);
        Debug.Log($"[CLIENT {Owner.ClientId}] ClientReEnableMovementAfterRespawn coroutine finished waiting. Re-enabling movement.");
        _playerController?.SetMovementEnabled(true);
    }

    [ObserversRpc]
    private void PlayHurtEffectObserversRpc()
    {
        if (hurtSound != null)
            _audioSource.PlayOneShot(hurtSound);
    }

    [ObserversRpc]
    private void PlayDeathEffectObserversRpc()
    {
        if (deathSound != null)
            _audioSource.PlayOneShot(deathSound);

        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
    }
}