using UnityEngine;

public abstract class SimIntent
{
}

public sealed class MoveIntent : SimIntent
{
    public readonly Vector2Int from;
    public readonly Vector2Int to;
    public readonly Vector2Int logicalFrom;
    public readonly string itemId;

    public MoveIntent(Vector2Int from, Vector2Int to, string itemId)
        : this(from, to, from, itemId)
    {
    }

    public MoveIntent(Vector2Int from, Vector2Int to, Vector2Int logicalFrom, string itemId)
    {
        this.from = from;
        this.to = to;
        this.logicalFrom = logicalFrom;
        this.itemId = itemId;
    }
}

public sealed class SupplyMoveIntent : SimIntent
{
    public readonly Vector2Int from;
    public readonly Vector2Int to;
    public readonly Vector2Int logicalFrom;
    public readonly string itemId;

    public SupplyMoveIntent(Vector2Int from, Vector2Int to, string itemId)
        : this(from, to, from, itemId)
    {
    }

    public SupplyMoveIntent(Vector2Int from, Vector2Int to, Vector2Int logicalFrom, string itemId)
    {
        this.from = from;
        this.to = to;
        this.logicalFrom = logicalFrom;
        this.itemId = itemId;
    }
}

public sealed class SpawnIntent : SimIntent
{
    public readonly Vector2Int at;
    public readonly string itemId;

    public SpawnIntent(Vector2Int at, string itemId)
    {
        this.at = at;
        this.itemId = itemId;
    }
}

public sealed class ConsumeIntent : SimIntent
{
    public readonly Vector2Int at;

    public ConsumeIntent(Vector2Int at)
    {
        this.at = at;
    }
}

public sealed class FurnaceReleaseIntent : SimIntent
{
    public readonly PlacedComponentInstance furnace;
    public readonly Vector2Int from;
    public readonly Vector2Int to;
    public readonly Vector2Int logicalFrom;
    public readonly string itemId;

    public FurnaceReleaseIntent(PlacedComponentInstance furnace, Vector2Int from, Vector2Int to, string itemId)
        : this(furnace, from, to, from, itemId)
    {
    }

    public FurnaceReleaseIntent(PlacedComponentInstance furnace, Vector2Int from, Vector2Int to, Vector2Int logicalFrom, string itemId)
    {
        this.furnace = furnace;
        this.from = from;
        this.to = to;
        this.logicalFrom = logicalFrom;
        this.itemId = itemId;
    }
}

public sealed class SplitterReleaseIntent : SimIntent
{
    public readonly PlacedComponentInstance splitter;
    public readonly Vector2Int from;
    public readonly Vector2Int to;
    public readonly Vector2Int logicalFrom;
    public readonly string itemId;
    public readonly bool wasSingleRelease;
    public readonly int releasedOutputLane;

    public SplitterReleaseIntent(PlacedComponentInstance splitter, Vector2Int from, Vector2Int to, Vector2Int logicalFrom, string itemId, bool wasSingleRelease, int releasedOutputLane)
    {
        this.splitter = splitter;
        this.from = from;
        this.to = to;
        this.logicalFrom = logicalFrom;
        this.itemId = itemId;
        this.wasSingleRelease = wasSingleRelease;
        this.releasedOutputLane = releasedOutputLane;
    }
}

public sealed class AnvilStrikeIntent : SimIntent
{
    public readonly PlacedComponentInstance anvil;
    public readonly Vector2Int itemTile;

    public AnvilStrikeIntent(PlacedComponentInstance anvil, Vector2Int itemTile)
    {
        this.anvil = anvil;
        this.itemTile = itemTile;
    }
}