
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using System.Collections.Generic;

public class ChunkWFC
{
    public Vector2Int position;
    public bool isInstantiated;

    public List<GameObject> tiles = new List<GameObject>();
    public List<GameObject> towerTiles = new List<GameObject>();

    // Remember to deallocate
    public NativeArray<int> tileMap;
    public NativeList<TowerTile> towers;

    public NativeArray<int> outNorth;
    public NativeArray<int> outSouth;
    public NativeArray<int> outEast;
    public NativeArray<int> outWest;

    // Job information
    public JobWFC jobWFC;
    public JobHandle jobHandle;
}