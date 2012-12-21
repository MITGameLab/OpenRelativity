using UnityEngine;
using System.Collections;

public class ReceiverScript : MonoBehaviour {

    //Store our partner's transform
    public Transform senderTransform;
    // Use this for initialization
    void Start()
    {
		//Look at our paired sender.
        this.transform.LookAt(senderTransform);
    }
	
	// Update is called once per frame
    void LateUpdate()
    {
        
    }
}
