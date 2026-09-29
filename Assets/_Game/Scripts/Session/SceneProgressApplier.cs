using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneProgressApplier
{
    public static void ApplyPendingRestoreForCurrentScene(
        GameSessionState sessionState,
        SceneMapRegistry sceneMapRegistry,
        ItemDefinitionRegistry itemRegistry,
        RunWorldPrefabRegistry runWorldPrefabRegistry)
    {
        if (sessionState == null || !sessionState.HasPendingSceneRestore)
        {
            return;
        }

        if (sceneMapRegistry != null && sceneMapRegistry.TryGetById(sessionState.CurrentMapId, out SceneMapRegistry.SceneMapEntry map))
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != map.SceneName)
            {
                return;
            }
        }

        if (Object.FindFirstObjectByType<PlayerInventory>() == null)
        {
            return;
        }

        if (sessionState.CurrentSceneKind == GameSceneKind.Run && sessionState.HasPendingSceneRestore)
        {
            if (itemRegistry == null || runWorldPrefabRegistry == null)
            {
                Debug.LogWarning("Run restore is waiting for ItemDefinitionRegistry and RunWorldPrefabRegistry references.");
                return;
            }
        }

        if (!sessionState.TryGetPendingSceneRestore(out PlayerSceneSaveData playerScene, out RunWorldSaveData runWorld))
        {
            return;
        }

        if (!ApplyPlayerScene(playerScene, sceneMapRegistry, sessionState.CurrentMapId))
        {
            return;
        }

        if (sessionState.CurrentSceneKind == GameSceneKind.Run && runWorld != null)
        {
            ApplyRunWorld(runWorld, itemRegistry, runWorldPrefabRegistry);
        }

        sessionState.ConsumePendingSceneRestore(out _, out _);
        Debug.Log("Scene restore completed: player, inventory/equipment bindings and world state are ready.");
    }

    private static bool ApplyPlayerScene(PlayerSceneSaveData playerScene, SceneMapRegistry sceneMapRegistry, string mapId)
    {
        PlayerInventory player = Object.FindFirstObjectByType<PlayerInventory>();
        if (player == null)
        {
            return false;
        }

        Vector3 position = playerScene != null
            ? new Vector3(playerScene.positionX, playerScene.positionY, playerScene.positionZ)
            : GetFallbackPosition(sceneMapRegistry, mapId);
        Quaternion rotation = playerScene != null
            ? Quaternion.Euler(0f, playerScene.yaw, 0f)
            : GetFallbackRotation(sceneMapRegistry, mapId);

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        player.transform.SetPositionAndRotation(position, rotation);

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null && playerScene != null && playerScene.currentHealth > 0f)
        {
            health.RestoreHealth(playerScene.currentHealth);
        }

        return true;
    }

    private static Vector3 GetFallbackPosition(SceneMapRegistry sceneMapRegistry, string mapId)
    {
        return sceneMapRegistry != null && sceneMapRegistry.TryGetById(mapId, out SceneMapRegistry.SceneMapEntry map)
            ? map.FallbackSpawnPosition
            : Vector3.zero;
    }

    private static Quaternion GetFallbackRotation(SceneMapRegistry sceneMapRegistry, string mapId)
    {
        return sceneMapRegistry != null && sceneMapRegistry.TryGetById(mapId, out SceneMapRegistry.SceneMapEntry map)
            ? Quaternion.Euler(0f, map.FallbackSpawnYaw, 0f)
            : Quaternion.identity;
    }

    private static void ApplyRunWorld(
        RunWorldSaveData runWorld,
        ItemDefinitionRegistry itemRegistry,
        RunWorldPrefabRegistry runWorldPrefabRegistry)
    {
        Dictionary<string, EnemySaveData> savedEnemies = new Dictionary<string, EnemySaveData>();
        if (runWorld.enemies != null)
        {
            foreach (EnemySaveData enemy in runWorld.enemies)
            {
                if (enemy != null && !string.IsNullOrWhiteSpace(enemy.sceneObjectId))
                {
                    savedEnemies[enemy.sceneObjectId] = enemy;
                }
            }
        }

        DummyEnemy[] sceneEnemies = Object.FindObjectsByType<DummyEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        HashSet<string> availableIds = new HashSet<string>();
        foreach (DummyEnemy enemy in sceneEnemies)
        {
            ScenePersistentId id = enemy.GetComponent<ScenePersistentId>();
            if (id == null || string.IsNullOrWhiteSpace(id.PersistentId) || !availableIds.Add(id.PersistentId))
            {
                throw new System.InvalidOperationException($"Enemy '{enemy.name}' has a missing or duplicate scene ID.");
            }
        }
        foreach (string id in savedEnemies.Keys)
        {
            if (!availableIds.Contains(id))
            {
                throw new System.InvalidOperationException($"Saved enemy '{id}' is missing from the loaded scene.");
            }
        }

        List<EnemyAI> resumeAI = new List<EnemyAI>();
        foreach (DummyEnemy enemy in sceneEnemies)
        {
            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null && ai.enabled)
            {
                resumeAI.Add(ai);
                ai.enabled = false;
            }
        }

        foreach (DummyEnemy enemy in sceneEnemies)
        {
            ScenePersistentId sceneId = enemy.GetComponent<ScenePersistentId>();
            if (sceneId == null || string.IsNullOrWhiteSpace(sceneId.PersistentId))
            {
                continue;
            }

            if (!savedEnemies.TryGetValue(sceneId.PersistentId, out EnemySaveData saveData))
            {
                enemy.gameObject.SetActive(false);
                Object.Destroy(enemy.gameObject);
                continue;
            }

            enemy.gameObject.SetActive(true);
            Vector3 position = new Vector3(saveData.positionX, saveData.positionY, saveData.positionZ);
            Quaternion rotation = Quaternion.Euler(0f, saveData.yaw, 0f);
            enemy.transform.SetPositionAndRotation(position, rotation);
            Rigidbody body = enemy.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            enemy.RestoreHealth(saveData.currentHealth);
            enemy.GetComponent<EnemyAI>()?.ResetAfterRestore();
            Debug.Log($"Restored enemy '{sceneId.PersistentId}' before AI resume: saved={position.ToString("F4")}, applied={(body != null ? body.position : enemy.transform.position).ToString("F4")}, HP={enemy.CurrentHealth}.", enemy);
        }

        Dictionary<string, PickupSaveData> savedScenePickups = new Dictionary<string, PickupSaveData>();
        HashSet<string> runtimePickupIds = new HashSet<string>();
        if (runWorld.pickups != null)
        {
            foreach (PickupSaveData pickup in runWorld.pickups)
            {
                if (pickup == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(pickup.sceneObjectId))
                {
                    savedScenePickups[pickup.sceneObjectId] = pickup;
                }
                else if (!string.IsNullOrWhiteSpace(pickup.runtimeSpawnId))
                {
                    runtimePickupIds.Add(pickup.runtimeSpawnId);
                }
            }
        }

        RestoreAuthoredPickups(savedScenePickups, itemRegistry);
        DestroyExistingRuntimePickups();
        RestoreRuntimePickups(runWorld, runtimePickupIds, itemRegistry, runWorldPrefabRegistry);
        Physics.SyncTransforms();
        foreach (EnemyAI ai in resumeAI)
        {
            if (ai != null && ai.gameObject.activeInHierarchy)
            {
                ai.enabled = true;
            }
        }
    }

    private static void RestoreAuthoredPickups(Dictionary<string, PickupSaveData> savedScenePickups, ItemDefinitionRegistry itemRegistry)
    {
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour is not IPlayerPickup)
            {
                continue;
            }

            Component component = behaviour;
            ScenePersistentId sceneId = component.GetComponent<ScenePersistentId>();
            if (sceneId == null || string.IsNullOrWhiteSpace(sceneId.PersistentId))
            {
                continue;
            }

            if (!savedScenePickups.TryGetValue(sceneId.PersistentId, out PickupSaveData saveData))
            {
                component.gameObject.SetActive(false);
                Object.Destroy(component.gameObject);
                continue;
            }

            RestorePickupComponent(component.gameObject, saveData, itemRegistry);
        }
    }

    private static void DestroyExistingRuntimePickups()
    {
        foreach (RuntimeSpawnId runtimeId in Object.FindObjectsByType<RuntimeSpawnId>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (runtimeId.GetComponent<IPlayerPickup>() != null)
            {
                runtimeId.gameObject.SetActive(false);
                Object.Destroy(runtimeId.gameObject);
            }
        }
    }

    private static void RestoreRuntimePickups(
        RunWorldSaveData runWorld,
        HashSet<string> runtimePickupIds,
        ItemDefinitionRegistry itemRegistry,
        RunWorldPrefabRegistry runWorldPrefabRegistry)
    {
        if (runWorld.pickups == null || runtimePickupIds.Count == 0 || runWorldPrefabRegistry == null)
        {
            return;
        }

        foreach (PickupSaveData pickup in runWorld.pickups)
        {
            if (pickup == null || !runtimePickupIds.Contains(pickup.runtimeSpawnId))
            {
                continue;
            }

            if (!itemRegistry.TryGetDefinition(pickup.itemId, out _))
            {
                continue;
            }

            GameObject instance = null;
            if (pickup.pickupType == "Ammo" && runWorldPrefabRegistry.AmmoPickupPrefab != null)
            {
                instance = Object.Instantiate(runWorldPrefabRegistry.AmmoPickupPrefab).gameObject;
            }
            else if (pickup.pickupType == "Item" && runWorldPrefabRegistry.ItemPickupPrefab != null)
            {
                instance = Object.Instantiate(runWorldPrefabRegistry.ItemPickupPrefab).gameObject;
            }

            if (instance == null)
            {
                continue;
            }

            RuntimeSpawnId runtimeId = instance.GetComponent<RuntimeSpawnId>() ?? instance.AddComponent<RuntimeSpawnId>();
            runtimeId.RestoreId(pickup.runtimeSpawnId);
            RestorePickupComponent(instance, pickup, itemRegistry);
        }
    }

    private static void RestorePickupComponent(GameObject gameObject, PickupSaveData saveData, ItemDefinitionRegistry itemRegistry)
    {
        if (gameObject == null || saveData == null || itemRegistry == null || !itemRegistry.TryGetDefinition(saveData.itemId, out ItemDefinition definition))
        {
            return;
        }

        gameObject.SetActive(true);
        gameObject.transform.SetPositionAndRotation(
            new Vector3(saveData.positionX, saveData.positionY, saveData.positionZ),
            Quaternion.Euler(0f, saveData.yaw, 0f));

        if (saveData.pickupType == "Ammo")
        {
            AmmoPickup ammoPickup = gameObject.GetComponent<AmmoPickup>();
            ammoPickup?.Configure(definition, saveData.amount);
            return;
        }

        ItemPickup itemPickup = gameObject.GetComponent<ItemPickup>();
        itemPickup?.Configure(definition, saveData.amount);
    }
}
