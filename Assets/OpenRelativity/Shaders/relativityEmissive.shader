//NOTE: For correct relativistic behavior, all light sources must be static
// with respect to world coordinates! General constant velocity lights are more complicated,
// and lights that accelerate might not be at all feasible.

Shader "Relativity/VertexLit/EmissiveColorShift" {
	Properties{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo", 2D) = "white" {}
		_UVTex("UV",2D) = "" {} //UV texture
		_IRTex("IR",2D) = "" {} //IR texture
		_Cutoff("Base Alpha cutoff", Range(0,.9)) = 0.1
		_viw("viw", Vector) = (0,0,0,0)
		_EmissionMap("Emission Map", 2D) = "black" {}
		[HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
		_EmissionMultiplier("Emission Multiplier", Range(0,10)) = 1
	}
		CGINCLUDE

#pragma exclude_renderers xbox360
#pragma glsl
#include "UnityCG.cginc"
#include "UnityLightingCommon.cginc" // for _LightColor0
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

	//This is the data sent from the vertex shader to the fragment shader
	struct v2f
	{
		float4 pos : POSITION; //internal, used for display
		float4 pos2 : TEXCOORD0; //Position in world, relative to player position in world
		float2 uv1 : TEXCOORD1; //Used to specify what part of the texture to grab in the fragment shader(not relativity specific, general shader variable)
		float svc : TEXCOORD2; //sqrt( 1 - (v-c)^2), calculated in vertex shader to save operations in fragment. It's a term used often in lorenz and doppler shift calculations, so we need to keep it cached to save computing
		float4 vr : TEXCOORD3; //Relative velocity of object vpc - viw
		float2 uv2 : TEXCOORD4; //UV TEXCOORD
		float2 uv3 : TEXCOORD5; //IR TEXCOORD
		float2 uv4 : TEXCOORD6; //EmisionMap TEXCOORD
		float4 diff : COLOR0; //Diffuse lighting color in world rest frame
	};


	//Variables that we use to access texture data
	//sampler2D _MainTex;
	//uniform float4 _MainTex_ST;
	sampler2D _IRTex;
	uniform float4 _IRTex_ST;
	sampler2D _UVTex;
	uniform float4 _UVTex_ST;
	sampler2D _CameraDepthTexture;
	uniform float4 _EmissionMap_ST;

	float _EmissionMultiplier;

	//float4 _piw = float4(0, 0, 0, 0); //position of object in world
	float4 _viw = float4(0, 0, 0, 0); //velocity of object in world
	float4 _aviw = float4(0, 0, 0, 0); //scaled angular velocity
	float4 _vpc = float4(0, 0, 0, 0); //velocity of player
									  //float _gtt = 1; //velocity of player
	float4 _playerOffset = float4(0, 0, 0, 0); //player position in world
	float _spdOfLight = 100; //current speed of light
	float _colorShift = 1; //actually a boolean, should use color effects or not ( doppler + spotlight). 

	float xyr = 1; // xy ratio
	float xs = 1; // x scale

	uniform float4 _MainTex_TexelSize;
	uniform float4 _CameraDepthTexture_ST;

	//Per vertex operations
	v2f vert(appdata_base v)
	{
		v2f o;

		float4 viw = _viw;

		o.pos = mul(unity_ObjectToWorld, v.vertex) - _playerOffset; //Shift coordinates so player is at origin

		o.uv1.xy = (v.texcoord + _MainTex_ST.zw) * _MainTex_ST.xy; //get the UV coordinate for the current vertex, will be passed to fragment shader
		o.uv2.xy = (v.texcoord + _UVTex_ST.zw) * _UVTex_ST.xy; //also for UV texture
		o.uv3.xy = (v.texcoord + _IRTex_ST.zw) * _IRTex_ST.xy; //also for IR texture
		o.uv4.xy = (v.texcoord + _EmissionMap_ST.zw) * _EmissionMap_ST.xy; //also for IR texture

		float speed = sqrt(dot(_vpc, _vpc));
		//vw + vp/(1+vw*vp/c^2)


		float vuDot = dot(_vpc, viw); //Get player velocity dotted with velocity of the object.
		float4 vr;
		//IF our speed is zero, this parallel velocity component will be NaN, so we have a check here just to be safe
		if (speed > divByZeroCutoff)
		{
			float4 uparra = (vuDot / (speed*speed)) * _vpc; //Get the parallel component of the object's velocity
															//Get the perpendicular component of our velocity, just by subtraction
			float4 uperp = viw - uparra;
			//relative velocity calculation
			vr = (_vpc - uparra - (sqrt(1 - speed*speed))*uperp) / (1 + vuDot);
		}
		//If our speed is nearly zero, it could lead to infinities.
		else
		{
			//relative velocity calculation
			vr = -viw;
		}

		//set our relative velocity
		o.vr = vr;
		vr *= -1;
		//relative speed
		float speedr = sqrt(dot(vr, vr));
		o.svc = sqrt(1 - speedr * speedr); // To decrease number of operations in fragment shader, we're storing this value

										   //You need this otherwise the screen flips and weird stuff happens
#ifdef SHADER_API_D3D9
		if (_MainTex_TexelSize.y < 0)
			o.uv1.y = 1.0 - o.uv1.y;
#endif 
		//riw = location in world, for reference
		float4 riw = o.pos; //Position that will be used in the output

		if (speedr > divByZeroCutoff)
		{
			float4 viwScaled = _spdOfLight * viw;

			//Here begins a rotation-free modification of the original OpenRelativity shader:

			float c = -dot(riw, riw); //first get position squared (position doted with position)

			float b = -(2 * dot(riw, viwScaled)); //next get position doted with velocity, should be only in the Z direction

			float d = (_spdOfLight*_spdOfLight) - dot(viwScaled, viwScaled);

			float tisw = (-b - (sqrt((b * b) - 4.0f * d * c))) / (2 * d);

			//get the new position offset, based on the new time we just found
			//Should only be in the Z direction

			riw = riw + (tisw * viwScaled);

			//Apply Lorentz transform
			// float newz =(riw.z + state.PlayerVelocity * tisw) / state.SqrtOneMinusVSquaredCWDividedByCSquared;
			//I had to break it up into steps, unity was getting order of operations wrong.	
			float newz = (((float)speed*_spdOfLight) * tisw);

			if (speed > divByZeroCutoff) {
				float4 vpcUnit = _vpc / speed;
				newz = (dot(riw, vpcUnit) + newz) / (float)sqrt(1 - (speed*speed));
				riw = riw + (newz - dot(riw, vpcUnit)) * vpcUnit;
			}
		}

		riw += _playerOffset;

		//Transform the vertex back into local space for the mesh to use it
		o.pos = mul(unity_WorldToObject*1.0, riw);

		o.pos2 = riw - _playerOffset;


		o.pos = UnityObjectToClipPos(o.pos);

		float4 nrml = float4(v.normal, 0);
		float4 worldNormal = mul(unity_ObjectToWorld*1.0, nrml);
		// dot product between normal and light direction for
		// standard diffuse (Lambert) lighting
		float nl = max(0, dot(worldNormal.xyz, _WorldSpaceLightPos0.xyz));
		// factor in the light color
		o.diff = nl * _LightColor0;
		// add ambient light
		o.diff.rgb += ShadeSH9(half4(worldNormal));

#ifdef VERTEXLIGHT_ON
		float4 lightPosition;
		float3 vertexToLightSource, lightDirection, diffuseReflection;
		float squaredDistance;
		float attentuation;
		for (int index = 0; index < 4; index++)
		{
			lightPosition = float4(unity_4LightPosX0[index],
				unity_4LightPosY0[index],
				unity_4LightPosZ0[index], 1.0);

			vertexToLightSource =
				lightPosition.xyz - pos.xyz;
			lightDirection = normalize(vertexToLightSource);
			squaredDistance =
				dot(vertexToLightSource, vertexToLightSource);
			attenuation = 1.0 / (1.0 +
				unity_4LightAtten0[index] * squaredDistance);
			diffuseReflection = attenuation
				* unity_LightColor[index].rgb * _Color.rgb
				* max(0.0, dot(worldNormal.xyz, lightDirection));

			o.diff += diffuseReflection;
		}
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

		float UV = tex2D(_UVTex, i.uv2).r;
		float IR = tex2D(_IRTex, i.uv3).r;

		float3 xyz = RGBToXYZC((tex2D(_EmissionMap, i.uv4) * _EmissionMultiplier) * _EmissionColor);
		float3 weights = weightFromXYZCurves(xyz);
		float3 rParam, gParam, bParam, UVParam, IRParam;		rParam.x = weights.x; rParam.y = (float)615; rParam.z = (float)8;
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
		shift = shift * shift;

		//Get initial color 
		float4 data = tex2D(_MainTex, i.uv1).rgba;

		//Apply lighting in world frame:
		float3 rgb = data.xyz * i.diff;

		//Color shift due to doppler, go from RGB -> XYZ, shift, then back to RGB.
		xyz = RGBToXYZC(rgb);
		weights = weightFromXYZCurves(xyz);
		rParam.x = weights.x;
		gParam.x = weights.y;
		bParam.x = weights.z;
		xyz.x = (getXFromCurve(rParam, shift) + getXFromCurve(gParam, shift) + getXFromCurve(bParam, shift) + getXFromCurve(IRParam, shift) + getXFromCurve(UVParam, shift));
		xyz.y = (getYFromCurve(rParam, shift) + getYFromCurve(gParam, shift) + getYFromCurve(bParam, shift) + getYFromCurve(IRParam, shift) + getYFromCurve(UVParam, shift));
		xyz.z = (getZFromCurve(rParam, shift) + getZFromCurve(gParam, shift) + getZFromCurve(bParam, shift) + getZFromCurve(IRParam, shift) + getZFromCurve(UVParam, shift));
		float3 rgbFinal = XYZToRGBC(pow((1 / shift), 3) * xyz);
		//Apply lighting:
		rgbFinal *= i.diff;
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
			Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" "LightMode" = "ForwardBase" }

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

		Pass
		{
			Name "META"
			Tags{ "LightMode" = "Meta" }
			Cull Off
			CGPROGRAM

			//Per pixel shader, does color modifications
			//Necessarily from static geometry, (so no Doppler shift).
			fixed4 frag_custom_meta(v2f i) : SV_Target
			{
				UnityMetaInput o;
				UNITY_INITIALIZE_OUTPUT(UnityMetaInput, o);
				o.Albedo = tex2D(_MainTex, i.uv1).rgb;
				o.Emission = (tex2D(_EmissionMap, i.uv4) * _EmissionMultiplier) * _EmissionColor;
				return UnityMetaFragment(o);
			}

			#pragma shader_feature _EMISSION
			#pragma vertex vert
			#pragma fragment frag_custom_meta

			ENDCG
		}
	}

	FallBack "Relativity/Lit/ColorShift"
}


//o.Emission = (tex2D(_EmissionMap, IN.uv_MainTex) * _EmissionMultiplier) * _EmissionColor;
