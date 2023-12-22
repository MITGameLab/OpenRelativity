using UnityEngine;
using System.Collections.Generic;
using System;

namespace OpenRelativity.Objects
{
    public class ObjectBoxColliderDensity : MonoBehaviour
    {
        //If a large number of voxels are static with respect to world coordinates, we can batch them and gain performance:
        public float oversizePercent = 0.1f;
        private Guid staticQueueNumber;

        //Need these three lists to create a new set of each variable needed for a new mesh
        public Vector3[] origPositions { get; set; }
        //state array contains information on splitting
        public bool state { get; set; }

        //Store the original box collider (disabled):
        public BoxCollider[] original { get; set; }
        public List<BoxCollider> change { get; set; }

        //This constant determines maximum box size. We subdivide boxes until all their dimensions are less than this length.
        private float constant = 16;

        // Use this for initialization, before relativistic object CombineParent() starts.
        void Awake()
        {
            //Grab the meshfilter, and if it's not null, keep going
            BoxCollider[] origBoxColliders = GetComponents<BoxCollider>();

            //Prepare a new list of colliders for our split collider
            change = new List<BoxCollider>();

            //Store a copy of our original mesh
            original = origBoxColliders;
            for (int i = 0; i < original.Length; ++i)
            {
                original[i].enabled = false;
                //Split this collider until all of its dimensions have length less than our chosen value
                Subdivide(original[i], change);
            }
        }

        private void OnEnable()
        {
            TakeQueueNumber();
        }

        void OnDisable()
        {
            ReturnQueueNumber();
        }

        private void TakeQueueNumber()
        {
            StaticVoxelSystem svt = StaticVoxelSystem.Instance;
            if (svt != null)
            {
                Vector3[] worldPositions = new Vector3[origPositions.Length];
                for (int i = 0; i < origPositions.Length; ++i)
                {
                    worldPositions[i] = transform.TransformPoint(origPositions[i]);
                }
                staticQueueNumber = svt.TakeQueueNumber(change, worldPositions);
            }
        }

        private void ReturnQueueNumber()
        {
            StaticVoxelSystem svt = StaticVoxelSystem.Instance;
            if (svt != null)
            {
                svt.ReturnQueueNumber(staticQueueNumber);
            }
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

            Vector3 xEdge = origSize.x / 2 * new Vector3(1, 0, 0);
            float xFar = (origCenter + xEdge).x;
            float xNear = (origCenter - xEdge).x;
            float xExtent = (origTransform.TransformPoint(xEdge + origCenter) - origWorldCenter).magnitude;
            Vector3 yEdge = origSize.y / 2 * new Vector3(0, 1, 0);
            float yFar = (origCenter + yEdge).y;
            float yNear = (origCenter - yEdge).y;
            float yExtent = (origTransform.TransformPoint(yEdge + origCenter) - origWorldCenter).magnitude;
            Vector3 zEdge = origSize.z / 2 * new Vector3(0, 0, 1);
            float zFar = (origCenter + zEdge).z;
            float zNear = (origCenter - zEdge).z;
            float zExtent = (origTransform.TransformPoint(zEdge + origCenter) - origWorldCenter).magnitude;

            int xCount = ((int)(2 * xExtent / constant + 1));
            int yCount = ((int)(2 * yExtent / constant + 1));
            int zCount = ((int)(2 * zExtent / constant + 1));

            Vector3 newColliderPos = new Vector3();
            Vector3 newColliderSize = new Vector3(origSize.x / xCount, origSize.y / yCount, origSize.z / zCount);
            if (xCount > 1) newColliderSize.x = newColliderSize.x * (1 + oversizePercent);
            if (yCount > 1) newColliderSize.y = newColliderSize.y * (1 + oversizePercent);
            if (zCount > 1) newColliderSize.z = newColliderSize.z * (1 + oversizePercent);
            for (int i = 0; i < xCount; ++i)
            {
                newColliderPos.x = xNear + ((xFar - xNear) * i / xCount) + newColliderSize.x / 2;
                for (int j = 0; j < yCount; ++j)
                {
                    newColliderPos.y = yNear + ((yFar - yNear) * j / yCount) + newColliderSize.y / 2;
                    for (int k = 0; k < zCount; ++k)
                    {
                        newColliderPos.z = zNear + ((zFar - zNear) * k / zCount) + newColliderSize.z / 2;
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

            return change;
        }

        void OnDestroy()
        {
            for (int i = 0; i < original.Length; ++i)
            {
                original[i].enabled = true;
            }
        }
    }
}