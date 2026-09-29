using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerWeapon : MonoBehaviour
{
    public event Action AmmoChanged;

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string fireActionName = "Fire";
    [SerializeField] private string reloadActionName = "Reload";
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0.01f)] private float fireRate = 4f;
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField, Min(0f)] private float range = 18f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField, Min(1)] private int magazineSize = 12;
    [SerializeField, Min(0)] private int currentAmmo = 12;
    [SerializeField, Min(0)] private int reserveAmmo = 48;
    [SerializeField, Min(0.01f)] private float reloadDuration = 1.5f;
    [SerializeField] private AmmoType requiredAmmoType = AmmoType.LightRifle;
    [SerializeField] private ItemDefinition reserveAmmoItem;
    [SerializeField] private PlayerInventory playerInventory;

    private InputAction fireAction;
    private InputAction reloadAction;
    private float nextFireTime;
    private bool isReloading;
    private Coroutine reloadCoroutine;
    private PlayerHealth playerHealth;
    private RuntimeItemInstance activeWeaponItem;
    private bool usesRuntimeEquipment;

    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => GetReserveAmmo();
    public int MagazineSize => magazineSize;
    public int TotalRemainingAmmo => currentAmmo + ReserveAmmo;
    public bool IsReloading => isReloading;
    public AmmoType RequiredAmmoType => requiredAmmoType;
    public ItemDefinition ReserveAmmoItem => reserveAmmoItem;
    public bool HasRuntimeWeaponBinding => activeWeaponItem != null;

    public void BindSessionInventory(PlayerInventory inventory)
    {
        playerInventory = inventory;
        // Session grids already contain all reserve ammo. Prefab reserve is legacy seed data.
        reserveAmmo = 0;
    }

    public void InitializeAmmo(int totalAmmo)
    {
        InitializeRunAmmo(totalAmmo, reserveAmmoItem);
    }

    public InventoryAddResult InitializeRunAmmo(int totalAmmo, ItemDefinition ammoItem)
    {
        CancelReload();

        int availableAmmo = Mathf.Max(0, totalAmmo);
        currentAmmo = Mathf.Min(magazineSize, availableAmmo);
        SaveCurrentMagazineToActiveWeapon();
        return InitializeCarriedAmmo(Mathf.Max(0, availableAmmo - currentAmmo), ammoItem);
    }

    public InventoryAddResult InitializeCarriedAmmo(int amount, ItemDefinition ammoItem)
    {
        CancelReload();

        ClearInventoryReserveAmmo();
        reserveAmmo = 0;
        InventoryAddResult result = AddAmmoToInventory(amount, ammoItem);
        AmmoChanged?.Invoke();
        return result;
    }

    public InventoryAddResult MigrateSerializedReserveAmmoToInventory(ItemDefinition ammoItem)
    {
        if (reserveAmmo <= 0)
        {
            return new InventoryAddResult(ammoItem, 0, 0);
        }

        int amountToMigrate = reserveAmmo;
        reserveAmmo = 0;
        InventoryAddResult result = AddAmmoToInventory(amountToMigrate, ammoItem);

        if (result.RemainingAmount > 0)
        {
            Debug.LogWarning($"Could not fit all serialized reserve ammo into inventory. Lost for this run: {result.RemainingAmount}", this);
        }

        AmmoChanged?.Invoke();
        return result;
    }

    public void AddReserveAmmo(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        InventoryAddResult result = AddAmmoToInventory(amount, reserveAmmoItem);
        if (!result.AddedAny && !HasInventoryAmmoSource())
        {
            reserveAmmo += amount;
        }

        AmmoChanged?.Invoke();
    }

    public void EquipRuntimeWeapon(RuntimeItemInstance weaponItem)
    {
        if (weaponItem?.Definition == null || weaponItem.Definition.Category != ItemCategory.Weapon)
        {
            return;
        }

        usesRuntimeEquipment = true;
        CancelReload();
        SaveCurrentMagazineToActiveWeapon();
        activeWeaponItem = weaponItem;
        ApplyWeaponDefinition(weaponItem.Definition);
        currentAmmo = Mathf.Clamp(weaponItem.CurrentMagazineAmmo, 0, magazineSize);
        AmmoChanged?.Invoke();
    }

    public void ClearRuntimeWeapon()
    {
        usesRuntimeEquipment = true;
        CancelReload();
        if (activeWeaponItem == null)
        {
            currentAmmo = 0;
            AmmoChanged?.Invoke();
            return;
        }

        SaveCurrentMagazineToActiveWeapon();
        activeWeaponItem = null;
        currentAmmo = 0;
        AmmoChanged?.Invoke();
    }

    private void Awake()
    {
        fireAction = FindAction(fireActionName);
        reloadAction = FindAction(reloadActionName, "<Keyboard>/r");
        playerHealth = GetComponent<PlayerHealth>();
        playerInventory ??= GetComponent<PlayerInventory>();
        currentAmmo = Mathf.Clamp(currentAmmo, 0, magazineSize);
    }

    private void OnEnable()
    {
        fireAction?.Enable();
        reloadAction?.Enable();

        if (playerInventory != null)
        {
            playerInventory.InventoryChanged += HandleInventoryChanged;
        }
    }

    private void OnDisable()
    {
        SaveCurrentMagazineToActiveWeapon();
        CancelReload();
        fireAction?.Disable();
        reloadAction?.Disable();

        if (playerInventory != null)
        {
            playerInventory.InventoryChanged -= HandleInventoryChanged;
        }
    }

    private void Start()
    {
        if (reserveAmmo > 0 && HasInventoryAmmoSource())
        {
            MigrateSerializedReserveAmmoToInventory(reserveAmmoItem);
            return;
        }

        AmmoChanged?.Invoke();
    }

    private void Update()
    {
        if (usesRuntimeEquipment && activeWeaponItem == null)
        {
            return;
        }

        if (reloadAction != null && reloadAction.WasPressedThisFrame())
        {
            TryStartReload();
        }

        if (fireAction == null || !fireAction.IsPressed())
        {
            return;
        }

        if (isReloading || IsPlayerDead())
        {
            return;
        }

        if (currentAmmo <= 0)
        {
            if (fireAction.WasPressedThisFrame())
            {
                Debug.Log("Magazine empty.", this);
            }

            return;
        }

        if (Time.time < nextFireTime)
        {
            return;
        }

        if (Fire())
        {
            currentAmmo--;
            SaveCurrentMagazineToActiveWeapon();
            AmmoChanged?.Invoke();
            nextFireTime = Time.time + 1f / fireRate;
        }
    }

    private bool Fire()
    {
        Vector3 direction = transform.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        direction.Normalize();

        Vector3 origin = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up * 0.2f + direction * 0.65f;

        float drawDistance = range;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            drawDistance = hit.distance;

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
        }

        Debug.DrawRay(origin, direction * drawDistance, Color.red, 0.15f);
        return true;
    }

    private void TryStartReload()
    {
        if (usesRuntimeEquipment && activeWeaponItem == null)
        {
            return;
        }

        if (isReloading || IsPlayerDead() || currentAmmo >= magazineSize || ReserveAmmo <= 0)
        {
            return;
        }

        reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        AmmoChanged?.Invoke();

        float elapsedTime = 0f;
        while (elapsedTime < reloadDuration)
        {
            if (IsPlayerDead())
            {
                isReloading = false;
                reloadCoroutine = null;
                AmmoChanged?.Invoke();
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        int neededAmmo = magazineSize - currentAmmo;
        int loadedAmmo = ConsumeReserveAmmo(neededAmmo);
        currentAmmo += loadedAmmo;
        SaveCurrentMagazineToActiveWeapon();
        isReloading = false;
        reloadCoroutine = null;
        AmmoChanged?.Invoke();
    }

    private void CancelReload()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        if (isReloading)
        {
            isReloading = false;
            AmmoChanged?.Invoke();
        }
    }

    private bool IsPlayerDead()
    {
        return playerHealth != null && playerHealth.IsDead;
    }

    private void ApplyWeaponDefinition(ItemDefinition definition)
    {
        magazineSize = definition.WeaponMagazineSize;
        fireRate = definition.WeaponFireRate;
        damage = definition.WeaponDamage;
        range = definition.WeaponRange;
        reloadDuration = definition.WeaponReloadDuration;
        requiredAmmoType = definition.WeaponAmmoType;
        currentAmmo = Mathf.Clamp(currentAmmo, 0, magazineSize);
    }

    private void SaveCurrentMagazineToActiveWeapon()
    {
        if (activeWeaponItem?.Definition == null || activeWeaponItem.Definition.Category != ItemCategory.Weapon)
        {
            return;
        }

        activeWeaponItem.SetCurrentMagazineAmmo(currentAmmo, magazineSize);
    }

    private int GetReserveAmmo()
    {
        if (HasInventoryAmmoSource())
        {
            return playerInventory.GetCompatibleAmmoCount(requiredAmmoType);
        }

        return reserveAmmo;
    }

    private int ConsumeReserveAmmo(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        if (HasInventoryAmmoSource())
        {
            return playerInventory.ConsumeCompatibleAmmo(requiredAmmoType, amount);
        }

        int loadedAmmo = Mathf.Min(amount, reserveAmmo);
        reserveAmmo -= loadedAmmo;
        return loadedAmmo;
    }

    private InventoryAddResult AddAmmoToInventory(int amount, ItemDefinition ammoItem)
    {
        if (amount <= 0)
        {
            return new InventoryAddResult(ammoItem, Mathf.Max(0, amount), 0);
        }

        if (ammoItem != null)
        {
            reserveAmmoItem = ammoItem;
        }

        if (playerInventory == null || reserveAmmoItem == null)
        {
            return new InventoryAddResult(reserveAmmoItem, amount, 0);
        }

        if (reserveAmmoItem.Category != ItemCategory.Ammo || reserveAmmoItem.AmmoType != requiredAmmoType)
        {
            Debug.LogWarning($"{reserveAmmoItem.DisplayName} is not compatible with {requiredAmmoType}.", this);
            return new InventoryAddResult(reserveAmmoItem, amount, 0);
        }

        return playerInventory.TryAddItem(reserveAmmoItem, amount);
    }

    private void ClearInventoryReserveAmmo()
    {
        if (!HasInventoryAmmoSource())
        {
            return;
        }

        int currentReserve = playerInventory.GetCompatibleAmmoCount(requiredAmmoType);
        if (currentReserve > 0)
        {
            playerInventory.ConsumeCompatibleAmmo(requiredAmmoType, currentReserve);
        }
    }

    private bool HasInventoryAmmoSource()
    {
        return playerInventory != null && reserveAmmoItem != null;
    }

    private void HandleInventoryChanged()
    {
        AmmoChanged?.Invoke();
    }

    private InputAction FindAction(string actionName, string fallbackBinding = "<Mouse>/leftButton")
    {
        if (inputActions == null)
        {
            return new InputAction(actionName, InputActionType.Button, fallbackBinding);
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, true);
        return actionMap.FindAction(actionName, true);
    }
}
