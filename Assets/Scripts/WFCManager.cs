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
    private Dictionary<string, BufferZone> bufferZones;
    List<Vector2Int> pendingChunks = new List<Vector2Int>();
    List<BufferZone> scheduledBufferZones = new List<BufferZone>();
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

    public int TilesPerChunk { get => tilesPerChunk; set =>  tilesPerChunk = value / tileSize - 1; }

    public void Setup()
    {
        seed = (uint)Random.Range(1, Int32.MaxValue);
        
        dimensions = new Vector3Int(TilesPerChunk, numberOfFloors, TilesPerChunk);
        bufferZones = new Dictionary<string, BufferZone>();

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

        for (int i = scheduledBufferZones.Count - 1; i >= 0; i--)
        {
            BufferZone bufferZone = scheduledBufferZones[i];
            if (bufferZone != null && bufferZone.jobHandle.IsCompleted)
            {
                bufferZone.jobHandle.Complete();
                InstantiateBuffer(bufferZone);
                //Debug.Log("Bufferzone done between " + bufferZone.positionA + " & " + bufferZone.positionB);
                scheduledBufferZones.RemoveAt(i);
            }
        }

        for (int i = scheduledTowerJobs.Count - 1; i >= 0;i--)
        {
            TowerJob job = scheduledTowerJobs[i];
            if (job != null && job.job.IsCompleted)
            {
                job.job.Complete();
                job.towerJob.heights.Dispose();
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

        foreach (BufferZone bufferZone in bufferZones.Values)
        {
            bufferZone.tileMap.Dispose();
        }

        for (int i = scheduledTowerJobs.Count - 1; i >= 0; i--)
        {
            TowerJob job = scheduledTowerJobs[i];

            job.towerJob.heights.Dispose();
        }
    }

    public override void DestroyChunk(Vector2Int chunkPos)
    {
        RemoveLeftOverBufferZones(chunkPos);
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

    private void RemoveLeftOverBufferZones(Vector2Int chunkPosition)
    {
        for (int i = -1; i < 2; i+=2)
        {
            Vector2Int v = new Vector2Int(chunkPosition.x, chunkPosition.y + i);
            Vector2Int h = new Vector2Int(chunkPosition.x + i, chunkPosition.y);

            // Remove bufferzone to the north or south
            string key = chunkPosition.ToString() + ":" + v.ToString();
            string key2 = v.ToString() + ":" + chunkPosition.ToString();
            if (bufferZones.ContainsKey(key))
            {
                DeleteBufferZone(key);
            } else if (bufferZones.ContainsKey(key2))
            {
                DeleteBufferZone(key2);
            }
            // Remove bufferzone to the west or east
            key = chunkPosition.ToString() + ":" + h.ToString();
            key2 = h.ToString() + ":" + chunkPosition.ToString();
            if (bufferZones.ContainsKey(key))
            {
                DeleteBufferZone(key);
            }
            else if (bufferZones.ContainsKey(key2))
            {
                DeleteBufferZone(key2);
            }
        }

    }

    private void DeleteBufferZone(string key)
    {
        BufferZone bufferZone = bufferZones[key];

        int i = 0;
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                int index = bufferZone.tileMap[x + y * dimensions.x];
                if (index < 0 || index == tower_bottom || imported_tiles[index].id < 0) continue;
                pooler.DespawnTile(imported_tiles[index].id, bufferZone.tiles[i]);
                i++;
            }
        }
        bufferZone.tileMap.Dispose();
        bufferZones.Remove(key);
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

        CreateBufferZones(chunkPos, handle);
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

    private void CreateBufferZones(Vector2Int chunkPos, JobHandle jobHandle)
    {
        foreach (Vector2Int chunk in chunks.Keys)
        {
            // North
            if (chunk.Equals(new Vector2Int(chunkPos.x, chunkPos.y + 1)))
            {
                string key = chunk + ":" + chunkPos;

                if (!bufferZones.ContainsKey(key))
                {
                    BufferZone b = new BufferZone();
                    b.positionB = chunkPos; b.positionA = chunk;
                    b.IsHorizontal = true;

                    b.jobHandle = JobHandle.CombineDependencies(chunks[chunk].jobHandle, jobHandle);

                    b.tileMap = new NativeArray<int>(dimensions.x * dimensions.y, Allocator.Persistent);

                    // Create buffer job
                    b.bufferJob = new BufferJob(b.tileMap, tileTypes.AsReadOnly(),
                        neighborData.AsReadOnly(), hasConnectionData.AsReadOnly(),
                        new Vector3Int(dimensions.x, dimensions.y, 1), tileCount,
                        chunks[chunk].outSouth.AsReadOnly(),
                        chunks[chunkPos].outNorth.AsReadOnly());

                    b.jobHandle = b.bufferJob.Schedule(b.jobHandle);
                    scheduledBufferZones.Add(b);
                    bufferZones.Add(key, b);
                }

            }
            // South
            if (chunk.Equals(new Vector2Int(chunkPos.x, chunkPos.y - 1)))
            {
                string key = chunkPos + ":" + chunk;
                if (!bufferZones.ContainsKey(key))
                {
                    BufferZone b = new BufferZone();
                    b.positionB = chunk; b.positionA = chunkPos;
                    b.IsHorizontal = true;

                    b.jobHandle = JobHandle.CombineDependencies(jobHandle, chunks[chunk].jobHandle);

                    b.tileMap = new NativeArray<int>(dimensions.x * dimensions.y, Allocator.Persistent);

                    bufferZones.Add(chunkPos + ":" + chunk, b);

                    // Create buffer job
                    b.bufferJob = new BufferJob(b.tileMap, tileTypes.AsReadOnly(),
                        neighborData.AsReadOnly(), hasConnectionData.AsReadOnly(),
                        new Vector3Int(dimensions.x, dimensions.y, 1), tileCount,
                        chunks[chunkPos].outSouth.AsReadOnly(),
                        chunks[chunk].outNorth.AsReadOnly());

                    b.jobHandle = b.bufferJob.Schedule(b.jobHandle);
                    scheduledBufferZones.Add(b);
                }
            }

            // East
            if (chunk.Equals(new Vector2Int(chunkPos.x + 1, chunkPos.y)))
            {
                string key = chunkPos + ":" + chunk;
                if (!bufferZones.ContainsKey(key))
                {
                    BufferZone b = new BufferZone();
                    b.positionA = chunk; b.positionB = chunkPos;
                    b.IsHorizontal = false;

                    b.jobHandle = JobHandle.CombineDependencies(chunks[chunk].jobHandle, chunks[chunkPos].jobHandle);

                    b.tileMap = new NativeArray<int>(dimensions.z * dimensions.y, Allocator.Persistent);

                    bufferZones.Add(chunkPos + ":" + chunk, b);

                    // Create buffer job
                    b.bufferJob = new BufferJob(b.tileMap, tileTypes.AsReadOnly(),
                        neighborData.AsReadOnly(), hasConnectionData.AsReadOnly(),
                        new Vector3Int(1, dimensions.y, dimensions.z), tileCount,
                        chunks[chunk].outWest.AsReadOnly(),
                        chunks[chunkPos].outEast.AsReadOnly());

                    b.jobHandle = b.bufferJob.Schedule(b.jobHandle);
                    scheduledBufferZones.Add(b);
                }
            }

            // West
            if (chunk.Equals(new Vector2Int(chunkPos.x - 1, chunkPos.y)))
            {
                string key = chunk + ":" + chunkPos;
                if (!bufferZones.ContainsKey(key))
                {
                    BufferZone b = new BufferZone();
                    b.positionA = chunkPos; b.positionB = chunk;
                    b.IsHorizontal = false;

                    b.jobHandle = JobHandle.CombineDependencies(chunks[chunk].jobHandle, chunks[chunkPos].jobHandle);

                    b.tileMap = new NativeArray<int>(dimensions.z * dimensions.y, Allocator.Persistent);

                    bufferZones.Add(chunk + ":" + chunkPos, b);

                    // Create buffer job
                    b.bufferJob = new BufferJob(b.tileMap, tileTypes.AsReadOnly(),
                        neighborData.AsReadOnly(), hasConnectionData.AsReadOnly(),
                        new Vector3Int(1, dimensions.y, dimensions.z), tileCount,
                        chunks[chunkPos].outWest.AsReadOnly(),
                        chunks[chunk].outEast.AsReadOnly());

                    b.jobHandle = b.bufferJob.Schedule(b.jobHandle);
                    scheduledBufferZones.Add(b);
                }
            }
        }

    }

    private int ConvertTo1D(int x, int y, int z)
    {
        return x + dimensions.x * (y + dimensions.y * z);
    }

    private void InstantiateTiles(Vector2Int position)
    {
        NativeArray<int> tileMap = chunks[position].tileMap;
        int xOffset = position.x * TilesPerChunk * tileSize + tileSize * position.x;
        int zOffset = position.y * TilesPerChunk * tileSize + tileSize * position.y;
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

    private void InstantiateBuffer(BufferZone bufferZone)
    {
        
        int xOffset = bufferZone.positionA.x;
        xOffset = xOffset * TilesPerChunk * tileSize + xOffset * tileSize;

        int zOffset = bufferZone.positionA.y;
        zOffset = zOffset * TilesPerChunk * tileSize + zOffset * tileSize;

        bufferZone.IsInstantiated = true;

        if (bufferZone.IsHorizontal)
        {
            zOffset -= tileSize;
            for (int x = 0; x < dimensions.x; x++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int height = floorHeights[y] * tileSize;
                    //Debug.Log("Yo " + bufferZone.tileMap[x + y * dimensions.x]);
                    int index = bufferZone.tileMap[x + y * dimensions.x];
                    if (index < 0 || index == tower_bottom || imported_tiles[index].id < 0) continue;
                    //bufferZone.tiles.Add(InstantiateTile(index, xOffset + x * tileSize, height, zOffset));
                    bufferZone.tiles.Add(pooler.SpawnTile(imported_tiles[index].id, new Vector3Int(xOffset + x * tileSize, height, zOffset), imported_tiles[index].rotation, tileScale));
                }
            }
        } else
        {
            xOffset -= tileSize;
            //Debug.Log("Yo");
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int height = floorHeights[y] * tileSize;
                    //Debug.Log("Yoy " + bufferZone.tileMap[z + y * dimensions.z]);
                    int index = bufferZone.tileMap[z + y * dimensions.z];
                    if (index < 0 || index == tower_bottom || imported_tiles[index].id < 0) continue;
                    //bufferZone.tiles.Add(InstantiateTile(index, xOffset, height, zOffset + z * tileSize));
                    bufferZone.tiles.Add(pooler.SpawnTile(imported_tiles[index].id, new Vector3Int(xOffset, height, zOffset + z * tileSize), imported_tiles[index].rotation, tileScale));
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

    private void OnDrawGizmos()
    {
        if (chunks == null)
        {
            return;
        }


        Gizmos.color = Color.green;
        Vector3 sizeA = new Vector3(tileSize, 1f, 1f);
        Vector3 sizeB = new Vector3(1f, 1f, tileSize);

        Gizmos.DrawCube(new Vector3(tileSize * TilesPerChunk / 2 - tileSize / 2, 0, tileSize * TilesPerChunk / 2 - tileSize / 2), new Vector3(2f ,2f, 2f));
        Gizmos.color = Color.white;
        foreach (ChunkWFC chunk in chunks.Values)
        {
            if(!chunk.jobHandle.IsCompleted || !chunk.isInstantiated)
            {
                continue;
            }
            int xOffset = chunk.position.x * TilesPerChunk * tileSize + tileSize * chunk.position.x;
            int zOffset = chunk.position.y * TilesPerChunk * tileSize + tileSize * chunk.position.y;
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
            height = UnityEngine.Random.Range(height + floorGapMin, height + floorGapMax);
        }
    }
}