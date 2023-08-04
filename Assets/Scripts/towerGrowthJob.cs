using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

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

    public TowerGrowthJob(NativeList<TowerTile> towers, NativeArray<int> heights, Vector3Int dimensions, int tower_id, int tower_bottom_id, int tower_top_id, int tower_window_id, int tilesBelowBottomFloor)
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
    }

    public void Execute()
    {
        for (int i = 0; i < tilesToProcess; i++)
        {
            ProcessTile(towers[i]);
        }
        Debug.Log("From " + tilesToProcess + ". Generated " + (towers.Length - tilesToProcess) + " tower tiles");
    }

    private void ProcessTile(TowerTile towerTile)
    {
        int index = towerTile.tileId;
        if (index == tower_bottom)
        {
            HandleBottomTower(towerTile);
        }
        else if (index == tower)
        {
            /*
            // Grow tower to fill gaps between floors
            if (y == dimensions.y - 1 || tileMap[ConvertTo1D(x, y + 1, z)] == EMPTY_TILE)
            {
                Interval interval = ComputeTowerGrowth(y);

                chunks[position].tiles.Add(InstantiateTile(tower_top, a, interval.end, b));

                for (int i = interval.start; i < interval.end; i++)
                {
                    chunks[position].tiles.Add(InstantiateTile(tower, a, i, b));
                }
            }
            else
            {
                for (int i = 1; i < height; i++)
                {
                    chunks[position].tiles.Add(InstantiateTile(index, a, i + height, b));
                }
            }
            */
        }
        else if (index == tower_top)
        {
            /*
            // Set current tile to be a normal tower tile
            tileMap[convertedTileIndex] = tower;

            Interval interval = ComputeTowerGrowth(y);

            chunks[position].tiles.Add(InstantiateTile(tower_top, a, interval.end, b));

            for (int i = interval.start; i < interval.end; i++)
            {
                chunks[position].tiles.Add(InstantiateTile(tower, a, i, b));
            }
            */
        }
    }

    private void FillBetween(Vector3Int posFrom, Vector3Int posTo)
    {

    }

    private void HandleBottomTower(TowerTile towerTile)
    {
        if (dimensions.y > 1)
        {
            int foundTileAbove = -1;
            for (int i = 0; i < tilesToProcess;i++)
            {
                if (towers[i].position.x == towerTile.position.x && towers[i].position.y == towerTile.position.y + 1 && towers[i].position.z == towerTile.position.z)
                {
                    foundTileAbove = i; break;
                }
            }
            if (foundTileAbove != -1)
            {

            } else
            {

            }
            
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

    /*


    
    private Interval ComputeTowerGrowth(int y)
    {
        Interval interval = new Interval();

        // Get a number to grow to
        float f = Random.Range(0.0f, 1.0f);
        float h = towerGrowth.Evaluate(f);
        int height_i = Mathf.RoundToInt(h) + 1;

        int start = floorHeights[y] + 1; //floorHeights[y] + 1;
        int end = start + height_i; //floorHeights[y] + height_i;

        // Make sure that the growth does not lead to poking through floors
        // If we are at the top, there is no need to worry about breaking through floors
        //if (y != dimensions.y - 1 && floorHeights[y + 1] < end) end = floorHeights[y + 1] - 1;
        if (y != dimensions.y - 1 && floorHeights[(y + 1)] < end) end = start;

        interval.start = start;
        interval.end = end;

        return interval;
    }
    */
}