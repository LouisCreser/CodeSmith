using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LevelRegistry", menuName = "Factory/Level Registry")]
public class LevelRegistry : ScriptableObject
{
    public List<LevelData> levels = new();

    public LevelData GetLevelById(string levelId)
    {
        if (string.IsNullOrEmpty(levelId) || levels == null)
            return null;

        foreach (var level in levels)
        {
            if (level != null && level.levelId == levelId)
                return level;
        }

        return null;
    }

    public string GetNextLevelId(string currentLevelId)
    {
        if (levels == null || levels.Count == 0 || string.IsNullOrEmpty(currentLevelId))
            return null;

        for (int i = 0; i < levels.Count; i++)
        {
            LevelData level = levels[i];
            if (level == null)
                continue;

            if (level.levelId != currentLevelId)
                continue;

            for (int j = i + 1; j < levels.Count; j++)
            {
                if (levels[j] != null && !string.IsNullOrEmpty(levels[j].levelId))
                    return levels[j].levelId;
            }

            return null;
        }

        return null;
    }

    public void EnsureDefaultUnlocks(PlayerData playerData)
    {
        if (playerData == null || levels == null || levels.Count == 0)
            return;

        bool unlockedAny = false;

        foreach (var level in levels)
        {
            if (level == null)
                continue;

            if (level.unlockedByDefault)
            {
                playerData.UnlockLevel(level.levelId);
                unlockedAny = true;
            }
        }

        if (!unlockedAny)
        {
            foreach (var level in levels)
            {
                if (level == null || string.IsNullOrEmpty(level.levelId))
                    continue;

                playerData.UnlockLevel(level.levelId);
                break;
            }
        }
    }
}