using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInteractor : MonoBehaviour
{
    public event Action<IInteractable> CurrentInteractableChanged;
    public event Action<IPlayerPickup> CurrentPickupChanged;

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField, Min(0.1f)] private float interactionRange = 1.75f;
    [SerializeField] private LayerMask pickupMask = ~0;

    private readonly Collider[] overlapResults = new Collider[16];
    private InputAction interactAction;
    private IInteractable currentInteractable;

    public IInteractable CurrentInteractable => currentInteractable;
    public IPlayerPickup CurrentNearbyPickup => currentInteractable as IPlayerPickup;

    private void Awake()
    {
        interactAction = FindAction(interactActionName);
    }

    private void OnEnable()
    {
        interactAction?.Enable();
    }

    private void OnDisable()
    {
        interactAction?.Disable();
    }

    private void Update()
    {
        IInteractable nearestInteractable = FindNearestInteractable();
        if (nearestInteractable != currentInteractable)
        {
            currentInteractable = nearestInteractable;
            CurrentInteractableChanged?.Invoke(currentInteractable);
            CurrentPickupChanged?.Invoke(currentInteractable as IPlayerPickup);
            if (currentInteractable != null)
            {
                Debug.Log($"Nearby interaction: {currentInteractable.PromptText}", this);
            }
        }

        if (currentInteractable != null && interactAction != null && interactAction.WasPressedThisFrame())
        {
            currentInteractable.Interact(gameObject);
            currentInteractable = null;
            CurrentInteractableChanged?.Invoke(currentInteractable);
            CurrentPickupChanged?.Invoke(null);
        }
    }

    private IInteractable FindNearestInteractable()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            interactionRange,
            overlapResults,
            pickupMask,
            QueryTriggerInteraction.Collide);

        IInteractable nearestInteractable = null;
        float nearestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            IInteractable interactable = GetInteractable(overlapResults[i]);
            if (interactable == null || !interactable.CanInteract(gameObject))
            {
                continue;
            }

            float distanceSqr = (interactable.Transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestInteractable = interactable;
            }
        }

        return nearestInteractable;
    }

    private static IInteractable GetInteractable(Collider source)
    {
        IInteractable interactable = source.GetComponentInParent<IInteractable>();
        if (interactable != null)
        {
            return interactable;
        }

        ItemPickup itemPickup = source.GetComponentInParent<ItemPickup>();
        if (itemPickup != null && itemPickup.Definition != null)
        {
            return itemPickup;
        }

        AmmoPickup ammoPickup = source.GetComponentInParent<AmmoPickup>();
        return ammoPickup;
    }

    private InputAction FindAction(string actionName)
    {
        if (inputActions == null)
        {
            return new InputAction(actionName, InputActionType.Button, "<Keyboard>/e");
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, true);
        return actionMap.FindAction(actionName, true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
