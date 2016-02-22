using UnityEngine;
using System.Collections;

public class InfoScript : MonoBehaviour {
	
    //Gamestate reference for quick access
    GameState state;
	// Use this for initialization
	void Start () {
	
        state = GetComponent<GameState>();
	}
	
	//just print out a bunch of information onto the screen.
	void OnGUI(){
		//What's our speed of light?
		GUI.Box (new Rect (0,0,200,100), "Current C: " + state.SpeedOfLight);
		GUI.Label(new Rect(20,50,200,50), "Increase/Decrease with N/M");
		//What's our velocity?
		GUI.Box (new Rect (0,100,200,100), "Current Speed: " + state.PlayerVelocity);
		GUI.Label(new Rect(20,150,200,50), "Move with with WASD");
		
		
		
	}
	
}
