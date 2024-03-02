using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public struct Socket
{
    public string id;
    public Vector2Int[] vertices;

    public Socket(string id, Vector2Int[] vertices)
    {
        this.id = id;
        this.vertices = vertices;
    }
}

public class MeshSockets
{
    private List<Socket> sockets = new List<Socket>();
    private string lastSocketH = string.Empty;
    private string lastSocketV = string.Empty;

    public void ClearList()
    {
        sockets.Clear();
        lastSocketH = string.Empty;
        lastSocketV = string.Empty;
    }

    // Comptues sockets for a mesh
    // Note that ignoreSide should contain a value for each direction (North, South, East, West, Up, Down) 
    public List<string> ComputeMeshSockets(Mesh mesh, Vector3Int meshRotation, bool[] ignoreSide)
    {
        int mult = 100; // Change to suit your mesh size

        List<string> meshSockets = new List<string>();
        Vector3[] verts = mesh.vertices;
        Vector3Int[] vertices = new Vector3Int[verts.Length]; 

        int minX = 1000;
        int maxX = -1000;
        int minY = 1000;
        int maxY = -1000;
        int minZ = 1000;
        int maxZ = -1000;

        for (int i = 0; i < verts.Length; i++)
        {
            
            Vector3 vf = verts[i] * mult;
            vf = Quaternion.Euler(meshRotation.x, meshRotation.y, meshRotation.z) * vf;
            Vector3Int v = new Vector3Int(Mathf.RoundToInt(vf.x), Mathf.RoundToInt(vf.y), Mathf.RoundToInt(vf.z));
            //Debug.Log(v);
            vertices[i] = v;
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x ;
            if (v.y  < minY) minY = v.y ;
            if (v.y  > maxY) maxY = v.y ;
            if (v.z  < minZ) minZ = v.z ;
            if (v.z  > maxZ) maxZ = v.z ;

        }

        //Debug.Log(maxX + ", " + minX + ", " + maxY + ", " + minY + ", " + maxZ + ", " + minZ);

        List<Vector2Int> posX = new List<Vector2Int>();
        List<Vector2Int> negX = new List<Vector2Int>();
        List<Vector2Int> posY = new List<Vector2Int>();
        List<Vector2Int> negY = new List<Vector2Int>();
        List<Vector2Int> posZ = new List<Vector2Int>();
        List<Vector2Int> negZ = new List<Vector2Int>();

        // Store the vertices for each face in a list
        for (int i = 0; i < vertices.Length; i++)
        {
            if (i > 0 && vertices[i].Equals(vertices[i-1]))
            {
                continue;
            }
            int x = vertices[i].x;
            int y = vertices[i].y;
            int z = vertices[i].z;

            if (x == maxX && !posX.Contains(new Vector2Int(z, y)))
            {
                posX.Add(new Vector2Int(z, y));
            } else if (x == minX && !negX.Contains(new Vector2Int(-1* z, y)))
            {
                negX.Add(new Vector2Int(-1 * z, y));
            }

            if (y == maxY && !posY.Contains(new Vector2Int(x, z)))
            {
                posY.Add(new Vector2Int(x, z));
            }
            else if (y == minY && !negY.Contains(new Vector2Int(-1 * x, z)))
            {
                negY.Add(new Vector2Int(-1 * x, z));
            }

            if (z == maxZ && !posZ.Contains(new Vector2Int(x, y)))
            {
                posZ.Add(new Vector2Int(x, y));
            }
            else if (z == minZ && !negZ.Contains(new Vector2Int(x, y)))
            {
                negZ.Add(new Vector2Int(x, y));
            }
        }

        //Debug.Log(posX.Count + ", " + negX.Count + ", " + posY.Count + ", " + negY.Count + ", " + posZ.Count + ", " + negZ.Count);

        // The following could be shortened quite a bit...

        // ---- If the face is valid, store it as a socket ----
        if (!ignoreSide[(int)Direction.East] && maxX == 100)
        {
            string id = SearchForID(posX, false);            

            if (id == String.Empty)
            {
                //Debug.Log("Adding on X");
                id = GenerateID(false);

                if (!IsSymmetrical(posX, false, true))
                {
                    Debug.Log("Not Symmetrical");
                    sockets.Add(new Socket(id, posX.ToArray()));
                    List<Vector2Int> flipped = CreateFlipped(posX, false, true);
                    sockets.Add(new Socket(id + 'f', flipped.ToArray()));

                    string original = "";
                    string flip = "";
                    for (int i = 0; i < posX.Count; i++)
                    {
                        original += posX[i].ToString();
                        flip += flipped[i].ToString();
                    }
                    Debug.Log("Original: " + original);
                    Debug.Log("Flipped: " + flip);

                } else
                {
                    Debug.Log("Symmetrical");
                    id = id + 's';
                    sockets.Add(new Socket(id, posX.ToArray()));
                }
            }

            meshSockets.Add(id);
        } else
        {
            meshSockets.Add("-1");
        }

        if (!ignoreSide[(int)Direction.West] && minX == -100)
        {
            string id = SearchForID(negX, false);

            if (id == String.Empty)
            {
                //Debug.Log("Adding on -X");
                id = GenerateID(false);
                
                if (!IsSymmetrical(negX, false, true))
                {
                    Debug.Log("Not Symmetrical");
                    sockets.Add(new Socket(id, negX.ToArray()));
                    sockets.Add(new Socket(id + 'f', CreateFlipped(negX, false, true).ToArray()));
                }
                else
                {
                    Debug.Log("Symmetrical");
                    id = id + 's';
                    sockets.Add(new Socket(id, negX.ToArray()));
                }
            }
            meshSockets.Add(id);
        }
        else
        {
            meshSockets.Add("-1");
        }

        if (!ignoreSide[(int)Direction.North] && maxZ == 100)
        {
            string id = SearchForID(posZ, false);

            if (id == String.Empty)
            {
                //Debug.Log("Adding on Z");
                id = GenerateID(false);
                
                if (!IsSymmetrical(posZ, false, true))
                {
                    Debug.Log("Not Symmetrical");
                    sockets.Add(new Socket(id, posZ.ToArray()));
                    sockets.Add(new Socket(id + 'f', CreateFlipped(posZ, false, true).ToArray()));
                }
                else
                {
                    Debug.Log("Symmetrical");
                    id = id + 's';
                    sockets.Add(new Socket(id, posZ.ToArray()));
                }
            }
            meshSockets.Add(id);
        }
        else
        {
            meshSockets.Add("-1");
        }

        if (!ignoreSide[(int)Direction.South] && minZ == -100)
        {
            string id = SearchForID(negZ, false);

            if (id == String.Empty)
            {
                //Debug.Log("Adding on -Z");
                id = GenerateID(false);

                if (!IsSymmetrical(negZ, false, true))
                {
                    Debug.Log("Not Symmetrical");
                    sockets.Add(new Socket(id, negZ.ToArray()));
                    sockets.Add(new Socket(id + 'f', CreateFlipped(negZ, false, true).ToArray()));
                }
                else
                {
                    Debug.Log("Symmetrical");
                    id = id + 's';
                    sockets.Add(new Socket(id, negZ.ToArray()));
                }
            }
            meshSockets.Add(id);
        }
        else
        {
            meshSockets.Add("-1");
        }

        // Remember that vertical sockets need special names AND that vertical
        // sockets should be rotated to produce a total of 4 versions
        if (!ignoreSide[(int)Direction.Up] && maxY == 100)
        {
            string id = SearchForID(posY, true);

            if (id == String.Empty)
            {
                id = GenerateID(true) + "_0";
                Debug.Log("Adding on Y: " + id);
                sockets.Add(new Socket(id, posY.ToArray()));
                for (int i = 0; i < posY.Count; i++)
                {
                    Debug.Log(posY[i]);
                }
                GenerateRotated(posY, id);
            }
            meshSockets.Add(id);
        }
        else
        {
            meshSockets.Add("-1");
        }

        if (!ignoreSide[(int)Direction.Down] && minY == -100)
        {
            string id = SearchForID(negY, true);

            if (id == String.Empty)
            { 
                
                id = GenerateID(true) + "_0";
                Debug.Log("Adding on -Y: " + id);
                sockets.Add(new Socket(id, negY.ToArray()));
                for (int i = 0; i < negY.Count; i++)
                {
                    Debug.Log(negY[i]);
                }
                GenerateRotated(negY, id);
            }
            meshSockets.Add(id);
        }
        else
        {
            meshSockets.Add("-1");
        }

        return meshSockets;
    }

    // Go through all sockets to see if one of them has a matching set of vertices
    private string SearchForID(List<Vector2Int> vertices, bool vertical)
    {
        string id = String.Empty;

        for (int i = 0; i < sockets.Count; i++)
        {
            bool isIdentical = true;

            // Search for any deviation, if there is none we have already stored the same face of vertices
            if (vertices.Count == sockets[i].vertices.Length)
            {
                if (vertical)
                {
                    // Ignore non-vertical sockets when looking for vertical ones
                    if (sockets[i].id[0] != 'v') continue;
                } else
                {
                    // Ignore vertical sockets when looking for horizontal ones
                    if (sockets[i].id[0] == 'v') continue;
                }
                
                foreach (Vector2Int v in sockets[i].vertices)
                {
                    bool foundSimilar = false;
                    int smallestDiffX = 100;
                    int smallestDiffY = 100;
                    foreach (Vector2Int v2 in vertices)
                    {
                        Vector2Int difference = new Vector2Int(Math.Abs(v.x - v2.x), Math.Abs(v.y - v2.y));

                        if (difference.x < smallestDiffX) smallestDiffX = difference.x;
                        if (difference.y < smallestDiffY) smallestDiffY = difference.y;

                        if (difference.x < 2 && difference.y < 2)
                        {
                            foundSimilar = true;
                            break;
                        }
                    }
                    if (!foundSimilar)
                    {
                        Debug.Log("Did not contain: " + v + " smallest difference: " + smallestDiffX + ", " + smallestDiffY);
                        isIdentical = false;
                        break;
                    }
                }
            } else
            {
                isIdentical = false;
            }

            // Face of vertices has already been stored
            if (isIdentical)
            {
                id = sockets[i].id;
                break;
            }
        }
        return id;
    }

    public void PrintSockets()
    {
        Debug.Log("Number of sockets: " + sockets.Count);
        foreach (Socket s in sockets)
        {
            Debug.Log(s.id + " n# v:" + s.vertices.Length);
        }
    }

    // Generate an ID
    private string GenerateID(bool vertical)
    {
        
        if (vertical)
        {
            if (lastSocketV.Equals(String.Empty))
            {
                // Vertical sockets should have rotated versions as well
                lastSocketV = "v0";
                return lastSocketV;
            }

            string number = String.Empty;
            foreach (char c in lastSocketV.ToCharArray())
            {
                if (c >= '0' && c <= '9')
                {
                    number = string.Concat(number, c);
                }
                else if (c == 'v')
                {
                    continue;
                } else
                {
                    break;
                }
            }
            int lastId = Int32.Parse(number) + 1;
            lastSocketV = "v" + lastId.ToString();
            return lastSocketV;
        } else
        {
            if (lastSocketH.Equals(String.Empty))
            {
                lastSocketH = "0";
                return "0";
            }

            int lastId = Int32.Parse(lastSocketH) + 1;
            lastSocketH = lastId.ToString();

            return lastSocketH;
        }
    }

    // Checks if the list of vertices are symmetrical
    private bool IsSymmetrical(List<Vector2Int> vertices, bool X, bool Y)
    {
        bool symmetrical = true;
        foreach (Vector2Int v in vertices) {
            Vector2Int rotated = new Vector2Int(Y ? -v.x : v.x, X ? -v.y : v.y);
            bool found = false;

            foreach (Vector2Int v2 in vertices)
            {
                /*
                for (int i = -1; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        if (v2.x + i == rotated.x && v2.y + j == rotated.y)
                        {
                            found = true;
                        } 
                    }
                    
                }
                if (found) break;
                */
                Vector2Int difference = new Vector2Int(Math.Abs(v2.x - rotated.x), Math.Abs(v2.y - rotated.y));
                if (difference.x < 3 && difference.y < 3)
                {
                    //if (Y) { Debug.Log(-v.x + " " + v.y + " diff: " + difference); }
                    found = true;
                    break;
                }
            }
            if (!found) {
                Debug.Log("Rotated version of " + rotated + " had no equivalent ");
                symmetrical = false;
                break;
            }
        }

        return symmetrical;
    }

    private List<Vector2Int> CreateFlipped(List<Vector2Int> vertices, bool X, bool Y)
    {
        List<Vector2Int> flipped = new List<Vector2Int>();
        foreach (Vector2Int v in vertices)
        {
            flipped.Add(new Vector2Int(Y ? -v.x : v.x, X ? -v.y : v.y));
        }
        return flipped;
    }

    // When creating a vertical socket we want to rotate the socket vertices to see if there are any other versions
    private void GenerateRotated(List<Vector2Int> vertices, string id)
    {   
        if (!IsSymmetrical(vertices, true, false))
        {
            // Generate flipped
            sockets.Add(new Socket(id + "_1", CreateFlipped(vertices, true, false).ToArray()));
        }

        if (!IsSymmetrical(vertices, false, true))
        {
            // Generate flipped
            sockets.Add(new Socket(id + "_2", CreateFlipped(vertices, false, true).ToArray()));
        }

        if (!IsSymmetrical(vertices, true, true))
        {
            // Generate flipped
            sockets.Add(new Socket(id + "_3", CreateFlipped(vertices, true, true).ToArray()));
        }
    }

}
