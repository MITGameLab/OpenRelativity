using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class RelativisticParent : MonoBehaviour {
    //Keep track of our own Mesh Filter
    private MeshFilter meshFilter;
    //Store this object's velocity here.
    public Vector3 viw;
    private GameState state;

    // Use this for initialization
    void Start()
    {
        if (GetComponent<ObjectMeshDensity>())
        {
            GetComponent<ObjectMeshDensity>().enabled = false;
        }
        int vertCount = 0, triangleCount = 0;
		
        checkSpeed();
        Matrix4x4 worldLocalMatrix = transform.worldToLocalMatrix;

        //This code combines the meshes of children of parent objects
        //This increases our FPS by a ton
		//Get an array of the meshfilters
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
		//Count submeshes
        int[] subMeshCount = new int[meshFilters.Length];
		//Get all the meshrenderers
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
		//Length of our original array
        int meshFilterLength = meshFilters.Length;
		//And a counter
        int subMeshCounts = 0;
        
		//For every meshfilter,
        for (int y = 0; y < meshFilterLength; y++)
        {
			//If it's null, ignore it.
            if (meshFilters[y] == null) continue;
            if (meshFilters[y].sharedMesh == null) continue;
			//else add its vertices to the vertcount
            vertCount += meshFilters[y].sharedMesh.vertices.Length;
			//Add its triangles to the count
            triangleCount += meshFilters[y].sharedMesh.triangles.Length;
			//Add the number of submeshes to its spot in the array
            subMeshCount[y] = meshFilters[y].mesh.subMeshCount;
			//And add up the total number of submeshes
            subMeshCounts += meshFilters[y].mesh.subMeshCount;
        }
		// Get a temporary array of EVERY vertex
        Vector3[] tempVerts = new Vector3[vertCount];
		//And make a triangle array for every submesh
        int[][] tempTriangles = new int[subMeshCounts][];

        for (int u = 0; u < subMeshCounts; u++)
        {
			//Make every array the correct length of triangles
            tempTriangles[u] = new int[triangleCount];
        }
		//Also grab our UV texture coordinates
        Vector2[] tempUVs = new Vector2[vertCount];
        //And store a number of materials equal to the number of submeshes.
        Material[] tempMaterials =  new Material[subMeshCounts];

        int vertIndex = 0;
        Mesh MFs;
        int subMeshIndex = 0;
		//For all meshfilters
        for (int i = 0; i < meshFilterLength; i++)
        {
            //just doublecheck that the mesh isn't null
            MFs = meshFilters[i].sharedMesh;
            if (MFs == null) continue;
            
			//Otherwise, for all submeshes in the current mesh
            for (int q = 0; q < subMeshCount[i]; q++)
            {
                //grab its material
                tempMaterials[subMeshIndex] = meshRenderers[i].materials[q];
				//Grab its triangles
                int[] tempSubTriangles = MFs.GetTriangles(q);
				//And put them into the submesh's triangle array
                for (int k = 0; k < tempSubTriangles.Length; k++)
                {
                    tempTriangles[subMeshIndex][k] = tempSubTriangles[k] + vertIndex;
                }
				//Increment the submesh index
                subMeshIndex++;
            }
            Matrix4x4 cTrans = worldLocalMatrix * meshFilters[i].transform.localToWorldMatrix;
            //For all the vertices in the mesh
            for (int v = 0; v < MFs.vertices.Length; v++)
            {
				//Get the vertex and the UV coordinate
                tempVerts[vertIndex] = cTrans.MultiplyPoint3x4(MFs.vertices[v]);
                tempUVs[vertIndex] = MFs.uv[v];
                vertIndex++;
            }
			//And delete that gameobject.
            meshFilters[i].gameObject.SetActive(false);
        }
		//Put it all together now.
        Mesh myMesh = new Mesh();
		//Make the mesh have as many submeshes as you need
        myMesh.subMeshCount = subMeshCounts;
		//Set its vertices to tempverts
        myMesh.vertices = tempVerts;
		//start at the first submesh
        subMeshIndex = 0;
		//For every submesh in each meshfilter
        for (int l = 0; l < meshFilterLength; l++)
        {
            for (int g = 0; g < subMeshCount[l]; g++)
            {
				//Set a new submesh, using the triangle array and its submesh index (built in unity function)
                myMesh.SetTriangles(tempTriangles[subMeshIndex], subMeshIndex);
				//increment the submesh index
                subMeshIndex++;
            }
        }
		//Just shunt in the UV coordinates, we don't need to change them
        myMesh.uv = tempUVs;
		//THEN totally replace our object's mesh with this new, combined mesh
        GetComponent<MeshFilter>().mesh = myMesh;
        GetComponent<MeshRenderer>().enabled = true;
        GetComponent<MeshFilter>().mesh.RecalculateNormals();
        GetComponent<MeshFilter>().renderer.materials = tempMaterials;

        transform.gameObject.SetActive(true);
        //End section of combining meshes


        
        state = GameObject.FindGameObjectWithTag("Player").GetComponent<GameState>();
		
        meshFilter = GetComponent<MeshFilter>();
		
        MeshRenderer tempRenderer = GetComponent<MeshRenderer>();



		//Then the standard RelativisticObject startup
        if (tempRenderer.materials[0].mainTexture != null)
        {
            //So that we can set unique values to every moving object, we have to instantiate a material
            //It's the same as our old one, but now it's not connected to every other object with the same material
            Material quickSwapMaterial = Instantiate((tempRenderer as Renderer).materials[0]) as Material;
            //Then, set the value that we want
            quickSwapMaterial.SetFloat("_viw", 0);
            //And stick it back into our renderer. We'll do the SetVector thing every frame.
            tempRenderer.materials[0] = quickSwapMaterial;
        }

        //This code is a hack to ensure that frustrum culling does not take place
        //It changes the render bounds so that everything is contained within them
        Transform camTransform = Camera.main.transform;
        float distToCenter = (Camera.main.farClipPlane - Camera.main.nearClipPlane) / 2.0f;
        Vector3 center = camTransform.position + camTransform.forward * distToCenter;
        float extremeBound = 500000.0f;
        meshFilter.sharedMesh.bounds = new Bounds(center, Vector3.one * extremeBound);

      
        if (GetComponent<ObjectMeshDensity>())
        {
            GetComponent<ObjectMeshDensity>().enabled = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
     
        if (meshFilter != null && !state.MovementFrozen)
        {
        }



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
	/*
    //Use this function to add distance to our object moving with periodic motion
    public void PeriodicAddTime()
    {
        meshFilter.transform.Translate(new Vector3(0, amp * Mathf.Sin((float)(period * state.TotalTimeWorld)) - amp * Mathf.Sin((float)(period * (state.TotalTimeWorld - state.DeltaTimeWorld))), 0));
    }
    //Use this function to simulate the "flight time" of a photon, pass it tisw
    public Vector3 PeriodicSubtractTime(float tisw, Quaternion rotation)
    {
        Vector3 addedVelocity = rotation * (new Vector3(0, amp * Mathf.Sin((float)(period * (state.TotalTimeWorld + tisw))) - amp * Mathf.Sin((float)(period * (state.TotalTimeWorld))), 0));
        return addedVelocity;
    }
    //Use this to get our current velocity in world.
    public Vector3 CurrentVelocity()
    {
        Vector3 velocity = Vector3.zero;

        velocity.y = amp * period * Mathf.Cos((float)(period * state.TotalTimeWorld));

        return velocity;
    }
    public Vector4 CurrentVelocity4()
    {
        Vector4 velocity = Vector4.zero;

        velocity.y = amp * period * Mathf.Cos((float)(period * state.TotalTimeWorld));

        return velocity;
    }
    */
    private void checkSpeed()
    {/*
        if (periodic && amp * period > 4.95f)
        {
            period = 4.95f / amp;
        }
        else if (viw.magnitude > 4.95f)
        {
            viw = viw.normalized * 4.95f;
        }
        */
    }
    
}