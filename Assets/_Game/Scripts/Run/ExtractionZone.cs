using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class ExtractionZone : MonoBehaviour
{
    public event Action ExtractionStateChanged;

    [SerializeField, Min(0.1f)] private float extractionDuration = 3f;
    [SerializeField] private RunSessionController runSessionController;

    private float elapsedTime;
    private bool playerInside;
    private bool extractionCompleted;

    public bool IsPlayerInside => playerInside;
    public float CurrentTime => elapsedTime;
    public float RequiredTime => extractionDuration;
    public float CurrentExtractionTime => elapsedTime;
    public float ExtractionDuration => extractionDuration;
    public float RemainingTime => Mathf.Max(0f, extractionDuration - elapsedTime);
    public float Progress => ProgressNormalized;
    public float ProgressNormalized => extractionDuration > 0f ? Mathf.Clamp01(elapsedTime / extractionDuration) : 0f;

    private void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;

        if (runSessionController == null)
        {
            runSessionController = FindFirstObjectByType<RunSessionController>();
        }
    }

    private void Update()
    {
        if (!playerInside || extractionCompleted || runSessionController == null || !runSessionController.IsRunning)
        {
            return;
        }

        elapsedTime += Time.deltaTime;

        if (elapsedTime >= extractionDuration)
        {
            extractionCompleted = true;
            Debug.Log("Extraction complete.", this);
            ExtractionStateChanged?.Invoke();
            runSessionController.CompleteExtraction();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other) || extractionCompleted || runSessionController == null || !runSessionController.IsRunning)
        {
            return;
        }

        playerInside = true;
        elapsedTime = 0f;
        ExtractionStateChanged?.Invoke();
        Debug.Log($"Entered extraction zone.\nExtracting... {extractionDuration:0.#} seconds required.", this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other) || !playerInside || extractionCompleted)
        {
            return;
        }

        playerInside = false;
        elapsedTime = 0f;
        ExtractionStateChanged?.Invoke();
        Debug.Log("Extraction cancelled.", this);
    }

    private static bool IsPlayer(Collider other)
    {
        return other.GetComponentInParent<PlayerInventory>() != null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}
