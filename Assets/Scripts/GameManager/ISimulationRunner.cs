public interface ISimulationRunner
{
    bool HasError {get;}
    string ErrorMessage {get;}

    int ProducedCount {get;}
    int ProducedValue {get;}

    // Called when entering Run Mode
    void BeginRun();

    // Called when leaving Run Mode (or resetting after error)
    void EndRun();

    // One deterministic tick of simulation
    void TickOnce();

    // Called during Run Mode when the player interacts with a tile (important for buttons)
    void TryInteractAt(UnityEngine.Vector2Int tile);
}