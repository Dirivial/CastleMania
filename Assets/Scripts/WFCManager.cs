

using JetBrains.Annotations;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public struct Interval
{
    public int start;
    public int end;
}

public class WFCManager : MonoBehaviour
{
    public Vector3Int tileScale = new Vector3Int(400, 400, 400);
    public int chunkSize = 15;
    public int numberOfFloors = 3;
    public int floorHeight = 1;
    public XML_IO XML_IO;
    public int tileSize = 8;
    public AnimationCurve towerGrowth;

    private int allocations = 0;

    // Keep track of these to make instantiating possible - and to create NativeTileTypes
    List<TileType> imported_tiles;

    // These should remain the same for all chunks
    private NativeArray<NativeTileType> tileTypes;
    private NativeArray<bool> neighborData;
    private NativeArray<bool> hasConnectionData;
    
    // Gaming
    private int tileCount;
    private Dictionary<Vector2Int, ChunkWFC> chunks;
    private Dictionary<string, BufferZone> bufferZones;
    List<Vector2Int> pendingChunks = new List<Vector2Int>();
    List<BufferZone> scheduledBufferZones = new List<BufferZone>();
    private Vector3Int dimensions = new Vector3Int(5, 5, 5);
    private static int EMPTY_TILE = -2;
    //private static int UNDECIDED = -1;

    // Implementation specific
    private int tower_bottom = -2;
    private int tower_top = -2;
    private int tower = -2;

    private uint seed = 1;


    private void Awake()
    {
        dimensions = new Vector3Int(chunkSize, numberOfFloors, chunkSize);
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

            ComputeNeighborData(tileType, i);
            ComputeHasConnection(tileType, i);

            tileTypes[i] = tile;

            // Set indices of the tower tiles
            if (tileType.name.Equals("tower_0")) tower = i;
            if (tileType.name.Equals("tower_top_0")) tower_top = i;
            if (tileType.name.Equals("tower_bot_0")) tower_bottom = i;

            i++;
        }

        // Create dictionary to access chunks
        chunks = new Dictionary<Vector2Int, ChunkWFC> ();
    }

    public void LateUpdate()
    {
        for (int i = pendingChunks.Count - 1; i >= 0; i--)
        {
            Vector2Int pos = pendingChunks[i];
            if (chunks.ContainsKey(pos) && chunks[pos].jobHandle.IsCompleted)
            {
                
                chunks[pos].jobHandle.Complete();
                InstantiateTiles(pos);
                chunks[pos].jobWFC.OnDestroy();
                chunks[pos].isInstantiated = true;
                //Debug.Log("Job completed @ " + pendingChunks[i]);
                //FillFilters(pos);

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
            c.jobWFC.OnDestroy();
            c.outNorth.Dispose();
            c.outSouth.Dispose();
            c.outEast.Dispose();
            c.outWest.Dispose();

            allocations -= 5;

            //Debug.Log("Disposed of chunk @ " + c.position);
        }

        foreach (BufferZone bufferZone in bufferZones.Values)
        {
            bufferZone.tileMap.Dispose();
            allocations--;
        }

        Debug.Log("Number of allocations left without disposing: " + allocations);
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
            if (Mathf.Abs(chunk.x - playerPosition.x) > chunkCount + 2 || Mathf.Abs(chunk.y - playerPosition.y) > chunkCount + 2)
            {
                chunksToRemove.Add(chunk);
            }
        }

        foreach (Vector2Int chunkPos in chunksToRemove)
        {
            if (!chunks.ContainsKey(chunkPos)) { continue; }
            RemoveLeftOverBufferZones(chunkPos);
            foreach (GameObject tile in chunks[chunkPos].tiles)
            {
                Destroy(tile);
            }
            chunks[chunkPos].jobHandle.Complete();
            chunks[chunkPos].tileMap.Dispose();
            chunks[chunkPos].outNorth.Dispose();
            chunks[chunkPos].outSouth.Dispose();
            chunks[chunkPos].outEast.Dispose();
            chunks[chunkPos].outWest.Dispose();
            chunks[chunkPos].jobWFC.OnDestroy();

            allocations -= 5;

            //Debug.Log("Disposed of chunk @ " + chunkPos);
            chunks.Remove(chunkPos);
        }
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

        for (int i = bufferZone.tiles.Count-1; i >= 0; i--)
        {
            Destroy(bufferZone.tiles[i]);
        }
        bufferZone.tileMap.Dispose();
        allocations--;
        bufferZones.Remove(key);
    }

    private void CreateChunk(Vector2Int chunkPos)
    {
        // Create chunk
        ChunkWFC chunk = new ChunkWFC();
        chunk.position = chunkPos;

        // Allocate memory for the chunk/job
        NativeArray<int> tileMap = new NativeArray<int>(dimensions.x * dimensions.y * dimensions.z, Allocator.Persistent);
        chunk.outNorth = new NativeArray<int>(dimensions.y * dimensions.x, Allocator.Persistent);
        chunk.outSouth = new NativeArray<int>(dimensions.y * dimensions.x, Allocator.Persistent);
        chunk.outEast = new NativeArray<int>(dimensions.y * dimensions.z, Allocator.Persistent);
        chunk.outWest = new NativeArray<int>(dimensions.y * dimensions.z, Allocator.Persistent);
        allocations += 5;

        // Create job
        JobWFC job = new JobWFC(dimensions, tileTypes.AsReadOnly(), tileCount, tileMap, neighborData.AsReadOnly(), hasConnectionData.AsReadOnly(),
            chunk.outNorth, chunk.outSouth, chunk.outEast, chunk.outWest, seed++);

        // Fill chunk with the data we want to access later
        chunk.tileMap = tileMap;
        chunk.jobWFC = job;

        // Store chunk
        chunks.Add(chunkPos, chunk);

        pendingChunks.Add(chunkPos);
        JobHandle handle = chunks[chunkPos].jobWFC.Schedule();
        chunks[chunkPos].jobHandle = handle;

        CreateBufferZones(chunkPos, handle);
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
                    allocations++;

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
                    allocations++;

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
                    allocations++;

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
                    allocations++;

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
        int xOffset = position.x * chunkSize * tileSize + tileSize * position.x;
        int zOffset = position.y * chunkSize * tileSize + tileSize * position.y;
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int convertedTileIndex = ConvertTo1D(x, y, z);
                    int index = tileMap[convertedTileIndex];
                    if (index >= 0 && imported_tiles[index].name != "-1")
                    {
                        int height = y * floorHeight;//floorHeights[y];
                        int a = x * tileSize + xOffset;
                        int b = z * tileSize + zOffset;

                        if (index == tower_bottom)
                        {
                            // The selected tower is a bottom piece, remove it if there is no connection upwards
                            if (dimensions.y > 1 && tileMap[ConvertTo1D(x, y + 1, z)] == EMPTY_TILE)
                            {
                                tileMap[convertedTileIndex] = EMPTY_TILE;
                                continue;
                            }
                            else
                            {
                                // Put a couple of extra tower pieces in there to avoid starting at the same floor as all of the other tiles
                                for (int i = 1; i < floorHeight; i++)
                                {
                                    chunks[position].tiles.Add(InstantiateTile(tower, a, i, b)); // Fill upwards
                                    chunks[position].tiles.Add(InstantiateTile(index, a, -1 * i, b)); // Fill downwards (to make tower look like they are long)
                                }
                                chunks[position].tiles.Add(InstantiateTile(index, a, -floorHeight, b));

                                // Fill void from this tile up to the next floor
                            }
                        }
                        else if (index == tower)
                        {
                            // Grow tower to fill gaps between floors
                            if (y == dimensions.y - 1 || tileMap[ConvertTo1D(x, y + 1, z)] == EMPTY_TILE)
                            {
                                Interval interval = ComputeTowerGrowth(y);

                                chunks[position].tiles.Add(InstantiateTile(tower_top, a, interval.end, b));

                                for (int i = interval.start; i < interval.end; i++)
                                {
                                    chunks[position].tiles.Add(InstantiateTile(tower, a, i, b));
                                }
                            } else
                            {
                                for (int i = 1; i < floorHeight; i++)
                                {
                                    chunks[position].tiles.Add(InstantiateTile(index, a, i + height, b));
                                }
                            }
                        }
                        else if (index == tower_top)
                        {
                            // Set current tile to be a normal tower tile
                            tileMap[convertedTileIndex] = tower;

                            Interval interval = ComputeTowerGrowth(y);

                            chunks[position].tiles.Add(InstantiateTile(tower_top, a, interval.end, b));

                            for (int i = interval.start; i < interval.end; i++)
                            {
                                chunks[position].tiles.Add(InstantiateTile(tower, a, i, b));
                            }
                        }

                        chunks[position].tiles.Add(InstantiateTile(index, a, height, b));
                    }
                }
            }
        }
    }

    private void InstantiateBuffer(BufferZone bufferZone)
    {
        
        float xOffset = bufferZone.positionA.x;
        xOffset = xOffset * chunkSize * tileSize + xOffset * tileSize;

        float zOffset = bufferZone.positionA.y;
        zOffset = zOffset * chunkSize * tileSize + zOffset * tileSize;

        bufferZone.IsInstantiated = true;

        if (bufferZone.IsHorizontal)
        {
            zOffset -= tileSize;
            for (int x = 0; x < dimensions.x; x++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int height = y * floorHeight;
                    //Debug.Log("Yo " + bufferZone.tileMap[x + y * dimensions.x]);
                    int index = bufferZone.tileMap[x + y * dimensions.x];
                    if (index < 0 || index == tower_bottom) continue;
                    bufferZone.tiles.Add(InstantiateTile(index, xOffset + x * tileSize, height, zOffset));
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
                    int height = y * floorHeight;
                    //Debug.Log("Yoy " + bufferZone.tileMap[z + y * dimensions.z]);
                    int index = bufferZone.tileMap[z + y * dimensions.z];
                    if (index < 0 || index == tower_bottom) continue;
                    bufferZone.tiles.Add(InstantiateTile(index, xOffset, height, zOffset + z * tileSize));
                }
            }
        }
    }

    private Interval ComputeTowerGrowth(int y)
    {
        Interval interval = new Interval();

        // Get a number to grow to
        float f = Random.Range(0.0f, 1.0f);
        float h = towerGrowth.Evaluate(f);
        int height_i = Mathf.RoundToInt(h) + 1;

        int start = y * floorHeight + 1; //floorHeights[y] + 1;
        int end = start + height_i; //floorHeights[y] + height_i;

        // Make sure that the growth does not lead to poking through floors
        // If we are at the top, there is no need to worry about breaking through floors
        //if (y != dimensions.y - 1 && floorHeights[y + 1] < end) end = floorHeights[y + 1] - 1;
        if (y != dimensions.y - 1 && (y + 1) * floorHeight < end) end = start;

        interval.start = start;
        interval.end = end;

        return interval;
    }

    private GameObject InstantiateTile(int index, float x, float y, float z)
    {
        if (imported_tiles[index].name.Equals("-1")) return null;
        GameObject obj = Instantiate(imported_tiles[index].tileObject, new Vector3(x, y * tileSize, z), imported_tiles[index].rotation);
        obj.transform.localScale = tileScale;
        return obj;
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

    /*
    private Filter[] CreateMissingFilters(float x, float z)
    {
        Filter[] result = new Filter[4];
        result[0].position = "" + x + ";" + (z + 0.5f);
        result[1].position = "" + x + ";" + (z - 0.5f);
        result[2].position = "" + (x + 0.5f) + ";" + z;
        result[3].position = "" + (x - 0.5f) + ";" + z;

        for (int i = 0; i < 4; i++)
        {
            if (!bufferZones.ContainsKey(result[i].position))
            {
                result[i].isNew = true;
                if (i < 2)
                {
                    bufferZones.Add(result[i].position, new NativeArray<bool>(dimensions.y * dimensions.x, Allocator.Persistent));
                } else
                {
                    bufferZones.Add(result[i].position, new NativeArray<bool>(dimensions.y * dimensions.z, Allocator.Persistent));
                }
                
            }
        }

        return result;
    } */

    private void OnDrawGizmos()
    {
        if (chunks == null)
        {
            return;
        }


        Gizmos.color = Color.green;
        Vector3 sizeA = new Vector3(tileSize, 1f, 1f);
        Vector3 sizeB = new Vector3(1f, 1f, tileSize);

        Gizmos.DrawCube(new Vector3(tileSize * chunkSize / 2 - tileSize / 2, 0, tileSize * chunkSize / 2 - tileSize / 2), new Vector3(2f ,2f, 2f));
        Gizmos.color = Color.white;
        foreach (ChunkWFC chunk in chunks.Values)
        {
            if(!chunk.jobHandle.IsCompleted || !chunk.isInstantiated)
            {
                continue;
            }
            int xOffset = chunk.position.x * chunkSize * tileSize + tileSize * chunk.position.x;
            int zOffset = chunk.position.y * chunkSize * tileSize + tileSize * chunk.position.y;
            for (int y = 0; y < dimensions.y; y++)
            {
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
                    Vector3 p = new Vector3(xOffset - tileSize / 2, y * floorHeight * tileSize, zOffset + i * tileSize);
                    Gizmos.DrawCube(p, sizeB);

                    if (HasConnection(chunk.outEast[i], Direction.East))
                    {
                        Gizmos.color = Color.green;
                    }
                    else
                    {
                        Gizmos.color = Color.red;
                    }
                    p = new Vector3(xOffset + tileSize * chunkSize - tileSize / 2, y * floorHeight * tileSize, zOffset + i * tileSize);
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
                    Vector3 p = new Vector3(xOffset + i * tileSize, y * floorHeight * tileSize, zOffset + tileSize * chunkSize - tileSize / 2);
                    Gizmos.DrawCube(p, sizeA);

                    if (HasConnection(chunk.outSouth[i + y * dimensions.x], Direction.South))
                    {
                        Gizmos.color = Color.green;
                    }
                    else
                    {
                        Gizmos.color = Color.red;
                    }
                    p = new Vector3(xOffset + i * tileSize, y * floorHeight * tileSize, zOffset - tileSize / 2);
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
}