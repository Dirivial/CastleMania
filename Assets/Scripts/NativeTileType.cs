using System.Linq;
using Unity.Collections;
using UnityEngine;
using Unity.Burst;

[BurstCompile]
public struct NativeTileType
{
    public float weight;
    public bool grounded; // If the tile needs to be on the ground
    public bool mustConnect; // If the tile needs to have at least one its connections satisfied
    public bool noRepeatH; // If the tile cannot repeat horizontally
    public bool noRepeatV; // If the tile cannot repeat vertically
    public bool isTowerTile; // If the tile is a tower tile

    public NativeTileType(float weight)
    {
        this.weight = weight;

        grounded = false;
        mustConnect = false;
        noRepeatH = false;
        noRepeatV = false;
        isTowerTile = false;
    }
}

