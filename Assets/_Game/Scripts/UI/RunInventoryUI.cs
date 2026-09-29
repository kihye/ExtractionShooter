using System.Text;
using TMPro;
using UnityEngine;

public sealed class RunInventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerEquipment playerEquipment;
    [SerializeField] private TMP_Text itemListText;

    private readonly StringBuilder builder = new StringBuilder();

    private void Awake()
    {
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        if (playerEquipment == null && playerInventory != null)
        {
            playerEquipment = playerInventory.GetComponent<PlayerEquipment>();
        }

        playerEquipment ??= FindFirstObjectByType<PlayerEquipment>();
        ValidateAuthoredView();
    }

    private void OnEnable()
    {
        if (playerInventory != null)
        {
            playerInventory.InventoryChanged += Refresh;
        }

        if (playerEquipment != null)
        {
            playerEquipment.EquipmentChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (playerInventory != null)
        {
            playerInventory.InventoryChanged -= Refresh;
        }

        if (playerEquipment != null)
        {
            playerEquipment.EquipmentChanged -= Refresh;
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (itemListText == null)
        {
            return;
        }

        builder.Clear();
        builder.AppendLine("RUN INVENTORY");

        if (playerInventory == null)
        {
            builder.Append("(missing inventory)");
            itemListText.text = builder.ToString();
            return;
        }

        var entries = playerInventory.GetEntries();
        if (entries.Count == 0)
        {
            builder.Append("Empty");
        }
        else
        {
            builder.AppendLine("Items");
            foreach (InventoryItemStack entry in entries)
            {
                if (entry.Definition == null)
                {
                    continue;
                }

                builder.Append(entry.Definition.DisplayName);
                builder.Append("  x");
                builder.AppendLine(entry.Amount.ToString());
            }
        }

        if (playerInventory != null)
        {
            builder.AppendLine();
            builder.Append(playerInventory.FormatGridDebugText());
        }

        if (playerEquipment != null)
        {
            builder.AppendLine();
            builder.AppendLine("EQUIPMENT");

            foreach (EquipmentSlot slot in playerEquipment.Slots)
            {
                builder.Append(slot.DisplayName);
                builder.Append(": ");
                builder.AppendLine(slot.GetDisplayValue());
            }
        }

        itemListText.text = builder.ToString();
    }

    private void ValidateAuthoredView()
    {
        if (itemListText == null)
        {
            Debug.LogWarning("RunInventoryUI requires an authored itemListText reference.", this);
        }
    }
}
