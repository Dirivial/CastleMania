using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public struct DirAndCon
{
    public Direction direction;
    public int connection;
}

public struct Dir
{
    public string side; 
    public List<string> neighbors;
}

[System.Serializable]
public class TileType
{
    public string tileMesh;
    public string name;

    public string PosX;
    public string PosY;
    public string PosZ;
    public string NegX;
    public string NegY; 
    public string NegZ;

    public string Constrain_to;
    public string Constrain_from;

    public int rotation;
    public float weight = 1.0f;

    public List<Dir> validNeighbors;

    public TileType()
    {
        this.rotation = 0;
        validNeighbors = new List<Dir>();
        this.name = string.Empty;
        this.weight = 1.0f;
    }

    public TileType(string tileMesh, string name, Symmetry symmetry, 
        int rotation, float weight)
    {
        this.rotation = rotation;
        this.tileMesh = tileMesh;
        this.name = name;
        this.weight = weight;
    }
}

