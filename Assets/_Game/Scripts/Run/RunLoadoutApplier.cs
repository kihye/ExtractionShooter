using UnityEngine;

public sealed class RunLoadoutApplier : MonoBehaviour
{
    [SerializeField] private GameSessionState sessionState;
    [SerializeField] private PlayerWeapon playerWeapon;
    [SerializeField] private ItemDefinition defaultAmmoItem;

    private bool applied;

    private void Awake()
    {
        if (playerWeapon == null)
        {
            playerWeapon = FindFirstObjectByType<PlayerWeapon>();
        }

        if (sessionState == null)
        {
            StashInventory stashInventory = FindFirstObjectByType<StashInventory>();
            if (stashInventory != null)
            {
                sessionState = stashInventory.SessionState;
            }
        }
    }

    private void Start()
    {
        ApplyPendingLoadout();
    }

    private void ApplyPendingLoadout()
    {
        if (applied || sessionState == null || playerWeapon == null)
        {
            return;
        }

        applied = true;
        if (sessionState.ConsumeSuppressNextRunLoadoutApply())
        {
            Debug.Log("Skipped loadout ammo initialization because this Run was restored from save.", this);
            return;
        }

        if (sessionState.HasPendingSceneRestore)
        {
            return;
        }

        defaultAmmoItem ??= playerWeapon.ReserveAmmoItem;

        if (!sessionState.TryConsumePendingLoadout(out int ammoAmount))
        {
            playerWeapon.MigrateSerializedReserveAmmoToInventory(defaultAmmoItem);
            return;
        }

        InventoryAddResult result = playerWeapon.InitializeRunAmmo(ammoAmount, defaultAmmoItem);
        if (result.RemainingAmount > 0)
        {
            sessionState.AddAmmo(result.RemainingAmount);
            Debug.LogWarning($"Could not fit all loadout ammo into inventory. Refunded: {result.RemainingAmount}", this);
        }

        Debug.Log($"Applied loadout ammo: {ammoAmount}. Magazine: {playerWeapon.CurrentAmmo}. Inventory reserve: {playerWeapon.ReserveAmmo}", this);
    }
}
