using UnityEngine;
using System.Collections;

public class SenderScript : MonoBehaviour {

    //Store our partner's transform
    public Transform receiverTransform;
    //how long between character creation?
    public int launchTimer;
    //how much time has passed
    private float launchCounter;

	public string prefabName = "Moving Person";
    //What's their speed?
    public float viwMax = 3;
	
    void Start()
    {
		//We let you set a public variable to determine the number of seconds between each launch of an object.
		//If that variable is unset, we make sure to put it at 3 here.
        if (launchTimer <= 0)
        {
            launchTimer = 3;
        }
		//Point to the associated receiver.
		if (receiverTransform != null) {
			this.transform.LookAt (receiverTransform);
		}
		//Take the minimum of the chosen viwMax, and the Game State's chosen Max Speed
        viwMax = Mathf.Min(viwMax,(float)GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>().MaxSpeed);
    }

    // Update is called once per frame
    void Update()
    {	//If we're not paused, increment the timer
        if (!GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>().MovementFrozen)
        {
            launchCounter += Time.deltaTime;
        }
		//If it has been at least LaunchTimer seconds since we last fired an object
        if (launchCounter >= launchTimer)
        {
			//Reset the counter
            launchCounter = 0;
			//And instantiate a new object
            LaunchObject();
        }
    }
    void LaunchObject()
    {	
		//Instantiate a new Object (You can find this object in the GameObjects folder, it's a prefab.
        GameObject launchedObject = (GameObject)Instantiate(Resources.Load("GameObjects/"+prefabName, typeof(GameObject)), transform.position, this.transform.rotation);
        //Translate it to our center, and put it so that it's just touching the ground
		launchedObject.transform.Translate((new Vector3(0, launchedObject.GetComponent<MeshFilter>().mesh.bounds.extents.y, 0) ));
		//Make it a child of our transform.
        launchedObject.transform.parent = transform;
		//Determine if it has a Relativistic Object, Firework, or multiple RO's to set VIW on.
		RelativisticObject ro = launchedObject.GetComponent<RelativisticObject>();
		RelativisticObject [] ros = launchedObject.GetComponentsInChildren<RelativisticObject>();

		if (ro != null)
		{
			ro.viw = viwMax * this.transform.forward;
			//And let the object know when it was created, so that it knows when not to be seen by the player
			ro.SetStartTime();
		}
		else if(ros.Length>0)
		{
			for(int i=0;i<ros.Length;i++)
			{
				ros[i].viw = viwMax * this.transform.forward;
				//And let the object know when it was created, so that it knows when not to be seen by the player
				ros[i].SetStartTime();
			}
		}
		else if(launchedObject.GetComponent<Firework>()!=null)
		{
			launchedObject.GetComponent<Firework>().viw = viwMax * transform.forward;
			//And let the object know when it was created, so that it knows when not to be seen by the player
			launchedObject.GetComponent<Firework>().SetStartTime();
		}
    }
}
