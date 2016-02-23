using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
public class Firework : MonoBehaviour
{
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

	//FIREWORK VARIABLES
	public float count = 10;
	public float startTimer = 4;
	public bool isParticle = false;
	public float particleSpeed = 2;

	float timer=0;

	//This is in case you implement memory pools for generated objects. Whenever they are re enabled they should activate again
	void OnEnable()
	{
		timer = startTimer;
		ResetDeathTime();
	}
	//Grab the gamestate and keep it safe
	void Awake()
	{
		//Get the player's GameState, use it later for general information
		state = GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>();
	}

	// Get the start time of our object, so that we know where not to draw it
	//Turn off the mesh renderer to avoid transparency problems
	public void SetStartTime()
	{
		startTime = (float)state.TotalTimeWorld;
		GetComponent<MeshRenderer>().enabled = false;
		//start my countdown
		timer = startTimer;
	}
	//Set the death time, so that we know at what point to destroy the object in the player's view point.

	public void Start()
	{
		checkSpeed();
		//Get the meshfilter
		meshFilter = GetComponent<MeshFilter>();
		//Also get the meshrenderer so that we can give it a unique material
		MeshRenderer tempRenderer = GetComponent<MeshRenderer>();
		//If we have a MeshRenderer on our object
		if (tempRenderer != null)
		{
			//And if we have a texture on our material
			if (tempRenderer.materials[0].mainTexture != null)
			{
				//So that we can set unique values to every moving object, we have to instantiate a material
				//It's the same as our old one, but now it's not connected to every other object with the same material
				Material quickSwapMaterial = Instantiate((tempRenderer as Renderer).materials[0]) as Material;
				//Then, set the value that we want
				//And stick it back into our renderer. We'll do the SetVector thing every frame.
				tempRenderer.materials[0] = quickSwapMaterial;
				tempRenderer.materials[0].SetFloat("_strtTime", (float)startTime);
				tempRenderer.materials[0].SetVector("_strtPos", new Vector4(transform.position.x, transform.position.y, transform.position.z, 0));
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
		Transform camTransform = Camera.main.transform;
		float distToCenter = (Camera.main.farClipPlane + Camera.main.nearClipPlane) / 2.0f;
		Vector3 center = camTransform.position + camTransform.forward * distToCenter;
		float extremeBound = 500000.0f;
		meshFilter.sharedMesh.bounds = new Bounds(center, Vector3.one * extremeBound); 
	}
	//If you reenable the object, it will destroy itself unless you reset the death time
	void ResetDeathTime()
	{
		deathTime = 0;
	}
	//Set our death time. This tells the object when to die (it's not where it should be due to runtime effect)
	public  void SetDeathTime()
	{
		//If we're a firework not a particle, explode into particles!
		if (!isParticle)
		{
			//float slerp = 1.0f / count;
			//Get a spacing for the generated particles
			float spacing = (float)(360) / count;
			//Roll around a sphere, and send off a bunch of particles. Add judder to ensure it looks naturally not uniform
			for (int j = 0; j < count*2; j++)
			{
				for (int i = 0; i < count; i++)
				{
					float judder = UnityEngine.Random.Range(-.25f, .25f);
					float judderTwo = UnityEngine.Random.Range(-.25f, .25f);
					EmitParticle((j+ judderTwo) /2.0f*spacing,(i+ judder) * spacing);
				}
			}
		}
		//and, set the death time
		deathTime = (float)state.TotalTimeWorld;
	}
	//Standard update, taken out of Relativistic Object
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
				tempRenderer.materials[0].SetVector("_viw", new Vector4(tempViw.x, tempViw.y, tempViw.z, 0));
			}

			//As long as our object is actually alive, perform these calculations
			if (transform != null)// && deathTime != 0)
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
				if (state.TotalTimeWorld + tisw > deathTime && deathTime !=0)
				{
					KillObject();
				}
				if (state.TotalTimeWorld +tisw >startTime &&!tempRenderer.enabled)
				{

					tempRenderer.enabled = true;
				}

			}

			//make our rigidbody's velocity the same as viw
			if (GetComponent<Rigidbody> () != null && !isParticle) {

				if (!double.IsNaN ((double)state.SqrtOneMinusVSquaredCWDividedByCSquared) && (float)state.SqrtOneMinusVSquaredCWDividedByCSquared != 0) {
					Vector3 tempViw = viw;
					tempViw.x /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
					tempViw.y /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
					tempViw.z /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;

					GetComponent<Rigidbody> ().velocity = tempViw;
				}


			} else if (isParticle) {
				Vector3 tempViw = viw;
				tempViw.x /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
				tempViw.y /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
				tempViw.z /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
				transform.position = transform.position + tempViw * Time.deltaTime;
			}
		}
		//If nothing is null, then set the object to standstill, but make sure its rigidbody actually has a velocity.
		else if (meshFilter != null && tempRenderer != null && GetComponent<Rigidbody>() != null)
		{
			GetComponent<Rigidbody>().velocity = Vector3.zero;
		}

		//This time transformation goes from world time to this objects personal time
		timer -= (float)(state.DeltaTimeWorld * (Math.Sqrt(1 - (viw.sqrMagnitude) / state.SpeedOfLightSqrd)));
		//If we've passed the time to explode, blow it up
		if (timer <= 0)
		{
			SetDeathTime();

			timer = float.MaxValue;
		}
	}
	//Actually destroy the object
	public void KillObject()
	{
		GameObject.Destroy(gameObject);
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
		if (viw.magnitude > state.MaxSpeed - .01)
		{
			viw = viw.normalized * (float)(state.MaxSpeed - .01f);
		}
	}
	//Launch a single particle, see SetDeathTime above for its use
	public void EmitParticle(float gamma,float theta)
	{
		//rotate the direction down by gamma, around by theta
		Vector3 direction = Quaternion.AngleAxis(theta, transform.up) * Quaternion.AngleAxis(gamma,transform.right)*transform.up;
		GameObject launchedObject;
		//Instantiate a new Object (You can find this object in the GameObjects folder, it's a prefab.
		launchedObject = (GameObject)Instantiate(Resources.Load("GameObjects/Particle", typeof(GameObject)), transform.position, this.transform.rotation);

		//Translate it to our center, and put it so that it's just touching the ground
		launchedObject.transform.Translate(Vector3.zero);
		//Make it a child of our transform.
		launchedObject.transform.parent = transform.parent;
		launchedObject.transform.position = transform.position;
		launchedObject.GetComponent<Firework>().isParticle = true;
		//Their velocity should be in the direction we're facing, at viwMax magnitude
		launchedObject.GetComponent<Firework>().viw = particleSpeed * direction;
		//And let the object know when it was created, so that it knows when not to be seen by the player
		launchedObject.GetComponent<Firework>().SetStartTime();

	}



}

