using UnityEngine;
using FishNet.Object;
using UnityEngine.UI;

public class PlayerCombat : NetworkBehaviour
{
    [Header("Combat Settings")]
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private int damageAmount = 20;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float respawnDelay = 5f;

    [Header("References")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Slider healthBar;
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private Transform[] spawnPoints;

    private Camera playerCamera;
    private int currentHealth;
    private bool isDead = false;

    private void Awake()
    {
        // Awake'te temel referanslarý kontrol et
        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<Slider>();
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            spawnPoints = new Transform[] { transform };
            Debug.LogWarning("SpawnPoints atanmamýþ, varsayýlan olarak kendi pozisyonu kullanýlacak");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
        {
            if (healthBar != null) healthBar.gameObject.SetActive(false);
            return;
        }

        currentHealth = maxHealth;

        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
        else
        {
            Debug.LogWarning("HealthBar referansý bulunamadý");
        }

        if (cameraHolder != null)
        {
            playerCamera = cameraHolder.GetComponentInChildren<Camera>();
            if (playerCamera != null) playerCamera.enabled = true;
        }
    }

    [Server]
    private void ServerRespawn()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("Spawn noktasý yok, varsayýlan pozisyonda respawn");
            transform.position = Vector3.zero;
        }
        else
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            transform.position = spawnPoint.position;
        }

        currentHealth = maxHealth;
        isDead = false;
        RpcOnRespawn();
    }

    // Diðer metodlar ayný kalabilir...


[ObserversRpc]
    private void RpcOnRespawn()
    {
        if (healthBar != null)
            healthBar.value = currentHealth;
    }
}