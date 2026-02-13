using System;
using System.Collections.Generic;
using UnityEngine;

// Defines the role of each room within the dungeon
public enum RoomType { Common, Treasure, Boss, Start }

[Serializable]
public class RoomSaveData
{
    public Vector3Int min;
    public Vector3Int size;
    public RoomType type;
    public int roomLevel; // Path depth from the starting room (BFS distance)
}

[Serializable]
public class DungeonSaveData
{
    public int seed;
    public List<RoomSaveData> rooms = new List<RoomSaveData>();
}