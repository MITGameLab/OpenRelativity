using UnityEngine;
using System.Collections;
///Written by user tyoc213
public class InfoScript : MonoBehaviour {
	GameObject infoPanel;
	//Gamestate reference for quick access
	GameState state;
	// Use this for initialization
	void Start () {
		GameObject canvas = GameObject.Find ("Canvas");
		if (canvas != null) {
			GameObject go = Resources.Load<GameObject>("GameObjects/InfoPanel");
			if(go != null){
				infoPanel = Instantiate(go);
				infoPanel.GetComponent<Transform>().SetParent(canvas.GetComponent<Transform>(), false);
			}
		}
		state = GetComponent<GameState>();
	}

	//just print out a bunch of information onto the screen.
	void Update(){
		string msg = "Current C: " + state.SpeedOfLight
			+ "\nIncrease/Decrease with N/M"
			+ "\n\nCurrent Speed: " + state.PlayerVelocity
			+ "\nMove with with WASD";
		UnityEngine.UI.Text text = infoPanel.GetComponentInChildren<UnityEngine.UI.Text> ();
		text.text = msg;
	}

}
