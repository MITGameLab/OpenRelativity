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
        // Set with Rigidbody isKinematic flag instead
        private bool _isKinematic = false;
        public bool isKinematic
        {
            get
            {
                if (myRigidbody != null)
                {
                    _isKinematic = myRigidbody.isKinematic;
                }

                return _isKinematic;
            }

            set
            {
                _isKinematic = value;

                if (myRigidbody != null)
                {
                    myRigidbody.isKinematic = value;
                }
            }
        }
        public bool isLightMapStatic = false;
        public bool useGravity;
        #endregion

        #region Rigid body physics
        // How long (in seconds) do we wait before we detect collisions with an object we just collided with?
        private float collideWait = 0f;

        private bool isResting;

        public Vector3 _viw = Vector3.zero;
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

                UpdateViwAndAccel(value, _nonGravAccel);
                UpdateRigidbodyVelocity(_viw, _aviw);
            }
        }
        public Matrix4x4 viwLorentz { get; private set; }
        public Vector3 cviw;

        //Store this object's angular velocity here.
        public Vector3 _aviw;
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
                    _aviw = value;
                    UpdateRigidbodyVelocity(_viw, value);
                }
            }
        }

        // This is the part of acceleration that can be set. It neglects gravity.
        public Vector3 _nonGravAccel;
        public Vector3 nonGravAccel
        {
            get
            {
                return _nonGravAccel;
            }

            set
            {
                // Skip this all, if the change is negligible.
                if (IsNaNOrInf(value.sqrMagnitude) || (value - _nonGravAccel).sqrMagnitude < SRelativityUtil.divByZeroCutoff)
                {
                    return;
                }

                if (!isKinematic)
                {
                    UpdateViwAndAccel(_viw, value);
                    UpdateRigidbodyVelocity(_viw, _aviw);
                }
            }
        }

        //This is truly the object's "proper" acceleration, corresponding with the force it feels.
        private Vector3 _properAccel;
        public Vector3 properAccel
        {
            get
            {
                _properAccel = monopoleAccel ? nonGravAccel + frameDragAccel : nonGravAccel;

                if (!isResting)
                {
                    return _properAccel;
                }

                if (useGravity)
                {
                    _properAccel -= Physics.gravity;
                }

                if (state.conformalMap != null)
                {
                    _properAccel += state.conformalMap.GetRindlerAcceleration(piw);
                }

                return _properAccel;
            }

            set
            {
                _properAccel = value - frameDragAccel;
                Vector3 accel = _properAccel;

                if (isResting)
                {
                    if (useGravity)
                    {
                        accel += Physics.gravity;
                    }

                    if (state.conformalMap != null)
                    {
                        accel -= state.conformalMap.GetRindlerAcceleration(piw);
                    }
                }

                nonGravAccel = accel;
            }
        }

        // This hack-around is to support Physics.gravity in a way that is acceptable for a video game.
        // It is the object's "visual" acceleration.
        public Vector3 aiw
        {
            get
            {
                Vector3 _aiw = properAccel;

                if (useGravity)
                {
                    _aiw += Physics.gravity;
                }

                if (state.conformalMap != null)
                {
                    _aiw -= state.conformalMap.GetRindlerAcceleration(piw);
                }

                return _aiw;
            }
            set
            {
                Vector3 _aiw = value;

                if (useGravity)
                {
                    _aiw -= Physics.gravity;
                }

                if (state.conformalMap != null)
                {
                    _aiw += state.conformalMap.GetRindlerAcceleration(piw);
                }

                properAccel = _aiw;
            }
        }

        public void UpdateViwAndAccel(Vector3 vf, Vector3 af)
        {
            // Changing velocities lose continuity of position,
            // unless we transform the world position to optical position with the old velocity,
            // and inverse transform the optical position with the new the velocity.
            // (This keeps the optical position fixed.)

            Vector3 vi = _viw;
            Vector3 ai = aiw;

            _viw = vf;
            _nonGravAccel = af;

            piw = ((Vector4)((Vector4)piw).WorldToOptical(vi, ai.ProperToWorldAccel(vi, GetTimeFactor()))).OpticalToWorldHighPrecision(vf, aiw.ProperToWorldAccel(vf, GetTimeFactor()));

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

            // Update the shader parameters if necessary
            UpdateShaderParams();
        }

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
        public Vector3 _localScale;
        public Vector3 localScale
        {
            get
            {
                return _localScale;
            }
            set
            {
                transform.localScale = value;
                _localScale = value;
            }
        }
        //private int? oldParentID;
        //Store world position, mostly for a nonrelativistic shader:
        public Vector3 piw { get; set; }
        public void ResetPiw()
        {
            piw = nonrelativisticShader ? ((Vector4)transform.position).OpticalToWorldHighPrecision(viw, Get4Acceleration()) : transform.position;
        }
        //Store rotation quaternion
        public Quaternion riw { get; set; }

        //We use an attached shader to transform the collider verts:
        public ComputeShader colliderShader;
        //We set global constants in a struct;
        private ShaderParams colliderShaderParams;
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
        public Vector3[] colliderPiw { get; set; }

        private ComputeBuffer paramsBuffer;
        private ComputeBuffer vertBuffer;

        //We need to freeze any attached rigidbody if the world states is frozen 
        public bool wasKinematic { get; set; }
        private CollisionDetectionMode collisionDetectionMode;
        public bool wasFrozen { get; set; }

        // Based on Strano 2019, (preprint).
        // (I will always implement potentially "cranky" features so you can toggle them off, but I might as well.)
        public bool monopoleAccel = false;
        private Vector3 frameDragAccel;

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
                    collisionDetectionMode = myRigidbody.collisionDetectionMode;
                    myRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    myRigidbody.isKinematic = true;
                }
                return;
            }
            else if (wasFrozen)
            {
                //Restore the state of the rigidbody, once.
                wasFrozen = false;
                myRigidbody.isKinematic = wasKinematic;
                myRigidbody.collisionDetectionMode = collisionDetectionMode;
            }

            if (rawVertsBufferLength == 0)
            {
                return;
            }

            //Set remaining global parameters:
            colliderShaderParams.ltwMatrix = transform.localToWorldMatrix;
            colliderShaderParams.wtlMatrix = transform.worldToLocalMatrix;
            colliderShaderParams.vpc = -state.PlayerVelocityVector / (float)state.SpeedOfLight;
            colliderShaderParams.pap = state.PlayerAccelerationVector;
            colliderShaderParams.avp = state.PlayerAngularVelocityVector;
            colliderShaderParams.playerOffset = state.playerTransform.position;
            colliderShaderParams.spdOfLight = (float)state.SpeedOfLight;
            colliderShaderParams.vpcLorentzMatrix = state.PlayerLorentzMatrix;
            colliderShaderParams.invVpcLorentzMatrix = state.PlayerLorentzMatrix.inverse;

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
            _localScale = transform.localScale;
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
            cviw = Vector3.zero;
            frameDragAccel = Vector3.zero;
            ResetPiw();
            riw = transform.rotation;

            piw = ((Vector4)((Vector4)piw).WorldToOptical(Vector3.zero, Vector3.zero.ProperToWorldAccel(Vector3.zero, GetTimeFactor()))).OpticalToWorldHighPrecision(viw, aiw.ProperToWorldAccel(viw, GetTimeFactor()));

            if (nonrelativisticShader)
            {
                UpdateContractorPosition();
            }
            else
            {
                transform.position = piw;
            }

            // Update the shader parameters if necessary
            UpdateShaderParams();

            myRigidbody = GetComponent<Rigidbody>();
            rawVertsBufferLength = 0;
            wasKinematic = false;
            wasFrozen = false;

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
                    if (myColliderIsMesh && ((MeshCollider)myColliders[i]).sharedMesh != null)
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
                Vector3 center = camTransform.position;
                meshFilter.sharedMesh.bounds = new Bounds(distToCenter * camTransform.forward + center, 2 * distToCenter * Vector3.one);
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
                        if (myMeshColliders[i].sharedMesh != null)
                        {
                            trnsfrmdMesh = Instantiate(myMeshColliders[i].sharedMesh);
                            myMeshColliders[i].sharedMesh = trnsfrmdMesh;
                            trnsfrmdMesh.MarkDynamic();
                        }
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

        private void EnforceCollision(Collision collision)
        {
            // Like how Rigidbody components are co-opted for efficient relativistic motion,
            // it's feasible to get (at least reasonable, if not exact) relativistic collision
            // handling by transforming the end state after PhysX collisions.

            // We pass the RelativisticObject's rapidity to the rigidbody, right before the physics update
            // We restore the time-dilated visual apparent velocity, afterward

            if (useGravity && (collision.contacts.Length > 2))
            {
                ContactPoint contact = collision.contacts[0];
                if (Vector3.Dot(contact.normal, Vector3.up) > 0.5)
                {
                    viw = Vector3.zero;
                    aviw = Vector3.zero;
                    cviw = Vector3.zero;
                    isResting = true;

                    return;
                }
            }

            // Get the position and rotation after the collision:
            riw = myRigidbody.rotation;
            piw = nonrelativisticShader ? ((Vector4)myRigidbody.position).OpticalToWorldHighPrecision(viw, Get4Acceleration()) : myRigidbody.position;

            // Now, update the velocity and angular velocity based on the collision result:
            viw = myRigidbody.velocity.RapidityToVelocity(GetMetric());
            aviw = myRigidbody.angularVelocity / GetTimeFactor();

            // Make sure we're not updating to faster than max speed
            checkSpeed();

            UpdateContractorPosition();
            UpdateColliderPosition();
        }

        void Update()
        {
            if (myRigidbody != null)
            {
                UpdateRigidbodyVelocity(viw, aviw);
            }

            if (state.MovementFrozen || nonrelativisticShader || meshFilter != null)
            {
                UpdateShaderParams();
                return;
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
                if (!(density.state) && (meshFilter.transform.TransformPoint(rawVertsBuffer[0]).sqrMagnitude < (21000 * 21000)))
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
                else if (density.state && (meshFilter.transform.TransformPoint(rawVertsBuffer[0]).sqrMagnitude > (21000 * 21000)))
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
            return ((Vector4)pos.Value).GetTisw(viw, aiw);
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

            float deltaTime = (float)state.FixedDeltaTimePlayer * GetTimeFactor();
            float localDeltaT = deltaTime - (float)state.FixedDeltaTimeWorld;

            if (state.conformalMap != null)
            {
                Vector4 nPiw4 = state.conformalMap.ComoveOptical(deltaTime, piw);
                Vector3 pDiff = (Vector3)nPiw4 - piw;
                cviw = pDiff / deltaTime;
                piw = nPiw4;
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

            UpdateColliderPosition();

            #region rigidbody

            if (monopoleAccel)
            {
                Vector3 myAccel = properAccel;
                Vector3 pAccel = state.PlayerAccelerationVector;
                // To support Unity's concept of Newtonian gravity, we "cheat" a little on equivalence principle, here.
                // This isn't 100% right, but it keeps the world from looking like the space-time curvature is incomprehensibly 
                // warped in a "moderate" (really, extremely high) approximately Newtonian surface gravity.

                // If the RelativisticObject is at rest on the ground, according to Strano 2019, (not yet peer reviewed,)
                // it loses surface acceleration, (not weight force, directly,) the longer it stays in this configuration.
                Vector3 da = -myAccel.normalized * myAccel.sqrMagnitude / (float)state.SpeedOfLight * deltaTime;
                frameDragAccel += da;
                myAccel += da;
                // Per Strano 2019, due to the interaction with the thermal graviton gas radiated by the Rindler horizon,
                // there is also a change in mass. However, the monopole waves responsible for this is seen from a first-person perspective,
                // (i.e. as due to "player" acceleration).
                if (myRigidbody != null)
                {
                    // If a gravitating body this RO is attracted to is already excited above the rest mass vacuum,
                    // (which seems to imply the Higgs field vacuum)
                    // then it will spontaneously emit this excitation, with a coupling constant proportional to the
                    // gravitational constant "G" times (baryon) constituent particle rest mass.
                    // (For video game purposes, there's maybe no easy way to precisely model the mass flow, so just control it with an editor variable.)
                    float gravAccel = useGravity ? Physics.gravity.magnitude : 0;
                    gravAccel += state.conformalMap == null ? 0 : state.conformalMap.GetRindlerAcceleration(piw).magnitude;
                    myRigidbody.mass += state.planckMass * (state.gConst * myRigidbody.mass / state.planckMass) * ((state.fluxPerAccel * gravAccel - pAccel.magnitude) / state.planckAccel) * (deltaTime / state.planckTime);
                }
                //... But just turn "doDegradeAccel" off, if you don't want this effect for any reason.
                // (We ignore the "little bit" of acceleration from collisions, but maybe we could add that next.)

                properAccel = myAccel;
            }

            // The rest of the updates are for objects with Rigidbodies that move and aren't asleep.
            if (isKinematic || isResting || myRigidbody == null)
            {

                if (myRigidbody != null)
                {
                    myRigidbody.velocity = Vector3.zero;
                    myRigidbody.angularVelocity = Vector3.zero;
                }

                if (!isKinematic)
                {
                    viw = Vector3.zero;
                    aviw = Vector3.zero;
                    cviw = Vector3.zero;
                } else
                {
                    transform.position = nonrelativisticShader ? ((Vector4)piw).WorldToOptical(viw, Get4Acceleration()) : piw;
                }

                UpdateShaderParams();

                isResting = false;

                // We're done.
                return;
            }

            // Accelerate after updating gravity's effect on proper acceleration
            viw += aiw * deltaTime;

            Vector3 testVec = deltaTime * viw;
            if (!IsNaNOrInf(testVec.sqrMagnitude))
            {
                float aviwMag = aviw.magnitude;
                Quaternion diffRot;
                if (aviwMag < SRelativityUtil.divByZeroCutoff)
                {
                    diffRot = Quaternion.identity;
                }
                else
                {
                    diffRot = Quaternion.AngleAxis(Mathf.Rad2Deg * deltaTime * aviwMag, aviw / aviwMag);
                }
                riw = riw * diffRot;
                myRigidbody.MoveRotation(riw);

                piw += testVec;

                if (nonrelativisticShader)
                {
                    transform.parent = null;
                    testVec = ((Vector4)piw).WorldToOptical(viw, Get4Acceleration());
                    if (!IsNaNOrInf(testVec.sqrMagnitude))
                    {
                        myRigidbody.MovePosition(testVec);
                    }
                    contractor.position = myRigidbody.position;
                    transform.parent = contractor;
                    transform.localPosition = Vector3.zero;
                    ContractLength();
                }
                else
                {
                    myRigidbody.MovePosition(piw);
                }
            }
            #endregion

            // FOR THE PHYSICS UPDATE ONLY, we give our rapidity to the Rigidbody
            float gamma = GetTimeFactor(viw);
            myRigidbody.velocity = gamma * viw;
            myRigidbody.angularVelocity = gamma * aviw;
        }

        public void UpdateColliderPosition(Collider toUpdate = null)
        {
            Matrix4x4 vpcLorentz = state.PlayerLorentzMatrix;

            if (myColliderIsVoxel || nonrelativisticShader || myColliders == null || myColliders.Length == 0)
            {
                return;
            }

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
                Vector4 aiw4 = Get4Acceleration();
                Vector3 pos;
                BoxCollider collider;
                Vector3 testPos;
                float testMag;
                for (int i = 0; i < myColliders.Length; i++)
                {
                    collider = (BoxCollider)myColliders[i];
                    pos = transform.TransformPoint((Vector4)colliderPiw[i]);
                    testPos = transform.InverseTransformPoint(((Vector4)pos).WorldToOptical(viw, aiw4, viwLorentz));
                    testMag = testPos.sqrMagnitude;
                    if (!IsNaNOrInf(testMag))
                    {
                        collider.center = testPos;
                    }
                }
            }
        }

        private void UpdateShaderParams()
        {
            //Send our object's v/c (Velocity over the Speed of Light) to the shader
            if (myRenderer != null && !isLightMapStatic)
            {
                Vector3 tempViw = cviw.AddVelocity(viw) / (float)state.SpeedOfLight;
                Vector3 tempAviw = aviw;
                Vector4 tempAiw = Get4Acceleration();
                Vector4 tempVr = tempViw.AddVelocity(-(state.PlayerComovingVelocityVector.AddVelocity(state.PlayerVelocityVector))) / (float)state.SpeedOfLight;

                //Velocity of object Lorentz transforms are the same for all points in an object,
                // so it saves redundant GPU time to calculate them beforehand.
                Matrix4x4 viwLorentzMatrix = SRelativityUtil.GetLorentzTransformMatrix(tempViw);

                colliderShaderParams.viw = tempViw;
                colliderShaderParams.aiw = tempAiw;
                colliderShaderParams.viwLorentzMatrix = viwLorentzMatrix;
                colliderShaderParams.invViwLorentzMatrix = viwLorentzMatrix.inverse;
                for (int i = 0; i < myRenderer.materials.Length; i++)
                {
                    myRenderer.materials[i].SetVector("_viw", tempViw);
                    myRenderer.materials[i].SetVector("_aiw", tempAiw);
                    myRenderer.materials[i].SetMatrix("_viwLorentzMatrix", viwLorentzMatrix);
                    myRenderer.materials[i].SetMatrix("_invViwLorentzMatrix", viwLorentzMatrix.inverse);
                    myRenderer.materials[i].SetVector("_vr", tempVr);
                }
            }
        }

        public void KillObject()
        {
            gameObject.SetActive(false);
            //Destroy(this.gameObject);
        }

        //This is a function that just ensures we're slower than our maximum speed. The VIW that Unity sets SHOULD (it's creator-chosen) be smaller than the maximum speed.
        private void checkSpeed()
        {
            float maxSpeedSqr = (float)((state.MaxSpeed - 0.01f) * (state.MaxSpeed - 0.01f));

            if (viw.sqrMagnitude > maxSpeedSqr)
            {
                viw = viw.normalized * (float)(state.MaxSpeed - .01f);
            }

            // The tangential velocities of each vertex should also not be greater than the maximum speed.
            // (This is a relatively computationally costly check, but it's good practice.
            
            if (trnsfrmdMeshVerts != null)
            {
                for (int i = 0; i < trnsfrmdMeshVerts.Length; i++)
                {
                    Vector3 disp = Vector3.Scale(trnsfrmdMeshVerts[i], transform.lossyScale);
                    Vector3 tangentialVel = Vector3.Cross(aviw, disp);
                    float tanVelMagSqr = tangentialVel.sqrMagnitude;
                    if (tanVelMagSqr > maxSpeedSqr)
                    {
                        aviw = aviw.normalized * (float)(state.MaxSpeed - 0.01f) / disp.magnitude;
                    }
                }
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
            OnCollision(collision);
        }

        public void OnCollisionStay(Collision collision)
        {
            OnCollision(collision);
        }

        public void OnCollisionExit(Collision collision)
        {
            
        }

        public void OnCollision(Collision collision)
        {
            if (myRigidbody == null || myColliders == null || myRigidbody.isKinematic)
            {
                return;
            }

            // Let's start simple:
            // At low enough velocities, where the Newtonian approximation is reasonable,
            // PhysX is probably MORE accurate for even relativistic collision than the hacky relativistic collision we had
            // (which is still in the commit history, for reference).
            EnforceCollision(collision);
            // EnforceCollision() might opt not to set didCollide

            // We don't want to bug out, on many collisions with the same object
            if (collideWait > 0)
            {
                Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider, true);
                StartCoroutine(EnableCollision(collideWait, collision.collider));
            }
        }
        #endregion

        private void SetUpContractor()
        {
            _localScale = transform.localScale;
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
        }

        public void ContractLength()
        {
            Vector3 playerVel = state.PlayerVelocityVector;
            Vector3 relVel = cviw.AddVelocity(viw).AddVelocity(-(state.PlayerComovingVelocityVector.AddVelocity(playerVel)));
            float relVelMag = relVel.sqrMagnitude;

            if (relVelMag > (state.MaxSpeed))
            {
                relVel.Normalize();
                relVelMag = (float)state.MaxSpeed;
                relVel = relVelMag * relVel;
            }

            //Undo length contraction from previous state, and apply updated contraction:
            // - First, return to world frame:
            contractor.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            transform.localScale = _localScale;

            if (relVelMag > SRelativityUtil.divByZeroCutoff)
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
                contractor.localScale = new Vector3(1.0f, 1.0f, 1.0f).ContractLengthBy(relVelMag * Vector3.forward);
            }
        }

        //This is the metric tensor in an accelerated frame in special relativity.
        // Special relativity assumes a flat metric, (the "Minkowski metric").
        // In general relativity, the underlying metric could be curved, according to the Einstein field equations.
        // The (flat) metric appears to change due to proper acceleration from the player's/camera's point of view, since acceleration is not physically relative like velocity.
        // (Physically, proper acceleration could be detected by a force on the observer in the opposite direction from the acceleration,
        // like being pushed back into the seat of an accelerating car. When we stand still on the surface of earth, we feel our weight as the ground exerts a normal force "upward,"
        // which is equivalent to an acceleration in the opposite direction from the ostensible Newtonian gravity field, similar to the car.
        // "Einstein equivalence principle" says that, over small enough regions, we can't tell the difference between
        // a uniform acceleration and a gravitational field, that the two are physically equivalent over small enough regions of space.
        // In free-fall, gravitational fields disappear. Hence, when the player is in free-fall, their acceleration is considered to be zero,
        // while it is considered to be "upwards" when they are at rest under the effects of gravity, so they don't fall through the surface they're feeling pushed into.)
        // The apparent deformation of the Minkowski metric also depends on an object's distance from the player, so it is calculated by and for the object itself.
        public Matrix4x4 GetMetric()
        {   
            return SRelativityUtil.GetRindlerMetric(piw);
        }

        public Vector4 Get4Velocity()
        {
            return viw.ToMinkowski4Viw();
        }

        public Vector4 Get4Acceleration()
        {
            return aiw.ProperToWorldAccel(viw, GetTimeFactor(viw));
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

        // This is the factor commonly referred to as "gamma," for length contraction and time dilation,
        // only also with consideration for a gravitationally curved background, such as due to Rindler coordinates.
        // (Rindler coordinates are actually Minkowski flat, but the same principle applies.)
        public float GetTimeFactor(Vector3? pVel = null)
        {
            if (!pVel.HasValue)
            {
                // The common default case is, we want the player's "gamma,"
                // at this RO's position in space-time.
                pVel = state.PlayerVelocityVector;
            }

            // However, sometimes we want a different velocity, at this space-time point,
            // such as this RO's own velocity.

            Matrix4x4 metric = GetMetric();

            float timeFac = 1 / Mathf.Sqrt(1 + (float)(Vector4.Dot(pVel.Value, metric * pVel.Value) / state.SpeedOfLightSqrd));
            if (IsNaNOrInf(timeFac))
            {
                timeFac = 1;
            }

            return timeFac;
        }
    }
}