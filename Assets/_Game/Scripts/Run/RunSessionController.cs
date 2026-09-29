using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RunSessionController : MonoBehaviour
{
    public enum RunState
    {
        Running,
        Extracted,
        Failed
    }

    public event Action<RunState, IReadOnlyList<InventoryItemStack>> RunEnded;

    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private StashInventory stashInventory;
    [SerializeField] private GameSessionState sessionState;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAim playerAim;
    [SerializeField] private PlayerWeapon playerWeapon;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private Rigidbody playerBody;

    [SerializeField] private RunState state = RunState.Running;

    private int lastRunAmmoRecovered;
    private int lastRunAmmoLost;

    public bool IsRunning => state == RunState.Running;
    public RunState State => state;
    public int LastRunAmmoRecovered => lastRunAmmoRecovered;
    public int LastRunAmmoLost => lastRunAmmoLost;

    private void Awake()
    {
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        if (stashInventory == null)
        {
            stashInventory = FindFirstObjectByType<StashInventory>();
        }

        if (sessionState == null && stashInventory != null)
        {
            sessionState = stashInventory.SessionState;
        }

        if (playerInventory != null)
        {
            playerMovement ??= playerInventory.GetComponent<PlayerMovement>();
            playerAim ??= playerInventory.GetComponent<PlayerAim>();
            playerWeapon ??= playerInventory.GetComponent<PlayerWeapon>();
            playerInteractor ??= playerInventory.GetComponent<PlayerInteractor>();
            playerBody ??= playerInventory.GetComponent<Rigidbody>();
        }
    }

    public void CompleteExtraction()
    {
        if (!IsRunning)
        {
            return;
        }

        if (playerInventory == null || stashInventory == null)
        {
            Debug.LogWarning("Extraction failed to complete because inventory references are missing.", this);
            return;
        }

        int reserveAmmoInInventory = playerWeapon != null ? playerWeapon.ReserveAmmo : 0;
        List<InventoryItemStack> transferred = playerInventory.TransferAllTo(stashInventory);
        lastRunAmmoRecovered = reserveAmmoInInventory;
        lastRunAmmoLost = 0;

        if (sessionState != null)
        {
            sessionState.AddAmmo(lastRunAmmoRecovered);
        }

        state = RunState.Extracted;
        StopPlayerControls();

        Debug.Log("Extraction successful.\n\nTransferred:\n" + InventoryLogFormatter.Format(transferred), this);
        Debug.Log($"Ammo recovered: {lastRunAmmoRecovered}", this);
        stashInventory.LogContents();
        RunEnded?.Invoke(state, transferred);
    }

    public void FailRun()
    {
        if (!IsRunning)
        {
            return;
        }

        List<InventoryItemStack> lostItems = playerInventory != null
            ? playerInventory.GetEntries()
            : new List<InventoryItemStack>();

        lastRunAmmoRecovered = 0;
        lastRunAmmoLost = playerWeapon != null ? playerWeapon.ReserveAmmo : 0;

        playerInventory?.Clear();
        state = RunState.Failed;
        StopPlayerControls();

        Debug.Log("Run failed.\n\nLost:\n" + InventoryLogFormatter.Format(lostItems), this);
        Debug.Log($"Ammo lost: {lastRunAmmoLost}", this);

        if (stashInventory != null)
        {
            stashInventory.LogContents();
        }

        RunEnded?.Invoke(state, lostItems);
    }

    private void StopPlayerControls()
    {
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

        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
            playerBody.isKinematic = true;
        }
    }
}
