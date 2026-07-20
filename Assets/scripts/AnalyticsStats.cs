using UnityEngine;

/// <summary>Stores the statistics from the most recently completed gameplay run.</summary>
public static class AnalyticsStats
{
    public const string LastRunTimeSecondsKey = "LastRunTimeSeconds";
    public const string LastRunObstaclesDestroyedKey = "LastRunObstaclesDestroyed";

    public static int CurrentRunObstaclesDestroyed { get; private set; }
    public static int LastRunTimeSeconds => PlayerPrefs.GetInt(LastRunTimeSecondsKey, 0);
    public static int LastRunObstaclesDestroyed => PlayerPrefs.GetInt(LastRunObstaclesDestroyedKey, 0);

    public static void BeginRun()
    {
        CurrentRunObstaclesDestroyed = 0;
    }

    public static void RegisterObstacleDestroyed()
    {
        CurrentRunObstaclesDestroyed++;
    }

    public static void RecordCompletedRun(int elapsedSeconds)
    {
        PlayerPrefs.SetInt(LastRunTimeSecondsKey, Mathf.Max(0, elapsedSeconds));
        PlayerPrefs.SetInt(LastRunObstaclesDestroyedKey, CurrentRunObstaclesDestroyed);
        PlayerPrefs.Save();
    }
}
