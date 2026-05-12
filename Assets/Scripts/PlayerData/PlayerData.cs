using UnityEngine;
using System.Collections.Generic;

#region Save Data Types

[System.Serializable]
public class StoredStockSaveEntry
{
    public string componentId;
    public int amount;
}

[System.Serializable]
public class ComponentPurchaseCountSaveEntry
{
    public string componentId;
    public int count;
}

[System.Serializable]
public class UnlockedLevelSaveEntry
{
    public string levelId;
}

[System.Serializable]
public class PlacedComponentSaveEntry
{
    public string componentId;
    public Vector2Int anchor;
    public int rotationIndex;
    public string supplyItemIdOverride;
}

[System.Serializable]
public class WireSaveEntry
{
    public int x;
    public int y;
    public bool hasWire;
    public int connections;
}

[System.Serializable]
public class LevelProgressSaveEntry
{
    public string levelId;

    public List<PlacedComponentSaveEntry> placedComponents;
    public List<PlacedComponentSaveEntry> bestSuccessfulLayout;

    public List<WireSaveEntry> wireEntries;
    public List<WireSaveEntry> bestSuccessfulWireEntries;

    public List<StoredStockSaveEntry> storedStockEntries;
    public List<ComponentPurchaseCountSaveEntry> componentPurchaseCountEntries;
    public List<string> unlockedManualIds;

    public int bestProducedPerMinute;
    public int incomePerMinute;

    public bool firstCompletionRewardClaimed;
}

[System.Serializable]
public struct PlayerSaveData
{
    public int money;
    public List<UnlockedLevelSaveEntry> unlockedLevelEntries;
    public List<LevelProgressSaveEntry> levelProgressEntries;
}

#endregion

#region Runtime Data

public class PlayerData : MonoBehaviour
{
    private sealed class LevelProgressRuntime
    {
        public readonly Dictionary<string, int> storedStockByComponentId = new();
        public readonly Dictionary<string, int> purchaseCountByComponentId = new();
        public readonly HashSet<string> unlockedManualIds = new();

        public List<PlacedComponentSaveEntry> placedComponents = new();
        public List<PlacedComponentSaveEntry> bestSuccessfulLayout = new();

        public List<WireSaveEntry> wireEntries = new();
        public List<WireSaveEntry> bestSuccessfulWireEntries = new();

        public int bestProducedPerMinute;
        public int incomePerMinute;

        public bool firstCompletionRewardClaimed;

        public void Clear()
        {
            storedStockByComponentId.Clear();
            purchaseCountByComponentId.Clear();
            unlockedManualIds.Clear();

            placedComponents.Clear();
            bestSuccessfulLayout.Clear();

            wireEntries.Clear();
            bestSuccessfulWireEntries.Clear();

            bestProducedPerMinute = 0;
            incomePerMinute = 0;

            firstCompletionRewardClaimed = false;
        }
    }

    #endregion

    #region Singleton

    public static PlayerData Instance { get; private set; }

    #endregion

    #region Fields

    private int money;
    private float passiveIncomeAccumulator;

    private readonly Dictionary<string, LevelProgressRuntime> levelProgressByLevelId = new();
    private readonly HashSet<string> unlockedLevelIds = new();

    #endregion

    #region Properties

    public int Money
    {
        get { return money; }
        private set { money = Mathf.Max(0, value); }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadDataInternal();
    }

    private void Update()
    {
        TickPassiveIncome();
    }

    #endregion

    #region Money and Passive Income

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
            return true;

        if (Money < amount)
            return false;

        Money -= amount;
        return true;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
            return;

        Money += amount;
    }

    public int GetTotalIncomePerMinute()
    {
        int total = 0;

        foreach (LevelProgressRuntime state in levelProgressByLevelId.Values)
            total += Mathf.Max(0, state.incomePerMinute);

        return total;
    }

    private void TickPassiveIncome()
    {
        int totalIncomePerMinute = GetTotalIncomePerMinute();
        if (totalIncomePerMinute <= 0)
            return;

        float incomePerSecond = totalIncomePerMinute / 60f;
        passiveIncomeAccumulator += incomePerSecond * Time.unscaledDeltaTime;

        int wholeBits = Mathf.FloorToInt(passiveIncomeAccumulator);
        if (wholeBits <= 0)
            return;

        passiveIncomeAccumulator -= wholeBits;
        Money += wholeBits;
    }

    #endregion

    #region Level Unlocks

    public bool IsLevelUnlocked(string levelId)
    {
        return !string.IsNullOrEmpty(levelId) && unlockedLevelIds.Contains(levelId);
    }

    public void UnlockLevel(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
            return;

        unlockedLevelIds.Add(levelId);
    }

    #endregion

    #region Level Progress Initialization

    public bool HasSavedLevelState(string levelId)
    {
        return !string.IsNullOrEmpty(levelId) && levelProgressByLevelId.ContainsKey(levelId);
    }

    public bool HasSavedBoardState(string levelId)
    {
        LevelProgressRuntime state = TryGetLevelState(levelId);

        return state != null &&
               (state.placedComponents.Count > 0 || state.wireEntries.Count > 0);
    }

    public void InitializeLevelProgressIfMissing(LevelData levelData)
    {
        if (levelData == null || string.IsNullOrEmpty(levelData.levelId))
            return;

        if (HasSavedLevelState(levelData.levelId))
            return;

        LevelProgressRuntime state = GetOrCreateLevelState(levelData.levelId);
        state.Clear();

        ApplyStartingStoredComponents(state, levelData);
    }

    private void ApplyStartingStoredComponents(LevelProgressRuntime state, LevelData levelData)
    {
        if (state == null || levelData == null || levelData.startingStoredComponents == null)
            return;

        foreach (StartingStoredComponentEntry entry in levelData.startingStoredComponents)
        {
            if (entry == null || entry.componentData == null || string.IsNullOrEmpty(entry.componentData.id) || entry.amount <= 0)
            {
                continue;
            }

            state.storedStockByComponentId[entry.componentData.id] = entry.amount;
        }
    }

    #endregion

    #region Stored Stock

    public int GetStoredStock(string levelId, string componentId)
    {
        if (string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(componentId))
            return 0;

        LevelProgressRuntime state = TryGetLevelState(levelId);
        if (state == null)
            return 0;

        return state.storedStockByComponentId.TryGetValue(componentId, out int amount) ? Mathf.Max(0, amount) : 0;
    }

    public Dictionary<string, int> GetStoredStockDictionaryForLevel(string levelId)
    {
        Dictionary<string, int> copy = new();

        LevelProgressRuntime state = TryGetLevelState(levelId);
        if (state == null)
            return copy;

        foreach (KeyValuePair<string, int> kvp in state.storedStockByComponentId)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0)
                continue;

            copy[kvp.Key] = kvp.Value;
        }

        return copy;
    }

    public void SetStoredStockDictionaryForLevel(string levelId, Dictionary<string, int> stockByComponentId)
    {
        if (string.IsNullOrEmpty(levelId))
            return;

        LevelProgressRuntime state = GetOrCreateLevelState(levelId);
        state.storedStockByComponentId.Clear();

        if (stockByComponentId == null)
            return;

        foreach (KeyValuePair<string, int> kvp in stockByComponentId)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0)
                continue;

            state.storedStockByComponentId[kvp.Key] = kvp.Value;
        }
    }

    public void AddStoredStock(string levelId, string componentId, int amount = 1)
    {
        if (string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(componentId) || amount <= 0)
            return;

        LevelProgressRuntime state = GetOrCreateLevelState(levelId);
        int currentAmount = GetStoredStock(levelId, componentId);
        state.storedStockByComponentId[componentId] = currentAmount + amount;
    }

    public bool TryConsumeStoredStock(string levelId, string componentId, int amount = 1)
    {
        if (string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(componentId) || amount <= 0)
            return false;

        LevelProgressRuntime state = TryGetLevelState(levelId);
        if (state == null)
            return false;

        int currentAmount = GetStoredStock(levelId, componentId);
        if (currentAmount < amount)
            return false;

        int updatedAmount = currentAmount - amount;

        if (updatedAmount <= 0)
            state.storedStockByComponentId.Remove(componentId);
        else
            state.storedStockByComponentId[componentId] = updatedAmount;

        return true;
    }

    #endregion

    #region Component Purchase Counts

    public int GetComponentPurchaseCount(string levelId, string componentId)
    {
        if (string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(componentId))
            return 0;

        LevelProgressRuntime state = TryGetLevelState(levelId);
        if (state == null)
            return 0;

        return state.purchaseCountByComponentId.TryGetValue(componentId, out int count) ? Mathf.Max(0, count) : 0;
    }

    public int IncrementComponentPurchaseCount(string levelId, string componentId)
    {
        if (string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(componentId))
            return 0;

        LevelProgressRuntime state = GetOrCreateLevelState(levelId);
        int currentCount = GetComponentPurchaseCount(levelId, componentId);
        int updatedCount = currentCount + 1;

        state.purchaseCountByComponentId[componentId] = updatedCount;
        return updatedCount;
    }

    #endregion

    #region Manuals

    public bool IsManualUnlocked(string levelId, string manualId)
    {
        if (string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(manualId))
            return false;

        LevelProgressRuntime state = TryGetLevelState(levelId);
        return state != null && state.unlockedManualIds.Contains(manualId);
    }

    public void UnlockManual(string levelId, string manualId)
    {
        if (string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(manualId))
            return;

        LevelProgressRuntime state = GetOrCreateLevelState(levelId);
        state.unlockedManualIds.Add(manualId);
    }

    #endregion

    #region First Completion Reward

    public bool HasClaimedFirstCompletionReward(string levelId)
    {
        LevelProgressRuntime state = TryGetLevelState(levelId);
        return state != null && state.firstCompletionRewardClaimed;
    }

    public bool TryClaimFirstCompletionReward(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
            return false;

        LevelProgressRuntime state = GetOrCreateLevelState(levelId);
        if (state == null)
            return false;

        if (state.firstCompletionRewardClaimed)
            return false;

        state.firstCompletionRewardClaimed = true;
        return true;
    }

    #endregion

    #region Current Board State

    public List<PlacedComponentSaveEntry> GetPlacedComponentsForLevel(string levelId)
    {
        LevelProgressRuntime state = TryGetLevelState(levelId);
        return state != null ? CopyPlacedComponentList(state.placedComponents) : new List<PlacedComponentSaveEntry>();
    }

    public void SetPlacedComponentsForLevel(string levelId, List<PlacedComponentSaveEntry> placedComponents)
    {
        if (string.IsNullOrEmpty(levelId))
            return;

        LevelProgressRuntime state = GetOrCreateLevelState(levelId);
        state.placedComponents = CopyPlacedComponentList(placedComponents);
    }

    public List<WireSaveEntry> GetWireEntriesForLevel(string levelId)
    {
        LevelProgressRuntime state = TryGetLevelState(levelId);
        return state != null ? CopyWireEntryList(state.wireEntries) : new List<WireSaveEntry>();
    }

    public void SetWireEntriesForLevel(string levelId, List<WireSaveEntry> wireEntries)
    {
        if (string.IsNullOrEmpty(levelId))
            return;

        LevelProgressRuntime state = GetOrCreateLevelState(levelId);
        state.wireEntries = CopyWireEntryList(wireEntries);
    }

    #endregion

    #region Best Evaluation State

    public List<PlacedComponentSaveEntry> GetBestSuccessfulLayoutForLevel(string levelId)
    {
        LevelProgressRuntime state = TryGetLevelState(levelId);
        return state != null ? CopyPlacedComponentList(state.bestSuccessfulLayout) : new List<PlacedComponentSaveEntry>();
    }

    public List<WireSaveEntry> GetBestSuccessfulWireEntriesForLevel(string levelId)
    {
        LevelProgressRuntime state = TryGetLevelState(levelId);
        return state != null ? CopyWireEntryList(state.bestSuccessfulWireEntries) : new List<WireSaveEntry>();
    }

    public int GetBestIncomePerMinute(string levelId)
    {
        LevelProgressRuntime state = TryGetLevelState(levelId);
        return state != null ? Mathf.Max(0, state.incomePerMinute) : 0;
    }

    public bool RegisterLevelEvaluationResult(
        string levelId,
        int producedPerMinute,
        int incomePerMinute,
        List<PlacedComponentSaveEntry> placedComponents,
        List<WireSaveEntry> wireEntries)
    {
        if (string.IsNullOrEmpty(levelId))
            return false;

        producedPerMinute = Mathf.Max(0, producedPerMinute);
        incomePerMinute = Mathf.Max(0, incomePerMinute);

        LevelProgressRuntime state = GetOrCreateLevelState(levelId);

        bool isBetter =
            incomePerMinute > state.incomePerMinute ||
            (incomePerMinute == state.incomePerMinute && producedPerMinute > state.bestProducedPerMinute);

        if (!isBetter)
            return false;

        state.incomePerMinute = incomePerMinute;
        state.bestProducedPerMinute = producedPerMinute;
        state.bestSuccessfulLayout = CopyPlacedComponentList(placedComponents);
        state.bestSuccessfulWireEntries = CopyWireEntryList(wireEntries);

        return true;
    }

    #endregion

    #region Save Load Reset

    public void SaveData()
    {
        PlayerSaveData saveData = BuildSaveData();
        SaveSystem.Save(saveData);
        Debug.Log("Game saved");
    }

    public void LoadData()
    {
        LoadDataInternal();
        Debug.Log("Game loaded");
    }

    public void ResetProgress()
    {
        Money = 0;
        passiveIncomeAccumulator = 0f;

        levelProgressByLevelId.Clear();
        unlockedLevelIds.Clear();

        SaveSystem.DeleteSave();

        Debug.Log("Progress reset");
    }

    private PlayerSaveData BuildSaveData()
    {
        return new PlayerSaveData
        {
            money = Money,
            unlockedLevelEntries = BuildUnlockedLevelEntries(),
            levelProgressEntries = BuildLevelProgressEntries()
        };
    }

    private List<UnlockedLevelSaveEntry> BuildUnlockedLevelEntries()
    {
        List<UnlockedLevelSaveEntry> entries = new();

        foreach (string levelId in unlockedLevelIds)
        {
            if (string.IsNullOrEmpty(levelId))
                continue;

            entries.Add(new UnlockedLevelSaveEntry
            {
                levelId = levelId
            });
        }

        return entries;
    }

    private List<LevelProgressSaveEntry> BuildLevelProgressEntries()
    {
        List<LevelProgressSaveEntry> entries = new();

        foreach (KeyValuePair<string, LevelProgressRuntime> kvp in levelProgressByLevelId)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value == null)
                continue;

            entries.Add(BuildLevelProgressSaveEntry(kvp.Key, kvp.Value));
        }

        return entries;
    }

    private LevelProgressSaveEntry BuildLevelProgressSaveEntry(string levelId, LevelProgressRuntime state)
    {
        return new LevelProgressSaveEntry
        {
            levelId = levelId,

            placedComponents = CopyPlacedComponentList(state.placedComponents),
            bestSuccessfulLayout = CopyPlacedComponentList(state.bestSuccessfulLayout),

            wireEntries = CopyWireEntryList(state.wireEntries),
            bestSuccessfulWireEntries = CopyWireEntryList(state.bestSuccessfulWireEntries),

            storedStockEntries = BuildStoredStockEntries(state),
            componentPurchaseCountEntries = BuildComponentPurchaseCountEntries(state),
            unlockedManualIds = new List<string>(state.unlockedManualIds),

            bestProducedPerMinute = Mathf.Max(0, state.bestProducedPerMinute),
            incomePerMinute = Mathf.Max(0, state.incomePerMinute),

            firstCompletionRewardClaimed = state.firstCompletionRewardClaimed
        };
    }

    private List<StoredStockSaveEntry> BuildStoredStockEntries(LevelProgressRuntime state)
    {
        List<StoredStockSaveEntry> entries = new();

        if (state == null)
            return entries;

        foreach (KeyValuePair<string, int> stock in state.storedStockByComponentId)
        {
            if (string.IsNullOrEmpty(stock.Key) || stock.Value <= 0)
                continue;

            entries.Add(new StoredStockSaveEntry
            {
                componentId = stock.Key,
                amount = stock.Value
            });
        }

        return entries;
    }

    private List<ComponentPurchaseCountSaveEntry> BuildComponentPurchaseCountEntries(LevelProgressRuntime state)
    {
        List<ComponentPurchaseCountSaveEntry> entries = new();

        if (state == null)
            return entries;

        foreach (KeyValuePair<string, int> purchaseCount in state.purchaseCountByComponentId)
        {
            if (string.IsNullOrEmpty(purchaseCount.Key) || purchaseCount.Value <= 0)
                continue;

            entries.Add(new ComponentPurchaseCountSaveEntry
            {
                componentId = purchaseCount.Key,
                count = purchaseCount.Value
            });
        }

        return entries;
    }

    private void LoadDataInternal()
    {
        PlayerSaveData loadedData = SaveSystem.Load();

        Money = loadedData.money;
        passiveIncomeAccumulator = 0f;

        levelProgressByLevelId.Clear();
        unlockedLevelIds.Clear();

        LoadUnlockedLevels(loadedData.unlockedLevelEntries);
        LoadLevelProgress(loadedData.levelProgressEntries);
    }

    private void LoadUnlockedLevels(List<UnlockedLevelSaveEntry> entries)
    {
        if (entries == null)
            return;

        foreach (UnlockedLevelSaveEntry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.levelId))
                continue;

            unlockedLevelIds.Add(entry.levelId);
        }
    }

    private void LoadLevelProgress(List<LevelProgressSaveEntry> entries)
    {
        if (entries == null)
            return;

        foreach (LevelProgressSaveEntry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.levelId))
                continue;

            LevelProgressRuntime state = GetOrCreateLevelState(entry.levelId);
            LoadLevelProgressEntryIntoRuntimeState(entry, state);
        }
    }

    private void LoadLevelProgressEntryIntoRuntimeState(LevelProgressSaveEntry entry, LevelProgressRuntime state)
    {
        state.Clear();

        state.placedComponents = CopyPlacedComponentList(entry.placedComponents);
        state.bestSuccessfulLayout = CopyPlacedComponentList(entry.bestSuccessfulLayout);

        state.wireEntries = CopyWireEntryList(entry.wireEntries);
        state.bestSuccessfulWireEntries = CopyWireEntryList(entry.bestSuccessfulWireEntries);

        state.bestProducedPerMinute = Mathf.Max(0, entry.bestProducedPerMinute);
        state.incomePerMinute = Mathf.Max(0, entry.incomePerMinute);

        state.firstCompletionRewardClaimed = entry.firstCompletionRewardClaimed;

        LoadStoredStockIntoRuntimeState(entry.storedStockEntries, state);
        LoadComponentPurchaseCountsIntoRuntimeState(entry.componentPurchaseCountEntries, state);
        LoadUnlockedManualsIntoRuntimeState(entry.unlockedManualIds, state);
    }

    private void LoadStoredStockIntoRuntimeState(List<StoredStockSaveEntry> entries, LevelProgressRuntime state)
    {
        if (entries == null || state == null)
            return;

        foreach (StoredStockSaveEntry stock in entries)
        {
            if (stock == null || string.IsNullOrEmpty(stock.componentId) || stock.amount <= 0)
                continue;

            state.storedStockByComponentId[stock.componentId] = stock.amount;
        }
    }

    private void LoadComponentPurchaseCountsIntoRuntimeState(List<ComponentPurchaseCountSaveEntry> entries, LevelProgressRuntime state)
    {
        if (entries == null || state == null)
            return;

        foreach (ComponentPurchaseCountSaveEntry purchaseCount in entries)
        {
            if (purchaseCount == null || string.IsNullOrEmpty(purchaseCount.componentId) || purchaseCount.count <= 0)
                continue;

            state.purchaseCountByComponentId[purchaseCount.componentId] = purchaseCount.count;
        }
    }

    private void LoadUnlockedManualsIntoRuntimeState(List<string> manualIds, LevelProgressRuntime state)
    {
        if (manualIds == null || state == null)
            return;

        foreach (string manualId in manualIds)
        {
            if (string.IsNullOrEmpty(manualId))
                continue;

            state.unlockedManualIds.Add(manualId);
        }
    }

    #endregion

    #region Level State Helpers

    private LevelProgressRuntime GetOrCreateLevelState(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
            return null;

        if (!levelProgressByLevelId.TryGetValue(levelId, out LevelProgressRuntime state))
        {
            state = new LevelProgressRuntime();
            levelProgressByLevelId[levelId] = state;
        }

        return state;
    }

    private LevelProgressRuntime TryGetLevelState(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
            return null;

        levelProgressByLevelId.TryGetValue(levelId, out LevelProgressRuntime state);
        return state;
    }

    #endregion

    #region Copy Helpers

    private static List<PlacedComponentSaveEntry> CopyPlacedComponentList(List<PlacedComponentSaveEntry> source)
    {
        List<PlacedComponentSaveEntry> copy = new();

        if (source == null)
            return copy;

        foreach (PlacedComponentSaveEntry entry in source)
        {
            if (entry == null || string.IsNullOrEmpty(entry.componentId))
                continue;

            copy.Add(new PlacedComponentSaveEntry
            {
                componentId = entry.componentId,
                anchor = entry.anchor,
                rotationIndex = entry.rotationIndex,
                supplyItemIdOverride = entry.supplyItemIdOverride
            });
        }

        return copy;
    }

    private static List<WireSaveEntry> CopyWireEntryList(List<WireSaveEntry> source)
    {
        List<WireSaveEntry> copy = new();

        if (source == null)
            return copy;

        foreach (WireSaveEntry entry in source)
        {
            if (entry == null)
                continue;

            copy.Add(new WireSaveEntry
            {
                x = entry.x,
                y = entry.y,
                hasWire = entry.hasWire,
                connections = entry.connections
            });
        }

        return copy;
    }

    #endregion
}