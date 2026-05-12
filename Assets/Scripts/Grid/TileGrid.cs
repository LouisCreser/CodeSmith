using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TileGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int columns = 10;
    [SerializeField] private int rows = 8;

    [Header("References")]
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private RectTransform gridRect;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private AspectRatioFitter aspectFitter;

    [Header("Wire Layer")]
    [SerializeField] private bool wireOverlayVisible;

    [Header("Inactive Wire Sprites")]
    [SerializeField] private Sprite inactiveWireIsolatedSprite;
    [SerializeField] private Sprite inactiveWireStraightSprite;
    [SerializeField] private Sprite inactiveWireSingleSprite;
    [SerializeField] private Sprite inactiveWireCornerSprite;
    [SerializeField] private Sprite inactiveWireTJunctionSprite;
    [SerializeField] private Sprite inactiveWireCrossSprite;

    [Header("Active Wire Sprites")]
    [SerializeField] private Sprite activeWireIsolatedSprite;
    [SerializeField] private Sprite activeWireStraightSprite;
    [SerializeField] private Sprite activeWireSingleSprite;
    [SerializeField] private Sprite activeWireCornerSprite;
    [SerializeField] private Sprite activeWireTJunctionSprite;
    [SerializeField] private Sprite activeWireCrossSprite;

    private GridTile[,] tiles;
    private WireTileData[,] wires;
    private bool[,] activeWireTilesForDisplay;

    public int Columns => columns;
    public int Rows => rows;
    public RectTransform GridRect => gridRect;
    public GridLayoutGroup GridLayout => gridLayout;
    public Vector2 CellSize => gridLayout != null ? gridLayout.cellSize : Vector2.zero;

    private static readonly Vector2Int[] OrthogonalNeighborOffsets =
    {
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(1, 0)
    };

    private enum WireVisualShape
    {
        None,
        Isolated,
        Straight,
        Single,
        Corner,
        TJunction,
        Cross
    }

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        ValidateReferences();
        UpdateAspectRatio();
        BuildGrid();
    }

    private void OnValidate()
    {
        if (rows <= 0)
            rows = 1;

        if (columns <= 0)
            columns = 1;

        if (aspectFitter == null)
            aspectFitter = GetComponent<AspectRatioFitter>();

        UpdateAspectRatio();
    }

    private void OnRectTransformDimensionsChange()
    {
        UpdateCellSize();
    }

    #endregion

    #region Setup and Validation

    private void ResolveReferences()
    {
        if (gridRect == null)
            gridRect = GetComponent<RectTransform>();

        if (gridLayout == null)
            gridLayout = GetComponent<GridLayoutGroup>();

        if (aspectFitter == null)
            aspectFitter = GetComponent<AspectRatioFitter>();
    }

    private void ValidateReferences()
    {
        if (gridRect == null)
            Debug.LogError("TileGrid: gridRect is not assigned and could not be found", this);

        if (gridLayout == null)
            Debug.LogError("TileGrid: gridLayout is not assigned and could not be found", this);

        if (tilePrefab == null)
            Debug.LogError("TileGrid: tilePrefab is not assigned", this);
    }

    private void UpdateAspectRatio()
    {
        if (aspectFitter == null || rows <= 0)
            return;

        aspectFitter.aspectRatio = (float)columns / rows;
    }

    private void BuildGrid()
    {
        if (gridRect == null || gridLayout == null || tilePrefab == null)
            return;

        ClearGeneratedTiles();

        tiles = new GridTile[columns, rows];
        wires = new WireTileData[columns, rows];
        activeWireTilesForDisplay = new bool[columns, rows];

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columns;

        UpdateCellSize();
        CreateGridTiles();
        RefreshAllWireVisuals();
    }

    private void ClearGeneratedTiles()
    {
        if (gridRect == null)
            return;

        for (int i = gridRect.childCount - 1; i >= 0; i--)
            Destroy(gridRect.GetChild(i).gameObject);
    }

    private void CreateGridTiles()
    {
        int totalTiles = columns * rows;

        for (int i = 0; i < totalTiles; i++)
        {
            GameObject tileGridObject = Instantiate(tilePrefab, gridRect);

            GridTile tile = tileGridObject.GetComponent<GridTile>();
            if (tile == null)
                tile = tileGridObject.AddComponent<GridTile>();

            int x = i % columns;
            int y = i / columns;

            tile.gridPosition = new Vector2Int(x, y);
            tile.ClearPlacedComponent();

            tiles[x, y] = tile;
            wires[x, y] = new WireTileData();
        }
    }

    private void UpdateCellSize()
    {
        if (gridRect == null || gridLayout == null || columns <= 0 || rows <= 0)
            return;

        Vector2 size = gridRect.rect.size;
        float cellSize = Mathf.Min(size.x / columns, size.y / rows);
        gridLayout.cellSize = new Vector2(cellSize, cellSize);
    }

    #endregion

    #region Grid Queries

    public bool InBounds(Vector2Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < columns && gridPos.y >= 0 && gridPos.y < rows;
    }

    public GridTile GetTile(Vector2Int gridPos)
    {
        if (tiles == null)
        {
            Debug.LogError("TileGrid.GetTile called before grid was built", this);
            return null;
        }

        if (!InBounds(gridPos))
            return null;

        return tiles[gridPos.x, gridPos.y];
    }

    public GridTile GetTileAtScreenPosition(Vector2 screenPosition)
    {
        if (gridRect == null || gridLayout == null || tiles == null)
            return null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPosition, null, out Vector2 localPosition))
        {
            return null;
        }

        Rect rect = gridRect.rect;

        Vector2 bottomLeftSpace = localPosition + new Vector2(
            rect.width * gridRect.pivot.x,
            rect.height * gridRect.pivot.y);

        RectOffset padding = gridLayout.padding;
        float x = bottomLeftSpace.x - padding.left;
        float y = (rect.height - bottomLeftSpace.y) - padding.top;

        Vector2 cell = gridLayout.cellSize;
        Vector2 spacing = gridLayout.spacing;

        float strideX = cell.x + spacing.x;
        float strideY = cell.y + spacing.y;

        if (strideX <= 0f || strideY <= 0f)
            return null;

        if (x < 0f || y < 0f)
            return null;

        int column = Mathf.FloorToInt(x / strideX);
        int row = Mathf.FloorToInt(y / strideY);

        return GetTile(new Vector2Int(column, row));
    }

    public void ClearGrid()
    {
        ClearAllPlacedComponents();
        ClearAllWires();
    }

    #endregion

    #region Component Placement

    public IEnumerable<Vector2Int> GetFootprintTiles(FactoryComponentData data, Vector2Int anchor, int rotationIndex)
    {
        if (data == null || data.footprintCells == null || data.footprintCells.Length == 0)
        {
            yield return anchor;
            yield break;
        }

        foreach (Vector2Int offset in data.footprintCells)
            yield return anchor + DirUtil.RotateOffset(offset, rotationIndex);
    }

    public IEnumerable<Vector2Int> GetLogicInputTiles(FactoryComponentData data, Vector2Int anchor, int rotationIndex)
    {
        if (data == null || data.logicInputTiles == null || data.logicInputTiles.Length == 0)
            yield break;

        foreach (Vector2Int offset in data.logicInputTiles)
            yield return anchor + DirUtil.RotateOffset(offset, rotationIndex);
    }

    public bool CanPlaceComponentAt(FactoryComponentData data, Vector2Int anchor, int rotationIndex, PlacedBuildComponent ignore = null)
    {
        if (data == null)
            return false;

        foreach (Vector2Int tilePos in GetFootprintTiles(data, anchor, rotationIndex))
        {
            if (!InBounds(tilePos))
                return false;

            GridTile tile = GetTile(tilePos);
            if (tile == null)
                return false;

            if (!tile.IsEmpty && tile.placedComponent != ignore)
                return false;
        }

        return true;
    }

    public bool PlaceComponentAt(FactoryComponent component, FactoryComponentData data, Vector2Int anchor, int rotationIndex)
    {
        if (component == null || data == null)
            return false;

        PlacedBuildComponent placed = new PlacedBuildComponent(component, data, anchor, rotationIndex);
        return PlacePlacedComponent(placed);
    }

    public bool PlacePlacedComponent(PlacedBuildComponent placed)
    {
        if (placed == null || placed.component == null || placed.data == null)
            return false;

        placed.rotationIndex = NormalizeRotationIndex(placed.rotationIndex);

        if (!CanPlaceComponentAt(placed.data, placed.anchor, placed.rotationIndex, placed))
            return false;

        foreach (Vector2Int tilePos in placed.GetOccupiedTiles())
        {
            GridTile tile = GetTile(tilePos);
            if (tile == null)
                return false;

            tile.SetPlacedComponent(placed);
        }

        return true;
    }

    public void RemovePlacedComponent(PlacedBuildComponent placed)
    {
        if (placed == null)
            return;

        foreach (Vector2Int tilePos in placed.GetOccupiedTiles())
        {
            GridTile tile = GetTile(tilePos);
            if (tile != null && tile.placedComponent == placed)
                tile.ClearPlacedComponent();
        }
    }

    public void ClearAllPlacedComponents()
    {
        List<PlacedBuildComponent> placedComponents = new(GetAllPlacedComponents());

        foreach (PlacedBuildComponent placed in placedComponents)
            RemovePlacedComponent(placed);
    }

    public IEnumerable<PlacedBuildComponent> GetAllPlacedComponents()
    {
        HashSet<PlacedBuildComponent> seen = new();

        if (tiles == null)
            yield break;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                GridTile tile = tiles[x, y];
                if (tile == null || tile.placedComponent == null)
                    continue;

                if (seen.Add(tile.placedComponent))
                    yield return tile.placedComponent;
            }
        }
    }

    private int NormalizeRotationIndex(int rotationIndex)
    {
        return ((rotationIndex % 4) + 4) % 4;
    }

    #endregion

    #region Wire Data Queries

    public WireTileData GetWire(Vector2Int gridPos)
    {
        if (wires == null)
        {
            Debug.LogError("TileGrid.GetWire called before wire grid was built", this);
            return null;
        }

        if (!InBounds(gridPos))
            return null;

        return wires[gridPos.x, gridPos.y];
    }

    public WireTileData[,] CreateWireLayoutSnapshot()
    {
        if (wires == null)
            return null;

        WireTileData[,] snapshot = new WireTileData[columns, rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                WireTileData source = wires[x, y];

                snapshot[x, y] = new WireTileData();

                if (source == null)
                    continue;

                snapshot[x, y].hasWire = source.hasWire;
                snapshot[x, y].connections = source.connections;
            }
        }

        return snapshot;
    }

    public bool HasWireAt(Vector2Int gridPos)
    {
        WireTileData wire = GetWire(gridPos);
        return wire != null && wire.hasWire;
    }

    public WireSideFlags GetWireConnections(Vector2Int gridPos)
    {
        WireTileData wire = GetWire(gridPos);
        return wire != null ? wire.connections : WireSideFlags.None;
    }

    #endregion

    #region Wire Editing

    public void EnsureWireExists(Vector2Int gridPos)
    {
        WireTileData wire = GetWire(gridPos);
        if (wire == null)
            return;

        wire.hasWire = true;
        RefreshWireVisual(gridPos);
    }

    public void SetWireConnection(Vector2Int gridPos, WireSideFlags side, bool enabled)
    {
        WireTileData wire = GetWire(gridPos);
        if (wire == null || !wire.hasWire)
            return;

        wire.SetConnection(side, enabled);
        RefreshWireVisual(gridPos);
    }

    public void ClearWireAt(Vector2Int gridPos)
    {
        WireTileData wire = GetWire(gridPos);
        if (wire == null)
            return;

        wire.Clear();
        RefreshWireVisual(gridPos);
    }

    public void ClearAllWires()
    {
        if (wires == null)
            return;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
                wires[x, y].Clear();
        }

        RefreshAllWireVisuals();
    }

    public void RemoveWireAndConnections(Vector2Int gridPos)
    {
        if (!InBounds(gridPos))
            return;

        DisconnectAllNeighbors(gridPos);
        ClearWireAt(gridPos);
    }

    public void ConnectAdjacentWires(Vector2Int a, Vector2Int b)
    {
        if (!TryGetAdjacentWireSides(a, b, out WireSideFlags sideAB, out WireSideFlags sideBA))
            return;

        EnsureWireExists(a);
        EnsureWireExists(b);

        SetWireConnection(a, sideAB, true);
        SetWireConnection(b, sideBA, true);

        RefreshWireVisual(a);
        RefreshWireVisual(b);
    }

    public void DisconnectAdjacentWires(Vector2Int a, Vector2Int b)
    {
        if (!TryGetAdjacentWireSides(a, b, out WireSideFlags sideAB, out WireSideFlags sideBA))
            return;

        SetWireConnection(a, sideAB, false);
        SetWireConnection(b, sideBA, false);

        RefreshWireVisual(a);
        RefreshWireVisual(b);
    }

    public void DisconnectAllNeighbors(Vector2Int gridPos)
    {
        foreach (Vector2Int neighbor in GetOrthogonalNeighbors(gridPos))
            DisconnectAdjacentWires(gridPos, neighbor);
    }

    private IEnumerable<Vector2Int> GetOrthogonalNeighbors(Vector2Int gridPos)
    {
        foreach (Vector2Int offset in OrthogonalNeighborOffsets)
        {
            Vector2Int neighbor = gridPos + offset;
            if (InBounds(neighbor))
                yield return neighbor;
        }
    }

    private bool TryGetAdjacentWireSides(Vector2Int a, Vector2Int b, out WireSideFlags sideAB, out WireSideFlags sideBA)
    {
        sideAB = WireSideFlags.None;
        sideBA = WireSideFlags.None;

        if (!InBounds(a) || !InBounds(b))
            return false;

        Vector2Int delta = b - a;
        if (!TryDeltaToSide(delta, out sideAB))
            return false;

        sideBA = GetOppositeSide(sideAB);
        return true;
    }

    private bool TryDeltaToSide(Vector2Int delta, out WireSideFlags side)
    {
        if (delta == new Vector2Int(0, -1))
        {
            side = WireSideFlags.Up;
            return true;
        }

        if (delta == new Vector2Int(-1, 0))
        {
            side = WireSideFlags.Left;
            return true;
        }

        if (delta == new Vector2Int(0, 1))
        {
            side = WireSideFlags.Down;
            return true;
        }

        if (delta == new Vector2Int(1, 0))
        {
            side = WireSideFlags.Right;
            return true;
        }

        side = WireSideFlags.None;
        return false;
    }

    private WireSideFlags GetOppositeSide(WireSideFlags side)
    {
        return side switch
        {
            WireSideFlags.Up => WireSideFlags.Down,
            WireSideFlags.Left => WireSideFlags.Right,
            WireSideFlags.Down => WireSideFlags.Up,
            WireSideFlags.Right => WireSideFlags.Left,
            _ => WireSideFlags.None
        };
    }

    #endregion

    #region Wire Visuals

    public void SetWireOverlayVisible(bool visible)
    {
        wireOverlayVisible = visible;
        RefreshAllWireVisuals();
    }

    public void SetDisplayedActiveWireState(bool[,] activeTiles)
    {
        EnsureActiveWireDisplayBuffer();

        if (activeTiles == null)
        {
            ClearDisplayedActiveWireState();
            return;
        }

        if (activeTiles.GetLength(0) != columns || activeTiles.GetLength(1) != rows)
        {
            Debug.LogWarning("TileGrid: active wire display state size mismatch", this);
            ClearDisplayedActiveWireState();
            return;
        }

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
                activeWireTilesForDisplay[x, y] = activeTiles[x, y];
        }

        RefreshAllWireVisuals();
    }

    public void ClearDisplayedActiveWireState()
    {
        EnsureActiveWireDisplayBuffer();
        System.Array.Clear(activeWireTilesForDisplay, 0, activeWireTilesForDisplay.Length);
        RefreshAllWireVisuals();
    }

    public void RefreshAllWireVisuals()
    {
        if (tiles == null)
            return;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
                RefreshWireVisual(new Vector2Int(x, y));
        }
    }

    private void EnsureActiveWireDisplayBuffer()
    {
        if (activeWireTilesForDisplay == null || activeWireTilesForDisplay.GetLength(0) != columns || activeWireTilesForDisplay.GetLength(1) != rows)
        {
            activeWireTilesForDisplay = new bool[columns, rows];
        }
    }

    private void RefreshWireVisual(Vector2Int gridPos)
    {
        GridTile tile = GetTile(gridPos);
        if (tile == null)
            return;

        bool hasWire = HasWireAt(gridPos);
        bool isActive = activeWireTilesForDisplay != null && activeWireTilesForDisplay[gridPos.x, gridPos.y];

        if (!hasWire || !wireOverlayVisible)
        {
            tile.RefreshWireVisual(null, 0f, false);
            return;
        }

        WireSideFlags connections = GetWireConnections(gridPos);
        (WireVisualShape shape, float rotation) = GetWireVisualDefinition(connections);
        Sprite sprite = GetWireSprite(shape, isActive);

        tile.RefreshWireVisual(sprite, rotation, true);
    }

    private (WireVisualShape shape, float rotation) GetWireVisualDefinition(WireSideFlags connections)
    {
        int count = CountConnections(connections);

        if (count == 0)
            return (WireVisualShape.Isolated, 0f);

        if (count == 1)
            return GetSingleConnectionVisual(connections);

        if (count == 2)
            return GetTwoConnectionVisual(connections);

        if (count == 3)
            return GetThreeConnectionVisual(connections);

        return (WireVisualShape.Cross, 0f);
    }

    private (WireVisualShape shape, float rotation) GetSingleConnectionVisual(WireSideFlags connections)
    {
        if ((connections & WireSideFlags.Up) != 0)
            return (WireVisualShape.Single, 0f);

        if ((connections & WireSideFlags.Right) != 0)
            return (WireVisualShape.Single, -90f);

        if ((connections & WireSideFlags.Down) != 0)
            return (WireVisualShape.Single, 180f);

        return (WireVisualShape.Single, 90f);
    }

    private (WireVisualShape shape, float rotation) GetTwoConnectionVisual(WireSideFlags connections)
    {
        bool up = HasFlag(connections, WireSideFlags.Up);
        bool left = HasFlag(connections, WireSideFlags.Left);
        bool down = HasFlag(connections, WireSideFlags.Down);
        bool right = HasFlag(connections, WireSideFlags.Right);

        if (up && down)
            return (WireVisualShape.Straight, 0f);

        if (left && right)
            return (WireVisualShape.Straight, 90f);

        if (up && right)
            return (WireVisualShape.Corner, 0f);

        if (right && down)
            return (WireVisualShape.Corner, -90f);

        if (down && left)
            return (WireVisualShape.Corner, 180f);

        return (WireVisualShape.Corner, 90f);
    }

    private (WireVisualShape shape, float rotation) GetThreeConnectionVisual(WireSideFlags connections)
    {
        bool up = HasFlag(connections, WireSideFlags.Up);
        bool left = HasFlag(connections, WireSideFlags.Left);
        bool down = HasFlag(connections, WireSideFlags.Down);

        if (!down)
            return (WireVisualShape.TJunction, 0f);

        if (!left)
            return (WireVisualShape.TJunction, -90f);

        if (!up)
            return (WireVisualShape.TJunction, 180f);

        return (WireVisualShape.TJunction, 90f);
    }

    private Sprite GetWireSprite(WireVisualShape shape, bool isActive)
    {
        return shape switch
        {
            WireVisualShape.Isolated => isActive ? activeWireIsolatedSprite : inactiveWireIsolatedSprite,
            WireVisualShape.Straight => isActive ? activeWireStraightSprite : inactiveWireStraightSprite,
            WireVisualShape.Single => isActive ? activeWireSingleSprite : inactiveWireSingleSprite,
            WireVisualShape.Corner => isActive ? activeWireCornerSprite : inactiveWireCornerSprite,
            WireVisualShape.TJunction => isActive ? activeWireTJunctionSprite : inactiveWireTJunctionSprite,
            WireVisualShape.Cross => isActive ? activeWireCrossSprite : inactiveWireCrossSprite,
            _ => null
        };
    }

    private int CountConnections(WireSideFlags flags)
    {
        int count = 0;

        if (HasFlag(flags, WireSideFlags.Up))
            count++;

        if (HasFlag(flags, WireSideFlags.Left))
            count++;

        if (HasFlag(flags, WireSideFlags.Down))
            count++;

        if (HasFlag(flags, WireSideFlags.Right))
            count++;

        return count;
    }

    private bool HasFlag(WireSideFlags flags, WireSideFlags flag)
    {
        return (flags & flag) != 0;
    }

    #endregion

    #region Save Import and Export

    public void ApplyWireEntries(List<WireSaveEntry> entries)
    {
        ClearAllWires();

        if (entries == null)
            return;

        foreach (WireSaveEntry entry in entries)
        {
            if (entry == null)
                continue;

            Vector2Int pos = new Vector2Int(entry.x, entry.y);
            if (!InBounds(pos))
                continue;

            WireTileData wire = wires[pos.x, pos.y];
            wire.hasWire = entry.hasWire;
            wire.connections = entry.hasWire ? (WireSideFlags)entry.connections : WireSideFlags.None;
        }

        RefreshAllWireVisuals();
    }

    public List<WireSaveEntry> ExportWireEntries()
    {
        List<WireSaveEntry> entries = new();

        if (wires == null)
            return entries;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                WireTileData wire = wires[x, y];
                if (wire == null || !wire.hasWire)
                    continue;

                entries.Add(new WireSaveEntry
                {
                    x = x,
                    y = y,
                    hasWire = true,
                    connections = (int)wire.connections
                });
            }
        }

        return entries;
    }

    public List<PlacedComponentSaveEntry> ExportPlacedComponentEntries()
    {
        List<PlacedComponentSaveEntry> entries = new();

        foreach (PlacedBuildComponent placed in GetAllPlacedComponents())
        {
            if (placed == null || placed.data == null)
                continue;

            entries.Add(new PlacedComponentSaveEntry
            {
                componentId = placed.data.id,
                anchor = placed.anchor,
                rotationIndex = placed.rotationIndex,
                supplyItemIdOverride = placed.supplyItemIdOverride
            });
        }

        return entries;
    }

    #endregion
}