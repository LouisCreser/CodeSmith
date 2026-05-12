using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct SimulationProgressDisplayEntry
{
    public readonly Vector2Int Tile;
    public readonly int CompletedSegments;
    public readonly int TotalSegments;
    public readonly bool IsError;

    public bool IsValid => TotalSegments > 0;

    public SimulationProgressDisplayEntry(Vector2Int tile, int completedSegments, int totalSegments, bool isError = false)
    {
        Tile = tile;
        TotalSegments = Mathf.Max(0, totalSegments);
        CompletedSegments = Mathf.Clamp(completedSegments, 0, TotalSegments);
        IsError = isError;
    }
}

public class FactorySimulation
{
    public readonly int width;
    public readonly int height;

    public readonly string targetItemId;

    public ItemInstance[,] items;
    public PlacedComponentInstance[,] occupiedBy;
    public WireTileData[,] wires;
    public bool[,] activeLogicTiles;

    public readonly List<PlacedComponentInstance> components = new();

    public int TickIndex { get; private set; }
    public int ProducedCount { get; private set; }

    public bool HasError { get; private set; }
    public string ErrorMessage { get; private set; }

    private readonly HashSet<Vector2Int> pendingButtonPresses = new();
    private readonly HashSet<Vector2Int> activeButtonPressesThisTick = new();

    private readonly Dictionary<Vector2Int, SimulationProgressDisplayEntry> completedProgressHolds = new();
    private readonly List<CompletedAnvilProgressHoldCandidate> completedAnvilProgressHoldCandidates = new();
    private readonly List<CompletedFurnaceReleaseProgressHoldCandidate> completedFurnaceReleaseProgressHoldCandidates = new();

    private struct ResolvedMove
    {
        public Vector2Int from;
        public Vector2Int to;
        public Vector2Int logicalFrom;
        public ItemInstance item;
        public bool clearsSource;

        public ResolvedMove(Vector2Int from, Vector2Int to, Vector2Int logicalFrom, ItemInstance item, bool clearsSource)
        {
            this.from = from;
            this.to = to;
            this.logicalFrom = logicalFrom;
            this.item = item;
            this.clearsSource = clearsSource;
        }
    }

    private readonly struct CompletedAnvilProgressHoldCandidate
    {
        public readonly ItemInstance Item;
        public readonly int TotalSegments;

        public CompletedAnvilProgressHoldCandidate(ItemInstance item, int totalSegments)
        {
            Item = item;
            TotalSegments = Mathf.Max(1, totalSegments);
        }
    }

    private readonly struct CompletedFurnaceReleaseProgressHoldCandidate
    {
        public readonly Vector2Int TargetTile;
        public readonly int TotalSegments;

        public CompletedFurnaceReleaseProgressHoldCandidate(Vector2Int targetTile, int totalSegments)
        {
            TargetTile = targetTile;
            TotalSegments = Mathf.Max(1, totalSegments);
        }
    }

    private sealed class IntentBuckets
    {
        public readonly List<ResolvedMove> resolvedMoves = new();
        public readonly List<SpawnIntent> spawnIntents = new();
        public readonly List<ConsumeIntent> consumeIntents = new();
        public readonly List<FurnaceReleaseIntent> furnaceReleaseIntents = new();
        public readonly List<SplitterReleaseIntent> splitterReleaseIntents = new();
        public readonly List<AnvilStrikeIntent> anvilStrikeIntents = new();
    }

    public FactorySimulation(int width, int height, string targetItemId)
    {
        this.width = width;
        this.height = height;
        this.targetItemId = targetItemId;

        items = new ItemInstance[width, height];
        occupiedBy = new PlacedComponentInstance[width, height];
        wires = new WireTileData[width, height];
        activeLogicTiles = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                wires[x, y] = new WireTileData();
        }
    }

    #region Public API

    public bool InBounds(Vector2Int p)
    {
        return p.x >= 0 && p.x < width && p.y >= 0 && p.y < height;
    }

    public ItemInstance GetItemAt(Vector2Int tile)
    {
        return InBounds(tile) ? items[tile.x, tile.y] : null;
    }

    public PlacedComponentInstance GetComponentAt(Vector2Int tile)
    {
        return InBounds(tile) ? occupiedBy[tile.x, tile.y] : null;
    }

    public bool[,] GetActiveLogicTilesForDisplay()
    {
        return activeLogicTiles;
    }

    public List<SimulationProgressDisplayEntry> GetProgressDisplayEntries()
    {
        List<SimulationProgressDisplayEntry> entries = new();
        HashSet<Vector2Int> occupiedProgressTiles = new();

        AddFurnaceProgressEntries(entries, occupiedProgressTiles);
        AddAnvilProgressEntries(entries, occupiedProgressTiles);
        AddCompletedProgressHoldEntries(entries, occupiedProgressTiles);

        return entries;
    }

    public bool TryAddComponent(PlacedComponentInstance instance)
    {
        if (instance == null)
            return false;

        foreach (Vector2Int cell in instance.GetOccupiedTiles())
        {
            if (!InBounds(cell))
                return false;

            if (occupiedBy[cell.x, cell.y] != null)
                return false;
        }

        components.Add(instance);

        foreach (Vector2Int cell in instance.GetOccupiedTiles())
            occupiedBy[cell.x, cell.y] = instance;

        return true;
    }

    public void QueueButtonPress(Vector2Int tile)
    {
        if (InBounds(tile))
            pendingButtonPresses.Add(tile);
    }

    public bool WasButtonPressedThisTick(Vector2Int tile)
    {
        return activeButtonPressesThisTick.Contains(tile);
    }

    public void SetWireLayout(WireTileData[,] sourceWires)
    {
        if (sourceWires == null ||
            sourceWires.GetLength(0) != width ||
            sourceWires.GetLength(1) != height)
        {
            Fail("Wire layout size mismatch.");
            return;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                WireTileData source = sourceWires[x, y];
                WireTileData destination = wires[x, y];

                if (source == null)
                {
                    destination.Clear();
                    continue;
                }

                destination.hasWire = source.hasWire;
                destination.connections = source.connections;
            }
        }
    }

    public bool HasWireAt(Vector2Int tile)
    {
        return InBounds(tile) && wires[tile.x, tile.y] != null && wires[tile.x, tile.y].hasWire;
    }

    public bool IsLogicActiveAt(Vector2Int tile)
    {
        return InBounds(tile) && activeLogicTiles[tile.x, tile.y];
    }

    public bool IsComponentLogicActive(PlacedComponentInstance component)
    {
        if (component == null || component.data == null || component.data.logicInputTiles == null)
            return false;

        foreach (Vector2Int tile in component.GetLogicInputTiles())
        {
            if (IsLogicActiveAt(tile))
                return true;
        }

        return false;
    }

    public void ClearItems()
    {
        Array.Clear(items, 0, items.Length);
        Array.Clear(activeLogicTiles, 0, activeLogicTiles.Length);

        pendingButtonPresses.Clear();
        activeButtonPressesThisTick.Clear();
        completedProgressHolds.Clear();
        completedAnvilProgressHoldCandidates.Clear();
        completedFurnaceReleaseProgressHoldCandidates.Clear();

        TickIndex = 0;
        ProducedCount = 0;
        HasError = false;
        ErrorMessage = null;
    }

    public void Tick()
    {
        if (HasError)
            return;

        ClearOneTickProgressState();

        SortComponentsForDeterministicTickOrder();
        ResolveLogicPhase();

        List<SimIntent> intents = CollectComponentIntents();
        ValidateAndCommit(intents);

        activeButtonPressesThisTick.Clear();

        if (!HasError)
            TickIndex++;
    }

    public void Fail(string message)
    {
        if (HasError)
            return;

        HasError = true;
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "Simulation error."
            : message;
    }

    #endregion

    #region Progress Bars

    private void ClearOneTickProgressState()
    {
        completedProgressHolds.Clear();
        completedAnvilProgressHoldCandidates.Clear();
        completedFurnaceReleaseProgressHoldCandidates.Clear();
    }

    private void AddFurnaceProgressEntries(
        List<SimulationProgressDisplayEntry> entries,
        HashSet<Vector2Int> occupiedProgressTiles)
    {
        foreach (PlacedComponentInstance component in components)
        {
            if (component == null || component.data == null || component.data.type != ComponentType.Furnace)
                continue;

            if (!component.TryGetFurnaceProgress(out int completedSegments, out int totalSegments, out bool isError))
                continue;

            SimulationProgressDisplayEntry entry = new SimulationProgressDisplayEntry(
                component.anchor,
                completedSegments,
                totalSegments,
                isError);

            AddProgressEntryIfTileAvailable(entries, occupiedProgressTiles, entry);
        }
    }

    private void AddAnvilProgressEntries(
        List<SimulationProgressDisplayEntry> entries,
        HashSet<Vector2Int> occupiedProgressTiles)
    {
        foreach (PlacedComponentInstance component in components)
        {
            if (component == null || component.data == null || component.data.type != ComponentType.Anvil)
                continue;

            ComponentSimulationRules rules = component.simulationRules;
            if (rules == null || rules.anvilRecipeBook == null)
                continue;

            Vector2Int itemTile = component.GetAnvilItemTile();
            if (!InBounds(itemTile))
                continue;

            ItemInstance item = GetItemAt(itemTile);
            if (item == null || item.hammerProgress <= 0)
                continue;

            if (!rules.anvilRecipeBook.TryGetRecipe(item.itemId, out AnvilRecipeEntry recipe) || recipe == null)
                continue;

            int totalSegments = Mathf.Max(1, recipe.requiredStrikes);
            int completedSegments = Mathf.Clamp(item.hammerProgress, 0, totalSegments);

            if (completedSegments <= 0 || completedSegments >= totalSegments)
                continue;

            SimulationProgressDisplayEntry entry = new SimulationProgressDisplayEntry(
                itemTile,
                completedSegments,
                totalSegments);

            AddProgressEntryIfTileAvailable(entries, occupiedProgressTiles, entry);
        }
    }

    private void AddCompletedProgressHoldEntries(
        List<SimulationProgressDisplayEntry> entries,
        HashSet<Vector2Int> occupiedProgressTiles)
    {
        foreach (SimulationProgressDisplayEntry entry in completedProgressHolds.Values)
            AddProgressEntryIfTileAvailable(entries, occupiedProgressTiles, entry);
    }

    private void AddProgressEntryIfTileAvailable(
        List<SimulationProgressDisplayEntry> entries,
        HashSet<Vector2Int> occupiedProgressTiles,
        SimulationProgressDisplayEntry entry)
    {
        if (!entry.IsValid)
            return;

        if (!occupiedProgressTiles.Add(entry.Tile))
            return;

        entries.Add(entry);
    }

    private void AddCompletedProgressHold(Vector2Int tile, int totalSegments)
    {
        if (!InBounds(tile))
            return;

        int safeTotalSegments = Mathf.Max(1, totalSegments);

        completedProgressHolds[tile] = new SimulationProgressDisplayEntry(
            tile,
            safeTotalSegments,
            safeTotalSegments);
    }

    private void ResolveCompletedProgressHolds()
    {
        ResolveCompletedFurnaceReleaseProgressHolds();
        ResolveCompletedAnvilProgressHolds();
    }

    private void ResolveCompletedFurnaceReleaseProgressHolds()
    {
        foreach (CompletedFurnaceReleaseProgressHoldCandidate candidate in completedFurnaceReleaseProgressHoldCandidates)
        {
            if (!InBounds(candidate.TargetTile))
                continue;

            if (GetItemAt(candidate.TargetTile) == null)
                continue;

            AddCompletedProgressHold(candidate.TargetTile, candidate.TotalSegments);
        }
    }

    private void ResolveCompletedAnvilProgressHolds()
    {
        foreach (CompletedAnvilProgressHoldCandidate candidate in completedAnvilProgressHoldCandidates)
        {
            if (candidate.Item == null)
                continue;

            if (TryFindItemTile(candidate.Item, out Vector2Int tile))
                AddCompletedProgressHold(tile, candidate.TotalSegments);
        }
    }

    private bool TryFindItemTile(ItemInstance targetItem, out Vector2Int tile)
    {
        tile = Vector2Int.zero;

        if (targetItem == null)
            return false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (ReferenceEquals(items[x, y], targetItem))
                {
                    tile = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region Tick Pipeline

    private void SortComponentsForDeterministicTickOrder()
    {
        components.Sort((a, b) =>
        {
            int yCompare = a.anchor.y.CompareTo(b.anchor.y);
            if (yCompare != 0)
                return yCompare;

            return a.anchor.x.CompareTo(b.anchor.x);
        });
    }

    private List<SimIntent> CollectComponentIntents()
    {
        List<SimIntent> intents = new List<SimIntent>(128);

        foreach (PlacedComponentInstance component in components)
            component.CollectIntents(this, intents);

        return intents;
    }

    private void ValidateAndCommit(List<SimIntent> intents)
    {
        IntentBuckets buckets = BuildIntentBuckets(intents);
        if (HasError)
            return;

        ValidateAnvilEntryDirections(buckets.resolvedMoves);
        if (HasError)
            return;

        ApplyAnvilStrikes(buckets.anvilStrikeIntents);
        if (HasError)
            return;

        HashSet<Vector2Int> vacatedTiles = GetVacatedTiles(buckets.resolvedMoves);

        ValidateMoveTargets(buckets.resolvedMoves, vacatedTiles);
        if (HasError)
            return;

        ValidateSpawnTargets(buckets.spawnIntents, buckets.consumeIntents, vacatedTiles, buckets.resolvedMoves);
        if (HasError)
            return;

        CommitMovesAndSpawns(buckets.resolvedMoves, buckets.spawnIntents);
        UpdateInternalComponentReleaseState(buckets.furnaceReleaseIntents, buckets.splitterReleaseIntents);

        AbsorbFurnaceInputs(buckets.resolvedMoves);
        if (HasError)
            return;

        ValidateSplitterInputDirections(buckets.resolvedMoves);
        if (HasError)
            return;

        ValidateOutputBinInputs(buckets.resolvedMoves);
        if (HasError)
            return;

        ConsumeOutputItems(buckets.consumeIntents);
        if (HasError)
            return;

        ResolveCompletedProgressHolds();
    }

    #endregion

    #region Logic Resolution

    private void ResolveLogicPhase()
    {
        Array.Clear(activeLogicTiles, 0, activeLogicTiles.Length);

        activeButtonPressesThisTick.Clear();
        foreach (Vector2Int pressed in pendingButtonPresses)
            activeButtonPressesThisTick.Add(pressed);

        pendingButtonPresses.Clear();

        Queue<Vector2Int> frontier = new();
        HashSet<Vector2Int> visited = new();

        foreach (Vector2Int seed in CollectLogicSeeds())
        {
            if (!HasWireAt(seed))
                continue;

            if (visited.Add(seed))
            {
                activeLogicTiles[seed.x, seed.y] = true;
                frontier.Enqueue(seed);
            }
        }

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();

            foreach (Vector2Int neighbor in GetConnectedWireNeighbors(current))
            {
                if (!visited.Add(neighbor))
                    continue;

                activeLogicTiles[neighbor.x, neighbor.y] = true;
                frontier.Enqueue(neighbor);
            }
        }
    }

    private IEnumerable<Vector2Int> CollectLogicSeeds()
    {
        foreach (PlacedComponentInstance component in components)
        {
            foreach (Vector2Int seed in component.GetLogicOutputSeedTiles(this))
                yield return seed;
        }
    }

    private IEnumerable<Vector2Int> GetConnectedWireNeighbors(Vector2Int tile)
    {
        if (!HasWireAt(tile))
            yield break;

        WireTileData current = wires[tile.x, tile.y];
        if (current == null || !current.hasWire)
            yield break;

        foreach (WireSideFlags side in EnumerateConnectedSides(current.connections))
        {
            Vector2Int neighbor = tile + SideToDelta(side);
            if (!InBounds(neighbor) || !HasWireAt(neighbor))
                continue;

            WireTileData neighborWire = wires[neighbor.x, neighbor.y];
            WireSideFlags opposite = GetOppositeSide(side);

            if (neighborWire != null && neighborWire.hasWire && neighborWire.HasConnection(opposite))
                yield return neighbor;
        }
    }

    private IEnumerable<WireSideFlags> EnumerateConnectedSides(WireSideFlags flags)
    {
        if ((flags & WireSideFlags.Up) != 0)
            yield return WireSideFlags.Up;

        if ((flags & WireSideFlags.Left) != 0)
            yield return WireSideFlags.Left;

        if ((flags & WireSideFlags.Down) != 0)
            yield return WireSideFlags.Down;

        if ((flags & WireSideFlags.Right) != 0)
            yield return WireSideFlags.Right;
    }

    private Vector2Int SideToDelta(WireSideFlags side)
    {
        return side switch
        {
            WireSideFlags.Up => new Vector2Int(0, -1),
            WireSideFlags.Left => new Vector2Int(-1, 0),
            WireSideFlags.Down => new Vector2Int(0, 1),
            WireSideFlags.Right => new Vector2Int(1, 0),
            _ => Vector2Int.zero
        };
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

    #region Intent Resolution

    private IntentBuckets BuildIntentBuckets(List<SimIntent> intents)
    {
        IntentBuckets buckets = new();

        if (intents == null)
            return buckets;

        foreach (SimIntent intent in intents)
        {
            switch (intent)
            {
                case AnvilStrikeIntent strike:
                    buckets.anvilStrikeIntents.Add(strike);
                    break;

                case MoveIntent move:
                    AddResolvedMoveIntent(move, buckets);
                    break;

                case SupplyMoveIntent supplyMove:
                    AddResolvedSupplyMoveIntent(supplyMove, buckets);
                    break;

                case FurnaceReleaseIntent furnaceRelease:
                    AddResolvedFurnaceReleaseIntent(furnaceRelease, buckets);
                    break;

                case SplitterReleaseIntent splitterRelease:
                    AddResolvedSplitterReleaseIntent(splitterRelease, buckets);
                    break;

                case SpawnIntent spawn:
                    buckets.spawnIntents.Add(spawn);
                    break;

                case ConsumeIntent consume:
                    buckets.consumeIntents.Add(consume);
                    break;
            }

            if (HasError)
                return buckets;
        }

        return buckets;
    }

    private void AddResolvedMoveIntent(MoveIntent move, IntentBuckets buckets)
    {
        if (!InBounds(move.from) || !InBounds(move.to) || !InBounds(move.logicalFrom))
        {
            Fail("Move out of bounds.");
            return;
        }

        ItemInstance item = items[move.from.x, move.from.y];
        if (item == null)
        {
            Fail("Move attempted from an empty tile.");
            return;
        }

        buckets.resolvedMoves.Add(new ResolvedMove(
            move.from,
            move.to,
            move.logicalFrom,
            item,
            true));
    }

    private void AddResolvedSupplyMoveIntent(SupplyMoveIntent supplyMove, IntentBuckets buckets)
    {
        if (!InBounds(supplyMove.from) || !InBounds(supplyMove.to) || !InBounds(supplyMove.logicalFrom))
        {
            Fail("Supply move out of bounds.");
            return;
        }

        ItemInstance existing = items[supplyMove.from.x, supplyMove.from.y];
        ItemInstance itemToMove = existing != null
            ? existing
            : new ItemInstance(supplyMove.itemId);

        bool sourceHasItem = existing != null;

        buckets.resolvedMoves.Add(new ResolvedMove(
            supplyMove.from,
            supplyMove.to,
            supplyMove.logicalFrom,
            itemToMove,
            sourceHasItem));
    }

    private void AddResolvedFurnaceReleaseIntent(FurnaceReleaseIntent release, IntentBuckets buckets)
    {
        if (!InBounds(release.from) || !InBounds(release.to) || !InBounds(release.logicalFrom))
        {
            Fail("Furnace release out of bounds.");
            return;
        }

        if (release.furnace != null &&
            release.furnace.TryGetFurnaceProgress(out int completedSegments, out int totalSegments, out bool isError) &&
            !isError &&
            completedSegments >= totalSegments)
        {
            completedFurnaceReleaseProgressHoldCandidates.Add(
                new CompletedFurnaceReleaseProgressHoldCandidate(release.to, totalSegments));
        }

        buckets.resolvedMoves.Add(new ResolvedMove(
            release.from,
            release.to,
            release.logicalFrom,
            new ItemInstance(release.itemId),
            false));

        buckets.furnaceReleaseIntents.Add(release);
    }

    private void AddResolvedSplitterReleaseIntent(SplitterReleaseIntent splitterRelease, IntentBuckets buckets)
    {
        if (!InBounds(splitterRelease.from) || !InBounds(splitterRelease.to) || !InBounds(splitterRelease.logicalFrom))
        {
            Fail("Splitter release out of bounds.");
            return;
        }

        ItemInstance item = items[splitterRelease.from.x, splitterRelease.from.y];
        if (item == null)
        {
            Fail("Splitter move attempted from an empty tile.");
            return;
        }

        buckets.resolvedMoves.Add(new ResolvedMove(
            splitterRelease.from,
            splitterRelease.to,
            splitterRelease.logicalFrom,
            item,
            true));

        buckets.splitterReleaseIntents.Add(splitterRelease);
    }

    #endregion

    #region Direction Validation

    private void ValidateAnvilEntryDirections(List<ResolvedMove> resolvedMoves)
    {
        foreach (PlacedComponentInstance component in components)
        {
            if (component.data == null || component.data.type != ComponentType.Anvil)
                continue;

            Vector2Int itemTile = component.GetAnvilItemTile();
            Vector2Int expectedSource = component.GetAnvilInputSourceTile();

            bool movedIntoAnvilThisTick = false;
            bool movedFromExpectedSource = false;
            bool movedFromWrongDirection = false;

            foreach (ResolvedMove move in resolvedMoves)
            {
                if (move.to != itemTile)
                    continue;

                movedIntoAnvilThisTick = true;

                if (move.logicalFrom == expectedSource)
                    movedFromExpectedSource = true;
                else
                    movedFromWrongDirection = true;
            }

            if (movedFromWrongDirection || (movedIntoAnvilThisTick && !movedFromExpectedSource))
            {
                Fail("An item entered an anvil from the wrong direction.");
                return;
            }
        }
    }

    private void ValidateSplitterInputDirections(List<ResolvedMove> resolvedMoves)
    {
        foreach (PlacedComponentInstance component in components)
        {
            if (component.data == null || component.data.type != ComponentType.Splitter)
                continue;

            for (int lane = 0; lane < 2; lane++)
            {
                Vector2Int laneTile = component.GetSplitterLaneTile(lane);
                if (!InBounds(laneTile))
                    continue;

                ItemInstance itemAtLane = items[laneTile.x, laneTile.y];
                if (itemAtLane == null)
                    continue;

                Vector2Int expectedSource = component.GetSplitterInputSourceTile(lane);

                bool movedIntoLaneThisTick = false;
                bool movedFromExpectedSource = false;
                bool movedFromWrongDirection = false;

                foreach (ResolvedMove move in resolvedMoves)
                {
                    if (move.to != laneTile)
                        continue;

                    movedIntoLaneThisTick = true;

                    if (move.logicalFrom == expectedSource)
                        movedFromExpectedSource = true;
                    else
                        movedFromWrongDirection = true;
                }

                if (movedFromWrongDirection || (movedIntoLaneThisTick && !movedFromExpectedSource))
                {
                    Fail("An item entered a splitter from the wrong direction.");
                    return;
                }
            }
        }
    }

    private void ValidateOutputBinInputs(List<ResolvedMove> resolvedMoves)
    {
        foreach (PlacedComponentInstance component in components)
        {
            if (component.data == null || component.data.type != ComponentType.OutputBin)
                continue;

            Vector2Int outputTile = component.anchor;
            if (!InBounds(outputTile))
                continue;

            ItemInstance itemAtOutput = items[outputTile.x, outputTile.y];
            if (itemAtOutput == null)
                continue;

            Vector2Int expectedSource = component.GetOutputBinInputSourceTile();

            bool movedIntoOutputThisTick = false;
            bool movedFromExpectedSource = false;
            bool movedFromWrongDirection = false;

            foreach (ResolvedMove move in resolvedMoves)
            {
                if (move.to != outputTile)
                    continue;

                movedIntoOutputThisTick = true;

                if (move.logicalFrom == expectedSource)
                    movedFromExpectedSource = true;
                else
                    movedFromWrongDirection = true;
            }

            if (movedFromWrongDirection || !movedIntoOutputThisTick || !movedFromExpectedSource)
            {
                Fail("An item entered the output box from the wrong direction.");
                return;
            }
        }
    }

    #endregion

    #region Anvil Resolution

    private void ApplyAnvilStrikes(List<AnvilStrikeIntent> anvilStrikeIntents)
    {
        foreach (AnvilStrikeIntent strike in anvilStrikeIntents)
        {
            if (strike == null || strike.anvil == null || !InBounds(strike.itemTile))
                continue;

            ItemInstance item = items[strike.itemTile.x, strike.itemTile.y];
            if (item == null)
                continue;

            ComponentSimulationRules rules = strike.anvil.simulationRules;
            if (rules == null || rules.anvilRecipeBook == null)
            {
                Fail("An anvil attempted to hammer an item, but has no anvil recipe book.");
                return;
            }

            if (!rules.anvilRecipeBook.TryGetRecipe(item.itemId, out AnvilRecipeEntry recipe) || recipe == null)
            {
                Fail($"Invalid item '{item.itemId}' was hammered by an anvil.");
                return;
            }

            int totalSegments = Mathf.Max(1, recipe.requiredStrikes);

            item.hammerProgress++;

            if (item.hammerProgress >= totalSegments)
            {
                item.itemId = recipe.outputItemId;
                item.hammerProgress = 0;

                completedAnvilProgressHoldCandidates.Add(
                    new CompletedAnvilProgressHoldCandidate(item, totalSegments));
            }
        }
    }

    #endregion

    #region Movement Validation and Commit

    private HashSet<Vector2Int> GetVacatedTiles(List<ResolvedMove> resolvedMoves)
    {
        HashSet<Vector2Int> vacatedTiles = new();

        foreach (ResolvedMove move in resolvedMoves)
        {
            if (move.clearsSource)
                vacatedTiles.Add(move.from);
        }

        return vacatedTiles;
    }

    private void ValidateMoveTargets(List<ResolvedMove> resolvedMoves, HashSet<Vector2Int> vacatedTiles)
    {
        HashSet<Vector2Int> moveTargets = new();
        Dictionary<Vector2Int, Vector2Int> moveGraph = BuildMoveGraph(resolvedMoves);

        foreach (ResolvedMove move in resolvedMoves)
        {
            if (occupiedBy[move.to.x, move.to.y] == null)
            {
                Fail("Item was pushed onto the factory floor.");
                return;
            }

            if (moveTargets.Contains(move.to))
            {
                Fail("Collision: multiple items moved into the same tile.");
                return;
            }

            bool destinationOccupied = items[move.to.x, move.to.y] != null;
            bool destinationWillBeVacated = vacatedTiles.Contains(move.to);

            if (destinationOccupied && !destinationWillBeVacated)
            {
                Fail("Collision: item moved into an occupied tile.");
                return;
            }

            if (moveGraph.TryGetValue(move.to, out Vector2Int oppositeTo) && oppositeTo == move.from)
            {
                Fail("Collision: two items moved into each other.");
                return;
            }

            moveTargets.Add(move.to);
        }
    }

    private void ValidateSpawnTargets(
        List<SpawnIntent> spawnIntents,
        List<ConsumeIntent> consumeIntents,
        HashSet<Vector2Int> vacatedTiles,
        List<ResolvedMove> resolvedMoves)
    {
        HashSet<Vector2Int> moveTargets = GetMoveTargets(resolvedMoves);

        foreach (SpawnIntent spawn in spawnIntents)
        {
            if (!InBounds(spawn.at))
            {
                Fail("Spawn out of bounds.");
                return;
            }

            if (occupiedBy[spawn.at.x, spawn.at.y] == null)
            {
                Fail("Item was spawned onto the factory floor.");
                return;
            }

            bool destinationOccupied = items[spawn.at.x, spawn.at.y] != null;
            bool destinationWillBeVacated = vacatedTiles.Contains(spawn.at);
            bool destinationWillBeConsumed = TileWillBeConsumed(spawn.at, consumeIntents);

            if (destinationOccupied && !destinationWillBeVacated && !destinationWillBeConsumed)
            {
                Fail("Collision: item moved into an occupied tile.");
                return;
            }

            if (moveTargets.Contains(spawn.at))
            {
                Fail("Spawn collision: tried to spawn into a tile that an item is moving into.");
                return;
            }
        }
    }

    private Dictionary<Vector2Int, Vector2Int> BuildMoveGraph(List<ResolvedMove> resolvedMoves)
    {
        Dictionary<Vector2Int, Vector2Int> moveGraph = new();

        foreach (ResolvedMove move in resolvedMoves)
            moveGraph[move.from] = move.to;

        return moveGraph;
    }

    private HashSet<Vector2Int> GetMoveTargets(List<ResolvedMove> resolvedMoves)
    {
        HashSet<Vector2Int> moveTargets = new();

        foreach (ResolvedMove move in resolvedMoves)
            moveTargets.Add(move.to);

        return moveTargets;
    }

    private bool TileWillBeConsumed(Vector2Int tile, List<ConsumeIntent> consumeIntents)
    {
        foreach (ConsumeIntent consume in consumeIntents)
        {
            if (consume.at == tile)
                return true;
        }

        return false;
    }

    private void CommitMovesAndSpawns(List<ResolvedMove> resolvedMoves, List<SpawnIntent> spawnIntents)
    {
        foreach (ResolvedMove move in resolvedMoves)
        {
            if (move.clearsSource)
                items[move.from.x, move.from.y] = null;
        }

        foreach (SpawnIntent spawn in spawnIntents)
            items[spawn.at.x, spawn.at.y] = new ItemInstance(spawn.itemId);

        foreach (ResolvedMove move in resolvedMoves)
            items[move.to.x, move.to.y] = move.item;
    }

    #endregion

    #region Internal Component State Updates

    private void UpdateInternalComponentReleaseState(
        List<FurnaceReleaseIntent> furnaceReleaseIntents,
        List<SplitterReleaseIntent> splitterReleaseIntents)
    {
        foreach (FurnaceReleaseIntent release in furnaceReleaseIntents)
            release.furnace.ClearFurnaceItem();

        foreach (SplitterReleaseIntent splitterRelease in splitterReleaseIntents)
        {
            if (splitterRelease.wasSingleRelease)
                splitterRelease.splitter.SetNextSplitterSingleOutputLaneAfterRelease(splitterRelease.releasedOutputLane);
        }
    }

    private void AbsorbFurnaceInputs(List<ResolvedMove> resolvedMoves)
    {
        foreach (PlacedComponentInstance component in components)
        {
            if (component.data == null || component.data.type != ComponentType.Furnace)
                continue;

            Vector2Int inputTile = component.GetFurnaceInputTile();
            if (!InBounds(inputTile))
                continue;

            ItemInstance itemAtInput = items[inputTile.x, inputTile.y];
            if (itemAtInput == null)
                continue;

            Vector2Int expectedSource = inputTile - DirUtil.ToDelta(component.GetFurnaceInputDirection());

            bool movedIntoInputThisTick = false;
            bool movedFromExpectedSource = false;
            bool movedFromWrongDirection = false;

            foreach (ResolvedMove move in resolvedMoves)
            {
                if (move.to != inputTile)
                    continue;

                movedIntoInputThisTick = true;

                if (move.logicalFrom == expectedSource)
                    movedFromExpectedSource = true;
                else
                    movedFromWrongDirection = true;
            }

            if (movedFromWrongDirection || !movedIntoInputThisTick || !movedFromExpectedSource)
            {
                Fail("An item entered a furnace from the wrong direction.");
                return;
            }

            if (component.FurnaceHasItem)
            {
                Fail("A furnace was full when an item attempted to enter.");
                return;
            }

            bool loaded = component.TryLoadFurnaceItem(itemAtInput.itemId);
            if (!loaded)
            {
                Fail("Furnace failed to load absorbed item.");
                return;
            }

            items[inputTile.x, inputTile.y] = null;
        }
    }

    #endregion

    #region Output Consumption

    private void ConsumeOutputItems(List<ConsumeIntent> consumeIntents)
    {
        foreach (ConsumeIntent consume in consumeIntents)
        {
            if (!InBounds(consume.at))
            {
                Fail("Consume out of bounds.");
                return;
            }

            ItemInstance item = items[consume.at.x, consume.at.y];
            if (item == null)
                continue;

            if (item.itemId != targetItemId)
            {
                Fail($"Incorrect item entered the output box. Expected '{targetItemId}', got '{item.itemId}'.");
                return;
            }

            ProducedCount++;
            items[consume.at.x, consume.at.y] = null;
        }
    }

    #endregion
}