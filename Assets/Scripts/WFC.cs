using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using Debug = UnityEngine.Debug;
using System.Data;

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

// Todo - rename or remove
// This is used to display the connections
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
    [SerializeField] public Vector3Int tileScaling = new Vector3Int(100, 100, 100);
    [SerializeField] private int tileSize = 4;
    [SerializeField] private int floorGapMax = 5;
    [SerializeField] private int floorGapMin = 1;
    [SerializeField] private AnimationCurve towerGrowth;


    public static int UNDECIDED = -1;
    public static int EMPTY_TILE = -2;

    private List<TileType> tileTypes;
    private int tileCount = 1;
    private bool setupComplete = false;
    private int[,,] tileMap;
    private bool[,,][] tileMapArray;
    private Stack<Vector3Int> tilesToProcess;
    private List<GameObject> instantiatedTiles;
    private bool stepping = false;
    private float count = 0;

    private int[] floorHeights;

    // For debugging
    private List<Thing> viewConnections = new List<Thing>();

    // For my game
    private int tower_T;
    private int tower_N;
    private int tower_B;


    private void Awake()
    {
        tilesToProcess = new Stack<Vector3Int>();
        tileMap = new int[dimensions.x, dimensions.y, dimensions.z];
        XML_IO.ClearTileTypes();
        XML_IO.Import();
        tileTypes = XML_IO.GetTileTypes();
        tileCount = tileTypes.Count;

        tileMapArray = new bool[dimensions.x, dimensions.y, dimensions.z][];
        instantiatedTiles = new List<GameObject>();

        // For my game
        tower_T = tileTypes.FindIndex(t => t.name == "tower_top_0");
        tower_N = tileTypes.FindIndex(t => t.name == "tower_0");
        tower_B = tileTypes.FindIndex(t => t.name == "tower_bot_0");
    }

    void Start()
    {

        // For testing
        InstantiateTileTypes();

        //PrintConnectionCount();

        GenerateFull();
    }

    private void Update()
    {
        if (stepping)
        {
            if (count <= 0)
            {
                count = 1;
                TakeStep();
            } else
            {
                count -= Time.deltaTime * 8;
            }
        }
    }

    private void PrintConnectionCount()
    {
        foreach (TileType tileType in tileTypes)
        {
            int n_neighbors = 0;
            foreach (bool[] n in tileType.neighbors)
            {
                foreach(bool b in n)
                {
                    if (b)
                    {
                        n_neighbors++;
                    }
                }
            }
            Debug.Log(tileType.name + " has a total of " + n_neighbors + " neighbors");
        }
    }

    // Clear everything important
    public void Clear()
    {
        // Setup clears everything anyways so just call that one instead - a bit unnecessary if you change the dimensions though
        Setup();
        setupComplete = false;
    }

    // Do any necessary steps for running the WFC algorithm
    public void Setup()
    {
        //Debug.Log("Generating a new model - Clearing " + boi.Count + " items");

        // Clear any already instantiated tiles
        for (int i = instantiatedTiles.Count - 1; i >= 0; i--)
        {
            Destroy(instantiatedTiles[i]);
        }
        instantiatedTiles.Clear();

        // In the start we do not have any tiles to process
        tilesToProcess.Clear();

        // Create a new tileMap to be filled later on
        tileMap = new int[dimensions.x, dimensions.y, dimensions.z];

        // This variable is used to store an array of boolean values for each position
        tileMapArray = new bool[dimensions.x, dimensions.y, dimensions.z][];
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    // Set each tile to be undecided
                    tileMap[x, y, z] = UNDECIDED;

                    // In the beginning all positions has the potential to be one of all possible tiles
                    tileMapArray[x, y, z] = new bool[tileCount];

                    // Set each boolean value to true to represent the positions ability to
                    // be the corresponding tile in the tileTypes list
                    for (int i = 0; i < tileCount; i++)
                    {
                        tileMapArray[x, y, z][i] = true;
                    }
                }
            }
        }

        // Generate floor heights
        GenerateFloorHeights();

        setupComplete = true;
    }

    // Insert any important features before starting WFC
    private void PrePopulate()
    {
        List<Vector3Int> list = new List<Vector3Int>();

        // Sprinkle in some bottom tower pieces tiles
        for (int i = 0; i < Mathf.RoundToInt(dimensions.x); i++)
        {
            Vector3Int v = new Vector3Int(Random.Range(0, dimensions.x), 0, Random.Range(0, dimensions.z));

            if (!list.Contains(v) && HasEmptySpaceAround(v))
            {
                tileMap[v.x, v.y, v.z] = tower_B;
                for (int j = 0; j < tileTypes.Count; j++)
                {
                    tileMapArray[v.x, v.y, v.z][j] = false;
                }
                UpdateNeighbors(v);
                tilesToProcess.Push(v);
            }
            list.Add(v);
        }
 
        ProcessTiles();
        if (stepping) InstantiateTiles();
    }

    // Look at spaces around the given position, if they are undecided then this position has empty space around it
    private bool HasEmptySpaceAround(Vector3Int pos)
    {
        for (int i = -1; i < 2; i++)
        {
            for (int j = -1; j < 2; j++)
            {
                // Note that I do not have to care about i && j both being 0
                if (pos.x + i >= 0 && pos.z + j >= 0 && pos.x + i < dimensions.x && pos.z + j < dimensions.z)
                {
                    if (tileMap[pos.x + i, 0, pos.z + j] >= 0)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    // I did this to make my towers look neater
    private void PostProcess()
    {
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                for (int z = 0; z < dimensions.z; z++)
                {
                    int i = tileMap[x, y, z];
                    if (i >= 0)
                    {
                        if (i == tower_B)
                        {
                            // The selected tower is a bottom piece, remove it if there is no connection upwards
                            if (tileMap[x, y + 1, z] == EMPTY_TILE) { 
                                tileMap[x, y, z] = EMPTY_TILE; 
                            } else
                            {
                                // Put a couple of extra tower pieces in there to avoid starting at the same floor as all of the other tiles
                                for (int j = -1; j > -floorGapMin; j--)
                                {
                                    InstantiateTile(tileTypes[tower_N].tileObject, x, j, z, tileTypes[tower_N].rotation);
                                }
                                InstantiateTile(tileTypes[tower_B].tileObject, x, -floorGapMin, z, tileTypes[tower_B].rotation);
                            }
                            

                        } else if (i == tower_N)
                        {
                            // Swap to top piece if no upward connection
                            if (y == dimensions.y - 1 || tileMap[x, y + 1, z] == EMPTY_TILE)
                            {
                                GrowTower(x, y, z);
                            }
                        } else if (i == tower_T)
                        {
                            GrowTower(x, y, z);
                        }
                    }
                }
            }
        }
    }

    public void GrowTower(int x, int y, int z)
    {
        tileMap[x, y, z] = tower_N;

        // Get a number to grow to
        float f = Random.Range(0.0f, 1.0f);
        float height = towerGrowth.Evaluate(f);
        int height_i = Mathf.RoundToInt(height) + 1;

        int start = floorHeights[y] + 1;
        int end = floorHeights[y] + height_i;

        // Make sure that the growth does not lead to poking through floors
        // If we are at the top, there is no need to worry about breaking through floors
        if (y != dimensions.y - 1 && floorHeights[y + 1] < end) end = floorHeights[y + 1] - 1;

        InstantiateTile(tileTypes[tower_T].tileObject, x, end, z, tileTypes[tower_T].rotation);

        for (int i = start; i < end; i++)
        {
            InstantiateTile(tileTypes[tower_N].tileObject, x, i, z, tileTypes[tower_N].rotation);
        }
    }

    private void InstantiateTile(GameObject gameObject, int x, int y, int z, Quaternion rotation)
    {
        GameObject obj = Instantiate(gameObject, new Vector3(x * tileSize, y * tileSize, z * tileSize), rotation);
        obj.transform.localScale = tileScaling;
        obj.transform.parent = transform;
        instantiatedTiles.Add(obj);
    }

    // Generate a complete volume of tiles
    public void GenerateFull()
    {
        Debug.Log("Pre 'Processing' Started...");

        Setup();
        PrePopulate();

        Debug.Log("Pre 'Processing' Done.");



        Debug.Log("WFC Started...");

        int maxIterationsLeft = dimensions.x * dimensions.y * dimensions.z;
        Vector3Int nextWave = FindLowestEntropy();

        // Run while there are still tiles to collapse
        while (nextWave.x != -1 && nextWave.y != -1 && maxIterationsLeft > 0)
        {
            PickTileAt(nextWave);
            ProcessTiles();
            nextWave = FindLowestEntropy();
            maxIterationsLeft--;
        }
        Debug.Log("WFC Done");

        Debug.Log("Post 'Processing' Started...");

        PostProcess();

        Debug.Log("Post 'Processing' Done.");

        InstantiateTiles();
    }

    public void ToggleStepping()
    {
        stepping = !stepping;
        Debug.Log("Stepping " + (stepping ? "ON" : "OFF"));
    }

    private void TakeStep()
    {
        // Run setup if needed
        if (!setupComplete)
        {
            Setup();
            PrePopulate();
            count = 10;
            setupComplete = true;
        }

        Vector3Int nextWave = FindLowestEntropy();
        List<Vector3Int> toInstantiate;

        if (nextWave.x != -1 && nextWave.y != -1 && nextWave.z != -1)
        {
            PickTileAt(nextWave);
            toInstantiate = ProcessTiles();
            InstantiateTile(toInstantiate);
        }
        else
        {
            stepping = false;
            Debug.Log("Could not get any further");
        }
    }

    // Debug function to make sure that the tiles and rotations of tiles are correct - this should be kept for later when we add more types of tiles :-)
    private void InstantiateTileTypes()
    {
        float z = -3;
        float x = 0;
        for (int i = 0; i < tileCount - 1; i++)
        {
            if (tileTypes[i].name.EndsWith('0'))
            {
                x = 0;
                z -= (tileSize + 1f);
            }
            GameObject obj = Instantiate(tileTypes[i].tileObject, new Vector3(x, 0, z), tileTypes[i].rotation);
            obj.transform.localScale = tileScaling;
            obj.transform.parent = transform;
            viewConnections.Add(new Thing(i, new Vector3(x, 0, z)));

            x += (2.5f + tileSize);
        }
    }

    private void InstantiateTiles()
    {
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int index = tileMap[x, y, z];
                    if (index >= 0 && tileTypes[index].name != "-1")
                    {
                        int height = floorHeights[y]; 

                        GameObject obj = Instantiate(tileTypes[index].tileObject, new Vector3(x * tileSize, height * tileSize, z * tileSize), tileTypes[index].rotation);
                        obj.transform.localScale = tileScaling;
                        obj.transform.parent = transform;
                        instantiatedTiles.Add(obj);
                        
                        // Fill in towers
                        if (y > 0 && (index == tower_N || index == tower_T))
                        {
                            int start = floorHeights[y-1] * tileSize + tileSize;
                            int end = height * tileSize;
                            for (int y2 = start; y2 < end; y2 += tileSize)
                            {
                                obj = Instantiate(tileTypes[tower_N].tileObject, new Vector3(x * tileSize, y2, z * tileSize), tileTypes[tower_N].rotation);
                                obj.transform.localScale = tileScaling;
                                obj.transform.parent = transform;
                                instantiatedTiles.Add(obj);
                                //Debug.Log(y2);
                            }
                        } 
                    }
                }
            }
        }
    }

    private void InstantiateTile(List<Vector3Int> toInstantiate)
    {
        foreach (Vector3Int v in toInstantiate)
        {
            // Ignore empty tiles
            if (tileMap[v.x, v.y, v.z] == EMPTY_TILE) continue;

            TileType tileType = tileTypes[tileMap[v.x, v.y, v.z]];
            
            if (tileType.name == "-1") continue;

            int height = floorHeights[v.y];
            
            GameObject obj = Instantiate(tileType.tileObject, new Vector3(v.x * tileSize, height * tileSize, v.z * tileSize), tileType.rotation);
            obj.transform.localScale = tileScaling;
            obj.transform.parent = transform;
            instantiatedTiles.Add(obj);

            // Fill in towers
            if (height > 0 && tileMap[v.x, v.y, v.z] == tower_N)
            {
                int start = floorHeights[v.y - 1] * tileSize + tileSize;
                int end = height * tileSize;
                for (int y2 = start; y2 < end; y2 += tileSize)
                {
                    obj = Instantiate(tileTypes[tower_N].tileObject, new Vector3(v.x * tileSize, y2, v.z * tileSize), tileTypes[tower_N].rotation);
                    obj.transform.localScale = tileScaling;
                    obj.transform.parent = transform;
                    instantiatedTiles.Add(obj);
                    //Debug.Log(y2);
                }
            }
        }
    }

    // Pick a tile at the given tile position
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
                    if (tileMap[x, y, z] != UNDECIDED) continue;
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
        //Debug.Log("Position: " + x + " , " + y + ", " + z);
        List<int> choices = new List<int>(); // All possible choices

        for (int i = 0; i < tileCount; i++)
        {
            if (tileMapArray[x, y, z][i])
            {
                //Debug.Log("Trying tile: " + tileTypes[i].name);
                if (HasPossibleConnection(x, y, z, i) && EnforceCustomConstraints(x, y, z, i))
                {
                    choices.Add(i);
                }
            }
        }

        // Choose a random tile type from the list of possible tile types
        if (choices.Count > 0)
        {
            return ChooseWithWeights(choices);
        }
        else
        {
            //Debug.Log("No possible tile " + new Vector3Int(x, y, z));
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
            if (x > 0 && tileMap[x-1, y, z] == i) { return false; }
            if (x < dimensions.x - 1 && tileMap[x+1, y, z] == i) { return false; }
            if (z > 0 && tileMap[x, y, z - 1] == i) { return false; }
            if (z < dimensions.z - 1 && tileMap[x, y, z + 1] == i) { return false; }
        }

        if (tileTypes[i].noRepeatH)
        {
            if (y > 0 && tileMap[x, y - 1, z] == i) { return false; }
            if (y < dimensions.y - 1 && tileMap[x, y + 1, z] == i) { return false; }
        }

        return true;
    }

    private bool CanConnectTo(int x, int y, int z, bool[] neighbors)
    {
        for (int i = 0; i < neighbors.Length; i++)
        {
            if (neighbors[i] && tileMap[x, y, z] == i) return true;
        }
        return false;
    }

    private bool CanConnect(int x, int y, int z, int i)
    {

        if (tileTypes[i].hasConnection[(int)Direction.West])
        {
            if (x > 0 && CanConnectTo(x-1, y, z, tileTypes[i].neighbors[(int)Direction.West]))
            {
                return true;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.East])
        {
            if (x < dimensions.x - 1 && CanConnectTo(x + 1, y, z, tileTypes[i].neighbors[(int)Direction.East]))
            {
                //Debug.Log("Failed East.");
                return true;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.South])
        {
            if (z > 0 && CanConnectTo(x, y, z - 1, tileTypes[i].neighbors[(int)Direction.South]))
            {
                //Debug.Log("Failed South.");
                return true;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.North])
        {
            if (z < dimensions.z - 1 && CanConnectTo(x, y, z + 1, tileTypes[i].neighbors[(int)Direction.North]))
            {
                //Debug.Log("Failed North.");
                return true;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.Down] && (y > 0 && CanConnectTo(x, y - 1, z, tileTypes[i].neighbors[(int)Direction.Down])))
        {
            //Debug.Log("Failed Down.");
            return true;
        }

        if (tileTypes[i].hasConnection[(int)Direction.Up] && (y < dimensions.y - 1 && CanConnectTo(x, y + 1, z, tileTypes[i].neighbors[(int)Direction.Up])))
        {
            //Debug.Log("Failed Up.");
            return true;
        }
        return false;
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

    // Checks if the given postion has the potential to fulfill its connections
    // This is not possible if the give tile type has a connection to an empty tile that has no possible tiles
    // The goal is to reduce the number of tiles connecting to air/empty tiles
    private bool HasPossibleConnection(int x, int y, int z, int i)
    {
        // If the current tile has a connection to an empty tile (as in there can be no tile there), return false

        if (tileTypes[i].hasConnection[(int)Direction.West])
        {
            if (x == 0 || tileMap[x - 1, y, z] == EMPTY_TILE)
            {
                //Debug.Log("Failed West.");
                return false;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.East])
        {
            if (x == dimensions.x - 1 || tileMap[x + 1, y, z] == EMPTY_TILE)
            {
                //Debug.Log("Failed East.");
                return false;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.South])
        {
            if (z == 0 || tileMap[x, y, z - 1] == EMPTY_TILE)
            {
                //Debug.Log("Failed South.");
                return false;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.North])
        {
            if (z == dimensions.z - 1 || tileMap[x, y, z + 1] == EMPTY_TILE)
            {
                //Debug.Log("Failed North.");
                return false;
            }
        }

        if (tileTypes[i].hasConnection[(int)Direction.Down] && (y == 0 || tileMap[x, y - 1, z] == EMPTY_TILE))
        {
            //Debug.Log("Failed Down.");
            return false;
        }

        if (tileTypes[i].hasConnection[(int)Direction.Up] && (y == dimensions.y - 1 || tileMap[x, y + 1, z] == EMPTY_TILE))
        {
            //Debug.Log("Failed Up.");
            return false;
        }

        //Debug.Log("Passed");
        return true;
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

            if (tileMap[tilePosition.x, tilePosition.y, tilePosition.z] == UNDECIDED)
            {
                if (tileMapArray[tilePosition.x, tilePosition.y, tilePosition.z].Count(c => c == true) == 1)
                {
                    Debug.Log("GAMING");
                    int chosenTile = ChooseTileTypeAt(tilePosition.x, tilePosition.y, tilePosition.z);

                    tileMap[tilePosition.x, tilePosition.y, tilePosition.z] = chosenTile;
                    UpdateNeighbors(tilePosition);
                    setTiles.Add(tilePosition);

                    // We have a single chunk type, so we can set it
                    for (int j = tileCount - 1; j >= 0; j--)
                    {
                        tileMapArray[tilePosition.x, tilePosition.y, tilePosition.z][j] = false;
                    }
                }
                else if (tileMapArray[tilePosition.x, tilePosition.y, tilePosition.z].Count(c => c == true) == 0)
                {
                    tileMap[tilePosition.x, tilePosition.y, tilePosition.z] = EMPTY_TILE;
                    UpdateNeighbors(tilePosition);
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

            if (neighborPosition.x >= 0 && neighborPosition.x < dimensions.x &&
                neighborPosition.y >= 0 && neighborPosition.y < dimensions.y &&
                neighborPosition.z >= 0 && neighborPosition.z < dimensions.z)
            {

                if (tileMap[neighborPosition.x, neighborPosition.y, neighborPosition.z] == UNDECIDED)
                {
                    // See if there is a connection from the current tile to the neighbor
                    int tileIndex = tileMap[tilePosition.x, tilePosition.y, tilePosition.z];
                    bool found = false;

                    if (tileIndex > UNDECIDED)
                    {
                        TileType tileType = tileTypes[tileIndex];
                        bool originConnection = !tileType.hasConnection[(int)i];

                        // Remove all possible tiles from neighbor
                        if (originConnection)
                        {
                            for (int j = 0; j < tileCount; j++)
                            {
                                if (tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j])
                                {
                                    TileType tile = tileTypes[j];

                                    // Get opposite direction
                                    int opposite = (int)i % 2 == 0 ? (int)i + 1 : (int)i - 1;

                                    if (tile.hasConnection[opposite])
                                    {
                                        tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j] = false;
                                        found = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Remove the possible tiles of the neighbor that are not in the current tiles possible neighbors
                            for (int j = 0; j < tileCount; j++)
                            {
                                if (tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j])
                                {
                                    TileType tile = tileTypes[j];

                                    if (!tileType.neighbors[(int)i][j])
                                    {
                                        tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j] = false;
                                        found = true;
                                    }
                                }
                            }
                        }
                    } else if (tileIndex == EMPTY_TILE)
                    {
                        // Tile is empty, so remove tiles that have a connection to this position
                        for (int j = 0; j < tileCount; j++)
                        {
                            if (tileMapArray[neighborPosition.x, neighborPosition.y, neighborPosition.z][j])
                            {
                                TileType tile = tileTypes[j];

                                // Get opposite direction
                                int opposite = (int)i % 2 == 0 ? (int)i + 1 : (int)i - 1;

                                if (tile.hasConnection[opposite])
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
                                Vector3 p = new Vector3(x * tileSize - 0.5f + (i % 5) * (size + 0.05f), y * tileSize, z * tileSize - 0.5f + (i % 3) * (size + 0.05f));

                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }
                        }

                        Gizmos.color = Color.green;
                        // Draw cubes on the connections
                        if (tileMap[x, y, z] > UNDECIDED)
                        {
                            TileType t = tileTypes[tileMap[x, y, z]];
                            if (t.hasConnection[(int)Direction.North])
                            {
                                Vector3 p = new Vector3(x * tileSize, y * tileSize, z * tileSize + tileSize / 2);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }
                            if (t.hasConnection[(int)Direction.South])
                            {
                                Vector3 p = new Vector3(x * tileSize, y * tileSize, z * tileSize - tileSize / 2);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }

                            if (t.hasConnection[(int)Direction.East])
                            {
                                Vector3 p = new Vector3(x * tileSize + tileSize / 2, y * tileSize, z * tileSize);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }
                            if (t.hasConnection[(int)Direction.West])
                            {
                                Vector3 p = new Vector3(x * tileSize - tileSize / 2, y * tileSize, z * tileSize);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }

                            if (t.hasConnection[(int)Direction.Up])
                            {
                                Vector3 p = new Vector3(x * tileSize, y * tileSize + tileSize / 2, z * tileSize);
                                Gizmos.DrawCube(p, new Vector3(size, size, size));
                            }
                            if (t.hasConnection[(int)Direction.Down])
                            {
                                Vector3 p = new Vector3(x * tileSize, y * tileSize - tileSize / 2, z * tileSize);
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
                Vector3 p = new Vector3(x, y * tileSize, z + tileSize / 2);
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }
            if (t.hasConnection[(int)Direction.South])
            {
                Vector3 p = new Vector3(x, y * tileSize, z - tileSize / 2);
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }

            if (t.hasConnection[(int)Direction.East])
            {
                Vector3 p = new Vector3(x + tileSize / 2, y * tileSize, z);
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }
            if (t.hasConnection[(int)Direction.West])
            {
                Vector3 p = new Vector3(x - tileSize / 2, y * tileSize, z);
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }

            if (t.hasConnection[(int)Direction.Up])
            {
                Vector3 p = new Vector3(x, y * tileSize + tileSize / 2, z);
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }
            if (t.hasConnection[(int)Direction.Down])
            {
                Vector3 p = new Vector3(x, y * tileSize - tileSize / 2, z );
                Gizmos.DrawCube(p, new Vector3(size, size, size));
            }
        }
    }

}
            

