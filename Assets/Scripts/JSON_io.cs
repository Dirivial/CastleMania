using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

public enum SocketDirection
{
    Left, Right, Top, Bottom
}


[Serializable]
public class TileEntry
{
    public string Name;
    public string MeshName;
    public Mesh Mesh;
    public Symmetry Symmetry;

    public TileEntry()
    {
        Name = string.Empty;
        MeshName = string.Empty;
        Mesh = null;
        Symmetry = Symmetry.X;
    }

    public TileEntry(string name, string meshName, Mesh mesh, Symmetry symmetry)
    {
        Name = name;
        MeshName = meshName;
        Mesh = mesh;
        Symmetry = symmetry;
    }
}

[Serializable]
public class PrototypeXML
{
    // General information
    public string Name;
    public string Mesh;

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

    public PrototypeXML(string name, string mesh) {
        this.Name = name;
        this.Mesh = mesh;
    }
}

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObject/JSON_IO", order = 1)]
public class JSON_io : ScriptableObject
{
    public string jsonPath = "models";
    public List<TileEntry> Tiles;

    private MeshSockets Sockets = new MeshSockets();

    public void Export()
    {
        Debug.Log("Mobamba");
        Sockets.ClearList();
        if (jsonPath != null && jsonPath.Length > 0 && Tiles.Count > 0)
        {

            int totalTileCount = ComputeTileCount();

            PrototypeXML[] tileXML = new PrototypeXML[totalTileCount];

            int k = 0;

            // Compute the sockets
            for (int i = 0; i < Tiles.Count; i++)
            {
                tileXML[k] = new PrototypeXML(Tiles[i].Name + "_0", Tiles[i].MeshName);
                List<string> sockets = Sockets.ComputeMeshSockets(Tiles[i].Mesh);

                int cardinality = GetCardinality(Tiles[i].Symmetry);

                // Create rotated versions !!NOTE!! The tiles/models I am using has Z as the vertical axis
                for (int j = 1; j < cardinality; j++)
                {
                    tileXML[k + j] = new PrototypeXML(Tiles[i].Name + "_" + j, Tiles[i].MeshName);
                }

                // Assign sockets
                AssignSockets(sockets, tileXML, k, Tiles[i].Symmetry);
                k += cardinality;
            }

            // Compute neighbors with the earlier assigned sockets
            ComputeNeighbors(tileXML);

            // Create XmlSerializer
            XmlSerializer serializer = new XmlSerializer(typeof(PrototypeXML[]));

            // Specify the file path
            string filePath = Application.dataPath + "/" + jsonPath + ".xml";

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

    private void AssignSockets(List<string> sockets, PrototypeXML[] tileXML, int index, Symmetry symmetry)
    {

        // Assign the first sockets
        tileXML[index].posX = sockets[0];
        tileXML[index].negX = sockets[1];
        tileXML[index].posY = sockets[2];
        tileXML[index].negY = sockets[3];
        tileXML[index].posZ = sockets[4];
        tileXML[index].negZ = sockets[5];

        string PZ = sockets[4];
        string NZ = sockets[5];

        // This is to make th
        SocketDirection[] socketDirections = new SocketDirection[4] { 0, SocketDirection.Right, SocketDirection.Top, SocketDirection.Bottom };

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
                tileXML[i].posY = sockets[(int)socketDirections[2]];
                tileXML[i].negY = sockets[(int)socketDirections[3]];

                // These should always remain the same
                tileXML[i].posZ = PZ;
                tileXML[i].negZ = NZ;
            }
            return;
        } else if (cardinality == 2)
        {
            for (int j = 0; j < socketDirections.Length; j++)
            {
                socketDirections[j] = GetNext(GetNext(socketDirections[j]));
            }
            tileXML[index + 1].posX = sockets[(int)socketDirections[0]];
            tileXML[index + 1].negX = sockets[(int)socketDirections[1]];
            tileXML[index + 1].posY = sockets[(int)socketDirections[2]];
            tileXML[index + 1].negY = sockets[(int)socketDirections[3]];

            tileXML[index + 1].posZ = PZ;
            tileXML[index + 1].negZ = NZ;
        } 

        return;
    }

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
                if (tileXML[i].posX == tileXML[j].negX)
                {
                    xnp.Add(tileXML[j].Name);
                }
                // Negative X
                if (tileXML[i].negX == tileXML[j].posX)
                {
                    xnn.Add(tileXML[j].Name);
                }

                // Positive Y
                if (tileXML[i].posY == tileXML[j].negY)
                {
                    ynp.Add(tileXML[j].Name);
                }
                // Negative Y
                if (tileXML[i].negY == tileXML[j].posY)
                {
                    ynn.Add(tileXML[j].Name);
                }

                // Positive Z
                if (tileXML[i].posZ == tileXML[j].negZ)
                {
                    znp.Add(tileXML[j].Name);
                }
                // Negative Z
                if (tileXML[i].negZ == tileXML[j].posZ)
                {
                    znn.Add(tileXML[j].Name);
                }
            }

            // This might not makes sense for you, check if your models get fricked up
            tileXML[i].npX = xnp.ToArray();
            tileXML[i].nnX = xnn.ToArray();
            tileXML[i].npY = znp.ToArray();
            tileXML[i].nnY = znn.ToArray();
            tileXML[i].npZ = ynn.ToArray();
            tileXML[i].nnZ = ynp.ToArray();
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
        switch (d) // 0 -> 3 -> 1 -> 2 -> 0
        {
            case SocketDirection.Left:
                return SocketDirection.Bottom;
            case SocketDirection.Right:
                return SocketDirection.Top;
            case SocketDirection.Top:
                return SocketDirection.Left;
            case SocketDirection.Bottom:
                return SocketDirection.Right;
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





