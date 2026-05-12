using UnityEngine;
using UnityEngine.UI;

public class GridTile : MonoBehaviour
{
    public Vector2Int gridPosition;

    [Header("Component Layer")]
    [SerializeField] private Image componentImage;

    [Header("Wire Layer")]
    [SerializeField] private Image wireImage;

    public PlacedBuildComponent placedComponent;

    public bool IsEmpty => placedComponent == null;

    private void Awake()
    {
        if (componentImage == null)
        {
            Transform componentImageTransform = transform.Find("ComponentImage");
            if (componentImageTransform != null)
                componentImage = componentImageTransform.GetComponent<Image>();
        }

        if (wireImage == null)
        {
            Transform wireImageTransform = transform.Find("WireImage");
            if (wireImageTransform != null)
                wireImage = wireImageTransform.GetComponent<Image>();
        }

        if (componentImage == null)
            Debug.LogError("GridTile: Missing ComponentImage child or componentImage reference", this);
        else
            ConfigureComponentImage();

        if (wireImage != null)
            wireImage.enabled = false;
    }

    public void SetPlacedComponent(PlacedBuildComponent placed)
    {
        placedComponent = placed;
        RefreshComponentVisual();
    }

    public void ClearPlacedComponent()
    {
        placedComponent = null;
        RefreshComponentVisual();
    }

    public void RefreshComponentVisual()
    {
        if (componentImage == null)
            return;

        ConfigureComponentImage();

        if (placedComponent == null || placedComponent.component == null)
        {
            componentImage.sprite = null;
            componentImage.enabled = false;
            return;
        }

        Sprite sprite = placedComponent.GetSpriteForTile(gridPosition);
        componentImage.sprite = sprite;
        componentImage.enabled = sprite != null;
    }

    public void RefreshWireVisual(Sprite sprite, float rotationZDegrees, bool visible)
    {
        if (wireImage == null)
            return;

        if (!visible || sprite == null)
        {
            wireImage.sprite = null;
            wireImage.enabled = false;
            return;
        }

        wireImage.sprite = sprite;
        wireImage.color = Color.white;
        wireImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZDegrees);
        wireImage.enabled = true;
    }

    private void ConfigureComponentImage()
    {
        if (componentImage == null)
            return;

        componentImage.type = Image.Type.Simple;
        componentImage.preserveAspect = true;
        componentImage.useSpriteMesh = false;
    }
}