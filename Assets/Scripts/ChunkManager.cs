using System.Collections.Generic;
using UnityEngine;

public struct Arena
{
    int x;
    int y;
}

public class ChunkManager : MonoBehaviour
{
    public Transform playerTransform;

    public int chunkSize = 128;
    public int chunkSpawnDistance = 3;
    public int chunkDestroyDistance = 5;

    public int distanceBetweenChunks = 25;

    [Header("Map Generation Settings")]
    public int numArenas = 10; // Total number of arenas (including start and boss arenas)
    public int minSideRooms = 2; // Minimum number of side rooms
    public int maxSideRooms = 5; // Maximum number of side rooms

    private WFCManager wfcManager;
    private LavaManager lavaManager;
    private Vector2Int currentPlayerChunk;
    private Dictionary<Vector2Int, bool> chunks = new Dictionary<Vector2Int, bool>();
    private Dictionary<int, List<int>> arenaMap = new Dictionary<int, List<int>>();

    private List<Vector2Int> arenas = new List<Vector2Int>();

    public void Awake()
    {
        wfcManager = GetComponent<WFCManager>();
        wfcManager.TilesPerChunk = chunkSize;
        wfcManager.Setup();

        lavaManager = GetComponent<LavaManager>();
    }

    private void Start()
    {
        // Create map layout
        GenerateMap();
        PrintMap();

        // Spawn chunks close to the player
        UpdateChunks();
    }

    private void Update()
    {
        // Calculate the player's current chunk position
        Vector2Int playerChunk = new Vector2Int(
            Mathf.FloorToInt((playerTransform.position.x) / chunkSize),
            Mathf.FloorToInt((playerTransform.position.z - chunkSize / 2) / chunkSize)
        );

        // If the player has moved to a new chunk, update the chunks
        if (playerChunk != currentPlayerChunk)
        {
            //Debug.Log(playerTransform.position + " in chunk " + playerChunk);
            currentPlayerChunk = playerChunk;
            UpdateChunks();
        }
    }

    private void UpdateChunks()
    {
        // Loop through the chunks surrounding the player and load/unload as needed
        for (int x = currentPlayerChunk.x - chunkSpawnDistance; x <= currentPlayerChunk.x + chunkSpawnDistance; x++)
        {
            for (int y = currentPlayerChunk.y - 1; y <= currentPlayerChunk.y + chunkSpawnDistance; y++)
            {
                Vector2Int chunkPos = new Vector2Int(x, y);

                // Check if the chunk is already loaded
                if (arenas.Contains(chunkPos) && !chunks.ContainsKey(chunkPos))
                {
                    chunks.Add(chunkPos, true);
                    wfcManager.CreateChunk(chunkPos);
                    lavaManager.CreateChunk(chunkPos);
                }
            }
        }

        // Unload chunks that are too far from the player
        List<Vector2Int> chunksToRemove = new List<Vector2Int>();

        foreach (Vector2Int chunk in chunks.Keys)
        {
            if (Mathf.Abs(chunk.x - currentPlayerChunk.x) > chunkDestroyDistance || Mathf.Abs(chunk.y - currentPlayerChunk.y) > chunkDestroyDistance)
            {
                chunksToRemove.Add(chunk);
                wfcManager.DestroyChunk(chunk);
                lavaManager.DestroyChunk(chunk);
            }
        }

        for (int i = chunksToRemove.Count - 1; i >= 0; i--)
        {
            chunks.Remove(chunksToRemove[i]);
        }
    }

    void GenerateMap()
    {
        // Generate the main path using DFS
        GenerateMainPath();

        // Add side rooms
        //AddSideRooms();
    }

    void GenerateMainPath()
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        // Start DFS from the start arena (0)
        Vector2Int startPos = new Vector2Int(0, 0);
        DFS(0, startPos, visited, arenas);
    }

    bool DFS(int depth, Vector2Int currentPos, HashSet<Vector2Int> visited, List<Vector2Int> mainPath)
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
            mainPath.Add(currentPos);
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
                bool ret = DFS(depth + 1, nextPos, visited, mainPath);
                if (ret)
                {
                    mainPath.Add(currentPos);
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
            arenaMap[mainPathArena].Add(sideRoom);

            // Optionally, connect the side room back to the main path (for loops)
            if (UnityEngine.Random.value < 0.5f) // 50% chance to create a loop
            {
                arenaMap[sideRoom].Add(mainPathArena);
            }
        }
    }

    void PrintMap()
    {
        string path = arenas[arenas.Count - 1].ToString();
        for (int i = arenas.Count - 2; i >= 0; i--)
        {
            path += " -> " + arenas[i].ToString();
        }
        Debug.Log(path);
    }
}