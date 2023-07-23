using UnityEngine;

[System.Serializable]
public class TileType
{
    public string tileMesh;
    public string name;

    public string Constrain_to;
    public string Constrain_from;

    public Quaternion rotation;
    public float weight = 1.0f;

    public GameObject tileObject;

    public string[][] neighbors;
    public bool[] hasConnection;

    public TileType()
    {
        rotation = Quaternion.identity; // Might need to change this
        neighbors = new string[6][];
        name = string.Empty;
        weight = 0.5f;
        tileObject = null;
        hasConnection = new bool[6];
    }

    public TileType(string name, int rotation, float weight, GameObject tileObject)
    {
        this.rotation = Quaternion.Euler(new Vector3(-90, 0, 90 * rotation)); // This should be made more easily changed
        this.name = name;
        this.weight = weight;
        this.tileObject = tileObject;

        neighbors = new string[6][];
        hasConnection = new bool[6];
    }

    public void SetNeighbors(Direction d, string[] neighbors)
    {
        this.neighbors[(int)d] = neighbors;
        if (neighbors.Length > 0)
        {
            //Debug.Log(this.name + " has connection to " + d + " with " + neighbors.Length + " tiles.");
            hasConnection[(int)d] = true;
        } else
        {
            hasConnection[(int)d] = false;
        }
    }
}

