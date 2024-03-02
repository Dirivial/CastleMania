using System.Linq;
using UnityEngine;

[System.Serializable]
public class TileType
{
    public string name;
    public int id;
    public Quaternion rotation;
    public float weight;
    public bool grounded; // If the tile needs to be on the ground
    public bool mustConnect; // If the tile needs to have at least one its connections satisfied
    public bool noRepeatH;
    public bool noRepeatV;
    public bool isTowerType;

    public GameObject tileObject;

    public bool[][] neighbors;
    public bool[] hasConnection;

    public TileType(string name, int rotation, float weight, GameObject tileObject)
    {
        this.rotation = Quaternion.Euler(new Vector3(-90, 0, 90 * rotation)); // This should be made more easily changed
        this.name = name;
        this.weight = weight;
        this.tileObject = tileObject;

        grounded = false; // If the tile needs to be on the ground
        mustConnect = false; // If the tile needs to have at least one its connections satisfied
        noRepeatH = false;
        noRepeatV = false;
        isTowerType = false;

        neighbors = new bool[6][];
        hasConnection = new bool[6];
    }

    public void SetNeighbors(Direction d, bool[] neighbors)
    {
        this.neighbors[(int)d] = neighbors;
        if (neighbors.Any(v => v == true))
        {
            //Debug.Log(this.name + " has connection to " + d + " with " + neighbors.Length + " tiles.");
            hasConnection[(int)d] = true;
        } else
        {
            hasConnection[(int)d] = false;
        }
    }
}

