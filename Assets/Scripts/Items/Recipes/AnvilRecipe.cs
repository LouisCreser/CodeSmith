using UnityEngine;
using System;

[Serializable]
public class AnvilRecipeEntry
{
    public string inputItemId;
    public string outputItemId;
    public int requiredStrikes = 1;
}

[CreateAssetMenu(fileName = "AnvilRecipeBook", menuName = "Factory/Anvil Recipe Book")]
public class AnvilRecipe : ScriptableObject
{
    public AnvilRecipeEntry[] recipes = Array.Empty<AnvilRecipeEntry>();

    public bool TryGetRecipe(string inputItemId, out AnvilRecipeEntry recipe)
    {
        if (recipes != null)
        {
            foreach (var entry in recipes)
            {
                if (entry != null && entry.inputItemId == inputItemId)
                {
                    recipe = entry;
                    return true;
                }
            }
        }

        recipe = null;
        return false;
    }
}