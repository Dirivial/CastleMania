using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct Socket
{
    public string id;
    public Vector2[] vertices;

    public Socket(string id, Vector2[] vertices)
    {
        this.id = id;
        this.vertices = vertices;
    }
}

public class MeshSockets
{

    private List<Socket> sockets = new List<Socket>();

    public void ImportDictionaryEntries()
    {
        // Todo
    }

    public void ClearList()
    {
        sockets.Clear();
    }

    public List<string> ComputeMeshSocketsFromPath(string pathToMesh)
    {
        return new List<string>(); // Todo
    }

    public List<string> ComputeMeshSockets(Mesh mesh)
    {
        int mult = 100;
        List<string> meshSockets = new List<string>();
        Vector3[] vertices = mesh.vertices;

        float minX = 100;
        float maxX = -100;
        float minY = 100;
        float maxY = -100;
        float minZ = 100;
        float maxZ = -100;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i] * mult;
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

        List<Vector2> posX = new List<Vector2>();
        List<Vector2> negX = new List<Vector2>();
        List<Vector2> posY = new List<Vector2>();
        List<Vector2> negY = new List<Vector2>();
        List<Vector2> posZ = new List<Vector2>();
        List<Vector2> negZ = new List<Vector2>();

        // Store the vertices for each face in a list
        for (int i = 0; i < vertices.Length; i++)
        {
            if (i > 0 && vertices[i].Equals(vertices[i-1]))
            {
                continue;
            }
            float x = vertices[i].x; //Mathf.Round(v.x);
            float y = vertices[i].y; //Mathf.Round(v.y);
            float z = vertices[i].z; //Mathf.Round(v.z);
            if (x == maxX && !posX.Contains(new Vector2(y, z)))
            {
                posX.Add(new Vector2(y, z));
            } else if (x == minX && !negX.Contains(new Vector2(y, z)))
            {
                negX.Add(new Vector2(y, z));
            }

            if (y == maxY && !posY.Contains(new Vector2(x, z)))
            {
                posY.Add(new Vector2(x, z));
            }
            else if (y == minY && !negY.Contains(new Vector2(x, z)))
            {
                negY.Add(new Vector2(x, z));
            }

            if (z == maxZ && !posZ.Contains(new Vector2(x, y)))
            {
                posZ.Add(new Vector2(x, y));
            }
            else if (z == minZ && !negZ.Contains(new Vector2(x, y)))
            {
                negZ.Add(new Vector2(x, y));
            }
        }

        //Debug.Log(posX.Count + ", " + negX.Count + ", " + posY.Count + ", " + negY.Count + ", " + posZ.Count + ", " + negZ.Count);

        // If the face is valid, store it as a socket

        if (Math.Round(maxX) == 1)
        {
            string id = SearchForID(posX);            

            if (id == String.Empty)
            {
                //Debug.Log("Adding on X");
                id = GenerateID();
                sockets.Add(new Socket(id, posX.ToArray()));
            }

            meshSockets.Add(id);
        } else
        {
            meshSockets.Add("-1");
        }


        if (Math.Round(minX) == -1)
        {
            string id = SearchForID(negX);

            if (id == String.Empty)
            {
                //Debug.Log("Adding on -X");
                id = GenerateID();
                sockets.Add(new Socket(id, negX.ToArray()));
            }
            meshSockets.Add(id);
        }
        else
        {
            meshSockets.Add("-1");
        }

        if (Math.Round(maxY) == 1)
        {
            string id = SearchForID(posY);

            if (id == String.Empty)
            {
                //Debug.Log("Adding on Y");
                id = GenerateID();
                sockets.Add(new Socket(id, posY.ToArray()));
            }
            meshSockets.Add(id);
        }
        else
        {
            meshSockets.Add("-1");
        }

        if (Math.Round(minY) == -1)
        {
            string id = SearchForID(negY);

            if (id == String.Empty)
            { 
                //Debug.Log("Adding on -Y");
                id = GenerateID();
                sockets.Add(new Socket(id, negY.ToArray()));
            }
            meshSockets.Add(id);
        }
        else
        {
            meshSockets.Add("-1");
        }

        if (Math.Round(maxZ) == 1)
        {
            string id = SearchForID(posZ);

            if (id == String.Empty)
            {
                //Debug.Log("Adding on Z");
                id = GenerateID();
                sockets.Add(new Socket(id, posZ.ToArray()));
            }
            meshSockets.Add(id);
        }
        else
        {
            meshSockets.Add("-1");
        }
        if (Math.Round(minZ) == -1)
        {
            string id = SearchForID(negZ);

            if (id == String.Empty)
            {
                //Debug.Log("Adding on -Z");
                id = GenerateID();
                sockets.Add(new Socket(id, negZ.ToArray()));
            }
            meshSockets.Add(id);
        }
        else
        {
            meshSockets.Add("-1");
        }

        return meshSockets;
    }

    private string SearchForID(List<Vector2> vertices)
    {
        string id = String.Empty;

        for (int i = 0; i < sockets.Count; i++)
        {
            bool isIdentical = true;

            // Search for any deviation, if there is none we have already stored the same face of vertices
            if (vertices.Count == sockets[i].vertices.Length)
            {
                foreach (Vector2 v in sockets[i].vertices)
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
/*        foreach (Socket s in sockets)
        {
            Debug.Log(s.id + " n# v:" + s.vertices.Length);
        }*/
    }

    private string GenerateID()
    {
        if (sockets.Count == 0)
        {
            return "0";
        }

        string lastId = sockets.Count > 0 ? sockets[sockets.Count - 1].id : "0";
        string number = String.Empty;
        foreach (char c in lastId.ToCharArray())
        {
            if (c >= '0' && c <= '9')
            {
                number = string.Concat(number, c);
            }
            else
            {
                break;
            }
        }
        int id = Int32.Parse(number);
        id += 1;
        return id.ToString();
    }

}
