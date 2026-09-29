using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameProgressSaveFile
{
    public int schemaVersion;
    public string savedAtUtc;
    public GameProgressData progress;
}

[Serializable]
public sealed class GameProgressData
{
    public int credits;
    public int ammoStock;
    public SceneProgressSaveData scene;
    public PlayerSceneSaveData playerScene;
    public GridInventorySaveData stashGrid;
    public PlayerRuntimeSaveData player;
    public RunWorldSaveData runWorld;
}

[Serializable]
public sealed class SceneProgressSaveData
{
    public string mapId;
    public string sceneName;
    public string displayName;
    public string sceneKind;
}

[Serializable]
public sealed class PlayerSceneSaveData
{
    public float positionX;
    public float positionY;
    public float positionZ;
    public float yaw;
    public float currentHealth;
}

[Serializable]
public sealed class PlayerRuntimeSaveData
{
    public List<GridInventorySaveData> inventoryGrids = new List<GridInventorySaveData>();
    public List<EquipmentSlotSaveData> equipmentSlots = new List<EquipmentSlotSaveData>();
    public List<EquippedBagGridSaveData> equippedBagGrids = new List<EquippedBagGridSaveData>();
}

[Serializable]
public sealed class GridInventorySaveData
{
    public string displayName;
    public int width;
    public int height;
    public List<GridItemPlacementSaveData> placements = new List<GridItemPlacementSaveData>();
}

[Serializable]
public sealed class GridItemPlacementSaveData
{
    public RuntimeItemSaveData item;
    public int x;
    public int y;
}

[Serializable]
public sealed class RuntimeItemSaveData
{
    public string instanceId;
    public string itemId;
    public int quantity;
    public int upgradeLevel;
    public int currentMagazineAmmo;
}

[Serializable]
public sealed class EquipmentSlotSaveData
{
    public string slotType;
    public string displayName;
    public string state;
    public string occupancy;
    public RuntimeItemSaveData equippedItem;
}

[Serializable]
public sealed class EquippedBagGridSaveData
{
    public int slotIndex;
    public int gridIndex;
}

[Serializable]
public sealed class RunWorldSaveData
{
    public string runId;
    public List<EnemySaveData> enemies = new List<EnemySaveData>();
    public List<PickupSaveData> pickups = new List<PickupSaveData>();
}

[Serializable]
public sealed class EnemySaveData
{
    public string sceneObjectId;
    public float currentHealth;
    public float positionX;
    public float positionY;
    public float positionZ;
    public float yaw;
}

[Serializable]
public sealed class PickupSaveData
{
    public string sceneObjectId;
    public string runtimeSpawnId;
    public string pickupType;
    public string itemId;
    public int amount;
    public float positionX;
    public float positionY;
    public float positionZ;
    public float yaw;
}
