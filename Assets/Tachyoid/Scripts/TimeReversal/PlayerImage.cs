using UnityEngine;
using System.Collections;
using OpenRelativity;
using OpenRelativity.Objects;
using Tachyoid;
using Tachyoid.Objects;

namespace Tachyoid {
	public class PlayerImage : MonoBehaviour {

        public Transform head;
        public Animator myAnimator;
		public Graspable itemImageHeld { get; set; }

        public RelativisticObject myRO { get; set; }
        
        void Awake() {
            head.localRotation = Quaternion.identity;
            myRO = GetComponent<RelativisticObject>();
		}

		public void SetImageEnabled(bool enable) {
            if (gameObject.activeSelf != enable)
            {
                myRO.ResetLocalTime();
            }

            gameObject.SetActive(enable);
			if (itemImageHeld != null) {
				itemImageHeld.enabled = enable;
				itemImageHeld.GetComponent<Renderer>().enabled = enable;
			}
		}

        void OnEnable()
        {
            SetImageEnabled(true);
        }

        void OnDisable()
        {
            SetImageEnabled(false);
        }

        public void SetAnimation(Action action)
        {
            if (myAnimator != null)
            {
                if (action == Action.isRunning)
                {
                    myAnimator.SetInteger("Speed", 1);
                    myAnimator.SetBool("Jumping", false);
                }
                else if (action == Action.isJumping)
                {
                    myAnimator.SetBool("Jumping", true);
                }
                else
                {
                    myAnimator.SetInteger("Speed", 0);
                    myAnimator.SetBool("Jumping", false);
                }
            }
        }
    }
}