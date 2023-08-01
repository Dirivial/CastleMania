
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using System.Collections.Generic;

public class ChunkWFC
{
    public Vector2Int position;
    public bool isInstantiated;

    public List<GameObject> tiles = new List<GameObject>();

    // Remember to deallocate
    public NativeArray<int> tileMap;
    public NativeArray<JobHandle> dependencies;

    // Job information
    public JobWFC jobWFC;
    public JobHandle jobHandle;

    public void SetJobHandle(JobHandle jobHandle)
    {
        this.jobHandle = jobHandle;
    }

    public void SetIsInstantiated(bool isInstantiated)
    {
        this.isInstantiated = isInstantiated;
    }
}