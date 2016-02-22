using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class ObjectMeshDensity : MonoBehaviour {

    //Need these three lists to create a new set of each variable needed for a new mesh
    List<int> newTriangles = new List<int>();
    List<Vector3> newVerts = new List<Vector3>();
    List<Vector2> newUV = new List<Vector2>();
    //state array contains information on splitting
    public bool state;
    //These store the original and split mesh
    public Mesh change;
    public Mesh original;
    

    //This constant determines triangle size. We subdivide meshes until all their triangles have less than this area.
    public double constant = 2;

	// Use this for initialization
	void Start () {
		//Grab the meshfilter, and if it's not null, keep going
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
			//Store a copy of our original mesh
            original = new Mesh();

            original.vertices = meshFilter.mesh.vertices;
            original.uv = meshFilter.mesh.uv;
            original.triangles = meshFilter.mesh.triangles;

            original.RecalculateBounds();
            original.RecalculateNormals();
            original.name = meshFilter.mesh.name;
			//Prepare a new mesh for our split mesh
            change = new Mesh();
            change.vertices = meshFilter.mesh.vertices;
            change.uv = meshFilter.mesh.uv;
            change.triangles = meshFilter.mesh.triangles;

            change.RecalculateBounds();
            change.RecalculateNormals();
            change.name = meshFilter.mesh.name;

            //Keep splitting this mesh until all of its triangles have area less than our chosen value
            bool isDone = true;
            int numberOfLoops = 0;
            while (isDone == true)
            {
                isDone = Subdivide(change, meshFilter.transform);
                numberOfLoops++;
              
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
            

    //These are my own version of list to Array functions, I was having trouble with the regular ones
    // but might try them again

    //Copy over triangle list to triangle array
    int[] CopyOverT(List<int> newT, int[] T)
    {
        T = new int[newT.Count];
        for (int i = 0; i < newT.Count; i++)
        {
            T[i] = newT[i];
        }
        return T;
    }
    //Copy vector list, return vector array
    Vector3[] CopyOverV(List<Vector3> newT, Vector3[] T)
    {
        T = new Vector3[newT.Count];
        for (int i = 0; i < newT.Count; i++)
        {
            T[i].x = newT[i].x;
            T[i].y = newT[i].y;
            T[i].z = newT[i].z;
        }
        return T;
    }
    //Copy over UV list, return UV array
    Vector2[] CopyOverV2(List<Vector2> newT, Vector2[] T)
    {
        T = new Vector2[newT.Count];
        for (int i = 0; i < newT.Count; i++)
        {
            T[i].x = newT[i].x;
            T[i].y = newT[i].y;
        }
        return T;
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
        Vector3 vect;
        vect.x = (one.x + two.x) / 2;
        vect.y = (one.y + two.y) / 2;
        vect.z = (one.z + two.z) / 2;
        return vect;
    }
    //Finds midpoint of two V2s
    Vector2 Mid2(Vector2 one, Vector2 two)
    {
        Vector2 vect;
        vect.x = (one.x + two.x) / 2;
        vect.y = (one.y + two.y) / 2;
        return vect;
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
        for (int i = 0; i < mesh.triangles.Length / 3; i++)
        {
			//Get two edges, take their cross product, and divide the magnitude in half. That should give us the triangle's area.
            Vector3 dist1 = (RecursiveTransform(mesh.vertices[mesh.triangles[lastIndexAdded]], transform) - RecursiveTransform(mesh.vertices[mesh.triangles[lastIndexAdded + 1]], transform));
            Vector3 dist2 = (RecursiveTransform(mesh.vertices[mesh.triangles[lastIndexAdded + 2]], transform) - RecursiveTransform(mesh.vertices[mesh.triangles[lastIndexAdded + 1]], transform));
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
        oldVerts = CopyOverV(newVerts, oldVerts);
        mesh.vertices = oldVerts;
        //Copy over UVs
        oldUV = CopyOverV2(newUV, oldUV);
        mesh.uv = oldUV;
        //Triangles
        oldTriangles = CopyOverT(newTriangles, oldTriangles);
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
        for (int q = 0; q < mesh.subMeshCount; q++)
        {

            oldTriangles[q] = mesh.GetTriangles(q);
            for (int i = 0; i < oldTriangles[q].Length / 3; i++)
            {
                Vector3 dist1 = (RecursiveTransform(mesh.vertices[mesh.triangles[lastIndexAdded]], transform) - RecursiveTransform(mesh.vertices[mesh.triangles[lastIndexAdded + 1]], transform));
                Vector3 dist2 = (RecursiveTransform(mesh.vertices[mesh.triangles[lastIndexAdded + 2]], transform) - RecursiveTransform(mesh.vertices[mesh.triangles[lastIndexAdded + 1]], transform));
                float area = Vector3.Cross(dist2, dist1).magnitude / 2;
                if(i==0)
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
                    newTriangles.Add(offset+lastTriangleIndexAdded);
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
            oldTriangles[q] = CopyOverT(newTriangles, oldTriangles[q]);
            newTriangles.Clear();
        }


        print(count);
        //Clear Mesh
        mesh.Clear();
        //Copy over Verts
        oldVerts = CopyOverV(newVerts, oldVerts);
        mesh.vertices = oldVerts;
        mesh.subMeshCount = count;
        for (int j = 0; j < count; j++)
        {
            mesh.SetTriangles(oldTriangles[j], j);
        }
        //Copy over UVs
        oldUV = CopyOverV2(newUV, oldUV);
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
    public Vector3 RecursiveTransform(Vector3 pt, Transform trans)
    {
        //Basically, this will transform the point until it has no more parent transforms.
        Vector3 pt1 = Vector3.zero;
        //If we have a parent transform, run this function again
        if (trans.parent != null)
        {
            pt = RecursiveTransform(pt1, trans.parent);

            return pt;
        }
        else
        {
            pt1 = trans.TransformPoint(pt);
            return pt1;
        }
    }
}
