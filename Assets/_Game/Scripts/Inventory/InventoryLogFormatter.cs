using System.Collections.Generic;

public static class InventoryLogFormatter
{
    public static string Format(IReadOnlyList<InventoryItemStack> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return "(empty)";
        }

        List<string> lines = new List<string>();

        foreach (InventoryItemStack entry in entries)
        {
            if (entry.Definition == null || entry.Amount <= 0)
            {
                continue;
            }

            lines.Add($"{entry.Definition.DisplayName} x{entry.Amount}");
        }

        return lines.Count > 0 ? string.Join("\n", lines) : "(empty)";
    }
}
