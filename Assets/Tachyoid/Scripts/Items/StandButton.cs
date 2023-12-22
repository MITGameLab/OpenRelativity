using UnityEngine;
using System.Collections;

public class StandButton : MonoBehaviour
{
    public StandButtonReactor reactor;
    public AudioSource sound;

    // Use this for initialization
    void Start()
    {
        sound = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.tag.Contains("Player"))
        {
            if (sound != null)
            {
                sound.Play();
            }
            reactor.ButtonPress();
        }
    }
}
