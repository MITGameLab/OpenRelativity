using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.Objects
{
    public class RelativisticObject : RelativisticBehavior
    {
        #region Public Settings
        public bool isLightMapStatic = false;
        public bool useGravity;
        // Use this instead of relativistic parent
        public bool isParent = false;
        // Combine colliders under us in the hierarchy
        public bool isCombinedColliderParent = false;
        // Use this if not using an explicitly relativistic shader
        public bool isNonrelativisticShader = false;
        // The composite scalar monopole graviton gas is described by statistical mechanics and heat flow equations
        public float gravitonEmissivity = 0.1f;
        // By default, 12g per baryon mole would be carbon-12, and this controls the total baryons estimated in the object
        public float averageMolarMass = 0.012f;

        // Set with Rigidbody isKinematic flag instead
        public bool isKinematic
        {
            get
            {
                if (myRigidbody != null)
                {
                    return myRigidbody.isKinematic;
                }

                return false;
            }

            set
            {
                if (myRigidbody != null)
                {
                    myRigidbody.isKinematic = value;
                }
            }
        }
        #endregion

        protected float updateViwTimeFactor;
        protected float updatePlayerViwTimeFactor;
        protected float updateTisw;
        protected Matrix4x4 updateMetric;
        protected Vector4 updateWorld4Acceleration;

        #region Local Time
        //Acceleration desyncronizes our clock from the world clock:
        public float localTimeOffset { get; private set; }
        public float localDeltaTime { get; private set; }
        public float localFixedDeltaTime { get; private set; }
        public float GetLocalTime()
        {
            return state.TotalTimeWorld + localTimeOffset;
        }
        public void ResetLocalTime()
        {
            localTimeOffset = 0.0f;
        }
        public float GetTisw(Vector3? pos = null)
        {
            if (pos == null)
            {
                pos = piw;
            }

            if (isPhysicsCacheValid && pos == piw)
            {
                return updateTisw;
            }

            return ((Vector4)pos.Value).GetTisw(viw, GetWorld4Acceleration());
        }
        public float GetVisualTime()
        {
            return GetLocalTime() + GetTisw();
        }
        #endregion

        #region 4-vector relativity
        // This is the metric tensor in an accelerated frame in special relativity.
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
            if (isPhysicsCacheValid)
            {
                return updateMetric;
            }

            return SRelativityUtil.GetRindlerMetric(piw);
        }

        public Vector4 Get4Velocity()
        {
            return viw.ToMinkowski4Viw();
        }

        public Vector4 GetWorld4Acceleration()
        {
            if (isPhysicsCacheValid)
            {
                return updateWorld4Acceleration;
            }

            return aiw.ProperToWorldAccel(viw, GetTimeFactor(viw));
        }

        public Vector4 GetProper4Acceleration()
        {
            return properAccel.ProperToWorldAccel(viw, GetTimeFactor(viw));
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

            if (isPhysicsCacheValid)
            {
                if (pVel == state.PlayerAngularVelocityVector)
                {
                    return updatePlayerViwTimeFactor;
                }

                if (pVel == viw)
                {
                    return updateViwTimeFactor;
                }
            }

            // However, sometimes we want a different velocity, at this space-time point,
            // such as this RO's own velocity.

            Matrix4x4 metric = GetMetric();

            float timeFac = pVel.Value.InverseGamma(metric);
            if (IsNaNOrInf(timeFac))
            {
                timeFac = 1;
            }

            return timeFac;
        }
        #endregion

        #region Rigid body physics
        private const float SleepThreshold = 0.01f;
        private const float SleepTime = 0.05f;
        private float SleepTimer;
        private bool wasKinematic;
        private CollisionDetectionMode collisionDetectionMode;

        public float baryonCount
        {
            get
            {
                return myRigidbody == null ? 0 : (myRigidbody.mass + frameDragMass) / averageMolarMass * SRelativityUtil.avogadroNumber;
            }
        }

        //Store world position, mostly for a nonrelativistic shader:
        public Vector3 piw { get; set; }

        public Vector3 opticalPiw {
            get
            {
                return ((Vector4)piw).WorldToOptical(viw, GetWorld4Acceleration());
            }
            set
            {
                piw = ((Vector4)value).OpticalToWorldHighPrecision(viw, GetWorld4Acceleration());
            }
        }

        public void ResetPiw()
        {
            piw = isNonrelativisticShader ? ((Vector4)transform.position).OpticalToWorldHighPrecision(viw, GetWorld4Acceleration()) : transform.position;
        }
        //Store rotation quaternion
        public Quaternion riw { get; set; }

        public Vector3 cviw { get; private set; }

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

                if ((SleepTimer <= 0) && (value.sqrMagnitude < SleepThreshold))
                {
                    return;
                }

                if (isKinematic)
                {
                    _viw = value;
                    return;
                }

                UpdateViwAndAccel(value, _nonGravAccel);
                UpdateRigidbodyVelocity();
            }
        }

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
                    UpdateRigidbodyVelocity();
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
                if (isKinematic || IsNaNOrInf(value.sqrMagnitude) || (value - _nonGravAccel).sqrMagnitude < SRelativityUtil.divByZeroCutoff)
                {
                    return;
                }

                UpdateViwAndAccel(_viw, value);
                UpdateRigidbodyVelocity();
            }
        }

        //This is truly the object's "proper" acceleration, corresponding with the force it feels.
        private Vector3 _properAccel;
        public Vector3 properAccel
        {
            get
            {
                _properAccel = isMonopoleAccel ? nonGravAccel + frameDragAccel : nonGravAccel;

                if (SleepTimer > 0)
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
                Vector3 accel = value;

                if (SleepTimer <= 0)
                {
                    if (state.conformalMap != null)
                    {
                        accel -= state.conformalMap.GetRindlerAcceleration(piw);
                    }

                    if (useGravity)
                    {
                        accel += Physics.gravity;
                    }
                }

                nonGravAccel = accel;
                _properAccel = isMonopoleAccel ? accel - frameDragAccel : accel;
            }
        }

        // This hack-around is to support Physics.gravity in a way that is acceptable for a video game.
        // It is the object's "visual" acceleration.
        public Vector3 aiw
        {
            get
            {
                if (SleepTimer <= 0)
                {
                    return Vector3.zero;
                }

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

            float timeFac = GetTimeFactor();

            piw = ((Vector4)((Vector4)piw).WorldToOptical(vi, ai.ProperToWorldAccel(vi, timeFac))).OpticalToWorldHighPrecision(vf, aiw.ProperToWorldAccel(vf, timeFac));

            if (!IsNaNOrInf(piw.magnitude))
            {
                if (isNonrelativisticShader)
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

        #region Nonrelativistic Shader/Collider
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
        //If we specifically have a mesh collider, we need to know to transform the verts of the mesh itself.
        private bool isMyColliderMesh;
        private bool isMyColliderBox;
        private bool isMyColliderVoxel;
        //If we have a collider to transform, we cache it here
        private Collider[] myColliders;
        private Vector3[] colliderPiw { get; set; }
        public void MarkStaticColliderPos()
        {
            if (isMyColliderBox && myColliders != null)
            {
                List<Vector3> sttcPosList = new List<Vector3>();
                for (int i = 0; i < myColliders.Length; i++)
                {
                    sttcPosList.Add(((BoxCollider)myColliders[i]).center);
                }
                colliderPiw = sttcPosList.ToArray();
            }
        }
        #endregion

        #region RelativisticObject properties and caching
        //Don't render if object has relativistic parent
        private bool hasParent = false;
        //Keep track of our own Mesh Filter
        private MeshFilter meshFilter;

        //When was this object created? use for moving objects
        private bool hasStarted;
        private float _StartTime = float.NegativeInfinity;
        public float StartTime { get { return _StartTime; } set { _StartTime = value; } }
        //When should we die? again, for moving objects
        private float _DeathTime = float.PositiveInfinity;
        public float DeathTime { get { return _DeathTime; } set { _DeathTime = value; } }

        //We save and reuse the transformed vert array to avoid garbage collection 
        private Vector3[] trnsfrmdMeshVerts;
        //We create a new collider mesh, so as not to interfere with primitives, and reuse it
        private Mesh trnsfrmdMesh;
        //If we have a Rigidbody, we cache it here
        private Rigidbody myRigidbody;
        //If we have a Renderer, we cache it, too.
        public Renderer myRenderer { get; set; }

        //We need to freeze any attached rigidbody if the world states is frozen 
        public bool wasFrozen { get; set; }

        // Based on Strano 2019, (preprint).
        // (I will always implement potentially "cranky" features so you can toggle them off, but I might as well.)
        public bool isMonopoleAccel = false;
        private Vector3 frameDragAccel;
        private float frameDragMass;
        #endregion

        #region Collider transformation and update
        // We use an attached shader to transform the collider verts:
        public ComputeShader colliderShader;
        // If the object is light map static, we need a duplicate of its mesh
        public Mesh colliderShaderMesh;
        // We set global constants in a struct
        private ShaderParams colliderShaderParams;
        // Mesh collider params
        private ComputeBuffer paramsBuffer;
        // Mesh collider vertices
        private ComputeBuffer vertBuffer;

        private void UpdateMeshCollider(MeshCollider transformCollider)
        {
            //Freeze the physics if the global state is frozen.
            if (state.isMovementFrozen)
            {
                if (!wasFrozen)
                {
                    //Read the state of the rigidbody and shut it off, once.
                    wasFrozen = true;
                    if (myRigidbody == null)
                    {
                        wasKinematic = true;
                        collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    } else
                    {
                        wasKinematic = myRigidbody.isKinematic;
                        collisionDetectionMode = myRigidbody.collisionDetectionMode;
                        myRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        myRigidbody.isKinematic = true;
                    }
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

            if (colliderShaderMesh == null || colliderShaderMesh.vertexCount == 0)
            {
                return;
            }

            if (paramsBuffer == null)
            {
                paramsBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(colliderShaderParams));

                // Skip the first frame, so PhysX can clean the mesh;
                return;
            }

            if (vertBuffer == null)
            {
                vertBuffer = new ComputeBuffer(colliderShaderMesh.vertexCount, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
            }

            //Set remaining global parameters:
            colliderShaderParams.ltwMatrix = transform.localToWorldMatrix;
            colliderShaderParams.wtlMatrix = transform.worldToLocalMatrix;
            colliderShaderParams.vpc = -state.PlayerVelocityVector / state.SpeedOfLight;
            colliderShaderParams.pap = state.PlayerAccelerationVector;
            colliderShaderParams.avp = state.PlayerAngularVelocityVector;
            colliderShaderParams.playerOffset = state.playerTransform.position;
            colliderShaderParams.spdOfLight = state.SpeedOfLight;
            colliderShaderParams.vpcLorentzMatrix = state.PlayerLorentzMatrix;
            colliderShaderParams.invVpcLorentzMatrix = state.PlayerLorentzMatrix.inverse;

            ShaderParams[] spa = new ShaderParams[1];
            spa[0] = colliderShaderParams;
            //Put verts in R/W buffer and dispatch:
            paramsBuffer.SetData(spa);
            vertBuffer.SetData(colliderShaderMesh.vertices);
            int kernel = colliderShader.FindKernel("CSMain");
            colliderShader.SetBuffer(kernel, "glblPrms", paramsBuffer);
            colliderShader.SetBuffer(kernel, "verts", vertBuffer);
            colliderShader.Dispatch(kernel, colliderShaderMesh.vertexCount, 1, 1);
            vertBuffer.GetData(trnsfrmdMeshVerts);

            //Change mesh:
            trnsfrmdMesh.vertices = trnsfrmdMeshVerts;
            trnsfrmdMesh.RecalculateBounds();
            trnsfrmdMesh.RecalculateNormals();
            transformCollider.sharedMesh = trnsfrmdMesh;
        }

        private void UpdateCollider()
        {
            MeshCollider[] myMeshColliders = GetComponents<MeshCollider>();

            //Get the vertices of our mesh
            if ((colliderShaderMesh == null) && (meshFilter != null) && meshFilter.sharedMesh.isReadable)
            {
                colliderShaderMesh = Instantiate(meshFilter.sharedMesh);
            }

            if (colliderShaderMesh != null)
            {
                trnsfrmdMesh = Instantiate(colliderShaderMesh);
                trnsfrmdMeshVerts = (Vector3[])trnsfrmdMesh.vertices.Clone();
                trnsfrmdMesh.MarkDynamic();

                if (!enabled || !gameObject.activeInHierarchy)
                {
                    UpdateMeshCollider(myMeshColliders[0]);
                }
            }

            if (GetComponent<ObjectBoxColliderDensity>() == null)
            {
                myColliders = myMeshColliders;
                if (myColliders.Length > 0)
                {
                    isMyColliderMesh = true;
                    isMyColliderBox = false;
                    isMyColliderVoxel = false;
                }
                else
                {
                    myColliders = GetComponents<BoxCollider>();
                    isMyColliderBox = (myColliders.Length > 0);
                    isMyColliderMesh = false;
                    isMyColliderVoxel = false;
                }
            }
            else
            {
                isMyColliderVoxel = true;
                isMyColliderBox = false;
                isMyColliderMesh = false;
            }
        }

        public void UpdateColliderPosition()
        {
            if (isMyColliderVoxel || isNonrelativisticShader || myColliders == null || myColliders.Length == 0)
            {
                return;
            }

            //If we have a MeshCollider and a compute shader, transform the collider verts relativistically:
            if (isMyColliderMesh && (colliderShader != null) && (myColliders.Length > 0) && SystemInfo.supportsComputeShaders && state.IsInitDone)
            {
                UpdateMeshCollider((MeshCollider)myColliders[0]);
            }
            //If we have a BoxCollider, transform its center to its optical position
            else if (isMyColliderBox)
            {
                Vector4 aiw4 = GetWorld4Acceleration();
                Vector3 pos;
                BoxCollider collider;
                Vector3 testPos;
                float testMag;
                for (int i = 0; i < myColliders.Length; i++)
                {
                    collider = (BoxCollider)myColliders[i];
                    pos = transform.TransformPoint((Vector4)colliderPiw[i]);
                    testPos = transform.InverseTransformPoint(((Vector4)pos).WorldToOptical(viw, aiw4));
                    testMag = testPos.sqrMagnitude;
                    if (!IsNaNOrInf(testMag))
                    {
                        collider.center = testPos;
                    }
                }
            }
        }
        #endregion

        #region Nonrelativistic shader
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
                relVelMag = state.MaxSpeed;
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

        public void UpdateContractorPosition()
        {
            if (!isNonrelativisticShader)
            {
                return;
            }

            if (contractor == null)
            {
                SetUpContractor();
            }

            contractor.position = opticalPiw;
            transform.localPosition = Vector3.zero;
            ContractLength();
        }
        #endregion

        #region RelativisticObject internals

        // Get the start time of our object, so that we know where not to draw it
        public void SetStartTime()
        {
            Vector3 playerPos = state.playerTransform.position;
            float timeDelayToPlayer = Mathf.Sqrt((opticalPiw - playerPos).sqrMagnitude / state.SpeedOfLightSqrd);
            timeDelayToPlayer *= GetTimeFactor();
            StartTime = state.TotalTimeWorld - timeDelayToPlayer;
            hasStarted = false;
            if (myRenderer != null)
                myRenderer.enabled = false;
        }

        //Set the death time, so that we know at what point to destroy the object in the player's view point.
        public virtual void SetDeathTime()
        {
            Vector3 playerPos = state.playerTransform.position;
            float timeDelayToPlayer = Mathf.Sqrt((opticalPiw - playerPos).sqrMagnitude / state.SpeedOfLightSqrd);
            timeDelayToPlayer *= GetTimeFactor();
            DeathTime = state.TotalTimeWorld - timeDelayToPlayer;
        }
        public void ResetDeathTime()
        {
            DeathTime = float.PositiveInfinity;
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
                if (!meshFilters[y].sharedMesh.isReadable) continue;
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
                if (!MFs.isReadable) continue;

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

        private void UpdateShaderParams()
        {
            //Send our object's v/c (Velocity over the Speed of Light) to the shader
            if (myRenderer != null)
            {
                Vector3 tempViw = cviw.AddVelocity(viw) / state.SpeedOfLight;
                Vector4 tempAiw = GetWorld4Acceleration();
                Vector4 tempPao = GetProper4Acceleration();
                Vector4 tempVr = tempViw.AddVelocity(-state.PlayerComovingVelocityVector.AddVelocity(state.PlayerVelocityVector)) / state.SpeedOfLight;

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
                    myRenderer.materials[i].SetVector("_pao", tempPao);
                    myRenderer.materials[i].SetMatrix("_viwLorentzMatrix", viwLorentzMatrix);
                    myRenderer.materials[i].SetMatrix("_invViwLorentzMatrix", viwLorentzMatrix.inverse);
                    myRenderer.materials[i].SetVector("_vr", tempVr);
                    myRenderer.materials[i].SetFloat("_lastUpdateSeconds", Time.time);
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
            if (isLightMapStatic)
            {
                return;
            }

            float maxSpeed = state.MaxSpeed - 0.01f;
            float maxSpeedSqr = maxSpeed * maxSpeed;

            if (viw.sqrMagnitude > maxSpeedSqr)
            {
                viw = viw.normalized * maxSpeed;
            }
            
            if (trnsfrmdMeshVerts == null)
            {
                return;
            }

            // The tangential velocities of each vertex should also not be greater than the maximum speed.
            // (This is a relatively computationally costly check, but it's good practice.

            for (int i = 0; i < trnsfrmdMeshVerts.Length; i++)
            {
                Vector3 disp = Vector3.Scale(trnsfrmdMeshVerts[i], transform.lossyScale);
                Vector3 tangentialVel = Vector3.Cross(aviw, disp);
                float tanVelMagSqr = tangentialVel.sqrMagnitude;
                if (tanVelMagSqr > maxSpeedSqr)
                {
                    aviw = aviw.normalized * maxSpeed / disp.magnitude;
                }
            }
        }

        private void UpdateRigidbodyVelocity()
        {
            if (myRigidbody == null ||
                // Not a meaningful quantity, just to check if either parameter is inf/nan
                IsNaNOrInf((_viw + _aviw).magnitude))
            {
                return;
            }

            // If movement is frozen, set to zero.
            if (state.isMovementFrozen)
            {
                myRigidbody.velocity = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;

                return;
            }

            // If we're in an invalid state, (such as before full initialization,) set to zero.
            if (updatePlayerViwTimeFactor == 0)
            {
                myRigidbody.velocity = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;

                return;
            }

            float gamma = GetTimeFactor();
            myRigidbody.velocity = _viw * gamma;
            myRigidbody.angularVelocity = _aviw * gamma;
        }
        #endregion

        #region Unity lifecycle
        void OnDestroy()
        {
            if (paramsBuffer != null) paramsBuffer.Release();
            if (vertBuffer != null) vertBuffer.Release();
            if (contractor != null) Destroy(contractor.gameObject);
        }

        void Awake()
        {
            _localScale = transform.localScale;
        }

        void Start()
        {
            hasStarted = false;
            cviw = Vector3.zero;
            frameDragAccel = Vector3.zero;
            ResetPiw();
            riw = transform.rotation;

            if (isNonrelativisticShader)
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

            SleepTimer = (isLightMapStatic || myRigidbody == null || isKinematic) ? 0 : SleepTime;

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

            if (myRigidbody != null)
            {
                //Native rigidbody gravity should never be used:
                myRigidbody.useGravity = false;
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
                //And if we have a texture on our material
                for (int i = 0; i < myRenderer.materials.Length; i++)
                {
                    //So that we can set unique values to every moving object, we have to instantiate a material
                    //It's the same as our old one, but now it's not connected to every other object with the same material
                    Material quickSwapMaterial = Instantiate(myRenderer.materials[i]) as Material;
                    //Then, set the value that we want
                    quickSwapMaterial.SetVector("_viw", new Vector4(0, 0, 0, 1));
                    quickSwapMaterial.SetVector("_vr", new Vector4(0, 0, 0, 1));
                    quickSwapMaterial.SetVector("_aiw", new Vector4(0, 0, 0, 0));
                    quickSwapMaterial.SetMatrix("_viwLorentzMatrix", Matrix4x4.identity);


                    //And stick it back into our renderer. We'll do the SetVector thing every frame.
                    myRenderer.materials[i] = quickSwapMaterial;
                }
            }

            // TODO: Doesn't work under extreme conditions, or at all
            //This code is a hack to ensure that frustrum culling does not take place
            //It changes the render bounds so that everything is contained within them
            //At high speeds the Lorentz contraction means that some objects not normally in the view frame are actually visible
            //If we did frustrum culling, these objects would be ignored (because we cull BEFORE running the shader, which does the lorenz contraction)
            // if (meshFilter != null)
            // {
            //     Transform camTransform = Camera.main.transform;
            //     float distToCenter = (Camera.main.farClipPlane + Camera.main.nearClipPlane) / 2.0f;
            //     Vector3 center = camTransform.position;
            //     meshFilter.sharedMesh.bounds = new Bounds(distToCenter * camTransform.forward + center, 2 * distToCenter * Vector3.one);
            // }

            if (isNonrelativisticShader)
            {
                UpdateContractorPosition();
            }
        }

        protected bool isPhysicsCacheValid;

        protected void UpdatePhysicsCaches()
        {
            updateMetric = GetMetric();
            updatePlayerViwTimeFactor = state.PlayerVelocityVector.InverseGamma(updateMetric);
            if (IsNaNOrInf(updatePlayerViwTimeFactor))
            {
                updatePlayerViwTimeFactor = 1;
            }
            updateViwTimeFactor = viw.InverseGamma(updateMetric);
            if (IsNaNOrInf(updateViwTimeFactor))
            {
                updateViwTimeFactor = 1;
            }
            updateWorld4Acceleration = aiw.ProperToWorldAccel(viw, updateViwTimeFactor);
            updateTisw = ((Vector4)piw).GetTisw(viw, updateWorld4Acceleration);

            isPhysicsCacheValid = true;
        }

        void Update()
        {
            if (isLightMapStatic)
            {
                if (isNonrelativisticShader)
                {
                    UpdateContractorPosition();
                    return;
                }

                if (myRenderer == null)
                {
                    return;
                }

                bool doUpdate = false;

                float comparisonTime;
                for (int i = 0; i < myRenderer.materials.Length; i++)
                {
                    comparisonTime = myRenderer.materials[i].GetFloat("_lastUpdateSeconds");

                    // 1/60th of a second
                    if ((Time.time - comparisonTime) > (1.0f / 60.0f))
                    {
                        doUpdate = true;
                        break;
                    }
                }

                if (doUpdate)
                {
                    UpdateShaderParams();
                }

                return;
            }

            UpdatePhysicsCaches();

            localDeltaTime = state.DeltaTimePlayer * GetTimeFactor() - state.DeltaTimeWorld;

            if (myRigidbody != null)
            {
                if ((_viw.sqrMagnitude < SleepThreshold) &&
                (_aviw.sqrMagnitude < SleepThreshold) &&
                (aiw.sqrMagnitude < SleepThreshold))
                {
                    SleepTimer -= Time.deltaTime;
                }
                else
                {
                    SleepTimer = SleepTime;
                }

                if (SleepTimer <= 0)
                {
                    viw = Vector3.zero;
                    aviw = Vector3.zero;
                    aiw = Vector3.zero;

                    myRigidbody.Sleep();
                }

                UpdateRigidbodyVelocity();
            }

            if (state.isMovementFrozen)
            {
                UpdateShaderParams();
                isPhysicsCacheValid = false;
                return;
            }

            if (isNonrelativisticShader)
            {
                UpdateShaderParams();
                UpdateContractorPosition();
                isPhysicsCacheValid = false;
                return;
            }

            if (meshFilter == null)
            {
                UpdateShaderParams();
                isPhysicsCacheValid = false;
                return;
            }

            ObjectMeshDensity density = GetComponent<ObjectMeshDensity>();

            if (density == null)
            {
                UpdateShaderParams();
                isPhysicsCacheValid = false;
                return;
            }

            UpdateShaderParams();
            isPhysicsCacheValid = false;
        }

        void FixedUpdate()
        {
            if (state.isMovementFrozen)
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

            if (isLightMapStatic)
            {
                UpdateColliderPosition();
                return;
            }

            UpdatePhysicsCaches();

            float deltaTime = state.FixedDeltaTimePlayer * GetTimeFactor();
            localFixedDeltaTime = deltaTime - state.FixedDeltaTimeWorld;

            if (state.conformalMap != null)
            {
                Vector4 nPiw4 = state.conformalMap.ComoveOptical(deltaTime, piw);
                Vector3 pDiff = (Vector3)nPiw4 - piw;
                cviw = pDiff / deltaTime;
                piw = nPiw4;
            }

            if (!IsNaNOrInf(localFixedDeltaTime))
            {
                localTimeOffset += localFixedDeltaTime;
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
                    else if (!hasStarted && (state.TotalTimeWorld + localTimeOffset + tisw > StartTime))
                    {
                        hasStarted = true;
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

            if (isMonopoleAccel)
            {
                Vector3 myAccel = properAccel;
                Vector3 pAccel = state.PlayerAccelerationVector;
                // To support Unity's concept of Newtonian gravity, we "cheat" a little on equivalence principle, here.
                // This isn't 100% right, but it keeps the world from looking like the space-time curvature is incomprehensibly 
                // warped in a "moderate" (really, extremely high) approximately Newtonian surface gravity.

                // If the RelativisticObject is at rest on the ground, according to Strano 2019, (not yet peer reviewed,)
                // it loses surface acceleration, (not weight force, directly,) the longer it stays in this configuration.
                Vector3 da = -myAccel.normalized * myAccel.sqrMagnitude / state.SpeedOfLight * deltaTime;
                frameDragAccel += da;
                myAccel += da;

                float myTemperature = 0;

                // Per Strano 2019, due to the interaction with the thermal graviton gas radiated by the Rindler horizon,
                // there is also a change in mass. However, the monopole waves responsible for this are seen from a first-person perspective,
                // (i.e. as due to "player" acceleration).
                if ((myRigidbody != null) && (SleepTimer == 0))
                {
                    if (SleepTimer == 0)
                    {
                        // If a gravitating body this RO is attracted to is already excited above the rest mass vacuum,
                        // (which seems to imply the Higgs field vacuum)
                        // then it will spontaneously emit this excitation, with a coupling constant proportional to the
                        // gravitational constant "G" times (baryon) constituent particle rest mass.
                        Vector3 gravAccel = useGravity ? -Physics.gravity : Vector3.zero;
                        gravAccel += state.conformalMap == null ? Vector3.zero : state.conformalMap.GetRindlerAcceleration(piw);
                        float bdm = (myRigidbody.mass / state.planckMass) * Mathf.Abs((gravAccel + frameDragAccel).magnitude / state.planckAccel) * (deltaTime / state.planckTime) / baryonCount;
                        myTemperature = Mathf.Pow(bdm / (SRelativityUtil.sigmaPlanck / 2), 0.25f);
                    }
                }
                //... But just turn "doDegradeAccel" off, if you don't want this effect for any reason.
                // (We ignore the "little bit" of acceleration from collisions, but maybe we could add that next.)

                float surfaceArea = meshFilter.sharedMesh.SurfaceArea() / (state.planckLength * state.planckLength);
                float dm = SRelativityUtil.sigmaPlanck * surfaceArea * gravitonEmissivity * (Mathf.Pow(myTemperature, 4) - Mathf.Pow(state.gravityBackgroundTemperature, 4));

                frameDragMass += dm;
                myRigidbody.mass -= dm;

                properAccel = myAccel;
            }

            CheckSleepPosition();

            // The rest of the updates are for objects with Rigidbodies that move and aren't asleep.
            if (isKinematic || SleepTimer <= 0 || myRigidbody == null)
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
                }
                else
                {
                    transform.position = isNonrelativisticShader ? opticalPiw : piw;
                }

                UpdateShaderParams();

                SleepTimer = 0;

                isPhysicsCacheValid = false;

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

                if (isNonrelativisticShader)
                {
                    transform.parent = null;
                    testVec = opticalPiw;
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
            float gamma = GetTimeFactor();
            myRigidbody.velocity = gamma * viw;
            myRigidbody.angularVelocity = gamma * aviw;

            isPhysicsCacheValid = false;
        }

        protected void CheckSleepPosition()
        {
            if (SleepTimer > 0 || !useGravity)
            {
                return;
            }

            Collider myCollider = GetComponent<Collider>();
            Vector3 gravUnit = Physics.gravity.normalized;
            Vector3 dir = Vector3.Project(myCollider.bounds.extents, gravUnit);
            float dist = dir.magnitude;
            Ray ray = new Ray(myCollider.bounds.center - 0.99f * dist * gravUnit, gravUnit);
            RaycastHit hitInfo;
            if (Physics.Raycast(ray, out hitInfo, 1.99f * dist))
            {
                Collider oCollider = hitInfo.collider;
                opticalPiw -= (1.99f * dist - hitInfo.distance) * gravUnit;
                UpdateContractorPosition();
                UpdateColliderPosition();
            }
            else if (!Physics.Raycast(ray, out hitInfo, 2.05f * dist))
            {
                SleepTimer = SleepTime;
            }
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
        #endregion

        #region Rigidbody mechanics
        public void OnCollision(Collision collision)
        {
            if (myRigidbody == null || myColliders == null || isKinematic)
            {
                return;
            }

            if (SleepTimer <= 0)
            {
                RelativisticObject otherRO = collision.gameObject.GetComponent<RelativisticObject>();
                if (otherRO == null || otherRO.SleepTimer <= 0)
                {
                    CheckSleepPosition();
                    return;
                }
            }         

            // Let's start simple:
            // At low enough velocities, where the Newtonian approximation is reasonable,
            // PhysX is probably MORE accurate for even relativistic collision than the hacky relativistic collision we had
            // (which is still in the commit history, for reference).
            EnforceCollision(collision);
            // EnforceCollision() might opt not to set didCollide
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
                    SleepTimer = 0;

                    return;
                }
            }

            SleepTimer = SleepTime;

            // Get the position and rotation after the collision:
            riw = myRigidbody.rotation;
            piw = isNonrelativisticShader ? ((Vector4)myRigidbody.position).OpticalToWorldHighPrecision(viw, updateWorld4Acceleration) : myRigidbody.position;

            // Now, update the velocity and angular velocity based on the collision result:
            viw = myRigidbody.velocity.RapidityToVelocity(updateMetric);
            aviw = myRigidbody.angularVelocity / updatePlayerViwTimeFactor;

            // Make sure we're not updating to faster than max speed
            checkSpeed();

            UpdateContractorPosition();
            UpdateColliderPosition();
        }
        #endregion

        private bool IsNaNOrInf(float p)
        {
            return float.IsInfinity(p) || float.IsNaN(p);
        }
    }
}