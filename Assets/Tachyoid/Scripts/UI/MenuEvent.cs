using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Tachyoid
{
    public abstract class MenuEvent : MonoBehaviour
    {
        public void Activate()
        {
            OnActivate();
        }

        public void Deactivate()
        {
            OnDeactivate();
        }

        public abstract void OnActivate();
        public abstract void OnDeactivate();
    }
}