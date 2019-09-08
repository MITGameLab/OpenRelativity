using UnityEngine;

public class GravityMirror : MonoBehaviour
{
    public Camera playerCam;
    // Start is called before the first frame update
    void Start()
    {
        transform.position = -playerCam.transform.position;
        transform.LookAt(playerCam.transform.position);
        transform.rotation *= playerCam.transform.rotation;
    }

    // Update is called once per frame
    public void ManualUpdate()
    {
        transform.position = -playerCam.transform.position;
        transform.LookAt(playerCam.transform.position);
        transform.rotation *= playerCam.transform.rotation;
    }
}
