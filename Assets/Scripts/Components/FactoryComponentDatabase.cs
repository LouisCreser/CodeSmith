using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "FactoryComponentDatabase", menuName = "Factory/Component Database")]
public class FactoryComponentDatabase : ScriptableObject
{
    [SerializeField] private FactoryComponentData[] components;

    private readonly Dictionary<string, FactoryComponentData> byId = new();
    private bool isBuilt;

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        ValidateEntries();
    }

    public void RebuildLookup()
    {
        byId.Clear();
        isBuilt = true;

        if (components == null)
            return;

        foreach (FactoryComponentData component in components)
        {
            if (component == null)
                continue;

            if (string.IsNullOrWhiteSpace(component.id))
            {
                Debug.LogWarning($"FactoryComponentDatabase: component asset '{component.name}' has no id", component);
                continue;
            }

            if (byId.ContainsKey(component.id))
            {
                Debug.LogWarning($"FactoryComponentDatabase: duplicate component id '{component.id}", this);
                continue;
            }

            byId[component.id] = component;
        }
    }

    public bool TryGet(string componentId, out FactoryComponentData component)
    {
        if (!isBuilt)
            RebuildLookup();

        if (string.IsNullOrWhiteSpace(componentId))
        {
            component = null;
            return false;
        }

        return byId.TryGetValue(componentId, out component);
    }

    private void ValidateEntries()
    {
        if (components == null)
            return;

        HashSet<string> seenIds = new();

        for (int i = 0; i < components.Length; i++)
        {
            FactoryComponentData component = components[i];

            if (component == null)
            {
                Debug.LogWarning($"FactoryComponentDatabase: components[{i}] is null", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(component.id))
            {
                Debug.LogWarning($"FactoryComponentDatabase: component asset '{component.name}' has no id", component);
                continue;
            }

            if (!seenIds.Add(component.id))
            {
                Debug.LogWarning($"FactoryComponentDatabase: duplicate component id '{component.id}", this);
            }
        }
    }
}