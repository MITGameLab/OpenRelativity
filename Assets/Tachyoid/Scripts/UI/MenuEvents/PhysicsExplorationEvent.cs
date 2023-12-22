using UnityEngine;

namespace Tachyoid
{
    public class PhysicsExplorationEvent : MenuEvent
    {
        public GameObject parentMenu;
        public GameObject myMenu;

        override public void OnActivate()
        {
            myMenu.transform.localRotation = parentMenu.transform.localRotation;
            parentMenu.SetActive(false);
            myMenu.SetActive(true);
        }

        public override void OnDeactivate()
        {
            parentMenu.SetActive(true);
            myMenu.SetActive(false);
        }
    }
}
