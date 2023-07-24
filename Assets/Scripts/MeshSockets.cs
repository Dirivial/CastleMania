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
        int mult = 10000; // Change to suit your mesh size

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
            } else if (x == minX && !negX.Contains(new Vector2Int(z, y)))
            {
                negX.Add(new Vector2Int(z, y));
            }

            if (y == maxY && !posY.Contains(new Vector2Int(x, z)))
            {
                posY.Add(new Vector2Int(x, z));
            }
            else if (y == minY && !negY.Contains(new Vector2Int(x, z)))
            {
                negY.Add(new Vector2Int(x, z));
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
                    sockets.Add(new Socket(id + 'f', CreateFlipped(posX, false, true).ToArray()));
                } else
                {
                    Debug.Log("Symmetrical");
                    sockets.Add(new Socket(id + 's', posX.ToArray()));
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
                    sockets.Add(new Socket(id + 's', negX.ToArray()));
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
                    sockets.Add(new Socket(id + 's', posZ.ToArray()));
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
                    sockets.Add(new Socket(id + 's', negZ.ToArray()));
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
                id = GenerateID(true);
                //Debug.Log("Adding on Y: " + id);
                sockets.Add(new Socket(id+ "_0", posY.ToArray()));
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
                
                id = GenerateID(true);
                //Debug.Log("Adding on -Y: " + id);
                sockets.Add(new Socket(id + "_0", negY.ToArray()));
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
                    if (!vertices.Contains(v))
                    {
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
            if(!vertices.Contains(new Vector2Int(Y ? -v.x : v.x, X ? -v.y : v.y)))
            {
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
