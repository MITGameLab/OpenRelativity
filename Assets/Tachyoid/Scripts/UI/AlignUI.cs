using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tachyoid
{
    public class AlignUI : MonoBehaviour
    {
        public Transform camTransform;

        private void Update()
        {
            transform.localEulerAngles = new Vector3(90f, camTransform.eulerAngles.y, 0);
        }
    }
}
