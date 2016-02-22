using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
public class RelativisticObject : MonoBehaviour {
	public const float RAD_2_DEG = 57.2957795f; 
	//Keep track of our own Mesh Filter
	private MeshFilter meshFilter;
	//Store our raw vertices in this variable, so that we can refer to them later
	private Vector3[] rawVerts;
	//Store this object's velocity here.
	public Vector3 viw;
	//Keep track of Game State so that we can reference it quickly
	public GameState state;
	//When was this object created? use for moving objects
	private float startTime = 0;
	//When should we die? again, for moving objects
	private float deathTime = 0;

	//Use this instead of relativistic parent
	public bool isParent =false;
	void Awake()
	{
		//Get the player's GameState, use it later for general information
		state = GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>();
	}

	// Get the start time of our object, so that we know where not to draw it
	public void SetStartTime()
	{
		startTime = (float) state.TotalTimeWorld;
		if(GetComponent<MeshRenderer>()!=null)
			GetComponent<MeshRenderer>().enabled = false;
	}
	//Set the death time, so that we know at what point to destroy the object in the player's view point.
	public virtual void SetDeathTime()
	{
		deathTime = (float)state.TotalTimeWorld;
	}
	void CombineParent()
	{
		if (GetComponent<ObjectMeshDensity>())
		{
			GetComponent<ObjectMeshDensity>().enabled = false;
		}
		int vertCount = 0, triangleCount = 0;
		Matrix4x4 worldLocalMatrix = transform.worldToLocalMatrix;

		//This code combines the meshes of children of parent objects
		//This increases our FPS by a ton
		//Get an array of the meshfilters
		MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
		//Count submeshes
		int[] subMeshCount = new int[meshFilters.Length];
		//Get all the meshrenderers
		MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
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
		Material[] tempMaterials = new Material[subMeshCounts];

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

		MeshFilter meshy = gameObject.GetComponent<MeshFilter>();
		if (GetComponent<MeshFilter>() == null)
		{
			gameObject.AddComponent<MeshRenderer>();
			meshy = gameObject.AddComponent<MeshFilter>();
		}
		meshy.mesh = myMesh;
		GetComponent<MeshRenderer>().enabled = false;

		meshy.mesh.RecalculateNormals();
		meshy.GetComponent<Renderer>().materials = tempMaterials;

		transform.gameObject.SetActive(true);

	}
	void Start()
	{
		checkSpeed();
		//Get the meshfilter
		if(isParent)
		{
			CombineParent();
		}
		meshFilter = GetComponent<MeshFilter>();

		//Also get the meshrenderer so that we can give it a unique material
		MeshRenderer tempRenderer = GetComponent<MeshRenderer>();
		//If we have a MeshRenderer on our object
		if (tempRenderer != null)
		{
			//And if we have a texture on our material
			for (int i = 0; i < tempRenderer.materials.Length; i++)
			{
				//if (tempRenderer.materials[i]!=null && tempRenderer.materials[i].mainTexture != null)
				//{
				//So that we can set unique values to every moving object, we have to instantiate a material
				//It's the same as our old one, but now it's not connected to every other object with the same material
				Material quickSwapMaterial = Instantiate((tempRenderer as Renderer).materials[i]) as Material;
				//Then, set the value that we want
				quickSwapMaterial.SetFloat("_viw", 0);

				//And stick it back into our renderer. We'll do the SetVector thing every frame.
				tempRenderer.materials[i] = quickSwapMaterial;

				//set our start time and start position in the shader.
				tempRenderer.materials[i].SetFloat("_strtTime", (float)startTime);
				tempRenderer.materials[i].SetVector("_strtPos", new Vector4(transform.position.x, transform.position.y, transform.position.z, 0));
				//}
			}
		}
		//Get the vertices of our mesh
		if (meshFilter != null)
		{
			rawVerts = meshFilter.mesh.vertices;
		}
		else
			rawVerts = null;


		//This code is a hack to ensure that frustrum culling does not take place
		//It changes the render bounds so that everything is contained within them
		//At high speeds the Lorenz contraction means that some objects not normally in the view frame are actually visible
		//If we did frustrum culling, these objects would be ignored (because we cull BEFORE running the shader, which does the lorenz contraction)
		if (meshFilter != null)
		{
			Transform camTransform = Camera.main.transform;
			float distToCenter = (Camera.main.farClipPlane + Camera.main.nearClipPlane) / 2.0f;
			Vector3 center = camTransform.position + camTransform.forward * distToCenter;
			float extremeBound = 500000.0f;
			meshFilter.sharedMesh.bounds = new Bounds(center, Vector3.one * extremeBound);
		}
	}


	public void Update()
	{
		//Grab our renderer.
		MeshRenderer tempRenderer = GetComponent<MeshRenderer>();

		if (meshFilter != null && !state.MovementFrozen)
		{
			#region meshDensity
			//This is where I'm going to change our mesh density.
			//I'll take the model, and pass MeshDensity the mesh and unchanged vertices
			//If it comes back as having changed something, I'll edit the mesh.

			ObjectMeshDensity density = GetComponent<ObjectMeshDensity>();

			if (density != null)
			{

				//Only run MeshDensity if the mesh needs to change, and if it's passed a threshold distance.
				if (rawVerts != null && density.change != null)
				{
					//This checks if we're within our large range, first mesh density circle
					//If we're within a distance of 40, split this mesh
					if (density.state == false && RecursiveTransform(rawVerts[0], meshFilter.transform).magnitude < 21000)
					{
						if (density.ReturnVerts(meshFilter.mesh, true))
						{
							rawVerts = new Vector3[meshFilter.mesh.vertices.Length];
							System.Array.Copy(meshFilter.mesh.vertices, rawVerts, meshFilter.mesh.vertices.Length);

						}
					}

					//If the object leaves our wide range, revert mesh to original state
					else if (density.state == true && RecursiveTransform(rawVerts[0], meshFilter.transform).magnitude > 21000)
					{
						if (density.ReturnVerts(meshFilter.mesh, false))
						{
							rawVerts = new Vector3[meshFilter.mesh.vertices.Length];
							System.Array.Copy(meshFilter.mesh.vertices, rawVerts, meshFilter.mesh.vertices.Length);
						}
					}

				}
			}
			#endregion


			//Send our object's v/c (Velocity over the Speed of Light) to the shader
			if (tempRenderer != null)
			{
				Vector3 tempViw = viw / (float)state.SpeedOfLight;
				for (int i = 0; i < tempRenderer.materials.Length; i++)
				{
					tempRenderer.materials[i].SetVector("_viw", new Vector4(tempViw.x, tempViw.y, tempViw.z, 0));
				}
			}

			//As long as our object is actually alive, perform these calculations
			if (transform != null)
			{
				//Here I take the angle that the player's velocity vector makes with the z axis
				float rotationAroundZ = RAD_2_DEG * Mathf.Acos(Vector3.Dot(state.PlayerVelocityVector, Vector3.forward) / state.PlayerVelocityVector.magnitude);

				if (state.PlayerVelocityVector.sqrMagnitude == 0)
				{
					rotationAroundZ = 0;
				}

				//Now we turn that rotation into a quaternion

				Quaternion rotateZ = Quaternion.AngleAxis(-rotationAroundZ, Vector3.Cross(state.PlayerVelocityVector, Vector3.forward));
				//******************************************************************

				//Place the vertex to be changed in a new Vector3
				Vector3 riw = new Vector3(transform.position.x, transform.position.y, transform.position.z);
				riw -= state.playerTransform.position;


				//And we rotate our point that much to make it as if our magnitude of velocity is in the Z direction
				riw = rotateZ * riw;


				//Here begins the original code, made by the guys behind the Relativity game
				/****************************
                     * Start Part 6 Bullet 1
                
                */

				//Rotate that velocity!
				Vector3 storedViw = rotateZ * viw;

				float c = -Vector3.Dot(riw, riw); //first get position squared (position doted with position)

				float b = -(2 * Vector3.Dot(riw, storedViw)); //next get position doted with velocity, should be only in the Z direction

				float a = (float)state.SpeedOfLightSqrd - Vector3.Dot(storedViw, storedViw);

				/****************************
                 * Start Part 6 Bullet 2
                 * **************************/

				float tisw = (float)(((-b - (Math.Sqrt((b * b) - 4f * a * c))) / (2f * a)));
				//If we're past our death time (in the player's view, as seen by tisw)
				if (state.TotalTimeWorld + tisw > deathTime && deathTime!=0)
				{
					KillObject();
				}
				if (state.TotalTimeWorld + tisw > startTime && !tempRenderer.enabled)
				{
					tempRenderer.enabled = true;
					if(GetComponent<AudioSource>()!= null)
					{
						GetComponent<AudioSource>().enabled=true;
					}
				}
			}

			//make our rigidbody's velocity viw
			if (GetComponent<Rigidbody>() != null)
			{

				if (!double.IsNaN((double)state.SqrtOneMinusVSquaredCWDividedByCSquared) && (float)state.SqrtOneMinusVSquaredCWDividedByCSquared != 0)
				{
					Vector3 tempViw = viw;
					//ASK RYAN WHY THESE WERE DIVIDED BY THIS
					tempViw.x /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
					tempViw.y /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
					tempViw.z /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;

					GetComponent<Rigidbody>().velocity = tempViw;
				}


			}
		}
		//If nothing is null, then set the object to standstill, but make sure its rigidbody actually has a velocity.
		else if (meshFilter != null && tempRenderer != null && GetComponent<Rigidbody>() != null)
		{
			GetComponent<Rigidbody>().velocity = Vector3.zero;
		}



	}
	public  void KillObject()
	{
		gameObject.SetActive (false);
		//Destroy(this.gameObject);
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

	//This is a function that just ensures we're slower than our maximum speed. The VIW that Unity sets SHOULD (it's creator-chosen) be smaller than the maximum speed.
	private void checkSpeed()
	{
		if (viw.magnitude > state.MaxSpeed-.01)
		{
			viw = viw.normalized * (float)(state.MaxSpeed-.01f);
		}
	}

	void OnEnable()
	{
		ResetDeathTime();
	}
	void ResetDeathTime()
	{
		deathTime = 0;
	}
} 