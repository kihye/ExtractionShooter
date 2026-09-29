using System;
using UnityEngine;

public sealed class RuntimeSpawnId : MonoBehaviour
{
    [SerializeField] private string spawnId;

    public string SpawnId => EnsureId();

    public string EnsureId()
    {
        if (string.IsNullOrWhiteSpace(spawnId))
        {
            spawnId = Guid.NewGuid().ToString("N");
        }

        return spawnId;
    }

    public void RestoreId(string id)
    {
        spawnId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
    }
}
