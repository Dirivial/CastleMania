

using System.Collections.Generic;
using UnityEngine;

public class ChunkManager: MonoBehaviour
{
    public Transform playerTransform;

    public int chunkSize = 128;
    public int chunkSpawnDistance = 3;
    public int chunkDestroyDistance = 5;
    
    
    private WFCManager wfcManager;
    private LavaManager lavaManager;
    private Vector2Int currentPlayerChunk;
    private Dictionary<Vector2Int, bool> chunks = new Dictionary<Vector2Int, bool>();

    public void Awake()
    {
        wfcManager = GetComponent<WFCManager>();
        wfcManager.TilesPerChunk = chunkSize;
        wfcManager.Setup();

        lavaManager = GetComponent<LavaManager>();
    }

    private void Start()
    {
        UpdateChunks();
    }

    private void Update()
    {
        // Calculate the player's current chunk position
        Vector2Int playerChunk = new Vector2Int(
            Mathf.FloorToInt((playerTransform.position.x) / chunkSize),
            Mathf.FloorToInt((playerTransform.position.z - chunkSize / 2) / chunkSize)
        );

        // If the player has moved to a new chunk, update the chunks
        if (playerChunk != currentPlayerChunk)
        {
            //Debug.Log(playerTransform.position + " in chunk " + playerChunk);
            currentPlayerChunk = playerChunk;
            UpdateChunks();
        }
    }

    private void UpdateChunks()
    {
        // Loop through the chunks surrounding the player and load/unload as needed
        for (int x = currentPlayerChunk.x - chunkSpawnDistance; x <= currentPlayerChunk.x + chunkSpawnDistance; x++)
        {
            for (int y = currentPlayerChunk.y - 1; y <= currentPlayerChunk.y + chunkSpawnDistance; y++)
            {
                Vector2Int chunkPos = new Vector2Int(x, y);

                // Check if the chunk is already loaded
                if (!chunks.ContainsKey(chunkPos))
                {
                    chunks.Add(chunkPos, true);
                    wfcManager.CreateChunk(chunkPos);
                    lavaManager.CreateChunk(chunkPos);
                }
            }
        }

        // Unload chunks that are too far from the player
        List<Vector2Int> chunksToRemove = new List<Vector2Int>();

        foreach (Vector2Int chunk in chunks.Keys)
        {
            if (Mathf.Abs(chunk.x - currentPlayerChunk.x) > chunkDestroyDistance || Mathf.Abs(chunk.y - currentPlayerChunk.y) > chunkDestroyDistance)
            {
                chunksToRemove.Add(chunk);
                wfcManager.DestroyChunk(chunk);
                lavaManager.DestroyChunk(chunk);
            }
        }

        for (int i = chunksToRemove.Count-1; i >= 0; i--)
        {
            chunks.Remove(chunksToRemove[i]);
        }
    }
}