using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using Debug = UnityEngine.Debug;
using UnityEngine.Tilemaps;
using JetBrains.Annotations;
using static UnityEditor.PlayerSettings;

public enum Direction
{
    North,
    South,
    East,
    West,
    Up,
    Down,
}

public enum Symmetry
{
    X,
    L,
    I,
    T,
    D,
}

public class WFC : MonoBehaviour
{
    /*
    private List<TileType> tileTypes;
    [SerializeField] public Vector3Int dimensions = new Vector3Int(5, 5, 5);

    private int tileCount = 1;
    private bool setupComplete = false;
    private int[,,] tileMap;
    private bool[,,][] tileMapArray;
    private Stack<Vector3Int> tilesToProcess;
    //private Stack<SaveState> saveStates;

    private List<GameObject> boi;
    private TileGang tileGang;
    private int tileSize = 3;

    private void Awake()
    {
        tilesToProcess = new Stack<Vector3Int>();
        tileMap = new int[dimensions.x, dimensions.y, dimensions.z];

        // Store the initial entropy of each coordinate in the chunk map
        tileMapArray = new bool[dimensions.x, dimensions.y, dimensions.z][];
        boi = new List<GameObject>();
        tileGang = GetComponentInChildren<TileGang>();
    }

    void Start()
    {
        tileCount = tileGang.GetTileTypesCount();
        tileSize = tileGang.GetTileSize();
        tileTypes = tileGang.GetTileTypes();

        // For testing
        //InstantiateTileTypes();

        //PrintConnectionDirs();
    }

    private void PrintArrayCount()
    {
        string d;
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int y  = 0; y < dimensions.y; y++)
            {
                d = "";
                for (int z = 0; z < dimensions.z; z++)
                {
                    d += " " + tileMapArray[x, y, z].Count(c => c == true);
                }
                Debug.Log(d);
            }
        }
    }


    private void PrintConnectionDirs()
    {
        string debugString;
        for (int i = 0; i < tileCount; i++)
        {
            TileType v = tileTypes[i];
            debugString = v.name;
            for (int j = 0; j < 6; j++)
            {
                if (v.connections[j] > 0)
                { 
                    debugString += ", " + (Direction)j;
                }
            }
            Debug.Log(debugString);
        }
    }

    public void Clear()
    {
        setupComplete = false;
        tilesToProcess.Clear();
        tileMap = new int[dimensions.x, dimensions.y, dimensions.z];
        for (int i = boi.Count - 1; i >= 0; i--)
        {
            Destroy(boi[i]);
        }
    }

    public void Setup()
    {
        //Debug.Log("Generating a new model - Clearing " + boi.Count + " items");

        for (int i = boi.Count - 1; i >= 0; i--)
        {
            Destroy(boi[i]);
        }
        boi.Clear();
        tilesToProcess = new Stack<Vector3Int>();
        tileMap = new int[dimensions.x, dimensions.y, dimensions.z];

        // Store the initial entropy of each coordinate in the chunk map
        tileMapArray = new bool[dimensions.x, dimensions.y, dimensions.z][];
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    tileMapArray[x, y, z] = new bool[tileCount];

                    // Set floor to be ground tiles
/*                    if (y == 0)
                    {                        
                        //tileMap[x, y, z] = 1;
                        //tileMapArray[x, y, z][1] = true;
                        //tilesToProcess.Push(new Vector3Int(x, y, z));
                        tileMap[x, y, z] = -1;
                        for (int i = 1; i < tileTypes.Count; i++) // THIS MIGHT MAKE STUFF BREAK IN THE FUTURE
                        {
                            tileMapArray[x, y, z][i] = true;
                        }
                    }
                    else // The rest should be empty
                    {

                        tileMap[x, y, z] = -1;
                        for (int i = 0; i < tileTypes.Count; i++) // THIS MIGHT MAKE STUFF BREAK IN THE FUTURE
                        {
                            tileMapArray[x, y, z][i] = true;
                        }
/*                    }
                }
            }
        }
        Vector3Int v = new Vector3Int(Random.Range(0, dimensions.x), 0, Random.Range(0, dimensions.z));
        int c = 1;

        tileMap[v.x, v.y, v.z] = c;
        for (int i = 0; i < tileTypes.Count; i++) // THIS MIGHT MAKE STUFF BREAK IN THE FUTURE
        {
            tileMapArray[v.x, v.y, v.z][i] = false;
        }
        //Debug.Log("Initialized with tower @ " + v.ToString());
        //Debug.Log("Currently, we have " + tileTypes.Count + " many tile types");
        UpdateNeighbors(v);
        tilesToProcess.Push(v);

        ProcessTiles();

        InstantiateDeezNuts();
        setupComplete = true;
    }

    public void GenerateFull()
    {
        Setup();

        int maxIterations = dimensions.x * dimensions.y * dimensions.z;
        int iterations = 0;
        Vector3Int nextWave = FindLowestEntropy();

        while (nextWave.x != -1 && nextWave.y != -1 && iterations < maxIterations)
        {
            PickTileAt(nextWave);
            ProcessTiles();
            nextWave = FindLowestEntropy();
            iterations++;
        }
        InstantiateDeezNuts();
        //PrintArrayCount();
    }

    public void TakeStep()
    {
        if (!setupComplete)
        {
            Setup();
            setupComplete = true;
        }

        Vector3Int nextWave = FindLowestEntropy();
        List<Vector3Int> toInstantiate;

        if (nextWave.x != -1 && nextWave.y != -1 && nextWave.z != -1)
        {
            PickTileAt(nextWave);
            toInstantiate = ProcessTiles();
            InstantiateDeezNut(toInstantiate);
        }
        else
        {
            Debug.Log("Could not get any further");
        }
    }

    private void PrintStuff()
    {
        // Print out the tiles that have been fiddled on
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int index = tileMap[x, y, z];
                    if (index != -1)
                    {
                        Debug.Log("Decided: " + tileTypes[index] + " " + new Vector3Int(x, y, z));
                    }
                }
            }
        }
    }

    // Just to make sure that the rotations are correct - this should be kept for later when we add more types of tiles
    private void InstantiateTileTypes()
    {
        float i = 0.0f;
        foreach (TileType tile in tileTypes)
        {
            GameObject obj = Instantiate(tile.tileObject, new Vector3(i * tileSize, 0, 0), tile.rotation);
            obj.transform.parent = transform;
            i += 1.1f;
        }
    }

    private void InstantiateDeezNuts()
    {
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int index = tileMap[x, y, z];
                    if (index > 0)
                    {
                        GameObject obj = Instantiate(tileTypes[index].tileObject, new Vector3(x * tileSize, y * tileSize, z * tileSize), tileTypes[index].rotation);
                        obj.transform.parent = transform;
                        boi.Add(obj);
                    } else
                    {
                        GameObject obj = Instantiate(tileTypes[0].tileObject, new Vector3(x * tileSize, y * tileSize, z * tileSize), Quaternion.identity);
                        obj.transform.parent = transform;
                        boi.Add(obj);
                    }
                }
            }
        }
    }

    private void InstantiateDeezNut(List<Vector3Int> toInstantiate)
    {
        foreach (Vector3Int v in toInstantiate)
        {
            TileType tileType = tileTypes[tileMap[v.x, v.y, v.z]];
            GameObject obj = Instantiate(tileType.tileObject, new Vector3(v.x * tileSize, v.y * tileSize, v.z * tileSize), tileType.rotation);
            obj.transform.parent = transform;
            boi.Add(obj);
        }
    }

    // Pick a random tile at the given tile position, using the possible tiles at that position
    private void PickTileAt(Vector3Int pos)
    {
        int v = ChooseTileTypeAt(pos.x, pos.y, pos.z);
        tileMap[pos.x, pos.y, pos.z] = v;

        for (int i = 0; i < tileTypes.Count; i++)
        {
            tileMapArray[pos.x, pos.y, pos.z][i] = false;
        }

        //UpdateNeighbors(tilePosition); // I dunno walter, I guess we just update the neighbors ?? why would I do this?? 

        tilesToProcess.Push(pos);
    }

    // Find the first tile with the lowest entropy
    private Vector3Int FindLowestEntropy()
    {
        // Look for the lowest entropy in the tile map
        float lowestEntropy = tileCount + 1;
        Vector3Int lowestEntropyPosition = new Vector3Int(-1, -1, -1);
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    if (tileMap[x, y, z] != -1) continue;
                    float possibleTileCount = GetEntropy(new Vector3Int(x, y, z));

                    if (possibleTileCount < lowestEntropy && possibleTileCount > 0)
                    {
                        lowestEntropy = possibleTileCount;
                        lowestEntropyPosition = new Vector3Int(x, y, z);
                    }
                }
            }
        }
        return lowestEntropyPosition;
    }

    private int ChooseTileTypeAt(int x, int y, int z)
    {      
        if (y == 0)
        {
            List<int> groundTiles = new List<int>
            {
                0
            };  // Tiles that can touch ground
            for (int i = 1; i < tileCount; i++)
            {
                if (tileMapArray[x, y, z][i])
                {
                    if (tileTypes[i].CanTouchGround) groundTiles.Add(i);
                }
            }
            return ChooseWithWeights(groundTiles);
        
        } else
        {
            List<int> choices = new List<int>(); // All possible choices
            List<int> withinVolume = new List<int>(); // No connections to the outside

            bool isOnTopOf = tileMap[x, y - 1, z] > 0;

            for (int i = 1; i < tileCount; i++)
            {
                if (tileMapArray[x, y, z][i])
                {
                    // Check if tile has to stand on something
                    if (tileTypes[i].MustStandOn)
                    {
                        // Only add the option if it is standing on top of something
                        if (isOnTopOf)
                        {
                            choices.Add(i);
                            if (!ConnectsOutside(x, y, z, i)) withinVolume.Add(i);
                        }
                    } else
                    {
                        if (tileTypes[i].MustConnect)
                        {
                            if (HasConnection(x, y, z, i))
                            {
                                choices.Add(i);
                                if (!ConnectsOutside(x, y, z, i)) withinVolume.Add(i);
                            }
                        }
                        else
                        {
                            choices.Add(i);
                            if (!ConnectsOutside(x, y, z, i)) withinVolume.Add(i);
                        }
                    }
                }
            }

            // Try to take one from within the volume first
            if (withinVolume.Count > 0)
            {
                return ChooseWithWeights(withinVolume);
            }

            // Choose a random tile type from the list of possible tile types
            if (choices.Count > 0)
            {
                return ChooseWithWeights(choices);
            }
            else
            {
                return 0;
            }
        }
    }

    private int ChooseWithWeights(List<int> indices)
    {
        float cumulativeSum = 0.0f;
        float[] cumulativeWeights = new float[indices.Count];

        for (int i = 0; i < indices.Count; i++)
        {
            cumulativeSum += tileTypes[indices[i]].weight;
            cumulativeWeights[i] = cumulativeSum;
        }

        float r = Random.Range(0, cumulativeSum);

        int index = Array.BinarySearch(cumulativeWeights, r);
        if (index < 0) index = ~index;

        return indices[index];
    }

    private bool HasConnection(int x, int y, int z, int i)
    {
        if (x > 0 && tileMap[x - 1, y, z] != -1 && tileTypes[tileMap[x - 1, y, z]].connections[(int)Direction.East] == tileTypes[i].connections[(int)Direction.West]) return true;
        if (x < dimensions.x - 1 && tileMap[x + 1, y, z] != -1 && tileTypes[tileMap[x + 1, y, z]].connections[(int)Direction.West] == tileTypes[i].connections[(int)Direction.East]) return true;
        if (z > 0 && tileMap[x, y, z - 1] != -1 && tileTypes[tileMap[x, y, z - 1]].connections[(int)Direction.North] == tileTypes[i].connections[(int)Direction.South]) return true;
        if (z < dimensions.z - 1 && tileMap[x, y, z + 1] != -1 && tileTypes[tileMap[x, y, z + 1]].connections[(int)Direction.South] == tileTypes[i].connections[(int)Direction.North]) return true;

        return false;
    }


    // Used to see if a tiles connects to the outside
    private bool ConnectsOutside(int x, int y, int z, int i)
    {
        // Lower bounds
        if (x == 0 && tileTypes[i].connections[(int)Direction.West] > 0) { return true; }
        if (y == 0 && tileTypes[i].connections[(int)Direction.Down] > 0) { return true; }
        if (z == 0 && tileTypes[i].connections[(int)Direction.South] > 0) { return true; }
        
        // Upper bounds
        if (x == dimensions.x - 1 && tileTypes[i].connections[(int)Direction.East] > 0) { return true; }
        if (y == dimensions.y - 1 && tileTypes[i].connections[(int)Direction.Up] > 0) { return true; }
        if (z == dimensions.z - 1 && tileTypes[i].connections[(int)Direction.North] > 0) { return true; }

        // Could not find any connection to the outside
        return false;
    }

    // Process the tiles that were have been but in the tilesToProcess stack. Returns list of coordinates for tiles that have been set
    private List<Vector3Int> ProcessTiles()
    {
        int maxIterations = 1000;
        int i = 0;
        List<Vector3Int> setTiles = new List<Vector3Int>();
        while (tilesToProcess.Count > 0 && maxIterations > i)
        {
            Vector3Int tilePosition = tilesToProcess.Pop();

            if (tileMap[tilePosition.x, tilePosition.y, tilePosition.z] == -1)
            {
                if (tileMapArray[tilePosition.x, tilePosition.y, tilePosition.z].Count(c => c == true) == 1) {

                    int chosenTile = ChooseTileTypeAt(tilePosition.x, tilePosition.y, tilePosition.z);

                    if (chosenTile != -1)
                    {
                        tileMap[tilePosition.x, tilePosition.y, tilePosition.z] = chosenTile;
                        UpdateNeighbors(tilePosition);
                        setTiles.Add(tilePosition);
                    }

                    // We have a single chunk type, so we can set it
                    for (int j = tileCount - 1; j >= 0; j--)
                    {
                        tileMapArray[tilePosition.x, tilePosition.y, tilePosition.z][j] = false;
                    }
                }
            } else
            {
                if (tileMap[tilePosition.x, tilePosition.y, tilePosition.z] != 0)
                {
                    UpdateNeighbors(tilePosition);
                }
                
                setTiles.Add(tilePosition);
            }
            i++;
        }
        return setTiles;
    }

    // Update the neighbors of the given chunk position
    private void UpdateNeighbors(Vector3Int tilePosition)
    {
        int tileIndex = tileMap[tilePosition.x, tilePosition.y, tilePosition.z];
        TileType tileType = tileTypes[tileIndex];

        if (!tileType.CanRepeatV) EnforceRepeatV(tilePosition.x, tilePosition.y, tilePosition.z, tileIndex);
        if (!tileType.CanRepeatH) EnforceRepeatH(tilePosition.x, tilePosition.y, tilePosition.z, tileIndex);

        for (Direction i = 0; i <= Direction.Down; i++)
        {
            Vector3Int neighborPosition = tilePosition;
            switch (i)
            {
                case Direction.North:
                    neighborPosition.z += 1;
                    break;
                case Direction.South:
                    neighborPosition.z -= 1;
                    break;
                case Direction.East:
                    neighborPosition.x += 1;
                    break;
                case Direction.West:
                    neighborPosition.x -= 1;
                    break;
                case Direction.Down:
                    neighborPosition.y -= 1;
                    break;
                case Direction.Up:
                    neighborPosition.y += 1;
                    break;
            }

            if (neighborPosition.x >= 0 && neighborPosition.x < dimensions.x && neighborPosition.y >= 0 && neighborPosition.y < dimensions.y && neighborPosition.z >= 0 && neighborPosition.z < dimensions.z)
            {

                if (tileMap[neighborPosition.x, neighborPosition.y, neighborPosition.z] == -1 && tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z].Count(c => c == true) > 1)
                {
                    // See if there is a connection from the current tile to the neighbor
                    int originConnection = tileType.connections[(int)i];
                    bool found = false;

                    // Remove all possible tiles from neighbor
                    if (originConnection < 1)
                    {
                        for (int j = 0; j < tileCount; j++)
                        {
                            if (tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j])
                            {
                                tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j] = false;
                                found = true;
                            }
                        }
                    }
                    else
                    {
                        // If there is a connection, the neighbor must have a connection to the current tile as well, with the same connection ID
                        for (int j = 0; j < tileCount; j++)
                        {
                            if (tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j])
                            {
                                TileType tile = tileTypes[j];

                                int d = (int)i % 2 == 0 ? (int)i + 1 : (int)i - 1; // Get the opposite direction

                                // Remove if there is *NO* connection
                                if (tile.connections[d] != originConnection)
                                {
                                    tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j] = false;
                                    found = true;
                                }
                            }
                        }
                    }

                    if (found)
                    {
                        tilesToProcess.Push(neighborPosition);
                    }
                }
            }
        }
    }

    private void EnforceRepeatV(int x, int y, int z, int type)
    {
        if (y > 0) tileMapArray[x, y - 1, z][type] = false;
        if (y < dimensions.y - 1) tileMapArray[x, y + 1, z][type] = false;
    }

    private void EnforceRepeatH(int x, int y, int z, int type)
    {
        if (x > 0) tileMapArray[x - 1, y, z][type] = false;
        if (x < dimensions.x - 1) tileMapArray[x + 1, y, z][type] = false;
        if (z > 0) tileMapArray[x, y, z - 1][type] = false;
        if (z < dimensions.z - 1) tileMapArray[x, y, z + 1][type] = false;
    }

    // Get the entropy of a tile - TODO: Update this to support weights
    private float GetEntropy(Vector3Int pos)
    {
        float entropy = 0;
        for (int i = 0; i < tileCount; i++)
        {
            entropy += tileMapArray[pos.x, pos.y, pos.z][i] ? tileTypes[i].weight * Mathf.Log10(tileTypes[i].weight) : 0;
        }
        return -1 * entropy;
    }

    private void OnDrawGizmos()
    {
        if (tileMapArray != null && setupComplete)
        {
            for (int x = 0; x < dimensions.x; x++)
            {
                for (int z = 0; z < dimensions.z; z++)
                {
                    for (int y = 0; y < dimensions.y; y++)
                    {
                        float e = GetEntropy(new Vector3Int(x, y, z));
                        float size = 0.1f;
                        Vector3 p = new Vector3(x * 2, y * 2, z * 2);
                        if (e == 0 && tileMap[x, y, z] == -1)
                        {
                            Gizmos.color = Color.black;
                        } else if (e == 0)
                        {
                            Gizmos.color = Color.red;
                        } else
                        {
                            Gizmos.color = Color.white;
                        }
                        
                        if (tileMapArray[x, y, z][0])
                        {
                            Gizmos.DrawSphere(p, size * 2);
                        } else
                        {
                            Gizmos.DrawCube(p, new Vector3(size, size, size));
                        }
                        
                    }
                }
            }
        }
    }
*/
}

