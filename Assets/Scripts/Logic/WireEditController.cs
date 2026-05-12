using UnityEngine;

public class WireEditController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TileGrid gridUI;
    [SerializeField] private PlacementController placementController;

    private bool logicModeActive;
    private bool dragging;
    private bool eraseMode;
    private Vector2Int lastWireTile;
    private bool hasLastWireTile;

    private void Awake()
    {
        ValidateReferences();
    }

    private void Update()
    {
        if (!logicModeActive)
            return;

        if (MessagePopupUI.IsAnyPopupOpen)
        {
            ResetDragState();
            return;
        }

        if (placementController != null && placementController.HasActivePlacementSelectionPublic())
        {
            ResetDragState();
            return;
        }

        HandleWireInput();
    }

    private void ValidateReferences()
    {
        if (gridUI == null)
            Debug.LogError("WireEditController: gridUI is not assigned", this);

        if (placementController == null)
            Debug.LogError("WireEditController: placementController is not assigned", this);
    }

    public void SetLogicMode(bool enabled)
    {
        logicModeActive = enabled;

        if (!logicModeActive)
            ResetDragState();
    }

    private void HandleWireInput()
    {
        GridTile tile = placementController != null ? placementController.GetTileUnderMousePublic() : null;

        if (Input.GetMouseButtonDown(0))
            BeginDrag(tile, erase: false);

        if (Input.GetMouseButtonDown(1))
            BeginDrag(tile, erase: true);

        if (dragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
            ContinueDrag(tile);

        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
            ResetDragState();
    }

    private void BeginDrag(GridTile tile, bool erase)
    {
        if (MessagePopupUI.IsAnyPopupOpen)
            return;

        if (tile == null || gridUI == null)
            return;

        if (placementController != null && placementController.HasActivePlacementSelectionPublic())
            return;

        eraseMode = erase;
        dragging = true;
        lastWireTile = tile.gridPosition;
        hasLastWireTile = true;

        if (eraseMode)
            gridUI.RemoveWireAndConnections(tile.gridPosition);
        else
            gridUI.EnsureWireExists(tile.gridPosition);
    }

    private void ContinueDrag(GridTile tile)
    {
        if (MessagePopupUI.IsAnyPopupOpen)
        {
            ResetDragState();
            return;
        }

        if (tile == null || gridUI == null)
            return;

        if (placementController != null && placementController.HasActivePlacementSelectionPublic())
        {
            ResetDragState();
            return;
        }

        Vector2Int current = tile.gridPosition;

        if (!hasLastWireTile)
        {
            lastWireTile = current;
            hasLastWireTile = true;
            return;
        }

        if (current == lastWireTile)
            return;

        if (!AreOrthogonallyAdjacent(lastWireTile, current))
        {
            lastWireTile = current;
            return;
        }

        if (eraseMode)
        {
            gridUI.DisconnectAdjacentWires(lastWireTile, current);
            gridUI.RemoveWireAndConnections(current);
        }
        else
        {
            gridUI.ConnectAdjacentWires(lastWireTile, current);
        }

        lastWireTile = current;
    }

    private bool AreOrthogonallyAdjacent(Vector2Int a, Vector2Int b)
    {
        Vector2Int delta = b - a;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
    }

    private void ResetDragState()
    {
        dragging = false;
        eraseMode = false;
        hasLastWireTile = false;
        lastWireTile = Vector2Int.zero;
    }
}