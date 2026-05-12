using UnityEngine;
using System.Collections.Generic;

public class LevelBoardStateController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TileGrid gridUI;
    [SerializeField] private LevelContext levelContext;
    [SerializeField] private FactoryComponentDatabase componentDatabase;
    [SerializeField] private ShopManualsUI shopManualsUI;

    private LevelData CurrentLevelData => levelContext != null ? levelContext.LevelData : null;
    private string CurrentLevelId => levelContext != null ? levelContext.LevelId : null;

    private readonly struct ResolvedPlacement
    {
        public readonly PlacedComponentSaveEntry SaveEntry;
        public readonly FactoryComponentData ComponentData;

        public ResolvedPlacement(PlacedComponentSaveEntry saveEntry, FactoryComponentData componentData)
        {
            SaveEntry = saveEntry;
            ComponentData = componentData;
        }
    }

    private void Start()
    {
        ValidateReferences();

        LevelData levelData = CurrentLevelData;
        if (gridUI == null || levelData == null || componentDatabase == null)
            return;

        if (PlayerData.Instance != null)
            PlayerData.Instance.InitializeLevelProgressIfMissing(levelData);

        componentDatabase.RebuildLookup();
        LoadBoardState();
    }

    private void ValidateReferences()
    {
        if (gridUI == null)
            Debug.LogError("LevelBoardStateController: gridUI is not assigned", this);

        if (levelContext == null)
            Debug.LogError("LevelBoardStateController: levelContext is not assigned", this);
        else if (levelContext.LevelData == null)
            Debug.LogError("LevelBoardStateController: levelContext has no LevelData", this);

        if (componentDatabase == null)
            Debug.LogError("LevelBoardStateController: componentDatabase is not assigned", this);

        if (shopManualsUI == null)
            Debug.LogWarning("LevelBoardStateController: shopManualsUI is not assigned", this);
    }

    private void LoadBoardState()
    {
        LevelData levelData = CurrentLevelData;
        string levelId = CurrentLevelId;

        if (levelData == null || string.IsNullOrEmpty(levelId))
        {
            Debug.LogError("LevelBoardStateController: cannot load board state because current level is missing", this);
            return;
        }

        if (PlayerData.Instance != null && PlayerData.Instance.HasSavedBoardState(levelId))
        {
            PlaceSavedBoardState();
            return;
        }

        PlaceDefaultBoardState();
    }

    private void PlaceSavedBoardState()
    {
        if (PlayerData.Instance == null)
        {
            Debug.LogError("LevelBoardStateController: cannot place saved board state because PlayerData.Instance is missing", this);
            return;
        }

        string levelId = CurrentLevelId;
        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogError("LevelBoardStateController: cannot place saved board state because current level ID is missing", this);
            return;
        }

        List<PlacedComponentSaveEntry> placedComponents = PlayerData.Instance.GetPlacedComponentsForLevel(levelId);

        foreach (PlacedComponentSaveEntry entry in placedComponents)
        {
            if (entry == null || string.IsNullOrEmpty(entry.componentId))
                continue;

            if (!TryGetComponentDefinition(entry.componentId, out FactoryComponentData def))
            {
                Debug.LogWarning($"LevelBoardStateController: missing component definition for saved placement '{entry.componentId}", this);
                continue;
            }

            if (def.component == null)
            {
                Debug.LogWarning($"LevelBoardStateController: component definition '{entry.componentId}' has no component prototype", def);
                continue;
            }

            FactoryComponent placedComponent = def.component.Clone();

            PlacedBuildComponent placed = new PlacedBuildComponent(placedComponent, def, entry.anchor, entry.rotationIndex, entry.supplyItemIdOverride);

            bool success = gridUI.PlacePlacedComponent(placed);
            if (!success)
                Debug.LogWarning($"LevelBoardStateController: failed to place saved component '{entry.componentId}' at {entry.anchor}", this);
        }

        List<WireSaveEntry> savedWires = PlayerData.Instance.GetWireEntriesForLevel(levelId);
        gridUI.ApplyWireEntries(savedWires);
    }

    private void PlaceDefaultBoardState()
    {
        PlaceDefaultFixedComponents();

        if (gridUI != null)
            gridUI.ApplyWireEntries(null);
    }

    private void PlaceDefaultFixedComponents()
    {
        LevelData levelData = CurrentLevelData;
        if (levelData == null)
        {
            Debug.LogError("LevelBoardStateController: cannot place default fixed components because current LevelData is missing", this);
            return;
        }

        PlaceDefaultSupplyBoxes(levelData);
        PlaceDefaultOutputBin(levelData);
        PlaceDefaultStartSignal(levelData);
    }

    private void PlaceDefaultSupplyBoxes(LevelData levelData)
    {
        if (levelData == null || levelData.supplyBoxes == null)
            return;

        foreach (SupplyBoxPlacement supplyBox in levelData.supplyBoxes)
        {
            if (supplyBox == null)
                continue;

            if (!TryGetComponentDefinition(supplyBox.componentId, out FactoryComponentData def))
            {
                Debug.LogWarning($"LevelBoardStateController: missing Supply Box definition for id '{supplyBox.componentId}", this);
                continue;
            }

            if (def.component == null)
            {
                Debug.LogWarning($"LevelBoardStateController: Supply Box definition '{supplyBox.componentId}' has no component prototype", def);
                continue;
            }

            FactoryComponent placedComponent = def.component.Clone();

            PlacedBuildComponent placed = new PlacedBuildComponent(placedComponent, def, supplyBox.anchor, supplyBox.rotationIndex, supplyBox.supplyItemId);

            bool success = gridUI.PlacePlacedComponent(placed);
            if (!success)
                Debug.LogWarning($"LevelBoardStateController: failed to place Supply Box at {supplyBox.anchor}", this);
        }
    }

    private void PlaceDefaultOutputBin(LevelData levelData)
    {
        if (levelData == null)
            return;

        if (string.IsNullOrEmpty(levelData.outputBinComponentId) ||
            !TryGetComponentDefinition(levelData.outputBinComponentId, out FactoryComponentData outputDef))
        {
            Debug.LogWarning("LevelBoardStateController: missing Output Bin definition or outputBinComponentId is not set", this);
            return;
        }

        if (outputDef.component == null)
        {
            Debug.LogWarning($"LevelBoardStateController: Output Bin definition '{levelData.outputBinComponentId}' has no component prototype", outputDef);
            return;
        }

        FactoryComponent placedComponent = outputDef.component.Clone();

        PlacedBuildComponent placed = new PlacedBuildComponent(placedComponent, outputDef, levelData.outputBinAnchor, levelData.outputBinRotationIndex);

        bool success = gridUI.PlacePlacedComponent(placed);
        if (!success)
            Debug.LogWarning($"LevelBoardStateController: failed to place Output Bin at {levelData.outputBinAnchor}", this);
    }

    private void PlaceDefaultStartSignal(LevelData levelData)
    {
        if (levelData == null)
            return;

        if (string.IsNullOrEmpty(levelData.startSignalComponentId) ||
            !TryGetComponentDefinition(levelData.startSignalComponentId, out FactoryComponentData startSignalDef))
        {
            Debug.LogWarning("LevelBoardStateController: missing Start Signal definition or startSignalComponentId is not set", this);
            return;
        }

        if (startSignalDef.component == null)
        {
            Debug.LogWarning($"LevelBoardStateController: Start Signal definition '{levelData.startSignalComponentId}' has no component prototype", startSignalDef);
            return;
        }

        FactoryComponent placedComponent = startSignalDef.component.Clone();

        PlacedBuildComponent placed = new PlacedBuildComponent(placedComponent, startSignalDef, levelData.startSignalAnchor, levelData.startSignalRotationIndex);

        bool success = gridUI.PlacePlacedComponent(placed);
        if (!success)
            Debug.LogWarning($"LevelBoardStateController: failed to place Start Signal at {levelData.startSignalAnchor}", this);
    }

    public void RestoreGridToBestState()
    {
        if (gridUI == null || PlayerData.Instance == null)
            return;

        string levelId = CurrentLevelId;
        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogError("LevelBoardStateController: cannot restore best state because current level ID is missing", this);
            return;
        }

        List<PlacedComponentSaveEntry> bestPlaced = PlayerData.Instance.GetBestSuccessfulLayoutForLevel(levelId);
        List<WireSaveEntry> bestWires = PlayerData.Instance.GetBestSuccessfulWireEntriesForLevel(levelId);

        if (bestPlaced == null || bestPlaced.Count == 0)
        {
            Debug.LogWarning("LevelBoardStateController: Restore Best skipped because no saved best layout exists for this level", this);
            return;
        }

        if (!TryResolveAndValidatePlacementList(bestPlaced, out List<ResolvedPlacement> resolvedPlacements))
        {
            Debug.LogWarning("LevelBoardStateController: Restore Best aborted. Current board was left unchanged", this);
            return;
        }

        Dictionary<string, int> stock = RefundCurrentBoard(levelId);
        ConsumeRestoredStock(stock, resolvedPlacements);

        gridUI.ClearGrid();

        foreach (ResolvedPlacement resolved in resolvedPlacements)
        {
            bool success = PlaceResolvedPlacement(resolved);
            if (!success)
            {
                Debug.LogError($"LevelBoardStateController: Restore Best failed unexpectedly while placing '{resolved.SaveEntry.componentId}' at {resolved.SaveEntry.anchor} " + this);
                return;
            }
        }

        gridUI.ApplyWireEntries(bestWires);

        SaveBoardStateAndStock(levelId, stock);
        RefreshShopStockDisplay();
    }

    public void ResetGrid()
    {
        if (gridUI == null || PlayerData.Instance == null)
            return;

        string levelId = CurrentLevelId;
        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogError("LevelBoardStateController: cannot Reset Grid because current level ID is missing", this);
            return;
        }

        Dictionary<string, int> stock = RefundCurrentBoard(levelId);

        gridUI.ClearGrid();

        PlaceDefaultFixedComponents();

        SaveBoardStateAndStock(levelId, stock);
        RefreshShopStockDisplay();
    }

    private void SaveBoardStateAndStock(string levelId, Dictionary<string, int> stock)
    {
        if (PlayerData.Instance == null || gridUI == null || string.IsNullOrEmpty(levelId))
            return;

        PlayerData.Instance.SetStoredStockDictionaryForLevel(levelId, stock);
        PlayerData.Instance.SetPlacedComponentsForLevel(levelId, gridUI.ExportPlacedComponentEntries());
        PlayerData.Instance.SetWireEntriesForLevel(levelId, gridUI.ExportWireEntries());
        PlayerData.Instance.SaveData();
    }

    private void RefreshShopStockDisplay()
    {
        if (shopManualsUI != null)
            shopManualsUI.RefreshVisibleRows();
    }

    private bool TryResolveAndValidatePlacementList(
        List<PlacedComponentSaveEntry> entries,
        out List<ResolvedPlacement> resolvedPlacements)
    {
        resolvedPlacements = new List<ResolvedPlacement>();

        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("LevelBoardStateController: placement validation failed because there are no entries", this);
            return false;
        }

        HashSet<Vector2Int> occupiedTiles = new();

        foreach (PlacedComponentSaveEntry entry in entries)
        {
            if (entry == null)
            {
                Debug.LogWarning("LevelBoardStateController: placement validation failed because an entry is null", this);
                return false;
            }

            if (string.IsNullOrEmpty(entry.componentId))
            {
                Debug.LogWarning("LevelBoardStateController: placement validation failed because an entry has no componentId", this);
                return false;
            }

            if (!TryGetComponentDefinition(entry.componentId, out FactoryComponentData componentData))
            {
                Debug.LogWarning($"LevelBoardStateController: placement validation failed because component '{entry.componentId}' is missing from the component database", this);
                return false;
            }

            if (componentData.component == null)
            {
                Debug.LogWarning($"LevelBoardStateController: placement validation failed because component '{entry.componentId}' has no component prototype", componentData);
                return false;
            }

            int normalizedRotation = NormalizeRotation(entry.rotationIndex);

            foreach (Vector2Int tile in gridUI.GetFootprintTiles(componentData, entry.anchor, normalizedRotation))
            {
                if (!gridUI.InBounds(tile))
                {
                    Debug.LogWarning($"LevelBoardStateController: placement validation failed because '{entry.componentId}' at {entry.anchor} occupies out-of-bounds tile {tile}", this);
                    return false;
                }

                if (!occupiedTiles.Add(tile))
                {
                    Debug.LogWarning($"LevelBoardStateController: placement validation failed because tile {tile} is occupied by multiple best-state components", this);
                    return false;
                }
            }

            resolvedPlacements.Add(new ResolvedPlacement(entry, componentData));
        }

        return true;
    }

    private Dictionary<string, int> RefundCurrentBoard(string levelId)
    {
        Dictionary<string, int> stock = PlayerData.Instance.GetStoredStockDictionaryForLevel(levelId);
        List<PlacedComponentSaveEntry> currentPlaced = gridUI.ExportPlacedComponentEntries();

        foreach (PlacedComponentSaveEntry currentEntry in currentPlaced)
        {
            if (currentEntry == null || string.IsNullOrEmpty(currentEntry.componentId))
                continue;

            if (!TryGetComponentDefinition(currentEntry.componentId, out FactoryComponentData currentDef))
            {
                Debug.LogWarning($"LevelBoardStateController: current placed component '{currentEntry.componentId}' has no definition, skipping storage refund", this);
                continue;
            }

            if (!IsComponentStorable(currentDef))
                continue;

            if (!stock.ContainsKey(currentEntry.componentId))
                stock[currentEntry.componentId] = 0;

            stock[currentEntry.componentId]++;
        }

        return stock;
    }

    private void ConsumeRestoredStock(Dictionary<string, int> stock, List<ResolvedPlacement> resolvedPlacements)
    {
        if (stock == null || resolvedPlacements == null)
            return;

        foreach (ResolvedPlacement resolved in resolvedPlacements)
        {
            string componentId = resolved.SaveEntry.componentId;

            if (!IsComponentStorable(resolved.ComponentData))
                continue;

            if (!stock.ContainsKey(componentId))
                stock[componentId] = 0;

            stock[componentId] = Mathf.Max(0, stock[componentId] - 1);
        }
    }

    private bool PlaceResolvedPlacement(ResolvedPlacement resolved)
    {
        PlacedComponentSaveEntry entry = resolved.SaveEntry;
        FactoryComponentData componentData = resolved.ComponentData;

        FactoryComponent placedComponent = componentData.component.Clone();

        PlacedBuildComponent placed = new PlacedBuildComponent(placedComponent, componentData, entry.anchor, entry.rotationIndex, entry.supplyItemIdOverride);

        return gridUI.PlacePlacedComponent(placed);
    }

    private bool TryGetComponentDefinition(string componentId, out FactoryComponentData componentData)
    {
        componentData = null;

        if (componentDatabase == null)
        {
            Debug.LogError("LevelBoardStateController: componentDatabase is not assigned", this);
            return false;
        }

        return componentDatabase.TryGet(componentId, out componentData);
    }

    private bool IsComponentStorable(FactoryComponentData data)
    {
        if (data == null)
            return false;

        return data.type != ComponentType.SupplyBox && data.type != ComponentType.OutputBin && data.type != ComponentType.StartSignal;
    }

    private static int NormalizeRotation(int rotationIndex)
    {
        return ((rotationIndex % 4) + 4) % 4;
    }
}