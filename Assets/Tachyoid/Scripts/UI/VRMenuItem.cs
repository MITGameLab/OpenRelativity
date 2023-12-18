using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Tachyoid
{
    public class VRMenuItem : MonoBehaviour
    {

        public Material selectedMaterial;
        public MenuEvent onActivate;

        private Renderer myRenderer;
        private Material origMaterial;

        private AudioSource selectAudio;

        private const float soundDelay = 0.2f;
        private float soundTimer;

        // Use this for initialization
        void Start()
        {
            myRenderer = GetComponent<Renderer>();
            origMaterial = myRenderer.material;

            selectAudio = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            soundTimer = soundDelay;
        }

        private void Update()
        {
            if (soundTimer > 0)
            {
                soundTimer -= Time.deltaTime;
            }
        }

        public void OnSelected()
        {
            if (soundTimer <= 0)
            {
                selectAudio.Play();
            }
            myRenderer.material = selectedMaterial;
        }

        public void OnDeselected()
        {
            myRenderer.material = origMaterial;
        }

        public void OnActivate()
        {
            onActivate.Activate();
        }
    }
}
