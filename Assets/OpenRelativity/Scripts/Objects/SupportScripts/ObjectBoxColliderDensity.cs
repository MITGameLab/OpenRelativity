using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using OpenRelativity;

namespace OpenRelativity.Objects
{
    public class ObjectBoxColliderDensity : MonoBehaviour
    {

        //Need these three lists to create a new set of each variable needed for a new mesh
        public Vector3[] origPositions { get; set; }
        //state array contains information on splitting
        public bool state { get; set; }

        //Store the original box collider (disabled):
        public BoxCollider[] original { get; set; }
        public List<BoxCollider> change { get; set; }

        public ComputeShader colliderShader;

        //This constant determines maximum box size. We subdivide boxes until all their dimensions are less than this length.
        private float constant = 16;

        private int totalBoxCount;

        //We handle length contraction in a separate step, for a little improvement in accuracy.
        // To do it, we use a "contractor" transform:
        private Transform contractor;
        private Vector3 contractorLocalScale;
        //private int oldParentID;
        private Transform colliderTransform;
        private RelativisticObject myRO;
        private Rigidbody myRB;

        private ComputeBuffer paramsBuffer;
        private ComputeBuffer posBuffer;
        //To avoid garbage collection, we might over-allocate the buffer:
        private int origPositionsBufferLength;
        private Vector3[] trnsfrmdPositions;

        System.Diagnostics.Stopwatch coroutineTimer;

        private bool finishedCoroutine;

        private GameState _gameState = null;
        private GameState gameState
        {
            get
            {
                if (_gameState == null)
                {
                    _gameState = GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>();
                }

                return _gameState;
            }
        }

        // Use this for initialization, before relativistic object CombineParent() starts.
        void Awake()
        {
            finishedCoroutine = false;
            coroutineTimer = new System.Diagnostics.Stopwatch();
            myRO = GetComponent<RelativisticObject>();
            SetUpContractor();
            //Grab the meshfilter, and if it's not null, keep going
            BoxCollider[] origBoxColliders = GetComponents<BoxCollider>();
            if (origBoxColliders.Length > 0 && myRO != null)
            {
                //Prepare a new list of colliders for our split collider
                change = new List<BoxCollider>();

                //Store a copy of our original mesh
                original = origBoxColliders;
                totalBoxCount = 0;
                for (int i = 0; i < original.Length; i++)
                {
                    original[i].enabled = false;
                    //Split this collider until all of its dimensions have length less than our chosen value
                    Subdivide(original[i], change);
                }
            }
            else
            {
                original = null;
                change = null;
            }
        }

        private void Update()
        {
            //if (finishedCoroutine)
            //{
            //    finishedCoroutine = false;
            //    StartCoroutine("UpdatePositions");
            //}

            if (colliderShader != null && SystemInfo.supportsComputeShaders)
            {
                UpdateColliderPositions();
                ContractLength();
            }
        }

        private IEnumerator CPUUpdatePositions()
        {
            //if (colliderShader != null && SystemInfo.supportsComputeShaders)
            //{
            //UpdateColliderPositions();
            coroutineTimer.Start();
            ContractLength();
            Vector3 initCOM = myRB.centerOfMass;
            Vector3 viw = myRO.viw;
            Vector3 playerPos = gameState.playerTransform.position;
            Vector3 vpw = gameState.PlayerVelocityVector;
            for (int i = 0; i < totalBoxCount; i++)
            {
                change[i].center = origPositions[i].WorldToOpticalNoLengthContract(viw, playerPos, vpw);
                if (coroutineTimer.ElapsedMilliseconds >= 5)
                {
                    coroutineTimer.Stop();
                    coroutineTimer.Reset();
                    yield return null;
                    coroutineTimer.Start();
                }
            }
            //Cache actual world center of mass, and then reset local (rest frame) center of mass:
            myRB.ResetCenterOfMass();
            myRO.opticalWorldCenterOfMass = myRB.worldCenterOfMass;
            myRB.centerOfMass = initCOM;
            //}
            finishedCoroutine = true;
            coroutineTimer.Stop();
            coroutineTimer.Reset();
        }

        private void UpdateColliderPositions()
        {
            //Debug.Log("Updating mesh collider.");

            //Freeze the physics if the global state is frozen.
            if (gameState.MovementFrozen)
            {
                if (!myRO.wasFrozen)
                {
                    //Read the state of the rigidbody and shut it off, once.
                    myRO.wasFrozen = true;
                    myRO.wasKinematic = myRB.isKinematic;
                    myRB.isKinematic = true;
                }
                myRO.collideTimeStart += gameState.DeltaTimeWorld;
                return;
            }
            else if (myRO.wasFrozen)
            {
                //Restore the state of the rigidbody, once.
                myRO.wasFrozen = false;
                myRO.isKinematic = myRO.wasKinematic;
            }

            //if (!GetComponent<MeshRenderer>().enabled /*|| colliderShaderParams.gtt == 0*/)
            //{
            //    return;
            //}

            //Set remaining global parameters:
            ShaderParams colliderShaderParams = myRO.colliderShaderParams;
            colliderShaderParams.ltwMatrix = colliderTransform.localToWorldMatrix;
            colliderShaderParams.wtlMatrix = colliderTransform.worldToLocalMatrix;
            //colliderShaderParams.piw = transform.position;
            //colliderShaderParams.viw = viw / (float)state.SpeedOfLight;
            //colliderShaderParams.aviw = aviw;
            colliderShaderParams.vpc = -gameState.PlayerVelocityVector / (float)gameState.SpeedOfLight;
            //colliderShaderParams.gtt = 
            colliderShaderParams.playerOffset = gameState.playerTransform.position;
            colliderShaderParams.speed = (float)(gameState.PlayerVelocity / gameState.SpeedOfLight);
            colliderShaderParams.spdOfLight = (float)gameState.SpeedOfLight;
            //colliderShaderParams.wrldTime = (float)state.TotalTimeWorld;
            //colliderShaderParams.strtTime = startTime;

            //Center of mass in local coordinates should be invariant,
            // but transforming the collider verts will change it,
            // so we save it and restore it at the end:
            Vector3 initCOM = myRB.centerOfMass;

            ShaderParams[] spa = new ShaderParams[1];
            spa[0] = colliderShaderParams;

            //Put verts in R/W buffer and dispatch:
            if (paramsBuffer == null)
            {
                paramsBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(colliderShaderParams));
            }
            paramsBuffer.SetData(spa);
            if (posBuffer == null)
            {
                posBuffer = new ComputeBuffer(origPositionsBufferLength, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
            }
            else if (posBuffer.count != origPositionsBufferLength)
            {
                posBuffer.Dispose();
                posBuffer = new ComputeBuffer(origPositionsBufferLength, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
            }
            posBuffer.SetData(origPositions);
            int kernel = colliderShader.FindKernel("CSMain");
            colliderShader.SetBuffer(kernel, "glblPrms", paramsBuffer);
            colliderShader.SetBuffer(kernel, "verts", posBuffer);
            colliderShader.Dispatch(kernel, origPositionsBufferLength, 1, 1);
            posBuffer.GetData(trnsfrmdPositions);

            //Change mesh:
            for (int i = 0; i < totalBoxCount; i++)
            {
                change[i].center = trnsfrmdPositions[i];
            }
            //Cache actual world center of mass, and then reset local (rest frame) center of mass:
            myRB.ResetCenterOfMass();
            myRO.opticalWorldCenterOfMass = myRB.worldCenterOfMass;
            myRB.centerOfMass = initCOM;

            //Debug.Log("Finished updating mesh collider.");
        }

        private void SetUpContractor()
        {
            if (contractor == null)
            {
                GameObject contractorGO = new GameObject();
                contractorGO.name = gameObject.name + " Contractor";
                contractorGO.layer = gameObject.layer;
                contractorGO.tag = "Contractor";
                contractor = contractorGO.transform;
                contractor.parent = null;
                contractor.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                contractor.parent = this.transform;
                contractor.localPosition = Vector3.zero;
                contractor.rotation = Quaternion.identity;
                contractorLocalScale = contractor.localScale;

                GameObject colliderGO = new GameObject();
                colliderGO.name = gameObject.name + " Collider";
                colliderGO.layer = gameObject.layer;
                colliderGO.tag = "Voxel Collider";
                colliderTransform = colliderGO.transform;
                colliderTransform.parent = contractor;
                colliderTransform.localPosition = Vector3.zero;
                colliderTransform.rotation = transform.rotation;
                colliderGO.AddComponent<ObjectBoxColliderDensityTag>().myRO = myRO;
                Rigidbody origRB = GetComponent<Rigidbody>();
                myRB = colliderGO.AddComponent<Rigidbody>();
                myRB.isKinematic = origRB.isKinematic;
                myRB.useGravity = origRB.useGravity;
                myRB.mass = origRB.mass;
                myRB.drag = origRB.drag;
                myRB.angularDrag = origRB.angularDrag;
                myRB.interpolation = origRB.interpolation;
                myRB.collisionDetectionMode = origRB.collisionDetectionMode;
                myRB.constraints = origRB.constraints;
            }
            else
            {
                contractor.parent = null;
                colliderTransform.parent =null;
                contractor.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                contractor.parent = transform;
                contractor.position = gameState.playerTransform.position;
                colliderTransform.parent = contractor;
                contractorLocalScale = contractor.localScale;
            }
        }

        public void ContractLength()
        {
            //WARNING: Doppler shift is inaccurate due to order of player and object frame updates

            if (contractor == null)
            {
                SetUpContractor();
            }
            //else
            //{
            //    int parentID = contractor.parent.gameObject.GetInstanceID();
            //    if (parentID != oldParentID)
            //    {
            //        SetUpContractor();
            //    }
            //}
            Vector3 playerVel = gameState.PlayerVelocityVector;
            Vector3 relVel = myRO.viw.RelativeVelocityTo(playerVel);
            float relVelMag = relVel.sqrMagnitude;

            //Undo length contraction from previous state, and apply updated contraction:
            // - First, return to world frame:
            //contractor.localPosition = Vector3.zero;
            //Transform cparent = contractor.parent;
            //contractor.parent = null;
            //contractor.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            contractor.localScale = contractorLocalScale;
            contractor.localScale = contractorLocalScale;
            if ((contractor.lossyScale - new Vector3(1.0f, 1.0f, 1.0f)).sqrMagnitude > 0.0001)
            {
                SetUpContractor();
            }

            // - Reset the contractor, in any case:
            colliderTransform.parent = null;
            contractor.position = gameState.playerTransform.position;
            colliderTransform.position = transform.position;

            if (relVelMag > 0.0f)
            {
                relVelMag = Mathf.Sqrt(relVelMag);
                // - If we need to contract the object, unparent it from the contractor before rotation:
                //transform.parent = cparent;

                // - Rotate contractor to point parallel to velocity relative player:
                contractor.rotation = Quaternion.FromToRotation(Vector3.forward, relVel / relVelMag);

                // - Re-parent the object to the contractor before length contraction:
                colliderTransform.parent = contractor;

                // - Set the scale based only on the velocity relative to the player:
                contractor.localScale = contractorLocalScale.ContractLengthBy(relVelMag * Vector3.forward);
            }
            else
            {
                colliderTransform.parent = contractor;
            }
            //contractor.parent = cparent;
        }

        //Just subdivide something
        //Using this for subdividing a mesh as far as we need to for its mesh triangle area to be less than our defined constant
        public bool Subdivide(BoxCollider orig, List<BoxCollider> newColliders)
        {
            List<Vector3> origPosList = new List<Vector3>();
            //Take in the UVs and Triangles
            bool change = false;
            Transform origTransform = orig.transform;

            colliderTransform.parent = transform;
            colliderTransform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            colliderTransform.localRotation = Quaternion.identity;

            Vector3 origSize = orig.size;
            Vector3 origCenter = orig.center;
            Vector3 origWorldCenter = origTransform.TransformPoint(origCenter);

            Vector3 xEdge = origSize.x / 2.0f * new Vector3(1.0f, 0.0f, 0.0f);
            float xFar = (origCenter + xEdge).x;
            float xNear = (origCenter - xEdge).x;
            float xExtent = (origTransform.TransformPoint(xEdge + origCenter) - origWorldCenter).magnitude;
            Vector3 yEdge = origSize.y / 2.0f * new Vector3(0.0f, 1.0f, 0.0f);
            float yFar = (origCenter + yEdge).y;
            float yNear = (origCenter - yEdge).y;
            float yExtent = (origTransform.TransformPoint(yEdge + origCenter) - origWorldCenter).magnitude;
            Vector3 zEdge = origSize.z / 2.0f * new Vector3(0.0f, 0.0f, 1.0f);
            float zFar = (origCenter + zEdge).z;
            float zNear = (origCenter - zEdge).z;
            float zExtent = (origTransform.TransformPoint(zEdge + origCenter) - origWorldCenter).magnitude;

            int xCount = ((int)(2.0f * xExtent / constant + 0.5f));
            int yCount = ((int)(2.0f * yExtent / constant + 0.5f));
            int zCount = ((int)(2.0f * zExtent / constant + 0.5f));
            totalBoxCount += xCount * yCount * zCount;

            Vector3 newColliderPos = new Vector3();
            Vector3 newColliderSize = new Vector3(origSize.x / xCount, origSize.y / yCount, origSize.z / zCount);
            GameObject colliderGO = colliderTransform.gameObject;
            for (int i = 0; i < xCount; i++)
            {
                newColliderPos.x = xNear + ((xFar - xNear) * i / xCount) + newColliderSize.x /2.0f;
                for (int j = 0; j < yCount; j++)
                {
                    newColliderPos.y = yNear + ((yFar - yNear) * j / yCount) + newColliderSize.y / 2.0f;
                    for (int k = 0; k < zCount; k++)
                    {
                        newColliderPos.z = zNear + ((zFar - zNear) * k / zCount) + newColliderSize.z / 2.0f;
                        BoxCollider newCollider = colliderGO.AddComponent<BoxCollider>();
                        newColliders.Add(newCollider);
                        newCollider.size = newColliderSize;
                        newCollider.center = newColliderPos;
                        origPosList.Add(newCollider.center);
                    }
                }
            }

            origPositions = origPosList.ToArray();
            origPositionsBufferLength = origPositions.Length;
            trnsfrmdPositions = new Vector3[origPositionsBufferLength];
            colliderTransform.parent = contractor;

            return change;
        }

        void OnDestroy()
        {
            if (paramsBuffer != null) paramsBuffer.Release();
            if (posBuffer != null) posBuffer.Release();
            if (colliderTransform != null) Destroy(colliderTransform.gameObject);
            if (contractor != null) Destroy(contractor.gameObject);
            for (int i = 0; i < original.Length; i++)
            {
                original[i].enabled = true;
            }
        }
    }
}