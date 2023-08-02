
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using System.Collections.Generic;

public class BufferZone
{
    public Vector2Int positionA;
    public Vector2Int positionB;
    private bool isInstantiated;
    private bool isHorizontal;

    public List<GameObject> tiles = new List<GameObject>();

    // Remember to deallocate
    public NativeArray<int> tileMap;

    // Job information
    public BufferJob bufferJob;
    public JobHandle jobHandle;

    public bool IsInstantiated { get => isInstantiated; set => isInstantiated = value; }
    public bool IsHorizontal { get => isHorizontal; set => isHorizontal = value; }

    public void SetJobHandle(JobHandle jobHandle)
    {
        this.jobHandle = jobHandle;
    }

    public void SetIsInstantiated(bool isInstantiated)
    {
        this.IsInstantiated = isInstantiated;
    }
}