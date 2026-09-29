using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class PlayerRuntimeStateBinder : MonoBehaviour
{
    [SerializeField] private GameSessionState sessionState;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerEquipment playerEquipment;
    private PlayerRuntimeState boundState;

    private void Awake()
    {
        Bind();
    }

    public void Bind()
    {
        if (playerInventory == null)
        {
            playerInventory = GetComponent<PlayerInventory>();
        }

        if (playerEquipment == null)
        {
            playerEquipment = GetComponent<PlayerEquipment>();
        }

        if (sessionState == null)
        {
            StashInventory stashInventory = FindFirstObjectByType<StashInventory>();
            if (stashInventory != null)
            {
                sessionState = stashInventory.SessionState;
            }
        }

        if (sessionState == null || playerInventory == null)
        {
            return;
        }

        PlayerRuntimeState runtimeState = sessionState.PlayerRuntimeState;
        if (ReferenceEquals(boundState, runtimeState))
        {
            return;
        }

        runtimeState.InitializeIfNeeded(playerInventory.DefaultBagWidth, playerInventory.DefaultBagHeight);
        GetComponent<PlayerWeapon>()?.BindSessionInventory(playerInventory);
        playerInventory.BindRuntimeGrids(runtimeState.InventoryGrids);
        playerEquipment?.BindRuntimeState(runtimeState);
        boundState = runtimeState;
    }
}
