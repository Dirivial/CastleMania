

using JetBrains.Annotations;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UIElements;

public struct JobAndData
{
    public JobWFC jobWFC;
    public NativeArray<int> tileMap;

    public JobAndData(JobWFC jobWFC, NativeArray<int> tileMap)
    {
        this.jobWFC = jobWFC;
        this.tileMap = tileMap;
    }
}

public class WFCManager : MonoBehaviour
{
    public Vector3Int dimensions = new Vector3Int(5, 5, 5);
    public int numberOfJobs = 1;
    public XML_IO XML_IO;

    private Dictionary<Vector2Int, JobAndData> positionToJob;

    private List<JobHandle> jobHandles = new List<JobHandle>();
    List<TileType> imported_tiles;
    private NativeArray<NativeTileType> tileTypes;
    private NativeArray<bool> tileMapArray;
    private NativeArray<bool> neighborData;
    private NativeArray<bool> hasConnectionData;
    private NativeList<Vector3Int> tilesToProcess;
    private int tileCount;

    private bool view = true;

    private void Awake()
    {
        XML_IO.ClearTileTypes();
        XML_IO.Import();
        imported_tiles = XML_IO.GetTileTypes();
        tileCount = imported_tiles.Count;
        tileTypes = new NativeArray<NativeTileType>(tileCount, Allocator.Persistent);
        neighborData = new NativeArray<bool>(tileCount * tileCount * 6, Allocator.Persistent); // (Number of tiles (n# depends) ^ 2) * directions (6)
        hasConnectionData = new NativeArray<bool>(tileCount * 6, Allocator.Persistent); // Number of tiles (n# depends) * directions (6)
        tileMapArray = new NativeArray<bool>(dimensions.x * dimensions.y * dimensions.z * tileCount, Allocator.Persistent); // 6 for directions
        tilesToProcess = new NativeList<Vector3Int>(0, Allocator.Persistent);

        // Create NativeTileTypes
        int i = 0;
        foreach (TileType tileType in imported_tiles)
        {
            NativeTileType tile = new NativeTileType(tileType.weight);

            ComputeNeighborData(tileType, i);
            ComputeHasConnection(tileType, i);

            tileTypes[i] = tile;
            i++;
        }

        positionToJob = new Dictionary<Vector2Int, JobAndData> ();

        // Create jobs
        for (i = 0; i < numberOfJobs; i++)
        {
            Vector2Int position = new Vector2Int(i, 0);
            NativeArray<int> tileMap = new NativeArray<int> (dimensions.x * dimensions.y * dimensions.z, Allocator.Persistent);
            JobWFC job = new JobWFC(position, dimensions, tileTypes, tileMapArray, tileCount, tileMap, neighborData, hasConnectionData, tilesToProcess);
            positionToJob[position] = new JobAndData(job, tileMap);
        }
    }

    public void Start()
    {
        Debug.Log("Starting Jobs");

        for (int i = 0; i < numberOfJobs; i++)
        {
            Vector2Int position = new Vector2Int(i, 0);
            JobWFC job = positionToJob[position].jobWFC;
            jobHandles.Add(job.Schedule());
        }

        Debug.Log("Done");
    }

    public void LateUpdate()
    {
        foreach (JobHandle handle in jobHandles)
        {
            handle.Complete();
            if (handle.IsCompleted && view)
            {
                InstantiateTiles();
                view = false;
            }
        }
    }

    private void OnDestroy()
    {
        tileTypes.Dispose();
        tilesToProcess.Dispose();
        neighborData.Dispose();
        hasConnectionData.Dispose();
        positionToJob[new Vector2Int(0, 0)].tileMap.Dispose();
        tileMapArray.Dispose();
    }

    private int ConvertTo1D(int x, int y, int z)
    {
        return x + dimensions.x * (y + dimensions.y * z);
    }

    private void InstantiateTiles()
    {
        NativeArray<int> tileMap = positionToJob[new Vector2Int(0, 0)].tileMap;
        Vector3Int tileScaling = new Vector3Int(400, 400, 400);
        int tileSize = 8;
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    int index = tileMap[ConvertTo1D(x, y, z)];
                    if (index >= 0 && imported_tiles[index].name != "-1")
                    {
                        int height = y;//floorHeights[y];

                        GameObject obj = Instantiate(imported_tiles[index].tileObject, new Vector3(x * tileSize, height * tileSize, z * tileSize), imported_tiles[index].rotation);
                        obj.transform.localScale = tileScaling;
                        obj.transform.parent = transform;
                        //instantiatedTiles.Add(obj);
                    }
                }
            }
        }
    }


    private void ComputeNeighborData(TileType tileType, int index)
    {
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < tileCount; j++)
            {
                neighborData[index + tileCount * (i + 6 * j)] = tileType.neighbors[i][j];
            }
        }
    }

    private void ComputeHasConnection(TileType tileType, int index)
    {
        bool anyPositive = false;
        for (int i = 0;i < 6; i++)
        {
            for (int j = 0; j < tileCount; j++)
            {
                if (tileType.neighbors[i][j])
                {
                    hasConnectionData[index + tileCount * i] = true;
                    anyPositive = true;
                    break;
                }
            }
            if (!anyPositive)
            {
                hasConnectionData[index + tileCount * i] = false;
            }
            anyPositive = false;
        }
    }
}