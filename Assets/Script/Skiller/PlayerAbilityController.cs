using UnityEngine;
using FishNet.Object;
using System.Collections;
using UnityEngine.InputSystem;
using FishNet.Connection;
using Game.Player;

public class PlayerAbilityController : NetworkBehaviour
{
    public AbilityType currentAbility = AbilityType.SpeedBoost;

    [SerializeField] private float abilityDuration = 3f;
    [SerializeField] private float abilityCooldown = 10f;
    [SerializeField] private float blinkDistance = 5f;
    [SerializeField] private AbilitySettings speedBoostSettings;
    [SerializeField] private AbilitySettings damageBoostSettings;
    [SerializeField] private AbilitySettings invisibilitySettings;
    [SerializeField] private AbilitySettings blinkSettings;

    private bool isActive = false;
    private float cooldownTimer = 0f;

    [System.Serializable]
    public struct AbilitySettings
    {
        public float cooldown;
        public float duration;
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryUseAbility();
        }
    }

    void TryUseAbility()
    {
        if (isActive || cooldownTimer > 0f)
            return;

        cooldownTimer = GetCurrentSettings().cooldown;
        StartCoroutine(ActivateAbilityRoutine());
    }

    IEnumerator ActivateAbilityRoutine()
    {
        isActive = true;
        float duration = GetCurrentSettings().duration;

        switch (currentAbility)
        {
            case AbilityType.SpeedBoost:
                Server_UseSpeedBoost();
                break;
            case AbilityType.DamageBoost:
                Server_UseDamageBoost();
                break;
            case AbilityType.Invisibility:
                Server_UseInvisibility();
                break;
            case AbilityType.TeleportBlink:
                Server_UseBlink(transform.forward);
                break;
        }

        if (duration > 0)
            yield return new WaitForSeconds(duration);

        isActive = false;
    }
    // --- HIZLI KOÞMA ---
    [ServerRpc]
    void Server_UseSpeedBoost() => Rpc_ApplySpeedBoost();

    [ObserversRpc]
    void Rpc_ApplySpeedBoost()
    {
        var move = GetComponent<PlayerController>();
        move?.SetSpeedMultiplier(2f);
        StartCoroutine(ResetSpeedAfterDelay());
    }

    private IEnumerator ResetSpeedAfterDelay()
    {
        yield return new WaitForSeconds(abilityDuration);
        GetComponent<PlayerController>()?.ResetSpeedMultiplier();
    }

    // --- FAZLA HASAR ---
    [ServerRpc]
    void Server_UseDamageBoost() => Rpc_ApplyDamageBoost();

    [ObserversRpc]
    void Rpc_ApplyDamageBoost()
    {
        var combat = GetComponent<PlayerCombat>();
        combat?.SetDamageMultiplier(2f);
        StartCoroutine(ResetDamageAfterDelay());
    }

    private IEnumerator ResetDamageAfterDelay()
    {
        yield return new WaitForSeconds(abilityDuration);
        GetComponent<PlayerCombat>()?.ResetDamageMultiplier();
    }

    // --- TELEPORT KAÇIÞ ---
    [ServerRpc]
    void Server_UseBlink(Vector3 direction)
    {
        Vector3 targetPos = transform.position + direction.normalized * blinkDistance;

        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, blinkDistance))
        {
            targetPos = hit.point - direction.normalized * 1f;
        }

        Rpc_TeleportAll(targetPos);
    }

    [ObserversRpc]
    void Rpc_TeleportAll(Vector3 newPos)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = newPos;
        if (cc != null) cc.enabled = true;
    }
    //Görünmezlik
    [ServerRpc]
    void Server_UseInvisibility() => Rpc_ApplyInvisibility();

    [ObserversRpc]
    void Rpc_ApplyInvisibility()
    {
        GetComponent<PlayerVisual>()?.SetVisible(false);
        StartCoroutine(ResetInvisibilityAfterDelay());
    }

    private IEnumerator ResetInvisibilityAfterDelay()
    {
        yield return new WaitForSeconds(abilityDuration);
        GetComponent<PlayerVisual>()?.SetVisible(true);
    }

    private AbilitySettings GetCurrentSettings()
    {
        return currentAbility switch
        {
            AbilityType.SpeedBoost => speedBoostSettings,
            AbilityType.DamageBoost => damageBoostSettings,
            AbilityType.Invisibility => invisibilitySettings,
            AbilityType.TeleportBlink => blinkSettings,
            _ => new AbilitySettings { cooldown = 10f, duration = 3f } // varsayýlan
        };
    }
}
