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

                Vector3 playerPos = state.playerTransform.position;
                Vector3 playerVel = state.PlayerVelocityVector;
                //Under instantaneous changes in velocity, the optical position should be invariant:
                //Vector3 test = piw.WorldToOptical(_viw, playerPos, playerVel);
                piw = piw.WorldToOptical(_viw, playerPos, playerVel).OpticalToWorldHighPrecision(value, playerPos, playerVel);
                //test = test - piw.WorldToOptical(value, playerPos, playerVel);
                if (!nonrelativisticShader)
                {
                    transform.position = piw;
                }
                _viw = value;
                //Also update the Rigidbody, if any
                if (myRigidbody != null && !state.MovementFrozen)
                {
                    float timeFactor = (float)(GetGtt() / state.SqrtOneMinusVSquaredCWDividedByCSquared);
                    myRigidbody.velocity = value * timeFactor;
                    myRigidbody.angularVelocity = aviw * timeFactor;
                }

                //Update the shader parameters if necessary
                UpdateShaderParams();
            }
        }

        public void SetViwAndPosition(Vector3 newViw, Vector3 newPiw)
        {
            piw = newPiw;
            _viw = newViw;
            if (nonrelativisticShader)
            {
                newPiw = newPiw.WorldToOptical(newViw, state.playerTransform.position, state.PlayerVelocityVector);
            }
            transform.position = newPiw;

            if (nonrelativisticShader)
            {
                if (contractor == null) SetUpContractor();
                contractor.position = transform.position;
                transform.localPosition = Vector3.zero;
                ContractLength();
            }

            MarkStaticColliderPos();

            //Also update the Rigidbody, if any
            if (myRigidbody != null && !state.MovementFrozen)
            {
                myRigidbody.velocity = newViw * (float)(GetGtt() / state.SqrtOneMinusVSquaredCWDividedByCSquared);
                myRigidbody.angularVelocity = aviw * (float)(GetGtt() / state.SqrtOneMinusVSquaredCWDividedByCSquared);
            }
            UpdateShaderParams();
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
                    myRigidbody.velocity = viw * (float)(GetGtt() / state.SqrtOneMinusVSquaredCWDividedByCSquared);
                    myRigidbody.angularVelocity = value * (float)(GetGtt() / state.SqrtOneMinusVSquaredCWDividedByCSquared);
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
        private const float sleepVelocity = 0.01f;
        //Gravity might keep the velocity above this, so we also check whether the position is changing:
        private const float sleepDistance = 0.0001f;
        private Vector3 sleepOldPosition;
        //Once we're below the sleep velocity threshold, this is how many frames we wait for
        // before sleeping the object:
        private const int sleepFrameDelay = 3;
        private int sleepFrameCounter;
        private bool isSleepingOnCollider;
        //TODO: Rigidbody doesn't stay asleep. Figure out why, and get rid of this:
        private bool isSleeping;
        //Length contraction and rounding error pulls sleeping objects off the surfaces they rest on.
        // Save the original orientation on sleep, and force it back while sleeping.
        private Quaternion sleepRotation;
        //This is a cap on penalty method collision.
        //(It's roughly the Unity units equivalent of Young's Modulus of diamond.)
        private const float maxYoungsModulus = 1220.0e9f;
        //The center of mass calculation in the rigidbody becomes non-physical when we transform the collider
        public Vector3 opticalWorldCenterOfMass { get; set; }
        //private const float drag = 0.1f;
        //private const float angularDrag = 0.1f;
        public float initBounciness = 0.4f;
        //During collision, viw getters and setters trigger many "enter" rather than "stay" events, so we smooth this:
        private class RecentCollision
        {
            public double LastTime { get; set; }
            public Collider Collider { get; set; }
        }
        private List<RecentCollision> collidedWith;
        private float collideAgainWait = 0.3f;
        #endregion
        //Keep track of our own Mesh Filter
        private MeshFilter meshFilter;
        //Store our raw vertices in this variable, so that we can refer to them later.
        private Vector3[] rawVertsBuffer;
        //To avoid garbage collection, we might over-allocate the buffer:
        private int rawVertsBufferLength;

        //Store this object's velocity here.

        //Keep track of Game State so that we can reference it quickly.
        private GameState _state;
        private GameState state
        {
            get
            {
                if (_state == null)
                {
                    _state = GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>();
                }

                return _state;
            }
        }
        private void FetchState()
        {
            _state = GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>();
        }
        //When was this object created? use for moving objects
        private float startTime = float.NegativeInfinity;
        //When should we die? again, for moving objects
        private float _DeathTime = float.PositiveInfinity;
        public float DeathTime { get { return _DeathTime; } set { _DeathTime = value; } }

        //Acceleration desyncronizes our clock from the world clock:
        public double localTimeOffset { get; private set; }

        public void ResetLocalTime()
        {
            localTimeOffset = 0.0;
        }

        //Use this instead of relativistic parent
        public bool isParent = false;
        public bool isCombinedColliderParent = false;
        //Don't render if object has relativistic parent
        private bool hasParent = false;
        //Use this if not using an explicitly relativistic shader
        public bool nonrelativisticShader = false;
        //If the shader is not relativistic, we need to handle length contraction with a "contractor" transform.
        private Transform contractor;
        //Changing a transform's parent is expensive, but we can avoid it with this:
        private Vector3 contractorLocalScale;
        //private int? oldParentID;
        //Store world position, and player position and velocity, only for a nonrelativistic shader:
        private Vector3 piw;

        //We use an attached shader to transform the collider verts:
        public ComputeShader colliderShader;
        //We set global constants in a struct;
        public ShaderParams colliderShaderParams;
        //We save and reuse the transformed vert array to avoid garbage collection 
        private Vector3[] trnsfrmdMeshVerts;
        //If we have a collider to transform, we cache it here
        private Collider[] myColliders;
        private PhysicMaterial myPhysicMaterial;
        //If we specifically have a mesh collider, we need to know to transform the verts of the mesh itself.
        private bool myColliderIsMesh;
        private bool myColliderIsBox;
        private bool myColliderIsVoxel;
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
        //Time when the collision started
        public double collideTimeStart { get; set; }
        //Collision-softening time
        //public float collideSoftenTime = 0.2f;
        //For penalty methods, we need an intermediate collision velocity result
        private Vector3 oldCollisionResultVel3;
        private Vector3 oldCollisionResultAngVel3;
        //If the shader is nonrelativistic, and if the object is static, it helps to save and restore the initial position
        public bool isStatic = false;
        public void MarkStaticColliderPos()
        {
            if (myColliderIsBox && myColliders != null)
            {
                List<Vector3> sttcPosList = new List<Vector3>();
                for (int i = 0;i < myColliders.Length; i++)
                {
                    sttcPosList.Add(((BoxCollider)myColliders[i]).center);
                }
                colliderPiw = sttcPosList.ToArray();
            }
        }
        public float staticResetPlayerSpeedSqr;
        public Vector3 staticTransformPosition;
        public Vector3[] colliderPiw;

        ComputeBuffer paramsBuffer;
        ComputeBuffer vertBuffer;

        //We need to freeze any attached rigidbody if the world states is frozen 
        public bool wasKinematic { get; set; }
        public bool wasFrozen { get; set; }
        private void UpdateMeshCollider(MeshCollider transformCollider)
        {
            //Debug.Log("Updating mesh collider.");

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

            //if (!GetComponent<MeshRenderer>().enabled /*|| colliderShaderParams.gtt == 0*/)
            //{
            //    return;
            //}

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
            //colliderShaderParams.wrldTime = (float)state.TotalTimeWorld;
            //colliderShaderParams.strtTime = startTime;

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
                vertBuffer = new ComputeBuffer(rawVertsBufferLength, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
            }
            else if (vertBuffer.count != rawVertsBufferLength)
            {
                vertBuffer.Dispose();
                vertBuffer = new ComputeBuffer(rawVertsBufferLength, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
            }
            vertBuffer.SetData(rawVertsBuffer);
            int kernel = colliderShader.FindKernel("CSMain");
            colliderShader.SetBuffer(kernel, "glblPrms", paramsBuffer);
            colliderShader.SetBuffer(kernel, "verts", vertBuffer);
            colliderShader.Dispatch(kernel, rawVertsBufferLength, 1, 1);
            vertBuffer.GetData(trnsfrmdMeshVerts);

            //Change mesh:
            trnsfrmdMesh.vertices = trnsfrmdMeshVerts;
            trnsfrmdMesh.RecalculateBounds();
            transformCollider.sharedMesh = trnsfrmdMesh;
            //meshFilter.mesh.RecalculateBounds();
            //meshFilter.mesh.RecalculateNormals();
            //Mobile only:
            //meshFilter.mesh.RecalculateTangents();
            //Cache actual world center of mass, and then reset local (rest frame) center of mass:
            myRigidbody.ResetCenterOfMass();
            opticalWorldCenterOfMass = myRigidbody.worldCenterOfMass;
            myRigidbody.centerOfMass = initCOM;

            //Debug.Log("Finished updating mesh collider.");
        }

        void OnDestroy()
        {
            if (paramsBuffer != null) paramsBuffer.Release();
            if (vertBuffer != null) vertBuffer.Release();
            if (contractor != null) Destroy(contractor.gameObject);
        }

        void Awake()
        {
            //Get the player's GameState, use it later for general information
            FetchState();
        }

        // Get the start time of our object, so that we know where not to draw it
        public void SetStartTime()
        {
            Vector3 playerPos = state.playerTransform.position;
            float timeDelayToPlayer = (float)Math.Sqrt((transform.position.WorldToOptical(viw, playerPos, state.PlayerVelocityVector) - playerPos).sqrMagnitude / state.SpeedOfLightSqrd);
            timeDelayToPlayer *= (float)(GetGtt() / state.SqrtOneMinusVSquaredCWDividedByCSquared);
            startTime = (float)(state.TotalTimeWorld - timeDelayToPlayer);
            if (GetComponent<MeshRenderer>() != null)
                GetComponent<MeshRenderer>().enabled = false;
        }
        //Set the death time, so that we know at what point to destroy the object in the player's view point.
        public virtual void SetDeathTime()
        {
            Vector3 playerPos = state.playerTransform.position;
            float timeDelayToPlayer = (float)Math.Sqrt((transform.position.WorldToOptical(viw, playerPos, state.PlayerVelocityVector) - playerPos).sqrMagnitude / state.SpeedOfLightSqrd);
            timeDelayToPlayer *= (float)(GetGtt() / state.SqrtOneMinusVSquaredCWDividedByCSquared);
            DeathTime = (float)(state.TotalTimeWorld - timeDelayToPlayer);
        }
        void CombineParent()
        {
            if (GetComponent<ObjectMeshDensity>())
            {
                GetComponent<ObjectMeshDensity>().enabled = false;
            }
            bool wasStatic = gameObject.isStatic;
            gameObject.isStatic = false;
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
            //We can optimize further for duplicate materials:
            Dictionary<string, Material> uniqueMaterials = new Dictionary<string, Material>();
            List<string> uniqueMaterialNames = new List<string>();

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
                    //turn off the original renderer
                    meshRenderers[i].enabled = false;
                    RelativisticObject ro = meshRenderers[i].GetComponent<RelativisticObject>();
                    if (ro != null)
                    {
                        ro.hasParent = true;
                    }
                    //grab its material
                    tempMaterials[subMeshIndex] = meshRenderers[i].materials[q];
                    //Check if material is unique
                    string name = meshRenderers[i].materials[q].name.Replace(" (Instance)", "");
                    if (!uniqueMaterials.ContainsKey(name))
                    {
                        uniqueMaterials.Add(name, meshRenderers[i].materials[q]);
                        uniqueMaterialNames.Add(name);
                    }
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
                //meshFilters[i].gameObject.SetActive(false);
            }

            //Put it all together now.
            Mesh myMesh = new Mesh();
            //If any materials are the same, we can combine triangles and give them the same material.
            myMesh.subMeshCount = uniqueMaterials.Count;
            myMesh.vertices = tempVerts;
            Material[] finalMaterials = new Material[uniqueMaterials.Count];
            for (int i = 0; i < uniqueMaterialNames.Count; i++)
            {
                string uniqueName = uniqueMaterialNames[i];
                List<int> combineTriangles = new List<int>();
                for (int j = 0; j < tempMaterials.Length; j++)
                {
                    string name = tempMaterials[j].name.Replace(" (Instance)", "");
                    if (uniqueName.Equals(name))
                    {
                        combineTriangles.AddRange(tempTriangles[j]);
                    }
                }
                myMesh.SetTriangles(combineTriangles.ToArray(), i);
                finalMaterials[i] = uniqueMaterials[uniqueMaterialNames[i]];
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
            if (uniqueMaterials.Count == 1)
            {
                meshy.GetComponent<Renderer>().material = finalMaterials[0];
            }
            else
            {
                meshy.GetComponent<Renderer>().materials = finalMaterials;
            }

            MeshCollider mCollider = GetComponent<MeshCollider>();
            if (mCollider != null)
            {
                mCollider.sharedMesh = myMesh;
            }

            transform.gameObject.SetActive(true);
            gameObject.isStatic = wasStatic;

            if (isCombinedColliderParent)
            {
                MeshCollider myMeshCollider = GetComponent<MeshCollider>();
                if (myMeshCollider != null)
                {
                    myMeshCollider.sharedMesh = myMesh;
                }  
            }
            else
            {
                MeshCollider[] childrenColliders = GetComponentsInChildren<MeshCollider>();
                List<Collider> dupes = new List<Collider>();
                for (int i = 0; i < childrenColliders.Length; i++)
                {
                    MeshCollider orig = childrenColliders[i];
                    MeshCollider dupe = CopyComponent(childrenColliders[i], gameObject);
                    dupe.convex = orig.convex;
                    dupe.inflateMesh = orig.inflateMesh;
                    dupe.skinWidth = orig.skinWidth;
                    dupe.sharedMesh = Instantiate(orig.sharedMesh);
                    dupes.Add(dupe);
                }
                if (myColliders == null)
                {
                    myColliders = dupes.ToArray();
                }
                else
                {
                    dupes.AddRange(myColliders);
                    myColliders = dupes.ToArray();
                }
            }
            //"Delete" all children.
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child.tag != "Contractor" && child.tag != "Voxel Collider")
                {
                    transform.GetChild(i).gameObject.SetActive(false);
                    Destroy(transform.GetChild(i).gameObject);
                }
            }
            GetComponent<MeshRenderer>().enabled = true;
            GetComponent<RelativisticObject>().enabled = true;
        }

        T CopyComponent<T>(T original, GameObject destination) where T : Component
        {
            System.Type type = original.GetType();
            Component copy = destination.AddComponent(type);
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy as T;
        }

        void Start()
        {
            _viw = initialViw;
            _aviw = initialAviw;
            isSleeping = false;
            isSleepingOnCollider = false;
            didCollide = false;
            sleepFrameCounter = 0;
            myRigidbody = GetComponent<Rigidbody>();
            rawVertsBufferLength = 0;
            collidedWith = new List<RecentCollision>();
            wasKinematic = false;
            wasFrozen = false;

            staticResetPlayerSpeedSqr = (float)(state.SpeedOfLightSqrd * 0.005f * 0.005f);

            UpdateCollider();
            //Debug.Log("Found collider.");

            MarkStaticColliderPos();

            //Get the meshfilter
            if (isParent)
            {
                CombineParent();
                //Debug.Log("Combined parent.");
            }
            meshFilter = GetComponent<MeshFilter>();

            if (myColliders != null)
            {
                for (int i = 0; i < myColliders.Length; i++)
                {
                    myColliders[i].material.bounciness = initBounciness;
                    //Debug.Log("Set bounciness.");
                    if (myColliderIsMesh)
                    {
                        ((MeshCollider)myColliders[i]).sharedMesh.MarkDynamic();
                    }
                }
            }

            if (myRigidbody != null)
            {
                //Native rigidbody gravity should never be used:
                myRigidbody.useGravity = false;
                //myRigidbody.centerOfMass = Vector3.zero;
            }

            //Get the vertices of our mesh
            if (meshFilter != null)
            {
                rawVertsBufferLength = meshFilter.mesh.vertices.Length;
                rawVertsBuffer = meshFilter.mesh.vertices;
                //if (trnsfrmdMesh != null)
                //{
                //    meshFilter.mesh = trnsfrmdMesh;
                //}
                //meshFilter.mesh.MarkDynamic();
                //Debug.Log("Got mesh verts.");
            }
            else
            {
                rawVertsBuffer = null;
                rawVertsBufferLength = 0;
            }

            //Once we have the mesh vertices, allocate and immediately transform the collider:
            if (myColliderIsMesh && rawVertsBufferLength > 0 && (myColliders != null))
            {
                trnsfrmdMeshVerts = new Vector3[rawVertsBufferLength];
                //Debug.Log("Initialized verts.");
            }

            colliderShaderParams.viw = Vector3.zero;
            //Debug.Log("Initialized viw.");
            //colliderShaderParams.aviw = Vector3.zero;
            //Debug.Log("Initialized aviw.");
            //colliderShaderParams.piw = Vector3.zero;
            //Debug.Log("Initialized piw.");
            //colliderShaderParams.strtTime = startTime;
            //Debug.Log("Initialized mesh shader.");

            checkSpeed();

            //Also get the meshrenderer so that we can give it a unique material
            Renderer tempRenderer = GetComponent<Renderer>();
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
                    Material quickSwapMaterial = Instantiate(tempRenderer.materials[i]) as Material;
                    //Then, set the value that we want
                    quickSwapMaterial.SetFloat("_viw", 0);
                    //quickSwapMaterial.SetFloat("_aviw", 0);
                    //quickSwapMaterial.SetFloat("_piw", 0);
                    

                    //And stick it back into our renderer. We'll do the SetVector thing every frame.
                    tempRenderer.materials[i] = quickSwapMaterial;

                    //set our start time and start position in the shader.
                    //tempRenderer.materials[i].SetFloat("_strtTime", (float)startTime);
                    //tempRenderer.materials[i].SetVector("_strtPos", new Vector4(transform.position.x, transform.position.y, transform.position.z, 0));
                    //}
                }
            }

            //This code is a hack to ensure that frustrum culling does not take place
            //It changes the render bounds so that everything is contained within them
            //At high speeds the Lorentz contraction means that some objects not normally in the view frame are actually visible
            //If we did frustrum culling, these objects would be ignored (because we cull BEFORE running the shader, which does the lorenz contraction)
            if (meshFilter != null)
            {
                Transform camTransform = Camera.main.transform;
                float distToCenter = (Camera.main.farClipPlane + Camera.main.nearClipPlane) / 2.0f;
                Vector3 center = camTransform.position + camTransform.forward * distToCenter;
                float extremeBound = 500000.0f;
                meshFilter.sharedMesh.bounds = new Bounds(center, Vector3.one * extremeBound);
            }

            //If the shader is nonrelativistic, map the object from world space to optical space and handle length contraction:
            if (nonrelativisticShader)
            {
                transform.position = transform.position.WorldToOptical(viw, state.playerTransform.position, state.PlayerVelocityVector);
                if (contractor == null) SetUpContractor();
                ContractLength();
            }

            if (myRigidbody != null)
            {
                opticalWorldCenterOfMass = myRigidbody.worldCenterOfMass;
            }
        }

        private void UpdateCollider()
        {
            if (GetComponent<ObjectBoxColliderDensity>() == null)
            {
                MeshCollider[] myMeshColliders = GetComponents<MeshCollider>();
                myColliders = myMeshColliders;
                if (myColliders.Length > 0)
                {
                    myColliderIsMesh = true;
                    myColliderIsBox = false;
                    myColliderIsVoxel = false;
                    for (int i = 0; i < myMeshColliders.Length; i++)
                    {
                        trnsfrmdMesh = Instantiate(myMeshColliders[i].sharedMesh);
                        myMeshColliders[i].sharedMesh = trnsfrmdMesh;
                        trnsfrmdMesh.MarkDynamic();
                        //Debug.Log("My collider is mesh.");
                    }
                }
                else
                {
                    myColliders = GetComponents<BoxCollider>();
                    myColliderIsBox = (myColliders.Length > 0);
                    myColliderIsMesh = false;
                    myColliderIsVoxel = false;
                }
            }
            else
            {
                myColliderIsVoxel = true;
                myColliderIsBox = false;
                myColliderIsMesh = false;
            }

            Collider collider = GetComponent<Collider>();
            if (collider != null)
            {
                myPhysicMaterial = collider.material;
            }
        }

        private void EnforceCollision()
        {
            oldCollisionResultVel3 = collisionResultVel3;
            oldCollisionResultAngVel3 = collisionResultAngVel3;

            if (didCollide)
            {
                //Finish and shut off enforcement
                viw = collisionResultVel3;
                aviw = collisionResultAngVel3;
                myRigidbody.angularVelocity = aviw;
                didCollide = false;
            }
        }

        public void UpdateGravity()
        {
            Vector3 tempViw = viw.Gamma() * viw;
            tempViw += Physics.gravity * (float)state.FixedDeltaTimeWorld;
            tempViw = tempViw.RapidityToVelocity();
            float test = tempViw.x + tempViw.y + tempViw.z;
            if (!float.IsNaN(test) && !float.IsInfinity(test) && (tempViw.sqrMagnitude < ((state.MaxSpeed - .01) * (state.MaxSpeed - .01))))
            {
                viw = tempViw;
            }
        }

        public void Update()
        {
            localTimeOffset += state.DeltaTimeWorld * (GetGtt() - 1.0);

            EnforceCollision();

            //Update the rigidbody reference.
            //myRigidbody = GetComponent<Rigidbody>();
            
            //Update the collider reference.
            //UpdateCollider();

            if (meshFilter != null && !state.MovementFrozen)
            {
                #region meshDensity
                //This is where I'm going to change our mesh density.
                //I'll take the model, and pass MeshDensity the mesh and unchanged vertices
                //If it comes back as having changed something, I'll edit the mesh.

                //If the shader is nonrelativistic, there's no reason to change the mesh density.
                if (!nonrelativisticShader) {
                    ObjectMeshDensity density = GetComponent<ObjectMeshDensity>();

                    if (density != null)
                    {

                        //Only run MeshDensity if the mesh needs to change, and if it's passed a threshold distance.
                        if (rawVertsBuffer != null && density.change != null)
                        {
                            //This checks if we're within our large range, first mesh density circle
                            //If we're within a distance of 40, split this mesh
                            if (!(density.state) && (RecursiveTransform(rawVertsBuffer[0], meshFilter.transform).sqrMagnitude < (21000 * 21000)))
                            {
                                Mesh meshFilterMesh = meshFilter.mesh;
                                if (density.ReturnVerts(meshFilterMesh, true))
                                {
                                    Vector3[] meshVerts = meshFilterMesh.vertices;
                                    rawVertsBufferLength = meshVerts.Length;
                                    if (rawVertsBuffer.Length < rawVertsBufferLength)
                                    {
                                        rawVertsBuffer = new Vector3[rawVertsBufferLength];
                                    }
                                    System.Array.Copy(meshVerts, rawVertsBuffer, rawVertsBufferLength);
                                }
                            }

                            //If the object leaves our wide range, revert mesh to original state
                            else if (density.state && (RecursiveTransform(rawVertsBuffer[0], meshFilter.transform).sqrMagnitude > (21000 * 21000)))
                            {
                                Mesh meshFilterMesh = meshFilter.mesh;
                                if (density.ReturnVerts(meshFilterMesh, false))
                                {
                                    Vector3[] meshVerts = meshFilterMesh.vertices;
                                    rawVertsBufferLength = meshVerts.Length;
                                    if (rawVertsBuffer.Length < rawVertsBufferLength)
                                    {
                                        rawVertsBuffer = new Vector3[rawVertsBufferLength];
                                    }
                                    System.Array.Copy(meshVerts, rawVertsBuffer, rawVertsBufferLength);
                                }
                            }

                        }
                    }
                }
                #endregion
            }

            UpdateShaderParams();
        }

        public float GetTisw(Vector3? playerPos = null)
        {
            Vector3 riw;
            if (playerPos == null)
            {
                riw = transform.position - state.playerTransform.position;
            }
            else
            {
                riw = transform.position - playerPos.Value;
            }
            //Place the vertex to be changed in a new Vector3
            

            //Here begins a rotation-free modification of the original OpenRelativity shader:

            float c = -Vector3.Dot(riw, riw); //first get position squared (position doted with position)

            float b = -(2 * Vector3.Dot(riw, viw)); //next get position doted with velocity, should be only in the Z direction

            float d = (float)(state.SpeedOfLightSqrd) - Vector3.Dot(viw, viw);

            return (-b - (Mathf.Sqrt((b * b) - 4.0f * d * c))) / (2.0f * d);
        }

        void FixedUpdate() {
            int lcv = 0;
            while (lcv < collidedWith.Count)
            {
                if (collidedWith[lcv].LastTime + collideAgainWait <= state.TotalTimeWorld)
                {
                    collidedWith.RemoveAt(lcv);
                }
                else
                {
                    lcv++;
                }
            }

            if (myRigidbody != null)
            {

                if (!isSleeping
                    && (((viw.sqrMagnitude < (sleepVelocity * sleepVelocity)) && (aviw.sqrMagnitude < (sleepVelocity * sleepVelocity)))
                    || ((sleepOldPosition - transform.position).sqrMagnitude < (sleepDistance * sleepDistance))) )
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

                if (!isSleeping && useGravity && state.SqrtOneMinusVSquaredCWDividedByCSquared != 0)
                {
                    UpdateGravity();
                }

                if ((sleepOldPosition - transform.position).sqrMagnitude >= (sleepDistance * sleepDistance))
                {
                    sleepOldPosition = transform.position;
                }
            }

            if (meshFilter != null && !state.MovementFrozen)
            {
                //As long as our object is actually alive, perform these calculations
                if (transform != null)
                {
                    /***************************
                     * Start Part 6 Bullet 1
                     * *************************/

                    float tisw = GetTisw();

                    /****************************
                     * Start Part 6 Bullet 2
                     * **************************/

                    //If we're past our death time (in the player's view, as seen by tisw)
                    if (state.TotalTimeWorld + localTimeOffset + tisw > DeathTime)
                    {
                        KillObject();
                    }
                    else if ((state.TotalTimeWorld + localTimeOffset + tisw > startTime))
                    {
                        //Grab our renderer.
                        Renderer tempRenderer = GetComponent<Renderer>();
                        if (!tempRenderer.enabled)
                        {
                            tempRenderer.enabled = !hasParent;
                            AudioSource[] audioSources = GetComponents<AudioSource>();
                            if (audioSources.Length > 0)
                            {
                                for (int i = 0; i < audioSources.Length; i++)
                                {
                                    audioSources[i].enabled = true;
                                }
                            }
                        }
                    }
                }

                //update our viw and set the rigid body proportionally
                if (myRigidbody != null
                    && !double.IsNaN(state.SqrtOneMinusVSquaredCWDividedByCSquared)
                    && state.SqrtOneMinusVSquaredCWDividedByCSquared != 0.0
                    && state.SpeedOfLightSqrd != 0.0)
                {
                    //Dragging probably happens intrinsically in the rest frame,
                    // so it acts on the rapidity. (Drag is computationally expensive
                    // due to tripping the velocity setter every frame.)
                    // TODO: Replace with drag force
                    //Vector3 rapidity = (float)(1.0 - drag * state.DeltaTimeWorld) * viw.Gamma() * viw;
                    //viw = rapidity.RapidityToVelocity();
                    //aviw = (float)(1.0 - angularDrag * state.DeltaTimeWorld) * aviw;

                    //Correct for both time dilation and change in metric due to player acceleration:
                    float timeFactor = GetGtt() / (float)state.SqrtOneMinusVSquaredCWDividedByCSquared;
                    myRigidbody.velocity = viw * timeFactor;
                    myRigidbody.angularVelocity = aviw * timeFactor;
                }
            }
            //If nothing is null, then set the object to standstill, but make sure its rigidbody actually has a velocity.
            else if (meshFilter != null && myRigidbody != null && GetComponent<MeshRenderer>() != null)
            {
                myRigidbody.velocity = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;
            }

            if (nonrelativisticShader)
            {
                if (!isStatic && !isSleeping)
                {
                    //Update the position in world, if necessary:
                    piw += transform.position - contractor.position;
                }
                transform.localPosition = Vector3.zero;
                contractor.position = piw.WorldToOptical(viw, state.playerTransform.position, state.PlayerVelocityVector);
                ContractLength();
            }
            else
            {
                piw = transform.position;
            }

            if (!myColliderIsVoxel)
            {
                UpdateColliderPosition();
            }
            MarkStaticColliderPos();

            //This might be nonphysical, but we want resting colliders to stay "glued" to the floor:
            if (myColliderIsBox && isSleeping && isSleepingOnCollider)
            {
                float extentY = myColliders[0].bounds.extents.y;
                float maxDist = 100f;
                Ray downRay = new Ray()
                {
                    direction = Vector3.down,
                    origin = transform.TransformPoint(((BoxCollider)myColliders[0]).center) + extentY * Vector3.up
                };
                RaycastHit hitInfo;
                if (Physics.Raycast(downRay, out hitInfo, 2.0f * maxDist))
                {
                    
                    if (nonrelativisticShader)
                    {
                        contractor.position += (hitInfo.distance - 2.0f * extentY) * Vector3.down;
                        transform.localPosition = Vector3.zero;
                    }
                    else
                    {
                        transform.position += (hitInfo.distance - 2.0f * extentY) * Vector3.down;
                    }
                }
            }
        }

        public void UpdateColliderPosition(Collider toUpdate = null)
        {
            Vector3 playerPos = state.playerTransform.position;
            Vector3 playerVel = state.PlayerVelocityVector;

            if (myColliderIsVoxel)
            {
                ObjectBoxColliderDensity obcd = GetComponent<ObjectBoxColliderDensity>();
                if (obcd != null)
                {
                    obcd.UpdatePositions(toUpdate);
                }
            }
            else if (!nonrelativisticShader && (myColliders != null) && (myColliders.Length > 0))
            {
                //If we have a MeshCollider and a compute shader, transform the collider verts relativistically:
                if (myColliderIsMesh && (colliderShader != null) && SystemInfo.supportsComputeShaders)
                {
                    for (int i = 0; i < myColliders.Length; i++)
                    {
                        UpdateMeshCollider((MeshCollider)myColliders[i]);
                    }
                }
                //If we have a BoxCollider, transform its center to its optical position
                else if (myColliderIsBox)
                {
                    if (isStatic || isSleeping)
                    {
                        Vector3 pos;
                        for (int i = 0; i < myColliders.Length; i++)
                        {
                            BoxCollider collider = (BoxCollider)myColliders[i];
                            pos = transform.TransformPoint(colliderPiw[i]);
                            collider.center = pos.WorldToOptical(Vector3.zero, playerPos, playerVel);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < myColliders.Length; i++)
                        {
                            BoxCollider collider = (BoxCollider)myColliders[i];
                            collider.center = transform.InverseTransformPoint(colliderPiw[i].WorldToOptical(viw, playerPos, playerVel));
                        }
                    }
                }
            }

            if (myRigidbody != null)
            {
                opticalWorldCenterOfMass = myRigidbody.worldCenterOfMass;
            }
        }

        private void UpdateShaderParams()
        {
            Renderer tempRenderer = GetComponent<Renderer>();
            //Send our object's v/c (Velocity over the Speed of Light) to the shader
            if (tempRenderer != null)
            {
                Vector3 tempViw = viw / (float)state.SpeedOfLight;
                Vector3 tempAviw = aviw;
                Vector3 tempPiw = transform.position;
                colliderShaderParams.viw = tempViw;
                //colliderShaderParams.aviw = tempAviw;
                //colliderShaderParams.piw = tempPiw;
                for (int i = 0; i < tempRenderer.materials.Length; i++)
                {
                    tempRenderer.materials[i].SetVector("_viw", new Vector4(tempViw.x, tempViw.y, tempViw.z, 0));
                    //tempRenderer.materials[i].SetVector("_aviw", new Vector4(tempAviw.x, tempAviw.y, tempAviw.z, 0));
                    //tempRenderer.materials[i].SetVector("_piw", new Vector4(tempPiw.x, tempPiw.y, tempPiw.z, 0));
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
            if (viw.sqrMagnitude > ((state.MaxSpeed - .01) * (state.MaxSpeed - .01)))
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
            //ResetDeathTime();
        }
        public void ResetDeathTime()
        {
            DeathTime = float.PositiveInfinity;
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
            if (myRigidbody == null || myRigidbody.isKinematic || isSleeping)
            {
                return;
            }

            GameObject otherGO = collision.gameObject;
            RelativisticObject otherRO = otherGO.GetComponent<RelativisticObject>();

            //Lorentz transformation might make us come "unglued" from a collider we're resting on.
            // If we're asleep, and the other collider has zero velocity, we don't need to wake up:
            if (isSleeping && otherRO.viw == Vector3.zero)
            {
                return;
            }

            PhysicMaterial otherMaterial = collision.collider.material;
            float combFriction = CombinePhysics(myPhysicMaterial.frictionCombine, myPhysicMaterial.staticFriction, otherMaterial.staticFriction);
            float combRestCoeff = CombinePhysics(myPhysicMaterial.bounceCombine, myPhysicMaterial.bounciness, otherMaterial.bounciness);

            //Tangental relationship scales normalized "bounciness" to a Young's modulus

            float combYoungsModulus = GetYoungsModulus(combRestCoeff);

            PointAndNorm contactPoint = DecideContactPoint(collision);
            ApplyPenalty(collision, otherRO, contactPoint, combFriction, combYoungsModulus);
            didCollide = true;
        }

        private float GetYoungsModulus(float combRestCoeff)
        {
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

            return combYoungsModulus;
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (myRigidbody == null || myColliders == null || myRigidbody.isKinematic)
            {
                return;
            }
            else if (collidedWith.Count > 0)
            {
                for (int i = 0; i < collidedWith.Count; i++)
                {
                    //If this collision is returned, redirect to OnCollisionStay
                    if (collision.collider.Equals(collidedWith[i].Collider))
                    {
                        collidedWith[i].LastTime = state.TotalTimeWorld;
                        //didCollide = true;
                        //OnCollisionStay(collision);
                        //Then, immediately finish.
                        return;
                    }
                }
            }
            collidedWith.Add(new RecentCollision()
            {
                Collider = collision.collider,
                LastTime = state.TotalTimeWorld
            });

            GameObject otherGO = collision.gameObject;
            RelativisticObject otherRO = otherGO.GetComponent<RelativisticObject>();

            //Lorentz transformation might make us come "unglued" from a collider we're resting on.
            // If we're asleep, and the other collider has zero velocity, we don't need to wake up:
            if (isSleeping && otherRO.viw == Vector3.zero)
            {
                return;
            }

            //If we made it this far, we shouldn't be sleeping:
            isSleeping = false;
            didCollide = true;

            PointAndNorm contactPoint = DecideContactPoint(collision);
            if (contactPoint == null)
            {
                return;
            }

            PhysicMaterial otherMaterial = collision.collider.material;
            float myFriction = isSleeping ? myPhysicMaterial.staticFriction : myPhysicMaterial.dynamicFriction;
            float otherFriction = otherRO.isSleeping ? otherMaterial.staticFriction : otherMaterial.dynamicFriction;
            float combFriction = CombinePhysics(myPhysicMaterial.frictionCombine, myFriction, otherFriction);
            float combRestCoeff = CombinePhysics(myPhysicMaterial.bounceCombine, myPhysicMaterial.bounciness, otherMaterial.bounciness);

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
            Vector3 otherVel = otherRO.oldCollisionResultVel3;
            Vector3 otherAngVel = otherRO.oldCollisionResultAngVel3;

            Vector3 playerPos = state.playerTransform.position;
            Vector3 playerVel = state.PlayerVelocityVector;
            Vector3 myPRelVel = myVel.AddVelocity(-playerVel);
            //Vector3 otherPRelVel = otherVel.AddVelocity(-playerVel);

            //We want to find the contact offset relative the centers of mass of in each object's inertial frame;
            Vector3 myLocPoint, otLocPoint, contact, com;
            if (myColliderIsMesh)
            {
                contact = contactPoint.point.OpticalToWorld(myVel, playerPos, playerVel);
                com = opticalWorldCenterOfMass.OpticalToWorld(myVel, playerPos, playerVel);
                myLocPoint = contact - com;
            }
            else if (nonrelativisticShader)
            {
                contact = contactPoint.point;
                com = opticalWorldCenterOfMass.OpticalToWorld(myVel, playerPos, playerVel);
                myLocPoint = (contact - com).InverseContractLengthBy(myVel);
            }
            else
            {
                contact = contactPoint.point;
                com = opticalWorldCenterOfMass.OpticalToWorld(myVel, playerPos, playerVel);
                myLocPoint = (contact - com);
            }
            
            if (otherRO.myColliderIsMesh)
            {
                contact = contactPoint.point.OpticalToWorld(otherVel, playerPos, playerVel);
                com = otherRO.opticalWorldCenterOfMass.OpticalToWorld(otherVel, playerPos, playerVel);
                otLocPoint = contact - com;
            }
            else if (otherRO.nonrelativisticShader)
            {
                contact = contactPoint.point;
                com = otherRO.opticalWorldCenterOfMass.OpticalToWorld(otherVel, playerPos, playerVel);
                otLocPoint = (contact - com).InverseContractLengthBy(otherVel);
            }
            else
            {
                contact = contactPoint.point;
                com = opticalWorldCenterOfMass.OpticalToWorld(otherVel, playerPos, playerVel);
                otLocPoint = (contact - com);
            }

            Vector3 myAngTanVel = Vector3.Cross(myAngVel, myLocPoint);
            Vector3 myTotalVel = myVel.AddVelocity(myAngTanVel);
            Vector3 otherAngTanVel = Vector3.Cross(otherAngVel, otLocPoint);
            Vector3 otherTotalVel = otherVel.AddVelocity(otherAngTanVel);
            Vector3 lineOfAction = -contactPoint.normal.InverseContractLengthBy(myVel);
            lineOfAction.Normalize();
            //Decompose velocity in parallel and perpendicular components:
            Vector3 myParraVel = Vector3.Project(myTotalVel, lineOfAction);
            Vector3 myPerpVel = (myTotalVel - myParraVel) * myParraVel.Gamma();
            //Vector3 testVel = myTotalVel.AddVelocity(-myPerpVel);
            //Boost to the inertial frame where my velocity is entirely along the line of action:
            Vector3 otherContactVel = otherTotalVel.AddVelocity(-myPerpVel);
            //Find the relative velocity:
            Vector3 relVel = myParraVel.RelativeVelocityTo(otherContactVel);
            //Find the relative rapidity on the line of action, where my contact velocity is 0:
            float relVelGamma = relVel.Gamma();
            Vector3 rapidityOnLoA = relVelGamma * relVel;

            Vector3 myPos = opticalWorldCenterOfMass;
            Vector3 otherPos = otherRO.opticalWorldCenterOfMass;
            float penDist = GetPenetrationDepth(collision, myColliders[0], myPRelVel, myPos, otherPos, ref contactPoint);

            //We will apply penalty methods after the initial collision, in order to stabilize objects coming to rest on flat surfaces with gravity.
            // Our penalty methods can give a somewhat obvious apparent deviation from conservation of energy and momentum,
            // unless we account for the initial energy and momentum loss due to "spring-loading":
            float springImpulse = 0;
            if (penDist > 0.0f)
            {
                float combYoungsModulus = GetYoungsModulus(combRestCoeff);
                //Potential energy as if from a spring,
                //H = K + V = p^2/(2m) + 1/2*k*l^2
                // from which it can be shown that this is the change in momentum from the implied initial loading of the "spring":
                springImpulse = Mathf.Sqrt(hookeMultiplier * combYoungsModulus * penDist * penDist * myRigidbody.mass);
            }

            //Rotate my relative contact point:
            Vector3 rotatedLoc = Quaternion.Inverse(transform.rotation) * myLocPoint;
            rotatedLoc.Scale(rotatedLoc);
            //The relative contact point is the lever arm of the torque:
            float myMOI = Vector3.Dot(myRigidbody.inertiaTensor, rotatedLoc);
            //In special relativity, the impulse relates the change in rapidities, rather than the change in velocities.
            float impulse;
            if (isReflected)
            {
                impulse = -rapidityOnLoA.magnitude * (combRestCoeff + 1.0f) / (1.0f / mass + Vector3.Dot(lineOfAction, Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, lineOfAction), myLocPoint)));
            }
            else
            {
                rotatedLoc = Quaternion.Inverse(otherRB.transform.rotation) * otLocPoint;
                rotatedLoc.Scale(rotatedLoc);
                float otherMOI = Vector3.Dot(otherRB.inertiaTensor, rotatedLoc);
                impulse = -rapidityOnLoA.magnitude * (combRestCoeff + 1.0f) / (1.0f / mass + 1.0f / otherRB.mass + Vector3.Dot(lineOfAction, Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, lineOfAction), myLocPoint)) + Vector3.Dot(lineOfAction, Vector3.Cross(1.0f / otherMOI * Vector3.Cross(otLocPoint, lineOfAction), otLocPoint)));
            }

            //impulse *= (1.0f + combFriction);
            impulse += springImpulse;

            //The change in rapidity on the line of action:
            Vector3 finalLinearRapidity = relVelGamma * myVel + impulse / mass * lineOfAction;
            //The change in rapidity perpendincular to the line of action:
            Vector3 finalTanRapidity = relVelGamma * myAngTanVel + Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, impulse * lineOfAction), myLocPoint);
            //Velocities aren't linearly additive in special relativity, but rapidities are:
            float finalRapidityMag = (finalLinearRapidity + finalTanRapidity).magnitude;
            Vector3 tanVelFinal = finalTanRapidity.RapidityToVelocity(finalRapidityMag);
            //This is a hack. We save the new velocities to overwrite the Rigidbody velocities on the next frame:
            collisionResultVel3 = finalLinearRapidity.RapidityToVelocity(finalRapidityMag);
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
            //oldCollisionResultVel3 = collisionResultVel3;
            //oldCollisionResultAngVel3 = collisionResultAngVel3;
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
            //Vector3 otherPRelVel = otherVel.AddVelocity(-playerVel);

            //We want to find the contact offset relative the centers of mass of in each object's inertial frame;
            Vector3 myLocPoint, otLocPoint, contact, com;
            if (myColliderIsMesh)
            {
                contact = contactPoint.point.OpticalToWorld(myVel, playerPos, playerVel);
                com = opticalWorldCenterOfMass.OpticalToWorld(myVel, playerPos, playerVel);
                myLocPoint = contact - com;
            }
            else if (nonrelativisticShader)
            {
                contact = contactPoint.point;
                com = opticalWorldCenterOfMass.OpticalToWorld(myVel, playerPos, playerVel);
                myLocPoint = (contact - com).InverseContractLengthBy(myVel);
            }
            else
            {
                contact = contactPoint.point;
                com = opticalWorldCenterOfMass.OpticalToWorld(myVel, playerPos, playerVel);
                myLocPoint = (contact - com);
            }

            if (otherRO.myColliderIsMesh)
            {
                contact = contactPoint.point.OpticalToWorld(otherVel, playerPos, playerVel);
                com = otherRO.opticalWorldCenterOfMass.OpticalToWorld(otherVel, playerPos, playerVel);
                otLocPoint = contact - com;
            }
            else if (otherRO.nonrelativisticShader)
            {
                contact = contactPoint.point;
                com = otherRO.opticalWorldCenterOfMass.OpticalToWorld(otherVel, playerPos, playerVel);
                otLocPoint = (contact - com).InverseContractLengthBy(otherVel);
            }
            else
            {
                contact = contactPoint.point;
                com = opticalWorldCenterOfMass.OpticalToWorld(otherVel, playerPos, playerVel);
                otLocPoint = (contact - com);
            }

            Vector3 myAngTanVel = Vector3.Cross(myAngVel, myLocPoint);
            Vector3 myTotalVel = myVel.AddVelocity(myAngTanVel);
            Vector3 otherAngTanVel = Vector3.Cross(otherAngVel, otLocPoint);
            Vector3 otherTotalVel = otherVel.AddVelocity(otherAngTanVel);
            Vector3 lineOfAction = -contactPoint.normal.InverseContractLengthBy(myVel);
            lineOfAction.Normalize();
            //Decompose velocity in parallel and perpendicular components:
            Vector3 myParraVel = Vector3.Project(myTotalVel, lineOfAction);
            Vector3 myPerpVel = (myTotalVel - myParraVel) * myParraVel.Gamma();
            //Vector3 testVel = myTotalVel.AddVelocity(-myPerpVel);
            //Boost to the inertial frame where my velocity is entirely along the line of action:
            Vector3 otherContactVel = otherTotalVel.AddVelocity(-myPerpVel);
            //Find the relative velocity:
            Vector3 relVel = myParraVel.RelativeVelocityTo(otherContactVel);
            //Find the relative rapidity on the line of action, where my contact velocity is 0:
            float relVelGamma = relVel.Gamma();
            //Vector3 rapidityOnLoA = relVelGamma * relVel;

            Vector3 myPos = opticalWorldCenterOfMass;
            Vector3 otherPos = otherRO.opticalWorldCenterOfMass;
            float penDist = GetPenetrationDepth(collision, myColliders[0], myPRelVel, myPos, otherPos, ref contactPoint);

            if (penDist <= 0.0f)
            {
                return;
            }

            //Rotate my relative contact point:
            Vector3 rotatedLoc = Quaternion.Inverse(transform.rotation) * myLocPoint;
            //The relative contact point is the lever arm of the torque:
            float myMOI = Vector3.Dot(myRigidbody.inertiaTensor, new Vector3(rotatedLoc.x * rotatedLoc.x, rotatedLoc.y * rotatedLoc.y, rotatedLoc.z * rotatedLoc.z));

            float impulse = (float)(hookeMultiplier * combYoungsModulus * penDist * state.FixedDeltaTimeWorld * GetGtt());

            //The change in rapidity on the line of action:
            Vector3 finalLinearRapidity = relVelGamma * myVel + impulse / mass * lineOfAction;
            //The change in rapidity perpendincular to the line of action:
            Vector3 finalTanRapidity = relVelGamma * myAngTanVel + Vector3.Cross(1.0f / myMOI * Vector3.Cross(myLocPoint, impulse * lineOfAction), myLocPoint);
            //Velocities aren't linearly additive in special relativity, but rapidities are:
            float finalRapidityMag = (finalLinearRapidity + finalTanRapidity).magnitude;
            Vector3 tanVelFinal = finalTanRapidity.RapidityToVelocity(finalRapidityMag);
            //This is a hack. We save the new velocities to overwrite the Rigidbody velocities on the next frame:
            collisionResultVel3 = finalLinearRapidity.RapidityToVelocity(finalRapidityMag);
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

            didCollide = true;
        }

        private float GetPenetrationDepth(Collision collision, Collider myCollider, Vector3 myPRelVel, Vector3 myPos, Vector3 otherPos, ref PointAndNorm contactPoint)
        {
            //Raycast with other collider in a collision

            //float gamma = myPRelVel.Gamma();

            Vector3 testNormal;
            float penDist = 0.0f;
            float penTest = 0.0f;
            Vector3 myExtents = myCollider.bounds.extents;
            Vector3 otherExtents = collision.collider.bounds.extents;
            float startDist = 4.0f * Mathf.Max(otherExtents.x, otherExtents.y, otherExtents.z, myExtents.x, myExtents.y, myExtents.z);
            RaycastHit hitInfo;
            ContactPoint point;
            float maxLCV = collision.contacts.Length;
            Ray ray = new Ray();
            for (int i = 0; i < maxLCV; i++)
            {
                point = collision.contacts[i];
                testNormal = point.normal;
                ray.origin = myPos + startDist * testNormal;
                ray.direction = -testNormal;

                if (collision.collider.Raycast(ray, out hitInfo, startDist))
                {
                    penTest = hitInfo.distance - startDist;
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

                ray.origin = otherPos - startDist * testNormal;
                ray.direction = testNormal;

                if (myCollider.Raycast(ray, out hitInfo, startDist))
                {
                    penTest = hitInfo.distance - startDist;
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

        private void SetUpContractor()
        {
            if (contractor == null)
            {
                GameObject contractorGO = new GameObject();
                contractorGO.name = gameObject.name + " Contractor";
                contractor = contractorGO.transform;
                contractor.parent = transform.parent;
            }
            else
            {
                Transform prnt = contractor.parent;
                contractor.parent = null;
                transform.parent = null;
                contractor.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                contractor.parent = prnt;
            }
            contractor.position = transform.position;
            transform.parent = contractor;
            transform.localPosition = Vector3.zero;
            contractorLocalScale = contractor.localScale;
        }

        public void ContractLength()
        {
            Vector3 playerVel = state.PlayerVelocityVector;
            Vector3 relVel = viw.RelativeVelocityTo(playerVel);
            float relVelMag = relVel.sqrMagnitude;

            if (relVelMag > (state.SpeedOfLightSqrd * 0.95f))
            {
                relVel.Normalize();
                relVelMag = (float)(state.SpeedOfLightSqrd * 0.95f);
                relVel = relVelMag * relVel;
            }

            //Undo length contraction from previous state, and apply updated contraction:
            // - First, return to world frame:
            contractor.localScale = contractorLocalScale;
            if ((contractor.lossyScale - new Vector3(1.0f, 1.0f, 1.0f)).sqrMagnitude > 0.0001)
            {
                //If we can't avoid (expensive) re-parenting, we do it:
                SetUpContractor();
            }

            if (relVelMag > 0.0f)
            {
                Quaternion rot = transform.rotation;

                relVelMag = Mathf.Sqrt(relVelMag);
                // - If we need to contract the object, unparent it from the contractor before rotation:
                //transform.parent = cparent;

                Quaternion origRot = transform.rotation;

                // - Rotate contractor to point parallel to velocity relative player:
                contractor.rotation = Quaternion.FromToRotation(Vector3.forward, relVel / relVelMag);

                // - Re-parent the object to the contractor before length contraction:
                transform.rotation = origRot;

                // - Set the scale based only on the velocity relative to the player:
                contractor.localScale = contractorLocalScale.ContractLengthBy(relVelMag * Vector3.forward);
            }
        }

        //This is the "t-t" or "0-0" component of the metric tensor in an accelerated frame in special relativity.
        // It appears to change due to proper acceleration from the player's/camera's point of view, since acceleration is not relative.
        // It also depends on an object's distance from the player, so it is calculated by and for the object itself.
        public float GetGtt()
        {
            Vector3 playerPos = state.playerTransform.position;
            Vector3 playerVel = state.PlayerVelocityVector;
            return (float)Math.Pow(1.0 + 1.0 / state.SpeedOfLightSqrd * Vector3.Dot(-state.PlayerAccelerationVector, transform.position.WorldToOptical(viw, playerPos, playerVel)), 2);
        }
    }
}