using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileGang : MonoBehaviour
{
    /*
    private void ComputeRotations()
    {
        List<TileType> newTileTypes = new List<TileType>();
        foreach (TileType tileType in tileTypes)
        {
            tileType.UpdateConnections();
            Symmetry sym = tileType.symmetry;
            Vector3 r = tileType.rotation.eulerAngles;
            r.x = Mathf.RoundToInt(r.x);
            r.y = Mathf.RoundToInt(r.y);
            r.z = Mathf.RoundToInt(r.z);

            switch (sym)
            {
                case Symmetry.L:
                    for (int i = 1; i < 4; i++)
                    {
                        TileType tile = new TileType(tileType.tileObject, tileType.name + " " + i, tileType.symmetry, 
                            Quaternion.Euler(r.x, r.y + 90 * i, r.z), tileType.connections, tileType.weight,
                            tileType.CanTouchGround, tileType.CanRepeatH, tileType.CanRepeatV, tileType.MustStandOn, tileType.MustConnect);
                        List<int> directions = RotateAll(tile.connections, i);

                        tile.ClearConnections();

                        for (int j = 0; j < directions.Count; j += 2)
                        {
                            tile.AddConnection((Direction)directions[j], directions[j + 1]);
                        }
                        newTileTypes.Add(tile);
                    }
                    break;
                case Symmetry.T:
                    for (int i = 1; i < 4; i++)
                    {
                        TileType tile = new TileType(tileType.tileObject, tileType.name + " " + i, tileType.symmetry, 
                            Quaternion.Euler(r.x, r.y + 90 * i, r.z), tileType.connections, tileType.weight,
                            tileType.CanTouchGround, tileType.CanRepeatH, tileType.CanRepeatV, tileType.MustStandOn, tileType.MustConnect);
                        List<int> directions = RotateAll(tile.connections, i);

                        tile.ClearConnections();

                        for (int j = 0; j < directions.Count; j += 2)
                        {
                            tile.AddConnection((Direction)directions[j], directions[j + 1]);
                        }
                        newTileTypes.Add(tile);
                    }
                    break;
                case Symmetry.I:
                    TileType my_tile = new TileType(tileType.tileObject, tileType.name + " " + 1, tileType.symmetry, 
                        Quaternion.Euler(r.x, r.y + 90, r.z), tileType.connections, tileType.weight,
                        tileType.CanTouchGround, tileType.CanRepeatH, tileType.CanRepeatV, tileType.MustStandOn, tileType.MustConnect);
                    List<int> dirs = RotateAll(my_tile.connections, 1);

                    my_tile.ClearConnections();

                    for (int j = 0; j < dirs.Count; j += 2)
                    {
                        my_tile.AddConnection((Direction)dirs[j], dirs[j + 1]);
                    }
                    newTileTypes.Add(my_tile);
                    break;
                case Symmetry.D:
                    // Todo, implement this shiz
                    break;
                default:
                    // No rotations / reflections
                    break;
            }
        }
        // Save the new tile types
        tileTypes.AddRange(newTileTypes);
    }
    */

    // Rotate all
    private List<int> RotateAll(int[] connections, int i)
    {
        List<int> directions = new List<int>();
        for (int j = 0; j < 4; j++)
        {
            directions.Add((int)RotateClockwise((Direction)j, i));
            directions.Add(connections[j]);
        }

        directions.Add((int)Direction.Up);
        directions.Add(connections[4]);
        directions.Add((int)Direction.Down);
        directions.Add(connections[5]);

        return directions;
    }


    // Rotate a direction clockwise, X times
    private Direction RotateClockwise(Direction dir, int times)
    {
        Direction direction;
        switch (dir)
        {
            case Direction.North:
                direction = Direction.East;
                break;
            case Direction.East:
                direction = Direction.South;
                break;
            case Direction.South:
                direction = Direction.West;
                break;
            case Direction.West:
                direction = Direction.North;
                break;
            default:
                Debug.LogError("Invalid direction passed");
                return Direction.North;
        }
        if (times > 1) return RotateClockwise(direction, times - 1);
        else return direction;
    }
}
