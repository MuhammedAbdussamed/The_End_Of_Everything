using NUnit.Framework;
using UnityEngine;

public class LevelProgressTests
{
    private const string ActiveSlotKey = "ActiveSaveSlot";
    private const string SlotTwoKey = "SaveSlot.2.HighestUnlockedLevel";
    private const string SlotThreeKey = "SaveSlot.3.HighestUnlockedLevel";

    private bool hadActiveSlot;
    private bool hadSlotTwo;
    private bool hadSlotThree;
    private int activeSlot;
    private int slotTwoProgress;
    private int slotThreeProgress;

    [SetUp]
    public void PreservePlayerPrefs()
    {
        hadActiveSlot = PlayerPrefs.HasKey(ActiveSlotKey);
        hadSlotTwo = PlayerPrefs.HasKey(SlotTwoKey);
        hadSlotThree = PlayerPrefs.HasKey(SlotThreeKey);
        activeSlot = PlayerPrefs.GetInt(ActiveSlotKey, 1);
        slotTwoProgress = PlayerPrefs.GetInt(SlotTwoKey, 1);
        slotThreeProgress = PlayerPrefs.GetInt(SlotThreeKey, 1);

        PlayerPrefs.DeleteKey(SlotTwoKey);
        PlayerPrefs.DeleteKey(SlotThreeKey);
    }

    [TearDown]
    public void RestorePlayerPrefs()
    {
        RestoreKey(ActiveSlotKey, hadActiveSlot, activeSlot);
        RestoreKey(SlotTwoKey, hadSlotTwo, slotTwoProgress);
        RestoreKey(SlotThreeKey, hadSlotThree, slotThreeProgress);
        PlayerPrefs.Save();
    }

    [Test]
    public void SaveSlotsKeepIndependentLevelProgress()
    {
        LevelProgress.SelectSlot(2);
        LevelProgress.CompleteLevel("SampleScene");

        LevelProgress.SelectSlot(3);
        Assert.That(LevelProgress.HighestUnlockedLevel, Is.EqualTo(1));
        Assert.That(LevelProgress.IsUnlocked(2), Is.False);

        LevelProgress.SelectSlot(2);
        Assert.That(LevelProgress.HighestUnlockedLevel, Is.EqualTo(2));
        Assert.That(LevelProgress.IsUnlocked(2), Is.True);
        Assert.That(LevelProgress.IsUnlocked(3), Is.False);

        LevelProgress.CompleteLevel("Level2");
        Assert.That(LevelProgress.IsUnlocked(3), Is.True);
        Assert.That(LevelProgress.GetHighestUnlockedLevel(3), Is.EqualTo(1));
    }

    private static void RestoreKey(string key, bool existed, int value)
    {
        if (existed) PlayerPrefs.SetInt(key, value);
        else PlayerPrefs.DeleteKey(key);
    }
}
