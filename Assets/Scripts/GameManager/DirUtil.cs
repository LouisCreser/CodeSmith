using UnityEngine;

public enum Dir 
{ 
    Down = 0, 
    Left = 1, 
    Up = 2, 
    Right = 3}

public static class DirUtil
{
    // (0,0) is top left: Up means y-1
    public static Vector2Int ToDelta(Dir d) => d switch
    {
        Dir.Down => new Vector2Int(0, 1),
        Dir.Left => new Vector2Int(-1, 0),
        Dir.Up => new Vector2Int(0, -1),
        Dir.Right => new Vector2Int(1, 0),
        _ => Vector2Int.zero
    };

    public static Dir FromRotationIndex(int rotationIndex)
    {
        int rot = ((rotationIndex % 4) + 4) % 4;
        return (Dir)rot;
    }

    public static Vector2Int RotateOffset(Vector2Int v, int rotationIndex)
    {
        int rot = ((rotationIndex % 4) + 4) % 4;

        return rot switch
        {
            0 => v,
            1 => new Vector2Int(-v.y, v.x),
            2 => new Vector2Int(-v.x, -v.y),
            3 => new Vector2Int(v.y, -v.x),
            _ => v
        };
    }
}