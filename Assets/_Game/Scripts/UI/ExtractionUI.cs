using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExtractionUI : MonoBehaviour
{
    [SerializeField] private ExtractionZone extractionZone;
    [SerializeField] private GameObject panel;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text remainingTimeText;

    private RectTransform progressFillRect;

    private void Awake()
    {
        if (extractionZone == null)
        {
            extractionZone = FindFirstObjectByType<ExtractionZone>();
        }

        ValidateAuthoredView();
        progressFillRect = progressFill != null ? progressFill.rectTransform : null;
    }

    private void OnEnable()
    {
        if (extractionZone != null)
        {
            extractionZone.ExtractionStateChanged += RefreshVisibility;
        }

        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (extractionZone != null)
        {
            extractionZone.ExtractionStateChanged -= RefreshVisibility;
        }
    }

    private void Update()
    {
        if (extractionZone == null || panel == null || !panel.activeSelf)
        {
            return;
        }

        float progress = extractionZone.ProgressNormalized;
        float remainingTime = extractionZone.RemainingTime;

        if (progressFill != null)
        {
            progressFill.fillAmount = progress;
        }

        if (progressFillRect != null)
        {
            progressFillRect.localScale = new Vector3(progress, 1f, 1f);
        }

        if (remainingTimeText != null)
        {
            remainingTimeText.text = $"{remainingTime:0.0}s";
        }
    }

    public void Hide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void RefreshVisibility()
    {
        bool show = extractionZone != null && extractionZone.IsPlayerInside;

        if (panel != null)
        {
            panel.SetActive(show);
        }

        if (!show && progressFill != null)
        {
            progressFill.fillAmount = 0f;
        }

        if (!show && progressFillRect != null)
        {
            progressFillRect.localScale = new Vector3(0f, 1f, 1f);
        }
    }

    private void ValidateAuthoredView()
    {
        if (panel == null || progressFill == null || remainingTimeText == null)
        {
            Debug.LogWarning("ExtractionUI requires authored panel, progressFill, and remainingTimeText references.", this);
        }
    }
}
