

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;


/* 
 * This is not the most elegant solution I think, the goal is just to 
 * create something that allows continuity in the world generation
 */

[BurstCompile]
public struct BufferJob: IJob
{
    private NativeArray<int> tileMap;

    [DeallocateOnJobCompletion]
    private NativeArray<bool> tileMapArray;

    // Readonly
    private NativeArray<NativeTileType>.ReadOnly tileTypes;
    private NativeArray<bool>.ReadOnly neighborData;
    private NativeArray<bool>.ReadOnly hasConnectionData;

    private NativeArray<int>.ReadOnly connectionsA;
    private NativeArray<int>.ReadOnly connectionsB;

    // Whatever
    private Vector3Int dimensions;
    private int tileCount;
    private bool isHorizontal;

    private static readonly int UNDECIDED = -1;
    private static readonly int EMPTY_TILE = -2;

    private Unity.Mathematics.Random random;

    public BufferJob(NativeArray<int> tileMap, 
        NativeArray<NativeTileType>.ReadOnly tileTypes, 
        NativeArray<bool>.ReadOnly neighborData, 
        NativeArray<bool>.ReadOnly hasConnectionData, 
        Vector3Int dimensions, int tileCount,
        NativeArray<int>.ReadOnly connectionsA,
        NativeArray<int>.ReadOnly connectionsB)
    {
        this.tileMap = tileMap;
        this.tileTypes = tileTypes;
        this.neighborData = neighborData;
        this.hasConnectionData = hasConnectionData;
        this.dimensions = dimensions;
        this.tileCount = tileCount;

        this.connectionsA = connectionsA;
        this.connectionsB = connectionsB;
        
        isHorizontal = dimensions.z == 1;

        random = new Unity.Mathematics.Random(1);

        tileMapArray = new NativeArray<bool>(dimensions.x * dimensions.z * dimensions.y * tileCount, Allocator.Persistent);


        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    // Set each tile to be undecided
                    this.tileMap[ConvertTo1D(x, y, z)] = UNDECIDED;

                    // Set each boolean value to true to represent the positions ability to
                    // be the corresponding tile in the tileTypes list
                    for (int i = 0; i < tileCount; i++)
                    {
                        if (!tileTypes[i].isTowerTile) {
                            tileMapArray[TileMapArrayCoord(x, y, z, i)] = true;
                        } else
                        {
                            tileMapArray[TileMapArrayCoord(x, y, z, i)] = false;
                        }

                    }
                }
            }
        }
    }


    public void Execute()
    {

        for (int x = 0; x < dimensions.x;  x++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                for (int z  = 0; z < dimensions.z; z++)
                {
                    PickTileAt(new Vector3Int(x, y, z));
                }
            }
        }
    }

    // Pick a tile at the given tile position
    private void PickTileAt(Vector3Int pos)
    {
        int index = ChooseTileTypeAt(pos.x, pos.y, pos.z);
        tileMap[ConvertTo1D(pos.x, pos.y, pos.z)] = index;

        for (int i = 0; i < tileCount; i++)
        {
            tileMapArray[TileMapArrayCoord(pos.x, pos.y, pos.z, i)] = false;
        }
    }

    private int ChooseTileTypeAt(int x, int y, int z)
    {
        NativeList<int> choices = new NativeList<int>(0, Allocator.Temp); // All possible choices

        for (int i = 0; i < tileCount; i++)
        {
            //Debug.Log("Trying tile: " + tileTypes[i].name);
            if (HasPossibleConnection(x, y, z, i) && EnforceCustomConstraints(x, y, z, i))
            {
                choices.Add(i);
            }
        }

        // Choose a random tile type from the list of possible tile types
        if (choices.Length > 0)
        {
            return ChooseWithWeights(choices);
        }
        else
        {
            return EMPTY_TILE;
        }
    }

    // If you do not have any custom constraints, just return true here or remove the function
    private bool EnforceCustomConstraints(int x, int y, int z, int i)
    {
        if (tileTypes[i].grounded && y != 0)
        {
            return false;
        }

        if (tileTypes[i].mustConnect && !CanConnect(x, y, z, i))
        {
            return false;
        }

        if (tileTypes[i].noRepeatH)
        {
            if (x > 0 && tileMap[ConvertTo1D(x - 1, y, z)] == i) { return false; }
            if (x < dimensions.x - 1 && tileMap[ConvertTo1D(x + 1, y, z)] == i) { return false; }
            if (z > 0 && tileMap[ConvertTo1D(x, y, z - 1)] == i) { return false; }
            if (z < dimensions.z - 1 && tileMap[ConvertTo1D(x, y, z + 1)] == i) { return false; }
        }

        if (tileTypes[i].noRepeatV)
        {
            if (y > 0 && tileMap[ConvertTo1D(x, y - 1, z)] == i) { return false; }
            if (y < dimensions.y - 1 && tileMap[ConvertTo1D(x, y + 1, z)] == i) { return false; }
        }

        return true;
    }

    private int ChooseWithWeights(NativeList<int> indices)
    {
        float cumulativeSum = 0.0f;
        NativeArray<float> cumulativeWeights = new NativeArray<float>(indices.Length, Allocator.Temp);

        for (int i = 0; i < indices.Length; i++)
        {
            cumulativeSum += tileTypes[indices[i]].weight;
            cumulativeWeights[i] = cumulativeSum;
        }

        float r = random.NextFloat(0, cumulativeSum);

        int index = NativeSortExtension.BinarySearch(cumulativeWeights, r);
        if (index < 0) index = ~index;

        return indices[index];
    }

    // Convert coordinates to singular coordinate for tile map
    private int ConvertTo1D(int x, int y, int z)
    {
        return isHorizontal ? x + dimensions.x * (y + dimensions.y * z) : z + dimensions.z * (y + dimensions.y * x);
    }

    // Convert to coordinate to use for the tile map array
    private int TileMapArrayCoord(int x, int y, int z, int i)
    {
        return x + dimensions.x * (y + dimensions.y * (z + dimensions.z * i));
    }

    // Check if the given tile has a connection to some direction
    private bool HasConnection(int index, Direction direction)
    {
        return hasConnectionData[index + ((int)direction * tileCount)];
    }

    // Check two tiles are valid neighbors
    private bool IsNeighbor(int index, Direction direction, int otherIndex)
    {
        if (otherIndex < 0) return false;
        return neighborData[index + tileCount * ((int)direction + 6 * otherIndex)];
    }

    private bool HasPossibleConnection(int x, int y, int z, int i)
    {
        // If the current tile has a connection to an empty tile (as in there can be no tile there), return false
        if (isHorizontal)
        {
            if (HasConnection(i, Direction.West))
            {
                if (x == 0)
                {
                    return false;
                } else
                {
                    int index = tileMap[ConvertTo1D(x - 1, y, z)];
                    if (index == EMPTY_TILE || (index != UNDECIDED && !IsNeighbor(i, Direction.West, index))) {
                        return false;
                    }
                }
            } else if (x > 0 && HasConnection(tileMap[ConvertTo1D(x - 1, y, z)], Direction.East))
            {
                return false;
            }

            if (HasConnection(i, Direction.East))
            {
                
                if (x == dimensions.x - 1)
                {
                    //Debug.Log("Failed East.");
                    return false;
                } else
                {
                    int index = tileMap[ConvertTo1D(x + 1, y, z)];
                    if (index == EMPTY_TILE || (index != UNDECIDED && !IsNeighbor(i, Direction.West, index)))
                    {
                        return false;
                    }
                }
            }
            else if (x < dimensions.x - 1 && HasConnection(tileMap[ConvertTo1D(x + 1, y, z)], Direction.West))
            {
                return false;
            }

            // Any incoming vertical connections should be respected

            int south = connectionsB[x + y * dimensions.x];
            if (HasConnection(south, Direction.North))
            {
                if (!HasConnection(i, Direction.South)) { return false; }
            } else
            {
                if (HasConnection(i, Direction.South)) { return false; }
            }

            int north = connectionsA[x + y * dimensions.x];
            if (HasConnection(north, Direction.South))
            {
                if (!HasConnection(i, Direction.North)) { return false; }
            }
            else
            {
                if (HasConnection(i, Direction.North)) { return false; }
            }
        }
        else
        {
            if (HasConnection(i, Direction.South))
            {
                if (z == 0)
                {
                    return false;
                }
                else
                {
                    int index = tileMap[ConvertTo1D(x, y, z - 1)];
                    if (index == EMPTY_TILE || (index != UNDECIDED && !IsNeighbor(i, Direction.South, index)))
                    {
                        return false;
                    }
                }
            }
            else if (z > 0 && HasConnection(tileMap[ConvertTo1D(x, y, z - 1)], Direction.North))
            {
                return false;
            }

            if (HasConnection(i, Direction.North))
            {

                if (z == dimensions.z - 1)
                {
                    return false;
                }
                else
                {
                    int index = tileMap[ConvertTo1D(x, y, z + 1)];
                    if (index == EMPTY_TILE || (index != UNDECIDED && !IsNeighbor(i, Direction.North, index)))
                    {
                        return false;
                    }
                }
            }
            else if (x < dimensions.x - 1 && HasConnection(tileMap[ConvertTo1D(x, y, z + 1)], Direction.South))
            {
                return false;
            }

            // Any incoming horizontal connections should be respected
            int left = connectionsB[z + y * dimensions.z];
            int right = connectionsA[z + y * dimensions.z];

            if (HasConnection(left, Direction.East))
            {
                if (!HasConnection(i, Direction.West)) { return false; }
            }
            else
            {
                if (HasConnection(i, Direction.West)) { return false; }
            }


            if (HasConnection(right, Direction.West))
            {
                if (!HasConnection(i, Direction.East)) { return false; }
            }
            else
            {
                if (HasConnection(i, Direction.East)) { return false; }
            }
        }

        if (HasConnection(i, Direction.Down) && (y > 0 && tileMap[ConvertTo1D(x, y - 1, z)] == EMPTY_TILE))
        {
            //Debug.Log("Failed Down.");
            return false;
        }

        if (HasConnection(i, Direction.Up) && (y < dimensions.y - 1 && tileMap[ConvertTo1D(x, y + 1, z)] == EMPTY_TILE))
        {
            //Debug.Log("Failed Up.");
            return false;
        }

        return true;
    }

    private bool CanConnect(int x, int y, int z, int i)
    {
        /*
        if (HasConnection(i, Direction.West) && x > 0)
        {
            int neighbor = tileMap[ConvertTo1D(x - 1, y, z)];
            if (IsNeighbor(i, Direction.West, neighbor))
            {
                return true;
            }
        }

        if (HasConnection(i, Direction.East) && x < dimensions.x - 1)
        {
            int neighbor = tileMap[ConvertTo1D(x + 1, y, z)];
            if (IsNeighbor(i, Direction.East, neighbor))
            {
                return true;
            }
        }

        if (HasConnection(i, Direction.South) && z > 0)
        {
            int neighbor = tileMap[ConvertTo1D(x, y, z - 1)];
            if (IsNeighbor(i, Direction.South, neighbor))
            {
                return true;
            }
        }

        if (HasConnection(i, Direction.North) && z < dimensions.z - 1)
        {
            int neighbor = tileMap[ConvertTo1D(x, y, z + 1)];
            if (IsNeighbor(i, Direction.North, neighbor))
            {
                return true;
            }
        } */

        if (HasConnection(i, Direction.Down) && y > 0)
        {
            int neighbor = tileMap[ConvertTo1D(x, y - 1, z)];
            if (IsNeighbor(i, Direction.Down, neighbor))
            {
                return true;
            }
        }

        if (HasConnection(i, Direction.Up) && y < dimensions.y - 1)
        {
            int neighbor = tileMap[ConvertTo1D(x, y + 1, z)];
            if (IsNeighbor(i, Direction.Up, neighbor))
            {
                return true;
            }
        }
        return false;
    }

    private void CanConnectH()
    {

    }

    private void CanConnectV()
    {

    }
}