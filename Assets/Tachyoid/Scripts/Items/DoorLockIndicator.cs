using UnityEngine;
using System.Collections;
using Tachyoid;

namespace Tachyoid.Objects
{
    public class DoorLockIndicator : MonoBehaviour
    {
        public GameObject unlockedGO;
        public GameObject lockedGO;

        public void SetLockedState(bool locked)
        {
            unlockedGO.SetActive(!locked);
            lockedGO.SetActive(locked);
        }
    }
}
