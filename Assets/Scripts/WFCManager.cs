

using JetBrains.Annotations;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UIElements;

public class WFCManager : MonoBehaviour
{
    public Vector3Int dimensions = new Vector3Int(5, 5, 5);
    public int numberOfJobs = 1;
    public XML_IO XML_IO;
    public int tileSize = 8;

    private Dictionary<Vector2Int, Chunk> chunks;

    // Keep track of these to make instantiating possible - and to create NativeTileTypes
    List<TileType> imported_tiles;

    // These should remain the same for all chunks
    private NativeArray<NativeTileType> tileTypes;
    private NativeArray<bool> neighborData;
    private NativeArray<bool> hasConnectionData;

    // These need to be created for each new job
    //private NativeArray<bool> tileMapArray;
    //private NativeList<Vector3Int> tilesToProcess;
    
    
    // Gaming
    private int tileCount;

    private void Awake()
    {
        XML_IO.ClearTileTypes();
        XML_IO.Import();
        imported_tiles = XML_IO.GetTileTypes();
        tileCount = imported_tiles.Count;

        // Create the arrays needed for all jobs
        tileTypes = new NativeArray<NativeTileType>(tileCount, Allocator.Persistent);
        neighborData = new NativeArray<bool>(tileCount * tileCount * 6, Allocator.Persistent); // (Number of tiles (n# depends) ^ 2) * directions (6)
        hasConnectionData = new NativeArray<bool>(tileCount * 6, Allocator.Persistent); // Number of tiles (n# depends) * directions (6)

        // Create NativeTileTypes - These have very little information
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
        chunks = new Dictionary<Vector2Int, Chunk> ();

        // Create chunk & jobs
        for (i = 0; i < numberOfJobs; i++)
        {
            // Create chunk
            Vector2Int position = new Vector2Int(i, 0);
            Chunk chunk = new Chunk();
            chunk.position = position;

            // Allocate memory for the chunk/job
            NativeArray<int> tileMap = new NativeArray<int> (dimensions.x * dimensions.y * dimensions.z, Allocator.Persistent);
            NativeArray<bool> tileMapArray = new NativeArray<bool>(dimensions.x * dimensions.y * dimensions.z * tileCount, Allocator.Persistent);
            NativeList<Vector3Int> tilesToProcess = new NativeList<Vector3Int>(0, Allocator.Persistent);

            // Create job
            JobWFC job = new JobWFC(position, dimensions, tileTypes.AsReadOnly(), tileMapArray, tileCount, tileMap, neighborData.AsReadOnly(), hasConnectionData.AsReadOnly(), tilesToProcess);
            
            // Fill chunk with datachunks
            chunk.tileMap = tileMap;
            chunk.tileMapArray = tileMapArray;
            chunk.tilesToProcess = tilesToProcess;
            chunk.jobWFC = job;

            // Store chunk
            chunks[position] = chunk;
        }
    }

    public void Start()
    {
        Debug.Log("Starting Jobs");

        for (int i = 0; i < numberOfJobs; i++)
        {
            Vector2Int position = new Vector2Int(i, 0);
            chunks[position].jobHandle = chunks[position].jobWFC.Schedule();
            Debug.Log("Scheduled job #" + i);
        }

        Debug.Log("Done");
    }

    public void LateUpdate()
    {
        foreach (Chunk c in chunks.Values)
        {
            if (!c.isInstantiated) {
                c.jobHandle.Complete();
                if (c.jobHandle.IsCompleted)
                {
                    InstantiateTiles(c.position);
                    c.isInstantiated = true;
                }
            }
        }
    }

    private void OnDestroy()
    {
        tileTypes.Dispose();
        
        neighborData.Dispose();
        hasConnectionData.Dispose();

        // Dispose of data - This should be done earlier for at least tileMapArray and tilesToProcess
        foreach (Chunk c in chunks.Values)
        {
            c.tileMap.Dispose();
            c.tileMapArray.Dispose();
            c.tilesToProcess.Dispose();
        }
    }

    private int ConvertTo1D(int x, int y, int z)
    {
        return x + dimensions.x * (y + dimensions.y * z);
    }

    private void InstantiateTiles(Vector2Int position)
    {
        NativeArray<int> tileMap = chunks[position].tileMap;
        int xOffset = position.x * dimensions.x * tileSize;
        int zOffset = position.y * dimensions.z * tileSize;
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
                        obj.transform.parent = transform;
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