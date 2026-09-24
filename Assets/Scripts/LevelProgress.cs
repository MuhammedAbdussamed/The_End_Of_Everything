using UnityEngine;

public static class LevelProgress
{
    private const string LegacyHighestUnlockedLevelKey = "HighestUnlockedLevel";
    private const string ActiveSaveSlotKey = "ActiveSaveSlot";
    private const string SaveMigrationKey = "SaveSlotsMigrated";
    public const int SaveSlotCount = 3;
    public const int LevelCount = 3;

    public static int ActiveSlot
    {
        get
        {
            EnsureLegacySaveIsMigrated();
            return Mathf.Clamp(PlayerPrefs.GetInt(ActiveSaveSlotKey, 1), 1, SaveSlotCount);
        }
    }

    public static int HighestUnlockedLevel => GetHighestUnlockedLevel(ActiveSlot);

    public static bool HasData(int slot)
    {
        EnsureLegacySaveIsMigrated();
        return IsValidSlot(slot) && PlayerPrefs.HasKey(GetProgressKey(slot));
    }

    public static int GetHighestUnlockedLevel(int slot)
    {
        EnsureLegacySaveIsMigrated();
        if (!IsValidSlot(slot)) return 1;
        return Mathf.Clamp(PlayerPrefs.GetInt(GetProgressKey(slot), 1), 1, LevelCount);
    }

    public static void SelectSlot(int slot)
    {
        EnsureLegacySaveIsMigrated();
        if (!IsValidSlot(slot)) return;

        PlayerPrefs.SetInt(ActiveSaveSlotKey, slot);
        if (!PlayerPrefs.HasKey(GetProgressKey(slot)))
            PlayerPrefs.SetInt(GetProgressKey(slot), 1);
        PlayerPrefs.Save();
    }

    public static bool IsUnlocked(int levelNumber) =>
        levelNumber >= 1 && levelNumber <= HighestUnlockedLevel;

    public static void CompleteLevel(string sceneName)
    {
        int completedLevel = GetLevelNumber(sceneName);
        int highestUnlockedLevel = HighestUnlockedLevel;
        if (completedLevel <= 0 || completedLevel >= LevelCount || completedLevel != highestUnlockedLevel) return;

        int unlockedLevel = completedLevel + 1;
        PlayerPrefs.SetInt(GetProgressKey(ActiveSlot), unlockedLevel);
        PlayerPrefs.Save();
    }

    private static bool IsValidSlot(int slot) => slot >= 1 && slot <= SaveSlotCount;

    private static string GetProgressKey(int slot) => $"SaveSlot.{slot}.HighestUnlockedLevel";

    private static void EnsureLegacySaveIsMigrated()
    {
        if (PlayerPrefs.GetInt(SaveMigrationKey, 0) != 0) return;

        string firstSlotKey = GetProgressKey(1);
        if (PlayerPrefs.HasKey(LegacyHighestUnlockedLevelKey) && !PlayerPrefs.HasKey(firstSlotKey))
        {
            int legacyProgress = Mathf.Clamp(PlayerPrefs.GetInt(LegacyHighestUnlockedLevelKey, 1), 1, LevelCount);
            PlayerPrefs.SetInt(firstSlotKey, legacyProgress);
        }

        PlayerPrefs.SetInt(SaveMigrationKey, 1);
        PlayerPrefs.Save();
    }

    public static int GetLevelNumber(string sceneName) => sceneName switch
    {
        "SampleScene" => 1,
        "Level2" => 2,
        "Level3" => 3,
        _ => 0
    };
}
