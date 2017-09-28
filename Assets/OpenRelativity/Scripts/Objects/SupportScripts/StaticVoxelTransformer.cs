using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using OpenRelativity;

namespace OpenRelativity.Objects
{
    public class StaticVoxelTransformer : MonoBehaviour
    {

        //We cache (static) colliders and collider positions in parallel: 
        private Vector3[] origPositions { get; set; }
        private List<Vector3> origPositionsList { get; set; }
        private List<BoxCollider> queuedColliders { get; set; }

        //The queue is a one-dimensional FIFO of contiguouos sub-list intervals:
        private Dictionary<Guid, int> batchSizeDict;
        private List<Guid> serialQueue;

        //state array contains information on splitting
        public bool state { get; set; }

        public ComputeShader colliderShader;

        //This constant determines maximum box size. We subdivide boxes until all their dimensions are less than this length.
        private float constant = 16;

        private ComputeBuffer paramsBuffer;
        private ComputeBuffer posBuffer;
        //To avoid garbage collection, we might over-allocate the buffer:
        private int origPositionsBufferLength;
        private Vector3[] trnsfrmdPositions;

        System.Diagnostics.Stopwatch coroutineTimer;

        private bool finishedCoroutine;

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

        // Use this for initialization
        private void Start()
        {
            Init();
        }
        void Init()
        {
            if (origPositionsList == null) origPositionsList = new List<Vector3>();
            if (queuedColliders == null) queuedColliders = new List<BoxCollider>();
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

            queuedColliders.AddRange(collidersToAdd);
            origPositionsList.AddRange(positionsToAdd);
            origPositions = origPositionsList.ToArray();
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
                        queuedColliders.RemoveRange(startIndex, size);

                        origPositions = origPositionsList.ToArray();
                        batchSizeDict.Remove(qn);
                        serialQueue.RemoveAt(i);

                        break;
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        // Update is called once per frame
        public void UpdatePositions()
        {
            if (finishedCoroutine)
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
            else if (posBuffer.count != origPositionsBufferLength)
            {
                posBuffer.Dispose();
                posBuffer = new ComputeBuffer(origPositionsBufferLength, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
            }
            posBuffer.SetData(origPositions);
            int kernel = colliderShader.FindKernel("CSMain");
            colliderShader.SetBuffer(kernel, "glblPrms", paramsBuffer);
            colliderShader.SetBuffer(kernel, "verts", posBuffer);

            //Dispatch doesn't block, but it might take multiple frames to return:
            colliderShader.Dispatch(kernel, origPositionsBufferLength, 1, 1);
            
            //Update the old result while waiting:
            float nanInfTest;
            for (int i = 0; i < queuedColliders.Count; i++)
            {
                nanInfTest = Vector3.Dot(trnsfrmdPositions[i], trnsfrmdPositions[i]);
                if (!float.IsInfinity(nanInfTest) && !float.IsNaN(nanInfTest))
                {
                    if (coroutineTimer.ElapsedMilliseconds > 6)
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

            //Finish by reading in the data for the next frame:
            posBuffer.GetData(trnsfrmdPositions);
        }

        private IEnumerator CPUUpdatePositions()
        {
            coroutineTimer.Reset();
            coroutineTimer.Start();
            Vector3 playerPos = gameState.playerTransform.position;
            Vector3 vpw = gameState.PlayerVelocityVector;
            float nanInfTest;
            for (int i = 0; i < queuedColliders.Count; i++)
            {
                Transform changeTransform = queuedColliders[i].transform;
                Vector3 newPos = changeTransform.InverseTransformPoint(changeTransform.TransformPoint(origPositions[i]).WorldToOptical(Vector3.zero, playerPos, vpw));
                nanInfTest = Vector3.Dot(newPos, newPos);
                if (!float.IsInfinity(nanInfTest) && !float.IsNaN(nanInfTest))
                {
                    //Change mesh:
                    if (coroutineTimer.ElapsedMilliseconds > 1)
                    {
                        coroutineTimer.Stop();
                        coroutineTimer.Reset();
                        yield return null;
                        coroutineTimer.Start();
                    }
                    queuedColliders[i].center = changeTransform.InverseTransformPoint(changeTransform.TransformPoint(origPositions[i]).WorldToOptical(Vector3.zero, playerPos, vpw));
                }
            }

            finishedCoroutine = true;
            coroutineTimer.Stop();
            coroutineTimer.Reset();
        }
    }
}
