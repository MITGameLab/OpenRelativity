using System.Collections.Generic;
using UnityEngine;
using Google.XR.Cardboard;
using Tachyoid;

namespace Tachyoid
{
    public class Selector : MonoBehaviour
    {
        public FTLPlayerController playerCtrl;
        public Transform camTransform;
        public AudioSource activateSound;
        public List<VRMenuItem> menuItems = new List<VRMenuItem>();
        public VRMenuItem selectedItem { get; private set; }

        private const float maxAngle = 60f;
        private const float inputWait = 0.2f;

        private float inputTimer;

        private void OnEnable()
        {
            inputTimer = inputWait;
        }

        // Update is called once per frame
        void Update()
        {
            Vector3 testFwd = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
            if (testFwd == Vector3.zero) testFwd = Vector3.ProjectOnPlane(camTransform.up, Vector3.up).normalized;
            if (testFwd == Vector3.zero) testFwd = Vector3.forward;
            transform.forward = testFwd;

            float minAngle = 180f;
            float angle;
            VRMenuItem oldSelectedItem = selectedItem;
            selectedItem = null;
            for (int i = 0; i < menuItems.Count; i++)
            {
                angle = Vector3.Angle(transform.forward, menuItems[i].transform.forward);
                if (angle < minAngle)
                {
                    minAngle = angle;
                    selectedItem = menuItems[i];
                }
            }

            if (minAngle > maxAngle)
            {
                selectedItem = null;
            }

            if (oldSelectedItem == null || selectedItem == null || !oldSelectedItem.Equals(selectedItem))
            {
                if (oldSelectedItem != null) oldSelectedItem.OnDeselected();
                if (selectedItem != null) selectedItem.OnSelected();
            }

            if (inputTimer <= 0.0)
            {
                bool isPressed = (Input.GetMouseButton(0) || Api.IsTriggerPressed);

                if (isPressed && !playerCtrl.isMenuUIOn)
                {
                    activateSound.Play();
                    selectedItem.OnActivate();
                }
            }
            else
            {
                inputTimer -= Time.deltaTime;
            }
        }
    }
}
