using UnityEngine;

[System.Serializable]
public class RotationSpriteSet
{
    [Tooltip("Sprites for each occupied tile in this rotation")]
    public Sprite[] tileSprites = new Sprite[1];
}

[System.Serializable]
public class FactoryComponent
{
    [Header("Basic Info")]
    public string name;
    public int price;

    [Header("Rotation Sprites (Down, Left, Up, Right)")]
    [Tooltip("One entry per rotation, each entry contains the sprites for that component's occupied tiles")]
    public RotationSpriteSet[] rotationSpriteSets = new RotationSpriteSet[4]
    {
        new RotationSpriteSet(),
        new RotationSpriteSet(),
        new RotationSpriteSet(),
        new RotationSpriteSet()
    };

    // Shallow copy
    public FactoryComponent Clone()
    {
        return new FactoryComponent
        {
            name = name,
            price = price,
            rotationSpriteSets = rotationSpriteSets
        };
    }

    public Sprite GetSpriteForTileIndex(int rotationIndex, int tileIndex)
    {
        RotationSpriteSet spriteSet = GetRotationSpriteSet(rotationIndex);

        if (spriteSet == null || spriteSet.tileSprites == null)
            return null;

        if (tileIndex < 0 || tileIndex >= spriteSet.tileSprites.Length)
            return null;

        return spriteSet.tileSprites[tileIndex];
    }

    private RotationSpriteSet GetRotationSpriteSet(int rotationIndex)
    {
        if (rotationSpriteSets == null || rotationSpriteSets.Length == 0)
            return null;

        int index = Mathf.Clamp(NormalizeRotationIndex(rotationIndex), 0, rotationSpriteSets.Length - 1);
        return rotationSpriteSets[index];
    }

    private static int NormalizeRotationIndex(int rotationIndex)
    {
        return ((rotationIndex % 4) + 4) % 4;
    }
}