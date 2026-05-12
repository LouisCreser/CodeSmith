using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PlacementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopManualsUI shopUI;
    [SerializeField] private LevelContext levelContext;
    [SerializeField] private TileGrid gridUI;

    [Header("Edit Menu")]
    [SerializeField] private ComponentEditMenuUI editMenu;

    [Header("Ghost UI")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image ghostImage;

    private readonly List<Image> ghostImages = new();

    private enum Mode
    {
        None,
        PlacingFromShop,
        MovingExisting
    }

    private Mode mode = Mode.None;

    private FactoryComponent heldPrototype;
    private FactoryComponentData heldPrototypeData;
    private int heldRotationIndex;

    private PlacedBuildComponent movingPlacedComponent;
    private Vector2Int originalMovingAnchor;
    private int originalMovingRotation;

    private GridTile hoveredTile;
    private bool logicModeActive;

    private LevelData CurrentLevelData => levelContext != null ? levelContext.LevelData : null;
    private string CurrentLevelId => levelContext != null ? levelContext.LevelId : null;

    private void Awake()
    {
        ValidateReferences();

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        if (ghostImage == null)
            ghostImage = CreateGhostImage("PlacementGhost_0");

        if (ghostImage != null && !ghostImages.Contains(ghostImage))
            ghostImages.Add(ghostImage);

        if (shopUI != null)
        {
            shopUI.ComponentClicked += SelectComponent;
            shopUI.GetStoredCount = GetStoredCountForCurrentLevel;
            shopUI.GetPurchaseCount = GetPurchaseCountForCurrentLevel;
        }
    }

    private void Start()
    {
        RefreshShopUI();
    }

    private void OnDestroy()
    {
        if (shopUI != null)
        {
            shopUI.ComponentClicked -= SelectComponent;

            if (shopUI.GetStoredCount == GetStoredCountForCurrentLevel)
                shopUI.GetStoredCount = null;

            if (shopUI.GetPurchaseCount == GetPurchaseCountForCurrentLevel)
                shopUI.GetPurchaseCount = null;
        }
    }

    private void Update()
    {
        hoveredTile = GetTileUnderMouse();
        HandleUserInput();
    }

    private void ValidateReferences()
    {
        if (shopUI == null)
            Debug.LogWarning("PlacementController: shopUI is not assigned", this);

        if (levelContext == null)
            Debug.LogError("PlacementController: levelContext is not assigned", this);
        else if (levelContext.LevelData == null)
            Debug.LogError("PlacementController: levelContext has no LevelData", this);

        if (gridUI == null)
            Debug.LogError("PlacementController: gridUI is not assigned", this);

        if (editMenu == null)
            Debug.LogWarning("PlacementController: editMenu is not assigned", this);
    }

    public void SetLogicMode(bool enabled)
    {
        logicModeActive = enabled;

        if (logicModeActive && mode == Mode.MovingExisting)
            ClearActivePlacementState();

        RefreshGhostVisibility();
    }

    public bool HasActivePlacementSelectionPublic()
    {
        return mode != Mode.None;
    }

    public GridTile GetTileUnderMousePublic()
    {
        return GetTileUnderMouse();
    }

    public void ClearActivePlacementState()
    {
        if (mode == Mode.MovingExisting && movingPlacedComponent != null && gridUI != null)
        {
            movingPlacedComponent.anchor = originalMovingAnchor;
            movingPlacedComponent.rotationIndex = originalMovingRotation;
            gridUI.PlacePlacedComponent(movingPlacedComponent);
        }

        heldPrototype = null;
        heldPrototypeData = null;
        heldRotationIndex = 0;

        movingPlacedComponent = null;
        originalMovingAnchor = Vector2Int.zero;
        originalMovingRotation = 0;

        mode = Mode.None;

        if (shopUI != null)
            shopUI.ClearSelectedComponent();

        if (editMenu != null)
            editMenu.Hide();

        RefreshGhostVisibility();
    }

    private void RefreshShopUI()
    {
        LevelData levelData = CurrentLevelData;

        if (shopUI != null && levelData != null)
            shopUI.Refresh(levelData);
    }

    private void RefreshSelectedShopRow()
    {
        if (shopUI == null || heldPrototypeData == null)
            return;

        shopUI.RefreshPriceForComponent(heldPrototypeData.id);
    }

    private void HandleUserInput()
    {
        bool popupBlockingGameplay = IsGameplayBlockedByPopup();

        // Right-click to cancel is still allowed while a popup is open
        if (Input.GetMouseButtonDown(1))
            HandleRightClickCancel();

        if (!popupBlockingGameplay && !logicModeActive && Input.GetMouseButtonDown(0))
            HandleLeftClick();

        if (mode == Mode.MovingExisting || mode == Mode.PlacingFromShop)
        {
            SyncGhostSizeToGrid();

            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.01f)
                Rotate(+1);
            else if (wheel < -0.01f)
                Rotate(-1);

            if (Input.GetKeyDown(KeyCode.R))
                Rotate(+1);

            UpdateGhostVisual();
            UpdateGhostPositions();
            RefreshGhostVisibility();

            if (!popupBlockingGameplay && Input.GetMouseButtonDown(0))
                TryPlace();
        }
    }

    private void HandleRightClickCancel()
    {
        if (mode == Mode.PlacingFromShop || mode == Mode.MovingExisting)
            ClearActivePlacementState();

        if (editMenu != null)
            editMenu.Hide();
    }

    private void HandleLeftClick()
    {
        if (mode != Mode.None)
            return;

        if (IsPointerOverEditMenu())
            return;

        PlacedBuildComponent clickedPlaced = hoveredTile != null ? hoveredTile.placedComponent : null;

        if (clickedPlaced != null)
        {
            if (editMenu != null)
            {
                bool allowStore = IsStoreAllowed(clickedPlaced.data);
                Vector2 menuPosition = GetEditMenuScreenPosition(clickedPlaced);
                editMenu.Show(menuPosition, () => Move(clickedPlaced), () => Store(clickedPlaced), allowStore);
            }
        }
        else if (editMenu != null)
        {
            editMenu.Hide();
        }
    }

    private Vector2 GetEditMenuScreenPosition(PlacedBuildComponent placed)
    {
        if (placed == null || gridUI == null)
            return Input.mousePosition;

        Vector2Int centreTile = GetPlacedComponentCentreTile(placed);

        bool atTopEdge = centreTile.y <= 0;
        bool atLeftEdge = centreTile.x <= 0;
        bool atRightEdge = centreTile.x >= gridUI.Columns - 1;

        int xOffset = 0;
        int yOffset = 0;

        if (atLeftEdge)
            xOffset = 2;
        else if (atRightEdge)
            xOffset = -2;
        else if (atTopEdge)
            yOffset = 1;
        else
            yOffset = -1;

        Vector2Int offset = new Vector2Int(xOffset, yOffset);
        Vector2Int menuTile = centreTile + offset;

        if (gridUI.InBounds(menuTile))
            return GetTileScreenCentre(menuTile);

        return GetTileScreenCentre(centreTile);
    }

    private Vector2Int GetPlacedComponentCentreTile(PlacedBuildComponent placed)
    {
        if (placed == null)
            return Vector2Int.zero;

        Vector2 sum = Vector2.zero;
        int count = 0;

        foreach (Vector2Int tile in placed.GetOccupiedTiles())
        {
            if (gridUI != null && !gridUI.InBounds(tile))
                continue;

            sum += tile;
            count++;
        }

        if (count <= 0)
            return placed.anchor;

        Vector2 average = sum / count;

        return new Vector2Int(
            Mathf.RoundToInt(average.x),
            Mathf.RoundToInt(average.y));
    }

    private Vector2 GetTileScreenCentre(Vector2Int tilePosition)
    {
        if (gridUI == null)
            return Input.mousePosition;

        GridTile tile = gridUI.GetTile(tilePosition);
        if (tile == null)
            return Input.mousePosition;

        RectTransform tileRect = tile.transform as RectTransform;
        if (tileRect == null)
            return Input.mousePosition;

        Vector3 worldCentre = tileRect.TransformPoint(tileRect.rect.center);

        Canvas canvas = rootCanvas != null
            ? rootCanvas
            : GetComponentInParent<Canvas>();

        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return worldCentre;

        return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldCentre);
    }

    private void SelectComponent(FactoryComponentData prototypeData)
    {
        if (mode == Mode.MovingExisting)
            ClearActivePlacementState();

        if (prototypeData == null || prototypeData.component == null)
        {
            heldPrototypeData = null;
            heldPrototype = null;
            heldRotationIndex = 0;
            mode = Mode.None;

            if (shopUI != null)
                shopUI.ClearSelectedComponent();

            RefreshGhostVisibility();
            return;
        }

        heldPrototypeData = prototypeData;
        heldPrototype = prototypeData.component;
        heldRotationIndex = prototypeData.defaultRotationIndex;
        mode = Mode.PlacingFromShop;

        if (shopUI != null)
            shopUI.SetSelectedComponentId(prototypeData.id);

        if (editMenu != null)
            editMenu.Hide();

        UpdateGhostVisual();
        UpdateGhostPositions();
        RefreshGhostVisibility();
    }

    private int GetMoney()
    {
        return PlayerData.Instance != null ? PlayerData.Instance.Money : 0;
    }

    private int GetStoredCountForCurrentLevel(FactoryComponentData componentData)
    {
        if (componentData == null)
            return 0;

        return GetStoredStockForCurrentLevel(componentData.id);
    }

    private int GetPurchaseCountForCurrentLevel(FactoryComponentData componentData)
    {
        if (componentData == null)
            return 0;

        return GetPurchaseCountForCurrentLevel(componentData.id);
    }

    private int GetStoredStockForCurrentLevel(string componentId)
    {
        string levelId = CurrentLevelId;

        if (PlayerData.Instance == null || string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(componentId))
            return 0;

        return PlayerData.Instance.GetStoredStock(levelId, componentId);
    }

    private int GetPurchaseCountForCurrentLevel(string componentId)
    {
        string levelId = CurrentLevelId;

        if (PlayerData.Instance == null || string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(componentId))
            return 0;

        return PlayerData.Instance.GetComponentPurchaseCount(levelId, componentId);
    }

    private int GetHeldComponentPrice()
    {
        int purchaseCount = heldPrototypeData != null ? GetPurchaseCountForCurrentLevel(heldPrototypeData.id) : 0;

        return ComponentPricingService.GetCurrentPrice(
            heldPrototypeData,
            CurrentLevelData,
            purchaseCount);
    }

    private void Rotate(int delta)
    {
        heldRotationIndex = (heldRotationIndex + delta) % 4;
        if (heldRotationIndex < 0)
            heldRotationIndex += 4;

        if (mode == Mode.MovingExisting && movingPlacedComponent != null)
            movingPlacedComponent.rotationIndex = heldRotationIndex;

        UpdateGhostVisual();
        UpdateGhostPositions();
        RefreshGhostVisibility();
    }

    private bool IsStoreAllowed(FactoryComponentData data)
    {
        if (data == null)
            return false;

        return data.type != ComponentType.SupplyBox && data.type != ComponentType.OutputBin && data.type != ComponentType.StartSignal;
    }

    private bool IsRestrictedColumnPlacementValid(FactoryComponentData data, Vector2Int anchor)
    {
        if (data == null || gridUI == null)
            return false;

        switch (data.type)
        {
            case ComponentType.SupplyBox:
                return anchor.x == 0;

            case ComponentType.OutputBin:
                return anchor.x == gridUI.Columns - 1;

            default:
                return true;
        }
    }

    private bool CanPlaceHeldAt(Vector2Int anchor)
    {
        if (gridUI == null || heldPrototypeData == null)
            return false;

        if (!IsRestrictedColumnPlacementValid(heldPrototypeData, anchor))
            return false;

        PlacedBuildComponent ignore = mode == Mode.MovingExisting ? movingPlacedComponent : null;
        return gridUI.CanPlaceComponentAt(heldPrototypeData, anchor, heldRotationIndex, ignore);
    }

    private void TryPlace()
    {
        if (IsGameplayBlockedByPopup())
            return;

        if (hoveredTile == null)
            return;

        Vector2Int anchor = hoveredTile.gridPosition;

        if (mode == Mode.MovingExisting)
        {
            TryPlaceMovingComponent(anchor);
            return;
        }

        if (mode == Mode.PlacingFromShop)
            TryPlacePurchasedComponent(anchor);
    }

    private void TryPlaceMovingComponent(Vector2Int anchor)
    {
        if (movingPlacedComponent == null || gridUI == null)
            return;

        if (!CanPlaceHeldAt(anchor))
            return;

        movingPlacedComponent.anchor = anchor;
        movingPlacedComponent.rotationIndex = heldRotationIndex;

        bool success = gridUI.PlacePlacedComponent(movingPlacedComponent);
        if (!success)
        {
            movingPlacedComponent.anchor = originalMovingAnchor;
            movingPlacedComponent.rotationIndex = originalMovingRotation;
            gridUI.PlacePlacedComponent(movingPlacedComponent);
            return;
        }

        movingPlacedComponent = null;
        mode = Mode.None;

        if (shopUI != null)
            shopUI.ClearSelectedComponent();

        RefreshGhostVisibility();
    }

    private void TryPlacePurchasedComponent(Vector2Int anchor)
    {
        if (heldPrototype == null || heldPrototypeData == null || gridUI == null)
            return;

        string levelId = CurrentLevelId;
        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogError("PlacementController: cannot place component because current level ID is missing", this);
            return;
        }

        if (!CanPlaceHeldAt(anchor))
            return;

        string componentId = heldPrototypeData.id;
        int storedCount = GetStoredStockForCurrentLevel(componentId);
        bool usedStored = storedCount > 0;
        int cost = GetHeldComponentPrice();

        if (!usedStored && (PlayerData.Instance == null || !PlayerData.Instance.TrySpend(cost)))
            return;

        FactoryComponent placedComponent = heldPrototype.Clone();

        bool success = gridUI.PlaceComponentAt(placedComponent, heldPrototypeData, anchor, heldRotationIndex);
        if (!success)
        {
            if (!usedStored && PlayerData.Instance != null)
                PlayerData.Instance.AddMoney(cost);

            return;
        }

        if (usedStored)
        {
            if (PlayerData.Instance != null)
                PlayerData.Instance.TryConsumeStoredStock(levelId, componentId, 1);
        }
        else if (PlayerData.Instance != null)
        {
            PlayerData.Instance.IncrementComponentPurchaseCount(levelId, componentId);
        }

        RefreshSelectedShopRow();
    }

    private GridTile GetTileUnderMouse()
    {
        if (gridUI == null)
            return null;

        return gridUI.GetTileAtScreenPosition(Input.mousePosition);
    }

    private void UpdateGhostPositions()
    {
        List<Vector2Int> ghostTiles = GetCurrentGhostTiles();
        EnsureGhostImageCount(ghostTiles.Count);

        if (ghostTiles.Count == 0)
        {
            HideAllGhostImages();
            return;
        }

        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
            return;

        Vector2 cellSize = gridUI != null ? gridUI.CellSize : Vector2.zero;

        if (hoveredTile != null)
        {
            Vector2Int anchorTile = hoveredTile.gridPosition;

            for (int i = 0; i < ghostTiles.Count && i < ghostImages.Count; i++)
            {
                Image image = ghostImages[i];
                if (image == null)
                    continue;

                Vector2Int ghostTile = ghostTiles[i];

                if (TryGetTileWorldCentre(ghostTile, out Vector3 tileWorldCentre))
                {
                    image.rectTransform.position = tileWorldCentre;
                    continue;
                }

                if (TryGetTileWorldCentre(anchorTile, out Vector3 anchorWorldCentre))
                {
                    Vector2Int offsetFromAnchor = ghostTile - anchorTile;
                    image.rectTransform.position = anchorWorldCentre + GetGridWorldOffset(offsetFromAnchor, cellSize);
                }
            }

            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out Vector2 mouseLocal))
            return;

        List<Vector2Int> freeOffsets = GetCurrentGhostLocalOffsets();
        if (freeOffsets.Count == 0)
            return;

        for (int i = 0; i < freeOffsets.Count && i < ghostImages.Count; i++)
        {
            Image image = ghostImages[i];
            if (image == null)
                continue;

            Vector2 anchored = mouseLocal + new Vector2(freeOffsets[i].x * cellSize.x, -freeOffsets[i].y * cellSize.y);
            image.rectTransform.anchoredPosition = anchored;
        }
    }

    private bool TryGetTileWorldCentre(Vector2Int tilePosition, out Vector3 worldCentre)
    {
        worldCentre = Vector3.zero;

        if (gridUI == null || !gridUI.InBounds(tilePosition))
            return false;

        GridTile tile = gridUI.GetTile(tilePosition);
        if (tile == null)
            return false;

        RectTransform tileRect = tile.transform as RectTransform;
        if (tileRect == null)
            return false;

        worldCentre = tileRect.TransformPoint(tileRect.rect.center);
        return true;
    }

    private Vector3 GetGridWorldOffset(Vector2Int gridOffset, Vector2 cellSize)
    {
        Vector3 localOffset = new Vector3(gridOffset.x * cellSize.x, -gridOffset.y * cellSize.y, 0f);

        RectTransform gridRect = gridUI != null ? gridUI.GridRect : null;
        return gridRect != null ? gridRect.TransformVector(localOffset) : localOffset;
    }

    private void UpdateGhostVisual()
    {
        List<Sprite> sprites = GetCurrentGhostSprites();
        EnsureGhostImageCount(sprites.Count);

        bool validPlacement = hoveredTile != null && CanPlaceHeldAt(hoveredTile.gridPosition);
        Color ghostColor = GetGhostColor(validPlacement);

        for (int i = 0; i < ghostImages.Count; i++)
        {
            Image image = ghostImages[i];
            if (image == null)
                continue;

            if (i < sprites.Count && sprites[i] != null)
            {
                image.sprite = sprites[i];
                image.color = ghostColor;
            }
            else
            {
                image.sprite = null;
                image.enabled = false;
            }
        }
    }

    private Color GetGhostColor(bool validPlacement)
    {
        if (mode == Mode.PlacingFromShop)
        {
            bool hasStored =
                heldPrototypeData != null &&
                GetStoredStockForCurrentLevel(heldPrototypeData.id) > 0;

            bool hasMoney =
                PlayerData.Instance != null &&
                heldPrototype != null &&
                GetMoney() >= GetHeldComponentPrice();

            bool canAfford = hasStored || hasMoney;

            return validPlacement && canAfford ? new Color(1f, 1f, 1f, 0.6f) : new Color(1f, 0.2f, 0.2f, 0.6f);
        }

        if (mode == Mode.MovingExisting)
        {
            return validPlacement ? new Color(1f, 1f, 1f, 0.6f) : new Color(1f, 0.2f, 0.2f, 0.6f);
        }

        return new Color(1f, 1f, 1f, 0.6f);
    }

    private void RefreshGhostVisibility()
    {
        bool hasActiveSelection = mode != Mode.None;
        List<Sprite> sprites = GetCurrentGhostSprites();
        EnsureGhostImageCount(sprites.Count);

        for (int i = 0; i < ghostImages.Count; i++)
        {
            Image image = ghostImages[i];
            if (image == null)
                continue;

            bool visible =
                hasActiveSelection &&
                i < sprites.Count &&
                sprites[i] != null;

            image.enabled = visible;
        }
    }

    public void SetGhostVisiblePublic(bool visible)
    {
        if (!visible)
        {
            HideAllGhostImages();
            return;
        }

        RefreshGhostVisibility();
    }

    private void SyncGhostSizeToGrid()
    {
        if (gridUI == null)
            return;

        Vector2 cell = gridUI.CellSize;

        foreach (Image image in ghostImages)
        {
            if (image == null)
                continue;

            RectTransform rectTransform = image.rectTransform;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cell.x);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cell.y);
        }
    }

    private void Move(PlacedBuildComponent placed)
    {
        if (placed == null || gridUI == null)
            return;

        movingPlacedComponent = placed;
        originalMovingAnchor = placed.anchor;
        originalMovingRotation = placed.rotationIndex;

        gridUI.RemovePlacedComponent(placed);

        heldPrototype = null;
        heldPrototypeData = placed.data;
        heldRotationIndex = placed.rotationIndex;

        mode = Mode.MovingExisting;

        if (shopUI != null)
            shopUI.ClearSelectedComponent();

        if (editMenu != null)
            editMenu.Hide();

        UpdateGhostVisual();
        UpdateGhostPositions();
        RefreshGhostVisibility();
    }

    private void Store(PlacedBuildComponent placed)
    {
        if (placed == null || placed.data == null || gridUI == null)
            return;

        if (!IsStoreAllowed(placed.data))
        {
            if (editMenu != null)
                editMenu.Hide();

            return;
        }

        string levelId = CurrentLevelId;
        if (string.IsNullOrEmpty(levelId))
        {
            Debug.LogError("PlacementController: cannot store component because current level ID is missing", this);
            return;
        }

        string componentId = placed.data.id;

        if (PlayerData.Instance != null)
            PlayerData.Instance.AddStoredStock(levelId, componentId, 1);

        gridUI.RemovePlacedComponent(placed);

        if (shopUI != null)
            shopUI.RefreshStoredCountForComponent(componentId);

        if (editMenu != null)
            editMenu.Hide();
    }

    private bool IsGameplayBlockedByPopup()
    {
        return MessagePopupUI.IsAnyPopupOpen;
    }

    private bool IsPointerOverEditMenu()
    {
        if (editMenu == null || !editMenu.gameObject.activeInHierarchy)
            return false;

        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject != null && result.gameObject.transform.IsChildOf(editMenu.transform))
                return true;
        }

        return false;
    }

    private Image CreateGhostImage(string objectName)
    {
        if (rootCanvas == null)
            return null;

        GameObject ghost = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ghost.transform.SetParent(rootCanvas.transform, false);
        ghost.transform.SetAsLastSibling();

        Image image = ghost.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;

        image.enabled = false;
        return image;
    }

    private void EnsureGhostImageCount(int count)
    {
        if (count < 1)
            count = 1;

        while (ghostImages.Count < count)
        {
            Image image = CreateGhostImage($"PlacementGhost_{ghostImages.Count}");
            if (image != null)
                ghostImages.Add(image);
            else
                break;
        }
    }

    private void HideAllGhostImages()
    {
        foreach (Image image in ghostImages)
        {
            if (image != null)
                image.enabled = false;
        }
    }

    private List<Vector2Int> GetCurrentGhostTiles()
    {
        List<Vector2Int> result = new();

        if (heldPrototypeData == null || gridUI == null)
            return result;

        Vector2Int anchor = hoveredTile != null ? hoveredTile.gridPosition : Vector2Int.zero;

        foreach (Vector2Int tile in gridUI.GetFootprintTiles(heldPrototypeData, anchor, heldRotationIndex))
            result.Add(tile);

        return result;
    }

    private List<Vector2Int> GetCurrentGhostLocalOffsets()
    {
        List<Vector2Int> result = new();

        if (heldPrototypeData == null)
            return result;

        if (heldPrototypeData.footprintCells == null || heldPrototypeData.footprintCells.Length == 0)
        {
            result.Add(Vector2Int.zero);
            return result;
        }

        foreach (Vector2Int offset in heldPrototypeData.footprintCells)
            result.Add(DirUtil.RotateOffset(offset, heldRotationIndex));

        return result;
    }

    private List<Sprite> GetCurrentGhostSprites()
    {
        List<Sprite> result = new();

        if (mode == Mode.MovingExisting)
        {
            if (movingPlacedComponent == null || movingPlacedComponent.component == null || movingPlacedComponent.data == null)
                return result;

            if (movingPlacedComponent.data.footprintCells == null || movingPlacedComponent.data.footprintCells.Length == 0)
            {
                result.Add(movingPlacedComponent.component.GetSpriteForTileIndex(heldRotationIndex, 0));
                return result;
            }

            for (int i = 0; i < movingPlacedComponent.data.footprintCells.Length; i++)
                result.Add(movingPlacedComponent.component.GetSpriteForTileIndex(heldRotationIndex, i));

            return result;
        }

        if (mode == Mode.PlacingFromShop)
        {
            if (heldPrototype == null || heldPrototypeData == null)
                return result;

            if (heldPrototypeData.footprintCells == null || heldPrototypeData.footprintCells.Length == 0)
            {
                result.Add(heldPrototype.GetSpriteForTileIndex(heldRotationIndex, 0));
                return result;
            }

            for (int i = 0; i < heldPrototypeData.footprintCells.Length; i++)
                result.Add(heldPrototype.GetSpriteForTileIndex(heldRotationIndex, i));

            return result;
        }

        return result;
    }
}