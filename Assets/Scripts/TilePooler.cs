

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class TilePooler : MonoBehaviour
{
    public int initialPoolSize;
    public int maximumPoolSize;

    private List<ObjectPool<GameObject>> tilePools = new List<ObjectPool<GameObject>>();

    public void CreateTilePools(List<TileType> tileTypes)
    {
        foreach (TileType tileType in tileTypes)
        {
            if (tileType.id == -1 || tilePools.Count > tileType.id) continue;

            ObjectPool<GameObject> tilePool = new ObjectPool<GameObject>(() =>
            {
                return Instantiate(tileType.tileObject);
            }, obj =>
            {
                obj.SetActive(true);
            }, obj =>
            {
                obj.SetActive(false);
            }, obj =>
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }, false, initialPoolSize, maximumPoolSize);
            tilePools.Add(tilePool);
        }
    }

    public GameObject SpawnTile(int tileId, Vector3Int position, Quaternion rotation, Vector3Int tileScaling)
    {
        GameObject tile = tilePools[tileId].Get();
        tile.transform.position = position;
        tile.transform.rotation = rotation;
        tile.transform.parent = transform;
        tile.transform.localScale = tileScaling;
        return tile;
    }

    public void DespawnTile(int tileId, GameObject tile)
    {
        tilePools[tileId].Release(tile);
    }
}