using UnityEngine;
using System.Collections;

public class BulletScript : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}
	void onColliderEnter(Collision collision) {
	
		print ("FUCKFUCKFUCK");
		if(collider.gameObject.layer == 10)
		{	
			//If an object crashes into us, set their death time so they know when to disappear.
        	collider.gameObject.GetComponent<RelativisticObject>().SetDeathTime();
		}
		print("HI");
		//regardless of what we hit, kill ourselves.
		this.gameObject.GetComponent<RelativisticObject>().SetDeathTime();
	}
	
}
