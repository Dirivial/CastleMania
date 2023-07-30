using System.Linq;
using Unity.Collections;
using UnityEngine;

[System.Serializable]
public struct NativeTileType
{
    public float weight;
    public bool grounded; // If the tile needs to be on the ground
    public bool mustConnect; // If the tile needs to have at least one its connections satisfied
    public bool noRepeatH;
    public bool noRepeatV;

    public NativeTileType(float weight)
    {
        this.weight = weight;

        grounded = false; // If the tile needs to be on the ground
        mustConnect = false; // If the tile needs to have at least one its connections satisfied
        noRepeatH = false;
        noRepeatV = false;
    }
}

