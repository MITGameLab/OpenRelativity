# OpenRelativity

Welcome to OpenRelativity! This project is a spin-off from the MIT Game Lab's game A Slower Speed of Light.

OpenRelativity is a set of tools for simulating the effects of traveling near the speed of light in the Unity3D game engine. We are producing this simple engine in the hopes that more developers or educators can use our toolset to produce more simulations or explorations of travel near the speed of light.

Within this readme is a quick overview of each piece of code that we have put into this project, along with a very quick description of how to use the toolset to make sure your project follows the rules of relativity. We hope you can use this toolset to produce something amazing, and if you find bugs or have an improvement to make to the code base, feel free to help turn OpenRelativity into an even better project.

## This is an "Unofficial OpenRelativity v2.0"

Daniel Strano is the lead developer of the high performance vm6502q/qrack quantum computer simulation framework, (alongside co-author Benn Bollay). Before Dan was familiar with GitHub and Agile methodology best practices, he started working on this expanded fork of OpenRelativity. Like Qrack, this fork of OpenRelativity has been a passionate "hobbyist" physics simulation project. The original MIT licensing terms of OpenRelativity are maintained for this fork, with many thanks to the MIT Game Lab!

In overview, this fork adds relativistic handling for **acceleration** to the original OpenRelativity project, (aided in large part by "inverting" the original OpenRelavity Lorentz-transforming shader). This is commonly regarded as the domain of the theory of general relativity, as opposed to special relativity. However, this fork does **not** directly simulate the "Einstein field equations," defining the true theory of general relativity. Rather, in essence, we simulate special relativity with relativistic acceleration on a general but fixed set of background curvatures, represented by the API of an abstract `ConformalMap` class. This allows us to simulate the local consequences of "Einstein equivalence principle" for small, approximately rigid mechanical bodies, including general Unity PhysX features, like collision, friction, and drag, despite the lack of a full simulation of the governing Einstein field equations of general relativity.
 
Since Dan is also lead developer of the Qrack quantum computer simulation framework, he has wrapped that framework as a plugin for OpenRelativity, here, so that "conformal" quantum mechanics can immediately be employed in our relativity simulations. **"Conformal" quantum computer programs** can be written with instructions "pinned" to the relativistic notions of distance and time of OpenRelativity, by overloading the `RealTimeQasmProgram` abstract class. (When we say "conformal," we mean basically that special relativity or "Minkowski" simulation, as well as quantum mechanics or quantum computation simulation, "conforms" to the backgroud curvature of a `ConformalMap` as if these mechanics happened in a locally approximately "flat" space that is "bent" over larger scales to conform the boundary of the underlying space-time, in accordance with "Einstein equivalence principle." Also, the "`RealTimeQasmProgram`" API is not actually proper "QASM," but it is a similar quantum assembly or intermediate representation.)

## Getting Started

Just download the project and open the Unity scene. Everything should work out of the box.

### Prerequisities

The only necessary component is Unity3D, available at [their website](https://unity3d.com/get-unity/download).

## OpenRelativity Limitations and Usage

### General Notes on Relativity and the Limitations of our Code:

To most accurately simulate the effects of special and general relativity using our toolkit, please adhere to the following guidelines:

1. Unlike the base OpenRelativity fork, velocities of all RelativisticObject instances may be freely varied. (We attempt "full" support for Unity PhysX features, by treating and superficially transforming PhysX' underlying mechanics as those of "rapidities" in a "local tangent tangent space.")

2. The Player's speed must never reach or exceed the speed of light.
 
3. We have largely re-written the lighting system, assuming that baked lightmaps remain static and stationary in Unity "world frame" coordinates. A notable caveat is that **real-time** reflections are not relativistically accurate, though **baked** reflections are. Another caveat is that full inverse square attenuation (typically corresponding to an attenuation parameter value of "1") is capable of simulating the bending of light by gravity in **real-time** lighting, but **baked** lighting in Unity does respect inverse square attentuation or expose the option to vary attenuation strength, so **baked lighting isn't relativistically accurate in a background gravitational field**.

### The Scenes (with "new physics")

Scenes include variants of setup to demonstrate the new features of this fork, including approximations of black hole metrics. Some of these scenes (like `monopole`, and "evaporation" effects in the black hole scenes) include original hypothetical "new physics" for gravity according to a hypothesis developed by Daniel Strano; these features are clearly indicated as non-canonical and turned off by default, unless the scene specifically intends to demonstrate them. These effects have not passed peer review, but you're free to leave them disabled or include them for purposes of a video game. We have tried to be very careful to make these "new physics" possible to entirely disable without affecting "canonical" relativity simulation.

### The Player

The player is already set up with our movement code and game state code. It is set up as a nested object. The player mesh is the parent object, which has the physical aspects of the player. It has a mesh renderer, a collider, and a rigidbody. Each of these components is necessary for our code base. If you wish to change the player's mesh, that won't cause any problems, but keep in mind that this framework was meant to provide a first-person view of relativity, and anything but a first person view will no longer be physically accurate.

At the second level, we have the Player gameobject, which is where we keep the code. There are three pieces of code on the player character. Movement Scripts takes care of the player's movement, the camera's movement, and the change in the speed of light by user input. Game State keeps track of many important variables for the relativistic effects, and controls pausing and ending the game.

Finally, we have the camera. The camera does not have any scripts on it, but is affected by Movement Scripts and must be attached to the player or else the perspective will be off.

### Other Objects

By a few simple steps, any object can be added to the scene of Open Relativity. These steps mostly consist of adding the following components to your new object.

The object must have a Mesh Filter with a mesh attached to it. Any mesh will do, regardless of complexity.

The object must have a Collider attached to it. Box Colliders work great and are the simplest, with the "Is Nonrelativistic Shader" option enabled on the RelativisticObject, for accuracy acceptable in most video games. Mesh colliders are costly, because they require a separate shader pass to Lorentz transform, per frame.

The object must have a Mesh Renderer. This renderer should typically use our "standard relativity shader." If this renderer uses a non-relativistic shader, you must enable the "Is Nonrelativistic Shader" option. We also provide a "ColorOnly" variant of the "standard relativity shader," and this "ColorOnly" shader assumes that the RelativisticObject will move its transform position as if in "optical world space," i.e. that the "Is Nonrelativistic Shader" option is enabled. You can use your own shader, so long as you enable the "Is Nonrelativistic Shader" option, but you lose some level of relativistic accuracy, including Doppler shift.

The object can have a rigidbody.

Finally, the object must have the script "RelativisticObject" attached to it. If the objects VIW (Velocity In World) is set to anything but (0,0,0), it will move constantly while the scene is playing. ("Peculiar velocity," similarly, is the velocity of the object as measured in coordinates that "comove" with the underlying curvature of the space-time `ConformalMap`, which is no different from "VIW" if there is no `ConformalMap`.)

With these components added to your new object, they will deform and change color according to the rules of special relativity. That's all it takes!


## Code Overview

### Relativistic Object / Relativistic Parent

Relativistic Object is the base code for all the non-player objects in the scene. Combined with the Relativity shader, it keeps track of relatvistic effects,moves the object if needed, and performs necessary actions for the shader to work. It first forces the object to have a unique material, so that the object's shader uses variables specific to that object, and not across all objects using the relativity shader (which is what would happen if we did nothing).

Relativistic Parent is much the same as Relativistic Object. However, for complex parented objects with many smaller parts, each with their own materials, it vastly increases the speed of the engine if they are all kept on a single object. This code takes all the meshes of the parent object's children and combines them with its own, attaching the materials and textures necessary to the correct submeshes. There is one important problem: I do not combine the colliders of the child objects, so it is important that the parent object's collider contains within it all of the child objects, or else the player will be able to clip through their meshes as only the parent's collider remains after the combining of all the meshes.

### Movement Scripts

Movement Scripts is what takes player input for the mouse and keyboard and moves the player accordingly. This code covers player movement, camera movement, and change in the speed of light. The movement follows a formula for relativistic velocity addition, which is included in the comments before the movement code. It is also currently set for free movement in three dimensions. If you wish to constrain the player to a flat ground plane (the X-Z plane, in Unity) then there are a couple lines of code that are marked for easy change. 

### Game State

Game State is the brain of the Open Relativity code, ("singleton"). It stores important variables for relativistic effects and keeps track of changes in the player's status. Relativistic Object and Movement Scripts rely on being able to find Game State and access its information. It also keeps track of the pause state of the game, letting all other objects query game state rather than keeping track of it themselves.

### Object Mesh Density

Object Mesh density takes a constant value (named constant). It searches over the triangles on the mesh of the object that it is attached to. If it finds a triangle with an area greater than the constant value, it splits that triangle into four smaller triangles, in a way that still works effectively with the relativistic code. The reason for this code is that the Lorentz contraction looks better with a higher concentration of vertices (since it is a vertex shader). Using this will slow down your startup, as the code is (currently) fairly inefficient because we never used it in the game. I hope to make this faster soon.

### Relativity Shaders

These shader implement vertex shaders that runs the Lorentz contraction, and fragment shaders that implements the relativistic Doppler shift. More detail is available in the comments of the code, in a line-by-line explanation. 

### Skybox Shader
	
This shader implements relativistic Doppler shift on the skybox, because the Lorentz contraction does not work (and should not really be used) on the very low-vertex skybox.

### Black hole shaders

These are shaders similar to the skybox shader. They approximate a black hole's bending of light, for objects far behind the black hole from the player's perspective. They do not work for objects nearby the player, and they assume that the black hole is far enough away from the player and/or small enough to be approximated as point-like. They **must** project the black hole position at Unity world coordinates origin, only, since moving the coordinate origin is very difficult in mathematical relativistic descriptions of black holes.

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

This project is licensed under the MIT License - see the [LICENSE](MITLicense.md) file for details

## Acknowledgments

Thank you to Gerd Kortemeyer and the MIT Game Lab for their contribution and instruction on this project.

Thanks to users Barnacle Nightshade, Tiago Morais Morgado, tyoc213, matthewh806, and sethwoodworth for contributing to the repo! 
