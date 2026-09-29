using UnityEngine;

[CreateAssetMenu(menuName = "Extraction Shooter/Item Definition", fileName = "NewItemDefinition")]
public class ItemDefinition : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private ItemCategory category = ItemCategory.SpecialLoot;
    [SerializeField] private AmmoType ammoType;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(1)] private int gridWidth = 1;
    [SerializeField, Min(1)] private int gridHeight = 1;
    [SerializeField] private bool canStack = true;
    [SerializeField, Min(1)] private int maxStack = 99;
    [SerializeField] private bool canEquip;
    [SerializeField] private EquipmentSlotType equipmentSlot;
    [SerializeField, Min(0)] private int maxUpgradeLevel = 3;
    [SerializeField, Min(0f)] private float maxHealthBonus;
    [SerializeField, Min(1)] private int bagGridWidth = 6;
    [SerializeField, Min(1)] private int bagGridHeight = 5;
    [SerializeField, Min(0)] private int sellValue;
    [Header("Weapon")]
    [SerializeField, Min(1)] private int weaponMagazineSize = 12;
    [SerializeField] private AmmoType weaponAmmoType = AmmoType.LightRifle;
    [SerializeField, Min(0.01f)] private float weaponFireRate = 4f;
    [SerializeField, Min(0f)] private float weaponDamage = 25f;
    [SerializeField, Min(0f)] private float weaponRange = 18f;
    [SerializeField, Min(0.01f)] private float weaponReloadDuration = 1.5f;
    [SerializeField, Min(0)] private int initialMagazineAmmo;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public ItemCategory Category => category;
    public AmmoType AmmoType => ammoType;
    public Sprite Icon => icon;
    public int GridWidth => Mathf.Max(1, gridWidth);
    public int GridHeight => Mathf.Max(1, gridHeight);
    public bool CanStack => canStack;
    public int MaxStack => canStack ? Mathf.Max(1, maxStack) : 1;
    public bool CanEquip => canEquip;
    public EquipmentSlotType EquipmentSlot => equipmentSlot;
    public int MaxUpgradeLevel => Mathf.Max(0, maxUpgradeLevel);
    public float MaxHealthBonus => Mathf.Max(0f, maxHealthBonus);
    public int BagGridWidth => Mathf.Max(1, bagGridWidth);
    public int BagGridHeight => Mathf.Max(1, bagGridHeight);
    public int SellValue => Mathf.Max(0, sellValue);
    public int WeaponMagazineSize => Mathf.Max(1, weaponMagazineSize);
    public AmmoType WeaponAmmoType => weaponAmmoType;
    public float WeaponFireRate => Mathf.Max(0.01f, weaponFireRate);
    public float WeaponDamage => Mathf.Max(0f, weaponDamage);
    public float WeaponRange => Mathf.Max(0f, weaponRange);
    public float WeaponReloadDuration => Mathf.Max(0.01f, weaponReloadDuration);
    public int InitialMagazineAmmo => Mathf.Clamp(initialMagazineAmmo, 0, WeaponMagazineSize);
}
