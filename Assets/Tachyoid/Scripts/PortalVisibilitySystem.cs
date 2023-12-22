using System.Collections.Generic;
using Tachyoid.TimeReversal.Generic;
using UnityEngine;

namespace Tachyoid {
    public class PortalVisibilitySystem : MonoBehaviour, ITimeReversibleObject {

        public Objects.DoorController doorController;
        public List<GameObject> room1 = new List<GameObject>();
        public List<GameObject> room2 = new List<GameObject>();
        public List<GameObject> common = new List<GameObject>();
        public ResetBounds resetBounds;
        public Vector3 room1PlayerResetPosition;
        public Vector3 room2PlayerResetPosition;

        private BoxCollider myCollider;

        private bool isInRoom1 = true;
        private bool isBetweenRooms;
        private bool wasBetweenRooms;

        private bool isFirstFrame;

        // Use this for initialization
        void Start() {
            myCollider = GetComponent<BoxCollider>();
            isInRoom1 = true;
            isBetweenRooms = false;
            wasBetweenRooms = false;
            isFirstFrame = true;
        }

        private void OnDisable()
        {
            if (isFirstFrame)
            {
                for (int i = 0; i < room2.Count; i++)
                {
                    room2[i].SetActive(false);
                }
                isFirstFrame = false;
            }
        }

        // Update is called once per frame
        void Update() {
            if (isFirstFrame)
            {
                for (int i = 0; i < room2.Count; i++)
                {
                    room2[i].SetActive(false);
                }
                isFirstFrame = false;
            }

            isBetweenRooms = doorController.isOpen;

            if (isBetweenRooms)
            {
                if (!wasBetweenRooms)
                {
                    if (isInRoom1) {
                        for (int i = 0; i < room2.Count; i++)
                        {
                            room2[i].SetActive(true);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < room1.Count; i++)
                        {
                            room1[i].SetActive(true);
                        }
                    }
                }
                wasBetweenRooms = true;
            }
            else
            {
                if (wasBetweenRooms)
                {
                    if (isInRoom1)
                    {
                        for (int i = 0; i < room2.Count; i++)
                        {
                            room2[i].SetActive(false);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < room1.Count; i++)
                        {
                            room1[i].SetActive(false);
                        }
                    }
                }
                wasBetweenRooms = false;
            }

            if (isInRoom1)
            {
                resetBounds.resetPos = room1PlayerResetPosition;
            }
            else
            {
                resetBounds.resetPos = room2PlayerResetPosition;
            }
        }

        private void OnTriggerEnter(Collider other)
        {

        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.tag == "Player")
            {
                Vector3 disp = (transform.InverseTransformPoint(other.transform.position) - myCollider.center).normalized;
                isInRoom1 = (Vector3.Dot(disp, Vector3.forward) < 0.0f);
            }
        }

        public void UpdateTimeTravelPrediction()
        {
            //No need
        }

        public void UndoTimeTravelPrediction()
        {
            //No need
        }

        public void ReverseTime()
        {
            FTLPlayerController playerCtrl = FindObjectOfType<FTLPlayerController>();
            if (playerCtrl.pvsInstanceID.HasValue && playerCtrl.pvsInstanceID.Value == gameObject.GetInstanceID())
            {
                isInRoom1 = !isInRoom1;
            }
        }

    }
}
