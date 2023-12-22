using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using OpenRelativity.Objects;

namespace OpenRelativity
{
    public class StaticVoxelSystem : RelativisticBehavior
    {
        public static StaticVoxelSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                Instance = this;
                Init();
            }
        }

        public bool forceCPU = true;
        public bool takePriority = true;
        public bool sphericalCulling = false;
        public ComputeShader colliderShader;

        private const int cullingSqrDistance = 64 * 64;
        private const int cullingFrameInterval = 8;
        private int cullingFrameCount;

        //We cache (static) colliders and collider positions in parallel: 
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
        private Vector3[] trnsfrmdPositions;

        System.Diagnostics.Stopwatch coroutineTimer;

        private bool finishedCoroutine;
        private bool dispatchedShader;
        private bool wasFrozen;

        private bool didInit = false;

        private ShaderParams colliderShaderParams;

        // Use this for initialization
        private void Start()
        {
            finishedCoroutine = true;
            dispatchedShader = false;

            Init();
        }

        void Init()
        {
            if (didInit)
            {
                return;
            }

            origPositionsList = new List<Vector3>();
            queuedColliders = new List<BoxCollider>();
            queuedOrigPositionsList = new List<Vector3>();
            allColliders = new List<BoxCollider>();
            batchSizeDict = new Dictionary<Guid, int>();
            serialQueue = new List<Guid>();
            coroutineTimer = new System.Diagnostics.Stopwatch();

            colliderShaderParams = new ShaderParams()
            {
                ltwMatrix = Matrix4x4.zero,
                wtlMatrix = Matrix4x4.zero,
                viw = Vector3.zero
            };

            didInit = true;
        }

        void StartTransformCoroutine()
        {
            finishedCoroutine = false;
            if (colliderShader != null && SystemInfo.supportsComputeShaders && !forceCPU)
            {
                StartCoroutine("GPUUpdatePositions");
            }
            else
            {
                StartCoroutine("CPUUpdatePositions");
            }
        }

        void StopTransformCoroutine()
        {
            if (colliderShader != null && SystemInfo.supportsComputeShaders && !forceCPU)
            {
                StopCoroutine("GPUUpdatePositions");
            }
            else
            {
                StopCoroutine("CPUUpdatePositions");
            }
            finishedCoroutine = true;
        }

        void OnDisable()
        {
            StopTransformCoroutine();
        }

        //We ask for a position in the single running physics queue.
        // We are responsible for telling the transformer that we no longer need collider physics.
        public Guid TakeQueueNumber(List<BoxCollider> collidersToAdd, Vector3[] positionsToAdd)
        {
            Init();

            Guid batchNum = Guid.NewGuid();

            StopTransformCoroutine();

            allColliders.AddRange(collidersToAdd);
            origPositionsList.AddRange(positionsToAdd);

            queuedColliders.AddRange(collidersToAdd);
            queuedOrigPositionsList.AddRange(positionsToAdd);

            Cull();

            batchSizeDict.Add(batchNum, positionsToAdd.Length);
            serialQueue.Add(batchNum);

            StartTransformCoroutine();

            return batchNum;
        }

        public bool ReturnQueueNumber(Guid qn)
        {
            if (!batchSizeDict.ContainsKey(qn))
            {
                return false;
            }

            StopTransformCoroutine();

            int startIndex = 0;
            Guid guid;
            for (int i = 0; i < batchSizeDict.Count; ++i)
            {
                if (!serialQueue[i].Equals(qn))
                {
                    guid = serialQueue[i];
                    startIndex += batchSizeDict[guid];
                }
                else
                {
                    int size = batchSizeDict[qn];

                    for (int j = startIndex; j < (startIndex + size); ++j)
                    {
                        allColliders[i].center = allColliders[i].transform.InverseTransformPoint(origPositionsList[i]);
                    }

                    origPositionsList.RemoveRange(startIndex, size);
                    allColliders.RemoveRange(startIndex, size);

                    //queuedOrigPositions = origPositionsList.ToArray();
                    batchSizeDict.Remove(qn);
                    serialQueue.RemoveAt(i);

                    break;
                }
            }

            queuedColliders.Clear();
            queuedColliders.AddRange(allColliders);
            queuedOrigPositionsList.Clear();
            queuedOrigPositionsList.AddRange(origPositionsList);

            StartTransformCoroutine();

            return true;
        }

        private void Update()
        {
            UpdatePositions();
        }

        // Update is called once per frame
        public void UpdatePositions()
        {
            if (!state.isMovementFrozen)
            {
                if (sphericalCulling) {
                    ++cullingFrameCount;
                }

                if (!finishedCoroutine)
                {
                    return;
                }

                StartTransformCoroutine();
            }
            else if (!wasFrozen)
            {
                StopTransformCoroutine();

                queuedColliders.Clear();
                queuedColliders.AddRange(allColliders);
                cullingFrameCount = 0;

                StartTransformCoroutine();
                wasFrozen = true;
            }
        }

        private IEnumerator GPUUpdatePositions()
        {
            coroutineTimer.Reset();
            coroutineTimer.Start();

            if (sphericalCulling && ((cullingFrameCount + 1) >= cullingFrameInterval))
            {
                cullingFrameCount = 0;
                Cull();
            }

            if (queuedOrigPositionsList.Count > 0)
            {
                colliderShaderParams.viw = Vector3.zero;
                colliderShaderParams.pao = Vector3.zero.ProperToWorldAccel(Vector3.zero, 1);
                colliderShaderParams.viwLorentzMatrix = Matrix4x4.identity;
                colliderShaderParams.invViwLorentzMatrix = Matrix4x4.identity;

                colliderShaderParams.ltwMatrix = Matrix4x4.identity;
                colliderShaderParams.wtlMatrix = Matrix4x4.identity;
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
                if (paramsBuffer == null)
                {
                    paramsBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(colliderShaderParams));
                }
                paramsBuffer.SetData(spa);
                if (posBuffer == null)
                {
                    posBuffer = new ComputeBuffer(queuedOrigPositionsList.Count, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
                }
                else if (posBuffer.count < queuedOrigPositionsList.Count)
                {
                    posBuffer.Dispose();
                    posBuffer = new ComputeBuffer(queuedOrigPositionsList.Count, System.Runtime.InteropServices.Marshal.SizeOf(new Vector3()));
                }
                if (trnsfrmdPositions == null || (trnsfrmdPositions.Length < queuedOrigPositionsList.Count))
                {
                    trnsfrmdPositions = queuedOrigPositionsList.ToArray();
                }
                //Read data for frame at last possible moment:
                if (dispatchedShader)
                {
                    posBuffer.GetData(trnsfrmdPositions);
                    dispatchedShader = false;
                }

                posBuffer.SetData(queuedOrigPositionsList.ToArray());
                int kernel = colliderShader.FindKernel("CSMain");
                colliderShader.SetBuffer(kernel, "glblPrms", paramsBuffer);
                colliderShader.SetBuffer(kernel, "verts", posBuffer);

                //Dispatch doesn't block, but it might take multiple frames to return:
                colliderShader.Dispatch(kernel, queuedOrigPositionsList.Count, 1, 1);
                dispatchedShader = true;

                //Update the old result while waiting:
                float nanInfTest;
                for (int i = 0; i < queuedColliders.Count; ++i)
                {
                    nanInfTest = Vector3.Dot(trnsfrmdPositions[i], trnsfrmdPositions[i]);
                    if (float.IsInfinity(nanInfTest) || float.IsNaN(nanInfTest))
                    {
                        continue;
                    }

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
        }

        private IEnumerator CPUUpdatePositions()
        {
            coroutineTimer.Reset();
            coroutineTimer.Start();

            if (sphericalCulling && ((cullingFrameCount + 1) >= cullingFrameInterval))
            {
                cullingFrameCount = 0;
                Cull();
            }

            for (int i = 0; i < queuedColliders.Count; ++i)
            {
                if (i >= queuedColliders.Count || i >= queuedOrigPositionsList.Count)
                {
                    break;
                }
                queuedColliders[i].center = queuedColliders[i].transform.InverseTransformPoint(
                    queuedOrigPositionsList[i].WorldToOptical(Vector3.zero, state.conformalMap.GetRindlerAcceleration(queuedOrigPositionsList[i]))
                );

                //Change mesh:
                if ((!takePriority) && (coroutineTimer.ElapsedMilliseconds > 16))
                {
                    coroutineTimer.Stop();
                    coroutineTimer.Reset();
                    yield return null;
                    coroutineTimer.Start();
                }
            }

            finishedCoroutine = true;
        }

        private void Cull()
        {
            Init();
            if (!sphericalCulling || state.isMovementFrozen)
            {
                return;
            }

            RelativisticObject[] ros = FindObjectsOfType<RelativisticObject>();
            List<Vector3> rosPiw = new List<Vector3>();
            for (int i = 0; i < ros.Length; ++i)
            {
                if (!ros[i].isLightMapStatic && ros[i].GetComponent<Rigidbody>() != null)
                {
                    Collider roC = ros[i].GetComponent<BoxCollider>();
                    if ((roC != null) && roC.enabled)
                    {
                        rosPiw.Add(ros[i].piw);
                    }
                }
            }
            queuedOrigPositionsList.Clear();
            queuedColliders.Clear();

            Vector3 playerPos = state.playerTransform.position;
            float distSqr;

            for (int i = 0; i < origPositionsList.Count; ++i)
            {
                // Don't cull anything (spherically) close to the player.
                Vector3 colliderPos = origPositionsList[i].WorldToOptical(Vector3.zero, state.conformalMap.GetRindlerAcceleration(queuedOrigPositionsList[i]));
                distSqr = (colliderPos - playerPos).sqrMagnitude;
                if (distSqr < cullingSqrDistance)
                {
                    queuedColliders.Add(allColliders[i]);
                    queuedOrigPositionsList.Add(origPositionsList[i]);
                }
                else
                {
                    bool didCull = true;
                    // The object isn't close to the player, but remote RelativisticObjects still need their own active collider spheres, if they're colliding far away.
                    for (int j = 0; j < rosPiw.Count; ++j)
                    {
                        distSqr = (colliderPos - rosPiw[j]).sqrMagnitude;
                        if (distSqr < cullingSqrDistance)
                        {
                            queuedColliders.Add(allColliders[i]);
                            queuedOrigPositionsList.Add(origPositionsList[i]);
                            didCull = false;
                            break;
                        }
                    }

                    // If the transform is culled, reset it.
                    if (didCull)
                    {
                        allColliders[i].center = allColliders[i].transform.InverseTransformPoint(origPositionsList[i]);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (dispatchedShader)
            {
                posBuffer.GetData(trnsfrmdPositions);
                dispatchedShader = false;

                posBuffer.Release();
                paramsBuffer.Release();
            }
        }
    }
}
