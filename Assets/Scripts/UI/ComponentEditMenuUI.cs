using UnityEngine;
using UnityEngine.UI;
using System;

public class ComponentEditMenuUI : MonoBehaviour
{
    public Button moveButton;
    public Button storeButton;

    private Action onMove;
    private Action onStore;

    private void Awake()
    {
        ApplyButtonColours();
    }

    public void Show(Vector3 screenPos, Action onMoveClicked, Action onStoreClicked, bool allowStore = true)
    {
        onMove = onMoveClicked;
        onStore = onStoreClicked;

        transform.position = screenPos;
        gameObject.SetActive(true);

        if (moveButton != null)
        {
            moveButton.onClick.RemoveAllListeners();
            moveButton.onClick.AddListener(() => onMove?.Invoke());
        }

        if (storeButton != null)
        {
            storeButton.onClick.RemoveAllListeners();
            storeButton.gameObject.SetActive(allowStore);

            if (allowStore)
                storeButton.onClick.AddListener(() => onStore?.Invoke());
        }

        ApplyButtonColours();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void ApplyButtonColours()
    {
        UIColourUtility.ApplySelectableColours(moveButton, UIColourUtility.BuildOrange);
        UIColourUtility.ApplySelectableColours(storeButton, UIColourUtility.InformationBlue);
    }
}