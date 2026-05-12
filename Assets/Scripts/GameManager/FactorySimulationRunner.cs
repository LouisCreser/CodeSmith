using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class FactorySimulationRunner : MonoBehaviour, ISimulationRunner
{
    [Header("References")]
    [SerializeField] private TileGrid gridUI;
    [SerializeField] private LevelContext levelContext;
    [SerializeField] private LevelBoardStateSaver boardStateSaver;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private ComponentSimulationRules simulationRules;

    [Header("Item Visuals")]
    [SerializeField] private RectTransform itemVisualRoot;
    [SerializeField] private Sprite placeholderItemSprite;

    private FactorySimulation sim;

    private readonly Dictionary<Vector2Int, Image> itemViews = new();
    private readonly Dictionary<Vector2Int, SegmentedProgressBarView> progressBarViews = new();

    public bool HasError => sim != null && sim.HasError;
    public string ErrorMessage => sim != null ? sim.ErrorMessage : null;

    public int ProducedCount => sim != null ? sim.ProducedCount : 0;

    public int ProducedValue
    {
        get
        {
            LevelData levelData = CurrentLevelData;
            return levelData != null ? ProducedCount * levelData.targetItemValue : 0;
        }
    }

    private LevelData CurrentLevelData => levelContext != null ? levelContext.LevelData : null;

    private void Awake()
    {
        ValidateReferences();

        if (itemDatabase != null)
            itemDatabase.RebuildLookup();
    }

    public void BeginRun()
    {
        LevelData levelData = CurrentLevelData;

        if (gridUI == null || levelData == null)
        {
            Debug.LogError("FactorySimulationRunner: cannot begin run because gridUI or LevelData is missing", this);
            return;
        }

        if (simulationRules == null)
        {
            Debug.LogError("FactorySimulationRunner: cannot begin run because simulationRules is missing", this);
            return;
        }

        ClearItemViews();
        ClearProgressBarViews();

        sim = new FactorySimulation(gridUI.Columns, gridUI.Rows, levelData.targetItemId);
        sim.SetWireLayout(gridUI.CreateWireLayoutSnapshot());

        if (sim.HasError)
            return;

        if (boardStateSaver != null)
            boardStateSaver.SaveCurrentBoardState();

        AddAllGridComponentsToSimulation();

        if (sim.HasError)
            return;

        gridUI.ClearDisplayedActiveWireState();
        RefreshItemViews();
        RefreshProgressBarViews();
    }

    public void EndRun()
    {
        ClearItemViews();
        ClearProgressBarViews();

        if (gridUI != null)
            gridUI.ClearDisplayedActiveWireState();

        sim = null;
    }

    public void TickOnce()
    {
        if (sim == null || sim.HasError)
            return;

        sim.Tick();

        if (gridUI != null)
            gridUI.SetDisplayedActiveWireState(sim.GetActiveLogicTilesForDisplay());

        RefreshItemViews();
        RefreshProgressBarViews();
    }

    public void TryInteractAt(Vector2Int tile)
    {
        if (sim == null || sim.HasError || !sim.InBounds(tile))
            return;

        PlacedComponentInstance component = sim.GetComponentAt(tile);
        if (component == null || component.data == null)
            return;

        if (component.data.type == ComponentType.Button)
            sim.QueueButtonPress(component.anchor);
    }

    private void ValidateReferences()
    {
        if (gridUI == null)
            Debug.LogError("FactorySimulationRunner: gridUI is not assigned", this);

        if (levelContext == null)
            Debug.LogError("FactorySimulationRunner: levelContext is not assigned", this);
        else if (levelContext.LevelData == null)
            Debug.LogError("FactorySimulationRunner: levelContext has no LevelData", this);

        if (boardStateSaver == null)
            Debug.LogWarning("FactorySimulationRunner: boardStateSaver is not assigned", this);

        if (itemDatabase == null)
            Debug.LogWarning("FactorySimulationRunner: itemDatabase is not assigned", this);

        if (simulationRules == null)
            Debug.LogError("FactorySimulationRunner: simulationRules is not assigned", this);

        if (itemVisualRoot == null)
            Debug.LogWarning("FactorySimulationRunner: itemVisualRoot is not assigned", this);

        if (placeholderItemSprite == null)
            Debug.LogWarning("FactorySimulationRunner: placeholderItemSprite is not assigned", this);
    }

    private void AddAllGridComponentsToSimulation()
    {
        if (sim == null || gridUI == null)
            return;

        foreach (PlacedBuildComponent placed in gridUI.GetAllPlacedComponents())
        {
            if (placed == null || placed.data == null)
                continue;

            PlacedComponentInstance instance = new PlacedComponentInstance(
                placed.data,
                placed.anchor,
                placed.rotationIndex,
                simulationRules,
                placed.supplyItemIdOverride);

            if (!sim.TryAddComponent(instance))
            {
                sim.Fail($"Failed to place component {placed.data.id} in the simulation.");
                return;
            }
        }
    }

    private Sprite GetItemSprite(string itemId)
    {
        if (itemDatabase == null)
            return placeholderItemSprite;

        return itemDatabase.GetSpriteOrFallback(itemId, placeholderItemSprite);
    }

    private void RefreshItemViews()
    {
        if (sim == null || itemVisualRoot == null || gridUI == null)
            return;

        List<Vector2Int> toRemove = new();

        foreach (KeyValuePair<Vector2Int, Image> kvp in itemViews)
        {
            Vector2Int position = kvp.Key;

            if (sim.GetItemAt(position) == null)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);

                toRemove.Add(position);
            }
        }

        foreach (Vector2Int position in toRemove)
            itemViews.Remove(position);

        for (int y = 0; y < sim.height; y++)
        {
            for (int x = 0; x < sim.width; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                ItemInstance item = sim.GetItemAt(position);

                if (item == null)
                    continue;

                if (!itemViews.TryGetValue(position, out Image view) || view == null)
                {
                    GameObject go = new GameObject($"Item_{x}_{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(itemVisualRoot, false);

                    view = go.GetComponent<Image>();
                    view.raycastTarget = false;
                    view.preserveAspect = true;
                    view.useSpriteMesh = false;

                    itemViews[position] = view;
                }

                view.sprite = GetItemSprite(item.itemId);

                GridTile tile = gridUI.GetTile(position);
                if (tile == null)
                    continue;

                RectTransform viewTransform = view.rectTransform;
                RectTransform tileTransform = tile.transform as RectTransform;

                if (tileTransform == null)
                    continue;

                viewTransform.anchorMin = new Vector2(0.5f, 0.5f);
                viewTransform.anchorMax = new Vector2(0.5f, 0.5f);
                viewTransform.pivot = new Vector2(0.5f, 0.5f);
                viewTransform.sizeDelta = gridUI.CellSize;
                viewTransform.position = tileTransform.TransformPoint(tileTransform.rect.center);
            }
        }
    }

    private void RefreshProgressBarViews()
    {
        if (sim == null || itemVisualRoot == null || gridUI == null)
            return;

        List<SimulationProgressDisplayEntry> entries = sim.GetProgressDisplayEntries();
        HashSet<Vector2Int> activeTiles = new();

        foreach (SimulationProgressDisplayEntry entry in entries)
        {
            if (!entry.IsValid)
                continue;

            activeTiles.Add(entry.Tile);

            if (!progressBarViews.TryGetValue(entry.Tile, out SegmentedProgressBarView view) || view == null)
            {
                view = SegmentedProgressBarView.Create(
                    $"ProgressBar_{entry.Tile.x}_{entry.Tile.y}",
                    itemVisualRoot);

                progressBarViews[entry.Tile] = view;
            }

            view.SetProgress(entry.CompletedSegments, entry.TotalSegments, entry.IsError);
            PositionProgressBarView(entry.Tile, view);
            view.SetVisible(true);
        }

        List<Vector2Int> toRemove = new();

        foreach (KeyValuePair<Vector2Int, SegmentedProgressBarView> kvp in progressBarViews)
        {
            if (activeTiles.Contains(kvp.Key))
                continue;

            if (kvp.Value != null)
                kvp.Value.Destroy();

            toRemove.Add(kvp.Key);
        }

        foreach (Vector2Int tile in toRemove)
            progressBarViews.Remove(tile);
    }

    private void PositionProgressBarView(Vector2Int tilePosition, SegmentedProgressBarView view)
    {
        if (view == null || gridUI == null)
            return;

        GridTile tile = gridUI.GetTile(tilePosition);
        if (tile == null)
            return;

        RectTransform tileTransform = tile.transform as RectTransform;
        if (tileTransform == null)
            return;

        Vector2 cellSize = gridUI.CellSize;

        Vector2 localPosition = tileTransform.rect.center;
        localPosition.y += tileTransform.rect.height * 0.34f;

        view.RectTransform.position = tileTransform.TransformPoint(localPosition);
        view.RectTransform.sizeDelta = new Vector2(cellSize.x * 0.68f, Mathf.Max(4f, cellSize.y * 0.08f));
    }

    private void ClearItemViews()
    {
        foreach (KeyValuePair<Vector2Int, Image> kvp in itemViews)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
        }

        itemViews.Clear();
    }

    private void ClearProgressBarViews()
    {
        foreach (KeyValuePair<Vector2Int, SegmentedProgressBarView> kvp in progressBarViews)
        {
            if (kvp.Value != null)
                kvp.Value.Destroy();
        }

        progressBarViews.Clear();
    }

    private sealed class SegmentedProgressBarView
    {
        private readonly RectTransform root;
        private readonly List<Image> segmentImages = new();

        public RectTransform RectTransform => root;

        private SegmentedProgressBarView(RectTransform root)
        {
            this.root = root;
        }

        public static SegmentedProgressBarView Create(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rectTransform = go.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;

            return new SegmentedProgressBarView(rectTransform);
        }

        public void SetProgress(int completedSegments, int totalSegments, bool isError)
        {
            totalSegments = Mathf.Max(1, totalSegments);
            completedSegments = Mathf.Clamp(completedSegments, 0, totalSegments);

            EnsureSegmentCount(totalSegments);
            LayoutSegments();

            for (int i = 0; i < segmentImages.Count; i++)
            {
                Image image = segmentImages[i];
                if (image == null)
                    continue;

                bool filled = i < completedSegments;

                if (isError)
                {
                    image.color = UIColourUtility.ErrorRed;
                    continue;
                }

                image.color = filled ? UIColourUtility.PositiveGreen : UIColourUtility.Darkened(UIColourUtility.DisabledGrey).Normal;
            }
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.gameObject.SetActive(visible);
        }

        public void Destroy()
        {
            if (root != null)
                UnityEngine.Object.Destroy(root.gameObject);
        }

        private void EnsureSegmentCount(int count)
        {
            while (segmentImages.Count < count)
                segmentImages.Add(CreateSegment(segmentImages.Count));

            for (int i = segmentImages.Count - 1; i >= count; i--)
            {
                if (segmentImages[i] != null)
                    UnityEngine.Object.Destroy(segmentImages[i].gameObject);

                segmentImages.RemoveAt(i);
            }
        }

        private Image CreateSegment(int index)
        {
            GameObject go = new GameObject($"Segment_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root, false);

            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;

            return image;
        }

        private void LayoutSegments()
        {
            int count = segmentImages.Count;
            if (count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                Image image = segmentImages[i];
                if (image == null)
                    continue;

                RectTransform rectTransform = image.rectTransform;

                float minX = (float)i / count;
                float maxX = (float)(i + 1) / count;

                rectTransform.anchorMin = new Vector2(minX, 0f);
                rectTransform.anchorMax = new Vector2(maxX, 1f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);

                float horizontalInset = count > 1 ? 0.75f : 0f;
                rectTransform.offsetMin = new Vector2(horizontalInset, 0f);
                rectTransform.offsetMax = new Vector2(-horizontalInset, 0f);
            }
        }
    }
}