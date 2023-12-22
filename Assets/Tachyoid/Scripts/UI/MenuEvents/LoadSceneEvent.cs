using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tachyoid
{
    public class LoadSceneEvent : MenuEvent
    {
        public string sceneToLoad;

        override public void OnActivate()
        {
            SceneManager.LoadScene(sceneToLoad);
        }

        public override void OnDeactivate()
        {

        }
    }
}
