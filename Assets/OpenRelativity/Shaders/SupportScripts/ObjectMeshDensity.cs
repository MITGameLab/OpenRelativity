using UnityEngine;
using System.Collections.Generic;

namespace OpenRelativity
{
    public class ObjectMeshDensity : MonoBehaviour
    {

        //Need these three lists to create a new set of each variable needed for a new mesh
        List<int> newTriangles = new List<int>();
        List<Vector3> newVerts = new List<Vector3>();
        List<Vector2> newUV = new List<Vector2>();
        //state array contains information on splitting
        public bool state { get; set; }
        //These store the original and split mesh
        public Mesh change { get; set; }
        public Mesh original { get; set; }

        public ComputeShader colliderShader;


        //This constant determines triangle size. We subdivide meshes until all their triangles have less than this area.
        private float constant = 8;

        // Use this for initialization, before relativistic object CombineParent() starts.
        void Awake()
        {
            //Grab the meshfilter, and if it's not null, keep going
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                //Store a copy of our original mesh
                original = Instantiate(meshFilter.mesh);

                //Prepare a new mesh for our split mesh
                change = Instantiate(meshFilter.mesh);

                //Keep splitting this mesh until all of its triangles have area less than our chosen value
                bool isDone = true;
                int numberOfLoops = 0;
                while (isDone == true)
                {
                    isDone = Subdivide(change, meshFilter.transform);
                    ++numberOfLoops;

                }
                if (numberOfLoops == 1)
                {
                    change = null;
                }

            }
            else
            {
                original = null;
                change = null;
            }
        }


        //This function takes in information from RelativityModel, and returns either the split mesh or the original mesh, depending on the variable Subdivide
        public bool ReturnVerts(Mesh mesh, bool Subdivide)
        {
            //Check that there is actually a mesh here, not just a placeholder

            //Are we supposed to subdivide again?
            if (Subdivide)
            {
                //Set the state of the current mesh to split
                state = true;
                //Clear the mesh and replace it with the split mesh's values
                mesh.Clear();
                mesh.vertices = change.vertices;
                mesh.uv = change.uv;
                mesh.triangles = change.triangles;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                return true;

            }
            //If not, we must be here to revert the mesh
            else if (!Subdivide)
            {
                //set the state of the current mesh to unchanged
                state = false;

                //Clear the mesh and replace it with the original mesh's values
                mesh.Clear();
                mesh.vertices = original.vertices;
                mesh.uv = original.uv;
                mesh.triangles = original.triangles;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                return true;
            }

            return false;
        }

        //Debug function, prints entire list of triangles and their vertices
        void printTriangles(Vector3[] oVerts, int[] triangles)
        {
            print(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                print("triangle " + i + " " + oVerts[triangles[i]] + " " + oVerts[triangles[i + 1]] + " " + oVerts[triangles[i + 2]]);
            }
        }
        //Find the midpoint between two vectors
        Vector3 Mid(Vector3 one, Vector3 two)
        {
            return (one + two) / 2;
        }
        //Finds midpoint of two V2s
        Vector2 Mid2(Vector2 one, Vector2 two)
        {
            return (one + two) / 2;
        }

        //Just subdivide something
        //Using this for subdividing a mesh as far as we need to for its mesh triangle area to be less than our defined constant
        public bool Subdivide(Mesh mesh, Transform transform)
        {
            //Take in the UVs and Triangles
            bool change = false;
            Vector2[] oldUV = new Vector2[mesh.uv.Length];
            int[] oldTriangles = new int[mesh.triangles.Length];
            Vector3[] oldVerts = new Vector3[mesh.vertices.Length];
            System.Array.Copy(mesh.triangles, oldTriangles, mesh.triangles.Length);
            System.Array.Copy(mesh.uv, oldUV, mesh.uv.Length);
            System.Array.Copy(mesh.vertices, oldVerts, mesh.vertices.Length);

            int lastIndexAdded = 0; //Use this to keep track of the last vertex that you added to the list from each loop.
            int lastTriangleIndexAdded = 0; //Use this to keep track of the last vertex that you added to the list from each loop.

            //Loop through the triangles. If one has an area greater than our chosen constant, then subdivide it
            for (int i = 0; i < mesh.triangles.Length / 3; ++i)
            {
                //Get two edges, take their cross product, and divide the magnitude in half. That should give us the triangle's area.
                Vector3 dist1 = (transform.TransformPoint(mesh.vertices[mesh.triangles[lastIndexAdded]]) - transform.TransformPoint(mesh.vertices[mesh.triangles[lastIndexAdded + 1]]));
                Vector3 dist2 = (transform.TransformPoint(mesh.vertices[mesh.triangles[lastIndexAdded + 2]]) - transform.TransformPoint(mesh.vertices[mesh.triangles[lastIndexAdded + 1]]));
                float area = Vector3.Cross(dist2, dist1).magnitude / 2;
                //If that area is larger than our desired triangle size:
                if (area > constant)
                {
                    change = true;
                    //Add old verts
                    newVerts.Add(oldVerts[oldTriangles[lastIndexAdded]]);
                    newVerts.Add(oldVerts[oldTriangles[lastIndexAdded + 1]]);
                    newVerts.Add(oldVerts[oldTriangles[lastIndexAdded + 2]]);
                    //new ones
                    newVerts.Add(Mid(oldVerts[oldTriangles[lastIndexAdded]], oldVerts[oldTriangles[lastIndexAdded + 1]]));
                    newVerts.Add(Mid(oldVerts[oldTriangles[lastIndexAdded]], oldVerts[oldTriangles[lastIndexAdded + 2]]));
                    newVerts.Add(Mid(oldVerts[oldTriangles[lastIndexAdded + 1]], oldVerts[oldTriangles[lastIndexAdded + 2]]));

                    //Triangle 1
                    newTriangles.Add(lastTriangleIndexAdded);
                    newTriangles.Add(lastTriangleIndexAdded + 3);
                    newTriangles.Add(lastTriangleIndexAdded + 4);
                    //Triangle 2
                    newTriangles.Add(lastTriangleIndexAdded + 5);
                    newTriangles.Add(lastTriangleIndexAdded + 4);
                    newTriangles.Add(lastTriangleIndexAdded + 3);
                    //Triangle 3
                    newTriangles.Add(lastTriangleIndexAdded + 4);
                    newTriangles.Add(lastTriangleIndexAdded + 5);
                    newTriangles.Add(lastTriangleIndexAdded + 2);
                    //Triangle 4
                    newTriangles.Add(lastTriangleIndexAdded + 5);
                    newTriangles.Add(lastTriangleIndexAdded + 3);
                    newTriangles.Add(lastTriangleIndexAdded + 1);
                    //Add the new and old UV verts
                    newUV.Add(oldUV[oldTriangles[lastIndexAdded]]);
                    newUV.Add(oldUV[oldTriangles[lastIndexAdded + 1]]);
                    newUV.Add(oldUV[oldTriangles[lastIndexAdded + 2]]);
                    newUV.Add(Mid2(oldUV[oldTriangles[lastIndexAdded]], oldUV[oldTriangles[lastIndexAdded + 1]]));
                    newUV.Add(Mid2(oldUV[oldTriangles[lastIndexAdded]], oldUV[oldTriangles[lastIndexAdded + 2]]));
                    newUV.Add(Mid2(oldUV[oldTriangles[lastIndexAdded + 1]], oldUV[oldTriangles[lastIndexAdded + 2]]));

                    lastIndexAdded += 3;
                    lastTriangleIndexAdded += 6;


                }
                else
                {
                    ///Else put in placeholder vertices of (0,0,0), and don't reference them
                    ///EDIT: Hopefully this lastIndexAdded variable will reduce this problem to nothing.
                    newVerts.Add(oldVerts[oldTriangles[lastIndexAdded]]);
                    newVerts.Add(oldVerts[oldTriangles[lastIndexAdded + 1]]);
                    newVerts.Add(oldVerts[oldTriangles[lastIndexAdded + 2]]);


                    newTriangles.Add(lastTriangleIndexAdded);
                    newTriangles.Add(lastTriangleIndexAdded + 1);
                    newTriangles.Add(lastTriangleIndexAdded + 2);

                    newUV.Add(oldUV[oldTriangles[lastIndexAdded]]);
                    newUV.Add(oldUV[oldTriangles[lastIndexAdded + 1]]);
                    newUV.Add(oldUV[oldTriangles[lastIndexAdded + 2]]);

                    lastIndexAdded += 3;
                    lastTriangleIndexAdded += 3;
                }

            }
            //Clear Mesh
            mesh.Clear();
            //Copy over Verts
            oldVerts = newVerts.ToArray();
            mesh.vertices = oldVerts;
            //Copy over UVs
            oldUV = newUV.ToArray();
            mesh.uv = oldUV;
            //Triangles
            oldTriangles = newTriangles.ToArray();
            mesh.triangles = oldTriangles;
            //Recalculate mesh values
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            //Clear our lists
            newTriangles.Clear();
            newVerts.Clear();
            newUV.Clear();
            return change;
        }
        public bool SubdivideSubMesh(Mesh mesh, Transform transform, int count)
        {
            //Take in the UVs and Triangles
            bool change = false;
            Vector2[] oldUV = new Vector2[mesh.uv.Length];
            Vector3[] oldVerts = new Vector3[mesh.vertices.Length];
            System.Array.Copy(mesh.uv, oldUV, mesh.uv.Length);
            System.Array.Copy(mesh.vertices, oldVerts, mesh.vertices.Length);
            int[][] oldTriangles = new int[count][];
            int lastIndexAdded = 0; //Use this to keep track of the last vertex that you added to the list from each loop.
            int lastTriangleIndexAdded = 0; //Use this to keep track of the last vertex that you added to the list from each loop.

            //Loop through the triangles. If one has an area greater than our chosen constant, then subdivide it]
            int offset = 0;
            for (int q = 0; q < mesh.subMeshCount; ++q)
            {

                oldTriangles[q] = mesh.GetTriangles(q);
                for (int i = 0; i < oldTriangles[q].Length / 3; ++i)
                {
                    Vector3 dist1 = (transform.TransformPoint(mesh.vertices[mesh.triangles[lastIndexAdded]]) - transform.TransformPoint(mesh.vertices[mesh.triangles[lastIndexAdded + 1]]));
                    Vector3 dist2 = (transform.TransformPoint(mesh.vertices[mesh.triangles[lastIndexAdded + 2]]) - transform.TransformPoint(mesh.vertices[mesh.triangles[lastIndexAdded + 1]]));
                    float area = Vector3.Cross(dist2, dist1).magnitude / 2;
                    if (i == 0)
                        print(area);
                    if (area > constant)
                    {
                        change = true;
                        //Add old verts
                        newVerts.Add(oldVerts[oldTriangles[q][lastIndexAdded]]);
                        newVerts.Add(oldVerts[oldTriangles[q][lastIndexAdded + 1]]);
                        newVerts.Add(oldVerts[oldTriangles[q][lastIndexAdded + 2]]);
                        //new ones
                        newVerts.Add(Mid(oldVerts[oldTriangles[q][lastIndexAdded]], oldVerts[oldTriangles[q][lastIndexAdded + 1]]));
                        newVerts.Add(Mid(oldVerts[oldTriangles[q][lastIndexAdded]], oldVerts[oldTriangles[q][lastIndexAdded + 2]]));
                        newVerts.Add(Mid(oldVerts[oldTriangles[q][lastIndexAdded + 1]], oldVerts[oldTriangles[q][lastIndexAdded + 2]]));

                        //Triangle 1
                        newTriangles.Add(offset + lastTriangleIndexAdded);
                        newTriangles.Add(offset + lastTriangleIndexAdded + 3);
                        newTriangles.Add(offset + lastTriangleIndexAdded + 4);
                        //Triangle 2
                        newTriangles.Add(offset + lastTriangleIndexAdded + 5);
                        newTriangles.Add(offset + lastTriangleIndexAdded + 4);
                        newTriangles.Add(offset + lastTriangleIndexAdded + 3);
                        //Triangle 3
                        newTriangles.Add(offset + lastTriangleIndexAdded + 4);
                        newTriangles.Add(offset + lastTriangleIndexAdded + 5);
                        newTriangles.Add(offset + lastTriangleIndexAdded + 2);
                        //Triangle 4
                        newTriangles.Add(offset + lastTriangleIndexAdded + 5);
                        newTriangles.Add(offset + lastTriangleIndexAdded + 3);
                        newTriangles.Add(offset + lastTriangleIndexAdded + 1);
                        //Add the new and old UV verts
                        newUV.Add(oldUV[oldTriangles[q][lastIndexAdded]]);
                        newUV.Add(oldUV[oldTriangles[q][lastIndexAdded + 1]]);
                        newUV.Add(oldUV[oldTriangles[q][lastIndexAdded + 2]]);
                        newUV.Add(Mid2(oldUV[oldTriangles[q][lastIndexAdded]], oldUV[oldTriangles[q][lastIndexAdded + 1]]));
                        newUV.Add(Mid2(oldUV[oldTriangles[q][lastIndexAdded]], oldUV[oldTriangles[q][lastIndexAdded + 2]]));
                        newUV.Add(Mid2(oldUV[oldTriangles[q][lastIndexAdded + 1]], oldUV[oldTriangles[q][lastIndexAdded + 2]]));

                        lastIndexAdded += 3;
                        lastTriangleIndexAdded += 6;


                    }
                    else
                    {
                        ///Else put in placeholder vertices of (0,0,0), and don't reference them
                        ///EDIT: Hopefully this lastIndexAdded variable will reduce this problem to nothing.
                        newVerts.Add(oldVerts[oldTriangles[q][lastIndexAdded]]);
                        newVerts.Add(oldVerts[oldTriangles[q][lastIndexAdded + 1]]);
                        newVerts.Add(oldVerts[oldTriangles[q][lastIndexAdded + 2]]);


                        newTriangles.Add(offset + lastTriangleIndexAdded);
                        newTriangles.Add(offset + lastTriangleIndexAdded + 1);
                        newTriangles.Add(offset + lastTriangleIndexAdded + 2);

                        newUV.Add(oldUV[oldTriangles[q][lastIndexAdded]]);
                        newUV.Add(oldUV[oldTriangles[q][lastIndexAdded + 1]]);
                        newUV.Add(oldUV[oldTriangles[q][lastIndexAdded + 2]]);

                        lastIndexAdded += 3;
                        lastTriangleIndexAdded += 3;
                    }

                }
                oldTriangles[q] = newTriangles.ToArray();
                newTriangles.Clear();
            }


            print(count);
            //Clear Mesh
            mesh.Clear();
            //Copy over Verts
            oldVerts = newVerts.ToArray();
            mesh.vertices = oldVerts;
            mesh.subMeshCount = count;
            for (int j = 0; j < count; ++j)
            {
                mesh.SetTriangles(oldTriangles[j], j);
            }
            //Copy over UVs
            oldUV = newUV.ToArray();
            mesh.uv = oldUV;
            //Recalculate mesh values
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            //Clear our lists
            newTriangles.Clear();
            newVerts.Clear();
            newUV.Clear();
            return change;
        }

        private void RunShader()
        {
            int kernelHandle = colliderShader.FindKernel("CSMain");

            RenderTexture tex = new RenderTexture(256, 256, 24);
            tex.enableRandomWrite = true;
            tex.Create();

            colliderShader.SetTexture(kernelHandle, "Result", tex);
            colliderShader.Dispatch(kernelHandle, 256 / 8, 256 / 8, 1);
        }
    }
}