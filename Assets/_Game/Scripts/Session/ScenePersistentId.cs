using System;
using UnityEngine;

public sealed class ScenePersistentId : MonoBehaviour
{
    [SerializeField] private string persistentId;

    public string PersistentId => persistentId;

    public void EnsureId(string preferredId = null)
    {
        if (!string.IsNullOrWhiteSpace(persistentId))
        {
            return;
        }

        persistentId = !string.IsNullOrWhiteSpace(preferredId)
            ? preferredId
            : Guid.NewGuid().ToString("N");
    }
}
