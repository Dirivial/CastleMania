using UnityEngine;

public abstract class Manager : MonoBehaviour
{

    public abstract void DestroyChunk(Vector2Int chunkPos);
    public abstract void CreateChunk(Vector2Int chunkPos);
}