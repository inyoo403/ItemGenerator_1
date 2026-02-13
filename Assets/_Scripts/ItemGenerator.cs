using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Data structures for JSON logging
[Serializable]
public class ItemEntry {
    public string name;
    public ItemRarity rarity;
    public string stats;
    public Vector2 worldPos;
}

[Serializable]
public class RoomLog {
    public int roomLevel;
    public List<ItemEntry> items = new List<ItemEntry>();
}

[Serializable]
public class DungeonItemReport {
    public int seed;
    // Parameter metadata for comparison
    public int totalItemBudget;
    public float noiseScale;
    public float noiseThreshold;
    public float globalDifficulty;
    public float penaltyIntensity;
    public float tradeOffSpawnChance;
    public List<RoomLog> rooms = new List<RoomLog>();
}

// Lets users override room types before spawning items
[Serializable]
public class RoomTypeOverride {
    public int roomIndex; // original index in DungeonSaveData.rooms
    public int roomLevel;
    public RoomType type;
}

public class ItemGenerator : MonoBehaviour
{
    [Serializable]
    public struct RarityWeight {
        public ItemRarity rarity;
        public int baseWeight; 
        public Sprite sprite; 
    }

    [Header("Item Budget")]
    [SerializeField] private int totalItemBudget = 50;

    [Header("Prefabs & Scaling")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Vector3 itemScale = new Vector3(2.5f, 2.5f, 1f); 

    [Header("Trade-Off Parameters")]
    [Range(0, 100)]
    [SerializeField] private float tradeOffSpawnChance = 50f;
    [Range(0.5f, 3.0f)]
    [SerializeField] private float penaltyIntensity = 1.0f;
    [Range(0.5f, 2.0f)]
    [SerializeField] private float globalDifficulty = 1.0f;

    [Header("Perlin Noise Placement")]
    [Range(0.01f, 1f)]
    [SerializeField] private float noiseScale = 0.15f;
    [Range(0f, 1f)]
    [SerializeField] private float noiseThreshold = 0.45f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float wallCheckRadius = 0.45f;

    [Header("Dependencies")]
    [SerializeField] private RoomFirstDungeonGenerator dungeonGenerator;
    [SerializeField] private Tilemap floorTilemap; 
    [SerializeField] private List<RarityWeight> raritySettings; 

    // User-editable room type assignments (populated via LoadRoomAssignments)
    [HideInInspector]
    [SerializeField] private List<RoomTypeOverride> roomAssignments = new List<RoomTypeOverride>();

    private DungeonItemReport currentSessionData;

    // Loads room data from the dungeon generator so users can assign types
    public void LoadRoomAssignments()
    {
        if (dungeonGenerator == null) dungeonGenerator = FindFirstObjectByType<RoomFirstDungeonGenerator>();
        if (dungeonGenerator == null || dungeonGenerator.currentDungeonData == null)
        {
            Debug.LogWarning("Dungeon data not found. Generate a dungeon first.");
            return;
        }

        roomAssignments.Clear();
        var rooms = dungeonGenerator.currentDungeonData.rooms;
        for (int i = 0; i < rooms.Count; i++)
        {
            roomAssignments.Add(new RoomTypeOverride {
                roomIndex = i,
                roomLevel = rooms[i].roomLevel,
                type = rooms[i].type
            });
        }
        // Sort by level so the inspector list is easy to read
        roomAssignments = roomAssignments.OrderBy(r => r.roomLevel).ToList();
        Debug.Log($"Loaded {roomAssignments.Count} rooms. Assign Treasure/Boss types, then spawn items.");
    }

    // Entry point for manual generation
    public void ManualSpawn()
    {
        if (floorTilemap == null) floorTilemap = GameObject.Find("Floor").GetComponent<Tilemap>();
        if (dungeonGenerator == null) dungeonGenerator = FindFirstObjectByType<RoomFirstDungeonGenerator>();
        if (dungeonGenerator == null || dungeonGenerator.currentDungeonData == null) return;

        ExecuteSpawning(dungeonGenerator.currentDungeonData);
    }

    private void ExecuteSpawning(DungeonSaveData data)
    {
        ClearItems();
        
        // Store current parameter values in the report
        currentSessionData = new DungeonItemReport { 
            seed = data.seed,
            totalItemBudget = this.totalItemBudget,
            noiseScale = this.noiseScale,
            noiseThreshold = this.noiseThreshold,
            globalDifficulty = this.globalDifficulty,
            penaltyIntensity = this.penaltyIntensity,
            tradeOffSpawnChance = this.tradeOffSpawnChance
        };

        GameObject rootFolder = new GameObject("--- [FINAL] Items Root ---");
        rootFolder.transform.SetParent(this.transform);

        // Build spawnable room list using user assignments (Treasure/Boss only)
        List<RoomSaveData> spawnableRooms = new List<RoomSaveData>();

        bool hasAssignments = roomAssignments.Count > 0 && roomAssignments.Count == data.rooms.Count;
        if (hasAssignments)
        {
            foreach (var assignment in roomAssignments)
            {
                if (assignment.type == RoomType.Treasure || assignment.type == RoomType.Boss)
                    spawnableRooms.Add(data.rooms[assignment.roomIndex]);
            }
        }
        else
        {
            // Fallback: no assignments loaded — use all non-Start rooms
            spawnableRooms = data.rooms.Where(r => r.type != RoomType.Start).ToList();
        }

        var sortedRooms = spawnableRooms.OrderBy(r => r.roomLevel).ToList();
        if (sortedRooms.Count == 0)
        {
            Debug.LogWarning("No Treasure or Boss rooms assigned. Load room data and assign types first.");
            return;
        }

        // Distribute totalItemBudget across rooms weighted by roomLevel
        int[] allocations = DistributeBudget(sortedRooms);

        for (int i = 0; i < sortedRooms.Count; i++)
        {
            var room = sortedRooms[i];
            
            GameObject roomFolder = new GameObject($"Room_{room.roomLevel:D2}_Folder");
            roomFolder.transform.SetParent(rootFolder.transform);

            RoomLog roomLog = new RoomLog { roomLevel = room.roomLevel };
            SpawnInRoom(room, allocations[i], roomFolder.transform, roomLog);
            currentSessionData.rooms.Add(roomLog);
        }
    }

    // Distributes totalItemBudget proportionally based on room level weights.
    // Total allocated is guaranteed to equal totalItemBudget exactly.
    private int[] DistributeBudget(List<RoomSaveData> rooms)
    {
        int roomCount = rooms.Count;
        float[] weights = new float[roomCount];
        float totalWeight = 0f;

        for (int i = 0; i < roomCount; i++)
        {
            // Logarithmic curve: still favors higher levels, but distributes more evenly
            weights[i] = 1f + Mathf.Log(1f + rooms[i].roomLevel) * 0.8f;
            totalWeight += weights[i];
        }

        int[] allocations = new int[roomCount];
        int allocated = 0;

        // First pass: proportional allocation with floor (no forced minimum)
        for (int i = 0; i < roomCount; i++)
        {
            allocations[i] = Mathf.FloorToInt((weights[i] / totalWeight) * totalItemBudget);
            allocated += allocations[i];
        }

        // Second pass: distribute remaining budget one-by-one to highest-weight rooms
        int remaining = totalItemBudget - allocated;
        if (remaining > 0)
        {
            var sortedIndices = Enumerable.Range(0, roomCount)
                .OrderByDescending(idx => weights[idx])
                .ToList();

            for (int j = 0; j < remaining; j++)
            {
                allocations[sortedIndices[j % roomCount]]++;
            }
        }

        return allocations;
    }

    private void SpawnInRoom(RoomSaveData room, int targetCount, Transform parent, RoomLog log)
    {
        // Derive noise offset from dungeon seed for reproducibility
        float noiseOffsetX = (currentSessionData.seed % 10000) * 0.01f;
        float noiseOffsetY = ((currentSessionData.seed / 10000) % 10000) * 0.01f;

        // Sample Perlin noise on every floor tile and filter by threshold
        List<(Vector2Int pos, float noise)> candidates = new List<(Vector2Int, float)>();
        for (int x = room.min.x + 1; x < room.min.x + room.size.x - 1; x++)
        {
            for (int y = room.min.y + 1; y < room.min.y + room.size.y - 1; y++)
            {
                if (!floorTilemap.HasTile(new Vector3Int(x, y, 0))) continue;

                float noiseVal = Mathf.PerlinNoise(
                    (x + noiseOffsetX) * noiseScale,
                    (y + noiseOffsetY) * noiseScale
                );

                if (noiseVal >= noiseThreshold)
                    candidates.Add((new Vector2Int(x, y), noiseVal));
            }
        }

        // Sort by noise value descending — items cluster in high-noise peaks
        candidates = candidates.OrderByDescending(c => c.noise).ToList();

        int spawned = 0;
        foreach (var (pos, noise) in candidates)
        {
            if (spawned >= targetCount) break;
            Vector3 worldPos = new Vector3(pos.x + 0.5f, pos.y + 0.5f, -1f);

            // Wall collision check (physical constraint only)
            if (Physics2D.OverlapCircle(worldPos, wallCheckRadius, wallLayer) != null) continue;

            ItemRarity rarity = GetPureLevelRarity(room.roomLevel);
            bool rollTradeOff = UnityEngine.Random.Range(0f, 100f) < tradeOffSpawnChance;

            Equipment equip = new Equipment(rarity, room.roomLevel, globalDifficulty, penaltyIntensity, rollTradeOff);

            CreateItemObject(worldPos, equip, parent);
            log.items.Add(new ItemEntry {
                name = equip.itemName,
                rarity = equip.rarity,
                stats = equip.GetStatString(),
                worldPos = new Vector2(worldPos.x, worldPos.y)
            });

            spawned++;
        }
    }

    private ItemRarity GetPureLevelRarity(int level)
    {
        float total = 0;
        List<float> weights = new List<float>();
        for (int i = 0; i < raritySettings.Count; i++) {
            float boost = Mathf.Pow(i + 1, level * 0.15f * 1.8f);
            float suppression = (level > 10 && i < 2) ? 1f / (level - 9) : 1f;
            float finalW = raritySettings[i].baseWeight * boost * suppression;
            weights.Add(finalW);
            total += finalW;
        }

        float roll = UnityEngine.Random.Range(0, total);
        float cursor = 0;
        for (int i = 0; i < weights.Count; i++) {
            cursor += weights[i];
            if (roll <= cursor) return raritySettings[i].rarity;
        }
        return ItemRarity.Normal;
    }

    private void CreateItemObject(Vector3 pos, Equipment data, Transform parent)
    {
        GameObject obj = Instantiate(itemPrefab, pos, Quaternion.identity, parent);
        obj.transform.localScale = itemScale;
        obj.tag = "Item";
        data.itemSprite = raritySettings.Find(s => s.rarity == data.rarity).sprite;
        if (obj.TryGetComponent(out ItemObject itemObj)) itemObj.Setup(data);
        obj.name = data.itemName;
    }

    // Incremental file saving to prevent overwriting
    public void SaveLogToJSON()
    {
        if (currentSessionData == null) return;
        
        string dir = Path.Combine(Application.dataPath, "_Scripts/Data");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string fileName = "Item_Log";
        string extension = ".json";
        string fullPath = Path.Combine(dir, fileName + extension);

        // Logic to increment filename if it already exists
        int counter = 2;
        while (File.Exists(fullPath))
        {
            fullPath = Path.Combine(dir, $"{fileName}_{counter}{extension}");
            counter++;
        }

        File.WriteAllText(fullPath, JsonUtility.ToJson(currentSessionData, true));
        Debug.Log($"Log saved to: {Path.GetFileName(fullPath)}");
#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    public void ClearItems()
    {
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform t in transform) toDestroy.Add(t.gameObject);
        foreach (GameObject g in toDestroy) {
            if (Application.isPlaying) Destroy(g);
            else DestroyImmediate(g);
        }
        foreach (var s in GameObject.FindGameObjectsWithTag("Item")) {
            if (Application.isPlaying) Destroy(s);
            else DestroyImmediate(s);
        }
    }
}

// Editor interface
#if UNITY_EDITOR
[CustomEditor(typeof(ItemGenerator))]
public class ItemGeneratorEditor : Editor
{
    private bool showRoomList = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw all default fields (roomAssignments is HideInInspector so it won't show)
        DrawPropertiesExcluding(serializedObject, "m_Script");

        ItemGenerator gen = (ItemGenerator)target;

        // --- Room Type Assignment Section ---
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Room Type Assignment", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load Room Data")) gen.LoadRoomAssignments();
        if (GUILayout.Button("Clear Assignments"))
        {
            var prop = serializedObject.FindProperty("roomAssignments");
            prop.ClearArray();
        }
        EditorGUILayout.EndHorizontal();

        SerializedProperty assignments = serializedObject.FindProperty("roomAssignments");

        if (assignments.arraySize > 0)
        {
            showRoomList = EditorGUILayout.Foldout(showRoomList, $"Rooms ({assignments.arraySize})", true);
            if (showRoomList)
            {
                EditorGUI.indentLevel++;
                int treasureCount = 0, bossCount = 0;

                for (int i = 0; i < assignments.arraySize; i++)
                {
                    var element = assignments.GetArrayElementAtIndex(i);
                    var levelProp = element.FindPropertyRelative("roomLevel");
                    var typeProp = element.FindPropertyRelative("type");

                    RoomType currentType = (RoomType)typeProp.enumValueIndex;
                    if (currentType == RoomType.Treasure) treasureCount++;
                    if (currentType == RoomType.Boss) bossCount++;

                    // Color-code by type
                    Color rowColor = currentType switch
                    {
                        RoomType.Treasure => new Color(1f, 0.85f, 0.2f),
                        RoomType.Boss => new Color(1f, 0.3f, 0.3f),
                        RoomType.Start => new Color(0.4f, 0.8f, 1f),
                        _ => Color.white
                    };
                    Color prev = GUI.color;
                    GUI.color = rowColor;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Room {i + 1}  |  Lv.{levelProp.intValue}", GUILayout.Width(160));
                    EditorGUILayout.PropertyField(typeProp, GUIContent.none);
                    EditorGUILayout.EndHorizontal();

                    GUI.color = prev;
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.HelpBox(
                    $"Treasure: {treasureCount}  |  Boss: {bossCount}  →  Items will spawn in these rooms only.",
                    MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "No room data loaded. Generate a dungeon first, then click 'Load Room Data'.",
                MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();

        // --- Action Buttons ---
        GUILayout.Space(15);
        if (GUILayout.Button("1. Spawn Trade-Off Items")) gen.ManualSpawn();
        if (GUILayout.Button("2. Save Data to JSON")) gen.SaveLogToJSON();
        if (GUILayout.Button("Clear All Items")) gen.ClearItems();
    }
}
#endif