using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerAim : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string aimActionName = "Aim";
    [SerializeField] private Camera aimCamera;

    private InputAction aimAction;
    private Vector2 screenPosition;

    private void Awake()
    {
        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        aimAction = FindAction(aimActionName);
    }

    private void OnEnable()
    {
        aimAction?.Enable();
    }

    private void OnDisable()
    {
        aimAction?.Disable();
    }

    private void Update()
    {
        if (aimAction == null || aimCamera == null)
        {
            return;
        }

        screenPosition = aimAction.ReadValue<Vector2>();
        RotateTowardScreenPosition(screenPosition);
    }

    private void RotateTowardScreenPosition(Vector2 position)
    {
        Ray ray = aimCamera.ScreenPointToRay(position);
        Plane aimPlane = new Plane(Vector3.up, transform.position);

        if (!aimPlane.Raycast(ray, out float distance))
        {
            return;
        }

        Vector3 targetPoint = ray.GetPoint(distance);
        Vector3 direction = targetPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private InputAction FindAction(string actionName)
    {
        if (inputActions == null)
        {
            return new InputAction(actionName, InputActionType.Value, "<Mouse>/position", expectedControlType: "Vector2");
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, true);
        return actionMap.FindAction(actionName, true);
    }
}
