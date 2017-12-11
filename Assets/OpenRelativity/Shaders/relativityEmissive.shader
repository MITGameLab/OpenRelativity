//NOTE: For correct relativistic behavior, all light sources must be static
// with respect to world coordinates! General constant velocity lights are more complicated,
// and lights that accelerate might not be at all feasible.

Shader "Relativity/Lit/Inertial/EmissiveColorShift" {
	Properties{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo", 2D) = "white" {}
		_UVTex("UV",2D) = "" {} //UV texture
		_IRTex("IR",2D) = "" {} //IR texture
		_Cutoff("Base Alpha cutoff", Range(0,.9)) = 0.1
		_viw("viw", Vector) = (0,0,0,0) //Vector that represents object's velocity in synchronous frame
		_aiw("aiw", Vector) = (0,0,0,0) //Vector that represents object's acceleration in world coordinates
		_EmissionMap("Emission Map", 2D) = "black" {}
		[HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
		_EmissionMultiplier("Emission Multiplier", Range(0,10)) = 1
	}
		CGINCLUDE

#pragma exclude_renderers xbox360
#pragma glsl
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"
#include "UnityStandardMeta.cginc"

		//Color shift variables, used to make guassians for XYZ curves
#define xla 0.39952807612909519
#define xlb 444.63156780935032
#define xlc 20.095464678736523

#define xha 1.1305579611401821
#define xhb 593.23109262398259
#define xhc 34.446036241271742

#define ya 1.0098874822455657
#define yb 556.03724875218927
#define yc 46.184868454550838

#define za 2.0648400466720593
#define zb 448.45126344558236
#define zc 22.357297606503543

		//Used to determine where to center UV/IR curves
#define IR_RANGE 400
#define IR_START 700
#define UV_RANGE 380
#define UV_START 0

		//Prevent NaN and Inf
#define divByZeroCutoff 1e-8f

	struct appdata {
		float4 vertex : POSITION;
		float4 texcoord : TEXCOORD0; // main uses 1st uv
		float4 texcoord1 : TEXCOORD1; // lightmap uses 2nd uv
		float4 normal : NORMAL;
	};

	//This is the data sent from the vertex shader to the fragment shader
	struct v2f
	{
		float4 pos : POSITION; //internal, used for display
		float4 pos2 : TEXCOORD0; //Position in world, relative to player position in world
		float2 uv1 : TEXCOORD1; //Used to specify what part of the texture to grab in the fragment shader(not relativity specific, general shader variable)
		float svc : TEXCOORD2; //sqrt( 1 - (v-c)^2), calculated in vertex shader to save operations in fragment. It's a term used often in lorenz and doppler shift calculations, so we need to keep it cached to save computing
		float4 vr : TEXCOORD3; //Relative velocity of object vpc - viw
		float2 uv2 : TEXCOORD4; //Lightmap TEXCOORD
		float2 uv3 : TEXCOORD5; //EmisionMap TEXCOORD
		float4 diff : COLOR0; //Diffuse lighting color in world rest frame
		float4 normal : TEXCOORD6; //normal in world
#if defined(SHADOWS_SCREEN) || defined(SHADOWS_CUBE) || (defined(SHADOWS_DEPTH) && defined(SPOT))
		float4 ambient : TEXCOORD7;
		SHADOW_COORDS(8)
#endif
	};

	float4x4 _MixedMetric; //The mixed index metric ("conformal map") at piw
	//For the time being, we can only approximate by the value of the metric at the center of the object.
	//Ideally, we'd have a differently numerical metric value for each vertex or fragment.

	//Lorentz transforms from player to world and from object to world are the same for all points in an object,
	// so it saves redundant GPU time to calculate them beforehand.
	float4x4 _vpcLorentzMatrix;
	float4x4 _viwLorentzMatrix;

	//uniform float4 _Color;
	//Variables that we use to access texture data
	//sampler2D _MainTex;
	//uniform float4 _MainTex_ST;
	sampler2D _IRTex;
	uniform float4 _IRTex_ST;
	sampler2D _UVTex;
	uniform float4 _UVTex_ST;
	//sampler2D _EmissionMap;
	uniform float4 _EmissionMap_ST;
	//float _EmissionColor;
	float _EmissionMultiplier;
	sampler2D _CameraDepthTexture;

	//float4 _piw = float4(0, 0, 0, 0); //position of object in world
	float4 _viw = float4(0, 0, 0, 0); //velocity of object in synchronous coordinates
	float4 _aviw = float4(0, 0, 0, 0); //scaled angular velocity
	float4 _vpc = float4(0, 0, 0, 0); //velocity of player
	float4 _pap = float4(0, 0, 0, 0); //acceleration of player
	float4 _avp = float4(0, 0, 0, 0); //angular velocity of player
	float4 _playerOffset = float4(0, 0, 0, 0); //player position in world
	float _spdOfLight = 100; //current speed of light
	float _colorShift = 1; //actually a boolean, should use color effects or not ( doppler + spotlight). 

	float xyr = 1; // xy ratio
	float xs = 1; // x scale

	uniform float4 _MainTex_TexelSize;
	uniform float4 _CameraDepthTexture_ST;

	//Per vertex operations
	v2f vert(appdata v)
	{
		v2f o;

		o.uv1.xy = (v.texcoord + _MainTex_ST.zw) * _MainTex_ST.xy; //get the UV coordinate for the current vertex, will be passed to fragment shader
		o.uv2 = float2(0, 0);
#ifdef LIGHTMAP_ON
		o.uv2 = v.texcoord1 * unity_LightmapST.xy + unity_LightmapST.zw;
#endif
		o.uv3.xy = (v.texcoord + _EmissionMap_ST.zw) * _EmissionMap_ST.xy;

		//You need this otherwise the screen flips and weird stuff happens
#ifdef SHADER_API_D3D9
		if (_MainTex_TexelSize.y < 0)
			o.uv1.y = 1.0 - o.uv1.y;
#endif 

		float4 tempPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0f));
		o.pos = float4(tempPos.xyz / tempPos.w - _playerOffset.xyz, 0);

		o.uv1.xy = (v.texcoord + _MainTex_ST.zw) * _MainTex_ST.xy; //get the UV coordinate for the current vertex, will be passed to fragment shader

		float speed = length(_vpc.xyz);
		float spdOfLightSqrd = _spdOfLight * _spdOfLight;

		//vw + vp/(1+vw*vp/c^2)
		float vuDot = dot(_vpc.xyz, _viw.xyz); //Get player velocity dotted with velocity of the object.
		float4 vr;
		//IF our speed is zero, this parallel velocity component will be NaN, so we have a check here just to be safe
		if (speed > divByZeroCutoff)
		{
			float3 uparra = (vuDot / (speed*speed)) * _vpc.xyz; //Get the parallel component of the object's velocity
																//Get the perpendicular component of our velocity, just by subtraction
			float3 uperp = _viw.xyz - uparra.xyz;
			//relative velocity calculation
			vr = float4((_vpc.xyz - uparra.xyz - (sqrt(1 - speed*speed))*uperp.xyz) / (1 + vuDot), 0);
		}
		//If our speed is nearly zero, it could lead to infinities.
		else
		{
			//relative velocity calculation
			vr = float4(-_viw.xyz, 0);
		}

		//set our relative velocity
		o.vr = vr;
		vr *= -1;
		//relative speed
		float speedr = sqrt(dot(vr.xyz, vr.xyz));
		o.svc = sqrt(1 - speedr * speedr); // To decrease number of operations in fragment shader, we're storing this value

										   //riw = location in world, for reference
		float4 riw = float4(o.pos.xyz, 0); //Position that will be used in the output

										   //Boost to rest frame of player:
		float4x4 vpcLorentzMatrix = _vpcLorentzMatrix;
		float4 riwForMetric = mul(vpcLorentzMatrix, riw);

		//Find metric based on player acceleration and rest frame:
		float3 angFac = cross(_avp.xyz, riwForMetric.xyz) / _spdOfLight;
		float linFac = dot(_pap.xyz, riwForMetric.xyz) / spdOfLightSqrd;
		linFac = ((1 + linFac) * (1 + linFac) - dot(angFac, angFac)) * spdOfLightSqrd;
		angFac *= -2 * _spdOfLight;

		float4x4 metric = {
			-1, 0, 0, angFac.x,
			0, -1, 0, angFac.y,
			0, 0, -1, angFac.z,
			angFac.x, angFac.y, angFac.z, linFac
		};

		//Lorentz boost back to world frame;
		float4 transComp = vpcLorentzMatrix._m30_m31_m32_m33;
		transComp.w = -(transComp.w);
		vpcLorentzMatrix._m30_m31_m32_m33 = -transComp;
		vpcLorentzMatrix._m03_m13_m23_m33 = -transComp;
		metric = mul(transpose(vpcLorentzMatrix), mul(metric, vpcLorentzMatrix));

		//Apply conformal map:
		metric = mul(_MixedMetric, metric);

		//We'll also Lorentz transform the vectors:
		float4x4 viwLorentzMatrix = _viwLorentzMatrix;

		//Remember that relativity is time-translation invariant.
		//The above metric gives the numerically correct result if the time coordinate of riw is zero,
		//(at least if the "conformal factor" or "mixed [indices] metric" is the identity).
		//We are free to translate our position in time such that this is the case.

		//Apply Lorentz transform;
		//metric = mul(transpose(viwLorentzMatrix), mul(metric, viwLorentzMatrix));
		float4 riwTransformed = mul(viwLorentzMatrix, riw);
		//Translate in time:
		float tisw = riwTransformed.w;
		riwForMetric.w = 0;
		riw = mul(vpcLorentzMatrix, riwForMetric);
		riwTransformed = mul(viwLorentzMatrix, riw);
		riwTransformed.w = 0;

		//(When we "dot" four-vectors, always do it with the metric at that point in space-time, like we do so here.)
		float riwDotRiw = -dot(riwTransformed, mul(metric, riwTransformed));

		float sqrtArg = riwDotRiw / spdOfLightSqrd;
		float t2 = 0;
		if (sqrtArg > 0)
		{
			t2 = -sqrt(sqrtArg);
		}
		tisw += t2;
		riwTransformed.w = tisw;
		//Inverse Lorentz transform the position:
		transComp = viwLorentzMatrix._m30_m31_m32_m33;
		transComp.w = -(transComp.w);
		viwLorentzMatrix._m30_m31_m32_m33 = -transComp;
		viwLorentzMatrix._m03_m13_m23_m33 = -transComp;
		riw = mul(viwLorentzMatrix, riwTransformed);
		tisw = riw.w;
		riw = float4(riw.xyz + tisw * _spdOfLight * _viw.xyz, 0);

		if (speed > divByZeroCutoff) {
			//Apply Lorentz transform
			// float newz =(riw.z + state.PlayerVelocity * tisw) / state.SqrtOneMinusVSquaredCWDividedByCSquared;
			//I had to break it up into steps, unity was getting order of operations wrong.	
			float newz = speed * _spdOfLight * tisw;
			float3 vpcUnit = _vpc.xyz / speed;
			newz = (dot(riw.xyz, vpcUnit) + newz) / (float)sqrt(1 - (speed * speed));
			riw += (newz - dot(riw.xyz, vpcUnit)) * float4(vpcUnit, 0);
		}

		riw += float4(_playerOffset.xyz, 0);

		//Transform the vertex back into local space for the mesh to use
		tempPos = mul(unity_WorldToObject, float4(riw.xyz, 1.0f));
		o.pos = float4(tempPos.xyz / tempPos.w, 0);

		o.pos2 = float4(riw.xyz - _playerOffset.xyz, 0);

		o.pos = UnityObjectToClipPos(o.pos);

		o.normal = float4(UnityObjectToWorldNormal(v.normal),0);

		//#if defined(FORWARD_BASE_PASS) || defined(DEFERRED_PASS)
#if defined(VERTEXLIGHT_ON)
		// dot product between normal and light direction for
		// standard diffuse (Lambert) lighting
		float nl = dot(o.normal.xyz, _WorldSpaceLightPos0.xyz);
		nl = max(0, nl);
		// factor in the light color
		o.diff = nl * _LightColor0;
		// add ambient light
#if defined(SHADOWS_SCREEN) || defined(SHADOWS_CUBE) || (defined(SHADOWS_DEPTH) && defined(SPOT))
		o.ambient = float4(max(0, ShadeSH9(half4(o.normal))), 0);
#else
		o.diff.rgb += max(0, ShadeSH9(half4(o.normal)));
#endif

		float4 lightPosition;
		float3 vertexToLightSource, lightDirection, diffuseReflection;
		float squaredDistance;
		float attenuation;
		for (int index = 0; index < 4; index++)
		{
			lightPosition = float4(unity_4LightPosX0[index],
				unity_4LightPosY0[index],
				unity_4LightPosZ0[index], 1.0);

			vertexToLightSource =
				lightPosition.xyz - o.pos2.xyz;
			squaredDistance =
				dot(vertexToLightSource, vertexToLightSource);
			if (dot(float4(0, 0, 1, 0), unity_SpotDirection[index]) != 1) // directional light?
			{
				attenuation = 1.0; // no attenuation
				lightDirection =
					normalize(unity_SpotDirection[index]);
			}
			else {
				attenuation = 1.0 / (1.0 +
					unity_4LightAtten0[index] * squaredDistance);
				lightDirection = normalize(vertexToLightSource);
			}
			diffuseReflection = attenuation
				* unity_LightColor[index].rgb * _Color.rgb
				* max(0.0, dot(o.normal.xyz, lightDirection));

			o.diff.rgb += diffuseReflection;
		}
#elif defined(LIGHTMAP_ON)
		//half3 lms = DecodeLightmap(UNITY_SAMPLE_TEX2DARRAY_LOD(unity_Lightmap, o.uv2, 200));
		//o.diff = float4((float3)lms, 0);
		o.diff = 0;
#else 
		// dot product between normal and light direction for
		// standard diffuse (Lambert) lighting
		float nl = dot(o.normal.xyz, _WorldSpaceLightPos0.xyz);
		nl = max(0, nl);
		// factor in the light color
		o.diff = nl * _LightColor0;
		// add ambient light
#if defined(SHADOWS_SCREEN) || defined(SHADOWS_CUBE) || (defined(SHADOWS_DEPTH) && defined(SPOT))
		o.ambient = float4(max(0, ShadeSH9(half4(o.normal))), 0);
#else
		o.diff.rgb += max(0, ShadeSH9(half4(o.normal)));
#endif
#endif
//#else
//		o.diff = 0;
//#endif

#if defined(SHADOWS_SCREEN) || defined(SHADOWS_CUBE) || (defined(SHADOWS_DEPTH) && defined(SPOT))
		TRANSFER_SHADOW(o)
#endif

		return o;
	}

	//Color functions, there's no check for division by 0 which may cause issues on
	//some graphics cards.
	float3 RGBToXYZC(float3 rgb)
	{
		float3 xyz;
		xyz.x = dot(float3(0.13514, 0.120432, 0.057128), rgb);
		xyz.y = dot(float3(0.0668999, 0.232706, 0.0293946), rgb);
		xyz.z = dot(float3(0.0, 0.0000218959, 0.358278), rgb);
		return xyz;
	}
	float3 XYZToRGBC(float3 xyz)
	{
		float3 rgb;
		rgb.x = dot(float3(9.94845, -5.1485, -1.16389), xyz);
		rgb.y = dot(float3(-2.86007, 5.77745, -0.0179627), xyz);
		rgb.z = dot(float3(0.000174791, -0.000353084, 2.79113), xyz);
		return rgb;
	}
	float3 weightFromXYZCurves(float3 xyz)
	{
		float3 returnVal;
		returnVal.x = dot(float3(0.0735806, -0.0380793, -0.00860837), xyz);
		returnVal.y = dot(float3(-0.0665378, 0.134408, -0.000417865), xyz);
		returnVal.z = dot(float3(0.00000299624, -0.00000605249, 0.0484424), xyz);
		return returnVal;
	}

	float getXFromCurve(float3 param, float shift)
	{
		//Use constant memory, or let the compiler optimize constants, where we can get away with it:
		const float sqrt2Pi = sqrt(2 * 3.14159265358979323f);

		//Re-use memory to save per-vertex operations:
		float bottom2 = param.z * shift;
		bottom2 *= bottom2;

		float top1 = param.x * xla * exp(-((((param.y * shift) - xlb) * ((param.y * shift) - xlb))
			/ (2 * (bottom2 + (xlc * xlc))))) * sqrt2Pi;
		float bottom1 = sqrt(1 / bottom2 + 1 / (xlc * xlc));

		float top2 = param.x * xha * exp(-((((param.y * shift) - xhb) * ((param.y * shift) - xhb))
			/ (2 * (bottom2 + (xhc * xhc))))) * sqrt2Pi;
		bottom2 = sqrt(1 / bottom2 + 1 / (xhc * xhc));

		return (top1 / bottom1) + (top2 / bottom2);
	}
	float getYFromCurve(float3 param, float shift)
	{
		//Use constant memory, or let the compiler optimize constants, where we can get away with it:
		const float sqrt2Pi = sqrt(2 * 3.14159265358979323f);

		//Re-use memory to save per-vertex operations:
		float bottom = param.z * shift;
		bottom *= bottom;

		float top = param.x * ya * exp(-((((param.y * shift) - yb) * ((param.y * shift) - yb))
			/ (2 * (bottom + yc * yc)))) * sqrt2Pi;
		bottom = sqrt(1 / bottom + 1 / (yc * yc));

		return top / bottom;
	}

	float getZFromCurve(float3 param, float shift)
	{
		//Use constant memory, or let the compiler optimize constants, where we can get away with it:
		const float sqrt2Pi = sqrt(2 * 3.14159265358979323f);

		//Re-use memory to save per-vertex operations:
		float bottom = param.z * shift;
		bottom *= bottom;

		float top = param.x * za * exp(-((((param.y * shift) - zb) * ((param.y * shift) - zb))
			/ (2 * (bottom + zc * zc))))* sqrt2Pi;
		bottom = sqrt(1 / bottom + 1 / (zc * zc));

		return top / bottom;
	}

	float3 constrainRGB(float r, float g, float b)
	{
		float w;

		w = (0 < r) ? 0 : r;
		w = (w < g) ? w : g;
		w = (w < b) ? w : b;
		w = -w;

		if (w > 0) {
			r += w;  g += w; b += w;
		}
		w = r;
		w = (w < g) ? g : w;
		w = (w < b) ? b : w;

		if (w > 1)
		{
			r /= w;
			g /= w;
			b /= w;
		}
		float3 rgb;
		rgb.x = r;
		rgb.y = g;
		rgb.z = b;
		return rgb;

	};

	//Per pixel shader, does color modifications
	float4 frag(v2f i) : COLOR
	{
		//Used to maintian a square scale ( adjust for screen aspect ratio )
		float3 x1y1z1 = i.pos2 * (float3)(2 * xs, 2 * xs / xyr, 1);

		// ( 1 - (v/c)cos(theta) ) / sqrt ( 1 - (v/c)^2 )
		float shift = (1 - dot(x1y1z1, i.vr.xyz) / sqrt(dot(x1y1z1, x1y1z1))) / i.svc;
		if (_colorShift == 0)
		{
			shift = 1.0f;
		}

		float UV = tex2D(_UVTex, i.uv1).r;
		float IR = tex2D(_IRTex, i.uv1).r;

		float3 xyz = RGBToXYZC((tex2D(_EmissionMap, i.uv3) * _EmissionMultiplier) * _EmissionColor);
		float3 weights = weightFromXYZCurves(xyz);
		float3 rParam, gParam, bParam, UVParam, IRParam;
		rParam.x = weights.x; rParam.y = (float)615; rParam.z = (float)8;
		gParam.x = weights.y; gParam.y = (float)550; gParam.z = (float)4;
		bParam.x = weights.z; bParam.y = (float)463; bParam.z = (float)5;
		UVParam.x = 0.02; UVParam.y = UV_START + UV_RANGE*UV; UVParam.z = (float)5;
		IRParam.x = 0.02; IRParam.y = IR_START + IR_RANGE*IR; IRParam.z = (float)5;
		xyz.x = (getXFromCurve(rParam, shift) + getXFromCurve(gParam, shift) + getXFromCurve(bParam, shift) + getXFromCurve(IRParam, shift) + getXFromCurve(UVParam, shift));
		xyz.y = (getYFromCurve(rParam, shift) + getYFromCurve(gParam, shift) + getYFromCurve(bParam, shift) + getYFromCurve(IRParam, shift) + getYFromCurve(UVParam, shift));
		xyz.z = (getZFromCurve(rParam, shift) + getZFromCurve(gParam, shift) + getZFromCurve(bParam, shift) + getZFromCurve(IRParam, shift) + getZFromCurve(UVParam, shift));
		float3 rgbEmission = XYZToRGBC(xyz);

		//The Doppler factor is squared for (diffuse or specular) reflected light.
		//The light color is given in the world frame. That is, the Doppler-shifted "photons" in the world frame
		//are indistinguishable from photons of the same color emitted from a source at rest with respect to the world frame.
		//Assume the albedo is an intrinsic reflectance property. The reflection spectrum should not be frame dependent. 

		//Get initial color 

		float4 data = tex2D(_MainTex, i.uv1).rgba;

		//Apply lighting in world frame:
		float3 rgb = data.xyz;

		//Color shift due to doppler, go from RGB -> XYZ, shift, then back to RGB.
		xyz = RGBToXYZC(rgb);
		weights = weightFromXYZCurves(xyz);
		rParam.x = weights.x; rParam.y = (float)615; rParam.z = (float)8;
		gParam.x = weights.y; gParam.y = (float)550; gParam.z = (float)4;
		bParam.x = weights.z; bParam.y = (float)463; bParam.z = (float)5;
		UVParam.x = 0.02; UVParam.y = UV_START + UV_RANGE*UV; UVParam.z = (float)5;
		IRParam.x = 0.02; IRParam.y = IR_START + IR_RANGE*IR; IRParam.z = (float)5;

		xyz.x = (getXFromCurve(rParam, shift) + getXFromCurve(gParam, shift) + getXFromCurve(bParam, shift) + getXFromCurve(IRParam, shift) + getXFromCurve(UVParam, shift));
		xyz.y = (getYFromCurve(rParam, shift) + getYFromCurve(gParam, shift) + getYFromCurve(bParam, shift) + getYFromCurve(IRParam, shift) + getYFromCurve(UVParam, shift));
		xyz.z = (getZFromCurve(rParam, shift) + getZFromCurve(gParam, shift) + getZFromCurve(bParam, shift) + getZFromCurve(IRParam, shift) + getZFromCurve(UVParam, shift));
		float3 rgbFinal = XYZToRGBC(pow(1 / shift, 3) * xyz);

#if defined(LIGHTMAP_ON)
		half3 lms = DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, i.uv2));
		i.diff = float4((float3)lms, 0);
#elif defined(POINT)
		float3 normalDirection = normalize(i.normal.xyz);
		float3 lightDirection;
		float attenuation;
		if (0.0 == _WorldSpaceLightPos0.w) // directional light?
		{
			attenuation = 1.0; // no attenuation
			lightDirection =
				normalize(_WorldSpaceLightPos0.xyz);
		}
		else // point or spot light
		{
			float3 vertexToLightSource =
				_WorldSpaceLightPos0.xyz - i.pos2.xyz;
			float distance = length(vertexToLightSource);
			attenuation = 1.0 / (1.0 + 0.0005 * distance * distance);
			lightDirection = normalize(vertexToLightSource);
		}
		float nl = max(0, dot(normalDirection, lightDirection));
		i.diff = float4(attenuation * _LightColor0.rgb * _Color.rgb * nl, 1);
#endif

		//Apply lighting:
#if defined(SHADOWS_SCREEN) || defined(SHADOWS_CUBE) || (defined(SHADOWS_DEPTH) && defined(SPOT))
		fixed shadow = SHADOW_ATTENUATION(i);
		rgbFinal *= (i.diff * shadow + i.ambient);
#else
		rgbFinal *= i.diff;
#endif
		//Doppler factor should be squared for reflected light:
		xyz = RGBToXYZC(rgbFinal);
		weights = weightFromXYZCurves(xyz);
		rParam.x = weights.x; rParam.y = (float)615; rParam.z = (float)8;
		gParam.x = weights.y; gParam.y = (float)550; gParam.z = (float)4;
		bParam.x = weights.z; bParam.y = (float)463; bParam.z = (float)5;
		xyz.x = (getXFromCurve(rParam, shift) + getXFromCurve(gParam, shift) + getXFromCurve(bParam, shift) + getXFromCurve(IRParam, shift) + getXFromCurve(UVParam, shift));
		xyz.y = (getYFromCurve(rParam, shift) + getYFromCurve(gParam, shift) + getYFromCurve(bParam, shift) + getYFromCurve(IRParam, shift) + getYFromCurve(UVParam, shift));
		xyz.z = (getZFromCurve(rParam, shift) + getZFromCurve(gParam, shift) + getZFromCurve(bParam, shift) + getZFromCurve(IRParam, shift) + getZFromCurve(UVParam, shift));
		rgbFinal = XYZToRGBC(pow(1 / shift, 3) * xyz);
		//Add emission:
		rgbFinal += rgbEmission;
		rgbFinal = constrainRGB(rgbFinal.x,rgbFinal.y, rgbFinal.z); //might not be needed

																	//Test if unity_Scale is correct, unity occasionally does not give us the correct scale and you will see strange things in vertices,  this is just easy way to test
																	//float4x4 temp  = mul(unity_Scale.w*_Object2World, _World2Object);
																	//float4 temp2 = mul( temp,float4( (float)rgbFinal.x,(float)rgbFinal.y,(float)rgbFinal.z,data.a));
																	//return temp2;	
																	//float4 temp2 =float4( (float)rgbFinal.x,(float)rgbFinal.y,(float)rgbFinal.z,data.a );
		return float4(rgbFinal.xyz, data.a);
	}

	ENDCG

	Subshader {

		Pass{
			//Shader properties, for things such as transparency
			Cull Off ZWrite On
			ZTest LEqual
			Fog{ Mode off } //Fog does not shift properly and there is no way to do so with this fog
			Tags{ "LightMode" = "ForwardBase" }
			LOD 100

			AlphaTest Greater[_Cutoff]
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM

			#pragma fragmentoption ARB_precision_hint_nicest

			#pragma multi_compile_fwdbase

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			ENDCG
		}

		Pass{
			Tags{ "LightMode" = "ForwardAdd" }
			// pass for additional light sources
			Blend One One // additive blending
			LOD 200

			CGPROGRAM

			#pragma fragmentoption ARB_precision_hint_nicest
			#pragma multi_compile_fwdadd

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			ENDCG
		}

		Pass
		{
			Name "META"
			Tags{ "LightMode" = "Meta" }
			Cull Off
			CGPROGRAM

			sampler2D _GIAlbedoTex;
			fixed4 _GIAlbedoColor;
			float4 frag_meta2(v2f i) : SV_Target
			{
				// We're interested in diffuse & specular colors
				// and surface roughness to produce final albedo.

				FragmentCommonData data = UNITY_SETUP_BRDF_INPUT(float4(i.uv1.xy,0,0));
				UnityMetaInput o;
				UNITY_INITIALIZE_OUTPUT(UnityMetaInput, o);
				fixed4 c = tex2D(_GIAlbedoTex, i.uv1);
				o.Emission = (tex2D(_EmissionMap, i.uv3) * _EmissionMultiplier) * _EmissionColor;
				o.Albedo = fixed3(c.rgb * _GIAlbedoColor.rgb);
				return UnityMetaFragment(o);
			}

			#pragma vertex vert_meta
			#pragma fragment frag_meta2
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature ___ _DETAIL_MULX2
			ENDCG
		}

		Pass
		{
			Tags{ "LightMode" = "ShadowCaster" }

			CGPROGRAM
			#pragma vertex vertShadow
			#pragma fragment fragShadow
			#pragma multi_compile_shadowcaster

			struct v2fShadow {
				V2F_SHADOW_CASTER;
			};

			v2fShadow vertShadow(appdata_base v)
			{
				v2fShadow o;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
					return o;
			}

			float4 fragShadow(v2fShadow i) : SV_Target
			{
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}
	}

	FallBack "Relativity/Lit/ColorShift"
}


//o.Emission = (tex2D(_EmissionMap, IN.uv_MainTex) * _EmissionMultiplier) * _EmissionColor;
