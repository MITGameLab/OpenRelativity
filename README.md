# OpenRelativity

Welcome to OpenRelativity! This project is a spin-off from the MIT Game Lab's game A Slower Speed of Light.

OpenRelativity is a set of tools for simulating the effects of traveling near the speed of light in the Unity3D game engine. We are producing this simple engine in the hopes that more developers or educators can use our toolset to produce more simulations or explorations of travel near the speed of light.

Within this readme is a quick overview of each piece of code that we have put into this project, along with a very quick description of how to use the toolset to make sure your project follows the rules of relativity. We hope you can use this toolset to produce something amazing, and if you find bugs or have an improvement to make to the code base, feel free to help turn OpenRelativity into an even better project.

## Getting Started

Just download the project and open the Unity scene. Everything should work out of the box.

### Prerequisities

The only necessary component is Unity3D, available at [their website](https://unity3d.com/get-unity/download).


## OpenRelativity Limitations and Usage

### General Notes on Relativity and the Limitations of our Code:

To most accurately simulate the effects of special relativity using our toolkit, please adhere to the following guidelines:

1. Only the player object may move freely. All other objects must either have a constant velocity originating at infinity and ending at infinity, or else be still.

2. The Player's speed must never reach or exceed the speed of light.

3. Due to constraint one, the other objects in the scene cannot travel in any direction but a straight line. 

4. Because we did not write a new lighting system, all shadows must be permanent. That is, any shadow that is present on an object must be part of its texture and assumed to never change. DO NOT use the built in Unity lighting system with baked shadows unless no objects in the scene will ever disappear. Lighting and shadows are an extremely complicated field of special relativity, and they are not supported at this time. 

### The Scene

We have set up this project with a few basic settings that are meant to make our code base work smoothly. First, under Render Settings, we have the skybox's shader. This shader is slightly different from the one on every other object, and it also determines the basic texture of the sky. We do not work with anything other than solid colors, but if you would like to experiment with a more interesting texture on the skybox, feel free to do so. Next, under the Project Settings/Tags, we have a custom tag of "Playermesh" and a custom layer "Player." The Playermesh tag is used to access the top level player object, which contains the player's mesh and physical information. The Player layer is used to denote which objects should collide with the player. We currently have the Project Settings/Physics collision layers set up so that objects collide only with objects denoted "Player" and not with other objects in the scene. There is one exception to this, in the Receiver object, but that will be covered later. Apart from this, the scene is as it should be normally.

###The Player

The player is already set up with our movement code, game state code, and a quick script that puts the speed of light and player speed on the screen. It is set up as a nested object. The player mesh is the parent object, which has the physical aspects of the player. It has a mesh renderer, a collider, and a rigidbody. Each of these components is necessary for our code base. Any of these attributes can be modified, but if the rigidbody's drag, gravity, or frozen rotation settings are changed, the project will no longer adhere to the rules of special relativity and it may react unpredictably with our code, so please don't change these settings unless you know what you're doing. If you wish to change the player's mesh, that won't cause any problems, but keep in mind that this framework was meant to provide a first-person view of relativity, and anything but a first person view will no longer be physically accurate.

At the second level, we have the Player gameobject, which is where we keep the code. There are three pieces of code on the player character. Movement Scripts takes care of the player's movement, the camera's movement, and the change in the speed of light by user input. Game State keeps track of many important variables for the relativistic effects, and controls pausing and ending the game.

Finally, we have the camera. The camera does not have any scripts on it, but is affected by Movement Scripts and must be attached to the player or else the perspective will be off.

###Other Objects

By a few simple steps, any object can be added to the scene of Open Relativity. These steps mostly consist of adding the following components to your new object.

The object must have a Mesh Filter with a mesh attached to it. Any mesh will do, regardless of complexity.

The object must have a Collider attached to it. Box Colliders work great and are the simplest. Mesh colliders will most likely not work well, and they will definitely not conform to the Lorenz contraction because we do that in the graphics card, not in scripts.

The object must have a Mesh Renderer. As in constraint four, we do not have a lighting system, so it is best if you uncheck "cast shadows" and "receive shadows" in the Mesh Renderer. Any materials that are a part of the Mesh Renderer must use the ColorShift shader. Note that they do not need to use the relativity materials that are in the project, but they do need to have the ColorShift shader (the Relativity/ColorShift option in the Shader drop down menu) in order to function properly.

The object must have a rigidbody with gravity unchecked, 100 mass and infinite drag. 

Finally, the object must have the script "RelativisticObject" attached to it. If the objects VIW (Velocity In World) is set to anything but (0,0,0), it will move constantly while the scene is playing.

With these components added to your new object, they will deform and change color according to the rules of special relativity. That's all it takes!


## Code Overview

### Relativistic Object / Relativistic Parent

Relativistic Object is the base code for all the non-player objects in the scene. Combined with the Relativity shader, it keeps track of relatvistic effects,moves the object if needed, and performs necessary actions for the shader to work. It first forces the object to have a unique material, so that the object's shader uses variables specific to that object, and not across all objects using the relativity shader (which is what would happen if we did nothing). It also keeps track of when the object was created and when it is supposed to disappear, so that the time-distorting effects of special relativity do not accidentally force the object to appear before its start location or after its disappearance. 

Relativistic Parent is much the same as Relativistic Object. However, for complex parented objects with many smaller parts, each with their own materials, it vastly increases the speed of the engine if they are all kept on a single object. This code takes all the meshes of the parent object's children and combines them with its own, attaching the materials and textures necessary to the correct submeshes. There is one important problem: I do not combine the colliders of the child objects, so it is important that the parent object's collider contains within it all of the child objects, or else the player will be able to clip through their meshes as only the parent's collider remains after the combining of all the meshes.
### Firework
Firework is identical to Relativistic Object except that there is also a timer on the object. When it dies, it releases a cloud of particles. This behaviour is mostly to show the possibilites of working with timers. As you lower the speed of light and the fireworks travel closer to the speed of light, you will notice they last longer (as will their particles) due to time dilation.
### Movement Scripts

Movement Scripts is what takes player input for the mouse and keyboard and moves the player accordingly. This code covers player movement, camera movement, and change in the speed of light. The movement follows a formula for relativistic velocity addition, which is included in the comments before the movement code. It is also currently set for free movement in three dimensions. If you wish to constrain the player to a flat ground plane (the X-Z plane, in Unity) then there are a couple lines of code that are marked for easy change. 

### Info Script

This just displays the two text boxes in the upper left hand corner, using info from Game State.

### Game State

Game State is the brain of the Open Relativity code. It stores important variables for relativistic effects and keeps track of changes in the player's status. Relativistic Object and Movement Scripts rely on being able to find Game State and access its information. It also keeps track of the pause state of the game, letting all other objects query game state rather than keeping track of it themselves.

### Object Mesh Density

Object Mesh density takes a constant value (named constant). It searches over the triangles on the mesh of the object that it is attached to. If it finds a triangle with area greater than the constant value, it splits that triangle into four smaller triangles, in a way that still works effectively with the relativistic code. The reason for this code is that the lorenz contraction looks better with a higher concentration of vertices (since it is a vertex shader). Using this will slow down your startup, as the code is (currently) fairly inefficient because we never used it in the game. I hope to make this faster soon.

### Relativity Shader

This shader implements a vertex shader that runs the Lorenz contraction, and a fragment shader that implements the relativistic doppler shift. More detail is available in the comments of the code, in a line-by-line explanation. 

### Skybox Shader
	
This shader implements relativistic doppler shift on the skybox, because the lorenz contraction does not work (and should not really be used) on the very low-vertex skybox.

### Receiver and Receiver2 Script
	
This is the receiver side of the objects that create new moving characters. The receiver has to be given the transform of the sender object so that it knows where to face. Within the receiver object itself is the receiver2 object, whose only purpose is to know when the Moving Person object has entered its collider, and register a time for the Moving Person to delete itself. The receiver script simply takes a sender object's transform and points in that object's direction. The receiver2 script just has a collision modifier on it that causes any objects that collide with it to register a death time.

### Sender Script

The Sender script is the other half of the objects that create moving characters. It takes the name of a prefab in the Resources/Gameobjects folder, time interval, a velocity, and a receiver's transform. On start up it will point the object to look at the receiver, and at every interval specified will create a new Moving Person object. The Moving Person object will then move with a velocity specified in the Sender Script until it hits the Receiver2 object.

##Prefabs

### Receiver
	
The Receiver object is made up of two separate objects. It has the Receiver as the parent object, and the Receiver2 as the child object. The meshes on this prefab can be changed without worry, as can the colliders. Be warned, however, that the collider for the Receiver2 object must be small enough that the Moving Person objects can enter the receiver before they disappear on colliding with the receiver2 object. In order to make the Receiver object work, it must be given a Sender object's transform, and the Sender must have the Receiver's transform. Also, the Receiver2 object and the Moving Person must collide with each other, while the Receiver object should not collide with the Moving Person object. This can be done by editing the layers of the Receiver2 and Receiver object.

### Sender

The Sender object is much simpler than the Receiver object. It just requires the Sender script and the transform of a Receiver. The mesh on this object can also be changed to anything else, as can its collider as there are no restrictions on the size of the collider or the mesh.

If you choose to leave the receiver transform blank and point it to a prefab with the firework script, it will launch self-deleting objects.


### Moving Person
	
This is the object that will be created at a Sender and move towards a Receiver. There is nothing to change on the scripts in the Moving Person object, as everything is specified by the Sender that creates the Moving Person. The mesh and collider on the Moving Person can also be modified without changing the function of the Moving Person.

### Firework Launcher

Essentially a sender object that specifically references the Firework instead of a Moving Person.

### Firework

This object uses the Firework script and is referenced by the Firework Launcher prefab. Its timer and number of launched particles are set to reasonable numbers for the scene, but can be tweaked at will.

### Particle

A simple cube to be used as the "explosion" effect of a Firework.

## Materials

The materials that come provided with Open Relativity are built to work with the relativity shader. If you have an object that you want to create a new material for, you must make sure the material has the following properties. It must use the relativity shader, and if you want it to have IR or UV spectrum wavelengths on it, the IR/UV textures in the material must have a grayscale image.

## Controller Support
OpenRelativity has two separate InputManager.asset files that should be switched out depending on your target platform. The standard one (that is already in the project) is designed to work on Windows with an Xbox 360 controller. The file labeled InputManager - OSXPS3 is built to use a PS3 controller on OSX platforms. Simply rename the file "InputManager.asset" and replace the existing InputManager.asset file in the Project Settings folder to change which configuration you use. Again, these configurations are for both separate platforms and separate controllers.

The controls are as follows:

- Movement: left analog stick
- Camera Movement: right analog stick
- Invert Camera Y Axis: Y/Triangle button
- Toggle Color Effects: B/Circle Button
- Pause/Unpause Game: Start Button
- Change C: Left/Right D-Pad keys

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details

## Acknowledgments

Thank you to Gerd Kortemeyer and the MIT Game Lab for their contribution and instruction on this project.

Thanks to users tyoc213, matthewh806, and sethwoodworth for contributing to the repo! 