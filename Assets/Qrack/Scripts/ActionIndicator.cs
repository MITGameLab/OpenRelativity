using UnityEngine;

namespace Qrack
{
    public class ActionIndicator : MonoBehaviour
    {
        // Use this for initialization
        void Awake()
        {
            SetState(false);
        }

        public void SetState(bool onOrOff)
        {
            Renderer myRenderer = transform.GetComponent<Renderer>();

            if (myRenderer == null)
            {
                return;
            }

            myRenderer.enabled = onOrOff;
        }
    }
}
