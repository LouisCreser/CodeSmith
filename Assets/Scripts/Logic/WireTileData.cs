[System.Serializable]
public class WireTileData
{
    public bool hasWire;
    public WireSideFlags connections = WireSideFlags.None;

    public bool HasConnection(WireSideFlags side)
    {
        return (connections & side) != 0;
    }

    public void SetConnection(WireSideFlags side, bool enabled)
    {
        if (enabled)
            connections |= side;
        else
            connections &= ~side;
    }

    public void Clear()
    {
        hasWire = false;
        connections = WireSideFlags.None;
    }
}