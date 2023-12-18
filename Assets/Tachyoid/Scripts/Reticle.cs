using UnityEngine;
using System.Collections;
using OpenRelativity;
using OpenRelativity.Objects;

namespace Tachyoid
{
    public class Reticle : MonoBehaviour
    {

        public Texture yesTexture;
        public Texture noTexture;
        public Renderer myRenderer;
        public GameObject myGameObject;
        private Material myMaterial;

        // Use this for initialization
        void Start()
        {
            Hide();
            myMaterial = myRenderer.materials[0];
        }

        // Update is called once per frame
        void Update()
        {
        }

        public void ShowYes(Vector3 globalPos, Vector3 forward)
        {
            myMaterial.SetTexture("_MainTex", yesTexture);
            transform.position = globalPos;
            transform.forward = forward;
            myRenderer.enabled = true;
            myGameObject.SetActive(true);
        }

        public void ShowNo(Vector3 globalPos, Vector3 forward)
        {
            myMaterial.SetTexture("_MainTex", noTexture);
            transform.position = globalPos;
            transform.forward = forward;
            myRenderer.enabled = true;
            myGameObject.SetActive(true);
        }

        public void Hide()
        {
            myRenderer.enabled = false;
            myGameObject.SetActive(false);
        }
    }
}
