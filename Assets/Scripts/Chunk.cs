
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using Unity.VisualScripting;

[BurstCompile]
public class Chunk
{
    public Vector2Int position;
    public bool isInstantiated;

    // Remember to deallocate
    public NativeArray<int> tileMap;
    public NativeArray<bool> tileMapArray;
    public NativeList<Vector3Int> tilesToProcess;

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