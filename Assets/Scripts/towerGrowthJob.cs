using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public struct Interval
{
    public int start;
    public int end;
}

//[BurstCompile]
public struct TowerGrowthJob: IJob
{

    public NativeList<TowerTile> towers;

    public NativeArray<int> heights;

    private Vector3Int dimensions;
    private int tower;
    private int tower_bottom;
    private int tower_top;
    private int tower_window;

    private int tilesToProcess;
    private int negativeHeight;
    private Unity.Mathematics.Random random;

    public TowerGrowthJob(NativeList<TowerTile> towers, NativeArray<int> heights, Vector3Int dimensions, int tower_id, int tower_bottom_id, int tower_top_id, int tower_window_id, int tilesBelowBottomFloor, uint seed)
    {
        this.towers = towers;
        this.heights = heights;
        this.dimensions = dimensions;
        tower = tower_id;
        tower_bottom = tower_bottom_id;
        tower_top = tower_top_id;
        tower_window = tower_window_id;
        tilesToProcess = towers.Length;
        negativeHeight = -1 * tilesBelowBottomFloor;

        random = new Unity.Mathematics.Random(seed);
    }

    public void Execute()
    {
        for (int i = 0; i < tilesToProcess; i++)
        {
            ProcessTile(towers[i]);
        }
        for (int i = 0; i < tilesToProcess; i++)
        {
            TowerTile t = towers[i];
            t.position = new Vector3Int(t.position.x, heights[t.position.y], t.position.z);
            towers[i] = t;
        }
    }

    private void ProcessTile(TowerTile towerTile)
    {
        int index = towerTile.tileId;
        if (index == tower_bottom)
        {
            HandleBottomTower(towerTile);
        }
        else if (index == tower_top)
        {
            GenerateTop(towerTile.position);
        } else
        {
            HandleTower(towerTile);
        }
    }

    private void HandleTower(TowerTile towerTile)
    {
        if (towerTile.position.y != dimensions.y - 1)
        {
            // Fill gap if there is a tile above
            int tileAbove = FindTileAbove(towerTile.position);
            if (tileAbove != -1)
            {
                FillBetween(towerTile.position, towers[tileAbove].position);
                return; // We do not want to generate a top because we have already filled a gap, so therefore exit the function here
            }
        }
        // Generate the top of the tower
        GenerateTop(towerTile.position);
    }

    private void FillBetween(Vector3Int posFrom, Vector3Int posTo)
    {
        // Connect this bottom tower tile with the next floor
        for (int i = heights[posFrom.y] + 1; i < heights[posTo.y]; i++)
        {
            TowerTile t = new TowerTile();
            t.tileId = random.NextBool() ? tower : tower_window;
            t.position = new Vector3Int(posTo.x, i, posTo.z);
            towers.Add(t);
        }
    }

    private int FindTileAbove(Vector3Int pos)
    {
        for (int i = 0; i < tilesToProcess; i++)
        {
            if (towers[i].position.x == pos.x && towers[i].position.y == pos.y + 1 && towers[i].position.z == pos.z)
            {
                return i;
            }
        }
        return -1;
    }

    private void HandleBottomTower(TowerTile towerTile)
    {

        if (dimensions.y > 1)
        {
            int foundTileAbove = FindTileAbove(towerTile.position);

            if (foundTileAbove != -1)
            {
                FillBetween(towerTile.position, towers[foundTileAbove].position);
            } else
            {
                GenerateTop(towerTile.position);
            }
        } else
        {
            GenerateTop(towerTile.position);
        }

        // Put a couple of extra tower pieces in there to avoid starting at the same floor as all of the other tiles
        for (int i = -1; i >= negativeHeight; i--)
        {
            TowerTile t = new TowerTile();
            t.tileId = tower;
            t.position = new Vector3Int(towerTile.position.x, i, towerTile.position.z);
            towers.Add(t);
        }
    }

    private void GenerateTop(Vector3Int position)
    {
        Interval interval = ComputeTowerGrowth(position.y);

        // Insert the head of the tower
        TowerTile t = new TowerTile();
        t.tileId = tower_top;
        t.position = new Vector3Int(position.x, interval.end, position.z);
        towers.Add(t);

        // Fill the tiles from the original position to the head of the tower
        for (int i = interval.start; i < interval.end; i++)
        {
            t = new TowerTile();
            t.tileId = tower;
            t.position = new Vector3Int(position.x, i, position.z);
            towers.Add(t);
        }
    }

    private Interval ComputeTowerGrowth(int y)
    {
        Interval interval = new Interval();

        // Get a number to grow to
        int end = y != dimensions.y - 1 ? random.NextInt(heights[y] + 1, heights[y + 1]) : random.NextInt(heights[y] + 1, heights[y] + 4);
        int start = heights[y] + 1;

        // Make sure that the growth does not lead to poking through floors
        // If we are at the top, there is no need to worry about breaking through floors
        //if (y != dimensions.y - 1 && floorHeights[y + 1] < end) end = floorHeights[y + 1] - 1;
        if (y != dimensions.y - 1 && heights[(y + 1)] < end) end = start;

        interval.start = start;
        interval.end = end;

        return interval;
    }
}