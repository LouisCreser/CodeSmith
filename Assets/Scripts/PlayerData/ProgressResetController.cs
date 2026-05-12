using UnityEngine;

public sealed class ProgressResetController : MonoBehaviour
{
    [Header("Progression")]
    [SerializeField] private LevelRegistry levelRegistry;

    public void ResetProgress()
    {
        if (PlayerData.Instance == null)
        {
            Debug.LogError("ProgressResetController: PlayerData.Instance is null", this);
            return;
        }

        PlayerData.Instance.ResetProgress();

        if (levelRegistry != null)
        {
            levelRegistry.EnsureDefaultUnlocks(PlayerData.Instance);
        }
        else
        {
            Debug.LogWarning("ProgressResetController: levelRegistry is not assigned", this);
        }
    }
}