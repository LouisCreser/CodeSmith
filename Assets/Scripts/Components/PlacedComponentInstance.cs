using System.Collections.Generic;
using UnityEngine;

public class PlacedComponentInstance
{
    public readonly FactoryComponentData data;
    public readonly Vector2Int anchor;
    public readonly int rotationIndex;
    public readonly ComponentSimulationRules simulationRules;
    public readonly string supplyItemIdOverride;

    private string furnaceHeldItemId;
    private FurnaceRecipeEntry furnaceActiveRecipe;
    private int furnaceCookProgress;
    private int furnaceCookTotalSegments;
    private bool furnaceHeldItemHasNoRecipe;

    private int splitterNextSingleOutputLane = -1;

    public bool FurnaceHasItem => !string.IsNullOrEmpty(furnaceHeldItemId);

    public PlacedComponentInstance(
        FactoryComponentData data,
        Vector2Int anchor,
        int rotationIndex,
        ComponentSimulationRules simulationRules = null,
        string supplyItemIdOverride = null)
    {
        this.data = data;
        this.anchor = anchor;
        this.rotationIndex = rotationIndex;
        this.simulationRules = simulationRules;
        this.supplyItemIdOverride = supplyItemIdOverride;
    }

    public IEnumerable<Vector2Int> GetOccupiedTiles()
    {
        if (data == null || data.footprintCells == null || data.footprintCells.Length == 0)
        {
            yield return anchor;
            yield break;
        }

        foreach (Vector2Int offset in data.footprintCells)
            yield return anchor + DirUtil.RotateOffset(offset, rotationIndex);
    }

    public IEnumerable<Vector2Int> GetLogicInputTiles()
    {
        if (data == null || data.logicInputTiles == null || data.logicInputTiles.Length == 0)
            yield break;

        foreach (Vector2Int offset in data.logicInputTiles)
            yield return anchor + DirUtil.RotateOffset(offset, rotationIndex);
    }

    public IEnumerable<Vector2Int> GetLogicOutputSeedTiles(FactorySimulation sim)
    {
        if (data == null || sim == null)
            yield break;

        switch (data.type)
        {
            case ComponentType.Button:
                if (sim.WasButtonPressedThisTick(anchor) && sim.HasWireAt(anchor))
                    yield return anchor;
                break;

            case ComponentType.StartSignal:
                if (sim.TickIndex == 0 && sim.HasWireAt(anchor))
                    yield return anchor;
                break;

            case ComponentType.Sensor:
            {
                Vector2Int scanTile = anchor + DirUtil.ToDelta(DirUtil.FromRotationIndex(rotationIndex));

                if (!sim.InBounds(scanTile))
                    yield break;

                if (sim.GetItemAt(scanTile) != null && sim.HasWireAt(anchor))
                    yield return anchor;

                break;
            }
        }
    }

    public Vector2Int GetFurnaceInputTile()
    {
        return anchor;
    }

    public Dir GetFurnaceInputDirection()
    {
        return DirUtil.FromRotationIndex(rotationIndex);
    }

    public Vector2Int GetFurnaceOutputTile()
    {
        return anchor;
    }

    public Dir GetFurnaceOutputDirection()
    {
        return DirUtil.FromRotationIndex(rotationIndex);
    }

    public bool TryGetFurnaceProgress(out int completedSegments, out int totalSegments, out bool isError)
    {
        completedSegments = 0;
        totalSegments = 0;
        isError = false;

        if (data == null || data.type != ComponentType.Furnace)
            return false;

        if (!FurnaceHasItem)
            return false;

        if (furnaceHeldItemHasNoRecipe)
        {
            completedSegments = 1;
            totalSegments = 1;
            isError = true;
            return true;
        }

        if (furnaceCookTotalSegments <= 0)
            return false;

        totalSegments = Mathf.Max(1, furnaceCookTotalSegments);
        completedSegments = Mathf.Clamp(furnaceCookProgress, 0, totalSegments);
        return true;
    }

    public Vector2Int GetOutputBinInputSourceTile()
    {
        Vector2Int facingDelta = DirUtil.ToDelta(DirUtil.FromRotationIndex(rotationIndex));
        return anchor + facingDelta;
    }

    public Vector2Int GetAnvilItemTile()
    {
        Vector2Int offset = simulationRules != null ? simulationRules.anvilItemTile : Vector2Int.zero;
        return anchor + DirUtil.RotateOffset(offset, rotationIndex);
    }

    public Dir GetAnvilFlowDirection()
    {
        return DirUtil.FromRotationIndex(rotationIndex);
    }

    public Vector2Int GetAnvilInputSourceTile()
    {
        return GetAnvilItemTile() - DirUtil.ToDelta(GetAnvilFlowDirection());
    }

    public Vector2Int GetAnvilOutputTargetTile()
    {
        return GetAnvilItemTile() + DirUtil.ToDelta(GetAnvilFlowDirection());
    }

    public bool IsAnvilHammerLogicActive(FactorySimulation sim)
    {
        if (sim == null || simulationRules == null || simulationRules.anvilHammerLogicInputTiles == null)
            return false;

        foreach (Vector2Int offset in simulationRules.anvilHammerLogicInputTiles)
        {
            Vector2Int tile = anchor + DirUtil.RotateOffset(offset, rotationIndex);
            if (sim.IsLogicActiveAt(tile))
                return true;
        }

        return false;
    }

    public bool IsAnvilEjectLogicActive(FactorySimulation sim)
    {
        if (sim == null || simulationRules == null || simulationRules.anvilEjectLogicInputTiles == null)
            return false;

        foreach (Vector2Int offset in simulationRules.anvilEjectLogicInputTiles)
        {
            Vector2Int tile = anchor + DirUtil.RotateOffset(offset, rotationIndex);
            if (sim.IsLogicActiveAt(tile))
                return true;
        }

        return false;
    }

    public bool TryLoadFurnaceItem(string itemId)
    {
        if (data == null || data.type != ComponentType.Furnace || FurnaceHasItem || string.IsNullOrEmpty(itemId))
            return false;

        furnaceHeldItemId = itemId;
        furnaceCookProgress = 0;
        furnaceCookTotalSegments = 0;
        furnaceHeldItemHasNoRecipe = false;

        if (simulationRules != null &&
            simulationRules.furnaceRecipeBook != null &&
            simulationRules.furnaceRecipeBook.TryGetRecipe(itemId, out FurnaceRecipeEntry recipe) &&
            recipe != null)
        {
            furnaceActiveRecipe = recipe;
            furnaceCookTotalSegments = Mathf.Max(1, recipe.cookTicks);
        }
        else
        {
            furnaceActiveRecipe = null;
            furnaceHeldItemHasNoRecipe = true;
        }

        return true;
    }

    public void AdvanceFurnaceCooking()
    {
        if (data == null || data.type != ComponentType.Furnace || !FurnaceHasItem)
            return;

        if (furnaceHeldItemHasNoRecipe)
            return;

        if (furnaceActiveRecipe == null)
            return;

        furnaceCookProgress++;

        if (furnaceCookProgress >= Mathf.Max(1, furnaceCookTotalSegments))
        {
            if (!string.IsNullOrEmpty(furnaceActiveRecipe.outputItemId))
                furnaceHeldItemId = furnaceActiveRecipe.outputItemId;

            furnaceCookProgress = Mathf.Max(1, furnaceCookTotalSegments);
            furnaceActiveRecipe = null;
        }
    }

    public void ClearFurnaceItem()
    {
        furnaceHeldItemId = null;
        furnaceActiveRecipe = null;
        furnaceCookProgress = 0;
        furnaceCookTotalSegments = 0;
        furnaceHeldItemHasNoRecipe = false;
    }

    public void SetNextSplitterSingleOutputLaneAfterRelease(int releasedOutputLane)
    {
        splitterNextSingleOutputLane = 1 - Mathf.Clamp(releasedOutputLane, 0, 1);
    }

    public Vector2Int[] GetSplitterLaneTiles()
    {
        Vector2Int[] result = new Vector2Int[2];

        if (data == null || data.footprintCells == null || data.footprintCells.Length < 2)
            return result;

        result[0] = anchor + DirUtil.RotateOffset(data.footprintCells[0], rotationIndex);
        result[1] = anchor + DirUtil.RotateOffset(data.footprintCells[1], rotationIndex);
        return result;
    }

    public Vector2Int GetSplitterInputSourceTile(int laneIndex)
    {
        Vector2Int[] laneTiles = GetSplitterLaneTiles();
        Vector2Int laneTile = laneTiles[Mathf.Clamp(laneIndex, 0, 1)];
        Vector2Int forward = DirUtil.ToDelta(DirUtil.FromRotationIndex(rotationIndex));
        return laneTile - forward;
    }

    public Vector2Int GetSplitterLaneTile(int laneIndex)
    {
        Vector2Int[] laneTiles = GetSplitterLaneTiles();
        return laneTiles[Mathf.Clamp(laneIndex, 0, 1)];
    }

    public Vector2Int GetSplitterOutputTargetTile(int laneIndex)
    {
        Vector2Int laneTile = GetSplitterLaneTile(laneIndex);
        Vector2Int forward = DirUtil.ToDelta(DirUtil.FromRotationIndex(rotationIndex));
        return laneTile + forward;
    }

    public void CollectIntents(FactorySimulation sim, List<SimIntent> intents)
    {
        if (data == null || sim == null || intents == null)
            return;

        switch (data.type)
        {
            case ComponentType.Conveyor:
                CollectConveyorIntents(sim, intents);
                break;

            case ComponentType.Splitter:
                CollectSplitterIntents(sim, intents);
                break;

            case ComponentType.SupplyBox:
                CollectSupplyBoxIntents(sim, intents);
                break;

            case ComponentType.OutputBin:
                CollectOutputBinIntents(sim, intents);
                break;

            case ComponentType.Furnace:
                CollectFurnaceIntents(sim, intents);
                break;

            case ComponentType.Anvil:
                CollectAnvilIntents(sim, intents);
                break;
        }
    }

    private void CollectConveyorIntents(FactorySimulation sim, List<SimIntent> intents)
    {
        Vector2Int from = anchor;
        Vector2Int to = from + DirUtil.ToDelta(DirUtil.FromRotationIndex(rotationIndex));

        if (!sim.InBounds(from) || !sim.InBounds(to))
            return;

        ItemInstance item = sim.GetItemAt(from);
        if (item == null)
            return;

        intents.Add(new MoveIntent(from, to, item.itemId));
    }

    private void CollectSplitterIntents(FactorySimulation sim, List<SimIntent> intents)
    {
        Vector2Int lane0 = GetSplitterLaneTile(0);
        Vector2Int lane1 = GetSplitterLaneTile(1);

        bool lane0HasItem = sim.GetItemAt(lane0) != null;
        bool lane1HasItem = sim.GetItemAt(lane1) != null;

        if (!lane0HasItem && !lane1HasItem)
            return;

        if (lane0HasItem && lane1HasItem)
        {
            ItemInstance item0 = sim.GetItemAt(lane0);
            ItemInstance item1 = sim.GetItemAt(lane1);

            Vector2Int lane0To = GetSplitterOutputTargetTile(0);
            Vector2Int lane1To = GetSplitterOutputTargetTile(1);

            if (sim.InBounds(lane0To) && item0 != null)
                intents.Add(new SplitterReleaseIntent(this, lane0, lane0To, lane0, item0.itemId, false, 0));

            if (sim.InBounds(lane1To) && item1 != null)
                intents.Add(new SplitterReleaseIntent(this, lane1, lane1To, lane1, item1.itemId, false, 1));

            return;
        }

        int occupiedLane = lane0HasItem ? 0 : 1;
        Vector2Int from = GetSplitterLaneTile(occupiedLane);
        ItemInstance item = sim.GetItemAt(from);
        if (item == null)
            return;

        int outputLane = splitterNextSingleOutputLane >= 0 ? splitterNextSingleOutputLane : occupiedLane;
        Vector2Int logicalFrom = GetSplitterLaneTile(outputLane);
        Vector2Int to = GetSplitterOutputTargetTile(outputLane);

        if (!sim.InBounds(to))
            return;

        intents.Add(new SplitterReleaseIntent(this, from, to, logicalFrom, item.itemId, true, outputLane));
    }

    private void CollectSupplyBoxIntents(FactorySimulation sim, List<SimIntent> intents)
    {
        Vector2Int from = anchor;
        Vector2Int to = from + DirUtil.ToDelta(DirUtil.FromRotationIndex(rotationIndex));

        if (!sim.InBounds(from) || !sim.InBounds(to))
            return;

        if (string.IsNullOrEmpty(supplyItemIdOverride))
            return;

        if (!sim.IsComponentLogicActive(this))
            return;

        ItemInstance existing = sim.GetItemAt(from);

        if (existing != null)
        {
            intents.Add(new MoveIntent(from, to, existing.itemId));
            return;
        }

        intents.Add(new SupplyMoveIntent(from, to, supplyItemIdOverride));
    }

    private void CollectOutputBinIntents(FactorySimulation sim, List<SimIntent> intents)
    {
        if (sim.InBounds(anchor))
            intents.Add(new ConsumeIntent(anchor));
    }

    private void CollectFurnaceIntents(FactorySimulation sim, List<SimIntent> intents)
    {
        if (!FurnaceHasItem)
            return;

        AdvanceFurnaceCooking();

        if (sim.IsComponentLogicActive(this))
        {
            Vector2Int from = GetFurnaceOutputTile();
            Vector2Int to = from + DirUtil.ToDelta(GetFurnaceOutputDirection());

            if (sim.InBounds(from) && sim.InBounds(to) && !string.IsNullOrEmpty(furnaceHeldItemId))
                intents.Add(new FurnaceReleaseIntent(this, from, to, furnaceHeldItemId));
        }
    }

    private void CollectAnvilIntents(FactorySimulation sim, List<SimIntent> intents)
    {
        Vector2Int itemTile = GetAnvilItemTile();

        if (!sim.InBounds(itemTile))
            return;

        ItemInstance item = sim.GetItemAt(itemTile);

        if (item != null && IsAnvilHammerLogicActive(sim))
            intents.Add(new AnvilStrikeIntent(this, itemTile));

        if (item != null && IsAnvilEjectLogicActive(sim))
        {
            Vector2Int to = GetAnvilOutputTargetTile();
            if (!sim.InBounds(to))
                return;

            intents.Add(new MoveIntent(itemTile, to, itemTile, item.itemId));
        }
    }
}