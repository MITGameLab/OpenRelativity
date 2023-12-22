using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenRelativity.Objects;

public class AudioSenderTest : MonoBehaviour
{
    private RelativisticObject myRO;

    // Start is called before the first frame update
    void Start()
    {
        myRO = GetComponent<RelativisticObject>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        myRO.viw = (1 - 0.01f * Time.fixedDeltaTime) * myRO.viw;
    }
}
