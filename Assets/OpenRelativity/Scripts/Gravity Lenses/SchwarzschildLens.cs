﻿using OpenRelativity;
using OpenRelativity.ConformalMaps;
using UnityEngine;

public class SchwarzschildLens : GravityLens
{
    public GameState state;
    public Schwarzschild schwarzschild;
    public GravityMirror gravityMirror;
    public Material interiorMaterial;
    private Material origLensMaterial;

    private void Start()
    {
        origLensMaterial = lensMaterial;

        if (isSkybox)
        {
            RenderSettings.skybox = interiorMaterial;
        }
    }

    // Update is called once per frame
    void Update()
    {
        float r = schwarzschild.schwarzschildRadius;
        float jFrac, spinColatitude, spinTilt;
        if (schwarzschild is Kerr) {
            Kerr kerr = schwarzschild as Kerr;
            jFrac = (kerr.spinMomentum / r) * (state.planckLength / state.planckAngularMomentum);
            spinColatitude = Mathf.Deg2Rad * Vector3.Angle(-cam.transform.position, kerr.spinAxis);
            spinTilt = Mathf.Deg2Rad * Vector3.Angle(cam.transform.up, kerr.spinAxis);
        }
        else
        {
            jFrac = 0.0f;
            spinColatitude = 0.0f;
            spinTilt = 0.0f;
        }

        if (r == 0)
        {
            doBlit = false;
            return;
        }

        doBlit = true;

        if (!schwarzschild.isExterior)
        {
            lensMaterial = interiorMaterial;
            lensMaterial.SetFloat("_lensRadius", r);
            lensMaterial.SetFloat("_lensSpinFrac", jFrac);
            lensMaterial.SetFloat("_lensSpinColat", spinColatitude);
            lensMaterial.SetFloat("_lensSpinTilt", spinTilt);
            lensMaterial.SetFloat("_playerDist", state.SpeedOfLight * state.TotalTimeWorld);

            return;
        }

        lensMaterial = origLensMaterial;

        if (gravityMirror != null)
        {
            gravityMirror.ManualUpdate();
        }

        lensPass = null;

        float playerAngle = Mathf.Deg2Rad * Vector3.Angle(-cam.transform.position, cam.transform.forward);
        float playerDist = cam.transform.position.magnitude;

        Vector3 lensUVPos = cam.WorldToViewportPoint(Vector3.zero);
        float frustumHeight = 2.0f * playerDist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * cam.aspect;

        lensMaterial.SetFloat("_playerDist", playerDist);
        lensMaterial.SetFloat("_playerAngle", playerAngle);
        lensMaterial.SetFloat("_lensRadius", r);
        lensMaterial.SetFloat("_lensSpinFrac", jFrac);
        lensMaterial.SetFloat("_lensSpinColat", spinColatitude);
        lensMaterial.SetFloat("_lensSpinTilt", spinTilt);
        lensMaterial.SetFloat("_lensUPos", lensUVPos.x);
        lensMaterial.SetFloat("_lensVPos", lensUVPos.y);
        lensMaterial.SetFloat("_frustumWidth", frustumWidth);
        lensMaterial.SetFloat("_frustumHeight", frustumHeight);
        lensMaterial.SetFloat("_isExterior", schwarzschild.isExterior ? 1.0f : 0.0f);
    }
}
