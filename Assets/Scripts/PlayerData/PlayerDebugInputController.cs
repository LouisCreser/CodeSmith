using UnityEngine;

public sealed class PlayerDebugInputController : MonoBehaviour
{
    [Header("Debug Controls")]
    [SerializeField] private bool enableDebugInput = true;

    [Header("Money")]
    [SerializeField] private KeyCode addMoneyKey = KeyCode.M;
    [SerializeField] private int debugMoneyAmount = 100;

    [Header("Save / Load")]
    [SerializeField] private KeyCode saveKey = KeyCode.S;
    [SerializeField] private KeyCode loadKey = KeyCode.L;
    [SerializeField] private KeyCode resetProgressKey = KeyCode.R;

    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    private void Update()
    {
        // Only works during development for testing purposes
        #if UNITY_EDITOR
        if (!enableDebugInput)
            return;

        if (PlayerData.Instance == null)
            return;

        if (Input.GetKeyDown(addMoneyKey))
            PlayerData.Instance.AddMoney(debugMoneyAmount);

        if (Input.GetKeyDown(saveKey))
            PlayerData.Instance.SaveData();

        if (Input.GetKeyDown(loadKey))
            PlayerData.Instance.LoadData();

        if (Input.GetKeyDown(resetProgressKey))
            PlayerData.Instance.ResetProgress();
        #endif
    }
}