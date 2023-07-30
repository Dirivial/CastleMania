

using JetBrains.Annotations;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UIElements;

public class WFCManager : MonoBehaviour
{
    public int chunkSize = 15;
    public int numberOfFloors = 3;
    public int numberOfJobs = 1;
    public XML_IO XML_IO;
    public int tileSize = 8;

    private Dictionary<Vector2Int, ChunkWFC> chunks;
    private Vector3Int dimensions = new Vector3Int(5, 5, 5);

    // Keep track of these to make instantiating possible - and to create NativeTileTypes
    List<TileType> imported_tiles;

    List<Vector2Int> pendingChunks = new List<Vector2Int>();

    // These should remain the same for all chunks
    private NativeArray<NativeTileType> tileTypes;
    private NativeArray<bool> neighborData;
    private NativeArray<bool> hasConnectionData;
    
    // Gaming
    private int tileCount;

    private void Awake()
    {
        dimensions = new Vector3Int(chunkSize, numberOfFloors, chunkSize);

        XML_IO.ClearTileTypes();
        XML_IO.Import();
        imported_tiles = XML_IO.GetTileTypes();
        tileCount = imported_tiles.Count;
        

        // Create the arrays needed for all jobs
        tileTypes = new NativeArray<NativeTileType>(tileCount, Allocator.Persistent);
        neighborData = new NativeArray<bool>(tileCount * tileCount * 6, Allocator.Persistent); // (Number of tiles (n# depends) ^ 2) * directions (6)
        hasConnectionData = new NativeArray<bool>(tileCount * 6, Allocator.Persistent); // Number of tiles (n# depends) * directions (6)

        // Create NativeTileTypes - A restricted TileType
        int i = 0;
        foreach (TileType tileType in imported_tiles)
        {
            NativeTileType tile = new NativeTileType(tileType.weight);
            tile.noRepeatH = tileType.noRepeatH;
            tile.noRepeatV = tileType.noRepeatV;
            tile.mustConnect = tileType.mustConnect;
            tile.grounded = tileType.grounded;

            ComputeNeighborData(tileType, i);
            ComputeHasConnection(tileType, i);

            tileTypes[i] = tile;
            i++;
        }

        // Create dictionary to access chunks
        chunks = new Dictionary<Vector2Int, ChunkWFC> ();
    }

    public void LateUpdate()
    {
        for (int i = pendingChunks.Count - 1; i >= 0; i--)
        {
            if (chunks.ContainsKey(pendingChunks[i]) && chunks[pendingChunks[i]].jobHandle.IsCompleted)
            {
                chunks[pendingChunks[i]].jobHandle.Complete();
                InstantiateTiles(pendingChunks[i]);
                chunks[pendingChunks[i]].jobWFC.OnDestroy();
                chunks[pendingChunks[i]].isInstantiated = true;
                pendingChunks.RemoveAt(i);
            }
        }
    }

    private void OnDestroy()
    {
        tileTypes.Dispose();
        
        neighborData.Dispose();
        hasConnectionData.Dispose();

        // Dispose of data - This should be done earlier for at least tileMapArray and tilesToProcess
        foreach (ChunkWFC c in chunks.Values)
        {
            c.tileMap.Dispose();
            c.tileMapArray.Dispose();
            c.tilesToProcess.Dispose();
        }
    }

    public void UpdateChunks(Vector2Int playerPosition, int chunkCount)
    {
        // Loop through the chunks surrounding the player and load/unload as needed
        for (int x = playerPosition.x - chunkCount; x <= playerPosition.x + chunkCount; x++)
        {
            for (int y = playerPosition.y - 1; y <= playerPosition.y + chunkCount; y++)
            {
                Vector2Int chunkPos = new Vector2Int(x, y);

                // Check if the chunk is already loaded
                if (!chunks.ContainsKey(chunkPos))
                {
                    CreateChunk(chunkPos);
                }
            }
        }
    
        // Unload chunks that are too far from the player
        List<Vector2Int> chunksToRemove = new List<Vector2Int>();
        foreach (Vector2Int chunk in chunks.Keys)
        {
            if (Mathf.Abs(chunk.x - playerPosition.x) > chunkCount || Mathf.Abs(chunk.y - playerPosition.y) > chunkCount)
            {
                chunksToRemove.Add(chunk);
            }
        }

        foreach (var chunkPos in chunksToRemove)
        {
            if (!chunks.ContainsKey(chunkPos)) { continue; }
            foreach (GameObject tile in chunks[chunkPos].tiles)
            {
                Destroy(tile);
            }
            chunks.Remove(chunkPos);
        }
    }

    private void CreateChunk(Vector2Int chunkPos)
    {
        // Create chunk
        ChunkWFC chunk = new ChunkWFC();
        chunk.position = chunkPos;

        // Allocate memory for the chunk/job
        NativeArray<int> tileMap = new NativeArray<int>(dimensions.x * dimensions.y * dimensions.z, Allocator.Persistent);
        NativeArray<bool> tileMapArray = new NativeArray<bool>(dimensions.x * dimensions.y * dimensions.z * tileCount, Allocator.Persistent);
        NativeList<Vector3Int> tilesToProcess = new NativeList<Vector3Int>(0, Allocator.Persistent);

        // Create job
        JobWFC job = new JobWFC(dimensions, tileTypes.AsReadOnly(), tileCount, tileMap, neighborData.AsReadOnly(), hasConnectionData.AsReadOnly());

        // Fill chunk with datachunks
        chunk.tileMap = tileMap;
        chunk.tileMapArray = tileMapArray;
        chunk.tilesToProcess = tilesToProcess;
        chunk.jobWFC = job;

        // Store chunk
        chunks.Add(chunkPos, chunk);

        pendingChunks.Add(chunkPos);

        chunks[chunkPos].jobHandle = chunks[chunkPos].jobWFC.Schedule();
        Debug.Log("Scheduled job @ " + chunkPos);
    }

    private void CreateChunks(List<Vector2Int> positions)
    {
        // Create chunk & jobs
        for (int i = 0; i < positions.Count; i++)
        {
            CreateChunk(positions[i]);
        }
    }

    private int ConvertTo1D(int x, int y, int z)
    {
        return x + dimensions.x * (y + dimensions.y * z);
    }

    private void InstantiateTiles(Vector2Int position)
    {
        NativeArray<int> tileMap = chunks[position].tileMap;
        int xOffset = position.x * chunkSize * tileSize;
        int zOffset = position.y * chunkSize * tileSize;
        int scale = 50 * tileSize;
        Vector3Int tileScaling = new Vector3Int(scale, scale, scale);
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int index = tileMap[ConvertTo1D(x, y, z)];
                    if (index >= 0 && imported_tiles[index].name != "-1")
                    {
                        int height = y;//floorHeights[y];

                        GameObject obj = Instantiate(imported_tiles[index].tileObject, new Vector3(x * tileSize + xOffset, height * tileSize, z * tileSize + zOffset), imported_tiles[index].rotation);
                        obj.transform.localScale = tileScaling;
                        chunks[position].tiles.Add(obj);
                        //obj.transform.parent = transform;
                        //instantiatedTiles.Add(obj);
                    }
                }
            }
        }
    }


    private void ComputeNeighborData(TileType tileType, int index)
    {
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < tileCount; j++)
            {
                neighborData[index + tileCount * (i + 6 * j)] = tileType.neighbors[i][j];
            }
        }
    }

    private void ComputeHasConnection(TileType tileType, int index)
    {
        bool anyPositive = false;
        for (int i = 0;i < 6; i++)
        {
            for (int j = 0; j < tileCount; j++)
            {
                if (tileType.neighbors[i][j])
                {
                    hasConnectionData[index + tileCount * i] = true;
                    anyPositive = true;
                    break;
                }
            }
            if (!anyPositive)
            {
                hasConnectionData[index + tileCount * i] = false;
            }
            anyPositive = false;
        }
    }
}