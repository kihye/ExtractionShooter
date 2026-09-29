using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Extraction Shooter/Item Definition Registry", fileName = "ItemDefinitionRegistry")]
public sealed class ItemDefinitionRegistry : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

    public IReadOnlyList<ItemDefinition> ItemDefinitions => itemDefinitions;

    public bool TryGetDefinition(string itemId, out ItemDefinition definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        foreach (ItemDefinition candidate in itemDefinitions)
        {
            if (candidate == null || candidate.ItemId != itemId)
            {
                continue;
            }

            definition = candidate;
            return true;
        }

        return false;
    }

    public List<string> ValidateRegistry()
    {
        List<string> issues = new List<string>();
        HashSet<string> ids = new HashSet<string>();

        foreach (ItemDefinition definition in itemDefinitions)
        {
            if (definition == null)
            {
                issues.Add("Registry contains an empty ItemDefinition reference.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(definition.ItemId))
            {
                issues.Add($"{definition.name} is missing itemId.");
                continue;
            }

            if (!ids.Add(definition.ItemId))
            {
                issues.Add($"Duplicate itemId: {definition.ItemId}.");
            }
        }

        return issues;
    }
}
