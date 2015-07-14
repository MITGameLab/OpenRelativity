using System;
using System.Collections;
using System.Text; 
using UnityEngine;

//using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Audio;
//using Microsoft.Xna.Framework.Content;
//using Microsoft.Xna.Framework.GamerServices;
//using Microsoft.Xna.Framework.Graphics;
//using Microsoft.Xna.Framework.Input;
//using Microsoft.Xna.Framework.Net;
//using Microsoft.Xna.Framework.Storage;


public class GameState : MonoBehaviour
{
    #region Member Variables

    private System.IO.TextWriter stateStream;

	//Player orientation
    private Quaternion orientation = Quaternion.identity;
	//world rotation so that we can transform between the two
    private Matrix4x4 worldRotation;
	//Player's velocity in vector format
    private Vector3 playerVelocityVector;

    //grab the player's transform so that we can use it
    public Transform playerTransform;
	//If we've paused the game
    private bool movementFrozen = false;
	//player Velocity as a scalar magnitude
    public double playerVelocity;
	//time passed since last frame in the world frame
    private double deltaTimeWorld;
	//time passed since last frame in the player frame
    private double deltaTimePlayer;
	//total time passed in the player frame
    private double totalTimePlayer;
	//total time passed in the world frame
    private double totalTimeWorld;
	//speed of light
    private double c = 200;
	//Speed of light that is affected by the Unity editor
    public double totalC = 200;
	//max speed the player can achieve (starting value accessible from Unity Editor)
    public double maxPlayerSpeed;
	//max speed, for game use, not accessible from Unity Editor
    private double maxSpeed;
	//speed of light squared, kept for easy access in many calculations
    private double cSqrd;
	
	//Use this to determine the state of the color shader. If it's True, all you'll see is the lorenz transform.
	private bool shaderOff = false;
	
	//Did we hit the menu key?
    public bool menuKeyDown = false;
	//Did we hit the shader key?
    public bool shaderKeyDown = false;


	//This is a value that gets used in many calculations, so we calculate it each frame
    private double sqrtOneMinusVSquaredCWDividedByCSquared;
	//Player rotation and change in rotation since last frame
    public Vector3 playerRotation = new Vector3(0, 0, 0);
    public Vector3 deltaRotation = new Vector3(0, 0, 0);
    public double pctOfSpdUsing = 0; // Percent of velocity you are using



    #endregion

    #region Properties
	
    public float finalMaxSpeed = .99f;
    public bool MovementFrozen { get { return movementFrozen; } set { movementFrozen = value; } }

    public Matrix4x4 WorldRotation { get { return worldRotation; } }
    public Quaternion Orientation { get { return orientation; } }
    public Vector3 PlayerVelocityVector { get { return playerVelocityVector; } set { playerVelocityVector = value; } }

    public double PctOfSpdUsing { get { return pctOfSpdUsing; } set { pctOfSpdUsing = value; } }
    public double PlayerVelocity { get { return playerVelocity; } }
    public double SqrtOneMinusVSquaredCWDividedByCSquared { get { return sqrtOneMinusVSquaredCWDividedByCSquared; } }
    public double DeltaTimeWorld { get { return deltaTimeWorld; } }
    public double DeltaTimePlayer { get { return deltaTimePlayer; } }
    public double TotalTimePlayer { get { return totalTimePlayer; } }
    public double TotalTimeWorld { get { return totalTimeWorld; } }
    public double SpeedOfLight { get { return c; } set { c = value; cSqrd = value * value; } }
    public double SpeedOfLightSqrd { get { return cSqrd; } }

    public bool keyHit = false;
    public double MaxSpeed { get { return maxSpeed; } set { maxSpeed = value; } }
    #endregion

    #region consts
    private const float ORB_SPEED_INC = 0.05f;
    private const float ORB_DECEL_RATE = 0.6f;
    private const float ORB_SPEED_DUR = 2f;
    private const float MAX_SPEED = 20.00f;
    public  const float NORM_PERCENT_SPEED = .99f;
    public const int splitDistance = 21000;
    #endregion


    public void Awake()
    {
        //Initialize the player's speed to zero
        playerVelocityVector = Vector3.zero;
        playerVelocity = 0;
		//Set our constants
		MaxSpeed = MAX_SPEED;
        pctOfSpdUsing = NORM_PERCENT_SPEED;
		
		c = totalC;
        cSqrd = c*c;
		//And ensure that the game starts/
        movementFrozen = false;
        
    }
    public void reset()
    {
        //Reset everything not level-based
        playerRotation.x = 0;
        playerRotation.y = 0;
        playerRotation.z = 0;
        pctOfSpdUsing = 0;
    }
   	//Call this function to pause and unpause the game
    public void ChangeState()
    {
        if (movementFrozen)
        {
			//When we unpause, lock the cursor and hide it so that it doesn't get in the way
            movementFrozen = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else 
        {
			//When we pause, set our velocity to zero, show the cursor and unlock it.
            GameObject.FindGameObjectWithTag(Tags.playerMesh).GetComponent<Rigidbody>().velocity = Vector3.zero;
			movementFrozen = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
       
    }
	//We set this in late update because of timing issues with collisions
    public void LateUpdate()
    {
        //Set the pause code in here so that our other objects can access it.
        if (Input.GetAxis("Menu Key") > 0 && !menuKeyDown)
        {
            menuKeyDown = true;
            ChangeState();
        }
		//set up our buttonUp function
        else if (!(Input.GetAxis("Menu Key") > 0))
        {
            menuKeyDown = false;
        }
		//Set our button code for the shader on/off button
		if (Input.GetAxis("Shader") > 0 && !shaderKeyDown)
        {
			if(shaderOff)
	            shaderOff = false;
			else
				shaderOff = true;
			
			shaderKeyDown = true;
        }
		//set up our buttonUp function
        else if (!(Input.GetAxis("Shader") > 0))
        {
            shaderKeyDown = false;
        }
		
        //If we're not paused, update everything
        if (!movementFrozen)
        {
			//Put our player position into the shader so that it can read it.
            Shader.SetGlobalVector("_playerOffset", new Vector4(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z, 0));
           
		
            //if we reached max speed, forward or backwards, keep at max speed

            if (playerVelocityVector.magnitude >= (float)MaxSpeed-.01f)
            {
                playerVelocityVector = playerVelocityVector.normalized * ((float)MaxSpeed-.01f);
            }
            
            //update our player velocity
            playerVelocity = playerVelocityVector.magnitude;
            

            //During colorshift on/off, during the last level we don't want to have the funky
            //colors changing so they can apperciate the other effects
            if (shaderOff)
            {
                Shader.SetGlobalFloat("_colorShift", (float)0.0);
            }
            else
            {
                Shader.SetGlobalFloat("_colorShift", (float)1);
            }

            //Send v/c to shader
            Shader.SetGlobalVector("_vpc", new Vector4(-playerVelocityVector.x, -playerVelocityVector.y, -playerVelocityVector.z, 0) / (float)c);
            //Send world time to shader
            Shader.SetGlobalFloat("_wrldTime", (float)TotalTimeWorld);
           
             /******************************
             * PART TWO OF ALGORITHM
             * THE NEXT 4 LINES OF CODE FIND
             * THE TIME PASSED IN WORLD FRAME
             * ****************************/
            //find this constant
            sqrtOneMinusVSquaredCWDividedByCSquared = (double)Math.Sqrt(1 - (playerVelocity * playerVelocity) / cSqrd);
			
			//Set by Unity, time since last update
			deltaTimePlayer = (double)Time.deltaTime; 
            //Get the total time passed of the player and world for display purposes
            if (keyHit)
            {
                totalTimePlayer += deltaTimePlayer;
                if (!double.IsNaN(sqrtOneMinusVSquaredCWDividedByCSquared))
                {
                    //Get the delta time passed for the world, changed by relativistic effects
                    deltaTimeWorld = deltaTimePlayer / sqrtOneMinusVSquaredCWDividedByCSquared;
                    //and get the total time passed in the world
                    totalTimeWorld += deltaTimeWorld;
                }
            }
        
            //Set our rigidbody's velocity
            if (!double.IsNaN(deltaTimePlayer) && !double.IsNaN(sqrtOneMinusVSquaredCWDividedByCSquared))
            {
                GameObject.FindGameObjectWithTag(Tags.playerMesh).GetComponent<Rigidbody>().velocity = -1*(playerVelocityVector / (float)sqrtOneMinusVSquaredCWDividedByCSquared);
            }
			//But if either of those two constants is null due to a zero error, that means our velocity is zero anyways.
            else
            {
				GameObject.FindGameObjectWithTag(Tags.playerMesh).GetComponent<Rigidbody>().velocity = Vector3.zero;
            }
                /*****************************
             * PART 3 OF ALGORITHM
             * FIND THE ROTATION MATRIX
             * AND CHANGE THE PLAYERS VELOCITY
             * BY THIS ROTATION MATRIX
             * ***************************/


            //Find the turn angle
            //Steering constant angular velocity in the player frame
            //Rotate around the y-axis

            orientation = Quaternion.AngleAxis(playerRotation.y, Vector3.up) * Quaternion.AngleAxis(playerRotation.x, Vector3.right);
            Quaternion WorldOrientation = Quaternion.Inverse(orientation);
            Normalize(orientation);
            worldRotation = CreateFromQuaternion(WorldOrientation);

            //Add up our rotation so that we know where the character (NOT CAMERA) should be facing 
            playerRotation += deltaRotation;

        
    }
	}
    #region Matrix/Quat math
    //They are functions that XNA had but Unity doesn't, so I had to make them myself

    //This function takes in a quaternion and creates a rotation matrix from it
    public Matrix4x4 CreateFromQuaternion(Quaternion q)
    {
        double w = q.w;
        double x = q.x;
        double y = q.y;
        double z = q.z;

		double wSqrd = w * w;
		double xSqrd = x * x;
		double ySqrd = y * y;
		double zSqrd = z * z;

        Matrix4x4 matrix;
		matrix.m00 = (float)(wSqrd + xSqrd - ySqrd - zSqrd);
        matrix.m01 = (float)(2 * x * y - 2 * w * z);
        matrix.m02 = (float)(2 * x * z + 2 * w * y);
        matrix.m03 = (float)0;
        matrix.m10 = (float)(2 * x * y + 2 * w * z);
		matrix.m11 = (float)(wSqrd - xSqrd + ySqrd - zSqrd);
        matrix.m12 = (float)(2 * y * z + 2 * w * x);
        matrix.m13 = (float)0;
        matrix.m20 = (float)(2 * x * z - 2 * w * y);
        matrix.m21 = (float)(2 * y * z - 2 * w * x);
		matrix.m22 = (float)(wSqrd - xSqrd - ySqrd + zSqrd);
        matrix.m23 = 0;
        matrix.m30 = 0;
        matrix.m31 = 0;
        matrix.m32 = 0;
        matrix.m33 = 1;
        return matrix;
    }

    //Normalize the given quaternion so that its magnitude is one.
    public Quaternion Normalize(Quaternion quat)
    {
        Quaternion q = quat;

		double magnitudeSqr = q.w * q.w + q.x * q.x + q.y * q.y + q.z * q.z;
        if (magnitudeSqr != 1)
        {
			double magnitude = (double)Math.Sqrt(magnitudeSqr);
			q.w = (float)((double)q.w / magnitude);
            q.x = (float)((double)q.x / magnitude);
            q.y = (float)((double)q.y / magnitude);
            q.z = (float)((double)q.z / magnitude);
        }
        return q;
    }
    //Transform the matrix by a vector3
    public Vector3 TransformNormal(Vector3 normal, Matrix4x4 matrix)
    {
        Vector3 final;
        final.x = matrix.m00 * normal.x + matrix.m10 * normal.y + matrix.m20 * normal.z;

        final.y = matrix.m02 * normal.x + matrix.m11 * normal.y + matrix.m21 * normal.z;

        final.z = matrix.m03 * normal.x + matrix.m12 * normal.y + matrix.m22 * normal.z;
        return final;
    }
    #endregion
}

