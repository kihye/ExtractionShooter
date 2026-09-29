using UnityEngine;

public sealed class GameplayUiModeController : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;

    private GameplayUiMode currentMode = GameplayUiMode.Gameplay;
    private PlayerMovement playerMovement;
    private PlayerAim playerAim;
    private PlayerWeapon playerWeapon;
    private PlayerInteractor playerInteractor;
    private bool wasMovementEnabled;
    private bool wasAimEnabled;
    private bool wasWeaponEnabled;
    private bool wasInteractorEnabled;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    public GameplayUiMode CurrentMode => currentMode;
    public bool IsGameplay => currentMode == GameplayUiMode.Gameplay;

    public bool CanOpen(GameplayUiMode mode)
    {
        return currentMode == GameplayUiMode.Gameplay || currentMode == mode;
    }

    public bool TryEnterMode(GameplayUiMode mode, GameObject playerObject)
    {
        if (mode == GameplayUiMode.Gameplay || !CanOpen(mode))
        {
            return false;
        }

        if (currentMode == mode)
        {
            return true;
        }

        ResolvePlayer(playerObject);
        currentMode = mode;

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        wasMovementEnabled = playerMovement != null && playerMovement.enabled;
        wasAimEnabled = playerAim != null && playerAim.enabled;
        wasWeaponEnabled = playerWeapon != null && playerWeapon.enabled;
        wasInteractorEnabled = playerInteractor != null && playerInteractor.enabled;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerAim != null)
        {
            playerAim.enabled = false;
        }

        if (playerWeapon != null)
        {
            playerWeapon.enabled = false;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled = false;
        }

        return true;
    }

    public void ExitMode(GameplayUiMode mode)
    {
        if (currentMode != mode)
        {
            return;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = wasMovementEnabled;
        }

        if (playerAim != null)
        {
            playerAim.enabled = wasAimEnabled;
        }

        if (playerWeapon != null)
        {
            playerWeapon.enabled = wasWeaponEnabled;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled = wasInteractorEnabled;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        currentMode = GameplayUiMode.Gameplay;
    }

    private void ResolvePlayer(GameObject playerObject)
    {
        if (playerObject != null)
        {
            playerInventory = playerObject.GetComponent<PlayerInventory>();
        }

        playerInventory ??= FindFirstObjectByType<PlayerInventory>();

        if (playerInventory == null)
        {
            return;
        }

        playerMovement = playerInventory.GetComponent<PlayerMovement>();
        playerAim = playerInventory.GetComponent<PlayerAim>();
        playerWeapon = playerInventory.GetComponent<PlayerWeapon>();
        playerInteractor = playerInventory.GetComponent<PlayerInteractor>();
    }
}
