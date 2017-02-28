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
        // we need to manually update some relativistic rigid body mechanics, for the time-being.

        public Vector3 initialViw;
        private Vector3 _viw = Vector3.zero;
        public Vector3 viw
        {
            get
            {
                return _viw;
            }
            //Changing velocities lose continuity of position,
            // unless we transform the world position to optical position with the old velocity,
            // and inverse transform the optical position with the new the velocity.
            // (This keeps the optical and Minkowski position fixed.)
            set
            {
                //This makes instantiation cleaner:
                initialViw = value;

                //Under instantaneous changes in velocity, the optical position should be invariant:
                Vector3 playerPos = state.transform.position;
                Vector3 otwEst = transform.position.WorldToOptical(_viw, playerPos).WorldToOptical(-value, playerPos);
                transform.position = transform.position.WorldToOptical(_viw, playerPos, state.PlayerVelocityVector).OpticalToWorldSearch(value, playerPos, state.PlayerVelocityVector, otwEst);
                _viw = value;
                //Also update the Rigidbody, if any
                if (myRigidbody != null && !state.MovementFrozen)
                {
                    myRigidbody.velocity = value / (float)(state.SqrtOneMinusVSquaredCWDividedByCSquared * GetGtt());
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
                initialAviw = value;
                _aviw = value;
                if (myRigidbody != null && !state.MovementFrozen)
                {
                    //Changes in angular velocity do not change the shader mapping to optical space.
                    myRigidbody.angularVelocity = value / (float)(state.SqrtOneMinusVSquaredCWDividedByCSquared * GetGtt());
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

        public bool useGravity;

        //This is a velocity we "sleep" the rigid body at:
        private const float sleepVelocity = 0.05f;
        //Once we're below the sleep velocity threshold, this is how many frames we wait for
        // before sleeping the object:
        private const int sleepFrameDelay = 3;
        private int sleepFrameCounter;
        private List<Collider> sleepingOnColliders;
        //TODO: Rigidbody doesn't stay asleep. Figure out why, and get rid of this:
        private bool isSleeping;
        //Length contraction and rounding error pulls sleeping objects off the surfaces they rest on.
        // Save the original orientation on sleep, and force it back while sleeping.
        private Quaternion sleepRotation;
        //This is a cap on penalty method collision.
        //(It's roughly the Unity units equivalent of Young's Modulus of diamond.)
        private const float maxYoungsModulus = 1220.0e9f;
        #endregion

        //Radians to degrees
        private const float RAD_2_DEG = 57.2957795f;
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
        //If we have a collider to transform, we cache it here
        private Collider myCollider;
        //If we specifically have a mesh collider, we need to know to transform the verts of the mesh itself.
        private bool myColliderIsMesh;
        private bool myColliderIsBox;
        //We create a new collider mesh, so as not to interfere with primitives, and reuse it
        private Mesh trnsfrmdMesh;
        //If we have a Rigidbody, we cache it here
        private Rigidbody myRigidbody;
        //The center of mass calculation in the rigidbody becomes non-physical when we transform the collider
        public Vector3 opticalWorldCenterOfMass { get; set; }

        //Did we collide last frame?
        private bool didCollide;
        //What was the translational velocity result?
        private Vector3 collisionResultVel3;
        //What was the angular velocity result?
        private Vector3 collisionResultAngVel3;
        //Time when the collision started
        private double collideTimeStart;
        //Collision-softening time
        //public float collideSoftenTime = 0.2f;
        //For penalty methods, we need an intermediate collision velocity result
        private Vector3 oldCollisionResultVel3;
        private Vector3 oldCollisionResultAngVel3;

        ComputeBuffer paramsBuffer;
        ComputeBuffer vertBuffer;

        //We need to freeze any attached rigidbody if the world states is frozen 
        private bool wasKinematic = false;
        private bool wasFrozen = false;
        private void UpdateMeshCollider()
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

            if (!GetComponent<MeshRenderer>().enabled /*|| colliderShaderParams.gtt == 0*/)
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
            //colliderShaderParams.gtt = 
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
            if (paramsBuffer == null)
            {
                paramsBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(colliderShaderParams));
            }
            paramsBuffer.SetData(spa);
            if (vertBuffer == null)
            {
                vertBuffer = new ComputeBuffer(rawVerts.Length, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
            }
            vertBuffer.SetData(rawVerts);
            int kernel = colliderShader.FindKernel("CSMain");
            colliderShader.SetBuffer(kernel, "glblPrms", paramsBuffer);
            colliderShader.SetBuffer(kernel, "verts", vertBuffer);
            colliderShader.Dispatch(kernel, rawVerts.Length, 1, 1);
            vertBuffer.GetData(trnsfrmdMeshVerts);

            //Change mesh:
            trnsfrmdMesh.vertices = trnsfrmdMeshVerts;
            ((MeshCollider)myCollider).sharedMesh = trnsfrmdMesh;
            //Cache actual world center of mass, and then reset local (rest frame) center of mass:
            myRigidbody.ResetCenterOfMass();
            meshFilter.mesh.RecalculateBounds();
            meshFilter.mesh.RecalculateNormals();
            //Mobile only:
            //meshFilter.mesh.RecalculateTangents();
            opticalWorldCenterOfMass = myRigidbody.worldCenterOfMass;
            myRigidbody.centerOfMass = initCOM;

            
            
        }

        void OnDestroy()
        {
            if (paramsBuffer != null) paramsBuffer.Release();
            if (vertBuffer != null) vertBuffer.Release();
        }

        void Awake()
        {
            //Get the player's GameState, use it later for general information
            state = GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>();
        }

        // Get the start time of our object, so that we know where not to draw it
        public void SetStartTime()
        {
            float timeDelayToPlayer = (float)Math.Sqrt((transform.position - state.playerTransform.position).sqrMagnitude / state.SpeedOfLightSqrd);
            timeDelayToPlayer /= (float)(GetGtt() * state.SqrtOneMinusVSquaredCWDividedByCSquared);
            startTime = (float)(state.TotalTimeWorld - timeDelayToPlayer);
            if (GetComponent<MeshRenderer>() != null)
                GetComponent<MeshRenderer>().enabled = false;
        }
        //Set the death time, so that we know at what point to destroy the object in the player's view point.
        public virtual void SetDeathTime()
        {
            float timeDelayToPlayer = (float)Math.Sqrt((transform.position - state.playerTransform.position).sqrMagnitude / state.SpeedOfLightSqrd);
            timeDelayToPlayer /= (float)(GetGtt() * state.SqrtOneMinusVSquaredCWDividedByCSquared);
            deathTime = (float)(state.TotalTimeWorld - timeDelayToPlayer);
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
                vertsToTransform = new Vector3[rawVerts.Length];
                System.Array.Copy(rawVerts, vertsToTransform, rawVerts.Length);
            }
            else
            {
                vertsToTransform = rawVerts;
            }
        }

        void Start()
        {
            _viw = initialViw;
            _aviw = initialAviw;
            isSleeping = false;
            sleepingOnColliders = new List<Collider>();
            didCollide = false;
            sleepFrameCounter = 0;
            myRigidbody = GetComponent<Rigidbody>();

            UpdateCollider();

            if (myRigidbody != null)
            {
                //Native rigidbody gravity should never be used:
                myRigidbody.useGravity = false;
            }

            //Get the meshfilter
            if (isParent)
            {
                CombineParent();
            }
            meshFilter = GetComponent<MeshFilter>();

            //Get the vertices of our mesh
            if (meshFilter != null)
            {
                rawVerts = meshFilter.mesh.vertices;
                meshFilter.mesh.MarkDynamic();
            }
            else
                rawVerts = null;

            //Once we have the mesh vertices, allocate and immediately transform the collider:
            if (myColliderIsMesh && myCollider != null)
            {
                trnsfrmdMeshVerts = new Vector3[rawVerts.Length];
            }

            checkSpeed();

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

        private void UpdateCollider()
        {
            myCollider = GetComponent<MeshCollider>();
            if (myCollider != null)
            {
                trnsfrmdMesh = Instantiate(((MeshCollider)myCollider).sharedMesh);
                myColliderIsMesh = true;
                myColliderIsBox = false;
            }
            else
            {
                myCollider = GetComponent<BoxCollider>();
                if (myCollider != null)
                {
                    myColliderIsBox = true;
                }
                else
                {
                    myColliderIsBox = false;
                    myCollider = GetComponent<Collider>();
                }

            }
        }

        private void EnforceCollision()
        {
            oldCollisionResultVel3 = collisionResultVel3;
            oldCollisionResultAngVel3 = collisionResultAngVel3;

            if (didCollide)
            {
                //float fracCollTime = (float)((state.TotalTimeWorld - collideTimeStart) / collideSoftenTime);
                //if (fracCollTime < 1.0f)
                //{
                //    viw = Vector3.Lerp(preCollisionVel3, collisionResultVel3, fracCollTime);
                //    aviw = Vector3.Lerp(preCollisionAngVel3, collisionResultAngVel3, fracCollTime);
                //    myRigidbody.angularVelocity = aviw;
                //}
                //else
                //{
                    //Finish and shut off enforcement
                    viw = collisionResultVel3;
                    aviw = collisionResultAngVel3;
                    myRigidbody.angularVelocity = aviw;
                    didCollide = false;
                //}
            }
        }

        public void UpdateGravity()
        {
            Vector3 tempViw = viw;
            tempViw += Physics.gravity * Time.deltaTime;
            tempViw = tempViw.RapidityToVelocity();
            float test = tempViw.x + tempViw.y + tempViw.z;
            if (!float.IsNaN(test) && !float.IsInfinity(test))
            {
                viw = tempViw;
                //Don't exceed max speed:
                checkSpeed();
            }
        }

        public void Update()
        {
            EnforceCollision();

            if (myRigidbody != null && viw.sqrMagnitude <= sleepVelocity * sleepVelocity && aviw.sqrMagnitude < sleepVelocity * sleepVelocity)
            {
                sleepFrameCounter++;
                if (sleepFrameCounter >= sleepFrameDelay)
                {
                    sleepFrameCounter = sleepFrameDelay;
                    Sleep();
                }
            }
            else
            {
                sleepFrameCounter = 0;
            }

            if (myRigidbody != null && useGravity && state.SqrtOneMinusVSquaredCWDividedByCSquared != 0 )
            {
                UpdateGravity();
            }

            //Grab our renderer.
            MeshRenderer tempRenderer = GetComponent<MeshRenderer>();

            //Update the rigidbody reference.
            myRigidbody = GetComponent<Rigidbody>();
            //Update the collider reference.
            UpdateCollider();

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
                        tempRenderer.materials[i].SetVector("_sav", new Vector4(tempAviw.x, tempAviw.y, tempAviw.z, 0));
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

                //update our viw and set the rigid body proportionally
                if (myRigidbody != null)
                {
                    if (!double.IsNaN((double)state.SqrtOneMinusVSquaredCWDividedByCSquared) && (float)state.SqrtOneMinusVSquaredCWDividedByCSquared != 0)
                    //if (!double.IsNaN((double)state.InverseAcceleratedGamma) && (float)state.InverseAcceleratedGamma != 0)
                    {
                        //Dragging probably happens intrinsically in the rest frame,
                        // so it acts on the rapidity. (Drag is computationally expensive
                        // due to tripping the velocity setter every frame.)
                        // TODO: Replace with drag force
                        //Vector3 rapidity = (float)(1.0 - drag * state.DeltaTimeWorld) * viw.Gamma() * viw;
                        //viw = rapidity.RapidityToVelocity();
                        //aviw = (float)(1.0 - angularDrag * state.DeltaTimeWorld) * aviw;

                        Vector3 tempViw = viw;
                        Vector3 tempAviw = aviw;
                        //ASK RYAN WHY THIS WAS DIVIDED BY THIS
                        tempViw /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
                        tempAviw /= (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
                        //Attempt to correct for acceleration:
                        if (state.SpeedOfLightSqrd != 0)
                        {
                            //Attempt to correct for acceleration:
                            float gtt = GetGtt();
                            tempViw /= gtt;
                            tempAviw /= gtt;
                            //colliderShaderParams.gtt = gtt;
                        }
                        myRigidbody.velocity = tempViw;
                        myRigidbody.angularVelocity = tempAviw;
                    }


                }
            }
            //If nothing is null, then set the object to standstill, but make sure its rigidbody actually has a velocity.
            else if (meshFilter != null && tempRenderer != null && myRigidbody != null)
            {
                myRigidbody.velocity = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;
            }

            //If we have a MeshCollider and a compute shader, transform the collider verts relativistically:
            if (myCollider != null)
            {
                if (myColliderIsMesh && colliderShader != null)
                {
                    UpdateMeshCollider();
                }
                //If we have a BoxCollider, transform its center to its optical position
                else if (myColliderIsBox)
                {
                    Vector3 initCOM = Vector3.zero;
                    if (myRigidbody != null)
                    {
                        initCOM = myRigidbody.centerOfMass;
                    }
                    Vector3 playerPos = state.playerTransform.position;
                    Vector3 playerVel = state.PlayerVelocityVector;
                    opticalWorldCenterOfMass = transform.position.WorldToOptical(viw, playerPos, playerVel);
                    ((BoxCollider)myCollider).center = transform.InverseTransformPoint(opticalWorldCenterOfMass);
                    if (myRigidbody != null)
                    {
                        myRigidbody.centerOfMass = initCOM;
                    }
                }
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
            //float maxSpeedSqr = (float)((state.MaxSpeed - 0.01f) * (state.MaxSpeed - 0.01f));
            //if (trnsfrmdMeshVerts != null)
            //{
            //    for (int i = 0; i < trnsfrmdMeshVerts.Length; i++)
            //    {
            //        float radius = trnsfrmdMeshVerts[i].magnitude;
            //        Vector3 tangentialVel = viw.AddVelocity((transform.rotation * trnsfrmdMeshVerts[i]).InverseContractLengthBy(viw).magnitude * aviw);
            //        float tanVelMagSqr = tangentialVel.sqrMagnitude;
            //        if (tanVelMagSqr > maxSpeedSqr)
            //        {
            //            tangentialVel = tangentialVel.normalized * (float)(state.MaxSpeed - 0.01f);
            //            aviw = (-viw).AddVelocity(tangentialVel.normalized * (float)(state.MaxSpeed - 0.01f) / radius);
            //        }
            //    }
            //}
        }

        private void checkCollisionSpeed()
        {
            if (collisionResultVel3.magnitude > state.MaxSpeed - .01)
            {
                oldCollisionResultVel3 = oldCollisionResultVel3.normalized * (float)(state.MaxSpeed - .01f);
                collisionResultVel3 = oldCollisionResultVel3;
            }

            //The tangential velocities of each vertex should also not be greater than the maximum speed.
            // (This is a relatively computationally costly check, but it's good practice.
            //float maxSpeedSqr = (float)((state.MaxSpeed - 0.01f) * (state.MaxSpeed - 0.01f));
            //if (rawVerts != null)
            //{
            //    for (int i = 0; i < rawVerts.Length; i++)
            //    {
            //        float radius = trnsfrmdMeshVerts[i].magnitude;
            //        Vector3 tangentialVel = viw.AddVelocity((transform.rotation * rawVerts[i]).magnitude * oldCollisionResultAngVel3);
            //        float tanVelMagSqr = tangentialVel.sqrMagnitude;
            //        if (tanVelMagSqr > maxSpeedSqr)
            //        {
            //            tangentialVel = tangentialVel.normalized * (float)(state.MaxSpeed - 0.01f);
            //            oldCollisionResultAngVel3 = (-viw).AddVelocity(tangentialVel.normalized * (float)(state.MaxSpeed - 0.01f) / radius);
            //            collisionResultVel3 = oldCollisionResultVel3;
            //        }
            //    }
            //}
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
            else if (isSleeping)
            {
                int i = 0;
                while (i < sleepingOnColliders.Count && !sleepingOnColliders[i].Equals(collision.collider))
                {
                    i++;
                }
                if (i >= sleepingOnColliders.Count)
                {
                    sleepingOnColliders.Add(collision.collider);
                }
                Sleep();
                return;
            }

            //If we made it this far, we shouldn't be sleeping:
            isSleeping = false;

            if (!didCollide)
            {
                OnCollisionEnter(collision);
            }

            RelativisticObject otherRO = collision.gameObject.GetComponent<RelativisticObject>();
            PhysicMaterial otherMaterial = collision.collider.material;
            PhysicMaterial myMaterial = myCollider.material;
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

            PointAndNorm contactPoint = DecideContactPoint(collision);
            ApplyPenalty(collision, otherRO, contactPoint, combFriction, combYoungsModulus);
            didCollide = true;
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (myRigidbody == null || myCollider == null || myRigidbody.isKinematic)
            {
                return;
            }
            else if (isSleeping)
            {
                if (sleepingOnColliders.Count == 0)
                {
                    //In this case, we're sitting just at the point to trip OnCollisionEnter constantly,
                    // but we haven't registered the collider we're sleeping on, yet.
                    sleepingOnColliders.Add(collision.collider);
                    Sleep();
                    return;
                }
                for (int i = 0; i < sleepingOnColliders.Count; i++)
                {
                    //If we're sleeping on this collider, and length contraction made us
                    // come a little unglued, ignore the OnCollisionEnter
                    if (sleepingOnColliders[i].Equals(collision.collider))
                    {
                        Sleep();
                        return;
                    }
                }
            }

            //If we made it this far, we shouldn't be sleeping:
            isSleeping = false;

            //If we made it this far, we're no longer sleeping.
            sleepingOnColliders.Clear();
            didCollide = true;
            //Debug.Log("Entered");

            PointAndNorm contactPoint = DecideContactPoint(collision);
            if (contactPoint == null)
            {
                return;
            }

            RelativisticObject otherRO = collision.gameObject.GetComponent<RelativisticObject>();
            PhysicMaterial otherMaterial = collision.collider.material;
            PhysicMaterial myMaterial = myCollider.material;
            float combFriction = CombinePhysics(myMaterial.frictionCombine, myMaterial.staticFriction, otherMaterial.staticFriction);
            float combRestCoeff = CombinePhysics(myMaterial.bounceCombine, myMaterial.bounciness, otherMaterial.bounciness);

            oldCollisionResultVel3 = viw;
            oldCollisionResultAngVel3 = aviw;
            otherRO.oldCollisionResultVel3 = otherRO.viw;
            otherRO.oldCollisionResultAngVel3 = otherRO.aviw;
            Collide(collision, otherRO, contactPoint, combRestCoeff, combFriction, (collision.rigidbody == null) || (collision.rigidbody.isKinematic));
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

        private void Collide(Collision collision, RelativisticObject otherRO, PointAndNorm contactPoint, float combRestCoeff, float combFriction, bool isReflected)
        {
            //We grab the velocities from the RelativisticObject rather than the Rigidbody,
            // since the Rigidbody velocity is not directly physical.
            float mass = myRigidbody.mass;
            Vector3 myVel = oldCollisionResultVel3;
            Vector3 myAngVel = oldCollisionResultAngVel3;
            Rigidbody otherRB = collision.rigidbody;
            Vector3 otherVel = otherRO.viw;
            Vector3 otherAngVel = otherRO.aviw;

            Vector3 playerPos = state.playerTransform.position;
            Vector3 playerVel = state.PlayerVelocityVector;
            Vector3 myPRelVel = myVel.AddVelocity(-playerVel);
            Vector3 otherPRelVel = otherVel.AddVelocity(-playerVel);

            //We want to find the contact offset relative the centers of mass of in each object's inertial frame;
            Vector3 myLocPoint = (contactPoint.point - opticalWorldCenterOfMass);
            Vector3 otLocPoint = (contactPoint.point - otherRO.opticalWorldCenterOfMass);
            if (myColliderIsMesh)
            {
                //If I have a mesh collider, my collider is affected by length contraction:
                myLocPoint = myLocPoint.InverseContractLengthBy(-myPRelVel);
            }
            if (otherRO.myColliderIsMesh)
            {
                otLocPoint = otLocPoint.InverseContractLengthBy(-otherPRelVel);
            }
            Vector3 myAngTanVel = Vector3.Cross(myAngVel, myLocPoint);
            Vector3 myTotalVel = myVel.AddVelocity(myAngTanVel);
            Vector3 otherAngTanVel = Vector3.Cross(otherAngVel, otLocPoint);
            Vector3 otherTotalVel = otherVel.AddVelocity(otherAngTanVel);
            Vector3 lineOfAction = -contactPoint.normal;
            //Decompose velocity in parallel and perpendicular components:
            Vector3 myParraVel = Vector3.Project(myTotalVel, lineOfAction);
            Vector3 myPerpVel = Vector3.Cross(lineOfAction, Vector3.Cross(lineOfAction, myTotalVel));
            //Boost to the inertial frame where my velocity is entirely along the line of action:
            Vector3 otherContactVel = otherTotalVel.AddVelocity(-myPerpVel);
            //Find the relative velocity:
            Vector3 relVel = otherContactVel.AddVelocity(myParraVel);
            lineOfAction = lineOfAction.InverseContractLengthBy(myPRelVel).normalized.ContractLengthBy(relVel).normalized;
            //Find the relative rapidity on the line of action, where my contact velocity is 0:
            Vector3 rapidityOnLoA = relVel.Gamma() * relVel;
            myLocPoint = myLocPoint.ContractLengthBy(relVel);
            otLocPoint = otLocPoint.ContractLengthBy(relVel);

            //Rotate my relative contact point:
            Vector3 rotatedLoc = Quaternion.Inverse(transform.rotation) * myLocPoint;
            //The relative contact point is the lever arm of the torque:
            float myMOI = Vector3.Dot(myRigidbody.inertiaTensor, new Vector3(rotatedLoc.x * rotatedLoc.x, rotatedLoc.y * rotatedLoc.y, rotatedLoc.z * rotatedLoc.z));
            rotatedLoc = Quaternion.Inverse(otherRB.transform.rotation) * otLocPoint;
            //In special relativity, the impulse relates the change in rapidities, rather than the change in velocities.
            float impulse;
            if (isReflected)
            {
                impulse = -rapidityOnLoA.magnitude * (combRestCoeff + 1.0f) / (1.0f / mass + Vector3.Dot(lineOfAction, Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, lineOfAction), myLocPoint)));
            }
            else
            {
                float otherMOI = Vector3.Dot(otherRB.inertiaTensor, new Vector3(rotatedLoc.x * rotatedLoc.x, rotatedLoc.y * rotatedLoc.y, rotatedLoc.z * rotatedLoc.z));
                impulse = -rapidityOnLoA.magnitude * (combRestCoeff + 1.0f) / (1.0f / mass + 1.0f / otherRB.mass + Vector3.Dot(lineOfAction, Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, lineOfAction), myLocPoint)) + Vector3.Dot(lineOfAction, Vector3.Cross(1.0f / otherMOI * Vector3.Cross(otLocPoint, lineOfAction), otLocPoint)));
            }

            impulse *= (1.0f + combFriction);

            //The change in rapidity on the line of action:
            Vector3 finalParraRapidity = myVel.Gamma() * myParraVel + impulse / mass * lineOfAction;
            //The change in rapidity perpendincular to the line of action:
            Vector3 finalPerpRapidity = myVel.Gamma() * myAngTanVel + Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, impulse * lineOfAction), myLocPoint);
            //Velocities aren't linearly additive in relativity, but rapidities are:

            double spdOfLight = state.SpeedOfLight;
            Vector3 tanVelFinal = finalPerpRapidity.RapidityToVelocity();
            //This is a hack. We save the new velocities to overwrite the Rigidbody velocities on the next frame:
            collisionResultVel3 = finalParraRapidity.RapidityToVelocity();
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
            //In the ideal, it shouldn't be necessary to clamp the speed
            // in order to prevent FTL collision results, but we could
            // still exceed the max speed and come very close to the speed of light
            checkCollisionSpeed();

            //Velocity overwrite will come on next frame:
            oldCollisionResultVel3 = collisionResultVel3;
            oldCollisionResultAngVel3 = collisionResultAngVel3;
            didCollide = true;
            //collideTimeStart = (float)state.TotalTimeWorld;
        }

        //EXPERIMENTAL PENALTY METHOD CODE BELOW
        private const float hookeMultiplier = 1.0f;
        private void ApplyPenalty(Collision collision, RelativisticObject otherRO, PointAndNorm contactPoint, float combFriction, float combYoungsModulus)
        {
            //We grab the velocities from the RelativisticObject rather than the Rigidbody,
            // since the Rigidbody velocity is not directly physical.
            float mass = myRigidbody.mass;
            Vector3 myVel = oldCollisionResultVel3;
            Vector3 myAngVel = oldCollisionResultAngVel3;
            Rigidbody otherRB = collision.rigidbody;
            Vector3 otherVel = otherRO.oldCollisionResultVel3;
            Vector3 otherAngVel = otherRO.oldCollisionResultAngVel3;

            Vector3 playerPos = state.playerTransform.position;
            Vector3 playerVel = state.PlayerVelocityVector;
            Vector3 myPRelVel = myVel.AddVelocity(-playerVel);
            Vector3 otherPRelVel = otherVel.AddVelocity(-playerVel);

            Vector3 lineOfAction = contactPoint.normal;
            Vector3 oPos = transform.position.WorldToOptical(oldCollisionResultVel3, playerPos, playerVel/*, GetGtt()*/);
            float penDist = GetPenetrationDepth(collision, myPRelVel, oPos, ref contactPoint);

            if (penDist <= 0.0f)
            {
                return;
            }

            //We want to find the contact offset relative the centers of mass of in each object's inertial frame;
            Vector3 myLocPoint = (contactPoint.point - opticalWorldCenterOfMass);
            Vector3 otLocPoint = (contactPoint.point - otherRO.opticalWorldCenterOfMass);
            if (myColliderIsMesh)
            {
                //If I have a mesh collider, my collider is affected by length contraction:
                myLocPoint = myLocPoint.InverseContractLengthBy(-myPRelVel);
            }
            if (otherRO.myColliderIsMesh)
            {
                otLocPoint = otLocPoint.InverseContractLengthBy(-otherPRelVel);
            }
            Vector3 myAngTanVel = Vector3.Cross(myAngVel, myLocPoint);
            Vector3 myTotalVel = myVel.AddVelocity(myAngTanVel);
            Vector3 otherAngTanVel = Vector3.Cross(otherAngVel, otLocPoint);
            Vector3 otherTotalVel = otherVel.AddVelocity(otherAngTanVel);

            //Decompose velocity in parallel and perpendicular components:
            Vector3 myParraVel = Vector3.Project(myTotalVel, lineOfAction);
            Vector3 myPerpVel = Vector3.Cross(lineOfAction, Vector3.Cross(lineOfAction, myTotalVel));
            //Boost to the inertial frame where my velocity is entirely along the line of action:
            Vector3 otherContactVel = otherTotalVel.AddVelocity(-myPerpVel);
            //Find the relative velocity:
            Vector3 relVel = otherContactVel.AddVelocity(myParraVel);
            lineOfAction = lineOfAction.InverseContractLengthBy(myPRelVel).normalized.ContractLengthBy(relVel).normalized;

            myLocPoint = myLocPoint.ContractLengthBy(relVel);
            otLocPoint = otLocPoint.ContractLengthBy(relVel);


            //Rotate my relative contact point:
            Vector3 rotatedLoc = Quaternion.Inverse(transform.rotation) * myLocPoint;
            //The relative contact point is the lever arm of the torque:
            float myMOI = Vector3.Dot(myRigidbody.inertiaTensor, new Vector3(rotatedLoc.x * rotatedLoc.x, rotatedLoc.y * rotatedLoc.y, rotatedLoc.z * rotatedLoc.z));
            rotatedLoc = Quaternion.Inverse(otherRB.transform.rotation) * otLocPoint;

            float impulse = (float)(hookeMultiplier * combYoungsModulus * penDist * state.FixedDeltaTimeWorld / GetGtt());

            //The change in rapidity on the line of action:
            Vector3 finalParraRapidity = myVel.Gamma() * myParraVel + impulse / mass * lineOfAction;
            //The change in rapidity perpendincular to the line of action:
            Vector3 finalPerpRapidity = myVel.Gamma() * myAngTanVel + Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, impulse * lineOfAction), myLocPoint);
            //Velocities aren't linearly additive in relativity, but rapidities are:
            Vector3 finalTotalRapidity = finalParraRapidity + finalPerpRapidity;
            Vector3 tanVelFinal = finalPerpRapidity.RapidityToVelocity();
            //This is a hack. We save the new velocities to overwrite the Rigidbody velocities on the next frame:
            collisionResultVel3 = finalTotalRapidity.RapidityToVelocity();
            //If the angle of the torque is close to 0 or 180, we have rounding problems:
            float angle = Vector3.Angle(myAngVel, myLocPoint);
            if (angle > 2.0f && angle < 178.0f)
            {
                oldCollisionResultAngVel3 = Vector3.Cross(tanVelFinal, myLocPoint) / myLocPoint.sqrMagnitude;
            }
            else
            {
                oldCollisionResultAngVel3 = myAngVel;
            }
            //In the ideal, it shouldn't be necessary to clamp the speed
            // in order to prevent FTL collision results, but we could
            // still exceed the max speed and come very close to the speed of light
            checkCollisionSpeed();
        }

        private float GetPenetrationDepth(Collision collision, Vector3 myPRelVel, Vector3 oPos, ref PointAndNorm contactPoint)
        {
            //Raycast with other collider in a collision

            float gamma = myPRelVel.Gamma();

            Vector3 testNormal;
            float penDist = 0.0f;
            float penTest = 0.0f;
            float temp = 0.0f;
            Vector3 extents = meshFilter.mesh.bounds.extents;
            float startDist = 4.0f * Mathf.Max(extents.x, extents.y, extents.z);
            RaycastHit hitInfo;
            ContactPoint point;
            float maxLCV = collision.contacts.Length;
            Ray ray = new Ray();
            for (int i = 0; i < maxLCV; i++)
            {
                point = collision.contacts[i];
                testNormal = point.normal;
                ray.origin = oPos + startDist * testNormal;
                ray.direction = -testNormal;
                if (collision.collider.Raycast(ray, out hitInfo, startDist * 2.0f))
                {
                    penTest = hitInfo.distance - startDist;
                    ray.origin = oPos - startDist * testNormal;
                    ray.direction = testNormal;
                    if (myCollider.Raycast(ray, out hitInfo, startDist))
                    {
                        temp = Mathf.Abs(((startDist - hitInfo.distance) - penTest) * gamma);
                        if (temp > 0.0f)
                        {
                            penTest = temp;
                        }
                    }
                }
                else
                {
                    penTest = 0.0f;
                }
                if (penTest > penDist)
                {
                    penDist = penTest;
                    contactPoint.point = point.point;
                    contactPoint.normal = point.normal;
                }
            }

            return penDist;
        }

        private void Sleep()
        {
            if (myRigidbody != null && !myRigidbody.isKinematic)
            {
                if (!isSleeping)
                {
                    //The sleep rotation has to be held fixed to keep sleeping objects
                    // resting flush on stationary surfaces below them.
                    sleepRotation = transform.rotation;
                }
                transform.rotation = sleepRotation;
                viw = Vector3.zero;
                collisionResultVel3 = Vector3.zero;
                oldCollisionResultVel3 = Vector3.zero;
                myRigidbody.velocity = Vector3.zero;
                aviw = Vector3.zero;
                collisionResultAngVel3 = Vector3.zero;
                oldCollisionResultAngVel3 = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;
                myRigidbody.Sleep();
                isSleeping = true;
            }
        }
        #endregion

        //This is the "t-t" or "0-0" component of the metric tensor in an accelerated frame in special relativity.
        // It appears to change due to proper acceleration from the player's/camera's point of view, since acceleration is not relative.
        // It also depends on an object's distance from the player, so it is calculated by and for the object itself.
        public float GetGtt()
        {
            Vector3 playerPos = state.playerTransform.position;
            Vector3 playerVel = state.PlayerVelocityVector;
            return (float)Math.Pow(1.0 + 1.0 / state.SpeedOfLightSqrd * Vector3.Dot(state.PlayerAccelerationVector, transform.position.WorldToOptical(viw, playerPos, playerVel)), 2);
        }
    }
}