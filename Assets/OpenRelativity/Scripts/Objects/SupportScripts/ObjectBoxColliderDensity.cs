using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using OpenRelativity;

namespace OpenRelativity.Objects
{
    public class ObjectBoxColliderDensity : MonoBehaviour
    {
        //If a large number of voxels are static with respect to world coordinates, we can batch them and gain performance:
        public bool isStatic = true;
        public float oversizePercent = 0.1f;
        private bool wasStatic;
        private Guid staticQueueNumber;

        //Need these three lists to create a new set of each variable needed for a new mesh
        public Vector3[] origPositions { get; set; }
        //state array contains information on splitting
        public bool state { get; set; }

        //Store the original box collider (disabled):
        public BoxCollider[] original { get; set; }
        public List<BoxCollider> change { get; set; }

        public ComputeShader colliderShader;

        //This constant determines maximum box size. We subdivide boxes until all their dimensions are less than this length.
        private float constant = 24;

        private int totalBoxCount;

        private RelativisticObject myRO;
        private Rigidbody myRB;

        private ComputeBuffer paramsBuffer;
        private ComputeBuffer posBuffer;
        //To avoid garbage collection, we might over-allocate the buffer:
        private int origPositionsBufferLength;
        private Vector3[] trnsfrmdPositions;

        System.Diagnostics.Stopwatch coroutineTimer;

        private bool finishedCoroutine;
        private bool dispatchedShader;

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
            wasStatic = isStatic;
            finishedCoroutine = true;
            dispatchedShader = false;
            coroutineTimer = new System.Diagnostics.Stopwatch();
            myRO = GetComponent<RelativisticObject>();
            myRB = GetComponent<Rigidbody>();
            //Grab the meshfilter, and if it's not null, keep going
            BoxCollider[] origBoxColliders = GetComponents<BoxCollider>();

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

        private void OnEnable()
        {
            if (isStatic)
            {
                TakeQueueNumber();
            }
            else
            {
                if (colliderShader == null)
                {
                    CPUUpdatePositions();
                }
                else
                {
                    GPUUpdatePositions();
                }
            }
        }

        void OnDisable()
        {
            if (isStatic)
            {
                ReturnQueueNumber();
            }
        }

        private void TakeQueueNumber()
        {
            StaticVoxelTransformer svt = FindObjectOfType<StaticVoxelTransformer>();
            if (svt != null)
            {
                Vector3[] worldPositions = new Vector3[origPositions.Length];
                for (int i = 0; i < origPositions.Length; i++)
                {
                    worldPositions[i] = transform.TransformPoint(origPositions[i]);
                }
                staticQueueNumber = svt.TakeQueueNumber(change, worldPositions);
            }
        }

        private void ReturnQueueNumber()
        {
            StaticVoxelTransformer svt = FindObjectOfType<StaticVoxelTransformer>();
            if (svt != null)
            {
                svt.ReturnQueueNumber(staticQueueNumber);
            }
        }

        private void Update()
        {
            if (wasStatic && !isStatic)
            {
                ReturnQueueNumber();
            }
            else if (!wasStatic && isStatic)
            {
                TakeQueueNumber();
            }
            wasStatic = isStatic;
        }

        private void FixedUpdate()
        {
            if (!isStatic && !wasStatic)
            {
                UpdatePositions();
            }
        }

        public void UpdatePositions(Collider toUpdate = null)
        {
            if (toUpdate != null)
            {
                BoxCollider toUpdateBox = (BoxCollider)toUpdate;
                bool foundCollider = false;
                int i = 0;
                while ((!foundCollider) && (i < change.Count)) {
                    if (toUpdateBox.Equals(change[i]))
                    {
                        foundCollider = true;
                    }
                    else
                    {
                        i++;
                    }
                }

                if (foundCollider)
                {
                    //Vector4 playerAccel = gameState.PlayerVisualAccelerationVector;
                    if (isStatic)
                    {
                        toUpdateBox.center = transform.InverseTransformPoint(
                            ((Vector4)(transform.TransformPoint(origPositions[i]))).WorldToOptical(Vector3.zero, gameState.playerTransform.position, gameState.PlayerVelocityVector, gameState.PlayerAccelerationVector, gameState.PlayerAngularVelocityVector, Vector4.zero, gameState.PlayerLorentzMatrix, Matrix4x4.identity)
                       );
                    }
                    else
                    {
                        toUpdateBox.center = transform.InverseTransformPoint(
                            ((Vector4)(transform.TransformPoint(origPositions[i]))).WorldToOptical(myRO.viw, gameState.playerTransform.position, gameState.PlayerVelocityVector, gameState.PlayerAccelerationVector, gameState.PlayerAngularVelocityVector, myRO.GetTotalAcceleration(myRO.piw), gameState.PlayerLorentzMatrix, myRO.viwLorentz)
                       );
                    }
                }
            }
            else if (finishedCoroutine)
            {
                if (colliderShader != null && SystemInfo.supportsComputeShaders)
                {
                    finishedCoroutine = false;
                    StartCoroutine("GPUUpdatePositions");
                }
                else //if (finishedCoroutine)
                {
                    finishedCoroutine = false;
                    StartCoroutine("CPUUpdatePositions");
                }
            }
        }

        private IEnumerator CPUUpdatePositions()
        {
            coroutineTimer.Reset();
            coroutineTimer.Start();
            Vector3 initCOM = Vector3.zero;
            if (!isStatic)
            {
                initCOM = myRB.centerOfMass;
            }
            Vector3 viw;
            Vector4 aiw;
            if (isStatic) {
                viw = Vector3.zero;
                aiw = Vector4.zero;
            }
            else {
                viw = myRO.viw;
                aiw = myRO.GetTotalAcceleration(myRO.piw);
            }

            Vector3 playerPos = gameState.playerTransform.position;
            Vector3 vpw = gameState.PlayerVelocityVector;
            Vector4 pap = gameState.PlayerAccelerationVector;
            Vector3 avp = gameState.PlayerAngularVelocityVector;
            Matrix4x4 vpcLorentz = gameState.PlayerLorentzMatrix;
            Matrix4x4 viwLorentz = myRO.viwLorentz;
            for (int i = 0; i < totalBoxCount; i++)
            {
                Transform changeTransform = change[i].transform;
                Vector3 newPos = changeTransform.InverseTransformPoint(((Vector4)(changeTransform.TransformPoint(origPositions[i]))).WorldToOptical(viw, playerPos, vpw, pap, avp, aiw, vpcLorentz, viwLorentz));
                //Change mesh:
                if (coroutineTimer.ElapsedMilliseconds > 1)
                {
                    coroutineTimer.Stop();
                    coroutineTimer.Reset();
                    yield return new WaitForFixedUpdate();
                    coroutineTimer.Start();
                }
                change[i].center = newPos;
            }
            //Cache actual world center of mass, and then reset local (rest frame) center of mass:
            if (!isStatic)
            {
                myRB.ResetCenterOfMass();
                myRO.opticalWorldCenterOfMass = myRB.worldCenterOfMass;
                myRB.centerOfMass = initCOM;
            }
            //}
            finishedCoroutine = true;
            coroutineTimer.Stop();
            coroutineTimer.Reset();
        }

        private IEnumerator GPUUpdatePositions()
        {
            coroutineTimer.Stop();
            coroutineTimer.Reset();
            coroutineTimer.Start();
            //Debug.Log("Updating mesh collider.");

            //Freeze the physics if the global state is frozen.
            if (!isStatic)
            {
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
                }
                else if (myRO.wasFrozen)
                {
                    //Restore the state of the rigidbody, once.
                    myRO.wasFrozen = false;
                    myRO.isKinematic = myRO.wasKinematic;
                }
            }

            //Set remaining global parameters:
            ShaderParams colliderShaderParams = new ShaderParams();
            colliderShaderParams.ltwMatrix = transform.localToWorldMatrix;
            colliderShaderParams.wtlMatrix = transform.worldToLocalMatrix;
            if (isStatic)
            {
                colliderShaderParams.viw = Vector3.zero;
            }
            else
            {
                colliderShaderParams.viw = myRO.viw / (float)gameState.SpeedOfLight;
            }
            colliderShaderParams.vpc = -gameState.PlayerVelocityVector / (float)gameState.SpeedOfLight;
            colliderShaderParams.playerOffset = gameState.playerTransform.position;
            colliderShaderParams.speed = (float)(gameState.PlayerVelocity / gameState.SpeedOfLight);
            colliderShaderParams.spdOfLight = (float)gameState.SpeedOfLight;

            //Center of mass in local coordinates should be invariant,
            // but transforming the collider verts will change it,
            // so we save it and restore it at the end:

            Vector3 initCOM = Vector3.zero;
            if (!isStatic)
            {
                initCOM = myRB.centerOfMass;
            }

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
            //Read data for frame at last possible moment:
            if (dispatchedShader)
            {
                posBuffer.GetData(trnsfrmdPositions);
                dispatchedShader = false;
            }

            posBuffer.SetData(origPositions);
            int kernel = colliderShader.FindKernel("CSMain");
            colliderShader.SetBuffer(kernel, "glblPrms", paramsBuffer);
            colliderShader.SetBuffer(kernel, "verts", posBuffer);

            //Dispatch doesn't block, but it might take multiple frames to return:
            colliderShader.Dispatch(kernel, origPositionsBufferLength, 1, 1);
            dispatchedShader = true;

            //Update the old result while waiting:
            float nanInfTest;
            for (int i = 0; i < totalBoxCount; i++)
            {
                nanInfTest = Vector3.Dot(trnsfrmdPositions[i], trnsfrmdPositions[i]);
                if (!float.IsInfinity(nanInfTest) && !float.IsNaN(nanInfTest))
                {
                    if (coroutineTimer.ElapsedMilliseconds > 2)
                    {
                        coroutineTimer.Stop();
                        coroutineTimer.Reset();
                        yield return new WaitForFixedUpdate();
                        coroutineTimer.Start();
                    }
                    change[i].center = trnsfrmdPositions[i];
                }
            }

            if (!isStatic)
            {
                //Cache actual world center of mass, and then reset local (rest frame) center of mass:
                myRB.ResetCenterOfMass();
                myRO.opticalWorldCenterOfMass = myRB.worldCenterOfMass;
                myRB.centerOfMass = initCOM;
            }

            //Debug.Log("Finished updating mesh collider.");

            finishedCoroutine = true;
            coroutineTimer.Stop();
            coroutineTimer.Reset();
        }

        //Just subdivide something
        //Using this for subdividing a mesh as far as we need to for its mesh triangle area to be less than our defined constant
        public bool Subdivide(BoxCollider orig, List<BoxCollider> newColliders)
        {
            List<Vector3> origPosList = new List<Vector3>();
            if (origPositions != null &&  origPositions.Length > 0)
            {
                origPosList.AddRange(origPositions);
            }
            //Take in the UVs and Triangles
            bool change = false;
            Transform origTransform = orig.transform;

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

            int xCount = ((int)(2.0f * xExtent / constant + 1.0f));
            int yCount = ((int)(2.0f * yExtent / constant + 1.0f));
            int zCount = ((int)(2.0f * zExtent / constant + 1.0f));
            totalBoxCount += xCount * yCount * zCount;

            Vector3 newColliderPos = new Vector3();
            Vector3 newColliderSize = new Vector3(origSize.x / xCount, origSize.y / yCount, origSize.z / zCount);
            if (xCount > 1) newColliderSize.x = newColliderSize.x * (1 + oversizePercent);
            if (yCount > 1) newColliderSize.y = newColliderSize.y * (1 + oversizePercent);
            if (zCount > 1) newColliderSize.z = newColliderSize.z * (1 + oversizePercent);
            for (int i = 0; i < xCount; i++)
            {
                newColliderPos.x = xNear + ((xFar - xNear) * i / xCount) + newColliderSize.x /2.0f;
                for (int j = 0; j < yCount; j++)
                {
                    newColliderPos.y = yNear + ((yFar - yNear) * j / yCount) + newColliderSize.y / 2.0f;
                    for (int k = 0; k < zCount; k++)
                    {
                        newColliderPos.z = zNear + ((zFar - zNear) * k / zCount) + newColliderSize.z / 2.0f;
                        BoxCollider newCollider = gameObject.AddComponent<BoxCollider>();
                        newCollider.isTrigger = orig.isTrigger;
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

            return change;
        }

        void OnDestroy()
        {
            if (paramsBuffer != null) paramsBuffer.Release();
            if (posBuffer != null) posBuffer.Release();
            for (int i = 0; i < original.Length; i++)
            {
                original[i].enabled = true;
            }
        }
    }
}