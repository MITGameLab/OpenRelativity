using System;
using System.Collections.Generic;
//using System.Linq; //For debugging purposes only
using UnityEngine;

namespace OpenRelativity.Objects
{
    public class RelativisticObject : MonoBehaviour
    {
        #region Rigid body physics
        //Since we don't have direct access to the level of rigid bodies or PhysX in Unity,
        // we need to "hack" in relativistic rigid body mechanics, for the time-being.
        // Any non-static or non-stationary relativistic object needs to follow a
        // (rigid body) optical clock that depends on its distance from the player.

        //We will use a base 3D rigid body and create a 4D rigid body on top of it.
        // The base 3D rigid body will generally track quantities in "real space,"
        // while the 4D rigid body tracks quantities in Minkowski space.

        //This is our Minkowski space position, with the Unity world coordinate origin convention
        private Vector3 _piw;
        public Vector3 piw {
            get
            {
                return _piw;
            }
            set
            {
                _piw = value;
                Vector3 playerPos = state.playerTransform.position;
                //Inverse Lorentz transform the Minkowski space position back to real space and update:
                transform.position = (value - playerPos).RealToMinkowski(-viw) + playerPos;
            }
        }
        //(Note that 4D rigid body quantities will generally track Minkowski space, in the
        // inertial frame of the the Unity world coordinate origin, with the same origin as
        // the Unity world coordinates, by convention.) 

        public Vector3 initialViw;
        private Vector3 _viw = Vector3.zero;
        public Vector3 viw
        {
            get
            {
                return _viw;
            }
            //Changing velocities lose continuity of position,
            // unless we inverse boost the global position by the original velocity
            // and then boost by the new velocity.
            set
            {
                //This makes instantiation cleaner:
                initialViw = value;

                //Transform from real space to position space for the original velocity, then back to real space for the new velocity
                Vector3 playerPos = state.transform.position;
                transform.position = (transform.position - playerPos).RealToMinkowski(_viw).RealToMinkowski(-value) + playerPos;
                _viw = value;
                //Also update the Rigidbody, if any
                if (myRigidbody != null)
                {
                    myRigidbody.velocity = value;
                    myRigidbody.centerOfMass = transform.InverseTransformPoint(((myRigidbody.worldCenterOfMass - playerPos).RealToMinkowski(_viw).RealToMinkowski(-value) + playerPos));
                }
            }
        }
        //Store this object's angular velocity here.
        public Vector3 initialAviw;
        private Vector3 _aviw;
        public Vector3 aviw
        {
            get
            {
                return _aviw;
            }
            set
            {
                if (!float.IsNaN(value.x + value.y + value.z))
                {
                    initialAviw = value;
                    _aviw = value;
                    if (myRigidbody != null)
                    {
                        //Changes in angular velocity do not change the object's position in real space relative to Minkowski space,
                        // so just update the base rigid body angular velocity without updating the position.
                        myRigidbody.angularVelocity = value;
                    }
                }
            }
        }

        public bool isKinematic
        {
            get
            {
                return myRigidbody.isKinematic;
            }
            set
            {
                myRigidbody.isKinematic = value;
            }
        }

        public float totalOpticalTime {
            get
            {
                //In addition to being Lorentz transformed, physics should (only) appear optically delayed
                // by the amount of time it takes light to travel between an object and the player.
                // We want the game physics entirely in line with the optics, (not just the Lorentz transform), since that's what the player should see.

                /*Total time on the world clock...*/
                return (float)(state.TotalTimeWorld
                    /*...Delayed by the distance between the player and object in Minkowski space*/
                    - (transform.position.RealToMinkowski(-state.PlayerVelocityVector) - state.playerTransform.position).magnitude / state.SpeedOfLight);
            }
        }
        public float deltaOpticalTime { get; set; }

        //Not sure why, but the physics values in our collider materials and rigid bodies become "corrupt"
        // before we can use them. We instantiate duplicates and keep them around:
        // (TODO: Identify why and fix this.)
        public float drag = 0.0f;
        public float angularDrag = 0.0f;
        public Vector3 inertiaTensor;
        public PhysicMaterial physicMaterial;

        //We use a recursive algorithm to approach the exact propagation delay of light
        public int lightSearchIterations = 4;
        //In calculating propagation delay, we also need to account for the player's change in position,
        // so we store the player position last frame here
        private Vector3 playerPositionLastFrame;
        //This is a velocity we "sleep" the rigid body at:
        public float sleepVelocity = 0.14f;
        //This is a cap on penalty method collision.
        //(It is the approximate Young's Modulus of diamond.)
        private const float maxYoungsModulus = 1220.0e9f;
        #endregion

        public const float RAD_2_DEG = 57.2957795f;
        //Keep track of our own Mesh Filter
        private MeshFilter meshFilter;
        //Store our raw vertices in this variable, so that we can refer to them later
        private Vector3[] rawVerts;
        //For a buffer of rawVerts plus other vectors to transform
        private Vector3[] vertsToTransform;
        //Store this object's velocity here.

        //Keep track of Game State so that we can reference it quickly.
        private GameState state;
        //When was this object created? use for moving objects
        private float startTime = 0;
        //When should we die? again, for moving objects
        private float deathTime = 0;

        //Use this instead of relativistic parent
        public bool isParent = false;

        //We use an attached shader to transform the collider verts:
        public ComputeShader colliderShader;
        //We set global constants in a struct;
        private ShaderParams colliderShaderParams;
        //We save and reuse the transformed vert array to avoid garbage collection 
        private Vector3[] trnsfrmdMeshVerts;
        //If we have a mesh collider to transform, we cache it here
        private MeshCollider myCollider;
        //We create a new collider mesh, so as not to interfere with primitives, and reuse it
        private Mesh trnsfrmdMesh;
        //If we have a Rigidbody, we cache it here
        private Rigidbody myRigidbody;

        //Did we collide last frame?
        private bool didCollide;
        //What was the translational velocity result?
        private Vector3 collisionResultVel3;
        //What was the angular velocity result?
        private Vector3 collisionResultAngVel3;
        //What was the original translational velocity?
        private Vector3 preCollisionVel3;
        //What was the original angular velocity?
        private Vector3 preCollisionAngVel3;
        //Time when the collision started
        private double collideTimeStart;
        //Collision-softening time
        public float collideSoftenTime = 0.2f;
        //For penalty methods, we need an intermediate collision velocity result
        private Vector3 oldCollisionResultVel3;
        private Vector3 oldCollisionResultAngVel3;

        //We need to freeze any attached rigidbody if the world states is frozen 
        private bool wasKinematic = false;
        private bool wasFrozen = false;
        private void UpdateCollider()
        {

            //Freeze the physics if the global state is frozen.
            if (state.MovementFrozen)
            {
                if (!wasFrozen)
                {
                    //Read the state of the rigidbody and shut it off, once.
                    wasFrozen = true;
                    wasKinematic = myRigidbody.isKinematic;
                    myRigidbody.isKinematic = true;
                }
                collideTimeStart += state.DeltaTimeWorld;
                return;
            }
            else if (wasFrozen)
            {
                //Restore the state of the rigidbody, once.
                wasFrozen = false;
                myRigidbody.isKinematic = wasKinematic;
            }

            if (!GetComponent<MeshRenderer>().enabled)
            {
                return;
            }

            //Set remaining global parameters:
            colliderShaderParams.ltwMatrix = transform.localToWorldMatrix;
            colliderShaderParams.wtlMatrix = transform.worldToLocalMatrix;
            //colliderShaderParams.piw = transform.position;
            //colliderShaderParams.viw = viw / (float)state.SpeedOfLight;
            //colliderShaderParams.aviw = aviw;
            colliderShaderParams.vpc = -state.PlayerVelocityVector / (float)state.SpeedOfLight;
            colliderShaderParams.playerOffset = state.playerTransform.position;
            colliderShaderParams.speed = (float)(state.PlayerVelocity / state.SpeedOfLight);
            colliderShaderParams.spdOfLight = (float)state.SpeedOfLight;
            colliderShaderParams.wrldTime = (float)state.TotalTimeWorld;
            colliderShaderParams.strtTime = startTime;

            //Center of mass in local coordinates should be invariant,
            // but transforming the collider verts will change it,
            // so we save it and restore it at the end:
            Vector3 initCOM = myRigidbody.centerOfMass;

            ShaderParams[] spa = new ShaderParams[1];
            spa[0] = colliderShaderParams;

            //Put verts in R/W buffer and dispatch:
            ComputeBuffer paramsBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(colliderShaderParams));
            paramsBuffer.SetData(spa);
            ComputeBuffer vertBuffer = new ComputeBuffer(rawVerts.Length, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
            vertBuffer.SetData(rawVerts);
            //ComputeBuffer drawBuffer = new ComputeBuffer(rawVerts.Length, sizeof(float));
            int kernel = colliderShader.FindKernel("CSMain");
            colliderShader.SetBuffer(kernel, "glblPrms", paramsBuffer);
            colliderShader.SetBuffer(kernel, "verts", vertBuffer);
            //colliderShader.SetBuffer(kernel, "drawBools", drawBuffer);
            colliderShader.Dispatch(kernel, rawVerts.Length, 1, 1);
            //float[] drawBools = new float[rawVerts.Length];
            vertBuffer.GetData(trnsfrmdMeshVerts);

            //Change mesh:
            trnsfrmdMesh.vertices = trnsfrmdMeshVerts;
            myCollider.sharedMesh = trnsfrmdMesh;
            //Reset center of mass:
            myRigidbody.centerOfMass = initCOM;
        }

        void Awake()
        {
            //Get the player's GameState, use it later for general information
            state = GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>();

            AwakeRigidbody();
        }

        // Get the start time of our object, so that we know where not to draw it
        public void SetStartTime()
        {
            //startTime = (float)state.TotalTimeWorld;
            //Probably a good sign if we can do this:
            startTime = float.NegativeInfinity;
            if (GetComponent<MeshRenderer>() != null)
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

        private void SetVertsToTransform()
        {
            if (myRigidbody != null)
            {
                vertsToTransform = new Vector3[rawVerts.Length + 1];
                System.Array.Copy(rawVerts, vertsToTransform, rawVerts.Length);
                vertsToTransform[rawVerts.Length] = myRigidbody.worldCenterOfMass;
            }
            else
            {
                vertsToTransform = rawVerts;
            }
        }

        void Start()
        {
            //Initialize the light delay attributes;
            playerPositionLastFrame = state.playerTransform.position;

            didCollide = false;
            myCollider = GetComponent<MeshCollider>();
            myRigidbody = GetComponent<Rigidbody>();
            if (myCollider != null)
            {
                trnsfrmdMesh = Instantiate(myCollider.sharedMesh);
            }
            if (myRigidbody != null)
            {
                myRigidbody.angularVelocity = aviw;
            }

            checkSpeed();
            //Get the meshfilter
            if (isParent)
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
                    quickSwapMaterial.SetFloat("_aviw", 0);
                    quickSwapMaterial.SetFloat("_piw", 0);
                    colliderShaderParams.viw = Vector3.zero;
                    colliderShaderParams.aviw = Vector3.zero;
                    colliderShaderParams.piw = Vector3.zero;

                    //And stick it back into our renderer. We'll do the SetVector thing every frame.
                    tempRenderer.materials[i] = quickSwapMaterial;

                    //set our start time and start position in the shader.
                    tempRenderer.materials[i].SetFloat("_strtTime", (float)startTime);
                    colliderShaderParams.strtTime = (float)startTime;
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

            //Once we have the mesh vertices, allocate and immediately transform the collider:
            if (myCollider != null)
            {
                trnsfrmdMeshVerts = new Vector3[rawVerts.Length];
                //UpdateCollider();
            }

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

        private void EnforceCollision()
        {
            oldCollisionResultVel3 = collisionResultVel3;
            oldCollisionResultAngVel3 = collisionResultAngVel3;

            if (didCollide)
            {
                float fracCollTime = (float)((state.TotalTimeWorld - collideTimeStart) / collideSoftenTime);
                if (fracCollTime < 1.0f)
                {
                    viw = Vector3.Lerp(preCollisionVel3, collisionResultVel3, fracCollTime);
                    aviw = Vector3.Lerp(preCollisionAngVel3, collisionResultAngVel3, fracCollTime);
                    myRigidbody.angularVelocity = aviw;
                }
                else
                {
                    //Finish and shut off enforcement
                    viw = collisionResultVel3;
                    aviw = collisionResultAngVel3;
                    myRigidbody.angularVelocity = aviw;
                    didCollide = false;
                }
            }
        }

        public void Update()
        {
            EnforceCollision();
            if (!state.MovementFrozen)
            {
                UpdateRigidBodyImage(lightSearchIterations);
            }

            //Grab our renderer.
            MeshRenderer tempRenderer = GetComponent<MeshRenderer>();

            //Update the rigidbody reference.
            myRigidbody = GetComponent<Rigidbody>();
            //Update the collider reference.
            myCollider = GetComponent<MeshCollider>();

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
                                //SetVertsToTransform();
                            }
                        }

                        //If the object leaves our wide range, revert mesh to original state
                        else if (density.state == true && RecursiveTransform(rawVerts[0], meshFilter.transform).magnitude > 21000)
                        {
                            if (density.ReturnVerts(meshFilter.mesh, false))
                            {
                                rawVerts = new Vector3[meshFilter.mesh.vertices.Length];
                                System.Array.Copy(meshFilter.mesh.vertices, rawVerts, meshFilter.mesh.vertices.Length);
                                //SetVertsToTransform();
                            }
                        }

                    }
                }
                #endregion


                //Send our object's v/c (Velocity over the Speed of Light) to the shader
                if (tempRenderer != null)
                {
                    Vector3 tempViw = viw / (float)state.SpeedOfLight;
                    Vector3 tempAviw = aviw;
                    Vector3 tempPiw = transform.position;
                    for (int i = 0; i < tempRenderer.materials.Length; i++)
                    {
                        tempRenderer.materials[i].SetVector("_viw", new Vector4(tempViw.x, tempViw.y, tempViw.z, 0));
                        tempRenderer.materials[i].SetVector("_aviw", new Vector4(tempAviw.x, tempAviw.y, tempAviw.z, 0));
                        tempRenderer.materials[i].SetVector("_piw", new Vector4(tempPiw.x, tempPiw.y, tempPiw.z, 0));
                        colliderShaderParams.viw = tempViw;
                        colliderShaderParams.aviw = tempAviw;
                        colliderShaderParams.piw = tempPiw;
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
                    if (state.TotalTimeWorld + tisw > deathTime && deathTime != 0)
                    {
                        KillObject();
                    }
                    if (state.TotalTimeWorld + tisw > startTime && !tempRenderer.enabled)
                    {
                        tempRenderer.enabled = true;
                        if (GetComponent<AudioSource>() != null)
                        {
                            GetComponent<AudioSource>().enabled = true;
                        }
                    }
                }

                //Deprecated:
                //make our rigidbody's velocity viw
                //if (myRigidbody != null)
                //{
                //    if (!double.IsNaN((double)state.SqrtOneMinusVSquaredCWDividedByCSquared) && (float)state.SqrtOneMinusVSquaredCWDividedByCSquared != 0)
                //    {
                //        Vector3 tempViw = viw;
                //        //ASK RYAN WHY THESE WERE DIVIDED BY THIS
                //        tempViw.x /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
                //        tempViw.y /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
                //        tempViw.z /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;

                //        myRigidbody.velocity = tempViw;

                //        //Store the angular velocity for collision resolution:
                //        aviw = myRigidbody.angularVelocity;
                //    }
                //}
            }
            //If nothing is null, then set the object to standstill, but make sure its rigidbody actually has a velocity.
            else if (meshFilter != null && tempRenderer != null && myRigidbody != null)
            {
                myRigidbody.velocity = Vector3.zero;
            }

            //If we have a collider and a compute shader, transform the collider verts relativistically:
            if (colliderShader != null && myCollider != null)
            {
                UpdateCollider();
            }
        }
        public void KillObject()
        {
            gameObject.SetActive(false);
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
            if (viw.magnitude > state.MaxSpeed - .01)
            {
                viw = viw.normalized * (float)(state.MaxSpeed - .01f);
            }

            //The tangential velocities of each vertex should also not be greater than the maximum speed.
            // (This is a relatively computationally costly check, but it's good practice.
            if (trnsfrmdMeshVerts != null)
            {
                float maxSpeedSqr = (float)((state.MaxSpeed - 0.01f) * (state.MaxSpeed - 0.01f));
                for (int i = 0; i < trnsfrmdMeshVerts.Length; i++)
                {
                    float radius = trnsfrmdMeshVerts[i].magnitude;
                    Vector3 tangentialVel = viw.AddVelocity(trnsfrmdMeshVerts[i].magnitude * aviw);
                    float tanVelMagSqr = tangentialVel.sqrMagnitude;
                    if (tanVelMagSqr > maxSpeedSqr)
                    {
                        tangentialVel = tangentialVel.normalized * (float)(state.MaxSpeed - 0.01f);
                        aviw = (-viw).AddVelocity(tangentialVel.normalized * (float)(state.MaxSpeed - 0.01f) / radius);
                    }
                }
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

        #region 4D Rigid body mechanics

        private void AwakeRigidbody()
        {
            Vector3 oldPos = transform.position;

            myRigidbody = GetComponent<Rigidbody>();
            //There's no good way to abstract around the fact that every relativistic object
            // needs a rigid body to track its local clock:
            if (myRigidbody == null)
            {
                myRigidbody = gameObject.AddComponent<Rigidbody>();
            }

            //Save these for physics later:
            //this.drag = myRigidbody.drag;
            //this.angularDrag = myRigidbody.angularDrag;
            this.inertiaTensor = myRigidbody.inertiaTensor;
            //Store a physics material
            // (The collider geometry transformation screws up the base material.)
            this.physicMaterial = new PhysicMaterial();
            this.physicMaterial.bounciness = 0.8f;
            this.physicMaterial.staticFriction = 0.6f;
            this.physicMaterial.dynamicFriction = 0.2f;

            //We're overriding the 3D real space mechanics with 4D relativistic mechanics:
            myRigidbody.constraints = RigidbodyConstraints.FreezeAll;
            myRigidbody.Sleep();
            myRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            viw = initialViw;
            aviw = initialAviw;

            didCollide = false;
            collisionResultVel3 = viw;

            piw = oldPos.RealToMinkowski(-state.PlayerVelocityVector);
        }

        //(We're not going to worry about gravity imposing accelerated frames for now, but this is a TODO.)
        public void UpdateRigidBodyImage(int maxIterations/*, bool applyGravity */)
        {
            //WARNING: Doppler shift might lag behind a frame due to order of player and object frame updates

            float worldDeltaTime = (float)state.DeltaTimeWorld;
            Vector3 playerVel = state.PlayerVelocityVector;

            //If it weren't for optical speed of light travel delay,
            // we'd just update the the position based on the player time and acceleration.
            // This is a good starting point for finding the optical behavior.

            //(We're going to use the private variables behind the setters and getters, here,
            // to reduce the overhead due to Lorentz transformations. We'll just calculate the parts we
            // need here, and then we'll pass the results through the setters at the end.)

            //First Account for rigid body drag.
            _viw = _viw * (1.0f - drag * worldDeltaTime);
            _aviw = _aviw * (1.0f - angularDrag * worldDeltaTime);
            Vector3 relVel = _viw.AddVelocity(-playerVel);
            float relVelMag = relVel.magnitude;
            float totalDeltaTime = worldDeltaTime;
            _piw += totalDeltaTime * _viw;
            float distanceFromPlayer = (_piw - playerPositionLastFrame).magnitude;

            //For gravity, we need to account for a potentially accelerated frame
            //if (isFalling && applyGravity)
            //{
            //    UpdateGravity(relDelT);
            //}

            //If the relative velocity happens to be zero, like if the player is at rest
            // relative to terrain objects, we can entirely skip this:
            if (relVelMag > 0.0f)
            {
                
                float spdOfLight = (float)state.SpeedOfLight;

                float deltaDistance = 0.0f;

                float iterationCount = 0;
                float deltaTimeCorrection = totalDeltaTime;

                float oldDistanceFromPlayer;

                //We iterate the mechanics back and forth to narrow in on the exact optical time delta.
                //(Some of these iterations will be forward in optical time, and some will be backward,
                // so we can't assume the sign of the time delta correction.)
                do
                {

                    oldDistanceFromPlayer = distanceFromPlayer;
                    distanceFromPlayer = (_piw - playerPositionLastFrame).magnitude;
                    //If the distance INCREASES, the apparent optical time should DECREASE
                    deltaDistance = (distanceFromPlayer - oldDistanceFromPlayer);
                    deltaTimeCorrection = (float)SRelativityUtil.LightDelayWithGravity(-deltaDistance / spdOfLight);

                    _viw = _viw * (1.0f - drag * deltaTimeCorrection);
                    _aviw = _aviw * (1.0f - angularDrag * deltaTimeCorrection);
                    _piw += deltaTimeCorrection * _viw;

                    //if (isFalling && applyGravity)
                    //{
                    //    UpdateGravity(SRelativity.AccelerateTime(cameraTRevCtrl.GetUnreversedAcceleration(), relVel, iterateDelT));
                    //    relVel = _viw.AddSRVelocity(-playerVel);
                    //    //relVelMag = relVel.magnitude;
                    //}

                    totalDeltaTime += deltaTimeCorrection;

                    iterationCount++;
                }
                while (Mathf.Abs(deltaTimeCorrection) > 0.001f && iterationCount < maxIterations);

                relVelMag = relVel.magnitude;
            }

            //if (totalDeltaTimeCorrection <= 0.0f)
            //{
            //    detectCollisions = false;
            //}
            //else
            //{
            //    detectCollisions = true;
            //}

            //We were putting off the transformations in these setters, to reduce overhead.
            // First make sure the transform is moved to the true real space position.
            transform.Translate(_piw.RealToMinkowski(-_viw) - transform.position);
            // Then trip all the setters.
            piw = _piw;
            viw = _viw;
            aviw = _aviw;

            //Update the rotation due to the apparent change in time, times the angular velocity.
            Vector3 angInc = totalDeltaTime * Mathf.Rad2Deg * _aviw;
            float angIncMag = angInc.magnitude;
            if (angIncMag > 0.0f)
            {
                Quaternion rot = Quaternion.AngleAxis(angIncMag, Vector3.forward);
                transform.localRotation = rot * transform.localRotation;
            }

            //Save the player position to account for player velocity in the next frame:
            playerPositionLastFrame = state.playerTransform.position;

            //We can now update the object's time delta
            deltaOpticalTime = totalDeltaTime;
        }

        //This is a reference type to package collision points with collision normal vectors
        private class PointAndNorm
        {
            public Vector3 point;
            public Vector3 normal;
        }

        public void OnCollisionStay(Collision collision)
        {
            if (myRigidbody.isKinematic)
            {
                return;
            }

            if (!didCollide)
            {
                OnCollisionEnter(collision);
            }

            //if (myRigidbody.IsSleeping())
            //{
            //    CorrectPosition(collision, true);
            //}
            //else
            //{
            //    RelativisticObject otherRO = collision.gameObject.GetComponent<RelativisticObject>();
            //    PhysicMaterial otherMaterial = otherRO.physicMaterial;
            //    PhysicMaterial myMaterial = this.physicMaterial;
            //    float combFriction = CombinePhysics(myMaterial.frictionCombine, myMaterial.staticFriction, otherMaterial.staticFriction);
            //    float combRestCoeff = CombinePhysics(myMaterial.bounceCombine, myMaterial.bounciness, otherMaterial.bounciness);

            //    //Tangental relationship scales normalized "bounciness" to a Young's modulus
            //    float combYoungsModulus;
            //    if (combRestCoeff < 1.0f)
            //    {
            //        combYoungsModulus = Mathf.Tan(combRestCoeff);
            //        //If the Young's modulus is higher than a realistic material, cap it.
            //        if (combYoungsModulus > maxYoungsModulus) combYoungsModulus = maxYoungsModulus;
            //    }
            //    else
            //    {
            //        //If the coeffecient of restitution is one, set the Young's modulus to max:
            //        combYoungsModulus = maxYoungsModulus;
            //    }

            //    PointAndNorm contactPoint = DecideContactPoint(collision);
            //    ApplyPenalty(collision, otherRO, contactPoint, combFriction, combYoungsModulus);
            //}
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (didCollide || myRigidbody == null || myCollider == null || myRigidbody.isKinematic)
            {
                return;
            }

            didCollide = true;
            //Debug.Log("Entered");

            PointAndNorm contactPoint = DecideContactPoint(collision);
            if (contactPoint == null)
            {
                return;
            }

            RelativisticObject otherRO = collision.gameObject.GetComponent<RelativisticObject>();
            PhysicMaterial otherMaterial = otherRO.physicMaterial;
            PhysicMaterial myMaterial = this.physicMaterial;
            float combFriction = CombinePhysics(myMaterial.frictionCombine, myMaterial.staticFriction, otherMaterial.staticFriction);
            float combRestCoeff = CombinePhysics(myMaterial.bounceCombine, myMaterial.bounciness, otherMaterial.bounciness);

            //Tangental relationship scales normalized "bounciness" to a Young's modulus
            float combYoungsModulus;
            if (combRestCoeff < 1.0f)
            {
                combYoungsModulus = Mathf.Tan(combRestCoeff);
                //If the Young's modulus is higher than a realistic material, cap it.
                if (combYoungsModulus > maxYoungsModulus) combYoungsModulus = maxYoungsModulus;
            }
            else
            {
                //If the coeffecient of restitution is one, set the Young's modulus to max:
                combYoungsModulus = maxYoungsModulus;
            }
            Collide(collision, otherRO, contactPoint, combRestCoeff, combFriction, combYoungsModulus, (collision.rigidbody == null) || (collision.rigidbody.isKinematic));
        }

        private float CombinePhysics(PhysicMaterialCombine physMatCombine, float mine, float theirs)
        {
            float effectiveValue = 0.0f;
            switch (physMatCombine)
            {
                case PhysicMaterialCombine.Average:
                    effectiveValue = (mine + theirs) * 0.5f;
                    break;
                case PhysicMaterialCombine.Maximum:
                    effectiveValue = Mathf.Max(mine, theirs);
                    break;
                case PhysicMaterialCombine.Minimum:
                    effectiveValue = Mathf.Min(mine, theirs);
                    break;
                default:
                    effectiveValue = mine;
                    break;
            }
            return effectiveValue;
        }

        private PointAndNorm DecideContactPoint(Collision collision)
        {
            PointAndNorm contactPoint;
            if (collision.contacts.Length == 0)
            {
                return null;
            }
            else if (collision.contacts.Length == 1)
            {
                contactPoint = new PointAndNorm()
                {
                    point = collision.contacts[0].point,
                    normal = collision.contacts[0].normal
                };
            }
            else
            {
                contactPoint = new PointAndNorm();
                for (int i = 0; i < collision.contacts.Length; i++)
                {
                    contactPoint.point += collision.contacts[i].point;
                    contactPoint.normal += collision.contacts[i].normal;
                }
                contactPoint.point = 1.0f / (float)collision.contacts.Length * contactPoint.point;
                contactPoint.normal.Normalize();
            }
            if ((contactPoint.point - transform.position).sqrMagnitude == 0.0f)
            {
                contactPoint.point = 0.001f * collision.collider.transform.position;
            }
            return contactPoint;
        }

        private void Collide(Collision collision, RelativisticObject otherRO, PointAndNorm contactPoint, float combRestCoeff, float combFriction, float combYoungsModulus, bool isReflected)
        {
            //We grab the velocities from the RelativisticObject rather than the Rigidbody,
            // since the RelativisticObject has not had its physics affected yet.
            float mass = myRigidbody.mass;
            Vector3 myVel = viw;
            preCollisionVel3 = viw;
            Vector3 myAngVel = aviw;
            preCollisionAngVel3 = aviw;
            Rigidbody otherRB = collision.rigidbody;
            Vector3 otherVel = otherRO.viw;
            Vector3 otherAngVel = otherRO.aviw;

            Vector3 playerVel = state.PlayerVelocityVector;
            Vector3 myPRelVel = myVel.AddVelocity(-playerVel);
            Vector3 otherPRelVel = otherVel.AddVelocity(-playerVel);

            //We want to find the contact offset relative the centers of mass of in each object's inertial frame;
            Vector3 myLocPoint = (contactPoint.point - (myRigidbody.centerOfMass + transform.position)).InverseContractLengthBy(myPRelVel);
            Vector3 otLocPoint = (contactPoint.point - (otherRB.centerOfMass + otherRB.position)).InverseContractLengthBy(otherPRelVel);
            Vector3 myAngTanVel = Vector3.Cross(myAngVel, myLocPoint);
            Vector3 myParVel = myVel.AddVelocity(myAngTanVel);
            Vector3 otherAngTanVel = Vector3.Cross(otherAngVel, otLocPoint);
            Vector3 otherContactVel = otherVel.AddVelocity(otherAngTanVel);
            //Boost to the inertial frame where my velocity is 0 along the line of action:
            Vector3 relVel = otherContactVel.AddVelocity(-myParVel);
            Vector3 lineOfAction = (-contactPoint.normal).RealToMinkowski(-myParVel).normalized;
            //parra is the remaining projection of the relative velocity on the unit of action:
            Vector3 parra = Vector3.Project(relVel, lineOfAction);
            //perp is a perpendicular projection:
            Vector3 perp = relVel - parra;
            //With parra and perp, we can find the velocity that removes the perpendicular component of motion:
            Vector3 relVelPerp = -parra.GetGamma() * perp;
            Vector3 relVelParra = relVel.AddVelocity(relVelPerp);
            lineOfAction = (-contactPoint.normal).RealToMinkowski(relVelPerp).normalized;
            //Rotate so our parrallel velocity is on the forward vector:
            Quaternion rotRVtoForward = Quaternion.FromToRotation(lineOfAction, Vector3.forward);
            relVelParra = rotRVtoForward * relVelParra;
            //Find the relative rapidity on the line of action, where the perpendicular component of the velocity is 0:
            Vector3 rapidityOnLoA = relVelParra.GetGamma() * relVelParra;
            otLocPoint = otLocPoint.ContractLengthBy(relVel);

            //Rotate my relative contact point:
            Vector3 rotatedLoc = Quaternion.Inverse(transform.rotation) * myLocPoint;
            //The relative contact point is the lever arm of the torque:
            float myMOI = Vector3.Dot(this.inertiaTensor, new Vector3(rotatedLoc.x * rotatedLoc.x, rotatedLoc.y * rotatedLoc.y, rotatedLoc.z * rotatedLoc.z));
            rotatedLoc = Quaternion.Inverse(otherRB.transform.rotation) * otLocPoint;
            //In special relativity, the impulse relates the change in rapidities, rather than the change in velocities.
            float impulse;
            if (isReflected)
            {
                impulse = -rapidityOnLoA.magnitude * (combRestCoeff + 1.0f) / (1.0f / mass + Vector3.Dot(lineOfAction, Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, lineOfAction), myLocPoint)));
            }
            else
            {
                float otherMOI = Vector3.Dot(otherRO.inertiaTensor, new Vector3(rotatedLoc.x * rotatedLoc.x, rotatedLoc.y * rotatedLoc.y, rotatedLoc.z * rotatedLoc.z));
                impulse = -rapidityOnLoA.magnitude * (combRestCoeff + 1.0f) / (1.0f / mass + 1.0f / otherRB.mass + Vector3.Dot(lineOfAction, Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, lineOfAction), myLocPoint)) + Vector3.Dot(lineOfAction, Vector3.Cross(1.0f / otherMOI * Vector3.Cross(otLocPoint, lineOfAction), otLocPoint)));
            }

            impulse *= (1.0f + combFriction);

            //We will be applying penalty methods next.
            //To conserve energy, we subtract the energy of "spring" deformation at the initial time of collision,
            // and then we immediately start applying Hooke's law to make up the difference.
            //The impulse has units of momentum. By the definition of the kinetic energy as K=p^2/2, what is the loss of momentum?
            //PointAndNorm dupePointAndNorm = new PointAndNorm()
            //{
            //    normal = contactPoint.normal,
            //    point = contactPoint.point
            //};
            //float penDepth = GetPenetrationDepth(collision, myPRelVel, ref dupePointAndNorm);
            ////Treat the Young's modulus rather as just a 1 dimensional spring constant:
            //float momentumLoss = Mathf.Sqrt(combYoungsModulus) * penDepth;
            //impulse -= momentumLoss;
            //We still need to apply a spring constant at the end.

            //The change in rapidity on the line of action:
            Vector3 finalParraRapidity = myParVel.GetGamma() * myParVel + impulse / mass * lineOfAction;
            //The change in rapidity perpendincular to the line of action:
            Vector3 finalPerpRapidity = myAngTanVel.GetGamma() * myAngTanVel + Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, impulse * lineOfAction), myLocPoint);
            Vector3 tanVelFinal = finalPerpRapidity.GetInverseGamma() * finalPerpRapidity;
            //This is a hack. We save the new velocities to overwrite the Rigidbody velocities on the next frame:
            collisionResultVel3 = finalParraRapidity.GetInverseGamma() * finalParraRapidity;
            //If the angle of the torque is close to 0 or 180, we have rounding problems:
            float angle = Vector3.Angle(myAngVel, myLocPoint);
            if (angle > 2.0f && angle < 178.0f)
            {
                collisionResultAngVel3 = Vector3.Cross(tanVelFinal, myLocPoint) / myLocPoint.sqrMagnitude;
            }
            else
            {
                collisionResultAngVel3 = myAngVel;
            }
            //Velocity overwrite will come on next frame:
            oldCollisionResultVel3 = collisionResultVel3;
            oldCollisionResultAngVel3 = collisionResultAngVel3;
            didCollide = true;
            collideTimeStart = (float)state.TotalTimeWorld;

            //Now, we start applying penalty methods:
            //ApplyPenalty(collision, otherRO, contactPoint, combFriction, combYoungsModulus);
        }

        //EXPERIMENTAL PENALTY METHOD CODE BELOW
        //private void ApplyPenalty(Collision collision, RelativisticObject otherRO, PointAndNorm contactPoint, float combFriction, float combYoungsModulus)
        //{
        //    Rigidbody otherRB = collision.rigidbody;
        //    Vector3 myVel = oldCollisionResultVel3;
        //    Vector3 otherVel = otherRO.oldCollisionResultVel3;
        //    Vector3 playerVel = state.PlayerVelocityVector;
        //    Vector3 myPRelVel = myVel.AddVelocity(-playerVel);
        //    Vector3 otherPRelVel = otherVel.AddVelocity(-playerVel);
        //    Vector3 piw = transform.position;
        //    float gamma = myPRelVel.GetGamma();

        //    Vector3 otherAngVel = otherRO.oldCollisionResultAngVel3;

        //    Vector3 lineOfAction = contactPoint.normal;

        //    float penDist = GetPenetrationDepth(collision, myPRelVel, ref contactPoint);

        //    if (penDist <= 0.0f)
        //    {
        //        return;
        //    }

        //    //We want to find the contact offset relative the centers of mass of in each object's inertial frame;
        //    Vector3 myLocPoint = (contactPoint.point - (myRigidbody.centerOfMass + transform.position)).InverseContractLengthBy(myPRelVel);
        //    Vector3 otLocPoint = (contactPoint.point - (otherRB.centerOfMass + otherRB.position)).InverseContractLengthBy(otherPRelVel);

        //    Vector3 tangV = Vector3.Cross(oldCollisionResultAngVel3, myLocPoint);
        //    Vector3 relVel = oldCollisionResultAngVel3.AddVelocity(tangV);
        //    relVel = otherVel.AddVelocity(Vector3.Cross(otherAngVel, otLocPoint)).AddVelocity(relVel);

        //    //Boost to my frame:
        //    Vector3 parra = Vector3.Project(relVel, lineOfAction);
        //    Vector3 perp = relVel - parra;
        //    Vector3 relVelPerp = -parra.GetGamma() * perp;
        //    Vector3 relVelParra = relVel.AddVelocity(relVelPerp);
        //    lineOfAction = (-contactPoint.normal).RealToPosition(relVelPerp).normalized;
        //    //Rotate so our parrallel velocity is on the forward vector:
        //    Quaternion rotRVtoForward = Quaternion.FromToRotation(lineOfAction, Vector3.forward);
        //    relVelParra = rotRVtoForward * relVelParra;
        //    //Find the relative rapidity on the line of action, where the perpendicular component of the velocity is 0:
        //    Vector3 rapidityOnLoA = relVelParra.GetGamma() * relVelParra;
        //    otLocPoint = otLocPoint.ContractLengthBy(relVel);

        //    Vector3 rotatedLoc = Quaternion.Inverse(transform.rotation) * myLocPoint;
        //    float myMOI = Vector3.Dot(this.inertiaTensor, new Vector3(rotatedLoc.x * rotatedLoc.x, rotatedLoc.y * rotatedLoc.y, rotatedLoc.z * rotatedLoc.z));

        //    float impulse = (float)(penDist * combYoungsModulus * (Time.fixedDeltaTime * myPRelVel.GetGamma()));
        //    Vector3 myRapidityInit = oldCollisionResultVel3.GetGamma() * oldCollisionResultVel3;
        //    Vector3 unitTangent = oldCollisionResultVel3.normalized;
        //    Vector3 impulseVec = (impulse * lineOfAction + impulse * combFriction * unitTangent);
        //    Vector3 myRapidityFinal = myRapidityInit + impulseVec / myRigidbody.mass;
        //    Vector3 myTanRapidityInit = tangV.GetGamma() * tangV;
        //    Vector3 myTanRapidityFinal = myTanRapidityInit + Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, impulseVec), myLocPoint);
        //    Vector3 tangVFinal = myTanRapidityFinal.GetInverseGamma() * myTanRapidityFinal;
        //    collisionResultVel3 = myRapidityFinal.GetInverseGamma() * myRapidityFinal;

        //    float angle = Vector3.Angle(aviw, myLocPoint);
        //    if ((otherRB.isKinematic) && collision.contacts.Length > 2)
        //    {
        //        collisionResultAngVel3 = Vector3.zero;
        //    }
        //    else if (angle > 2.0f && angle < 178.0f)
        //    {
        //        collisionResultAngVel3 = Vector3.Cross(tangVFinal, -myLocPoint) / myLocPoint.sqrMagnitude;
        //    }

        //    if (collision.contacts.Length > 1)
        //    {
        //        CorrectPosition(collision, false);
        //    }
        //}

        //private float GetPenetrationDepth(Collision collision, Vector3 velocity, ref PointAndNorm contactPoint)
        //{
        //    Vector3 myPRelVel = oldCollisionResultVel3.AddVelocity(-state.PlayerVelocityVector);
        //    Vector3 piw = transform.position;
        //    float gamma = myPRelVel.GetGamma();

        //    Rigidbody otherRB = collision.rigidbody;
        //    RelativisticObject otherRO = collision.gameObject.GetComponent<RelativisticObject>();
        //    Vector3 otherVel = otherRO.oldCollisionResultVel3;
        //    Vector3 otherAngVel = otherRO.oldCollisionResultAngVel3;

        //    Vector3 lineOfAction = contactPoint.normal;
        //    Vector3 testNormal;
        //    float penDist = 0.0f;
        //    float penTest = 0.0f;
        //    float penPart = 0.0f;
        //    float startDist = 10.0f * transform.lossyScale.sqrMagnitude;
        //    RaycastHit hitInfo;
        //    foreach (ContactPoint point in collision.contacts)
        //    {
        //        testNormal = point.normal;
        //        Ray ray = new Ray(piw + startDist * testNormal, -testNormal);
        //        if (collision.collider.Raycast(ray, out hitInfo, startDist * 2.0f))
        //        {
        //            penPart = hitInfo.distance - startDist;
        //            ray = new Ray(piw - startDist * testNormal, testNormal);
        //            if (myCollider.Raycast(ray, out hitInfo, startDist))
        //            {
        //                penTest = ((startDist - hitInfo.distance) - penPart) * gamma;
        //            }
        //            else
        //            {
        //                penTest = 0.0f;
        //            }
        //        }
        //        else
        //        {
        //            penTest = 0.0f;
        //        }
        //        if (penTest > penDist)
        //        {
        //            penDist = penTest;
        //            lineOfAction = testNormal;
        //            contactPoint = new PointAndNorm()
        //            {
        //                point = point.point,
        //                normal = point.normal
        //            };
        //        }
        //    }

        //    return penDist * velocity.GetInverseGamma();
        //}

        //private void CorrectPosition(Collision collision, bool glueToSurface)
        //{
        //    PointAndNorm contactPoint = null;
        //    RaycastHit hitInfo;
        //    Vector3 testNormal = Vector3.up;
        //    float penTest;
        //    float penDist = 100.0f;
        //    float startDist;
        //    foreach (ContactPoint point in collision.contacts)
        //    {
        //        testNormal = point.normal;
        //        startDist = 10.0f * Vector3.Dot(transform.lossyScale, transform.lossyScale);
        //        Ray ray = new Ray(transform.position + startDist * testNormal, -testNormal);
        //        if (collision.collider.Raycast(ray, out hitInfo, startDist * 2.0f))
        //        {
        //            penTest = hitInfo.distance - startDist;
        //            ray = new Ray(transform.position - startDist * testNormal, testNormal);
        //            if (myCollider.Raycast(ray, out hitInfo, startDist))
        //            {
        //                penTest = ((startDist - hitInfo.distance) - penTest);
        //            }
        //            else
        //            {
        //                penTest = 0.0f;
        //            }
        //        }
        //        else
        //        {
        //            penTest = 100.0f;
        //        }
        //        if (penTest < penDist)
        //        {
        //            penDist = penTest;
        //            contactPoint = new PointAndNorm()
        //            {
        //                point = point.point,
        //                normal = point.normal
        //            };
        //        }
        //    }
        //    //penDist -= 0.05f;
        //    Vector3 newVel = collisionResultVel3;
        //    if (contactPoint != null && glueToSurface || penDist > 0.0f)
        //    {
        //        Vector3 disp;
        //        try
        //        {
        //            disp = penDist * contactPoint.normal;
        //        }
        //        catch
        //        {
        //            return;
        //        }
        //        transform.position += disp;
        //        float recipGamma = 1.0f / oldCollisionResultVel3.GetGamma();
        //        Rigidbody otherRB = collision.rigidbody;
        //        if (otherRB.isKinematic)
        //        {
        //            Vector3 parra = Vector3.Project(oldCollisionResultVel3, contactPoint.normal);
        //            Vector3 perp = collisionResultVel3 - parra;
        //            Vector3 relVelPerp = -parra.GetGamma() * perp;
        //            newVel = newVel.AddVelocity(relVelPerp);
        //            List<Vector3> directions = new List<Vector3>();
        //            directions.Add(transform.up);
        //            directions.Add(-transform.up);
        //            directions.Add(transform.right);
        //            directions.Add(-transform.right);
        //            directions.Add(transform.forward);
        //            directions.Add(-transform.forward);
        //            float angle, minAngle = 180;
        //            Vector3 closestDirection = Vector3.forward;
        //            for (int i = 0; i < directions.Count; i++)
        //            {
        //                angle = Vector3.Angle(directions[i], contactPoint.normal);
        //                if (angle < minAngle)
        //                {
        //                    minAngle = angle;
        //                    closestDirection = directions[i];
        //                }
        //            }
        //            Quaternion rotToStraight = Quaternion.FromToRotation(closestDirection, contactPoint.normal);
        //            transform.rotation = Quaternion.RotateTowards(Quaternion.identity, rotToStraight, Mathf.Min((float)(90.0 * state.DeltaTimeWorld), minAngle)) * transform.rotation;
        //            if (transform.rotation == rotToStraight)
        //            {
        //                Sleep();
        //            }
        //            float gammaPowT = Mathf.Pow(oldCollisionResultVel3.AddVelocity(state.PlayerVelocityVector).GetGamma(), (float)(0.5 * 250.0 * state.DeltaTimeWorld));
        //            collisionResultVel3 = gammaPowT * newVel;
        //        }
        //    }
        //}

        //private void Sleep()
        //{
        //    viw = Vector3.zero;
        //    collisionResultVel3 = Vector3.zero;
        //    myRigidbody.velocity = Vector3.zero;
        //    aviw = Vector3.zero;
        //    collisionResultAngVel3 = Vector3.zero;
        //    myRigidbody.angularVelocity = Vector3.zero;
        //    myRigidbody.Sleep();
        //}
        #endregion
    }
}