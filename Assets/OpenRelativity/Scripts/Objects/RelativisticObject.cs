using OpenRelativity.ConformalMaps;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenRelativity.Objects
{
    [ExecuteInEditMode]
    public class RelativisticObject : RelativisticBehavior
    {
        #region Public Settings
        public bool isLightMapStatic = false;
        public bool useGravity = false;
        // Use this instead of relativistic parent
        public bool isParent = false;
        // Combine colliders under us in the hierarchy
        public bool isCombinedColliderParent = false;
        // Use this if not using an explicitly relativistic shader
        public bool isNonrelativisticShader = false;
        // We set the Rigidbody "drag" parameter in this object.
        public float unityDrag = 0;
        // We also set the Rigidbody "angularDrag" parameter in this object.
        public float unityAngularDrag = 0;
        // Comove via ConformalMap acceleration, or ComoveOptical?
        public bool comoveViaAcceleration = false;
        // The composite scalar monopole graviton gas is described by statistical mechanics and heat flow equations
        public float gravitonEmissivity = 0.1f;
        // By default, 12g per baryon mole would be carbon-12, and this controls the total baryons estimated in the object
        public float fundamentalAverageMolarMass = 0.012f;
        public float initialAverageMolarMass = 0.012f;
        public float currentAverageMolarMass
        {
            get
            {
                if (myRigidbody)
                {
                    return myRigidbody.mass * SRelativityUtil.avogadroNumber / baryonCount;
                }

                return 0;
            }
            protected set
            {
                if (myRigidbody)
                {
                    myRigidbody.mass = value * baryonCount / SRelativityUtil.avogadroNumber;
                }
            }
        }

        // Set with Rigidbody isKinematic flag instead
        public bool isKinematic
        {
            get
            {
                if (myRigidbody)
                {
                    return myRigidbody.isKinematic;
                }

                return false;
            }

            set
            {
                if (myRigidbody)
                {
                    myRigidbody.isKinematic = value;
                }
            }
        }
        #endregion

        protected bool isPhysicsUpdateFrame;

        protected float updateViwTimeFactor;
        protected float updatePlayerViwTimeFactor;
        protected float updateTisw;
        protected Matrix4x4 updateMetric;
        protected Vector4 updateWorld4Acceleration;

        #region Local Time
        //Acceleration desyncronizes our clock from the world clock:
        public float localTimeOffset { get; private set; }
        private float oldLocalTimeUpdate;
        public float localDeltaTime
        {
            get
            {
                return GetLocalTime() - oldLocalTimeUpdate;
            }
        }
        public float localFixedDeltaTime
        {
            get
            {
                return GetLocalTime() - oldLocalTimeFixedUpdate;
            }
        }
        private float oldLocalTimeFixedUpdate;
        public float GetLocalTime()
        {
            return state.TotalTimeWorld + localTimeOffset;
        }
        public void ResetLocalTime()
        {
            localTimeOffset = 0;
        }
        public float GetTisw()
        {
            if (isPhysicsCacheValid)
            {
                return updateTisw;
            }

            return piw.GetTisw(viw, GetComoving4Acceleration());
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

            Matrix4x4 metric = SRelativityUtil.GetRindlerMetric(piw);
            Matrix4x4 invVpcLorentz = state.PlayerLorentzMatrix.inverse;
            metric = invVpcLorentz.transpose * metric * invVpcLorentz;

            Matrix4x4 intrinsicMetric = state.conformalMap.GetMetric(piw);
            return intrinsicMetric * metric * intrinsicMetric.inverse;
        }

        public Vector4 Get4Velocity()
        {
            return viw.ToMinkowski4Viw();
        }

        public Vector4 GetComoving4Acceleration()
        {
            if (isPhysicsCacheValid)
            {
                return updateWorld4Acceleration;
            }

            return comovingAccel.ProperToWorldAccel(viw, GetTimeFactor());
        }

        public Vector4 GetWorld4Acceleration()
        {
            return aiw.ProperToWorldAccel(viw, GetTimeFactor());
        }

        // This is the factor commonly referred to as "gamma," for length contraction and time dilation,
        // only also with consideration for a gravitationally curved background, such as due to Rindler coordinates.
        // (Rindler coordinates are actually Minkowski flat, but the same principle applies.)
        public float GetTimeFactor()
        {
            if (isPhysicsCacheValid)
            {
                return updateViwTimeFactor;
            }

            // However, sometimes we want a different velocity, at this space-time point,
            // such as this RO's own velocity.
            return viw.InverseGamma(GetMetric());
        }
        #endregion

        #region Rigid body physics
        private bool wasKinematic;
        private CollisionDetectionMode collisionDetectionMode;
        private PhysicMaterial[] origPhysicMaterials;

        private Vector3 oldVelocity;
        private float lastFixedUpdateDeltaTime;

        public float baryonCount { get; set; }

        public float mass
        {
            get
            {
                if (myRigidbody)
                {
                    return myRigidbody.mass;
                }

                return 0;
            }

            set
            {
                if (myRigidbody)
                {
                    myRigidbody.mass = value;
                }
            }
        }

        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            if (!myRigidbody) {
                if (mode == ForceMode.Impulse)
                {
                    return;
                }
                if (mode == ForceMode.Force)
                {
                    return;
                }
            }

            switch (mode)
            {
                case ForceMode.Impulse:
                    peculiarVelocity += force / mass;
                    break;
                case ForceMode.VelocityChange:
                    peculiarVelocity += force;
                    break;
                case ForceMode.Force:
                    nonGravAccel += force / mass;
                    break;
                case ForceMode.Acceleration:
                    nonGravAccel += force;
                    break;
            }
        }

        //Store world position, mostly for a nonrelativistic shader:
        protected Vector3 _piw;
        public Vector3 piw {
            get
            {
                return _piw;
            }
            set
            {
                if ((value - _piw).sqrMagnitude <= SRelativityUtil.FLT_EPSILON) {
                    return;
                }

                _piw = value;
                UpdatePhysicsCaches();
            }
        }

        public Vector3 opticalPiw {
            get
            {
                return piw.WorldToOptical(peculiarVelocity, GetComoving4Acceleration());
            }
            set
            {
                piw = value.OpticalToWorld(peculiarVelocity, GetComoving4Acceleration());
            }
        }

        public void ResetPiw()
        {
            piw = isNonrelativisticShader ? transform.position.OpticalToWorld(peculiarVelocity, GetComoving4Acceleration()) : transform.position;
        }
        //Store rotation quaternion
        public Quaternion riw { get; set; }

        public Vector3 _peculiarVelocity = Vector3.zero;
        public Vector3 peculiarVelocity
        {
            get
            {
                return _peculiarVelocity;
            }

            set
            {
                // Skip this all, if the change is negligible.
                if ((value - _peculiarVelocity).sqrMagnitude <= SRelativityUtil.FLT_EPSILON)
                {
                    return;
                }

                UpdateMotion(value, nonGravAccel);
                UpdateRigidbodyVelocity();
            }
        }

        public Vector3 vff
        {
            get
            {
                return state.conformalMap ? state.conformalMap.GetFreeFallVelocity(piw) : Vector3.zero;
            }
        }

        public Vector3 viw
        {
            get
            {
                return vff.AddVelocity(peculiarVelocity);
            }

            set
            {
                peculiarVelocity = (-vff).AddVelocity(value);
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

        // This is the part of acceleration that can be set.
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
                if (isKinematic || (value - _nonGravAccel).sqrMagnitude <= SRelativityUtil.FLT_EPSILON)
                {
                    return;
                }

                UpdateMotion(peculiarVelocity, value);
                UpdateRigidbodyVelocity();
            }
        }

        // This would be the object's properAccleration if at rest in "world coordinates."
        public Vector3 aiw
        {
            get
            {
                Vector3 accel = nonGravAccel + leviCivitaDevAccel;

                if (useGravity)
                {
                    accel += Physics.gravity;
                }

                accel += state.conformalMap.GetRindlerAcceleration(piw);

                return accel;
            }

            set
            {
                Vector3 accel = value;

                if (state.conformalMap)
                {
                    accel -= state.conformalMap.GetRindlerAcceleration(piw);
                }

                if (useGravity)
                {
                    accel -= Physics.gravity;
                }

                accel -= leviCivitaDevAccel;

                nonGravAccel = accel;
            }
        }

        // This would be the object's "proper acceleration" in comoving coordinates,
        // but we compose with Rindler metric for Physics.gravity, so that we can
        // keep book as the Physics.gravity vector effect being an intrinsic property.
        public Vector3 comovingAccel
        {
            get
            {
                if (myRigidbody && myRigidbody.IsSleeping())
                {
                    return Vector3.zero;
                }

                return nonGravAccel;
            }
            set
            {
                nonGravAccel = value;
            }
        }

        public float monopoleTemperature
        {
            get
            {
                if (!myRigidbody)
                {
                    return 0;
                }

                // Per Strano 2019, due to the interaction with the thermal graviton gas radiated by the Rindler horizon,
                // there is also a change in mass. However, the monopole waves responsible for this are seen from a first-person perspective,
                // (i.e. as due to "player" acceleration).

                // If a gravitating body this RO is attracted to is already excited above the rest mass vacuum,
                // (which seems to imply the Higgs field vacuum)
                // then it will spontaneously emit this excitation, with a coupling constant proportional to the
                // gravitational constant "G" times (baryon) constituent particle rest mass.

                double nuclearMass = mass / baryonCount;
                double fundamentalNuclearMass = fundamentalAverageMolarMass / SRelativityUtil.avogadroNumber;

                if (nuclearMass < fundamentalNuclearMass)
                {
                    // We can't support negative temperatures, yet, for realistic constants,
                    // (not that we'd necessarily want them).
                    return 0;
                }

                double excitationEnergy = (nuclearMass - fundamentalNuclearMass) * state.SpeedOfLightSqrd;
                double temperature = 2 * excitationEnergy / state.boltzmannConstant;

                return (float)temperature;

                //... But just turn "doDegradeAccel" off, if you don't want this effect for any reason.
            }

            set
            {
                if (!myRigidbody)
                {
                    return;
                }

                double fundamentalNuclearMass = fundamentalAverageMolarMass / SRelativityUtil.avogadroNumber;
                double excitationEnergy = value * state.boltzmannConstant / 2;
                if (excitationEnergy < 0)
                {
                    excitationEnergy = 0;
                }
                double nuclearMass = excitationEnergy / state.SpeedOfLightSqrd + fundamentalNuclearMass;

                mass = (float)(nuclearMass * baryonCount);
            }
        }

        public Vector3 leviCivitaDevAccel = Vector3.zero;

        public void UpdateMotion(Vector3 pvf, Vector3 af)
        {
            // Changing velocities lose continuity of position,
            // unless we transform the world position to optical position with the old velocity,
            // and inverse transform the optical position with the new the velocity.
            // (This keeps the optical position fixed.)

            Vector3 pvi = peculiarVelocity;
            Vector3 ai = comovingAccel;
            _nonGravAccel = af;
            _peculiarVelocity = pvf;

            float timeFac = GetTimeFactor();

            _piw = piw.WorldToOptical(pvi, ai.ProperToWorldAccel(pvi, timeFac)).OpticalToWorld(pvf, comovingAccel.ProperToWorldAccel(pvf, timeFac));

            if (isNonrelativisticShader)
            {
                UpdateContractorPosition();
            }
            else
            {
                transform.position = piw;
            }

            UpdatePhysicsCaches();
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
        private bool isMyColliderVoxel;
        private bool isMyColliderGeneral {
            get {
                return !isMyColliderMesh && !isMyColliderVoxel;
            }
        }

        //If we have a collider to transform, we cache it here
        private Collider[] myColliders;
        private SphereCollider[] mySphereColliders;
        private BoxCollider[] myBoxColliders;
        private CapsuleCollider[] myCapsuleColliders;

        private Vector3[] colliderPiw { get; set; }
        public void MarkStaticColliderPos()
        {
            if (isMyColliderGeneral)
            {
                List<Vector3> sttcPosList = new List<Vector3>();

                for (int i = 0; i < mySphereColliders.Length; ++i)
                {
                    sttcPosList.Add(mySphereColliders[i].center);
                }

                for (int i = 0; i < myBoxColliders.Length; ++i)
                {
                    sttcPosList.Add(myBoxColliders[i].center);
                }

                for (int i = 0; i < myCapsuleColliders.Length; ++i)
                {
                    sttcPosList.Add(myCapsuleColliders[i].center);
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
        public float monopoleAccelerationSoften = 0;
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
            if (colliderShaderMesh == null || colliderShaderMesh.vertexCount == 0)
            {
                return;
            }

            if (paramsBuffer == null)
            {
                paramsBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(colliderShaderParams));
            }

            if (vertBuffer == null)
            {
                vertBuffer = new ComputeBuffer(colliderShaderMesh.vertexCount, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
            }

            //Freeze the physics if the global state is frozen.
            if (state.isMovementFrozen)
            {
                if (!wasFrozen)
                {
                    //Read the state of the rigidbody and shut it off, once.
                    wasFrozen = true;
                    if (!myRigidbody) {
                        wasKinematic = true;
                        collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    }
                    else      
                    {
                        wasKinematic = myRigidbody.isKinematic;
                        collisionDetectionMode = myRigidbody.collisionDetectionMode;
                        myRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        myRigidbody.isKinematic = true;
                    }
                }

                return;
            }
            
            if (wasFrozen)
            {
                //Restore the state of the rigidbody, once.
                wasFrozen = false;
                if (myRigidbody)
                {
                    myRigidbody.isKinematic = wasKinematic;
                    myRigidbody.collisionDetectionMode = collisionDetectionMode;
                }
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
            Matrix4x4 metric = state.conformalMap.GetMetric(piw);
            colliderShaderParams.intrinsicMetric = metric;
            colliderShaderParams.invIntrinsicMetric = metric.inverse;

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
            trnsfrmdMesh.RecalculateTangents();
            transformCollider.sharedMesh = trnsfrmdMesh;

            if (myRigidbody)
            {
                myRigidbody.ResetCenterOfMass();
                myRigidbody.ResetInertiaTensor();
            }
        }

        public void UpdateColliders()
        {
            MeshCollider[] myMeshColliders = GetComponents<MeshCollider>();

            //Get the vertices of our mesh
            if (!colliderShaderMesh && meshFilter && meshFilter.sharedMesh.isReadable)
            {
                colliderShaderMesh = Instantiate(meshFilter.sharedMesh);
            }

            if (colliderShaderMesh)
            {
                trnsfrmdMesh = Instantiate(colliderShaderMesh);
                trnsfrmdMeshVerts = (Vector3[])trnsfrmdMesh.vertices.Clone();
                trnsfrmdMesh.MarkDynamic();

                if (!enabled || !gameObject.activeInHierarchy)
                {
                    UpdateMeshCollider(myMeshColliders[0]);
                }
            }

            if (GetComponent<ObjectBoxColliderDensity>())
            {
                isMyColliderVoxel = true;
                isMyColliderMesh = false;
            }
            else
            {
                myColliders = myMeshColliders;
                if (myColliders.Length > 0)
                {
                    isMyColliderMesh = true;
                    isMyColliderVoxel = false;
                }
                else
                {
                    isMyColliderMesh = false;
                    isMyColliderVoxel = false;
                }
            }

            mySphereColliders = GetComponents<SphereCollider>();
            myBoxColliders = GetComponents<BoxCollider>();
            myCapsuleColliders = GetComponents<CapsuleCollider>();

            myColliders = GetComponents<Collider>();
            List<PhysicMaterial> origMaterials = new List<PhysicMaterial>();
            for (int i = 0; i < myColliders.Length; ++i)
            {
                // Friction needs a relativistic correction, so we need variable PhysicMaterial parameters.
                Collider collider = myColliders[i];
                origMaterials.Add(collider.material);
                collider.material = Instantiate(collider.material);
            }
            origPhysicMaterials = origMaterials.ToArray();
        }

        public void UpdateColliderPosition()
        {
            if (isMyColliderVoxel || isNonrelativisticShader || (myColliders.Length == 0))
            {
                return;
            }

            //If we have a MeshCollider and a compute shader, transform the collider verts relativistically:
            if (isMyColliderMesh && colliderShader && SystemInfo.supportsComputeShaders)
            {
                UpdateMeshCollider((MeshCollider)myColliders[0]);
            }
            //If we have a Collider, transform its center to its optical position
            else if (isMyColliderGeneral)
            {
                Vector4 aiw4 = GetComoving4Acceleration();

                int iTot = 0;
                for (int i = 0; i < mySphereColliders.Length; ++i)
                {
                    SphereCollider collider = mySphereColliders[i];
                    Vector3 pos = transform.TransformPoint((Vector4)colliderPiw[iTot]);
                    Vector3 pw = pos.WorldToOptical(peculiarVelocity, aiw4);
                    Vector3 testPos = transform.InverseTransformPoint(pw);
                    collider.center = testPos;
                    ++iTot;
                }

                for (int i = 0; i < myBoxColliders.Length; ++i)
                {
                    BoxCollider collider = myBoxColliders[i];
                    Vector3 pos = transform.TransformPoint((Vector4)colliderPiw[iTot]);
                    Vector3 pw = pos.WorldToOptical(peculiarVelocity, aiw4);
                    Vector3 testPos = transform.InverseTransformPoint(pw);
                    collider.center = testPos;
                    ++iTot;
                }

                for (int i = 0; i < myCapsuleColliders.Length; ++i)
                {
                    CapsuleCollider collider = myCapsuleColliders[i];
                    Vector3 pos = transform.TransformPoint((Vector4)colliderPiw[iTot]);
                    Vector3 pw = pos.WorldToOptical(peculiarVelocity, aiw4);
                    Vector3 testPos = transform.InverseTransformPoint(pw);
                    collider.center = testPos;
                    ++iTot;
                }
            }
        }
        #endregion

        #region Nonrelativistic shader
        private void SetUpContractor()
        {
            _localScale = transform.localScale;
            if (contractor)
            {
                contractor.parent = null;
                contractor.localScale = new Vector3(1, 1, 1);
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
            Vector3 relVel = (-playerVel).AddVelocity(peculiarVelocity);
            float relVelMag = relVel.sqrMagnitude;

            if (relVelMag > (state.MaxSpeed))
            {
                relVel.Normalize();
                relVelMag = state.MaxSpeed;
                relVel = relVelMag * relVel;
            }

            //Undo length contraction from previous state, and apply updated contraction:
            // - First, return to world frame:
            contractor.localScale = new Vector3(1, 1, 1);
            transform.localScale = _localScale;

            if (relVelMag > SRelativityUtil.FLT_EPSILON)
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
                contractor.localScale = new Vector3(1, 1, 1).ContractLengthBy(relVelMag * Vector3.forward);
            }
        }

        public void UpdateContractorPosition()
        {
            if (!Application.isPlaying || !isNonrelativisticShader)
            {
                return;
            }

            if (!contractor)
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
            if (myRenderer)
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
            for (int y = 0; y < meshFilterLength; ++y)
            {
                //If it's null, ignore it.
                if (!meshFilters[y]) continue;
                if (!meshFilters[y].sharedMesh) continue;
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

            for (int u = 0; u < subMeshCounts; ++u)
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
            for (int i = 0; i < meshFilterLength; ++i)
            {
                //just doublecheck that the mesh isn't null
                MFs = meshFilters[i].sharedMesh;
                if (!MFs) continue;
                if (!MFs.isReadable) continue;

                //Otherwise, for all submeshes in the current mesh
                for (int q = 0; q < subMeshCount[i]; ++q)
                {
                    //turn off the original renderer
                    meshRenderers[i].enabled = false;
                    RelativisticObject ro = meshRenderers[i].GetComponent<RelativisticObject>();
                    if (ro)
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
                    for (int k = 0; k < tempSubTriangles.Length; ++k)
                    {
                        tempTriangles[subMeshIndex][k] = tempSubTriangles[k] + vertIndex;
                    }
                    //Increment the submesh index
                    ++subMeshIndex;
                }
                Matrix4x4 cTrans = worldLocalMatrix * meshFilters[i].transform.localToWorldMatrix;
                //For all the vertices in the mesh
                for (int v = 0; v < MFs.vertices.Length; ++v)
                {
                    //Get the vertex and the UV coordinate
                    tempVerts[vertIndex] = cTrans.MultiplyPoint3x4(MFs.vertices[v]);
                    tempUVs[vertIndex] = MFs.uv[v];
                    ++vertIndex;
                }
            }

            //Put it all together now.
            Mesh myMesh = new Mesh();
            //If any materials are the same, we can combine triangles and give them the same material.
            myMesh.subMeshCount = uniqueMaterials.Count;
            myMesh.vertices = tempVerts;
            Material[] finalMaterials = new Material[uniqueMaterials.Count];
            for (int i = 0; i < uniqueMaterialNames.Count; ++i)
            {
                string uniqueName = uniqueMaterialNames[i];
                List<int> combineTriangles = new List<int>();
                for (int j = 0; j < tempMaterials.Length; ++j)
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
            if (!GetComponent<MeshFilter>())
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
            if (mCollider)
            {
                mCollider.sharedMesh = myMesh;
            }

            transform.gameObject.SetActive(true);
            gameObject.isStatic = wasStatic;

            if (isCombinedColliderParent)
            {
                MeshCollider myMeshCollider = GetComponent<MeshCollider>();
                if (myMeshCollider)
                {
                    myMeshCollider.sharedMesh = myMesh;
                }  
            }
            else
            {
                MeshCollider[] childrenColliders = GetComponentsInChildren<MeshCollider>();
                List<Collider> dupes = new List<Collider>();
                for (int i = 0; i < childrenColliders.Length; ++i)
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
            for (int i = 0; i < transform.childCount; ++i)
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
            if (!Application.isPlaying || !myRenderer)
            {
                return;
            }

            //Send our object's v/c (Velocity over the Speed of Light) to the shader

            Vector3 tempViw = peculiarVelocity / state.SpeedOfLight;
            Vector4 tempPao = GetComoving4Acceleration();
            Vector4 tempVr = (-state.PlayerVelocityVector).AddVelocity(peculiarVelocity) / state.SpeedOfLight;

            //Velocity of object Lorentz transforms are the same for all points in an object,
            // so it saves redundant GPU time to calculate them beforehand.
            Matrix4x4 viwLorentzMatrix = SRelativityUtil.GetLorentzTransformMatrix(tempViw);

            // Metric default (doesn't have correct signature):
            Matrix4x4 intrinsicMetric = state.conformalMap.GetMetric(piw);

            colliderShaderParams.viw = tempViw;
            colliderShaderParams.pao = tempPao;
            colliderShaderParams.viwLorentzMatrix = viwLorentzMatrix;
            colliderShaderParams.invViwLorentzMatrix = viwLorentzMatrix.inverse;
            for (int i = 0; i < myRenderer.materials.Length; ++i)
            {
                myRenderer.materials[i].SetVector("_viw", tempViw);
                myRenderer.materials[i].SetVector("_pao", tempPao);
                myRenderer.materials[i].SetMatrix("_viwLorentzMatrix", viwLorentzMatrix);
                myRenderer.materials[i].SetMatrix("_invViwLorentzMatrix", viwLorentzMatrix.inverse);
                myRenderer.materials[i].SetMatrix("_intrinsicMetric", intrinsicMetric);
                myRenderer.materials[i].SetMatrix("_invIntrinsicMetric", intrinsicMetric.inverse);
                myRenderer.materials[i].SetVector("_vr", tempVr);
                myRenderer.materials[i].SetFloat("_lastUpdateSeconds", Time.time);
            }
        }

        private void UpdateBakingShaderParams()
        {
            if (!myRenderer)
            {
                return;
            }

            //Send our object's v/c (Velocity over the Speed of Light) to the shader

            Vector3 tempViw = peculiarVelocity / state.SpeedOfLight;
            Vector4 tempPao = GetComoving4Acceleration();
            Vector4 tempVr = (-state.PlayerVelocityVector).AddVelocity(peculiarVelocity) / state.SpeedOfLight;

            //Velocity of object Lorentz transforms are the same for all points in an object,
            // so it saves redundant GPU time to calculate them beforehand.
            Matrix4x4 viwLorentzMatrix = SRelativityUtil.GetLorentzTransformMatrix(tempViw);

            // Metric default (doesn't have correct signature):
            Matrix4x4 intrinsicMetric = state.conformalMap.GetMetric(piw);

            colliderShaderParams.viw = tempViw;
            colliderShaderParams.pao = tempPao;
            colliderShaderParams.viwLorentzMatrix = viwLorentzMatrix;
            colliderShaderParams.invViwLorentzMatrix = viwLorentzMatrix.inverse;
            for (int i = 0; i < myRenderer.sharedMaterials.Length; ++i)
            {
                if (myRenderer.sharedMaterials[i] == null) {
                    continue;
                }
                myRenderer.sharedMaterials[i] = Instantiate(myRenderer.sharedMaterials[i]);
                myRenderer.sharedMaterials[i].SetVector("_viw", tempViw);
                myRenderer.sharedMaterials[i].SetVector("_pao", tempPao);
                myRenderer.sharedMaterials[i].SetMatrix("_viwLorentzMatrix", viwLorentzMatrix);
                myRenderer.sharedMaterials[i].SetMatrix("_invViwLorentzMatrix", viwLorentzMatrix.inverse);
                myRenderer.sharedMaterials[i].SetMatrix("_intrinsicMetric", intrinsicMetric);
                myRenderer.sharedMaterials[i].SetMatrix("_invIntrinsicMetric", intrinsicMetric.inverse);
                myRenderer.sharedMaterials[i].SetVector("_vr", tempVr);
                myRenderer.sharedMaterials[i].SetFloat("_lastUpdateSeconds", Time.time);
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

            if (peculiarVelocity.sqrMagnitude > maxSpeedSqr)
            {
                peculiarVelocity = peculiarVelocity.normalized * maxSpeed;
            }
            
            if (trnsfrmdMeshVerts == null)
            {
                return;
            }

            // The tangential velocities of each vertex should also not be greater than the maximum speed.
            // (This is a relatively computationally costly check, but it's good practice.

            for (int i = 0; i < trnsfrmdMeshVerts.Length; ++i)
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
            float gamma = GetTimeFactor();

            if (myRigidbody)
            {
                // If movement is frozen, set to zero.
                // If we're in an invalid state, (such as before full initialization,) set to zero.
                if (state.isMovementFrozen)
                {
                    myRigidbody.velocity = Vector3.zero;
                    myRigidbody.angularVelocity = Vector3.zero;
                }
                else
                {
                    // Factor of gamma corrects for length-contraction, (which goes like 1/gamma).
                    // Effectively, this replaces Time.DeltaTime with Time.DeltaTime / gamma.
                    myRigidbody.velocity = gamma * peculiarVelocity;
                    myRigidbody.angularVelocity = gamma * aviw;

                    Vector3 properAccel = nonGravAccel + leviCivitaDevAccel;
                    if (comoveViaAcceleration)
                    {
                        // This is not actually "proper acceleration," with this option active.
                        properAccel += state.conformalMap.GetRindlerAcceleration(piw);
                    }
                    if (properAccel.sqrMagnitude > SRelativityUtil.FLT_EPSILON)
                    {
                        myRigidbody.AddForce(gamma * properAccel, ForceMode.Acceleration);
                    }
                }

                nonGravAccel = Vector3.zero;

                // Factor of 1/gamma corrects for time-dilation, (which goes like gamma).
                // Unity's (physically inaccurate) drag formula is something like,
                // velocity = velocity * (1 - drag * Time.deltaTime),
                // where we counterbalance the time-dilation factor above, for observer path invariance.
                myRigidbody.drag = unityDrag / gamma;
                myRigidbody.angularDrag = unityAngularDrag / gamma;
            }

            for (int i = 0; i < myColliders.Length; ++i)
            {
                Collider collider = myColliders[i];

                // Energy dissipation goes like mu * F_N * d.
                // If the path parallel to d is also parallel to relative velocity,
                // d "already looks like" d' / gamma, to player, so multiply gamma * mu.
                // If the path parallel to d is perpendicular to relative velocity,
                // F_N "already looks like" it's being applied in time t' * gamma, to player, so multiply gamma * mu.
                collider.material.staticFriction = gamma * origPhysicMaterials[i].staticFriction;
                collider.material.dynamicFriction = gamma * origPhysicMaterials[i].dynamicFriction;
                // rapidity_after / rapidity_before - Doesn't seem to need an adjustment.
                collider.material.bounciness = origPhysicMaterials[i].bounciness;
            }
        }
        #endregion

        #region Unity lifecycle
        void OnDestroy()
        {
            if (paramsBuffer != null) paramsBuffer.Release();
            if (vertBuffer != null) vertBuffer.Release();
            if (Application.isPlaying && (contractor != null)) Destroy(contractor.gameObject);
        }

        void OnEnable() {
            //Also get the meshrenderer so that we can give it a unique material
            if (myRenderer == null)
            {
                myRenderer = GetComponent<Renderer>();
            }
            //If we have a MeshRenderer on our object and it's not world-static
            if (myRenderer)
            {
                UpdateBakingShaderParams();
            }
        }

        void Awake()
        {
            _localScale = transform.localScale;
            myRigidbody = GetComponent<Rigidbody>();
        }

        void Start()
        {
            hasStarted = false;
            isPhysicsUpdateFrame = false;

            if (myRigidbody)
            {
                myRigidbody.drag = unityDrag;
                myRigidbody.angularDrag = unityAngularDrag;
                baryonCount = mass * SRelativityUtil.avogadroNumber / initialAverageMolarMass;
            }
            
            _piw = isNonrelativisticShader ? transform.position.OpticalToWorld(peculiarVelocity, GetComoving4Acceleration()) : transform.position;
            riw = transform.rotation;
            checkSpeed();
            UpdatePhysicsCaches();

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

            wasKinematic = false;
            wasFrozen = false;

            UpdateColliders();

            MarkStaticColliderPos();

            //Get the meshfilter
            if (isParent)
            {
                CombineParent();
            }

            meshFilter = GetComponent<MeshFilter>();

            if (myRigidbody)
            {
                //Native rigidbody gravity should not be used except during isFullPhysX.
                myRigidbody.useGravity = useGravity && !isLightMapStatic;
            }
        }

        protected bool isPhysicsCacheValid;

        protected void UpdatePhysicsCaches()
        {
            isPhysicsCacheValid = false;

            updateMetric = GetMetric();
            updatePlayerViwTimeFactor = state.PlayerVelocityVector.InverseGamma(updateMetric);
            updateViwTimeFactor = viw.InverseGamma(updateMetric);
            updateWorld4Acceleration = comovingAccel.ProperToWorldAccel(viw, updateViwTimeFactor);
            updateTisw = piw.GetTisw(viw, updateWorld4Acceleration);

            isPhysicsCacheValid = true;
        }

        protected void AfterPhysicsUpdate()
        {
            oldLocalTimeFixedUpdate = GetLocalTime();

            if (myRigidbody)
            {
                if (!isNonrelativisticShader)
                {
                    // Get the relativistic position and rotation after the physics update:
                    riw = myRigidbody.rotation;
                    _piw = myRigidbody.position;
                }

                // Now, update the velocity and angular velocity based on the collision result:
                _aviw = myRigidbody.angularVelocity / updatePlayerViwTimeFactor;
                peculiarVelocity = myRigidbody.velocity.RapidityToVelocity(updateMetric);
            }

            if (isNonrelativisticShader)
            {
                // Immediately correct the nonrelativistic shader position.
                UpdateContractorPosition();
            }

            if (isMonopoleAccel)
            {
                float softenFactor = 1 + monopoleAccelerationSoften;
                float tempSoftenFactor = Mathf.Pow(softenFactor, 1.0f / 4);

                monopoleTemperature /= tempSoftenFactor;
                float origBackgroundTemp = state.gravityBackgroundPlanckTemperature;
                state.gravityBackgroundPlanckTemperature /= tempSoftenFactor;

                Vector3 accel = ((peculiarVelocity - oldVelocity) / lastFixedUpdateDeltaTime + aiw) / softenFactor;
                EvaporateMonopole(softenFactor * lastFixedUpdateDeltaTime, accel);

                state.gravityBackgroundPlanckTemperature = origBackgroundTemp;
                monopoleTemperature *= tempSoftenFactor;
            }

            checkSpeed();
            UpdatePhysicsCaches();
            UpdateColliderPosition();
        }

        void Update()
        {
            if (isPhysicsUpdateFrame)
            {
                AfterPhysicsUpdate();
            }
            isPhysicsUpdateFrame = false;
        }

        private void LateUpdate()
        {
            oldLocalTimeUpdate = GetLocalTime();
        }

        void FixedUpdate()
        {
            if (isPhysicsUpdateFrame)
            {
                AfterPhysicsUpdate();
            }
            isPhysicsUpdateFrame = false;

            if (!isPhysicsCacheValid)
            {
                UpdatePhysicsCaches();
            }

            if (state.isMovementFrozen || !state.isInitDone)
            {
                // If our rigidbody is not null, and movement is frozen, then set the object to standstill.
                UpdateRigidbodyVelocity();
                UpdateShaderParams();

                // We're done.
                return;
            }

            float deltaTime = state.FixedDeltaTimePlayer * GetTimeFactor();
            localTimeOffset += deltaTime - state.FixedDeltaTimeWorld;

            if (isLightMapStatic)
            {
                if (isMonopoleAccel)
                {
                    EvaporateMonopole(deltaTime, aiw);
                }

                UpdateColliderPosition();

                return;
            }

            if (!comoveViaAcceleration)
            {
                Comovement cm = state.conformalMap.ComoveOptical(deltaTime, piw, riw);
                riw = cm.riw;
                _piw = cm.piw;

                if (!isNonrelativisticShader && myRigidbody)
                {
                    // We'll MovePosition() for isNonrelativisticShader, further below.
                    myRigidbody.MovePosition(piw);
                }
            }

            //As long as our object is actually alive, perform these calculations
            if (meshFilter && transform) 
            {
                //If we're past our death time (in the player's view, as seen by tisw)
                if (state.TotalTimeWorld + localTimeOffset + deltaTime > DeathTime)
                {
                    KillObject();
                }
                else if (!hasStarted && (state.TotalTimeWorld + localTimeOffset + deltaTime > StartTime))
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
                            for (int i = 0; i < audioSources.Length; ++i)
                            {
                                audioSources[i].enabled = true;
                            }
                        }
                    }
                }
            }

            #region rigidbody
            // The rest of the updates are for objects with Rigidbodies that move and aren't asleep.
            if (isKinematic || !myRigidbody || myRigidbody.IsSleeping())
            {
                if (myRigidbody)
                {
                    myRigidbody.angularVelocity = Vector3.zero;
                    myRigidbody.velocity = Vector3.zero;

                    if (!isKinematic)
                    {
                        _aviw = Vector3.zero;
                        UpdateMotion(Vector3.zero, Vector3.zero);
                    }
                }

                if (!isLightMapStatic)
                {
                    transform.position = isNonrelativisticShader ? opticalPiw : piw;
                }

                UpdateShaderParams();

                // We're done.
                return;
            }

            if (isNonrelativisticShader)
            {
                // Update riw
                float aviwMag = aviw.magnitude;
                Quaternion diffRot;
                if (aviwMag <= SRelativityUtil.FLT_EPSILON)
                {
                    diffRot = Quaternion.identity;
                }
                else
                {
                    diffRot = Quaternion.AngleAxis(Mathf.Rad2Deg * deltaTime * aviwMag, aviw / aviwMag);
                }
                riw = riw * diffRot;
                myRigidbody.MoveRotation(riw);

                // Update piw from "peculiar velocity" in free fall coordinates.
                _piw += deltaTime * peculiarVelocity;

                transform.parent = null;
                myRigidbody.MovePosition(opticalPiw);
                contractor.position = myRigidbody.position;
                transform.parent = contractor;
                transform.localPosition = Vector3.zero;
                ContractLength();
            }
            UpdateColliderPosition();
            #endregion

            // FOR THE PHYSICS UPDATE ONLY, we give our rapidity to the Rigidbody
            UpdateRigidbodyVelocity();

            oldVelocity = peculiarVelocity;
            lastFixedUpdateDeltaTime = deltaTime;

            isPhysicsUpdateFrame = true;
        }

        protected void EvaporateMonopole(float deltaTime, Vector3 myAccel)
        {
            // If the RelativisticObject is at rest on the ground, according to Strano 2019, (not yet peer reviewed,)
            // it loses surface acceleration, (not weight force, directly,) the longer it stays in this configuration.
            // The Rindler horizon evaporates as would Schwarzschild, for event horizon surface acceleration equivalent
            // between the Rindler and Schwarzschild metrics. Further, Hawking(-Unruh, et al.) acceleration might have
            // the same effect.

            // The Rindler horizon evaporates as a Schwarzschild event horizon with the same surface gravity, according to Strano.
            // We add any background radiation power, proportional to the fourth power of the background temperature.
            double alpha = myAccel.magnitude;
            bool isNonZeroTemp = alpha > SRelativityUtil.FLT_EPSILON;

            double r = double.PositiveInfinity;
            // If alpha is in equilibrium with the background temperature, there is no evaporation.
            if (isNonZeroTemp)
            {
                // Surface acceleration at event horizon:
                r = state.SpeedOfLightSqrd / (2 * alpha);
                r = SRelativityUtil.EffectiveRaditiativeRadius((float)r, state.gravityBackgroundPlanckTemperature);
            }

            if (r < state.planckLength)
            {
                // For minimum area calculation, below.
                r = state.planckLength;
            }

            if (!double.IsInfinity(r) && !double.IsNaN(r))
            {
                isNonZeroTemp = true;
                r += SRelativityUtil.SchwarzschildRadiusDecay(deltaTime, r);
                if (r <= SRelativityUtil.FLT_EPSILON)
                {
                    leviCivitaDevAccel += myAccel;
                }
                else
                {
                    double alphaF = state.SpeedOfLightSqrd / (2 * r);
                    leviCivitaDevAccel += (float)(alpha - alphaF) * myAccel.normalized;
                }
            }

            if (myRigidbody)
            {
                double myTemperature = monopoleTemperature;
                double surfaceArea;
                if (meshFilter == null)
                {
                    Vector3 lwh = transform.localScale;
                    surfaceArea = 2 * (lwh.x * lwh.y + lwh.x * lwh.z + lwh.y * lwh.z);
                }
                else
                {
                    surfaceArea = meshFilter.sharedMesh.SurfaceArea();
                }
                // This is the ambient temperature, including contribution from comoving accelerated rest temperature.
                double ambientTemperature = isNonZeroTemp ? SRelativityUtil.SchwarzRadiusToPlanckScaleTemp(r) : state.gravityBackgroundPlanckTemperature;
                double dm = (gravitonEmissivity * surfaceArea * SRelativityUtil.sigmaPlanck * (Math.Pow(myTemperature, 4) - Math.Pow(ambientTemperature, 4))) / state.planckArea;

                // Momentum is conserved. (Energy changes.)
                Vector3 momentum = mass * peculiarVelocity;

                double camm = (mass - dm) * SRelativityUtil.avogadroNumber / baryonCount;

                if ((myTemperature >= 0) && (camm < fundamentalAverageMolarMass))
                {
                    currentAverageMolarMass = fundamentalAverageMolarMass;
                }
                else if (camm <= 0)
                {
                    mass = 0;
                }
                else
                {
                    mass -= (float)dm;
                }

                if (mass > SRelativityUtil.FLT_EPSILON)
                {
                    peculiarVelocity = momentum / mass;
                }
            }
        }

        public void OnCollisionEnter(Collision collision)
        {
            OnCollision();
        }

        public void OnCollisionStay(Collision collision)
        {
            OnCollision();
        }

        public void OnCollisionExit(Collision collision)
        {
            
        }
        #endregion

        #region Rigidbody mechanics
        public void OnCollision()
        {
            if (isKinematic || state.isMovementFrozen || !myRigidbody)
            {
                return;
            }

            // Like how Rigidbody components are co-opted for efficient relativistic motion,
            // it's feasible to get (at least reasonable, if not exact) relativistic collision
            // handling by transforming the end state after PhysX collisions.

            // We pass the RelativisticObject's rapidity to the rigidbody, right before the physics update
            // We restore the time-dilated visual apparent velocity, afterward

            if (isNonrelativisticShader)
            {
                riw = myRigidbody.rotation;
                _piw = myRigidbody.position.OpticalToWorld(peculiarVelocity, updateWorld4Acceleration);
            }

            isPhysicsUpdateFrame = true;
            AfterPhysicsUpdate();
            isPhysicsUpdateFrame = false;
        }
        #endregion
    }
}