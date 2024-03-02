using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

// We only need to rotate around the y-axis.
// forward/back = z-axis
// left/right = x-axis
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
    D, // This should be '\\', I chose D for diagonal
}

public enum SocketDirection
{
    Left, Right, Forward, Back
}

public struct TileNeighbors
{
    public int tileIndex;
    public string[] npX;
    public string[] npY;
    public string[] npZ;
    public string[] nnX;
    public string[] nnY;
    public string[] nnZ;
}

[Serializable]
public class TileEntry
{
    public string Name;
    public GameObject tileObject;
    public Symmetry Symmetry;
    public float Weight = 1f;
    public List<Direction> ignoreSide;
    public string constraints;

    public TileEntry()
    {
        Name = string.Empty;
        Symmetry = Symmetry.X;
        constraints = string.Empty;
    }

    public TileEntry(string name, Symmetry symmetry, float weight)
    {
        Name = name;
        Symmetry = symmetry;
        Weight = weight;
    }
}

[Serializable]
public class PrototypeXML
{
    // General information
    public string Name;

    public float weight;
    public string constraints;

    // Sockets
    public string posX = "-1";
    public string posY = "-1";
    public string posZ = "-1";
    public string negX = "-1";
    public string negY = "-1";
    public string negZ = "-1";

    // Neighbors
    public string[] npX;
    public string[] npY;
    public string[] npZ;
    public string[] nnX;
    public string[] nnY;
    public string[] nnZ;

    public PrototypeXML()
    {

    }

    public PrototypeXML(string name, float weight, string constraints)
    {
        this.Name = name;
        this.weight = weight;
        this.constraints = constraints;
    }
}

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObject/XML_IO", order = 1)]
public class XML_IO : ScriptableObject
{
    public Vector3Int meshRotation = new Vector3Int();
    public string xmlPath = "models";
    public List<TileEntry> Tiles;
    public float emptyTileWeight = 0.99f;

    private MeshSockets Sockets = new MeshSockets();
    private List<TileType> tileTypes = new List<TileType>();

    public List<TileType> GetTileTypes()
    {
        return tileTypes;
    }

    public void ClearTileTypes()
    {
        // Clear earlier tiles
        tileTypes.Clear();
    }

    // This is to import the information stored in a XML document, most likely generated from the function Export();
    public void Import()
    {
        // Specify the file path within the "Assets" folder
        string filePath = Application.dataPath + "/" + xmlPath + ".xml";

        // Check if the file exists before attempting to read it
        if (File.Exists(filePath))
        {
            // Create XmlSerializer for the Person type
            XmlSerializer serializer = new XmlSerializer(typeof(PrototypeXML[]));

            // Deserialize the XML data back into C# objects
            using (StreamReader reader = new StreamReader(filePath))
            {
                PrototypeXML[] tiles = (PrototypeXML[])serializer.Deserialize(reader);

                // Now you have your data in the 'people' array
                foreach (PrototypeXML tile in tiles)
                {
                    GameObject tileObject = null;
                    int foundId = 0;

                    // Find tileObject associated with the current tile
                    foreach (TileEntry t in Tiles)
                    {
                        if (tile.Name.Substring(0, tile.Name.Length - 2) == t.Name.ToLower())
                        {
                            //Debug.Log("Found game object! " + t.tileObject.name);
                            tileObject = t.tileObject;
                            break;
                        }
                        foundId++;
                    }

                    // The times of rotation is stored as the last character in the name
                    int rotation = Int32.Parse(tile.Name.Substring(tile.Name.Length - 1));

                    // Make sure to have at least a very low weight
                    float weight = tile.weight > 0 ? tile.weight : 0.001f;

                    TileType tileType = new TileType(tile.Name.ToLower(), rotation, weight, tileObject);

                    if (foundId < Tiles.Count)
                    {
                        tileType.id = foundId;
                    } else
                    {
                        tileType.id = -1;
                    }

                    // Assign any constraints to the tile
                    if (tile.constraints != null)
                    {
                        string[] parts = tile.constraints.Split(',');
                        foreach (string part in parts)
                        {
                            switch (part)
                            {
                                case "G":
                                    tileType.grounded = true;
                                    break;
                                case "MC":
                                    tileType.mustConnect = true;
                                    break;
                                case "NRH":
                                    tileType.noRepeatH = true;
                                    break;
                                case "NRV":
                                    tileType.noRepeatV = true;
                                    break;
                                case "TT":
                                    tileType.isTowerType = true;
                                    break;
                                default:
                                    continue;
                            }
                        }
                    }

                    tileTypes.Add(tileType);
                }

                // Set the neighbors now that we have extracted all of the tile types
                for (int i = 0; i < tileTypes.Count; i++)
                {
                    tileTypes[i].SetNeighbors(Direction.North, CreateNeighborsArray(tiles[i].npZ));
                    tileTypes[i].SetNeighbors(Direction.South, CreateNeighborsArray(tiles[i].nnZ));
                    tileTypes[i].SetNeighbors(Direction.East, CreateNeighborsArray(tiles[i].npX));
                    tileTypes[i].SetNeighbors(Direction.West, CreateNeighborsArray(tiles[i].nnX));
                    tileTypes[i].SetNeighbors(Direction.Up, CreateNeighborsArray(tiles[i].npY));
                    tileTypes[i].SetNeighbors(Direction.Down, CreateNeighborsArray(tiles[i].nnY));
                }

                Debug.Log("Number of tiles: " + tileTypes.Count);
            }
        }
        else
        {
            Debug.LogError("File not found: " + filePath);
        }
    }

    // Convert the neighbor array to an array of boolean values
    private bool[] CreateNeighborsArray(string[] neighbors)
    {
        bool[] n = new bool[tileTypes.Count];

        for (int i = 0; i < tileTypes.Count; i++)
        {
            n[i] = false;
        }

        foreach (string neighbor in neighbors)
        {
            int index = tileTypes.FindIndex(tile => tile.name.ToLower().Equals(neighbor));
            if (index < 0)
            {
                Debug.LogError("Neighbor not found");
            }
            n[index] = true;
        }

        return n;
    }

    // This function is for exporting tile information to an XML file, to reduce compute times
    public void Export()
    {
        Sockets.ClearList();
        if (xmlPath != null && xmlPath.Length > 0 && Tiles.Count > 0)
        {

            int totalTileCount = ComputeTileCount();

            PrototypeXML[] tileXML = new PrototypeXML[totalTileCount + 1];

            int k = 0;

            // Compute the sockets
            for (int i = 0; i < Tiles.Count; i++)
            {
                Debug.Log(Tiles[i].Name);

                // Get mesh
                MeshFilter mf = Tiles[i].tileObject.transform.GetComponent<MeshFilter>();
                Mesh mesh = mf.sharedMesh;

                // Assign sides to ignore
                bool[] sidesToIgnore = new bool[6] { false, false, false, false, false, false };
                foreach (Direction d in Tiles[i].ignoreSide)
                {
                    sidesToIgnore[(int)d] = true;
                }

                // Create XML prototype of the tile
                tileXML[k] = new PrototypeXML(Tiles[i].Name.ToLower() + "_0", Tiles[i].Weight, Tiles[i].constraints);
                List<string> sockets = Sockets.ComputeMeshSockets(mesh, meshRotation, sidesToIgnore);

                // Get cardinality of the current tile
                int cardinality = GetCardinality(Tiles[i].Symmetry);

                // Cardinality is the amount of rotated versions that look different -> so create rotated versions
                for (int j = 1; j < cardinality; j++) // Note that I start at 1 since the original one is already created
                {
                    tileXML[k + j] = new PrototypeXML(Tiles[i].Name.ToLower() + "_" + j, Tiles[i].Weight, Tiles[i].constraints);
                }

                // Assign sockets
                AssignSockets(sockets, tileXML, k, Tiles[i].Symmetry);
                k += cardinality;
            }

            // Add the empty tile
            tileXML[totalTileCount] = CreateEmpty();

            // Compute neighbors with the earlier assigned sockets
            ComputeNeighbors(tileXML);

            // Create XmlSerializer
            XmlSerializer serializer = new XmlSerializer(typeof(PrototypeXML[]));

            // Specify the file path
            string filePath = Application.dataPath + "/" + xmlPath + ".xml";

            // Serialize data to XML and save to the file
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                serializer.Serialize(writer, tileXML);
            }

            Debug.Log("Data saved to XML file: " + filePath);
            Sockets.PrintSockets();
        } else if (Tiles.Count == 0) {
            Debug.Log("No tile in list.");
        } else
        {
            Debug.Log("No path specified");
        }
    }

    private PrototypeXML CreateEmpty()
    {
        PrototypeXML emptyTile = new PrototypeXML();

        emptyTile.Name = "-1";
        emptyTile.weight = emptyTileWeight;

        emptyTile.posX = "-1";
        emptyTile.negX = "-1";
        emptyTile.posY = "-1";
        emptyTile.negY = "-1";
        emptyTile.posZ = "-1";
        emptyTile.negZ = "-1";

        emptyTile.npX = new string[0];
        emptyTile.nnX = new string[0];
        emptyTile.npY = new string[0];
        emptyTile.nnY = new string[0];
        emptyTile.npZ = new string[0];
        emptyTile.nnZ = new string[0];

        return emptyTile;
    }

    // I am switching stuff around a bit because my models are rotated
    private void AssignSockets(List<string> sockets, PrototypeXML[] tileXML, int index, Symmetry symmetry)
    {

        // Assign the first sockets
        tileXML[index].posX = sockets[0];
        tileXML[index].negX = sockets[1];

        tileXML[index].posZ = sockets[2];
        tileXML[index].negZ = sockets[3];

        tileXML[index].posY = sockets[4];
        tileXML[index].negY = sockets[5];


        // This is to make th
        SocketDirection[] socketDirections = new SocketDirection[4] { 0, SocketDirection.Right, SocketDirection.Forward, SocketDirection.Back };

        int cardinality = GetCardinality(symmetry);

        if (cardinality == 4)
        {
            for (int i = index + 1; i < index + 4; i++)
            {
                // Rotate sockets in the order specified in GetNext function.
                for (int j = 0; j < socketDirections.Length; j++)
                {
                    socketDirections[j] = GetNext(socketDirections[j]);
                }

                // Assign the rotated sockets to the prototype/tile
                tileXML[i].posX = sockets[(int)socketDirections[0]];
                tileXML[i].negX = sockets[(int)socketDirections[1]];
                tileXML[i].posZ = sockets[(int)socketDirections[2]];
                tileXML[i].negZ = sockets[(int)socketDirections[3]];

                // These should always remain the same
                tileXML[i].posY = sockets[4];
                tileXML[i].negY = sockets[5];
            }
            return;
        } else if (cardinality == 2)
        {
            for (int j = 0; j < socketDirections.Length; j++)
            {
                socketDirections[j] = GetNext(socketDirections[j]);
            }
            tileXML[index + 1].posX = sockets[(int)socketDirections[0]];
            tileXML[index + 1].negX = sockets[(int)socketDirections[1]];
            tileXML[index + 1].posZ = sockets[(int)socketDirections[2]];
            tileXML[index + 1].negZ = sockets[(int)socketDirections[3]];

            tileXML[index + 1].posY = sockets[4];
            tileXML[index + 1].negY = sockets[5];
        } 

        return;
    }

    // Compute which tiles can interface with each other
    private void ComputeNeighbors(PrototypeXML[] tileXML)
    {
        for (int i = 0; i < tileXML.Length; i++)
        {
            List<string> xnp = new List<string>();
            List<string> xnn = new List<string>();
            List<string> ynp = new List<string>();
            List<string> ynn = new List<string>();
            List<string> znp = new List<string>();
            List<string> znn = new List<string>();
            for (int j = 0; j < tileXML.Length; j++)
            {
                // Positive X
                if (tileXML[i].posX != "-1" && ShouldConnect(tileXML[i].posX, tileXML[j].negX))
                {
                    xnp.Add(tileXML[j].Name);
                }
                // Negative X
                if (tileXML[i].negX != "-1" && ShouldConnect(tileXML[i].negX, tileXML[j].posX))
                {
                    xnn.Add(tileXML[j].Name);
                }

                // Positive Z
                if (tileXML[i].posZ != "-1" && ShouldConnect(tileXML[i].posZ, tileXML[j].negZ))
                {
                    znp.Add(tileXML[j].Name);
                }
                // Negative Z
                if (tileXML[i].negZ != "-1" && ShouldConnect(tileXML[i].negZ, tileXML[j].posZ))
                {
                    znn.Add(tileXML[j].Name);
                }

                // These do not use symmetrical/asymmetrical sockets

                // Positive Y
                if (tileXML[i].posY != "-1" && tileXML[i].posY == tileXML[j].negY)
                {
                    ynp.Add(tileXML[j].Name);
                }
                // Negative Y
                if (tileXML[i].negY != "-1" && tileXML[i].negY == tileXML[j].posY)
                {
                    ynn.Add(tileXML[j].Name);
                }
            }

            // This might not makes sense for you, check if your models get fricked up
            tileXML[i].npX = xnp.ToArray();
            tileXML[i].nnX = xnn.ToArray();
            tileXML[i].npY = ynp.ToArray();
            tileXML[i].nnY = ynn.ToArray();
            tileXML[i].npZ = znp.ToArray();
            tileXML[i].nnZ = znn.ToArray();
        }
    }

    private bool ShouldConnect(string socketA, string socketB)
    {
        if (socketA.EndsWith('s'))
        {
            // Symmetrical
            return socketA.Equals(socketB);
        } else
        {
            return (socketA + 'f') == socketB || (socketB + 'f') == socketA;
        }
    }

    private int ComputeTileCount()
    {
        int count = 0;
        foreach (TileEntry t in Tiles)
        {
            switch (t.Symmetry)
            {
                case Symmetry.T:
                    count += 4;
                    break;
                case Symmetry.L:
                    count += 4;
                    break;
                case Symmetry.I:
                    count += 2;
                    break;
                case Symmetry.D:
                    count += 2;
                    break;
                default:
                    count++;
                    break;
            }
        }
        return count;
    }

    private SocketDirection GetNext(SocketDirection d)
    {
        switch (d)
        {
            case SocketDirection.Left:
                return SocketDirection.Forward;
            case SocketDirection.Right:
                return SocketDirection.Back;
            case SocketDirection.Forward:
                return SocketDirection.Right;
            case SocketDirection.Back:
                return SocketDirection.Left;
            default:
                return SocketDirection.Left;
        }
    }

    private int GetCardinality(Symmetry symmetry)
    {
        switch (symmetry)
        {
            case Symmetry.T:
                return 4;
            case Symmetry.L:
                return 4;
            case Symmetry.I:
                return 2;
            case Symmetry.D:
                return 2;
            default: return 1;
        }
    }
}





