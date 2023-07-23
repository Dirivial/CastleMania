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
using Unity.VisualScripting;

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

public struct Thing
{
    public int type;
    public Vector3 position;

    public Thing(int type, Vector3 position)
    {
        this.type = type;
        this.position = position;
    }
}

public class WFC : MonoBehaviour
{
    [SerializeField] private XML_IO XML_IO;
    [SerializeField] public Vector3Int dimensions = new Vector3Int(5, 5, 5);
    [SerializeField] private int tileSize = 2;

    private List<TileType> tileTypes;
    private int tileCount = 1;
    private bool setupComplete = false;
    private int[,,] tileMap;
    private bool[,,][] tileMapArray;
    private Stack<Vector3Int> tilesToProcess;
    //private Stack<SaveState> saveStates;

    private List<GameObject> boi;
    
    private List<Thing> viewConnections = new List<Thing>();

    private void Awake()
    {
        tilesToProcess = new Stack<Vector3Int>();
        tileMap = new int[dimensions.x, dimensions.y, dimensions.z];
        XML_IO.ClearTileTypes();
        XML_IO.Import();
        tileTypes = XML_IO.GetTileTypes();
        tileCount = tileTypes.Count;

        tileMapArray = new bool[dimensions.x, dimensions.y, dimensions.z][];
        boi = new List<GameObject>();
    }

    void Start()
    {

        // For testing
        InstantiateTileTypes();

        //GenerateFull();
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

        // Store the initial entropy of each coordinate in the tile map
        tileMapArray = new bool[dimensions.x, dimensions.y, dimensions.z][];
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    tileMapArray[x, y, z] = new bool[tileCount];

                    tileMap[x, y, z] = -1;
                    for (int i = 0; i < tileCount; i++)
                    {
                        tileMapArray[x, y, z][i] = true;
                    }
                }
            }
        }
        // This code was used to create a tower at some random location
        Vector3Int v = new Vector3Int(1, 0, 1);
        int c = 0;

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

        bool a = true;
        while (a)
        {
            Vector3Int nextWave = FindLowestEntropy();
            List<Vector3Int> toInstantiate;

            if (nextWave.x != -1 && nextWave.y != -1 && nextWave.z != -1)
            {
                PickTileAt(nextWave);
                toInstantiate = ProcessTiles();
                InstantiateDeezNut(toInstantiate);
                a = false;
            }
            else
            {
                Debug.Log("Could not get any further");
                a = false;
            }
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

    // Debug function to make sure that the tiles and rotations of tiles are correct - this should be kept for later when we add more types of tiles :-)
    private void InstantiateTileTypes()
    {
        for (int i = 0; i < tileCount; i++)
        {
            GameObject obj = Instantiate(tileTypes[i].tileObject, new Vector3(i * 2.3f, 0, -5), tileTypes[i].rotation);
            obj.transform.parent = transform;
            viewConnections.Add(new Thing(i, new Vector3(i * 2.3f, 0, -5)));
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
                    if (index >= 0)
                    {
                        GameObject obj = Instantiate(tileTypes[index].tileObject, new Vector3(x * tileSize, y * tileSize, z * tileSize), tileTypes[index].rotation);
                        obj.transform.parent = transform;
                        boi.Add(obj);
                    } else
                    {
                        continue;
/*                        GameObject obj = Instantiate(tileTypes[0].tileObject, new Vector3(x * tileSize, y * tileSize, z * tileSize), tileTypes[0].rotation);
                        obj.transform.parent = transform;
                        boi.Add(obj);*/
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

        tilesToProcess.Push(pos);
    }

    // Find the first tile with the lowest entropy
    private Vector3Int FindLowestEntropy()
    {
        // Look for the lowest entropy in the tile map
        float lowestEntropy = tileCount + 1;
        List<Vector3Int> possiblePositions = new List<Vector3Int>();
        Vector3Int lowestEntropyPosition = new Vector3Int(-1, -1, -1);
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    if (tileMap[x, y, z] != -1) continue;
                    possiblePositions.Add(new Vector3Int(x, y, z));

                    float entropy = GetEntropy(new Vector3Int(x, y, z));

                    if (entropy < lowestEntropy && entropy > 0)
                    {
                        lowestEntropy = entropy;
                        lowestEntropyPosition = new Vector3Int(x, y, z);
                    }
                }
            }
        }

        if (lowestEntropy != tileCount + 1) return lowestEntropyPosition;

        return possiblePositions.Count > 0 ? possiblePositions[Mathf.RoundToInt(Random.Range(0, possiblePositions.Count -1))] : new Vector3Int(-1, -1, -1);
    }

    private int ChooseTileTypeAt(int x, int y, int z)
    {
        Debug.Log("Investigating " + new Vector3Int(x, y, z));
/*        if (y == 0)
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
        }*/

        List<int> choices = new List<int>(); // All possible choices
        string names = "";
        string removed = "";

        for (int i = 1; i < tileCount; i++)
        {
            if (tileMapArray[x, y, z][i])
            {
                //Debug.Log("Trying tile: " + tileTypes[i].name);
                if (HasPossibleConnection(x, y, z, i))
                {
                    choices.Add(i);
                    names += tileTypes[i].name + " ";
                } else
                {
                    removed += tileTypes[i].name + " ";
                }
            }
        }
        


        // Choose a random tile type from the list of possible tile types
        if (choices.Count > 0)
        {
            //Debug.Log("removed tiles: " + removed);
            Debug.Log("Possible tiles: " + names);
            Debug.Log("");
            return ChooseWithWeights(choices);
        }
        else
        {
            Debug.Log("No possible tile " + new Vector3Int(x, y, z));
            return -1;
        }
    }

    // Thank you chatGPT :-)
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

    private bool HasPossibleConnection(int x, int y, int z, int i)
    {
        // If the current tile has a connection to an empty tile (as in there can be no tile there), return false

        if (tileTypes[i].hasConnection[(int)Direction.West])
        {
            if (x == 0 || (tileMap[x - 1, y, z] == -1 && !tileMapArray[x - 1, y, z].Any(t => t == true)))
            {
                //Debug.Log("Failed West.");
                return false;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.East])
        {
            if (x == dimensions.x - 1 || (tileMap[x + 1, y, z] == -1 && !tileMapArray[x + 1, y, z].Any(t => t == true)))
            {
                //Debug.Log("Failed East.");
                return false;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.South])
        {
            if (z == 0 || (tileMap[x, y, z - 1] == -1 && !tileMapArray[x, y, z - 1].Any(t => t == true)))
            {
                //Debug.Log("Failed South.");
                return false;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.North])
        {
            if (z == dimensions.z - 1 || (tileMap[x, y, z + 1] == -1 && !tileMapArray[x, y, z + 1].Any(t => t == true)))
            {
                //Debug.Log("Failed North.");
                return false;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.Down] && (y == 0 || (tileMap[x, y - 1, z] == -1 && !tileMapArray[x, y - 1, z].Any(t => t == true))))
        {
            //Debug.Log("Failed Down.");
            return false;
        }

        if (tileTypes[i].hasConnection[(int)Direction.Up] && (y == dimensions.y - 1 || (tileMap[x, y + 1, z] == -1 && !tileMapArray[x, y + 1, z].Any(t => t == true))))
        {
            //Debug.Log("Failed Up.");
            return false;
        }

        //Debug.Log("Passed");
        return true;
    }

    private bool HasConnection(int x, int y, int z, int i)
    {
        /*
        if (x > 0 && tileMap[x - 1, y, z] != -1 && tileTypes[tileMap[x - 1, y, z]].connections[(int)Direction.East] == tileTypes[i].connections[(int)Direction.West]) return true;
        if (x < dimensions.x - 1 && tileMap[x + 1, y, z] != -1 && tileTypes[tileMap[x + 1, y, z]].connections[(int)Direction.West] == tileTypes[i].connections[(int)Direction.East]) return true;
        if (z > 0 && tileMap[x, y, z - 1] != -1 && tileTypes[tileMap[x, y, z - 1]].connections[(int)Direction.North] == tileTypes[i].connections[(int)Direction.South]) return true;
        if (z < dimensions.z - 1 && tileMap[x, y, z + 1] != -1 && tileTypes[tileMap[x, y, z + 1]].connections[(int)Direction.South] == tileTypes[i].connections[(int)Direction.North]) return true;
        */
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
                    Debug.Log("GAMING");
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
                UpdateNeighbors(tilePosition);
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

        //if (!tileType.CanRepeatV) EnforceRepeatV(tilePosition.x, tilePosition.y, tilePosition.z, tileIndex);
        //if (!tileType.CanRepeatH) EnforceRepeatH(tilePosition.x, tilePosition.y, tilePosition.z, tileIndex);

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
                    //Debug.Log("This tile: " + tileType.name + " to direction: " + i);
                    bool originConnection = tileType.neighbors[(int)i].Length == 0;
                    bool found = false;

                    // Remove all possible tiles from neighbor
                    if (originConnection)
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

                        string names = "";
                        // Remove the possible tiles of the neighbor that are not in the current tiles possible neighbors
                        for (int j = 0; j < tileCount; j++)
                        {
                            if (tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j])
                            {
                                TileType tile = tileTypes[j];

                                if (!tileType.neighbors[(int)i].Contains(tile.name)) // With a complete array of possible connections, this should be a lot quicker
                                {
                                    tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j] = false;
                                    found = true;
                                    names += tile.name + " ";
                                }
                            }
                        }
                        //Debug.Log(tilePosition +  " From " + i + " removed " + names);
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

    // Get the entropy of a tile
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
        Gizmos.color = Color.black;
        float size = 0.1f;
        if (tileMapArray != null && setupComplete)
        {
            for (int x = 0; x < dimensions.x; x++)
            {
                for (int z = 0; z < dimensions.z; z++)
                {
                    for (int y = 0; y < dimensions.y; y++)
                    {

                        for (int i = 0; i < tileCount; i++)
                        {
                            if (tileMapArray[x, y, z][i])
                            {
                                if (i == 0)
                                {
                                    Gizmos.color = Color.red;
                                } else
                                {
                                    Gizmos.color = Color.black;
                                }
                                Vector3 p = new Vector3(x * 2 - 0.5f + (i % 5) * (size + 0.05f), y * 2, z * 2 - 0.5f + (i % 3) * (size + 0.05f));

                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }
                        }

                        Gizmos.color = Color.green;
                        // Draw cubes on the connections
                        if (tileMap[x, y, z] != -1)
                        {
                            TileType t = tileTypes[tileMap[x, y, z]];
                            if (t.hasConnection[(int)Direction.North])
                            {
                                Vector3 p = new Vector3(x * 2, y * 2, z * 2 + 1);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }
                            if (t.hasConnection[(int)Direction.South])
                            {
                                Vector3 p = new Vector3(x * 2, y * 2, z * 2 - 1);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }

                            if (t.hasConnection[(int)Direction.East])
                            {
                                Vector3 p = new Vector3(x * 2 + 1, y * 2, z * 2);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }
                            if (t.hasConnection[(int)Direction.West])
                            {
                                Vector3 p = new Vector3(x * 2 - 1, y * 2, z * 2);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }

                            if (t.hasConnection[(int)Direction.Up])
                            {
                                Vector3 p = new Vector3(x * 2, y * 2 - 1, z * 2);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }
                            if (t.hasConnection[(int)Direction.Down])
                            {
                                Vector3 p = new Vector3(x * 2, y * 2 + 1, z * 2);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }
                        }
                    }
                }
            }
        }
        foreach (Thing ting in viewConnections)
        {
            Gizmos.color = Color.green;
            // Draw cubes on the connections
            TileType t = tileTypes[ting.type];
            float x = ting.position.x; float y = ting.position.y; float z = ting.position.z;
            if (t.hasConnection[(int)Direction.North])
            {
                Vector3 p = new Vector3(x, y * 2, z + 1);
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }
            if (t.hasConnection[(int)Direction.South])
            {
                Vector3 p = new Vector3(x, y * 2, z - 1);
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }

            if (t.hasConnection[(int)Direction.East])
            {
                Vector3 p = new Vector3(x + 1, y * 2, z);
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }
            if (t.hasConnection[(int)Direction.West])
            {
                Vector3 p = new Vector3(x - 1, y * 2, z);
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }

            if (t.hasConnection[(int)Direction.Up])
            {
                Vector3 p = new Vector3(x, y * 2 - 1, z);
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }
            if (t.hasConnection[(int)Direction.Down])
            {
                Vector3 p = new Vector3(x, y * 2 + 1, z );
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }
        }
    }

}
            

