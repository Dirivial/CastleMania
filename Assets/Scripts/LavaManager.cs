using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class LavaManager : Manager
{
    
    public GameObject lavaPrefab;
    public int lavaHeight = -32;
    
    private Dictionary<Vector2Int, GameObject> lavaChunks = new Dictionary<Vector2Int, GameObject>();
    private int chunkSize = 128;

    public override void CreateChunk(Vector2Int chunkPos)
    {
        GameObject gameObject = Instantiate(lavaPrefab, new Vector3Int(chunkPos.x * chunkSize, lavaHeight, chunkPos.y * chunkSize), Quaternion.Euler(-90, 0, 0));
        lavaChunks.Add(chunkPos, gameObject);
    }

    public override void DestroyChunk(Vector2Int chunkPos)
    {
        Destroy(lavaChunks[chunkPos]);
        lavaChunks.Remove(chunkPos);
    }
}
