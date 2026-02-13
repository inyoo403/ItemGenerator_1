# ItemGenerator

This project is an automated pipeline designed for RPG and Roguelike game designers to rapidly prototype dungeon layouts and complex item balances. By adjusting various constraints and parameters, designers can simulate thousands of gameplay scenarios and reward structures without manual asset placement.

---

## Prerequisites

- **Unity** 2022.3 LTS or later (2D Template recommended)
- **Required Packages**: `2D Tilemap`, `2D Tilemap Extras`, `Input System`
- The project must contain a working `RoomFirstDungeonGenerator` and `TilemapVisualizer` setup before using the ItemGenerator.

---

## Key Features

### 1. Procedural Dungeon Generation

- **BSP Algorithm**: Utilizes Binary Space Partitioning to create non-overlapping rooms of randomized sizes.
- **BFS Leveling**: Measures path depth from the Start Room using a Breadth-First Search algorithm to assign a `roomLevel`, which scales the power of rewards.
- **Visualization**: Automatically renders floor and wall tiles using `TilemapVisualizer`, applying physical boundaries to the wall layer.

### 2. Room Type Assignment System

- **Manual Room Classification**: After dungeon generation, designers can manually assign each room as `Treasure`, `Boss`, `Start`, or `Normal` via the Inspector UI.
- **Color-Coded Inspector**: Room assignments are visually distinguished in the editor — **Treasure (Yellow)**, **Boss (Red)**, **Start (Blue)**, **Normal (White)** — for intuitive level design.
- **Selective Spawning**: Items are only spawned in rooms explicitly marked as `Treasure` or `Boss`, giving designers full control over loot distribution. If no assignments are loaded, all non-Start rooms are used as a fallback.
- **Sorted Room View**: Rooms are automatically sorted by level in the Inspector for easy readability.

### 3. Perlin Noise-Based Item Placement

- **Noise Sampling**: Each floor tile within a room is evaluated using Perlin Noise. Only tiles exceeding the `noiseThreshold` are considered as valid spawn candidates.
- **Seed-Deterministic Offsets**: The noise offset is derived from the dungeon seed, ensuring that identical seeds produce identical item layouts for reproducible testing.
- **Peak-Priority Spawning**: Candidate tiles are sorted by noise value in descending order, so items naturally cluster around high-noise peaks, creating organic groupings rather than uniform grids.
- **Wall Collision Filtering**: After noise selection, `Physics2D.OverlapCircle` performs a final physical check to prevent items from overlapping with walls.

### 4. Budget Distribution System

- **Total Item Budget**: A single `totalItemBudget` parameter controls the total number of items across the entire dungeon, allowing designers to tune overall loot density in one place.
- **Logarithmic Room Weighting**: Budget is distributed proportionally using a logarithmic curve `(1 + ln(1 + roomLevel) × 0.8)`, which favors higher-level rooms while still granting items to early rooms.
- **Exact Budget Guarantee**: A two-pass allocation (floor + remainder distribution) ensures the total allocated items exactly equals the budget with no rounding loss.

### 5. Item Spawning & Rarity Scaling

- **Exponential Rarity Boost**: As `roomLevel` increases, higher-tier rarities receive an exponential weight boost via `Mathf.Pow(index + 1, level × 0.15 × 1.8)`, making Epic/Unique/Legendary items progressively more likely in deeper rooms.
- **Low-Tier Suppression**: Beyond level 10, Normal and Rare weights are actively suppressed (divided by `level − 9`), preventing common items from diluting high-level reward pools.
- **Weighted Random Selection**: Final rarity is chosen via weighted random roll across all rarity tiers, respecting both base weights and level-scaled modifiers.

### 6. Equipment & Trade-off System

- **Trade-Off Spawn Chance**: Each item independently rolls against `tradeOffSpawnChance` (0–100%) to determine whether it receives both bonuses and penalties, or clean stats only.
- **Penalty Intensity**: Controls how aggressively penalties scale on trade-off items, allowing designers to tune risk-reward tension.
- **Global Difficulty**: A multiplier that scales overall stat values, enabling quick difficulty adjustments across the entire dungeon.
- **Independent Slot Logic**: Epic+ items receive randomized bonuses and penalties independently (Epic 2/1, Unique 3/2, Legendary 4/3).

### 7. Data Persistence & Analytics

- **Parameter-Aware Logging**: The JSON report header records all active tuning parameters (`totalItemBudget`, `noiseScale`, `noiseThreshold`, `globalDifficulty`, `penaltyIntensity`, `tradeOffSpawnChance`) alongside the dungeon seed, enabling direct comparison between different tuning sessions.
- **Per-Room Item Logs**: Each room's items are logged with name, rarity, stat summary, and world position for spatial analysis.
- **Incremental File Naming**: Log files are saved as `Item_Log.json`, `Item_Log_2.json`, `Item_Log_3.json`, etc., preventing accidental overwrites of previous sessions.

---

## How to Use

1. **Generate a dungeon** using the `DungeonGenerator` object in the Unity Inspector.

2. **Select the `ItemGenerator`** object and click **"Load Room Data"** to import room information.

3. **Assign room types** by setting each room to `Treasure`, `Boss`, `Start`, or `Normal` in the color-coded room list.

4. **Adjust parameters**:
   - **Item Budget** — Total number of items to distribute across the dungeon.
   - **Trade-Off Parameters** — Difficulty, Penalty Intensity, and Trade-off Chance.
   - **Noise Settings** — Scale and Threshold to control item clustering density.

5. Click **"1. Spawn Trade-Off Items"** to generate loot in the assigned rooms.

6. Click **"2. Save Data to JSON"** to record the session results in the `_Scripts/Data` folder.

7. Use **"Clear All Items"** to remove all spawned items before re-generating.

---

## Inspector Parameters

| Parameter | Range | Description |
|---|---|---|
| `totalItemBudget` | int | Total number of items distributed across all rooms |
| `tradeOffSpawnChance` | 0 – 100 | Probability (%) that an item receives trade-off penalties |
| `penaltyIntensity` | 0.5 – 3.0 | Multiplier for penalty severity on trade-off items |
| `globalDifficulty` | 0.5 – 2.0 | Global stat multiplier affecting all item values |
| `noiseScale` | 0.01 – 1.0 | Perlin noise frequency — lower values create larger clusters |
| `noiseThreshold` | 0 – 1.0 | Minimum noise value required for a tile to be a spawn candidate |
| `wallCheckRadius` | float | Radius for Physics2D wall overlap detection |

---

## Project Structure

```
Assets/
└── _Scripts/
    ├── ItemGenerator.cs      # Main item spawning pipeline + Custom Editor UI
    ├── Equipment.cs           # Equipment stat generation & trade-off logic
    ├── ItemObject.cs          # Runtime item component attached to spawned prefabs
    └── Data/
        ├── Item_Log.json      # Generated session log (auto-created)
        └── Item_Log_2.json    # Incremental logs for subsequent sessions
```

---

## Credits & Attribution

### Map Generation Reference

The core map generation logic including BSP, room connection, and tilemap visualization was implemented by referencing the following tutorial:

> **Sunny Valley Studio**: [Procedural Dungeon Generation in Unity](https://www.youtube.com/watch?v=szOq1HSWtm0&t=395s)

### Original Work & Design

All systems outside of the basic map generation were designed and implemented from scratch by me, including:

- **Room Type Assignment System** — Manual room classification with color-coded Inspector UI.
- **Perlin Noise Placement Algorithm** — Seed-deterministic noise sampling with threshold filtering and peak-priority spawning.
- **Budget Distribution System** — Logarithmic room weighting with exact budget guarantee.
- **Level Scaling System** — BFS-based depth leveling linked to exponential rarity boosting and low-tier suppression.
- **Trade-off Mechanics** — The independent bonus/penalty slot logic, penalty intensity, and growth formulas.
- **Data Logging System** — Parameter-aware metadata logging and incremental JSON file management.
- **Custom Editor Tools** — Color-coded room assignment UI, foldout room lists, and one-click action buttons.
