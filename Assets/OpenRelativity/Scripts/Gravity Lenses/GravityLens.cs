using System.Collections.Generic;
using UnityEngine;

public class GravityLens : MonoBehaviour
{
    public Camera cam;
    public Material lensMaterial;
    public GravityLens mirrorLens;
    public bool isMirror;
    public bool isSkybox = false;

    protected bool doBlit;
    protected bool wasBlit;
    protected List<RenderTexture> lensPass;

    private void Start()
    {
        doBlit = true;
        wasBlit = false;
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (isSkybox)
        {
            Graphics.Blit(src, dest);
            return;
        }

        if (doBlit)
        {
            wasBlit = true;
            if (mirrorLens)
            {
                if (isMirror)
                {
                    if (mirrorLens.lensPass != null && mirrorLens.lensPass.Count > 0)
                    {
                        lensMaterial.SetTexture("_lensTex", mirrorLens.lensPass[0]);
                        mirrorLens.lensPass.RemoveAt(0);
                        Graphics.Blit(src, dest, lensMaterial);
                    }
                }
                else
                {
                    if (lensPass == null)
                    {
                        lensPass = new List<RenderTexture>();
                    }
                    Graphics.Blit(src, dest, lensMaterial);
                    lensPass.Add(dest);
                }
            }
            else
            {
                Graphics.Blit(src, dest, lensMaterial);
            }
        }
        else
        {
            if (wasBlit && isMirror && mirrorLens)
            {
                wasBlit = false;
                gameObject.SetActive(false);
            } else
            {
                wasBlit = false;
                Graphics.Blit(src, dest);
            }
        }
    }
}
