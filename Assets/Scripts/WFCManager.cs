using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Random = UnityEngine.Random;

public struct TowerTile
{
    public Vector3Int position;
    public int tileId;
}

public class TowerJob
{
    public Vector2Int chunkPos;
    public JobHandle job;
    public TowerGrowthJob towerJob;
}

public class WFCManager : Manager
{
    public Vector3Int tileScale = new Vector3Int(400, 400, 400);

    public List<Vector2Int> selectedTiles = new List<Vector2Int>();
    public int numberOfFloors = 3;
    public int floorGapMin = 1;
    public int floorGapMax = 4;
    public int tilesBelowBottomFloor = 3;
    public XML_IO XML_IO;
    public int tileSize = 8;
    public AnimationCurve towerGrowth;

    // Keep track of these to make instantiating possible - and to create NativeTileTypes
    List<TileType> imported_tiles;

    // These should remain the same for all chunks
    private NativeArray<NativeTileType> tileTypes;
    private NativeArray<bool> neighborData;
    private NativeArray<bool> hasConnectionData;

    private int tileCount;
    private Dictionary<Vector2Int, ChunkWFC> chunks;
    List<Vector2Int> pendingChunks = new List<Vector2Int>();
    List<TowerJob> scheduledTowerJobs = new List<TowerJob>();
    private Vector3Int dimensions = new Vector3Int(5, 5, 5);
    // private static int EMPTY_TILE = -2;
    private int tilesPerChunk = 15;
    private int[] floorHeights;
    //private static int UNDECIDED = -1;

    // Implementation specific
    private int tower_bottom = -2;
    private int tower_top = -2;
    private int tower = -2;
    private int tower_window = -2;

    private uint seed = 1;

    private TilePooler pooler;

    public int TilesPerChunk { get => tilesPerChunk; set => tilesPerChunk = value / tileSize; }

    public void Setup()
    {
        seed = (uint)Random.Range(1, Int32.MaxValue);

        dimensions = new Vector3Int(TilesPerChunk, numberOfFloors, TilesPerChunk);

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
            tile.isTowerTile = tileType.isTowerType;

            ComputeNeighborData(tileType, i);
            ComputeHasConnection(tileType, i);

            // Set indices of the tower tiles
            if (tileType.name.Equals("tower_0")) { tower = i; }
            if (tileType.name.Equals("tower_top_0")) { tower_top = i; }
            if (tileType.name.Equals("tower_bot_0")) { tower_bottom = i; }
            if (tileType.name.Equals("tower_window_0")) { tower_window = i; }

            tileTypes[i] = tile;

            i++;
        }

        // Create dictionary to access chunks
        chunks = new Dictionary<Vector2Int, ChunkWFC>();

        // Generate the different floor heights
        floorHeights = new int[numberOfFloors];
        GenerateFloorHeights();

        // Get Pooler
        pooler = GetComponent<TilePooler>();
        pooler.CreateTilePools(imported_tiles);


        Debug.Log(tileSize);
        Debug.Log(TilesPerChunk);
        Debug.Log(new Vector3(tileSize * TilesPerChunk / 2 - tileSize / 2, 200, tileSize * TilesPerChunk / 2 - tileSize / 2));
    }

    public void LateUpdate()
    {
        for (int i = pendingChunks.Count - 1; i >= 0; i--)
        {
            Vector2Int pos = pendingChunks[i];
            if (chunks.ContainsKey(pos) && chunks[pos].jobHandle.IsCompleted)
            {

                chunks[pos].jobHandle.Complete();

                foreach (TowerJob t in scheduledTowerJobs)
                {
                    if (t.chunkPos == pos)
                    {
                        t.towerJob.towers = chunks[pos].towers;
                        break;
                    }
                }

                InstantiateTiles(pos);
                chunks[pos].jobWFC.OnDestroy();
                chunks[pos].isInstantiated = true;

                CreateTowerGrowthJobs(chunks[pos].towers, pos);

                pendingChunks.RemoveAt(i);

            }
        }

        for (int i = scheduledTowerJobs.Count - 1; i >= 0; i--)
        {
            TowerJob job = scheduledTowerJobs[i];
            if (job != null && job.job.IsCompleted)
            {
                job.job.Complete();
                //job.towerJob.heights.Dispose();
                InstantiateTowerTiles(job);

                scheduledTowerJobs.RemoveAt(i);
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
            //c.jobWFC.OnDestroy();
            c.tileMap.Dispose();
            c.outNorth.Dispose();
            c.outSouth.Dispose();
            c.outEast.Dispose();
            c.outWest.Dispose();
            c.towers.Dispose();
            //Debug.Log("Disposed of chunk @ " + c.position);
        }

        for (int i = scheduledTowerJobs.Count - 1; i >= 0; i--)
        {
            TowerJob job = scheduledTowerJobs[i];

            job.towerJob.heights.Dispose();
        }
    }

    public override void DestroyChunk(Vector2Int chunkPos)
    {
        Debug.Log("Destroy chunk:" + chunkPos.ToString());
        int i = 0;
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int convertedTileIndex = ConvertTo1D(x, y, z);
                    int index = chunks[chunkPos].tileMap[convertedTileIndex];
                    if (index >= 0 && imported_tiles[index].name != "-1" && !tileTypes[index].isTowerTile)
                    {
                        pooler.DespawnTile(imported_tiles[index].id, chunks[chunkPos].tiles[i]);
                        i++;
                    }
                }
            }
        }

        for (int j = 0; j < chunks[chunkPos].towerTiles.Count; j++)
        {
            TowerTile t = chunks[chunkPos].towers[j];
            pooler.DespawnTile(imported_tiles[t.tileId].id, chunks[chunkPos].towerTiles[j]);
        }

        chunks[chunkPos].jobHandle.Complete();
        chunks[chunkPos].tileMap.Dispose();
        chunks[chunkPos].outNorth.Dispose();
        chunks[chunkPos].outSouth.Dispose();
        chunks[chunkPos].outEast.Dispose();
        chunks[chunkPos].outWest.Dispose();
        chunks[chunkPos].towers.Dispose();
        chunks[chunkPos].jobWFC.OnDestroy();

        chunks.Remove(chunkPos);
    }

    public override void CreateChunk(Vector2Int chunkPos)
    {
        // Create chunk
        ChunkWFC chunk = new ChunkWFC();
        chunk.position = chunkPos;

        // Allocate memory for the chunk/job
        NativeArray<int> tileMap = new NativeArray<int>(dimensions.x * dimensions.y * dimensions.z, Allocator.Persistent);
        NativeList<TowerTile> towers = new NativeList<TowerTile>(0, Allocator.Persistent);
        chunk.outNorth = new NativeArray<int>(dimensions.y * dimensions.x, Allocator.Persistent);
        chunk.outSouth = new NativeArray<int>(dimensions.y * dimensions.x, Allocator.Persistent);
        chunk.outEast = new NativeArray<int>(dimensions.y * dimensions.z, Allocator.Persistent);
        chunk.outWest = new NativeArray<int>(dimensions.y * dimensions.z, Allocator.Persistent);

        // Create job
        JobWFC job = new JobWFC(dimensions, tileTypes.AsReadOnly(), tileCount, tileMap, neighborData.AsReadOnly(), hasConnectionData.AsReadOnly(), towers,
            chunk.outNorth, chunk.outSouth, chunk.outEast, chunk.outWest, seed++);

        // Fill chunk with the data we want to access later
        chunk.tileMap = tileMap;
        chunk.towers = towers;
        chunk.jobWFC = job;

        // Store chunk
        chunks.Add(chunkPos, chunk);

        pendingChunks.Add(chunkPos);
        JobHandle handle = chunks[chunkPos].jobWFC.Schedule();
        chunks[chunkPos].jobHandle = handle;
    }

    private void CreateTowerGrowthJobs(NativeList<TowerTile> towers, Vector2Int chunkPos)
    {
        TowerJob job = new TowerJob();
        job.chunkPos = chunkPos;
        NativeArray<int> heights = new NativeArray<int>(dimensions.y, Allocator.Persistent);
        for (int i = 0; i < floorHeights.Length; i++)
        {
            heights[i] = floorHeights[i];
        }
        job.towerJob = new TowerGrowthJob(towers, heights, dimensions, tower, tower_bottom, tower_top, tower_window, tilesBelowBottomFloor, seed++);
        job.job = job.towerJob.Schedule();
        scheduledTowerJobs.Add(job);
    }

    private int ConvertTo1D(int x, int y, int z)
    {
        return x + dimensions.x * (y + dimensions.y * z);
    }

    private void InstantiateTiles(Vector2Int position)
    {
        NativeArray<int> tileMap = chunks[position].tileMap;
        int xOffset = position.x * TilesPerChunk * tileSize;
        int zOffset = position.y * TilesPerChunk * tileSize;
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int convertedTileIndex = ConvertTo1D(x, y, z);
                    int index = tileMap[convertedTileIndex];
                    if (index >= 0 && imported_tiles[index].name != "-1" && !tileTypes[index].isTowerTile)
                    {
                        int height = floorHeights[y];
                        int a = x * tileSize + xOffset;
                        int b = z * tileSize + zOffset;

                        //chunks[position].tiles.Add(InstantiateTile(index, a, height, b));
                        chunks[position].tiles.Add(pooler.SpawnTile(imported_tiles[index].id, new Vector3Int(a, height * tileSize, b), imported_tiles[index].rotation, tileScale));
                    }
                }
            }
        }
    }

    private void InstantiateTowerTiles(TowerJob towerJob)
    {
        Vector2Int chunkPos = towerJob.chunkPos;
        int xOffset = chunkPos.x * TilesPerChunk * tileSize + tileSize * chunkPos.x;
        int zOffset = chunkPos.y * TilesPerChunk * tileSize + tileSize * chunkPos.y;
        foreach (TowerTile t in chunks[chunkPos].towers)
        {
            chunks[chunkPos].towerTiles.Add(pooler.SpawnTile(imported_tiles[t.tileId].id, new Vector3Int(xOffset + t.position.x * tileSize, t.position.y * tileSize, zOffset + t.position.z * tileSize), imported_tiles[t.tileId].rotation, tileScale));
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
        for (int i = 0; i < 6; i++)
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

    private void OnDrawGizmos()
    {
        if (chunks == null)
        {
            return;
        }


        Gizmos.color = Color.green;
        Vector3 sizeA = new Vector3(tileSize, 1f, 1f);
        Vector3 sizeB = new Vector3(1f, 1f, tileSize);

        Gizmos.DrawCube(new Vector3(tileSize / 2, 200, tileSize / 2), new Vector3(2f, 2f, 2f));
        Gizmos.color = Color.white;
        foreach (ChunkWFC chunk in chunks.Values)
        {
            if (!chunk.jobHandle.IsCompleted || !chunk.isInstantiated)
            {
                continue;
            }
            if (!selectedTiles.Contains(chunk.position))
            {
                continue;
            }
            int xOffset = chunk.position.x * TilesPerChunk * tileSize;
            int zOffset = chunk.position.y * TilesPerChunk * tileSize;
            for (int y = 0; y < dimensions.y; y++)
            {
                int height = floorHeights[y];
                for (int i = 0; i < dimensions.z; i++)
                {
                    if (HasConnection(chunk.outWest[i + y * dimensions.z], Direction.West))
                    {
                        Gizmos.color = Color.green;
                    }
                    else
                    {
                        Gizmos.color = Color.red;
                    }
                    Vector3 p = new Vector3(xOffset - tileSize / 2, height * tileSize, zOffset + i * tileSize);
                    Gizmos.DrawCube(p, sizeB);

                    if (HasConnection(chunk.outEast[i], Direction.East))
                    {
                        Gizmos.color = Color.green;
                    }
                    else
                    {
                        Gizmos.color = Color.red;
                    }
                    p = new Vector3(xOffset + tileSize * TilesPerChunk - tileSize / 2, height * tileSize, zOffset + i * tileSize);
                    Gizmos.DrawCube(p, sizeB);
                }
                for (int i = 0; i < dimensions.x; i++)
                {
                    if (HasConnection(chunk.outNorth[i + y * dimensions.x], Direction.North))
                    {
                        Gizmos.color = Color.green;
                    }
                    else
                    {
                        Gizmos.color = Color.red;
                    }
                    Vector3 p = new Vector3(xOffset + i * tileSize, height * tileSize, zOffset + tileSize * TilesPerChunk - tileSize / 2);
                    Gizmos.DrawCube(p, sizeA);

                    if (HasConnection(chunk.outSouth[i + y * dimensions.x], Direction.South))
                    {
                        Gizmos.color = Color.green;
                    }
                    else
                    {
                        Gizmos.color = Color.red;
                    }
                    p = new Vector3(xOffset + i * tileSize, height * tileSize, zOffset - tileSize / 2);
                    Gizmos.DrawCube(p, sizeA);
                }
            }
        }
    }

    // Check if the given tile has a connection to some direction
    private bool HasConnection(int index, Direction direction)
    {
        return hasConnectionData[index + ((int)direction * tileCount)];
    }


    // Generate a random gap between floors
    private void GenerateFloorHeights()
    {
        floorHeights = new int[dimensions.y];
        int height = 0;
        for (int i = 0; i < dimensions.y; i++)
        {
            floorHeights[i] = height;
            height = Random.Range(height + floorGapMin, height + floorGapMax);
        }
    }
}