using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity {
    public class StaticRelativitySystem : RelativisticBehavior
    {
        public static StaticRelativitySystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                Instance = this;
            }
        }

        public List<Material> staticRelativisticMaterials = new List<Material>();

        private void UpdateShaderParams()
        {
            Vector4 tempPao = -Physics.gravity;
            Vector4 tempVr = -state.PlayerVelocityVector / state.SpeedOfLight;

            //Velocity of object Lorentz transforms are the same for all points in an object,
            // so it saves redundant GPU time to calculate them beforehand.
            Matrix4x4 viwLorentzMatrix = SRelativityUtil.GetLorentzTransformMatrix(Vector3.zero);

            for (int i = 0; i < staticRelativisticMaterials.Count; ++i)
            {
                staticRelativisticMaterials[i].SetVector("_viw", Vector3.zero);
                staticRelativisticMaterials[i].SetVector("_aiw", Vector3.zero);
                staticRelativisticMaterials[i].SetVector("_pao", tempPao);
                staticRelativisticMaterials[i].SetMatrix("_viwLorentzMatrix", viwLorentzMatrix);
                staticRelativisticMaterials[i].SetMatrix("_invViwLorentzMatrix", viwLorentzMatrix.inverse);
                staticRelativisticMaterials[i].SetVector("_vr", tempVr);
                staticRelativisticMaterials[i].SetFloat("_lastUpdateSeconds", Time.time);
            }
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            UpdateShaderParams();
        }
    }
}
