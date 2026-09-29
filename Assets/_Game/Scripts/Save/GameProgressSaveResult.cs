using System;

public sealed class GameProgressSaveResult
{
    private GameProgressSaveResult(
        bool succeeded,
        string message,
        string diagnosticMessage,
        DateTime? savedAtUtc,
        bool backupAvailable,
        string targetSceneName,
        string targetMapId)
    {
        Succeeded = succeeded;
        Message = message;
        DiagnosticMessage = diagnosticMessage;
        SavedAtUtc = savedAtUtc;
        BackupAvailable = backupAvailable;
        TargetSceneName = targetSceneName;
        TargetMapId = targetMapId;
    }

    public bool Succeeded { get; }
    public string Message { get; }
    public string DiagnosticMessage { get; }
    public DateTime? SavedAtUtc { get; }
    public bool BackupAvailable { get; }
    public string TargetSceneName { get; }
    public string TargetMapId { get; }

    public static GameProgressSaveResult Success(string message, DateTime? savedAtUtc = null, string targetSceneName = null, string targetMapId = null)
    {
        return new GameProgressSaveResult(true, message, null, savedAtUtc, false, targetSceneName, targetMapId);
    }

    public static GameProgressSaveResult Fail(string message, string diagnosticMessage = null, bool backupAvailable = false)
    {
        return new GameProgressSaveResult(false, message, diagnosticMessage, null, backupAvailable, null, null);
    }
}
