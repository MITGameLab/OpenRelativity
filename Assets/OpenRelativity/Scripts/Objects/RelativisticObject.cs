using System;
using System.Collections;
using System.Collections.Generic;
//using System.Linq; //For debugging purposes only
using UnityEngine;

namespace OpenRelativity.Objects
{
    public class RelativisticObject : MonoBehaviour
    {
        #region Public Settings
        public bool isKinematic = false;
        public bool isLightMapStatic = false;
        public bool useGravity;
        public Vector3 initialViw;
        public Vector3 initialAviw;
        #endregion

        #region Rigid body physics
        // How long (in seconds) do we wait before we detect collisions with an object we just collided with?
        private float collideWait = 0f;
        // If we have intrinsic proper acceleration besides gravity, how quickly does it degrade?
        // (This isn't physically how we want to handle changing acceleration, but it's a stop-gap to experiment with smooth-ish changes in proper acceleration.)
        private float accelDrag = 0f;

        private Vector3 _viw = Vector3.zero;
        public Vector3 viw
        {
            get
            {
                return _viw;
            }

            set
            {
                // Skip this all, if the change is negligible.
                if (IsNaNOrInf(value.sqrMagnitude) || (value - _viw).sqrMagnitude < SRelativityUtil.divByZeroCutoff)
                {
                    return;
                }

                if (isKinematic)
                {
                    _viw = value;
                    return;
                }

                // This keeps the public parameter up-to-date:
                initialViw = value;

                UpdateViwAndAccel(_viw, _properAiw, value, _properAiw);
            }
        }
        public Matrix4x4 viwLorentz { get; private set; }

        //Store this object's angular velocity here.
        private Vector3 _aviw;
        public Vector3 aviw
        {
            get
            {
                return _aviw;
            }
            set
            {
                if (!isKinematic)
                {
                    initialAviw = value;
                    _aviw = value;
                    UpdateRigidbodyVelocity(viw, value);
                }
            }
        }

        //Store object's acceleration;
        public Vector3 _properAiw;
        public Vector3 properAiw
        {
            get
            {
                return _properAiw;
            }

            set
            {
                // Skip this all, if the change is negligible.
                if (IsNaNOrInf(value.sqrMagnitude) || (value - _properAiw).sqrMagnitude < SRelativityUtil.divByZeroCutoff)
                {
                    return;
                }

                if (isKinematic)
                {
                    _properAiw = value;
                    return;
                }

                UpdateViwAndAccel(_viw, _properAiw, _viw, value);
            }
        }

        public void UpdateViwAndAccel(Vector3 vi, Vector3 ai, Vector3 vf, Vector3 af)
        {
            //Changing velocities lose continuity of position,
            // unless we transform the world position to optical position with the old velocity,
            // and inverse transform the optical position with the new the velocity.
            // (This keeps the optical position fixed.)

            piw = ((Vector4)((Vector4)piw).WorldToOptical(vi, ai)).OpticalToWorldHighPrecision(vf, af);

            if (!IsNaNOrInf(piw.magnitude))
            {
                if (nonrelativisticShader)
                {
                    UpdateContractorPosition();
                }
                else
                {
                    transform.position = piw;
                }
            }

            _viw = vf;
            _properAiw = af;

            // Also update the Rigidbody and Collider, if any
            UpdateRigidbodyVelocity(_viw, _aviw);
            UpdateColliderPosition();

            // Update the shader parameters if necessary
            UpdateShaderParams();
        }

        public bool isRBKinematic
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

        //TODO: Rigidbody doesn't stay asleep. Figure out why, and get rid of this:
        private bool isSleeping;
        #endregion
        //Keep track of our own Mesh Filter
        private MeshFilter meshFilter;
        //Store our raw vertices in this variable, so that we can refer to them later.
        private Vector3[] rawVertsBuffer;
        //To avoid garbage collection, we might over-allocate the buffer:
        private int rawVertsBufferLength;

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
        //Store world position, mostly for a nonrelativistic shader:
        public Vector3 piw { get; set; }
        //Store rotation quaternion
        public Quaternion riw { get; set; }

        //We use an attached shader to transform the collider verts:
        public ComputeShader colliderShader;
        //We set global constants in a struct;
        public ShaderParams colliderShaderParams;
        //We save and reuse the transformed vert array to avoid garbage collection 
        private Vector3[] trnsfrmdMeshVerts;
        //If we have a collider to transform, we cache it here
        private Collider[] myColliders;
        //If we specifically have a mesh collider, we need to know to transform the verts of the mesh itself.
        private bool myColliderIsMesh;
        private bool myColliderIsBox;
        private bool myColliderIsVoxel;
        //We create a new collider mesh, so as not to interfere with primitives, and reuse it
        private Mesh trnsfrmdMesh;
        //If we have a Rigidbody, we cache it here
        private Rigidbody myRigidbody;
        //If we have a Renderer, we cache it, too.
        public Renderer myRenderer { get; set; }

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

        private bool IsNaNOrInf(double p)
        {
            return double.IsInfinity(p) || double.IsNaN(p);
        }

        private bool IsNaNOrInf(float p)
        {
            return float.IsInfinity(p) || float.IsNaN(p);
        }

        private void UpdateMeshCollider(MeshCollider transformCollider)
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
                return;
            }
            else if (wasFrozen)
            {
                //Restore the state of the rigidbody, once.
                wasFrozen = false;
                myRigidbody.isKinematic = wasKinematic;
            }

            //Set remaining global parameters:
            colliderShaderParams.ltwMatrix = transform.localToWorldMatrix;
            colliderShaderParams.wtlMatrix = transform.worldToLocalMatrix;
            colliderShaderParams.vpc = (-state.PlayerVelocityVector).ToMinkowski4Viw() / (float)state.SpeedOfLight;
            colliderShaderParams.pap = state.PlayerAccelerationVector;
            colliderShaderParams.avp = state.PlayerAngularVelocityVector;
            colliderShaderParams.playerOffset = state.playerTransform.position;
            colliderShaderParams.speed = (float)(state.PlayerVelocity / state.SpeedOfLight);
            colliderShaderParams.spdOfLight = (float)state.SpeedOfLight;
            colliderShaderParams.vpcLorentzMatrix = state.PlayerLorentzMatrix;

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

            // Reset physics:
            //myRigidbody.ResetCenterOfMass();
            //myRigidbody.ResetInertiaTensor();

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

            viwLorentz = Matrix4x4.identity;
        }

        // Get the start time of our object, so that we know where not to draw it
        public void SetStartTime()
        {
            Vector3 playerPos = state.playerTransform.position;
            float timeDelayToPlayer = (float)Math.Sqrt((((Vector4)piw).WorldToOptical(viw, Get4Acceleration()) - playerPos).sqrMagnitude / state.SpeedOfLightSqrd);
            timeDelayToPlayer *= GetTimeFactor();
            startTime = (float)(state.TotalTimeWorld - timeDelayToPlayer);
            if (myRenderer != null)
                myRenderer.enabled = false;
        }
        //Set the death time, so that we know at what point to destroy the object in the player's view point.
        public virtual void SetDeathTime()
        {
            Vector3 playerPos = state.playerTransform.position;
            float timeDelayToPlayer = (float)Math.Sqrt((((Vector4)piw).WorldToOptical(viw, Get4Acceleration()) - playerPos).sqrMagnitude / state.SpeedOfLightSqrd);
            timeDelayToPlayer *= GetTimeFactor();
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
            _properAiw = useGravity ? Physics.gravity : Vector3.zero;

            piw = transform.position;
            riw = transform.rotation;

            isSleeping = false;
            myRigidbody = GetComponent<Rigidbody>();
            rawVertsBufferLength = 0;
            wasKinematic = false;
            wasFrozen = false;

            staticResetPlayerSpeedSqr = (float)(state.SpeedOfLightSqrd * 0.005f * 0.005f);

            UpdateCollider();

            MarkStaticColliderPos();

            //Get the meshfilter
            if (isParent)
            {
                CombineParent();
            }
            meshFilter = GetComponent<MeshFilter>();

            if (myColliders != null)
            {
                for (int i = 0; i < myColliders.Length; i++)
                {
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
            }

            //Get the vertices of our mesh
            if (meshFilter != null)
            {
                rawVertsBufferLength = meshFilter.mesh.vertices.Length;
                rawVertsBuffer = meshFilter.mesh.vertices;
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

            colliderShaderParams.viw = new Vector4(0, 0, 0, 1);

            checkSpeed();

            //Also get the meshrenderer so that we can give it a unique material
            if (myRenderer == null)
            {
                myRenderer = GetComponent<Renderer>();
            }
            //If we have a MeshRenderer on our object and it's not world-static
            if (myRenderer != null && !isLightMapStatic)
            {
                float c = (float)state.SpeedOfLight;
                //And if we have a texture on our material
                for (int i = 0; i < myRenderer.materials.Length; i++)
                {
                    //if (tempRenderer.materials[i]!=null && tempRenderer.materials[i].mainTexture != null)
                    //{
                    //So that we can set unique values to every moving object, we have to instantiate a material
                    //It's the same as our old one, but now it's not connected to every other object with the same material
                    Material quickSwapMaterial = Instantiate(myRenderer.materials[i]) as Material;
                    //Then, set the value that we want
                    quickSwapMaterial.SetVector("_viw", new Vector4(0, 0, 0, 1));
                    quickSwapMaterial.SetVector("_aiw", new Vector4(0, 0, 0, 0));
                    quickSwapMaterial.SetMatrix("_viwLorentzMatrix", Matrix4x4.identity);


                    //And stick it back into our renderer. We'll do the SetVector thing every frame.
                    myRenderer.materials[i] = quickSwapMaterial;
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
            UpdateContractorPosition();
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
        }

        private void EnforceCollision()
        {
            // Like how Rigidbody components are co-opted for efficient relativistic motion,
            // it's feasible to get (at least reasonable, if not exact) relativistic collision
            // handling by transforming the end state after PhysX collisions.

            // We pass the RelativisticObject's rapidity to the rigidbody, right before the physics update
            // We restore the time-dilated visual apparent velocity, afterward.

            // Get the position and rotation after the collision:
            riw = myRigidbody.rotation;
            //piw = nonrelativisticShader ? ((Vector4)transform.position).OpticalToWorldHighPrecision(viw, Get4Acceleration()) : transform.position;

            // Now, update the velocity and angular velocity based on the collision result:
            Vector3 myViw = myRigidbody.velocity.RapidityToVelocity();
            // Make sure we're not updating to faster than max speed
            float mySpeed = myViw.magnitude;
            if (mySpeed > state.MaxSpeed)
            {
                myViw = (float)state.MaxSpeed / mySpeed * myViw;
            }

            float gamma = GetTimeFactor(myViw);
            Vector3 myAccel = accelDrag <= 0 ? properAiw : (properAiw + (myViw * gamma - viw * viw.Gamma()) * Mathf.Log(1 + (float)state.FixedDeltaTimePlayer * accelDrag) / accelDrag);

            UpdateViwAndAccel(viw, properAiw, myViw, myAccel);
            aviw = myRigidbody.angularVelocity / gamma;
        }

        public void Update()
        {
            if (state.MovementFrozen || nonrelativisticShader || meshFilter != null)
            {
                UpdateShaderParams();
                return;
            }

            if (myRigidbody != null)
            {
                UpdateRigidbodyVelocity(viw, aviw);
            }

            ObjectMeshDensity density = GetComponent<ObjectMeshDensity>();

            if (density == null)
            {
                UpdateShaderParams();
                return;
            }

            #region meshDensity
            //This is where I'm going to change our mesh density.
            //I'll take the model, and pass MeshDensity the mesh and unchanged vertices
            //If it comes back as having changed something, I'll edit the mesh.

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
            #endregion

            UpdateShaderParams();
        }

        public float GetTisw(Vector3? pos = null)
        {
            if (pos == null)
            {
                pos = piw;
            }
            return ((Vector4)pos.Value).GetTisw(viw, properAiw);
        }

        void FixedUpdate() {
            if (state.MovementFrozen)
            {
                // If our rigidbody is not null, and movement is frozen, then set the object to standstill.
                if (myRigidbody != null)
                {
                    myRigidbody.velocity = Vector3.zero;
                    myRigidbody.angularVelocity = Vector3.zero;
                }

                // We're done.
                return;
            }

            // FOR THE PHYSICS UPDATE ONLY, we give our rapidity to the Rigidbody
            //EnforceCollision();

            float deltaTime = (float)state.FixedDeltaTimePlayer * GetTimeFactor();
            float localDeltaT = deltaTime - (float)state.FixedDeltaTimeWorld;

            if (state.conformalMap != null)
            {
                //Update comoving position
                Vector4 piw4 = state.conformalMap.ComoveOptical(deltaTime, piw);
                float testMag = piw4.sqrMagnitude;
                if (!IsNaNOrInf(testMag))
                {
                    piw = piw4;
                    if (nonrelativisticShader)
                    {
                        contractor.position = ((Vector4)piw).WorldToOptical(viw, Get4Acceleration());
                        transform.localPosition = Vector3.zero;
                    }
                    deltaTime = piw4.w;
                    localDeltaT = deltaTime - (float)state.FixedDeltaTimeWorld;
                }
            }

            if (!IsNaNOrInf(localDeltaT))
            {
                localTimeOffset += localDeltaT;
            }

            if (meshFilter != null)
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
            }

            #region rigidbody
            // The rest of the updates are for objects with Rigidbodies that move and aren't asleep.
            if (isKinematic || isSleeping || myRigidbody == null)
            {

                if (myRigidbody != null)
                {
                    myRigidbody.velocity = Vector3.zero;
                    myRigidbody.angularVelocity = Vector3.zero;
                }

                viw = Vector4.zero;
                aviw = Vector4.zero;

                if (!myColliderIsVoxel)
                {
                    UpdateColliderPosition();
                }

                UpdateShaderParams();

                // We're done.
                return;
            }

            if (accelDrag > 0)
            {

                Vector3 myAccel = properAiw;

                if (useGravity)
                {
                    myAccel -= Physics.gravity;
                }

                float jerkDiff = (1 + deltaTime * accelDrag);
                myAccel = myAccel / jerkDiff;

                if (useGravity)
                {
                    myAccel += Physics.gravity;
                }

                properAiw = myAccel;
            }

            // Accelerate after updating gravity's effect on proper acceleration
            viw += properAiw * deltaTime;

            // Set the Rigidbody parameters, dependent on player's point of view
            UpdateRigidbodyVelocity(viw, aviw);

            Vector3 testVec = deltaTime * viw;
            if (!IsNaNOrInf(testVec.sqrMagnitude))
            {
                Quaternion diffRot = Quaternion.Euler(deltaTime * aviw);
                riw = riw * diffRot;
                myRigidbody.MoveRotation(riw);

                piw += testVec;

                if (nonrelativisticShader)
                {
                    transform.localPosition = Vector3.zero;
                    testVec = ((Vector4)piw).WorldToOptical(viw, Get4Acceleration());
                    if (!IsNaNOrInf(testVec.sqrMagnitude))
                    {
                        contractor.position = testVec;
                        ContractLength();
                    }
                }
                else
                {
                    myRigidbody.MovePosition(piw);
                }
            }

            if (!myColliderIsVoxel)
            {
                UpdateColliderPosition();
            }
            #endregion

            // FOR THE PHYSICS UPDATE ONLY, we give our rapidity to the Rigidbody
            float gamma = viw.Gamma();
            myRigidbody.velocity = gamma * viw;
            myRigidbody.angularVelocity = gamma * aviw;
        }

        public void UpdateColliderPosition(Collider toUpdate = null)
        {
            Matrix4x4 vpcLorentz = state.PlayerLorentzMatrix;

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
                if (myColliderIsMesh && (colliderShader != null) && SystemInfo.supportsComputeShaders && state.IsInitDone)
                {
                    for (int i = 0; i < myColliders.Length; i++)
                    {
                        UpdateMeshCollider((MeshCollider)myColliders[i]);
                    }
                }
                //If we have a BoxCollider, transform its center to its optical position
                else if (myColliderIsBox)
                {
                    Vector4 aiw = Get4Acceleration();
                    Vector3 pos;
                    BoxCollider collider;
                    Vector3 testPos;
                    float testMag;
                    for (int i = 0; i < myColliders.Length; i++)
                    {
                        collider = (BoxCollider)myColliders[i];
                        pos = transform.InverseTransformPoint(((Vector4)colliderPiw[i]));
                        testPos = transform.InverseTransformPoint(((Vector4)pos).WorldToOptical(viw, aiw, viwLorentz));
                        testMag = testPos.sqrMagnitude;
                        if (!IsNaNOrInf(testMag))
                        {
                            collider.center = testPos;
                        }
                    }
                }
            }
        }

        private void UpdateShaderParams()
        {
            //Send our object's v/c (Velocity over the Speed of Light) to the shader
            if (myRenderer != null && !isLightMapStatic)
            {
                Vector4 tempViw = viw.ToMinkowski4Viw() / (float)state.SpeedOfLight;
                Vector3 tempAviw = aviw;
                Vector3 tempPiw = transform.position;
                Vector4 tempAiw = Get4Acceleration();

                //Velocity of object Lorentz transforms are the same for all points in an object,
                // so it saves redundant GPU time to calculate them beforehand.
                Matrix4x4 viwLorentzMatrix = SRelativityUtil.GetLorentzTransformMatrix(tempViw);

                colliderShaderParams.viw = tempViw;
                colliderShaderParams.aiw = tempAiw;
                colliderShaderParams.viwLorentzMatrix = viwLorentzMatrix;
                for (int i = 0; i < myRenderer.materials.Length; i++)
                {
                    myRenderer.materials[i].SetVector("_viw", tempViw);
                    myRenderer.materials[i].SetVector("_aiw", tempAiw);
                    myRenderer.materials[i].SetMatrix("_viwLorentzMatrix", viwLorentzMatrix);
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
        }

        public void ResetDeathTime()
        {
            DeathTime = float.PositiveInfinity;
        }

        #region 4D Rigid body mechanics

        private IEnumerator EnableCollision(float delay, Collider otherCollider)
        {
            yield return new WaitForSeconds(delay);
            Physics.IgnoreCollision(GetComponent<Collider>(), otherCollider, false);
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (myRigidbody == null || myColliders == null || myRigidbody.isKinematic)
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

            if ((viw.AddVelocity(-otherRO.viw).magnitude < Physics.bounceThreshold))
            {
                // If we're lower than the bounce threshold, just reset the state.
                // We often end up here when player acceleration puts high apparent curvature on a too low vertex mesh collider.
                // PhysX will force the objects apart, but this might be the least error we can get away with.
                viw = Vector3.zero;
                aviw = Vector3.zero;
                UpdateRigidbodyVelocity(viw, aviw);

                return;
            }

            //If we made it this far, we shouldn't be sleeping:
            WakeUp();

            // We don't want to bug out, on many collisions with the same object
            if (collideWait > 0)
            {
                Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider, true);
                StartCoroutine(EnableCollision(collideWait, collision.collider));
            }

            // Let's start simple:
            // At low enough velocities, where the Newtonian approximation is reasonable,
            // PhysX is probably MORE accurate for even relativistic collision than the hacky relativistic collision we had
            // (which is still in the commit history, for reference).
            EnforceCollision();
        }

        public void OnCollisionStay(Collision collision)
        {
            if (myRigidbody == null || myRigidbody.isKinematic)
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

            if ((viw.AddVelocity(-otherRO.viw).magnitude < Physics.bounceThreshold))
            {
                // If we're lower than the bounce threshold, just reset the state.
                // We often end up here when player acceleration puts high apparent curvature on a too low vertex mesh collider.
                // PhysX will force the objects apart, but this might be the least error we can get away with.
                viw = Vector3.zero;
                aviw = Vector3.zero;
                UpdateRigidbodyVelocity(viw, aviw);

                return;
            }

            //If we made it this far, we shouldn't be sleeping:
            WakeUp();

            // We don't want to bug out, on many collisions with the same object
            if (collideWait > 0)
            {
                Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider, true);
                StartCoroutine(EnableCollision(collideWait, collision.collider));
            }

            // Let's start simple:
            // At low enough velocities, where the Newtonian approximation is reasonable,
            // PhysX is probably MORE accurate for even relativistic collision than the hacky relativistic collision we had
            // (which is still in the commit history, for reference).
            EnforceCollision();
        }

        public void Sleep()
        {
            viw = Vector3.zero;
            aviw = Vector3.zero;
            properAiw = Vector3.zero;

            if (myRigidbody != null)
            {
                myRigidbody.velocity = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;
                myRigidbody.Sleep();
            }

            isSleeping = true;
        }

        public void WakeUp()
        {
            isSleeping = false;
            if (myRigidbody != null)
            {
                myRigidbody.WakeUp();
            }
        }
        #endregion

        private void SetUpContractor()
        {
            if (contractor != null)
            {
                Transform prnt = contractor.parent;
                contractor.parent = null;
                contractor.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                transform.parent = null;
                Destroy(contractor.gameObject);
            }
            GameObject contractorGO = new GameObject();
            contractorGO.name = gameObject.name + " Contractor";
            contractor = contractorGO.transform;
            contractor.parent = transform.parent;
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

            if (relVelMag > (state.MaxSpeed))
            {
                relVel.Normalize();
                relVelMag = (float)state.MaxSpeed;
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

        //This is the metric tensor in an accelerated frame in special relativity.
        // Special relativity assumes a flat metric, (the "Minkowski metric").
        // In general relativity, the underlying metric could be curved, according to the Einstein field equations.
        // The (flat) metric appears to change due to proper acceleration from the player's/camera's point of view, since acceleration is not physically relative like velocity.
        // (Physically, proper acceleration could be detected by a force on the observer in the opposite direction from the acceleration,
        // like being pushed back into the seat of an accelerating car. When we stand still on the surface of earth, we feel our weight pushed into
        // the surface of the planet due to gravity, which is equivalent to an acceleration in the opposite direction, upwards, similar to the car.
        // "Einstein equivalence principle" says that, over small enough regions, we can't tell the difference between
        // a uniform acceleration and a gravitational field, that the two are physically equivalent over small enough regions of space.
        // In free-fall, gravitational fields disappear. Hence, when the player is in free-fall, their acceleration is considered to be zero,
        // while it is considered to be "upwards" when they are at rest under the effects of gravity, so they don't fall through the surface they're feeling pushed into.)
        // The apparent deformation of the Minkowski metric also depends on an object's distance from the player, so it is calculated by and for the object itself.
        public Matrix4x4 GetMetric()
        {   
            return SRelativityUtil.GetRindlerMetric(piw);
        }

        public Vector4 Get4Acceleration()
        {
            return properAiw.ProperToWorldAccel(viw);
        }

        private void UpdateRigidbodyVelocity(Vector3 mViw, Vector3 mAviw)
        {
            if (myRigidbody == null ||
                // Not a meaningful quantity, just to check if either parameter is inf/nan
                IsNaNOrInf((mViw + mAviw).magnitude))
            {
                return;
            }

            // If movement is frozen, set to zero.
            if (state.MovementFrozen)
            {
                myRigidbody.velocity = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;

                return;
            }

            // If we're in an invalid state, (such as before full initialization,) set to zero.
            float timeFac = GetTimeFactor();
            if (IsNaNOrInf(timeFac) || timeFac == 0)
            {
                myRigidbody.velocity = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;

                return;
            }

            myRigidbody.velocity = mViw * timeFac;
            myRigidbody.angularVelocity = mAviw * timeFac;
        }

        public void UpdateContractorPosition()
        {
            if (nonrelativisticShader)
            {
                if (contractor == null)
                {
                    SetUpContractor();
                }
                contractor.position = ((Vector4)piw).WorldToOptical(viw, Get4Acceleration());
                transform.localPosition = Vector3.zero;
                ContractLength();
            }
        }

        public float GetTimeFactor(Vector3? pVel = null)
        {
            if (!pVel.HasValue)
            {
                pVel = state.PlayerVelocityVector;
            }

            Matrix4x4 metric = GetMetric();

            float timeFac = 1 / Mathf.Sqrt(1 - (float)(Vector4.Dot(pVel.Value, metric * pVel.Value) / state.SpeedOfLightSqrd));
            if (IsNaNOrInf(timeFac))
            {
                timeFac = 1;
            }

            return timeFac;
        }
    }
}