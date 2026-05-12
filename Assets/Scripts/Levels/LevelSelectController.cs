using UnityEngine;

public class LevelSelectController : MonoBehaviour
{
    [Header("Scene Loading")]
    [SerializeField] private SceneLoader sceneLoader;

    [Header("References")]
    [SerializeField] private LevelRegistry levelRegistry;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private LevelSelectEntryUI entryPrefab;

    private void Start()
    {
        if (!HasRequiredReferences())
            return;

        if (PlayerData.Instance == null)
        {
            Debug.LogError("LevelSelectController: PlayerData instance not found", this);
            return;
        }

        // Defensive fallback
        levelRegistry.EnsureDefaultUnlocks(PlayerData.Instance);

        Refresh();
    }

    private bool HasRequiredReferences()
    {
        bool valid = true;

        if (sceneLoader == null)
        {
            Debug.LogError("LevelSelectController: sceneLoader is not assigned", this);
            valid = false;
        }

        if (levelRegistry == null)
        {
            Debug.LogError("LevelSelectController: levelRegistry is not assigned", this);
            valid = false;
        }

        if (itemDatabase == null)
        {
            Debug.LogError("LevelSelectController: itemDatabase is not assigned", this);
            valid = false;
        }

        if (contentRoot == null)
        {
            Debug.LogError("LevelSelectController: contentRoot is not assigned", this);
            valid = false;
        }

        if (entryPrefab == null)
        {
            Debug.LogError("LevelSelectController: entryPrefab is not assigned", this);
            valid = false;
        }

        return valid;
    }

    public void Refresh()
    {
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        if (levelRegistry == null || levelRegistry.levels == null)
            return;

        foreach (LevelData level in levelRegistry.levels)
        {
            if (level == null)
                continue;

            LevelSelectEntryUI entry = Instantiate(entryPrefab, contentRoot);

            bool isUnlocked = PlayerData.Instance != null && PlayerData.Instance.IsLevelUnlocked(level.levelId);
            int incomePerMinute = PlayerData.Instance != null ? PlayerData.Instance.GetBestIncomePerMinute(level.levelId) : 0;

            string targetItemDisplayName = GetTargetItemDisplayName(level);

            entry.Bind(level, isUnlocked, incomePerMinute, targetItemDisplayName, LoadLevel);
        }
    }

    private string GetTargetItemDisplayName(LevelData level)
    {
        if (level == null)
            return "Null";

        if (string.IsNullOrEmpty(level.targetItemId))
            return "Null";

        if (itemDatabase == null)
            return "Null";

        if (!itemDatabase.TryGetItem(level.targetItemId, out ItemData item))
            return "Null";

        if (item == null)
            return "Null";

        return string.IsNullOrEmpty(item.displayName) ? "Null" : item.displayName;
    }

    public void LoadLevel(LevelData level)
    {
        if (level == null)
        {
            Debug.LogError("LevelSelectController: level is null", this);
            return;
        }

        if (PlayerData.Instance == null)
        {
            Debug.LogError("LevelSelectController: PlayerData instance not found", this);
            return;
        }

        if (!PlayerData.Instance.IsLevelUnlocked(level.levelId))
        {
            Debug.LogWarning($"Level '{level.levelId}' is locked", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(level.sceneName))
        {
            Debug.LogError($"LevelSelectController: sceneName missing for level '{level.levelId}", this);
            return;
        }

        if (sceneLoader == null)
        {
            Debug.LogError("LevelSelectController: cannot load level because sceneLoader is not assigned", this);
            return;
        }

        sceneLoader.LoadScene(level.sceneName);
    }
}