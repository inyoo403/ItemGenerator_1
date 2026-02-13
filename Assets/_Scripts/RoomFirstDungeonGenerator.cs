using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RoomFirstDungeonGenerator : SimpleRandomWalkDungeonGenerator
{
    [SerializeField] private int minRoomWidth = 4, minRoomHeight = 4;
    [SerializeField] private int dungeonWidth = 20, dungeonHeight = 20;
    [SerializeField] [Range(0, 10)] private int offset = 1;
    [SerializeField] private bool randomWalkRooms = false;

    [Header("Save & Seed System")]
    [SerializeField] private bool useFixedSeed = false;
    [SerializeField] private int seed;

    [Header("Data Management")]
    [SerializeField] private string saveFileName = "dungeon_data.json";
    [SerializeField] private TextAsset dungeonDataFile;

    [Header("Debug Visualizer")]
    [SerializeField] private bool showRoomLevels = true;

    public DungeonSaveData currentDungeonData;

    protected override void RunProceduralGeneration()
    {
        CreateRooms();
    }

    private void CreateRooms()
    {
        if (!useFixedSeed)
        {
            seed = DateTime.Now.Ticks.GetHashCode();
        }
        Random.InitState(seed);

        var roomsList = ProceduralGenerationAlgorithms.BinarySpacePartitioning(
            new BoundsInt((Vector3Int)startPosition, new Vector3Int(dungeonWidth, dungeonHeight, 0)), 
            minRoomWidth, minRoomHeight);

        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        currentDungeonData = new DungeonSaveData { seed = this.seed };

        if (randomWalkRooms) floor = CreateRoomsRandomly(roomsList);
        else floor = CreateSimpleRooms(roomsList);

        // Prepare centers and a dictionary to track room connections
        List<Vector2Int> roomCenters = roomsList.Select(room => (Vector2Int)Vector3Int.RoundToInt(room.center)).ToList();
        
        // Track connections between room indices for BFS
        Dictionary<int, List<int>> roomGraph = new Dictionary<int, List<int>>();
        for (int i = 0; i < roomsList.Count; i++) roomGraph[i] = new List<int>();

        HashSet<Vector2Int> corridors = ConnectRoomsBFS(roomCenters, roomGraph);
        floor.UnionWith(corridors);

        // --- BFS Leveling Logic ---
        int[] levels = CalculateLevelsByBFS(roomsList.Count, roomGraph);

        // Find the room with the highest BFS level for Boss assignment
        int bossIndex = 0;
        for (int i = 1; i < levels.Length; i++)
        {
            if (levels[i] > levels[bossIndex]) bossIndex = i;
        }

        for (int i = 0; i < roomsList.Count; i++)
        {
            RoomType type = RoomType.Common;
            if (i == 0) type = RoomType.Start;
            else if (i == bossIndex) type = RoomType.Boss;
            else if (Random.value < 0.2f) type = RoomType.Treasure;

            currentDungeonData.rooms.Add(new RoomSaveData {
                min = roomsList[i].min,
                size = roomsList[i].size,
                type = type,
                roomLevel = levels[i] // Assign level based on path depth
            });
        }

        tilemapVisualizer.PaintFloorTiles(floor);
        WallGenerator.CreateWalls(floor, tilemapVisualizer);
    }

    // Breadth-First Search to determine the depth of each room from the start
    private int[] CalculateLevelsByBFS(int roomCount, Dictionary<int, List<int>> graph)
    {
        int[] levels = new int[roomCount];
        bool[] visited = new bool[roomCount];
        Queue<int> queue = new Queue<int>();

        // Start from the first room (Index 0)
        queue.Enqueue(0);
        visited[0] = true;
        levels[0] = 1;

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();

            foreach (int neighbor in graph[current])
            {
                if (!visited[neighbor])
                {
                    visited[neighbor] = true;
                    levels[neighbor] = levels[current] + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }
        return levels;
    }

    private HashSet<Vector2Int> ConnectRoomsBFS(List<Vector2Int> roomCenters, Dictionary<int, List<int>> graph)
    {
        HashSet<Vector2Int> corridors = new HashSet<Vector2Int>();
        // Map centers to original indices to build the graph
        List<Vector2Int> centersCopy = new List<Vector2Int>(roomCenters);
        
        var currentCenter = centersCopy[Random.Range(0, centersCopy.Count)];
        centersCopy.Remove(currentCenter);

        while (centersCopy.Count > 0)
        {
            Vector2Int closest = FindClosestPointTo(currentCenter, centersCopy);
            
            // Log connection in the graph
            int indexA = roomCenters.IndexOf(currentCenter);
            int indexB = roomCenters.IndexOf(closest);
            graph[indexA].Add(indexB);
            graph[indexB].Add(indexA);

            centersCopy.Remove(closest);
            HashSet<Vector2Int> newCorridor = CreateCorridor(currentCenter, closest);
            currentCenter = closest;
            corridors.UnionWith(newCorridor);
        }
        return corridors;
    }

    private void OnDrawGizmos()
    {
        if (showRoomLevels && currentDungeonData != null && currentDungeonData.rooms != null)
        {
            foreach (var room in currentDungeonData.rooms)
            {
                Vector3 center = new Vector3(room.min.x + room.size.x / 2f, room.min.y + room.size.y / 2f, 0);

#if UNITY_EDITOR
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.yellow;
                style.fontSize = 20;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;

                Handles.Label(center, room.roomLevel.ToString(), style);
#endif
            }
        }
    }

    public void SaveDungeon()
    {
        if (currentDungeonData == null) return;
        string directoryPath = Path.Combine(Application.dataPath, "_Scripts/Data");
        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
        string json = JsonUtility.ToJson(currentDungeonData, true);
        string fullPath = Path.Combine(directoryPath, saveFileName);
        File.WriteAllText(fullPath, json);
#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    public void LoadDungeon()
    {
        string json = "";
        if (dungeonDataFile != null) json = dungeonDataFile.text;
        else
        {
            string fullPath = Path.Combine(Application.dataPath, "_Scripts/Data", saveFileName);
            if (!File.Exists(fullPath)) return;
            json = File.ReadAllText(fullPath);
        }
        currentDungeonData = JsonUtility.FromJson<DungeonSaveData>(json);
        this.seed = currentDungeonData.seed;
        this.useFixedSeed = true;
        GenerateDungeon();
    }

    private HashSet<Vector2Int> CreateRoomsRandomly(List<BoundsInt> roomsList)
    {
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        for (int i = 0; i < roomsList.Count; i++)
        {
            var roomBounds = roomsList[i];
            var roomCenter = new Vector2Int(Mathf.RoundToInt(roomBounds.center.x), Mathf.RoundToInt(roomBounds.center.y));
            var roomFloor = RunRandomWalk(randomWalkParameters, roomCenter);
            foreach (var position in roomFloor)
            {
                if (position.x >= (roomBounds.xMin + offset) && position.x <= (roomBounds.xMax - offset) && 
                    position.y >= (roomBounds.yMin + offset) && position.y <= (roomBounds.yMax - offset))
                {
                    floor.Add(position);
                }
            }
        }
        return floor;
    }

    private HashSet<Vector2Int> CreateCorridor(Vector2Int current, Vector2Int destination)
    {
        HashSet<Vector2Int> corridor = new HashSet<Vector2Int>();
        var position = current;
        corridor.Add(position);
        while (position.y != destination.y)
        {
            if (destination.y > position.y) position += Vector2Int.up;
            else position += Vector2Int.down;
            corridor.Add(position);
        }
        while (position.x != destination.x)
        {
            if (destination.x > position.x) position += Vector2Int.right;
            else position += Vector2Int.left;
            corridor.Add(position);
        }
        return corridor;
    }

    private Vector2Int FindClosestPointTo(Vector2Int current, List<Vector2Int> roomCenters)
    {
        Vector2Int closest = Vector2Int.zero;
        float distance = float.MaxValue;
        foreach (var pos in roomCenters)
        {
            float currentDist = Vector2.Distance(pos, current);
            if (currentDist < distance)
            {
                distance = currentDist;
                closest = pos;
            }
        }
        return closest;
    }

    private HashSet<Vector2Int> CreateSimpleRooms(List<BoundsInt> roomsList)
    {
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        foreach (var room in roomsList)
        {
            for (int col = offset; col < room.size.x - offset; col++)
            {
                for (int row = offset; row < room.size.y - offset; row++)
                {
                    Vector2Int position = (Vector2Int)room.min + new Vector2Int(col, row);
                    floor.Add(position);
                }
            }
        }
        return floor;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(RoomFirstDungeonGenerator), true)]
public class DungeonGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        RoomFirstDungeonGenerator generator = (RoomFirstDungeonGenerator)target;
        GUILayout.Space(15);
        if (GUILayout.Button("1. Create Dungeon")) generator.GenerateDungeon();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("2. Save Data")) generator.SaveDungeon();
        if (GUILayout.Button("3. Load Data")) generator.LoadDungeon();
        GUILayout.EndHorizontal();
    }
}
#endif