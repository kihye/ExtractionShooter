using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class BaseStashUI : MonoBehaviour
{
    [SerializeField] private StashInventory stashInventory;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text itemListText;

    private void Awake()
    {
        stashInventory ??= FindFirstObjectByType<StashInventory>();
        ValidateAuthoredView();
    }

    private void OnEnable()
    {
        if (stashInventory != null)
        {
            stashInventory.StashChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (stashInventory != null)
        {
            stashInventory.StashChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        if (itemListText == null)
        {
            return;
        }

        if (stashInventory == null)
        {
            itemListText.text = "Empty";
            return;
        }

        List<InventoryItemStack> entries = stashInventory.GetEntries();
        if (entries.Count == 0)
        {
            itemListText.text = "Empty";
            return;
        }

        List<string> lines = new List<string>();
        foreach (InventoryItemStack entry in entries)
        {
            if (entry.Definition == null || entry.Amount <= 0)
            {
                continue;
            }

            string sellValue = entry.Definition.SellValue > 0
                ? $"{entry.Definition.SellValue} C"
                : "No sale";
            lines.Add($"{entry.Definition.DisplayName} x{entry.Amount}    {sellValue}");
        }

        itemListText.text = lines.Count > 0 ? string.Join("\n", lines) : "Empty";
    }

    private void ValidateAuthoredView()
    {
        if (panel == null || itemListText == null)
        {
            Debug.LogWarning("BaseStashUI requires authored panel and itemListText references.", this);
        }
    }
}
