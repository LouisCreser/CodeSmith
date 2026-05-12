Architecture Overview

- LevelContext owns the active level data for each level scene.
- PlayerData stores persistent per-level progress, including board state, unlocks, manuals, stored stock, and best evaluation results.
- TileGrid owns grid occupancy, tile lookup, wire data, and wire visuals.
- PlacementController owns build-mode component placement, movement, rotation, storing, and ghost previews.
- ComponentSimulationRules owns shared furnace and anvil simulation rules.
- FactorySimulation uses an intent-validation-commit pipeline. Components submit simulation intents, the simulation validates them, then commits state changes.
- EvaluationService calculates evaluation results and applies successful-run rewards.

The systems are designed to be extendable. New levels, items, manuals, recipes, and component definitions can be added mostly through ScriptableObject data without changing existing gameplay code.

Defensive programming is applied through validation scripts, which check that required scene references, project data, level data, recipes, and databases are assigned correctly during start-up and level loading.