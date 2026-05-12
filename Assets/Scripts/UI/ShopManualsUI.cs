using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class ShopManualsUI : MonoBehaviour
{
    public enum PanelMode
    {
        Shop,
        Manuals
    }

    [Header("UI References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RowItemUI rowPrefab;

    public event Action<FactoryComponentData> ComponentClicked;
    public event Action<ManualData> ManualClicked;

    public Func<FactoryComponentData, int> GetStoredCount { get; set; }
    public Func<FactoryComponentData, int> GetPurchaseCount { get; set; }
    public Func<ManualData, bool> IsManualUnlocked { get; set; }

    public PanelMode CurrentMode { get; private set; } = PanelMode.Shop;

    private readonly Dictionary<string, RowItemUI> componentRowsById = new();
    private readonly Dictionary<string, FactoryComponentData> componentDataById = new();

    private readonly Dictionary<string, RowItemUI> manualRowsById = new();
    private readonly Dictionary<string, ManualData> manualDataById = new();

    private LevelData currentLevelData;
    private string selectedComponentId;
    private int lastObservedMoney = int.MinValue;

    private void Awake()
    {
        ValidateReferences();
    }

    private void OnEnable()
    {
        lastObservedMoney = int.MinValue;
        RefreshVisibleRows();
    }

    private void Update()
    {
        RefreshVisibleRowAffordabilityIfMoneyChanged(force: false);
    }

    public void ShowShop(LevelData levelData)
    {
        currentLevelData = levelData;
        CurrentMode = PanelMode.Shop;
        RebuildShopRows(levelData);
    }

    public void ShowManuals(LevelData levelData)
    {
        currentLevelData = levelData;
        CurrentMode = PanelMode.Manuals;
        RebuildManualRows(levelData);
    }

    public void Refresh(LevelData levelData)
    {
        currentLevelData = levelData;

        if (CurrentMode == PanelMode.Manuals)
            RebuildManualRows(levelData);
        else
            RebuildShopRows(levelData);
    }

    public void RefreshVisibleRows()
    {
        if (CurrentMode == PanelMode.Shop)
        {
            RefreshAllComponentRowDisplays();
        }
        else if (CurrentMode == PanelMode.Manuals)
        {
            RefreshAllManualRowDisplays();
        }

        lastObservedMoney = GetCurrentMoney();
    }

    public void SetSelectedComponentId(string componentId)
    {
        if (selectedComponentId == componentId)
            return;

        string previousSelectedId = selectedComponentId;
        selectedComponentId = componentId;

        SetRowSelected(previousSelectedId, false);
        SetRowSelected(selectedComponentId, true);
    }

    public void ClearSelectedComponent()
    {
        if (string.IsNullOrEmpty(selectedComponentId))
            return;

        string previousSelectedId = selectedComponentId;
        selectedComponentId = null;

        SetRowSelected(previousSelectedId, false);
    }

    public void RefreshStoredCountForComponent(string componentId)
    {
        if (CurrentMode != PanelMode.Shop)
            return;

        if (string.IsNullOrEmpty(componentId))
            return;

        RefreshComponentRowDisplay(componentId);
        lastObservedMoney = GetCurrentMoney();
    }

    public void RefreshPriceForComponent(string componentId)
    {
        if (CurrentMode != PanelMode.Shop)
            return;

        if (string.IsNullOrEmpty(componentId))
            return;

        RefreshComponentRowDisplay(componentId);
        lastObservedMoney = GetCurrentMoney();
    }

    private void ValidateReferences()
    {
        if (scrollRect == null)
            Debug.LogWarning("ShopManualsUI: scrollRect is not assigned", this);

        if (contentRoot == null)
            Debug.LogError("ShopManualsUI: contentRoot is not assigned", this);

        if (rowPrefab == null)
            Debug.LogError("ShopManualsUI: rowPrefab is not assigned", this);
    }

    private void ClearRows()
    {
        componentRowsById.Clear();
        componentDataById.Clear();

        manualRowsById.Clear();
        manualDataById.Clear();

        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);
    }

    private void ResetScroll()
    {
        if (scrollRect != null)
            scrollRect.normalizedPosition = new Vector2(0f, 1f);
    }

    private void RebuildShopRows(LevelData levelData)
    {
        ClearRows();

        if (contentRoot == null || rowPrefab == null)
        {
            ResetScroll();
            return;
        }

        if (levelData == null)
        {
            Debug.LogError("ShopManualsUI: cannot rebuild shop rows because levelData is null", this);
            ResetScroll();
            return;
        }

        if (levelData.shopEntries == null)
        {
            Debug.LogError($"ShopManualsUI: level '{levelData.levelId}' has null shopEntries", levelData);
            ResetScroll();
            return;
        }

        for (int i = 0; i < levelData.shopEntries.Count; i++)
        {
            FactoryComponentData componentData = levelData.shopEntries[i];

            if (componentData == null)
            {
                Debug.LogWarning($"ShopManualsUI: level '{levelData.levelId}' has null shopEntries[{i}]", levelData);
                continue;
            }

            if (componentData.component == null)
            {
                Debug.LogWarning($"ShopManualsUI: shop component '{componentData.id}' has no FactoryComponent prototype", componentData);
                continue;
            }

            if (!ShouldShowInShop(componentData))
                continue;

            RowItemUI row = Instantiate(rowPrefab, contentRoot);

            int currentPrice = GetCurrentComponentPrice(componentData);
            int storedCount = GetStoredCountForComponent(componentData);
            bool selected = IsSelectedComponent(componentData);
            bool canAfford = CanAfford(currentPrice);

            row.BindComponent(componentData, currentPrice, storedCount, selected, canAfford, clickedComponentData => ComponentClicked?.Invoke(clickedComponentData));

            RegisterComponentRow(componentData, row);
        }

        lastObservedMoney = GetCurrentMoney();
        ResetScroll();
    }

    private void RebuildManualRows(LevelData levelData)
    {
        ClearRows();

        if (contentRoot == null || rowPrefab == null)
        {
            ResetScroll();
            return;
        }

        if (levelData == null)
        {
            Debug.LogError("ShopManualsUI: cannot rebuild manual rows because levelData is null", this);
            ResetScroll();
            return;
        }

        if (levelData.manualEntries == null)
        {
            Debug.LogWarning($"ShopManualsUI: level '{levelData.levelId}' has null manualEntries", levelData);
            ResetScroll();
            return;
        }

        for (int i = 0; i < levelData.manualEntries.Count; i++)
        {
            ManualData manual = levelData.manualEntries[i];

            if (manual == null)
            {
                Debug.LogWarning($"ShopManualsUI: level '{levelData.levelId}' has null manualEntries[{i}]", levelData);
                continue;
            }

            if (string.IsNullOrEmpty(manual.manualId))
            {
                Debug.LogWarning($"ShopManualsUI: manual asset '{manual.name}' has no manualId", manual);
                continue;
            }

            RowItemUI row = Instantiate(rowPrefab, contentRoot);

            bool unlocked = IsManualUnlockedForCurrentLevel(manual);
            bool canAfford = CanAfford(manual.unlockPrice);

            row.BindManual(manual, unlocked, canAfford, clickedManual => ManualClicked?.Invoke(clickedManual));

            RegisterManualRow(manual, row);
        }

        lastObservedMoney = GetCurrentMoney();
        ResetScroll();
    }

    private void RegisterComponentRow(FactoryComponentData componentData, RowItemUI row)
    {
        if (componentData == null || row == null || string.IsNullOrEmpty(componentData.id))
            return;

        componentRowsById[componentData.id] = row;
        componentDataById[componentData.id] = componentData;
    }

    private void RegisterManualRow(ManualData manual, RowItemUI row)
    {
        if (manual == null || row == null || string.IsNullOrEmpty(manual.manualId))
            return;

        manualRowsById[manual.manualId] = row;
        manualDataById[manual.manualId] = manual;
    }

    private void RefreshVisibleRowAffordabilityIfMoneyChanged(bool force)
    {
        int currentMoney = GetCurrentMoney();

        if (!force && currentMoney == lastObservedMoney)
            return;

        RefreshVisibleRows();
    }

    private void RefreshAllComponentRowDisplays()
    {
        foreach (string componentId in componentRowsById.Keys)
            RefreshComponentRowDisplay(componentId);
    }

    private void RefreshAllManualRowDisplays()
    {
        foreach (string manualId in manualRowsById.Keys)
            RefreshManualRowDisplay(manualId);
    }

    private void RefreshComponentRowDisplay(string componentId)
    {
        if (string.IsNullOrEmpty(componentId))
            return;

        if (!componentRowsById.TryGetValue(componentId, out RowItemUI row) || row == null)
            return;

        if (!componentDataById.TryGetValue(componentId, out FactoryComponentData componentData) || componentData == null || componentData.component == null)
            return;

        int currentPrice = GetCurrentComponentPrice(componentData);
        int storedCount = GetStoredCountForComponent(componentData);
        bool canAfford = CanAfford(currentPrice);

        row.SetComponentPriceState(currentPrice, storedCount, canAfford);
    }

    private void RefreshManualRowDisplay(string manualId)
    {
        if (string.IsNullOrEmpty(manualId))
            return;

        if (!manualRowsById.TryGetValue(manualId, out RowItemUI row) || row == null)
            return;

        if (!manualDataById.TryGetValue(manualId, out ManualData manual) || manual == null)
            return;

        bool unlocked = IsManualUnlockedForCurrentLevel(manual);
        bool canAfford = CanAfford(manual.unlockPrice);

        row.SetManualState(manual.unlockPrice, unlocked, canAfford);
    }

    private int GetCurrentComponentPrice(FactoryComponentData componentData)
    {
        int purchaseCount = GetPurchaseCountForComponent(componentData);
        return ComponentPricingService.GetCurrentPrice(componentData, currentLevelData, purchaseCount);
    }

    private int GetStoredCountForComponent(FactoryComponentData componentData)
    {
        return GetStoredCount != null && componentData != null ? Mathf.Max(0, GetStoredCount(componentData)) : 0;
    }

    private int GetPurchaseCountForComponent(FactoryComponentData componentData)
    {
        return GetPurchaseCount != null && componentData != null ? Mathf.Max(0, GetPurchaseCount(componentData)) : 0;
    }

    private bool IsManualUnlockedForCurrentLevel(ManualData manual)
    {
        return IsManualUnlocked != null && manual != null && IsManualUnlocked(manual);
    }

    private bool CanAfford(int price)
    {
        return PlayerData.Instance != null && PlayerData.Instance.Money >= Mathf.Max(0, price);
    }

    private int GetCurrentMoney()
    {
        return PlayerData.Instance != null ? PlayerData.Instance.Money : 0;
    }

    private bool IsSelectedComponent(FactoryComponentData componentData)
    {
        return componentData != null && !string.IsNullOrEmpty(selectedComponentId) && componentData.id == selectedComponentId;
    }

    private void SetRowSelected(string componentId, bool selected)
    {
        if (string.IsNullOrEmpty(componentId))
            return;

        if (componentRowsById.TryGetValue(componentId, out RowItemUI row) && row != null)
            row.SetComponentSelected(selected);
    }

    private bool ShouldShowInShop(FactoryComponentData componentData)
    {
        if (componentData == null)
            return false;

        return componentData.type != ComponentType.SupplyBox && componentData.type != ComponentType.OutputBin && componentData.type != ComponentType.StartSignal;
    }
}