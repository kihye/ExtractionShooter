using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameProgressSaveService
{
    public const int CurrentSchemaVersion = 3;
    public const string SaveFileName = "progress_slot_1.json";
    public const string BackupFileName = "progress_slot_1.bak.json";

    private readonly ItemDefinitionRegistry itemRegistry;
    private readonly SceneMapRegistry sceneMapRegistry;

    public GameProgressSaveService(ItemDefinitionRegistry itemRegistry, SceneMapRegistry sceneMapRegistry = null)
    {
        this.itemRegistry = itemRegistry;
        this.sceneMapRegistry = sceneMapRegistry;
    }

    public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    public string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);

    public bool HasSaveFile()
    {
        return File.Exists(SavePath);
    }

    public bool TryGetLastSaveTime(out DateTime savedAtUtc)
    {
        savedAtUtc = default;
        if (!TryReadAndValidate(SavePath, out GameProgressSaveFile saveFile, out _, out _))
        {
            return false;
        }

        return DateTime.TryParse(saveFile.savedAtUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out savedAtUtc);
    }

    public bool HasValidBackup(out string validationIssue)
    {
        return TryReadAndValidate(BackupPath, out _, out _, out validationIssue);
    }

    public bool TryReadSummary(bool useBackup, out GameProgressSaveSummary summary, out string issue)
    {
        summary = null;
        string path = useBackup ? BackupPath : SavePath;
        if (!TryReadAndValidate(path, out GameProgressSaveFile saveFile, out _, out issue))
        {
            return false;
        }

        GameProgressData progress = saveFile.progress;
        summary = new GameProgressSaveSummary
        {
            savedAtUtc = saveFile.savedAtUtc,
            sceneKind = progress.scene != null ? progress.scene.sceneKind : GameSceneKind.Base.ToString(),
            mapId = progress.scene != null ? progress.scene.mapId : "base",
            mapName = progress.scene != null ? progress.scene.displayName : "Base",
            sceneName = progress.scene != null ? progress.scene.sceneName : "BaseScene",
            positionX = progress.playerScene != null ? progress.playerScene.positionX : 0f,
            positionY = progress.playerScene != null ? progress.playerScene.positionY : 0f,
            positionZ = progress.playerScene != null ? progress.playerScene.positionZ : 0f,
            credits = progress.credits,
            inventoryItemCount = CountItems(progress.player?.inventoryGrids),
            stashItemCount = CountItems(progress.stashGrid)
        };

        return true;
    }

    public GameProgressSaveResult Save(GameSessionState sessionState)
    {
        if (sessionState == null)
        {
            return GameProgressSaveResult.Fail("저장할 진행 상태가 없습니다.", "GameSessionState is null.");
        }

        if (!ValidateRegistry(out string registryIssue))
        {
            return GameProgressSaveResult.Fail("아이템 데이터 설정이 올바르지 않습니다.", registryIssue);
        }

        sessionState.InitializeIfNeeded();
        DateTime savedAtUtc = DateTime.UtcNow;
        GameProgressSaveFile saveFile;
        try
        {
            saveFile = Capture(sessionState, savedAtUtc);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save snapshot capture failed: {exception}");
            return GameProgressSaveResult.Fail("저장 스냅샷 생성에 실패했습니다.", exception.ToString());
        }

        if (!TryValidate(saveFile, out _, out string validationIssue))
        {
            Debug.LogWarning($"Save data validation failed: {validationIssue}");
            return GameProgressSaveResult.Fail("저장 데이터 검증에 실패했습니다.", validationIssue);
        }

        string directory = Application.persistentDataPath;
        string tempPath = SavePath + ".tmp";
        bool hadExistingSave = File.Exists(SavePath);

        try
        {
            Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(saveFile, true);
            File.WriteAllText(tempPath, json, Encoding.UTF8);

            if (!TryReadAndValidate(tempPath, out _, out _, out string tempIssue))
            {
                Debug.LogWarning($"Temporary save file validation failed: {tempIssue}");
                return GameProgressSaveResult.Fail("저장 파일 검증에 실패했습니다.", tempIssue);
            }

            ReplaceSaveFile(tempPath);
            Debug.Log(hadExistingSave
                ? $"Save file replaced successfully: {SavePath}"
                : $"Save file created successfully: {SavePath}");
            return GameProgressSaveResult.Success("저장했습니다.", savedAtUtc);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save file write or replace failed: {exception}");
            return GameProgressSaveResult.Fail("저장에 실패했습니다.", exception.ToString());
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    public GameProgressSaveResult Load(GameSessionState sessionState, bool useBackup)
    {
        if (sessionState == null)
        {
            return GameProgressSaveResult.Fail("불러올 진행 상태 대상이 없습니다.", "GameSessionState is null.");
        }

        string path = useBackup ? BackupPath : SavePath;
        if (!File.Exists(path))
        {
            return GameProgressSaveResult.Fail(useBackup ? "백업 저장 파일이 없습니다." : "저장 파일이 없습니다.");
        }

        if (!TryReadAndValidate(path, out GameProgressSaveFile saveFile, out RestoredProgress restoredProgress, out string issue))
        {
            bool backupAvailable = !useBackup && HasValidBackup(out _);
            return GameProgressSaveResult.Fail(
                backupAvailable ? "저장 파일을 불러올 수 없습니다. 백업을 사용할 수 있습니다." : "저장 파일을 불러올 수 없습니다.",
                issue,
                backupAvailable);
        }

        sessionState.ApplyLoadedProgress(
            restoredProgress.Credits,
            restoredProgress.AmmoStock,
            restoredProgress.StashGrid,
            restoredProgress.PlayerRuntimeState,
            saveFile.progress.scene,
            saveFile.progress.playerScene,
            saveFile.progress.runWorld);

        DateTime.TryParse(saveFile.savedAtUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime savedAtUtc);
        return GameProgressSaveResult.Success(
            useBackup ? "백업 저장을 불러왔습니다." : "불러왔습니다.",
            savedAtUtc,
            saveFile.progress.scene?.sceneName,
            saveFile.progress.scene?.mapId);
    }

    private GameProgressSaveFile Capture(GameSessionState sessionState, DateTime savedAtUtc)
    {
        PlayerRuntimeState playerState = sessionState.PlayerRuntimeState;
        EnsureSaveReadyRuntimeState(playerState);
        Dictionary<GridInventory, int> gridIndexes = new Dictionary<GridInventory, int>();

        GameProgressSaveFile saveFile = new GameProgressSaveFile
        {
            schemaVersion = CurrentSchemaVersion,
            savedAtUtc = savedAtUtc.ToString("O"),
            progress = new GameProgressData
            {
                credits = sessionState.Credits,
                ammoStock = sessionState.AmmoStock,
                scene = CaptureScene(sessionState),
                playerScene = CapturePlayerScene(),
                stashGrid = CaptureGrid(sessionState.StashGrid),
                player = new PlayerRuntimeSaveData(),
                runWorld = null
            }
        };

        if (saveFile.progress.scene != null
            && Enum.TryParse(saveFile.progress.scene.sceneKind, out GameSceneKind sceneKind)
            && sceneKind == GameSceneKind.Run)
        {
            saveFile.progress.runWorld = CaptureRunWorld(sessionState);
        }

        for (int i = 0; i < playerState.InventoryGrids.Count; i++)
        {
            GridInventory grid = playerState.InventoryGrids[i];
            if (grid == null)
            {
                throw new InvalidOperationException($"Player inventory grid {i} is missing.");
            }

            gridIndexes[grid] = saveFile.progress.player.inventoryGrids.Count;
            saveFile.progress.player.inventoryGrids.Add(CaptureGrid(grid));
        }

        for (int i = 0; i < playerState.EquipmentSlots.Count; i++)
        {
            EquipmentSlot slot = playerState.EquipmentSlots[i];
            if (slot == null)
            {
                throw new InvalidOperationException($"Equipment slot definition {i} is missing.");
            }

            saveFile.progress.player.equipmentSlots.Add(new EquipmentSlotSaveData
            {
                slotType = slot.SlotType.ToString(),
                displayName = slot.DisplayName,
                state = slot.State.ToString(),
                occupancy = slot.EquippedItem == null ? "Empty" : "Equipped",
                equippedItem = CaptureItem(slot.EquippedItem)
            });
        }

        foreach (KeyValuePair<EquipmentSlot, GridInventory> pair in playerState.EquippedBagGrids)
        {
            int slotIndex = playerState.EquipmentSlots.IndexOf(pair.Key);
            if (slotIndex < 0 || pair.Value == null || !gridIndexes.TryGetValue(pair.Value, out int gridIndex))
            {
                continue;
            }

            saveFile.progress.player.equippedBagGrids.Add(new EquippedBagGridSaveData
            {
                slotIndex = slotIndex,
                gridIndex = gridIndex
            });
        }

        return saveFile;
    }

    private static void EnsureSaveReadyRuntimeState(PlayerRuntimeState playerState)
    {
        playerState?.InitializeIfNeeded(6, 5);
    }

    private SceneProgressSaveData CaptureScene(GameSessionState sessionState)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (sceneMapRegistry != null && sceneMapRegistry.TryGetBySceneName(activeScene.name, out SceneMapRegistry.SceneMapEntry map))
        {
            sessionState.SetCurrentScene(map.SceneKind, map.MapId);
            return new SceneProgressSaveData
            {
                mapId = map.MapId,
                sceneName = map.SceneName,
                displayName = map.DisplayName,
                sceneKind = map.SceneKind.ToString()
            };
        }

        GameSceneKind kind = activeScene.name == "PrototypeScene" ? GameSceneKind.Run : GameSceneKind.Base;
        string mapId = string.IsNullOrWhiteSpace(sessionState.CurrentMapId) ? kind.ToString() : sessionState.CurrentMapId;
        return new SceneProgressSaveData
        {
            mapId = mapId,
            sceneName = activeScene.name,
            displayName = activeScene.name,
            sceneKind = kind.ToString()
        };
    }

    private static PlayerSceneSaveData CapturePlayerScene()
    {
        PlayerInventory player = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
        if (player == null)
        {
            return new PlayerSceneSaveData();
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        Vector3 position = player.transform.position;
        return new PlayerSceneSaveData
        {
            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,
            yaw = player.transform.eulerAngles.y,
            currentHealth = health != null ? health.CurrentHealth : 0f
        };
    }

    private RunWorldSaveData CaptureRunWorld(GameSessionState sessionState)
    {
        RunWorldSaveData runWorld = new RunWorldSaveData
        {
            runId = string.IsNullOrWhiteSpace(sessionState.CurrentRunId) ? Guid.NewGuid().ToString("N") : sessionState.CurrentRunId
        };

        foreach (DummyEnemy enemy in UnityEngine.Object.FindObjectsByType<DummyEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            ScenePersistentId persistentId = enemy.GetComponent<ScenePersistentId>();
            if (persistentId == null || string.IsNullOrWhiteSpace(persistentId.PersistentId))
            {
                throw new InvalidOperationException($"Enemy '{enemy.name}' has no persistent scene ID and cannot be restored.");
            }

            if (enemy.CurrentHealth <= 0f)
            {
                continue;
            }

            Vector3 position = enemy.transform.position;
            runWorld.enemies.Add(new EnemySaveData
            {
                sceneObjectId = persistentId.PersistentId,
                currentHealth = enemy.CurrentHealth,
                positionX = position.x,
                positionY = position.y,
                positionZ = position.z,
                yaw = enemy.transform.eulerAngles.y
            });
        }

        foreach (ItemPickup pickup in UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            CapturePickup(runWorld, pickup.gameObject, "Item", pickup.Definition, pickup.Amount);
        }

        foreach (AmmoPickup pickup in UnityEngine.Object.FindObjectsByType<AmmoPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            CapturePickup(runWorld, pickup.gameObject, "Ammo", pickup.AmmoItem, pickup.AmmoAmount);
        }

        return runWorld;
    }

    private static void CapturePickup(RunWorldSaveData runWorld, GameObject gameObject, string pickupType, ItemDefinition definition, int amount)
    {
        if (definition == null || amount <= 0)
        {
            return;
        }

        ScenePersistentId sceneId = gameObject.GetComponent<ScenePersistentId>();
        RuntimeSpawnId runtimeId = gameObject.GetComponent<RuntimeSpawnId>();
        Vector3 position = gameObject.transform.position;

        runWorld.pickups.Add(new PickupSaveData
        {
            sceneObjectId = sceneId != null ? sceneId.PersistentId : null,
            runtimeSpawnId = sceneId == null && runtimeId != null ? runtimeId.SpawnId : null,
            pickupType = pickupType,
            itemId = definition.ItemId,
            amount = amount,
            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,
            yaw = gameObject.transform.eulerAngles.y
        });
    }

    private static GridInventorySaveData CaptureGrid(GridInventory grid)
    {
        GridInventorySaveData saveData = new GridInventorySaveData
        {
            displayName = grid != null ? grid.DisplayName : string.Empty,
            width = grid != null ? grid.Width : 0,
            height = grid != null ? grid.Height : 0,
            placements = new List<GridItemPlacementSaveData>()
        };

        if (grid == null)
        {
            return saveData;
        }

        foreach (GridItemPlacement placement in grid.Placements)
        {
            if (placement?.Item?.Definition == null)
            {
                throw new InvalidOperationException($"Grid '{grid.DisplayName}' contains a placement with missing item or definition.");
            }

            saveData.placements.Add(new GridItemPlacementSaveData
            {
                item = CaptureItem(placement.Item),
                x = placement.X,
                y = placement.Y
            });
        }

        return saveData;
    }

    private static RuntimeItemSaveData CaptureItem(RuntimeItemInstance item)
    {
        if (item == null)
        {
            return null;
        }

        if (item.Definition == null)
        {
            throw new InvalidOperationException("Existing item has no definition.");
        }

        return new RuntimeItemSaveData
        {
            instanceId = item.StoredInstanceId,
            itemId = item.Definition.ItemId,
            quantity = item.Quantity,
            upgradeLevel = item.UpgradeLevel,
            currentMagazineAmmo = item.CurrentMagazineAmmo
        };
    }

    private bool TryReadAndValidate(string path, out GameProgressSaveFile saveFile, out RestoredProgress restoredProgress, out string issue)
    {
        saveFile = null;
        restoredProgress = null;

        if (!File.Exists(path))
        {
            issue = $"Save file does not exist: {path}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            saveFile = JsonUtility.FromJson<GameProgressSaveFile>(json);
        }
        catch (Exception exception)
        {
            issue = exception.ToString();
            return false;
        }

        return TryValidate(saveFile, out restoredProgress, out issue);
    }

    private bool TryValidate(GameProgressSaveFile saveFile, out RestoredProgress restoredProgress, out string issue)
    {
        restoredProgress = null;

        if (!ValidateRegistry(out issue))
        {
            return false;
        }

        if (saveFile == null)
        {
            issue = "Save file is empty or malformed.";
            return false;
        }

        if (saveFile.schemaVersion != CurrentSchemaVersion && saveFile.schemaVersion != 2 && saveFile.schemaVersion != 1)
        {
            issue = $"Unsupported save schema version: {saveFile.schemaVersion}.";
            return false;
        }

        if (saveFile.progress == null)
        {
            issue = "Save file has no progress payload.";
            return false;
        }

        if (saveFile.progress.credits < 0)
        {
            issue = "Credits cannot be negative.";
            return false;
        }

        if (saveFile.progress.ammoStock < 0)
        {
            issue = "Ammo stock cannot be negative.";
            return false;
        }

        if (!TryValidateSceneData(saveFile, out issue))
        {
            return false;
        }

        HashSet<string> usedInstanceIds = new HashSet<string>();
        if (!TryRestoreGrid(saveFile.progress.stashGrid, usedInstanceIds, out GridInventory stashGrid, out issue))
        {
            issue = "Invalid stash grid: " + issue;
            return false;
        }

        if (!TryRestorePlayer(saveFile.progress.player, saveFile.schemaVersion, usedInstanceIds, out PlayerRuntimeState playerRuntimeState, out issue))
        {
            return false;
        }

        if (!TryValidateRunWorld(saveFile.progress.runWorld, out issue))
        {
            return false;
        }

        restoredProgress = new RestoredProgress(saveFile.progress.credits, saveFile.progress.ammoStock, stashGrid, playerRuntimeState);
        return true;
    }

    private bool TryValidateSceneData(GameProgressSaveFile saveFile, out string issue)
    {
        GameProgressData progress = saveFile.progress;
        if (saveFile.schemaVersion == 1)
        {
            progress.scene = BuildFallbackBaseSceneData();
            progress.playerScene ??= new PlayerSceneSaveData();
            progress.runWorld = null;
            issue = null;
            return true;
        }

        if (progress.scene == null)
        {
            issue = "Scene progress data is missing.";
            return false;
        }

        if (!Enum.TryParse(progress.scene.sceneKind, out GameSceneKind sceneKind) || !Enum.IsDefined(typeof(GameSceneKind), sceneKind))
        {
            issue = $"Unknown scene kind: {progress.scene.sceneKind}.";
            return false;
        }

        if (sceneKind == GameSceneKind.Title)
        {
            issue = "Saved gameplay state cannot target Title.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(progress.scene.mapId) || string.IsNullOrWhiteSpace(progress.scene.sceneName))
        {
            issue = "Saved scene mapId or sceneName is missing.";
            return false;
        }

        if (sceneMapRegistry != null)
        {
            List<string> registryIssues = sceneMapRegistry.ValidateRegistry();
            if (registryIssues.Count > 0)
            {
                issue = string.Join("\n", registryIssues);
                return false;
            }

            if (!sceneMapRegistry.TryGetById(progress.scene.mapId, out SceneMapRegistry.SceneMapEntry map))
            {
                issue = $"Unknown scene mapId: {progress.scene.mapId}.";
                return false;
            }

            if (map.SceneName != progress.scene.sceneName || map.SceneKind != sceneKind)
            {
                issue = $"Scene map mismatch for {progress.scene.mapId}.";
                return false;
            }
        }

        progress.playerScene ??= new PlayerSceneSaveData();
        if (!IsFinite(progress.playerScene.positionX)
            || !IsFinite(progress.playerScene.positionY)
            || !IsFinite(progress.playerScene.positionZ)
            || !IsFinite(progress.playerScene.yaw)
            || progress.playerScene.currentHealth < 0f)
        {
            issue = "Saved player transform or health is invalid.";
            return false;
        }

        if (sceneKind == GameSceneKind.Base)
        {
            progress.runWorld = null;
        }

        issue = null;
        return true;
    }

    private SceneProgressSaveData BuildFallbackBaseSceneData()
    {
        if (sceneMapRegistry != null && sceneMapRegistry.TryGetFirstByKind(GameSceneKind.Base, out SceneMapRegistry.SceneMapEntry map))
        {
            return new SceneProgressSaveData
            {
                mapId = map.MapId,
                sceneName = map.SceneName,
                displayName = map.DisplayName,
                sceneKind = GameSceneKind.Base.ToString()
            };
        }

        return new SceneProgressSaveData
        {
            mapId = "base",
            sceneName = "BaseScene",
            displayName = "Base",
            sceneKind = GameSceneKind.Base.ToString()
        };
    }

    private bool TryRestorePlayer(PlayerRuntimeSaveData saveData, int schemaVersion, HashSet<string> usedInstanceIds, out PlayerRuntimeState playerRuntimeState, out string issue)
    {
        playerRuntimeState = null;
        if (saveData == null)
        {
            issue = "Player runtime data is missing.";
            return false;
        }

        PlayerRuntimeState candidate = new PlayerRuntimeState();
        List<GridInventory> restoredGrids = candidate.InventoryGrids;
        List<EquipmentSlot> restoredSlots = candidate.EquipmentSlots;

        if (saveData.inventoryGrids == null || saveData.inventoryGrids.Count == 0)
        {
            issue = "Player inventory must contain at least the default bag grid.";
            return false;
        }

        foreach (GridInventorySaveData gridData in saveData.inventoryGrids)
        {
            if (!TryRestoreGrid(gridData, usedInstanceIds, out GridInventory grid, out issue))
            {
                issue = "Invalid player inventory grid: " + issue;
                return false;
            }

            restoredGrids.Add(grid);
        }

        if (saveData.equipmentSlots == null || saveData.equipmentSlots.Count == 0)
        {
            issue = "Equipment slots are missing.";
            return false;
        }

        foreach (EquipmentSlotSaveData slotData in saveData.equipmentSlots)
        {
            if (!TryRestoreSlot(slotData, schemaVersion, usedInstanceIds, out EquipmentSlot slot, out issue))
            {
                return false;
            }

            restoredSlots.Add(slot);
        }

        if (!TryRestoreEquippedBagGridLinks(saveData.equippedBagGrids, restoredSlots, restoredGrids, candidate.EquippedBagGrids, out issue))
        {
            return false;
        }

        playerRuntimeState = candidate;
        return true;
    }

    private bool TryRestoreGrid(GridInventorySaveData saveData, HashSet<string> usedInstanceIds, out GridInventory grid, out string issue)
    {
        grid = null;

        if (saveData == null)
        {
            issue = "Grid data is missing.";
            return false;
        }

        if (saveData.width <= 0 || saveData.height <= 0)
        {
            issue = $"Grid size is invalid: {saveData.width}x{saveData.height}.";
            return false;
        }

        GridInventory candidate = new GridInventory(saveData.displayName, saveData.width, saveData.height);
        if (saveData.placements == null)
        {
            grid = candidate;
            issue = null;
            return true;
        }

        foreach (GridItemPlacementSaveData placementData in saveData.placements)
        {
            if (placementData == null)
            {
                issue = "Grid contains an empty placement.";
                return false;
            }

            if (!TryRestoreItem(placementData.item, usedInstanceIds, out RuntimeItemInstance item, out issue))
            {
                issue = $"Grid '{saveData.displayName}' placement ({placementData.x}, {placementData.y}) failed during save validation: {issue}";
                return false;
            }

            if (!candidate.TryPlace(item, placementData.x, placementData.y))
            {
                issue = $"{item.Definition.DisplayName} cannot be placed at {placementData.x}, {placementData.y}.";
                return false;
            }
        }

        grid = candidate;
        issue = null;
        return true;
    }

    private bool TryRestoreSlot(EquipmentSlotSaveData saveData, int schemaVersion, HashSet<string> usedInstanceIds, out EquipmentSlot slot, out string issue)
    {
        slot = null;
        if (saveData == null)
        {
            issue = "Equipment slot data is missing.";
            return false;
        }

        if (!Enum.TryParse(saveData.slotType, out EquipmentSlotType slotType) || !Enum.IsDefined(typeof(EquipmentSlotType), slotType))
        {
            issue = $"Unknown equipment slot type: {saveData.slotType}.";
            return false;
        }

        if (!Enum.TryParse(saveData.state, out EquipmentSlotState slotState) || !Enum.IsDefined(typeof(EquipmentSlotState), slotState))
        {
            issue = $"Unknown equipment slot state: {saveData.state}.";
            return false;
        }

        EquipmentSlot candidate = new EquipmentSlot(slotType, saveData.displayName, slotState);
        bool hasItem = saveData.equippedItem != null;
        if (schemaVersion >= 3)
        {
            if (saveData.occupancy != "Empty" && saveData.occupancy != "Equipped")
            {
                issue = $"Equipment slot '{saveData.displayName}' has missing or invalid occupancy.";
                return false;
            }

            hasItem = saveData.occupancy == "Equipped";
            // JsonUtility may materialize a null inline class as an all-default object.
            // Only an explicitly empty slot may contain that placeholder.
            RuntimeItemSaveData payload = saveData.equippedItem;
            if (!hasItem && payload != null
                && (!string.IsNullOrEmpty(payload.instanceId) || !string.IsNullOrEmpty(payload.itemId)
                    || payload.quantity != 0 || payload.upgradeLevel != 0 || payload.currentMagazineAmmo != 0))
            {
                issue = $"Equipment slot '{saveData.displayName}' is Empty but contains item data.";
                return false;
            }
        }

        if (hasItem)
        {
            if (!TryRestoreItem(saveData.equippedItem, usedInstanceIds, out RuntimeItemInstance item, out issue))
            {
                issue = $"Equipment slot '{saveData.displayName}' ({saveData.slotType}) failed during save validation: {issue}";
                return false;
            }

            if (candidate.IsLocked || candidate.IsFixed || !candidate.CanEquip(item.Definition))
            {
                issue = $"{item.Definition.DisplayName} is not compatible with {candidate.DisplayName}.";
                return false;
            }

            candidate.Restore(item);
        }

        slot = candidate;
        issue = null;
        return true;
    }

    private bool TryRestoreItem(RuntimeItemSaveData saveData, HashSet<string> usedInstanceIds, out RuntimeItemInstance item, out string issue)
    {
        item = null;
        if (saveData == null)
        {
            issue = "Item data is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(saveData.instanceId))
        {
            issue = $"Item instanceId is missing. itemId='{saveData.itemId}', quantity={saveData.quantity}, magazine={saveData.currentMagazineAmmo}.";
            return false;
        }

        if (!usedInstanceIds.Add(saveData.instanceId))
        {
            issue = $"Duplicate item instanceId: {saveData.instanceId}.";
            return false;
        }

        if (!itemRegistry.TryGetDefinition(saveData.itemId, out ItemDefinition definition))
        {
            issue = $"Unknown itemId: {saveData.itemId}.";
            return false;
        }

        if (saveData.quantity <= 0 || saveData.quantity > definition.MaxStack)
        {
            issue = $"{definition.DisplayName} has invalid quantity {saveData.quantity}.";
            return false;
        }

        if (saveData.upgradeLevel < 0 || saveData.upgradeLevel > definition.MaxUpgradeLevel)
        {
            issue = $"{definition.DisplayName} has invalid upgrade level {saveData.upgradeLevel}.";
            return false;
        }

        if (definition.Category == ItemCategory.Weapon)
        {
            if (saveData.currentMagazineAmmo < 0 || saveData.currentMagazineAmmo > definition.WeaponMagazineSize)
            {
                issue = $"{definition.DisplayName} has invalid magazine ammo {saveData.currentMagazineAmmo}.";
                return false;
            }
        }
        else if (saveData.currentMagazineAmmo != 0)
        {
            issue = $"{definition.DisplayName} is not a weapon but has magazine ammo.";
            return false;
        }

        item = new RuntimeItemInstance(definition, saveData.quantity, saveData.upgradeLevel, saveData.instanceId);
        item.SetCurrentMagazineAmmo(saveData.currentMagazineAmmo, definition.WeaponMagazineSize);
        issue = null;
        return true;
    }

    private bool TryValidateRunWorld(RunWorldSaveData runWorld, out string issue)
    {
        issue = null;
        if (runWorld == null)
        {
            return true;
        }

        HashSet<string> enemyIds = new HashSet<string>();
        if (runWorld.enemies != null)
        {
            foreach (EnemySaveData enemy in runWorld.enemies)
            {
                if (enemy == null)
                {
                    issue = "Run enemy entry is empty.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(enemy.sceneObjectId) || !enemyIds.Add(enemy.sceneObjectId))
                {
                    issue = $"Run enemy sceneObjectId is missing or duplicated: {enemy.sceneObjectId}.";
                    return false;
                }

                if (!IsFinite(enemy.currentHealth) || enemy.currentHealth <= 0f || !IsFinite(enemy.positionX) || !IsFinite(enemy.positionY) || !IsFinite(enemy.positionZ) || !IsFinite(enemy.yaw))
                {
                    issue = $"Run enemy '{enemy.sceneObjectId}' has invalid health or position.";
                    return false;
                }
            }
        }

        HashSet<string> pickupIds = new HashSet<string>();
        if (runWorld.pickups != null)
        {
            foreach (PickupSaveData pickup in runWorld.pickups)
            {
                if (pickup == null)
                {
                    issue = "Run pickup entry is empty.";
                    return false;
                }

                string pickupId = !string.IsNullOrWhiteSpace(pickup.sceneObjectId)
                    ? "scene:" + pickup.sceneObjectId
                    : "runtime:" + pickup.runtimeSpawnId;
                if (string.IsNullOrWhiteSpace(pickup.sceneObjectId) && string.IsNullOrWhiteSpace(pickup.runtimeSpawnId))
                {
                    issue = $"Run pickup for itemId '{pickup.itemId}' has no scene or runtime id.";
                    return false;
                }

                if (!pickupIds.Add(pickupId))
                {
                    issue = $"Duplicate run pickup id: {pickupId}.";
                    return false;
                }

                if (!itemRegistry.TryGetDefinition(pickup.itemId, out ItemDefinition definition))
                {
                    issue = $"Run pickup has unknown itemId: {pickup.itemId}.";
                    return false;
                }

                if (pickup.amount <= 0 || pickup.amount > definition.MaxStack)
                {
                    issue = $"{definition.DisplayName} run pickup has invalid amount {pickup.amount}.";
                    return false;
                }

                if (pickup.pickupType != "Item" && pickup.pickupType != "Ammo")
                {
                    issue = $"Run pickup type is invalid: {pickup.pickupType}.";
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryRestoreEquippedBagGridLinks(
        List<EquippedBagGridSaveData> links,
        List<EquipmentSlot> slots,
        List<GridInventory> grids,
        Dictionary<EquipmentSlot, GridInventory> target,
        out string issue)
    {
        HashSet<int> linkedSlots = new HashSet<int>();
        HashSet<int> linkedGrids = new HashSet<int>();

        if (links != null)
        {
            foreach (EquippedBagGridSaveData link in links)
            {
                if (link == null)
                {
                    issue = "Equipped bag grid link is empty.";
                    return false;
                }

                if (link.slotIndex < 0 || link.slotIndex >= slots.Count)
                {
                    issue = $"Equipped bag slot index is out of range: {link.slotIndex}.";
                    return false;
                }

                if (link.gridIndex <= 0 || link.gridIndex >= grids.Count)
                {
                    issue = $"Equipped bag grid index is out of range: {link.gridIndex}.";
                    return false;
                }

                if (!linkedSlots.Add(link.slotIndex))
                {
                    issue = $"Duplicate equipped bag slot link: {link.slotIndex}.";
                    return false;
                }

                if (!linkedGrids.Add(link.gridIndex))
                {
                    issue = $"Duplicate equipped bag grid link: {link.gridIndex}.";
                    return false;
                }

                EquipmentSlot slot = slots[link.slotIndex];
                GridInventory grid = grids[link.gridIndex];
                RuntimeItemInstance bagItem = slot.EquippedItem;

                if (slot.SlotType != EquipmentSlotType.Bag || bagItem?.Definition == null || bagItem.Definition.Category != ItemCategory.Bag)
                {
                    issue = $"Slot {link.slotIndex} is not an equipped bag.";
                    return false;
                }

                if (grid.Width != bagItem.Definition.BagGridWidth || grid.Height != bagItem.Definition.BagGridHeight)
                {
                    issue = $"{bagItem.Definition.DisplayName} grid size does not match its definition.";
                    return false;
                }

                target[slot] = grid;
            }
        }

        for (int i = 1; i < grids.Count; i++)
        {
            if (!linkedGrids.Contains(i))
            {
                issue = $"Inventory grid {i} is not linked to an equipped bag.";
                return false;
            }
        }

        issue = null;
        return true;
    }

    private bool ValidateRegistry(out string issue)
    {
        if (itemRegistry == null)
        {
            issue = "ItemDefinitionRegistry is null.";
            return false;
        }

        List<string> registryIssues = itemRegistry.ValidateRegistry();
        if (registryIssues.Count > 0)
        {
            issue = string.Join("\n", registryIssues);
            return false;
        }

        issue = null;
        return true;
    }

    private void ReplaceSaveFile(string tempPath)
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("No existing save file found. Creating the first save file.");
            File.Move(tempPath, SavePath);
            return;
        }

        try
        {
            Debug.Log("Existing save file found. Replacing it while preserving backup.");
            File.Replace(tempPath, SavePath, BackupPath, true);
        }
        catch (PlatformNotSupportedException)
        {
            CopyReplaceSaveFile(tempPath);
        }
        catch (IOException)
        {
            CopyReplaceSaveFile(tempPath);
        }
    }

    private void CopyReplaceSaveFile(string tempPath)
    {
        if (File.Exists(SavePath))
        {
            File.Copy(SavePath, BackupPath, true);
        }

        File.Copy(tempPath, SavePath, true);
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not delete temporary save file: {exception}");
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static int CountItems(GridInventorySaveData grid)
    {
        if (grid?.placements == null)
        {
            return 0;
        }

        int count = 0;
        foreach (GridItemPlacementSaveData placement in grid.placements)
        {
            if (placement?.item != null)
            {
                count += Math.Max(0, placement.item.quantity);
            }
        }

        return count;
    }

    private static int CountItems(List<GridInventorySaveData> grids)
    {
        if (grids == null)
        {
            return 0;
        }

        int count = 0;
        foreach (GridInventorySaveData grid in grids)
        {
            count += CountItems(grid);
        }

        return count;
    }

    private sealed class RestoredProgress
    {
        public RestoredProgress(int credits, int ammoStock, GridInventory stashGrid, PlayerRuntimeState playerRuntimeState)
        {
            Credits = credits;
            AmmoStock = ammoStock;
            StashGrid = stashGrid;
            PlayerRuntimeState = playerRuntimeState;
        }

        public int Credits { get; }
        public int AmmoStock { get; }
        public GridInventory StashGrid { get; }
        public PlayerRuntimeState PlayerRuntimeState { get; }
    }
}
