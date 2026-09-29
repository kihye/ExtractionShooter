using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerRuntimeStateAutoBinder
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BindInitialScene()
    {
        BindScenePlayers();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindScenePlayers();
    }

    private static void BindScenePlayers()
    {
        PlayerInventory[] inventories = Object.FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
        foreach (PlayerInventory inventory in inventories)
        {
            if (inventory == null)
            {
                continue;
            }

            PlayerRuntimeStateBinder binder = inventory.GetComponent<PlayerRuntimeStateBinder>();
            if (binder == null)
            {
                binder = inventory.gameObject.AddComponent<PlayerRuntimeStateBinder>();
            }

            binder.Bind();
        }
    }
}
