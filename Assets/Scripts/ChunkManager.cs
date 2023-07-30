

using System.Collections.Generic;
using UnityEngine;

public class ChunkManager: MonoBehaviour
{
    public Transform playerTransform;

    public int chunkSize = 10;
    public int chunkCount = 3;
    public WFCManager wfcManager;

    private Vector2Int currentPlayerChunk;

    private void Start()
    {
        wfcManager.UpdateChunks(new Vector2Int(0, 0), chunkCount);
    }

    private void Update()
    {
        // Calculate the player's current chunk position
        Vector2Int playerChunk = new Vector2Int(
            Mathf.FloorToInt(playerTransform.position.x / chunkSize),
            Mathf.FloorToInt(playerTransform.position.z / chunkSize)
        );

        // If the player has moved to a new chunk, update the chunks
        if (playerChunk != currentPlayerChunk)
        {
            Debug.Log(playerTransform.position + " in chunk " + playerChunk);
            currentPlayerChunk = playerChunk;
            UpdateChunks();
        }
    }

    private void UpdateChunks()
    {
        Debug.Log("Yo");
        wfcManager.UpdateChunks(currentPlayerChunk, chunkCount);
    }
}