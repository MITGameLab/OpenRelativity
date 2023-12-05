using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.Objects {
    public class ChargedObject : RelativisticBehavior
    {
        // The object can be electrically charged
        public float electricCharge = 0.03f;
        // Magnetic monopole quasi-particles were recently discovered on the surface of hematite, so we might as well:
        public float magneticMonopoleCharge = 0.03f;
        // Outside of a radius, the effects of electric charge can be ignored.
        public float electromagnetismRange = 32.0f;
        // Do we distribute net charge over collding ChargeObjects?
        public bool combineChargeOnCollide = true;
        // Maximum force that can be applied.
        private float maxForce = 1e32f;

        void AddElectromagneticForces()
        {
            RelativisticObject myRO = GetComponent<RelativisticObject>();
            if (!myRO) {
                return;
            }

            Vector3 electricField = Vector3.zero;
            Vector3 magneticField = Vector3.zero;
            Collider[] otherColliders = Physics.OverlapSphere(myRO.piw, electromagnetismRange);
            foreach (Collider otherCollider in otherColliders)
            {
                ChargedObject otherCO = otherCollider.GetComponent<ChargedObject>();
                if (!otherCO) {
                    continue;
                }

                RelativisticObject otherRO = otherCollider.GetComponent<RelativisticObject>();
                if (!otherRO) {
                    continue;
                }

                Vector3 displacement = myRO.piw - otherRO.piw;
                Vector3 rUnit = displacement.normalized;
                Vector3 bVec = (otherRO.viw.magnitude <= SRelativityUtil.FLT_EPSILON) ? Vector3.zero : Vector3.Cross(otherRO.viw, rUnit);

                // Electric field:
                electricField += (float)(otherCO.electricCharge / (4 * Mathf.PI * state.vacuumPermittivity * displacement.sqrMagnitude)) * rUnit;
                // Magnetic monopole contribution:
                electricField += (float)((state.vacuumPermittivity *  otherCO.magneticMonopoleCharge) / (4 * Mathf.PI * displacement.sqrMagnitude)) * bVec;

                // Magnetic field:
                magneticField += (float)((state.vacuumPermeability *  otherCO.electricCharge) / (4 * Mathf.PI * displacement.sqrMagnitude)) * bVec;
                // Magnetic monopole contribution:
                magneticField += (float)(otherCO.magneticMonopoleCharge / (4 * Mathf.PI * state.vacuumPermeability * displacement.sqrMagnitude)) * rUnit;
            }

            bool isNonzeroViw = myRO.viw.magnitude > SRelativityUtil.FLT_EPSILON;

            Vector3 electricForce = electricCharge * electricField;
            if (isNonzeroViw) {
                electricForce += magneticMonopoleCharge * Vector3.Cross(myRO.viw, electricField);
            }
            float forceMag = electricForce.magnitude;
            if (float.IsNaN(forceMag) || (forceMag > maxForce)) {
                forceMag = maxForce;
            }
            myRO.AddForce(forceMag * electricForce.normalized);

            Vector3 magneticForce = magneticMonopoleCharge * magneticField;
            if (isNonzeroViw) {
                magneticForce += electricCharge * Vector3.Cross(myRO.viw, magneticField);
            }
            forceMag = magneticForce.magnitude;
            if (float.IsNaN(forceMag) || (forceMag > maxForce)) {
                forceMag = maxForce;
            }
            myRO.AddForce(forceMag * magneticForce.normalized);
        }

        float GetSurfaceArea() {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter)
            {
                return meshFilter.sharedMesh.SurfaceArea();
            }

            Vector3 lwh = transform.localScale;
            return 2 * (lwh.x * lwh.y + lwh.x * lwh.z + lwh.y * lwh.z);
        }

        // Start is called before the first frame update
        void Start()
        {
        
        }

        // FixedUpdate is called once before physics update
        void FixedUpdate()
        {
            AddElectromagneticForces();
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (!combineChargeOnCollide) {
                return;
            }

            ChargedObject otherCO = collision.collider.GetComponent<ChargedObject>();
            if (!otherCO || !otherCO.combineChargeOnCollide) {
                return;
            }
            
            float mySurfaceArea = GetSurfaceArea();
            float otherSurfaceArea = otherCO.GetSurfaceArea();
            float totSurfaceArea = mySurfaceArea + otherSurfaceArea;

            float netCharge = (electricCharge + otherCO.electricCharge);
            electricCharge = netCharge * mySurfaceArea / totSurfaceArea;
            otherCO.electricCharge = netCharge * otherSurfaceArea / totSurfaceArea;

            netCharge = (magneticMonopoleCharge + otherCO.magneticMonopoleCharge);
            magneticMonopoleCharge = netCharge * mySurfaceArea / totSurfaceArea;
            otherCO.magneticMonopoleCharge = netCharge * otherSurfaceArea / totSurfaceArea;
        }
    }
}
