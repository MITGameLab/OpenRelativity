using UnityEngine;
using System.Collections;

public class StandButtonReactor : MonoBehaviour {

    public Vector3 appearPosition;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

    public void ButtonPress()
    {
        transform.position = appearPosition;
    }
}
