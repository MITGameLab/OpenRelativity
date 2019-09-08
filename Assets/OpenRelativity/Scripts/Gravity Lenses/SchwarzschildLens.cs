using OpenRelativity;
using OpenRelativity.ConformalMaps;
using UnityEngine;

public class SchwarzschildLens : GravityLens
{
    public GameState state;
    public Schwarzschild schwarzschild;
    public GravityMirror gravityMirror;

    // Update is called once per frame
    void Update()
    {
        if (gravityMirror != null)
        {
            gravityMirror.ManualUpdate();
        }

        lensPass = null;

        float r = schwarzschild.radius;

        if (r == 0)
        {
            doBlit = false;
            return;
        }

        doBlit = true;

        Vector3 lensUVPos = cam.WorldToViewportPoint(Vector3.zero);
        float playerAngle = Mathf.Deg2Rad * Vector3.Angle(-cam.transform.position, cam.transform.forward);
        float playerDist = cam.transform.position.magnitude;

        float frustumHeight = 2.0f * playerDist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * cam.aspect;

        lensMaterial.SetFloat("_playerDist", playerDist);
        lensMaterial.SetFloat("_playerAngle", playerAngle);
        lensMaterial.SetFloat("_lensRadius", r);
        lensMaterial.SetFloat("_lensUPos", lensUVPos.x);
        lensMaterial.SetFloat("_lensVPos", lensUVPos.y);
        lensMaterial.SetFloat("_frustumWidth", frustumWidth);
        lensMaterial.SetFloat("_frustumHeight", frustumHeight);
    }
}
