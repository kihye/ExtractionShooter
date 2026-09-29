using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Extraction Shooter/Scene Map Registry", fileName = "SceneMapRegistry")]
public sealed class SceneMapRegistry : ScriptableObject
{
    [Serializable]
    public sealed class SceneMapEntry
    {
        [SerializeField] private string mapId;
        [SerializeField] private string displayName;
        [SerializeField] private string sceneName;
        [SerializeField] private GameSceneKind sceneKind;
        [SerializeField] private Vector3 fallbackSpawnPosition;
        [SerializeField] private float fallbackSpawnYaw;

        public string MapId => mapId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? sceneName : displayName;
        public string SceneName => sceneName;
        public GameSceneKind SceneKind => sceneKind;
        public Vector3 FallbackSpawnPosition => fallbackSpawnPosition;
        public float FallbackSpawnYaw => fallbackSpawnYaw;
    }

    [SerializeField] private List<SceneMapEntry> maps = new List<SceneMapEntry>();

    public IReadOnlyList<SceneMapEntry> Maps => maps;

    public bool TryGetById(string mapId, out SceneMapEntry entry)
    {
        foreach (SceneMapEntry candidate in maps)
        {
            if (candidate != null && candidate.MapId == mapId)
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }

    public bool TryGetBySceneName(string sceneName, out SceneMapEntry entry)
    {
        foreach (SceneMapEntry candidate in maps)
        {
            if (candidate != null && candidate.SceneName == sceneName)
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }

    public bool TryGetFirstByKind(GameSceneKind kind, out SceneMapEntry entry)
    {
        foreach (SceneMapEntry candidate in maps)
        {
            if (candidate != null && candidate.SceneKind == kind)
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }

    public List<string> ValidateRegistry()
    {
        List<string> issues = new List<string>();
        HashSet<string> ids = new HashSet<string>();
        HashSet<string> scenes = new HashSet<string>();

        foreach (SceneMapEntry entry in maps)
        {
            if (entry == null)
            {
                issues.Add("SceneMapRegistry contains an empty map entry.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.MapId))
            {
                issues.Add($"Scene map for scene '{entry.SceneName}' is missing mapId.");
            }
            else if (!ids.Add(entry.MapId))
            {
                issues.Add($"Duplicate scene mapId: {entry.MapId}");
            }

            if (string.IsNullOrWhiteSpace(entry.SceneName))
            {
                issues.Add($"Scene map '{entry.MapId}' is missing sceneName.");
            }
            else if (!scenes.Add(entry.SceneName))
            {
                issues.Add($"Duplicate scene name in SceneMapRegistry: {entry.SceneName}");
            }
        }

        return issues;
    }
}
