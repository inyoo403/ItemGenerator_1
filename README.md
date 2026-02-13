# ItemGenerator

A procedural item generation tool for 2D dungeon games built in Unity. It automatically creates and places randomized equipment (with names, rarity tiers, and stat values) across a procedurally generated dungeon map. Designers can control where items appear, how many spawn, how they cluster, and how powerful they are, all through simple sliders in the Unity Inspector.

**GitHub Repository**: [https://github.com/inyoo403/ItemGenerator_1](https://github.com/inyoo403/ItemGenerator_1)

---

## What It Generates

Given a procedurally generated dungeon, this tool:

- Places **randomized equipment items** (swords with unique names, rarity levels, and stat values) into selected rooms.
- Assigns **rarity tiers** (Normal, Rare, Epic, Unique, Legendary) based on how deep a room is in the dungeon.
- Generates **trade-off items** that have both powerful bonuses and meaningful penalties, creating risk-reward choices.
- Exports a **JSON log file** recording every item's name, rarity, stats, and position for analysis or comparison.

Each run produces a different layout of items. Changing any parameter or room assignment produces visibly different results while keeping the system stable and predictable.

---

## How to Run

### Prerequisites

- **Unity** 2022.3 LTS or later (2D Template recommended)
- **Required Packages**: 2D Tilemap, 2D Tilemap Extras, Input System

### Setup

1. Clone or download the repository from [GitHub](https://github.com/inyoo403/ItemGenerator_1).
2. Open the project in Unity.
3. Open the main scene containing the `DungeonGenerator` and `ItemGenerator` objects.

### Step-by-Step Usage

1. **Generate a dungeon** by selecting the `DungeonGenerator` object in the Inspector and clicking the generate button.

2. **Select the `ItemGenerator`** object and click **"Load Room Data"** to import the room list.

3. **Assign room types** in the color-coded room list. Set rooms to `Treasure` or `Boss` to receive items. Rooms left as `Normal` will remain empty.

4. **Adjust parameters** using the sliders:
   - **Item Budget**: Total number of items across the entire dungeon.
   - **Trade-Off Parameters**: Difficulty, Penalty Intensity, and Trade-off Chance.
   - **Noise Settings**: Scale and Threshold to control how items cluster.

5. Click **"1. Spawn Trade-Off Items"** to generate all items at once.

6. Click **"2. Save Data to JSON"** to export the session log to the `_Scripts/Data` folder.

7. Use **"Clear All Items"** to remove everything before re-generating.

---

## Parameters

The tool exposes 6 adjustable parameters, all controlled via sliders in the Unity Inspector.

| Parameter | Range | What It Does | Effect of Changing It |
| `totalItemBudget` | 1+ | Total number of items placed across all rooms | Higher values fill rooms with more items; lower values make loot scarce |
| `noiseScale` | 0.01 ~ 1.0 | Controls the size of item clusters | Lower values create large, spread-out clusters; higher values create tight, small groups |
| `noiseThreshold` | 0.0 ~ 1.0 | Minimum noise value for a tile to be a valid spawn point | Higher values restrict spawning to fewer peak locations; lower values allow items almost everywhere |
| `tradeOffSpawnChance` | 0 ~ 100 | Probability (%) that an item gets both bonuses and penalties | At 0% all items are clean; at 100% every item is a trade-off item with risk-reward stats |
| `penaltyIntensity` | 0.5 ~ 3.0 | How severe the penalties are on trade-off items | Higher values make penalties harsher, increasing the risk-reward tension |
| `globalDifficulty` | 0.5 ~ 2.0 | Global multiplier on all item stat values | Higher values inflate all stats across the dungeon; lower values produce weaker items overall |

Additionally, designers control item distribution by **manually assigning Room Types** (Treasure, Boss, Start, Normal), which determines which rooms receive items and which stay empty.

---

## Example Outputs

Over 25 JSON log files were generated with varying parameter settings to demonstrate the tool's range of output. Each log records the full dungeon seed, all active parameter values, and every item's name, rarity, stats, and world position.

Example logs (Item_Log_21 through Item_Log_25) show output variation after the final update, where items are placed using Perlin Noise and distributed only to rooms assigned as Treasure or Boss.

All example output files are available in the Data folder on GitHub:

[https://github.com/inyoo403/ItemGenerator_1/tree/main/Assets/_Scripts/Data](https://github.com/inyoo403/ItemGenerator_1/tree/main/Assets/_Scripts/Data)

---

## Feedback Response: Implemented Changes

### Room Type System (Suggested by Kyle Zhang)

Kyle observed that every room in the dungeon contained items, making the map feel over-saturated. He suggested introducing distinct Room Types such as Treasure Rooms and Combat Rooms so that loot distribution could be varied rather than relying solely on room level. In response, a full Room Type Assignment System was implemented. After generating a dungeon, designers can now load room data into the ItemGenerator and manually classify each room as Treasure, Boss, Start, or Normal through a color-coded Inspector UI. Items are only spawned in rooms explicitly marked as Treasure or Boss, leaving all other rooms empty. This gives designers direct control over which rooms contain loot and which remain clear, completely resolving the over-saturation issue. As a fallback for quick testing, if no room assignments are loaded, the system defaults to spawning items in all non-Start rooms.

### Parameter Constraints (Identified During Playtesting)

During playtesting, it was discovered that entering extreme minimum values into the dungeon generation parameters caused the room-splitting logic to break entirely, producing invalid or empty layouts. To address this, all key parameters now enforce strict value ranges using Unity's Range attribute. Trade-Off Spawn Chance is clamped between 0 and 100, Penalty Intensity between 0.5 and 3.0, Global Difficulty between 0.5 and 2.0, Noise Scale between 0.01 and 1.0, and Noise Threshold between 0 and 1.0.

### Perlin Noise-Based Item Placement (Self-Initiated)

In the previous version, item positions were determined using a physics-based spacing method with a fixed minSpacing parameter. This produced evenly distributed but visually uniform and grid-like item layouts that lacked organic feel. The placement system was completely replaced with a Perlin Noise sampling approach. Every floor tile in a room is now evaluated against a noise function, and only tiles whose noise value exceeds the configurable noiseThreshold qualify as spawn candidates. Candidates are then sorted by noise value in descending order, so items naturally cluster around high-noise peaks rather than being spread uniformly. The noise offset is derived from the dungeon seed, ensuring that the same seed always produces the same item layout for reproducible testing. After noise selection, a Physics2D overlap check still prevents items from spawning inside walls.

### Budget Distribution System (Self-Initiated)

Previously, each room spawned items independently with no global limit, meaning the total number of items across the dungeon was unpredictable and could vary wildly depending on room count and size. A centralized Budget Distribution System was introduced. Designers now set a single totalItemBudget value that controls exactly how many items appear across the entire dungeon. This budget is distributed proportionally across rooms using a logarithmic weighting curve based on room level, which favors deeper rooms while still granting items to early rooms.

---

## Feedback from Others (Final Version)

**Tester 1**

Key Comments: "It's impressive that all items are generated at once in a single pass. It would be nice to see item variety beyond just swords. Other equipment types like shields or accessories would make the output feel richer."

**Tester 2**

Key Comments: "The UI is excellent. Improving the readability of the item info boxes would be important, making stats and rarity easier to scan at a glance."

---

## Known Limitations

- **Sword-only output**: The generator currently only produces sword-type equipment. Other item categories (shields, accessories, armor) are not yet supported.
- **Item info readability**: The in-game item tooltip boxes could benefit from better visual formatting to improve stat readability at a glance.
- **Linear dungeon layouts**: The dungeon generator can produce multi-corridor layouts through specific parameter tuning, but there is no built-in preset for non-linear map structures.
- **No in-room obstacles**: Rooms are currently empty open spaces. Adding internal walls or obstacles would improve tactical variety but is not yet implemented.
- **Editor-only workflow**: The item spawning pipeline is designed for use in the Unity Editor. There is no runtime UI for players to adjust generation parameters during gameplay.

---

## Project Structure

```
Assets/
└── _Scripts/
    ├── ItemGenerator.cs       # Main item spawning pipeline + Custom Editor UI
    ├── Equipment.cs            # Equipment stat generation & trade-off logic
    ├── ItemObject.cs           # Runtime item component attached to spawned prefabs
    └── Data/
        ├── Item_Log.json       # Generated session log (auto-created)
        ├── Item_Log_2.json     # Incremental logs for subsequent sessions
        └── ...                 # 25+ example output files
```

---

## Credits & Attribution

### Map Generation Reference

The core map generation logic including BSP, room connection, and tilemap visualization was implemented by referencing the following tutorial:

> **Sunny Valley Studio**: [Procedural Dungeon Generation in Unity](https://www.youtube.com/watch?v=szOq1HSWtm0&t=395s)

### Original Work & Design

All systems outside of the basic map generation were designed and implemented from scratch, including:

- **Room Type Assignment System**: Manual room classification with color-coded Inspector UI.
- **Perlin Noise Placement Algorithm**: Seed-deterministic noise sampling with threshold filtering and peak-priority spawning.
- **Budget Distribution System**: Logarithmic room weighting with exact budget guarantee.
- **Level Scaling System**: BFS-based depth leveling linked to exponential rarity boosting and low-tier suppression.
- **Trade-off Mechanics**: The independent bonus/penalty slot logic, penalty intensity, and growth formulas.
- **Data Logging System**: Parameter-aware metadata logging and incremental JSON file management.
- **Custom Editor Tools**: Color-coded room assignment UI, foldout room lists, and one-click action buttons.
