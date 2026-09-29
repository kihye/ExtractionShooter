using UnityEngine;

public sealed class RunRestartController : MonoBehaviour
{
    [SerializeField] private SceneFlowController sceneFlowController;

    private bool isRestarting;

    private void Awake()
    {
        if (sceneFlowController == null)
        {
            sceneFlowController = FindFirstObjectByType<SceneFlowController>();
        }
    }

    public void RestartRun()
    {
        if (isRestarting)
        {
            return;
        }

        isRestarting = true;

        if (sceneFlowController == null)
        {
            sceneFlowController = FindFirstObjectByType<SceneFlowController>();
        }

        if (sceneFlowController == null)
        {
            Debug.LogWarning("Run restart requested but no SceneFlowController was found.", this);
            isRestarting = false;
            return;
        }

        if (!sceneFlowController.LoadRun())
        {
            isRestarting = false;
        }
    }
}
