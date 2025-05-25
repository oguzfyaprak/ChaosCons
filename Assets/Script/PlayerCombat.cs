using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using UnityEngine.UI;
using System.Collections;
using Game.Player;

public class PlayerCombat : NetworkBehaviour
{
    [Header("Combat Settings")]
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private int damageAmount = 20;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float respawnDelay = 3f;

    [Header("References")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Slider healthBar;
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private PlayerController playerController;

    private int currentHealth;
    private bool isDead = false;

    private void Awake()
    {
        if (healthBar == null)
            healthBar = GetComponentInChildren<Slider>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("SpawnPoints atanmadý. Sahnedeki PlayerSpawner'dan alýnacak.");
            
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
    }

    // === Hasar alma ===
    [Server]
    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;

        RpcUpdateHealth(currentHealth);

        if (currentHealth <= 0)
        {
            isDead = true;
            RpcOnDeath();
            StartCoroutine(ServerRespawnRoutine());
        }
    }

    // === Diðer oyuncuya saldýrý ===
    [Server]
    public void TryAttack()
    {
        if (!IsOwner || isDead) return;

        Ray ray = new Ray(cameraHolder.position, cameraHolder.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, attackRange))
        {
            if (hit.collider.TryGetComponent<PlayerCombat>(out var target))
            {
                target.TakeDamage(damageAmount);
            }
        }
    }

    // === Server'da yeniden doðma ===
    [Server]
    private IEnumerator ServerRespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        int id = playerController != null ? playerController.PlayerID : Owner.ClientId;
        Transform spawnPoint = (spawnPoints != null && id < spawnPoints.Length) ? spawnPoints[id] : null;

        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }
        else
        {
            Debug.LogWarning("SpawnPoint bulunamadý, (0,0,0)'a konumlandý.");
            transform.position = Vector3.zero;
        }

        currentHealth = maxHealth;
        isDead = false;
        RpcOnRespawn(currentHealth);
    }

    // === RPC'ler ===
    [ObserversRpc]
    private void RpcUpdateHealth(int newHealth)
    {
        if (healthBar != null)
            healthBar.value = newHealth;
    }

    [ObserversRpc]
    private void RpcOnDeath()
    {
        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
    }

    [ObserversRpc]
    private void RpcOnRespawn(int newHealth)
    {
        if (IsOwner && healthBar != null)
        {
            healthBar.value = newHealth;
        }
    }
}
