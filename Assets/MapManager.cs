using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Room
{
    public int id;
    public string name;
}

public class MapManager : MonoBehaviour
{

    [Header("Map Generation Settings")]
    public int numArenas = 10; // Total number of arenas (including start and boss arenas)
    public int minSideRooms = 2; // Minimum number of side rooms
    public int maxSideRooms = 5; // Maximum number of side rooms

    [Header("Map Walls")]
    public GameObject wall;


    public List<List<Vector2Int>> roomTypes = new List<List<Vector2Int>>();

    private Dictionary<Vector2Int, Room> map = new Dictionary<Vector2Int, Room>();

    public Dictionary<Vector2Int, Room> GenerateMap()
    {
        // Generate the main path using DFS
        GenerateMainPath();

        // Add side rooms
        //AddSideRooms();

        // Omit start room
        map.Remove(new Vector2Int(0, 0));

        PrintMap();

        return map;
    }

    void GenerateMainPath()
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        // Start DFS from the start arena (0)
        Vector2Int startPos = new Vector2Int(0, 0);
        DFS(0, startPos, visited);
    }

    bool DFS(int depth, Vector2Int currentPos, HashSet<Vector2Int> visited)
    {
        visited.Add(currentPos);

        // Abort if we are next to two paths
        int numAdjacent = 0;
        for (int i = -1; i < 2; i += 2)
        {
            if (visited.Contains(new Vector2Int(currentPos.x + i, currentPos.y)))
            {
                numAdjacent++;
            }
            if (visited.Contains(new Vector2Int(currentPos.x, currentPos.y + i)))
            {
                numAdjacent++;
            }
        }
        if (numAdjacent > 1)
        {
            return false;
        }


        // Stop if we've reached the boss arena
        if (depth == numArenas - 1)
        {
            Room r = new Room { id = 1, name = "Test" };
            map.Add(currentPos, r);
            return true;
        }

        // Randomly choose the next arena in the main path
        List<int> directions = new List<int> { 0, 1, 2, 3 };
        for (int i = 0; i < 4; i++)
        {
            // Choose a direction randomly 
            int index = Random.Range(0, 4 - i);
            int dir = directions[index];
            directions.Remove(index);

            Vector2Int nextPos = new Vector2Int(currentPos.x, currentPos.y);

            switch (dir)
            {
                case 0:
                    // Up
                    nextPos.y += 1;
                    break;
                case 1:
                    // Right
                    nextPos.x += 1;
                    break;
                case 2:
                    // Down 
                    nextPos.y -= 1;
                    break;
                case 3:
                    // Left
                    nextPos.x -= 1;
                    break;
            }

            if (!visited.Contains(nextPos))
            {
                bool ret = DFS(depth + 1, nextPos, visited);
                if (ret)
                {
                    Room r = new Room { id = 1, name = "Test" };
                    map.Add(currentPos, r);
                    return true;
                }
            }
        }
        return false;
    }

    void AddSideRooms(List<int> mainPath)
    {
        int numSideRooms = UnityEngine.Random.Range(minSideRooms, maxSideRooms + 1);

        for (int i = 0; i < numSideRooms; i++)
        {
            // Choose a random arena on the main path to attach a side room
            int mainPathArena = mainPath[UnityEngine.Random.Range(0, mainPath.Count - 1)];

            // Find an unused arena to use as a side room
            int sideRoom = UnityEngine.Random.Range(1, numArenas);
            while (mainPath.Contains(sideRoom)) // Ensure the side room isn't on the main path
            {
                sideRoom = UnityEngine.Random.Range(1, numArenas);
            }

            // Connect the main path arena to the side room
            //map[mainPathArena].Add(sideRoom);

            // Optionally, connect the side room back to the main path (for loops)
            if (UnityEngine.Random.value < 0.5f) // 50% chance to create a loop
            {
                //map[sideRoom].Add(mainPathArena);
            }
        }
    }
    public void SpawnWalls(int roomSize)
    {
        foreach (var room in map)
        {
            Vector2Int coord = room.Key;

            // TODO: If there is a room check to see if it belongs to another "area" 
            if (!(coord.x - 1 == 0 && coord.y == 0) && !map.ContainsKey(new Vector2Int(coord.x - 1, coord.y)))
            {
                // Spawn wall here
                Instantiate(wall, new Vector3Int(coord.x * roomSize, 0, coord.y * roomSize), Quaternion.Euler(new Vector3(0, -90, 0)), transform);
            }
            if (!(coord.x + 1 == 0 && coord.y == 0) && !map.ContainsKey(new Vector2Int(coord.x + 1, coord.y)))
            {
                // Spawn wall here
                Instantiate(wall, new Vector3Int((coord.x + 1) * roomSize, 0, coord.y * roomSize), Quaternion.Euler(new Vector3(0, -90, 0)), transform);
            }
            if (!(coord.x == 0 && coord.y - 1 == 0) && !map.ContainsKey(new Vector2Int(coord.x, coord.y - 1)))
            {
                // Spawn wall here
                Instantiate(wall, new Vector3Int(coord.x * roomSize, 0, coord.y * roomSize), Quaternion.identity, transform);
            }
            if (!(coord.x == 0 && coord.y + 1 == 0) && !map.ContainsKey(new Vector2Int(coord.x, coord.y + 1)))
            {
                // Spawn wall here
                Instantiate(wall, new Vector3Int(coord.x * roomSize, 0, (coord.y + 1) * roomSize), Quaternion.identity, transform);
            }
        }
    }

    void PrintMap()
    {
        string path = "";
        foreach (var key in map.Keys)
        {
            path += " -> " + key.ToString();
        }
        Debug.Log(path);
    }

}
