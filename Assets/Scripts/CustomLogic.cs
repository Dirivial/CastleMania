using System.Collections.Generic;
using UnityEngine;

public class CustomLogic: MonoBehaviour
{

    private int tower_top;
    private int tower_bottom;
    private int tower_normal;

    private Vector3Int dimensions;

    public void SaveImportantTiles(List<TileType> tileTypes)
    {
        for (int i = 0; i < tileTypes.Count; i++)
        {
            if (tileTypes[i].name.Equals("tower_0"))
            {
                tower_normal = i;
            }
            else if (tileTypes[i].name.Equals("tower_top_0"))
            {
                tower_top = i;
            }
            else if (tileTypes[i].name.Equals("tower_bottom_0"))
            {
                tower_bottom = i;
            }
        }
    }

    public void SetDimenions(Vector3Int dimensions)
    {
        this.dimensions = dimensions;
    }

    
    
}