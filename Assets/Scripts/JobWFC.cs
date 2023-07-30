
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct JobWFC: IJob
{

	public Vector2Int position;

	// Pain
	private NativeArray<int> tileMap;
	private NativeArray<bool> tileMapArray;
	private NativeArray<NativeTileType> tileTypes;
	private NativeArray<bool> neighborData;
	private NativeArray<bool> hasConnectionData;
	private Vector3Int dimensions;
	private int tileCount;

	private static int UNDECIDED = -1;
	private static int EMPTY_TILE = -2;
	//private int currentTileToProcess;
	private NativeList<Vector3Int> tilesToProcess;
    private Unity.Mathematics.Random random;

	public JobWFC(Vector2Int position, Vector3Int dimensions, 
		NativeArray<NativeTileType> tileTypes, NativeArray<bool> tileMapArray, int tileCount, 
		NativeArray<int> tileMap, NativeArray<bool> neighborData, 
		NativeArray<bool> hasConnectionData, NativeList<Vector3Int> tilesToProcess)
	{
		this.position = position;
		this.dimensions = dimensions;
		this.tileTypes = tileTypes;
        this.tileCount = tileCount;
		this.neighborData = neighborData;
		this.hasConnectionData = hasConnectionData;
        this.tilesToProcess = tilesToProcess;
        this.tileMap = tileMap;
        this.tileMapArray = tileMapArray;

        //currentTileToProcess = 0;

        random = new Unity.Mathematics.Random(1);

		for (int x = 0; x < dimensions.x; x++)
		{
			for (int z = 0; z < dimensions.z; z++)
			{
				for (int y = 0; y < dimensions.y; y++)
				{
					// Set each tile to be undecided
					this.tileMap[ConvertTo1D(x, y, z)] = UNDECIDED;

					// Set each boolean value to true to represent the positions ability to
					// be the corresponding tile in the tileTypes list
					for (int i = 0; i < tileCount; i++)
					{
                        tileMapArray[TileMapArrayCoord(x, y, z, i)] = true;
					}
				}
			}
		}
	}

	public void Execute()
	{
		int maxIterationsLeft = dimensions.x * dimensions.y * dimensions.z;
		Vector3Int nextWave = FindLowestEntropy();

		// Run while there are still tiles to collapse
		while (nextWave.x != -1 && nextWave.y != -1 && maxIterationsLeft > 0)
		{
			PickTileAt(nextWave);
			ProcessTiles();
			nextWave = FindLowestEntropy();
			maxIterationsLeft--;
		}

		Debug.Log("I have completed my job");
	}

	// Convert coordinates to singular coordinate for tile map
	private int ConvertTo1D(int x, int y, int z)
	{
		return x + dimensions.x * (y + dimensions.y * z);
	}

	// Convert to coordinate to use for the tile map array
	private int TileMapArrayCoord(int x, int y, int z, int i) {
        return x + dimensions.x * (y + dimensions.y * (z + dimensions.z * i));
    }

	// Check if the given tile has a connection to some direction
	private bool HasConnection(int index, Direction direction)
	{
		return hasConnectionData[index + ((int)direction * tileCount)];
	}

	// Check two tiles are valid neighbors
	private bool IsNeighbor(int index, Direction direction, int otherIndex) {
		return neighborData[index + tileCount * ((int)direction + 6 * otherIndex)];
	}


	// Find the first tile with the lowest entropy
	private Vector3Int FindLowestEntropy()
	{
		// Look for the lowest entropy in the tile map
		float lowestEntropy = tileCount + 1;
		List<Vector3Int> possiblePositions = new List<Vector3Int>();
		Vector3Int lowestEntropyPosition = new Vector3Int(-1, -1, -1);
		for (int x = 0; x < dimensions.x; x++)
		{
			for (int z = 0; z < dimensions.z; z++)
			{
				for (int y = 0; y < dimensions.y; y++)
				{
					if (tileMap[ConvertTo1D(x, y, z)] != UNDECIDED) continue;
					possiblePositions.Add(new Vector3Int(x, y, z));

					float entropy = GetEntropy(new Vector3Int(x, y, z));

					if (entropy < lowestEntropy && entropy > 0)
					{
						lowestEntropy = entropy;
						lowestEntropyPosition = new Vector3Int(x, y, z);
					}
				}
			}
		}

		if (lowestEntropy != tileCount + 1) return lowestEntropyPosition;

		return possiblePositions.Count > 0 ? possiblePositions[random.NextInt(0, possiblePositions.Count - 1)] : new Vector3Int(-1, -1, -1);
	}

	// Get the entropy of a tile
	private float GetEntropy(Vector3Int pos)
	{
		float entropy = 0;
		for (int i = 0; i < tileCount; i++)
		{
			entropy += tileMapArray[TileMapArrayCoord(pos.x, pos.y, pos.z, i)] ? tileTypes[i].weight * Mathf.Log10(tileTypes[i].weight) : 0;
		}

		return -1 * entropy;
	}

	// Pick a tile at the given tile position
	private void PickTileAt(Vector3Int pos)
	{
		tileMap[ConvertTo1D(pos.x, pos.y, pos.z)] = ChooseTileTypeAt(pos.x, pos.y, pos.z);

        for (int i = 0; i < tileCount; i++)
		{
            tileMapArray[TileMapArrayCoord(pos.x, pos.y, pos.z, i)] = false;
		}

		tilesToProcess.Add(pos);
	}

	// Process the tiles that were have been but in the tilesToProcess stack. Returns list of coordinates for tiles that have been set
	private List<Vector3Int> ProcessTiles()
	{
		int maxIterations = 1000;
		int i = 0;
		List<Vector3Int> setTiles = new List<Vector3Int>();
		while (tilesToProcess.Length > 0 && maxIterations > i)
		{
			Vector3Int tilePosition = tilesToProcess[tilesToProcess.Length - 1];
			tilesToProcess.RemoveAt(tilesToProcess.Length - 1);

			if (tileMap[ConvertTo1D(tilePosition.x, tilePosition.y, tilePosition.z)] == UNDECIDED)
			{
				int numTrue = 0;
				for (int j = 0; j < tileCount; j++)
				{
					if (tileMapArray[TileMapArrayCoord(tilePosition.x, tilePosition.y, tilePosition.z, j)])
					{
						numTrue++;
					}
				}

				if (numTrue == 1)
				{
					Debug.Log("GAMING");
					int chosenTile = ChooseTileTypeAt(tilePosition.x, tilePosition.y, tilePosition.z);

					tileMap[ConvertTo1D(tilePosition.x, tilePosition.y, tilePosition.z)] = chosenTile;
					UpdateNeighbors(tilePosition);
					setTiles.Add(tilePosition);

					// We have a single chunk type, so we can set it
					for (int j = tileCount - 1; j >= 0; j--)
					{
                        tileMapArray[TileMapArrayCoord(tilePosition.x, tilePosition.y, tilePosition.z, j)] = false;
					}
				}
				else if (numTrue == 0)
				{
					tileMap[ConvertTo1D(tilePosition.x, tilePosition.y, tilePosition.z)] = EMPTY_TILE;
					UpdateNeighbors(tilePosition);
				}
			}
			else
			{
				UpdateNeighbors(tilePosition);
				setTiles.Add(tilePosition);
			}
			i++;
		}
		return setTiles;
	}

	// Update the neighbors of the given chunk position
	private void UpdateNeighbors(Vector3Int tilePosition)
	{
		for (Direction i = 0; i <= Direction.Down; i++)
		{
			Vector3Int neighborPosition = tilePosition;
			switch (i)
			{
				case Direction.North:
					neighborPosition.z += 1;
					break;
				case Direction.South:
					neighborPosition.z -= 1;
					break;
				case Direction.East:
					neighborPosition.x += 1;
					break;
				case Direction.West:
					neighborPosition.x -= 1;
					break;
				case Direction.Down:
					neighborPosition.y -= 1;
					break;
				case Direction.Up:
					neighborPosition.y += 1;
					break;
			}

			if (neighborPosition.x >= 0 && neighborPosition.x < dimensions.x &&
				neighborPosition.y >= 0 && neighborPosition.y < dimensions.y &&
				neighborPosition.z >= 0 && neighborPosition.z < dimensions.z)
			{

				if (tileMap[ConvertTo1D(neighborPosition.x, neighborPosition.y, neighborPosition.z)] == UNDECIDED)
				{
					// See if there is a connection from the current tile to the neighbor
					int tileIndex = tileMap[ConvertTo1D(tilePosition.x, tilePosition.y, tilePosition.z)];
					bool found = false;

					if (tileIndex > UNDECIDED)
					{
						bool originConnection = !HasConnection(tileIndex, i);

						// Remove all possible tiles from neighbor
						if (originConnection)
						{
							for (int j = 0; j < tileCount; j++)
							{
								if (tileMapArray[TileMapArrayCoord(neighborPosition.x, neighborPosition.y, neighborPosition.z, j)])
								{
									// Get opposite direction
									int opposite = (int)i % 2 == 0 ? (int)i + 1 : (int)i - 1;

									if (HasConnection(j, (Direction)opposite))
									{
                                        tileMapArray[TileMapArrayCoord(neighborPosition.x, neighborPosition.y, neighborPosition.z, j)] = false;
										found = true;
									}
								}
							}
						}
						else
						{
							// Remove the possible tiles of the neighbor that are not in the current tiles possible neighbors
							for (int j = 0; j < tileCount; j++)
							{
								if (tileMapArray[TileMapArrayCoord(neighborPosition.x, neighborPosition.y, neighborPosition.z, j)])
								{
									if (!IsNeighbor(tileIndex, i, j))
									{
                                        tileMapArray[TileMapArrayCoord(neighborPosition.x, neighborPosition.y, neighborPosition.z, j)] = false;
										found = true;
									}
								}
							}
						}
					}
					else if (tileIndex == EMPTY_TILE)
					{
						// Tile is empty, so remove tiles that have a connection to this position
						for (int j = 0; j < tileCount; j++)
						{
							if (tileMapArray[TileMapArrayCoord(neighborPosition.x, neighborPosition.y, neighborPosition.z, j)])
							{
								// Get opposite direction
								int opposite = (int)i % 2 == 0 ? (int)i + 1 : (int)i - 1;

								if (HasConnection(j, (Direction)opposite))
								{
                                    tileMapArray[TileMapArrayCoord(neighborPosition.x, neighborPosition.y, neighborPosition.z, j)] = false;
									found = true;
								}
							}
						}
					}

					if (found)
					{
						tilesToProcess.Add(neighborPosition);
					}
				}
			}
		}
	}
	private int ChooseTileTypeAt(int x, int y, int z)
	{
		//Debug.Log("Position: " + x + " , " + y + ", " + z);
		List<int> choices = new List<int>(); // All possible choices

		for (int i = 0; i < tileCount; i++)
		{
			if (tileMapArray[TileMapArrayCoord(x, y, z, i)])
			{
				//Debug.Log("Trying tile: " + tileTypes[i].name);
				if (HasPossibleConnection(x, y, z, i) && EnforceCustomConstraints(x, y, z, i))
				{
					choices.Add(i);
				}
			}
		}

		// Choose a random tile type from the list of possible tile types
		if (choices.Count > 0)
		{
			return ChooseWithWeights(choices);
		}
		else
		{
			//Debug.Log("No possible tile " + new Vector3Int(x, y, z));
			return EMPTY_TILE;
		}
	}

    // If you do not have any custom constraints, just return true here or remove the function
    private bool EnforceCustomConstraints(int x, int y, int z, int i)
    {
        if (tileTypes[i].grounded && y != 0)
        {
            return false;
        }

		/*
        if (tileTypes[i].mustConnect && !CanConnect(x, y, z, i))
        {
            return false;
        } */

        if (tileTypes[i].noRepeatH)
        {
            if (x > 0 && tileMap[ConvertTo1D(x - 1, y, z)] == i) { return false; }
            if (x < dimensions.x - 1 && tileMap[ConvertTo1D(x + 1, y, z)] == i) { return false; }
            if (z > 0 && tileMap[ConvertTo1D(x, y, z - 1)] == i) { return false; }
            if (z < dimensions.z - 1 && tileMap[ConvertTo1D(x, y, z + 1)] == i) { return false; }
        }

        if (tileTypes[i].noRepeatV)
        {
            if (y > 0 && tileMap[ConvertTo1D(x, y - 1, z)] == i) { return false; }
            if (y < dimensions.y - 1 && tileMap[ConvertTo1D(x, y + 1, z)] == i) { return false; }
        }

        return true;
    }

    private int ChooseWithWeights(List<int> indices)
	{
		float cumulativeSum = 0.0f;
		float[] cumulativeWeights = new float[indices.Count];

		for (int i = 0; i < indices.Count; i++)
		{
			cumulativeSum += tileTypes[indices[i]].weight;
			cumulativeWeights[i] = cumulativeSum;
		}

		float r = random.NextFloat(0, cumulativeSum);

		int index = Array.BinarySearch(cumulativeWeights, r);
		if (index < 0) index = ~index;

		return indices[index];
	}

	private bool HasPossibleConnection(int x, int y, int z, int i)
	{
		// If the current tile has a connection to an empty tile (as in there can be no tile there), return false

		if (HasConnection(i, Direction.West))
		{
			if (x == 0 || tileMap[ConvertTo1D(x - 1, y, z)] == EMPTY_TILE)
			{
				//Debug.Log("Failed West.");
				return false;
			}
		}

		if (HasConnection(i, Direction.East))
		{
			if (x == dimensions.x - 1 || tileMap[ConvertTo1D(x + 1, y, z)] == EMPTY_TILE)
			{
				//Debug.Log("Failed East.");
				return false;
			}
		}

		if (HasConnection(i, Direction.South))
		{
			if (z == 0 || tileMap[ConvertTo1D(x, y, z - 1)] == EMPTY_TILE)
			{
				//Debug.Log("Failed South.");
				return false;
			}
		}

		if (HasConnection(i, Direction.North))
		{
			if (z == dimensions.z - 1 || tileMap[ConvertTo1D(x, y, z + 1)] == EMPTY_TILE)
			{
				//Debug.Log("Failed North.");
				return false;
			}
		}

		if (HasConnection(i, Direction.Down) && (y == 0 || tileMap[ConvertTo1D(x, y - 1, z)] == EMPTY_TILE))
		{
			//Debug.Log("Failed Down.");
			return false;
		}

		if (HasConnection(i, Direction.Up) && (y == dimensions.y - 1 || tileMap[ConvertTo1D(x, y + 1, z)] == EMPTY_TILE))
		{
			//Debug.Log("Failed Up.");
			return false;
		}

		//Debug.Log("Passed");
		return true;
	}

	/*
    private bool CanConnectTo(int x, int y, int z, bool[] neighbors)
    {
        for (int i = 0; i < neighbors.Length; i++)
        {
            if (neighbors[i] && tileMap[x, y, z] == i) return true;
        }
        return false;
    }

    private bool CanConnect(int x, int y, int z, int i)
    {

        if (HasConnection(i, Direction.West))
        {
            if (x > 0 && CanConnectTo(x - 1, y, z, neighborData[(int)Direction.West]))
            {
                return true;
            }
        }

        if (HasConnection(i, Direction.East))
        {
            if (x < dimensions.x - 1 && CanConnectTo(x + 1, y, z, tileTypes[i].neighbors[(int)Direction.East]))
            {
                //Debug.Log("Failed East.");
                return true;
            }
        }

        if (HasConnection(i, Direction.South))
        {
            if (z > 0 && CanConnectTo(x, y, z - 1, tileTypes[i].neighbors[(int)Direction.South]))
            {
                //Debug.Log("Failed South.");
                return true;
            }
        }

        if (HasConnection(i, Direction.North))
        {
            if (z < dimensions.z - 1 && CanConnectTo(x, y, z + 1, tileTypes[i].neighbors[(int)Direction.North]))
            {
                //Debug.Log("Failed North.");
                return true;
            }
        }

        if (HasConnection(i, Direction.Down) && (y > 0 && CanConnectTo(x, y - 1, z, tileTypes[i].neighbors[(int)Direction.Down])))
        {
            //Debug.Log("Failed Down.");
            return true;
        }

        if (HasConnection(i, Direction.Up) && (y < dimensions.y - 1 && CanConnectTo(x, y + 1, z, tileTypes[i].neighbors[(int)Direction.Up])))
        {
            //Debug.Log("Failed Up.");
            return true;
        }
        return false;
    } */
}