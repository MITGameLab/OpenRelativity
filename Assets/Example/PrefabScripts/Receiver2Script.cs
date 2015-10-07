using UnityEngine;
using System.Collections;

public class Receiver2Script : MonoBehaviour {

	// Use this for initialization
	void Start () {

	}
	
	// Update is called once per frame
    void OnTriggerEnter(Collider collider)
    {
		//If an object crashes into us, set their death time so they know when to disappear.
        collider.gameObject.GetComponent<RelativisticObject>().SetDeathTime();
    }
}
