using UnityEngine;
using System.Collections.Generic;

public class PlacedBuildComponent
{
    public FactoryComponent component;
    public FactoryComponentData data;
    public Vector2Int anchor;
    public int rotationIndex;

    // Stores the level-defined Supply Box output so they keep their output when moved. Should be refactored in the future as not every component needs this
    public string supplyItemIdOverride;

    public PlacedBuildComponent(
        FactoryComponent component,
        FactoryComponentData data,
        Vector2Int anchor,
        int rotationIndex,
        string supplyItemIdOverride = null)
    {
        this.component = component;
        this.data = data;
        this.anchor = anchor;
        this.rotationIndex = rotationIndex;
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

    public Sprite GetSpriteForTile(Vector2Int tile)
    {
        if (component == null)
            return null;

        int tileIndex = GetFootprintIndexForTile(tile);
        if (tileIndex < 0)
            return null;

        return component.GetSpriteForTileIndex(rotationIndex, tileIndex);
    }

    private int GetFootprintIndexForTile(Vector2Int tile)
    {
        if (data == null || data.footprintCells == null || data.footprintCells.Length == 0)
            return tile == anchor ? 0 : -1;

        for (int i = 0; i < data.footprintCells.Length; i++)
        {
            Vector2Int occupied = anchor + DirUtil.RotateOffset(data.footprintCells[i], rotationIndex);
            if (occupied == tile)
                return i;
        }

        return -1;
    }
}