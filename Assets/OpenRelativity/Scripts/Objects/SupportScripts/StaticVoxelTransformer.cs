using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using OpenRelativity;

namespace OpenRelativity.Objects
{
    public class StaticVoxelTransformer : MonoBehaviour
    {
        public bool forceCPU = true;
        public bool takePriority = true;
        public bool sphericalCulling = false;
        public ComputeShader colliderShader;

        private const int cullingSqrDistance = 178 * 178;
        private const int cullingFrameInterval = 10;
        private int cullingFrameCount;

        //We cache (static) colliders and collider positions in parallel: 
        private Vector3[] queuedOrigPositions { get; set; }
        private List<Vector3> origPositionsList { get; set; }
        private List<Vector3> queuedOrigPositionsList { get; set; }
        private List<BoxCollider> allColliders { get; set; }
        private List<BoxCollider> queuedColliders { get; set; }

        //The queue is a one-dimensional FIFO of contiguouos sub-list intervals:
        private Dictionary<Guid, int> batchSizeDict;
        private List<Guid> serialQueue;

        private ComputeBuffer paramsBuffer;
        private ComputeBuffer posBuffer;
        //To avoid garbage collection, we might over-allocate the buffer:
        private int origPositionsBufferLength;
        private Vector3[] trnsfrmdPositions;

        System.Diagnostics.Stopwatch coroutineTimer;

        private bool finishedCoroutine;
        private bool dispatchedShader;
        //private bool wasFrozen;

        private ShaderParams colliderShaderParams;

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

        private void Awake()
        {
            Init();
        }

        // Use this for initialization
        private void Start()
        {
            origPositionsBufferLength = 0;
            cullingFrameCount = 0;
            finishedCoroutine = true;
            dispatchedShader = false;
            //wasFrozen = false;
            coroutineTimer = new System.Diagnostics.Stopwatch();

            if (forceCPU || colliderShader == null)
            {
                CPUUpdatePositions();
            }
            else
            {
                GPUUpdatePositions();
            }
        }
        void Init()
        {
            if (origPositionsList == null) origPositionsList = new List<Vector3>();
            if (queuedColliders == null) queuedColliders = new List<BoxCollider>();
            if (queuedOrigPositionsList == null) queuedOrigPositionsList = new List<Vector3>();
            if (allColliders == null) allColliders = new List<BoxCollider>();
            if (batchSizeDict == null) batchSizeDict = new Dictionary<Guid, int>();
            if (serialQueue == null) serialQueue = new List<Guid>();

            colliderShaderParams = new ShaderParams()
            {
                ltwMatrix = Matrix4x4.zero,
                wtlMatrix = Matrix4x4.zero,
                viw = Vector3.zero
            };
        }

        //We ask for a position in the single running physics queue.
        // We are responsible for telling the transformer that we no longer need collider physics.
        public Guid TakeQueueNumber(List<BoxCollider> collidersToAdd, Vector3[] positionsToAdd)
        {
            Init();

            Guid batchNum = Guid.NewGuid();

            allColliders.AddRange(collidersToAdd);
            origPositionsList.AddRange(positionsToAdd);
            Cull();
            batchSizeDict.Add(batchNum, positionsToAdd.Length);
            serialQueue.Add(batchNum);

            return batchNum;
        }

        public bool ReturnQueueNumber(Guid qn)
        {
            if (batchSizeDict.ContainsKey(qn))
            {
                int startIndex = 0;
                Guid guid;
                for (int i = 0; i < batchSizeDict.Count; i++)
                {
                    if (!serialQueue[i].Equals(qn))
                    {
                        guid = serialQueue[i];
                        startIndex += batchSizeDict[guid];
                    }
                    else
                    {
                        int size = batchSizeDict[qn];

                        origPositionsList.RemoveRange(startIndex, size);
                        allColliders.RemoveRange(startIndex, size);

                        //queuedOrigPositions = origPositionsList.ToArray();
                        batchSizeDict.Remove(qn);
                        serialQueue.RemoveAt(i);

                        break;
                    }
                }

                Cull();

                return true;
            }
            else
            {
                return false;
            }
        }

        private void Update()
        {
            UpdatePositions();
        }

        // Update is called once per frame
        public void UpdatePositions()
        {
            if (!gameState.MovementFrozen)
            {
                if (sphericalCulling) cullingFrameCount++;
                if (finishedCoroutine)
                {
                    if (sphericalCulling && ((cullingFrameCount + 1) >= cullingFrameInterval))
                    {
                        cullingFrameCount = 0;
                        Cull();
                    }
                    if (colliderShader != null && SystemInfo.supportsComputeShaders && !forceCPU)
                    {
                        finishedCoroutine = false;
                        StartCoroutine("GPUUpdatePositions");
                    }
                    else
                    {
                        finishedCoroutine = false;
                        StartCoroutine("CPUUpdatePositions");
                    }
                }
            }
            else
            {
                queuedColliders.Clear();
                queuedColliders.AddRange(allColliders);
                queuedOrigPositionsList.Clear();
                queuedOrigPositionsList.AddRange(origPositionsList);
                queuedOrigPositions = queuedOrigPositionsList.ToArray();
                cullingFrameCount = cullingFrameInterval;
                //if (!wasFrozen)
                //{
                    if (colliderShader != null && SystemInfo.supportsComputeShaders && !forceCPU)
                    {
                        finishedCoroutine = false;
                        StartCoroutine("GPUUpdatePositions");
                    }
                    else //if (finishedCoroutine)
                    {
                        finishedCoroutine = false;
                        StartCoroutine("CPUUpdatePositions");
                    }
                //}
                //wasFrozen = true;
            }
        }

        private IEnumerator GPUUpdatePositions()
        {
            coroutineTimer.Stop();
            coroutineTimer.Reset();
            coroutineTimer.Start();

            colliderShaderParams.vpc = -gameState.PlayerVelocityVector / (float)gameState.SpeedOfLight;
            colliderShaderParams.playerOffset = gameState.playerTransform.position;
            colliderShaderParams.speed = (float)(gameState.PlayerVelocity / gameState.SpeedOfLight);
            colliderShaderParams.spdOfLight = (float)gameState.SpeedOfLight;

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
            else if (posBuffer.count < origPositionsBufferLength)
            {
                posBuffer.Dispose();
                posBuffer = new ComputeBuffer(origPositionsBufferLength, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
            }
            if (trnsfrmdPositions == null || (trnsfrmdPositions.Length < queuedOrigPositions.Length))
            {
                trnsfrmdPositions = new Vector3[queuedOrigPositions.Length];
            }
            //Read data for frame at last possible moment:
            if (dispatchedShader)
            {
                posBuffer.GetData(trnsfrmdPositions);
                dispatchedShader = false;
            }

            posBuffer.SetData(queuedOrigPositions);
            int kernel = colliderShader.FindKernel("CSMain");
            colliderShader.SetBuffer(kernel, "glblPrms", paramsBuffer);
            colliderShader.SetBuffer(kernel, "verts", posBuffer);

            //Dispatch doesn't block, but it might take multiple frames to return:
            colliderShader.Dispatch(kernel, origPositionsBufferLength, 1, 1);
            dispatchedShader = true;

            //Update the old result while waiting:
            float nanInfTest;
            for (int i = 0; i < queuedColliders.Count; i++)
            {
                nanInfTest = Vector3.Dot(trnsfrmdPositions[i], trnsfrmdPositions[i]);
                if (!float.IsInfinity(nanInfTest) && !float.IsNaN(nanInfTest))
                {
                    if ((!takePriority) && (coroutineTimer.ElapsedMilliseconds > 16))
                    {
                        coroutineTimer.Stop();
                        coroutineTimer.Reset();
                        yield return null;
                        coroutineTimer.Start();
                    }
                    queuedColliders[i].center = queuedColliders[i].transform.InverseTransformPoint(trnsfrmdPositions[i]);
                }
            }

            finishedCoroutine = true;
            coroutineTimer.Stop();
            coroutineTimer.Reset();
        }

        private IEnumerator CPUUpdatePositions()
        {
            coroutineTimer.Reset();
            coroutineTimer.Start();
            Vector3 playerPos = gameState.playerTransform.position;
            Vector3 vpw = gameState.PlayerVelocityVector;
            Vector4 pap = gameState.PlayerAccelerationVector;
            Vector3 avp = gameState.PlayerAngularVelocityVector;
            Matrix4x4 vpcLorentz = gameState.PlayerLorentzMatrix;
            for (int i = 0; i < queuedColliders.Count; i++)
            {
                Vector3 newPos = queuedColliders[i].transform.InverseTransformPoint(((Vector4)(queuedOrigPositions[i])).WorldToOptical(Vector3.zero, playerPos, vpw, pap, avp, Vector4.zero, vpcLorentz, Matrix4x4.identity));
                //Change mesh:
                if ((!takePriority) && (coroutineTimer.ElapsedMilliseconds > 16))
                {
                    coroutineTimer.Stop();
                    coroutineTimer.Reset();
                    yield return null;
                    coroutineTimer.Start();
                }
                queuedColliders[i].center = newPos;
            }

            finishedCoroutine = true;
            coroutineTimer.Stop();
            coroutineTimer.Reset();
        }

        private void Cull()
        {
            Init();
            if (sphericalCulling)
            {
                queuedOrigPositionsList.Clear();
                queuedColliders.Clear();
                Vector3 playerPos = gameState.playerTransform.position;
                Vector3 vpw = gameState.PlayerVelocityVector;
                Vector4 pap = gameState.PlayerAccelerationVector;
                Vector3 avp = gameState.PlayerAngularVelocityVector;
                Matrix4x4 vpcLorentz = gameState.PlayerLorentzMatrix;
                float distSqr;

                for (int i = 0; i < origPositionsList.Count; i++)
                {
                    distSqr = (((Vector4)origPositionsList[i]).WorldToOptical(Vector3.zero, playerPos, vpw, pap, avp, Vector4.zero, vpcLorentz, Matrix4x4.identity) - playerPos).sqrMagnitude;
                    if (distSqr < cullingSqrDistance)
                    {
                        allColliders[i].enabled = true;
                        queuedColliders.Add(allColliders[i]);
                        queuedOrigPositionsList.Add(origPositionsList[i]);
                    }
                    else
                    {
                        allColliders[i].enabled = false;
                    }
                }
            }
            else
            {
                queuedColliders.Clear();
                queuedColliders.AddRange(allColliders);
                queuedOrigPositionsList.Clear();
                queuedOrigPositionsList.AddRange(origPositionsList);
            }

            queuedOrigPositions = queuedOrigPositionsList.ToArray();
            origPositionsBufferLength = queuedOrigPositions.Length;
        }
    }
}
