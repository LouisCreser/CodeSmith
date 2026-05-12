using UnityEngine;
using System;
using System.IO;

public class SaveSystem
{
    private static readonly string SaveFilePath = Path.Combine(Application.persistentDataPath, "saveData.json");

    public static void Save(PlayerSaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath));

            string tmp = SaveFilePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
            File.Move(tmp, SaveFilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Save failed: {e.Message}");
        }
    }

    public static PlayerSaveData Load()
    {
        try
        {
            if (!File.Exists(SaveFilePath))
                return new PlayerSaveData();

            string json = File.ReadAllText(SaveFilePath);
            return JsonUtility.FromJson<PlayerSaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Load failed: {e.Message}");
            return new PlayerSaveData();
        }
    }

    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(SaveFilePath))
                File.Delete(SaveFilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DeleteSave failed: {e.Message}");
        }
    }
}
