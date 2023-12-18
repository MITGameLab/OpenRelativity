using UnityEngine;
using System.Collections;
using OpenRelativity;
using Tachyoid;

namespace Tachyoid.Objects
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
            if (onOrOff)
            {
                transform.GetComponent<Renderer>().enabled = true;
            }
            else
            {
                transform.GetComponent<Renderer>().enabled = false;
            }
        }
    }
}
