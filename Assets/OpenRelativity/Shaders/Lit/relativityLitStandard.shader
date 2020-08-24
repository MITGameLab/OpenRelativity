//NOTE: For correct relativistic behavior, all light sources must be static
// with respect to world coordinates! General constant velocity lights are more complicated,
// and lights that accelerate might not be at all feasible.

Shader "Relativity/Lit/Standard" {
	Properties{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo", 2D) = "white" {}
		_UVTex("UV",2D) = "" {} //UV texture
		_IRTex("IR",2D) = "" {} //IR texture
		_Cutoff("Base Alpha cutoff", Range(0,.9)) = 0.1
		[Toggle(SPECULAR)]
		_SpecularOn("Specular Reflections", Range(0, 1)) = 0
		_Specular("Normal Reflectance", Range(0, 1)) = 0
		[Toggle(_EMISSION)]
		_EmissionOn("Emission Lighting", Range(0, 1)) = 0
		_EmissionMap("Emission Map", 2D) = "black" {}
		[HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
		_EmissionMultiplier("Emission Multiplier", Range(0,10)) = 1
		[Toggle(IS_STATIC)]
		_IsStatic("Light Map Static", Range(0, 1)) = 0
		_viw("viw", Vector) = (0,0,0,0) //Vector that represents object's velocity in synchronous frame
		_aiw("aiw", Vector) = (0,0,0,0) //Vector that represents object's acceleration in world coordinates
		_pap("pap", Vector) = (0,0,0,0) //Vector that represents the player's acceleration in world coordinates
	}
		CGINCLUDE

#pragma exclude_renderers xbox360
#pragma glsl
// TODO: Shouldn't be necessary to define this if headers are included in shadow variants/passes only
#define TRANSFER_SHADOW(a) a._ShadowCoord = mul( unity_WorldToShadow[0], mul( unity_ObjectToWorld, v.vertex ) );
#include "UnityStandardCore.cginc"

//Color shift variables, used to make gaussians for XYZ curves
#define xla 0.39952807612909519f
#define xlb 444.63156780935032f
#define xlc 20.095464678736523f

#define xlcSqr 403.827700654347187546611654129529f

#define xha 1.1305579611401821f
#define xhb 593.23109262398259f
#define xhc 34.446036241271742f

#define xhcSqr 1186.529412735006279641477487714564f

#define ya 1.0098874822455657f
#define yb 556.03724875218927f
#define yc 46.184868454550838f

#define ycSqr 2133.042074164165111255232326502244f

#define za 2.0648400466720593f
#define zb 448.45126344558236f
#define zc 22.357297606503543f

#define zcSqr 499.848756265769052653089671552849f

#define bFac 0.5f
#define rFac 0.9f

//Used to determine where to center UV/IR curves
#define IR_RANGE 400
#define IR_START 700
#define UV_RANGE 380
#define UV_START 0

#define FORWARD_FOG (!defined(UNITY_PASS_DEFERRED) && defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
#define SHADOW_OR_SPOT (defined(SHADOWS_SCREEN) || defined(SHADOWS_CUBE) || (defined(SHADOWS_DEPTH) && defined(SPOT)))

//Prevent NaN and Inf
#define divByZeroCutoff 1e-8f

#define CAST_LIGHTCOLOR0 float4((float)_LightColor0.r, (float)_LightColor0.g, (float)_LightColor0.b, (float)_LightColor0.a)

		struct appdata {
			float4 vertex : POSITION;
			float4 texcoord : TEXCOORD0; // main uses 1st uv
			float4 texcoord1 : TEXCOORD1; // lightmap uses 2nd uv
			float4 normal : NORMAL; // lightmap uses 2nd uv
		};

		//This is the data sent from the vertex shader to the fragment shader
		struct v2f
		{
			float4 pos : POSITION; //internal, used for display
			float4 pos2 : TEXCOORD0; //Position in world, relative to player position in world
			float2 uv1 : TEXCOORD1; //Used to specify what part of the texture to grab in the fragment shader(not relativity specific, general shader variable)
			float svc : TEXCOORD2; //sqrt( 1 - (v-c)^2), calculated in vertex shader to save operations in fragment. It's a term used often in lorenz and doppler shift calculations, so we need to keep it cached to save computing
			float2 uv2 : TEXCOORD3; //Lightmap TEXCOORD
			float4 diff : COLOR0; //Diffuse lighting color in world rest frame
			float4 normal : TEXCOORD4; //normal in world
			float4 aiwt : TEXCOORD5;
// This section is a mess, but the problem is that shader semantics are "prime real estate."
// We want to use the bare minimum of TEXCOORD instances we can get away with, to support
// the oldest and most limited possible hardware.
// TODO: Prettify the syntax of this section.
#if SHADOW_OR_SPOT
			float4 ambient : TEXCOORD6;
			SHADOW_COORDS(7)
	#if FORWARD_FOG
			float4 pos3 : TEXCOORD8; //Untransformed position in world, relative to player position in world
		#if _EMISSION
			float2 uv3 : TEXCOORD9; //EmisionMap TEXCOORD
			#if defined(POINT)
			float4 vtlt : TEXCOORD10;
			#endif
		#elif defined(POINT)
			float4 vtlt : TEXCOORD9;
		#endif
	#elif _EMISSION
			float2 uv3 : TEXCOORD8; //EmisionMap TEXCOORD
		#if defined(POINT)
			float4 vtlt : TEXCOORD9;
		#endif
	#elif defined(POINT)
			float4 vtlt : TEXCOORD8;
	#endif
#elif FORWARD_FOG
			float4 pos3 : TEXCOORD6; //Untransformed position in world, relative to player position in world
	#if _EMISSION
			float2 uv3 : TEXCOORD7; //EmisionMap TEXCOORD
		#if defined(POINT)
			float4 vtlt : TEXCOORD8;
		#endif
	#elif defined(POINT)
			float4 vtlt : TEXCOORD7;
	#endif
#elif _EMISSION
			float2 uv3 : TEXCOORD6; //EmisionMap TEXCOORD
	#if defined(POINT)
			float4 vtlt : TEXCOORD7;
	#endif
#elif defined(POINT)
			float4 vtlt : TEXCOORD6;
#endif
		};

		//Variables that we use to access texture data
		sampler2D _IRTex;
		uniform float4 _IRTex_ST;
		sampler2D _UVTex;
		uniform float4 _UVTex_ST;
		sampler2D _CameraDepthTexture;

		uniform float4 _EmissionMap_ST;
		float _EmissionMultiplier;

		float _Specular;

		float _IsStatic;

		//Lorentz transforms from player to world and from object to world are the same for all points in an object,
		// so it saves redundant GPU time to calculate them beforehand.
		float4x4 _vpcLorentzMatrix;
		float4x4 _viwLorentzMatrix;
		float4x4 _invVpcLorentzMatrix;
		float4x4 _invViwLorentzMatrix;

		//float4 _piw = float4(0, 0, 0, 0); //position of object in world
		float4 _viw = float4(0, 0, 0, 0); //velocity of object in synchronous coordinates
		float4 _aiw = float4(0, 0, 0, 0); //acceleration of object in world coordinates
		float4 _aviw = float4(0, 0, 0, 0); //scaled angular velocity
		float4 _vpc = float4(0, 0, 0, 0); //velocity of player
		float4 _pap = float4(0, 0, 0, 0); //acceleration of player
		float4 _avp = float4(0, 0, 0, 0); //angular velocity of player
		float4 _playerOffset = float4(0, 0, 0, 0); //player position in world
		float4 _vr;
		float _spdOfLight = 100; //current speed of light;
		float _spdOfLightSqrd = 10000;
		float _colorShift = 1; //actually a boolean, should use color effects or not ( doppler + spotlight). 

		float xyr = 1; // xy ratio
		float xs = 1; // x scale

		uniform float4 _MainTex_TexelSize;
		uniform float4 _CameraDepthTexture_ST;

		//Color functions
		float3 RGBToXYZC(float3 rgb)
		{
			const float3x3 rgbToXyz = {
				0.13514f, 0.120432f, 0.057128f,
				0.0668999f, 0.232706f, 0.0293946f,
				0.0f, 0.0000218959f, 0.358278f
			};
			return mul(rgbToXyz, rgb);
		}
		float3 XYZToRGBC(float3 xyz)
		{
			const float3x3 xyzToRgb = {
				9.94845f, -5.1485f, -1.16389f,
				-2.86007f, 5.77745f, -0.0179627f,
				0.000174791f, -0.000353084f, 2.79113f
			};
			return mul(xyzToRgb, xyz);
		}
		float3 weightFromXYZCurves(float3 xyz)
		{
			const float3x3 xyzToWeight = {
				0.0735806f, -0.0380793f, -0.00860837f,
				-0.0665378f, 0.134408f, -0.000417865f,
				0.00000299624f, -0.00000605249f, 0.0484424f
			};
			return mul(xyzToWeight, xyz);
		}

		float getXFromCurve(float3 param, float shift)
		{
			//Use constant memory, or let the compiler optimize constants, where we can get away with it:
			const float sqrt2Pi = sqrt(2 * 3.14159265358979323f);

			//Re-use memory to save per-vertex operations:
			float bottom2 = param.z * shift;
			bottom2 *= bottom2;
			if (bottom2 < divByZeroCutoff) {
				bottom2 = 1.0;
			}

			float paramYShift = param.y * shift;

			float top1 = param.x * xla * exp(-(((paramYShift - xlb) * (paramYShift - xlb))
				/ (2 * (bottom2 + xlcSqr)))) * sqrt2Pi;
			float bottom1 = sqrt(1 / bottom2 + 1 / xlcSqr);

			float top2 = param.x * xha * exp(-(((paramYShift - xhb) * (paramYShift - xhb))
				/ (2 * (bottom2 + xhcSqr)))) * sqrt2Pi;
			bottom2 = sqrt(1 / bottom2 + 1 / xhcSqr);

			return (top1 / bottom1) + (top2 / bottom2);
		}
		float getYFromCurve(float3 param, float shift)
		{
			//Use constant memory, or let the compiler optimize constants, where we can get away with it:
			const float sqrt2Pi = sqrt(2 * 3.14159265358979323f);

			//Re-use memory to save per-vertex operations:
			float bottom = param.z * shift;
			bottom *= bottom;
			if (bottom < divByZeroCutoff) {
				bottom = 1.0f;
			}

			float top = param.x * ya * exp(-((((param.y * shift) - yb) * ((param.y * shift) - yb))
				/ (2 * (bottom + ycSqr)))) * sqrt2Pi;
			bottom = sqrt(1 / bottom + 1 / ycSqr);

			return top / bottom;
		}

		float getZFromCurve(float3 param, float shift)
		{
			//Use constant memory, or let the compiler optimize constants, where we can get away with it:
			const float sqrt2Pi = sqrt(2 * 3.14159265358979323f);

			//Re-use memory to save per-vertex operations:
			float bottom = param.z * shift;
			bottom *= bottom;
			if (bottom < divByZeroCutoff) {
				bottom = 1.0f;
			}

			float top = param.x * za * exp(-((((param.y * shift) - zb) * ((param.y * shift) - zb))
				/ (2 * (bottom + zcSqr)))) * sqrt2Pi;
			bottom = sqrt(1 / bottom + 1 / zcSqr);

			return top / bottom;
		}

		float3 constrainRGB(float3 rgb)
		{
			float w;

			w = (0 < rgb.r) ? 0 : rgb.r;
			w = (w < rgb.g) ? w : rgb.b;
			w = (w < rgb.b) ? w : rgb.g;

			if (w < 0) {
				rgb -= float3(w, w, w);
			}

			w = rgb.r;
			w = (w < rgb.g) ? rgb.g : w;
			w = (w < rgb.b) ? rgb.b : w;

			if (w > 1)
			{
				rgb /= float3(w, w, w);
			}

			return rgb;
		};

		float3 BoxProjection(
			float3 direction, float3 position,
			float4 cubemapPosition, float3 boxMin, float3 boxMax
		) {
			UNITY_BRANCH
				if (cubemapPosition.w > 0) {
					float3 factors = ((direction > 0 ? boxMax : boxMin) - position) / direction;
					float scalar = min(min(factors.x, factors.y), factors.z);
					return direction * scalar + (position - cubemapPosition);
				}

			return direction;
		}

		float3 DopplerShift(float3 rgb, float UV, float IR, float shift) {
			//Color shift due to doppler, go from RGB -> XYZ, shift, then back to RGB.

			if (shift < divByZeroCutoff) {
				shift = 1.0f;
			}

			if (shift == 1.0f) {
				return rgb;
			}

			float3 xyz = RGBToXYZC(rgb);
			float3 weights = weightFromXYZCurves(xyz);
			float3 rParam, gParam, bParam, UVParam, IRParam;
			rParam = float3(weights.x, 615.0f, 8.0f);
			gParam = float3(weights.y, 550.0f, 4.0f);
			bParam = float3(weights.z, 463.0f, 5.0f);
			UVParam = float3(0.02f, UV_START + UV_RANGE * UV, 5.0f);
			IRParam = float3(0.02f, IR_START + IR_RANGE * IR, 5.0f);

			xyz = float3(
				(getXFromCurve(rParam, shift) + getXFromCurve(gParam, shift) + getXFromCurve(bParam, shift) + getXFromCurve(IRParam, shift) + getXFromCurve(UVParam, shift)),
				(getYFromCurve(rParam, shift) + getYFromCurve(gParam, shift) + getYFromCurve(bParam, shift) + getYFromCurve(IRParam, shift) + getYFromCurve(UVParam, shift)),
				(getZFromCurve(rParam, shift) + getZFromCurve(gParam, shift) + getZFromCurve(bParam, shift) + getZFromCurve(IRParam, shift) + getZFromCurve(UVParam, shift)));
			return constrainRGB(XYZToRGBC(pow(1 / shift, 3) * xyz));
		}

		float3 ApplyFog(float3 color, float shift, float3 pos) {
			UNITY_CALC_FOG_FACTOR_RAW(length(pos));
#if defined(UNITY_PASS_FORWARDBASE)
	#if _EMISSION
			float3 fogColor = DopplerShift(unity_FogColor.rgb, unity_FogColor.r * rFac, unity_FogColor.b * bFac, shift);
			return lerp(fogColor, color, saturate(unityFogFactor));
	#else
			return DopplerShift(lerp(unity_FogColor.rgb, color, saturate(unityFogFactor)), unity_FogColor.r, unity_FogColor.b, shift);
	#endif
#else
			return lerp((float3)0, color, saturate(unityFogFactor));
#endif
		}

		//Per vertex operations
		v2f vert(appdata v)
		{
			v2f o;

			o.uv1.xy = (v.texcoord + _MainTex_ST.zw) * _MainTex_ST.xy; //get the UV coordinate for the current vertex, will be passed to fragment shader
			o.uv2 = float2(0, 0);
#ifdef LIGHTMAP_ON
			o.uv2 = v.texcoord1 * unity_LightmapST.xy + unity_LightmapST.zw;
#endif
#if _EMISSION
			o.uv3 = float2(0, 0);
#endif
			//You need this otherwise the screen flips and weird stuff happens
#ifdef SHADER_API_D3D9
			if (_MainTex_TexelSize.y < 0)
				o.uv1.y = 1.0f - o.uv1.y;
#endif 

#if defined(UNITY_PASS_FORWARDBASE) && _EMISSION
			o.uv3.xy = (v.texcoord + _EmissionMap_ST.zw) * _EmissionMap_ST.xy;
#endif

			float4 tempPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0f));
#if FORWARD_FOG
			o.pos3 = float4(tempPos.xyz / tempPos.w - _playerOffset.xyz, 0);
			o.pos = o.pos3;
#else
			o.pos = float4(tempPos.xyz / tempPos.w - _playerOffset.xyz, 0);
#endif

			float speed = length(_vpc.xyz);
			_spdOfLightSqrd = _spdOfLight * _spdOfLight;

			//relative speed
			float speedr = sqrt(dot(_vr.xyz, _vr.xyz));
			o.svc = sqrt(1 - speedr * speedr); // To decrease number of operations in fragment shader, we're storing this value

			//riw = location in world, for reference
			float4 riw = float4(o.pos.xyz, 0); //Position that will be used in the output

			//Boost to rest frame of player:
			float4 riwForMetric = mul(_vpcLorentzMatrix, riw);

			//Find metric based on player acceleration and rest frame:
			float linFac = 1 + dot(_pap.xyz, riwForMetric.xyz) / _spdOfLightSqrd;
			linFac *= linFac;
			float angFac = dot(_avp.xyz, riwForMetric.xyz) / _spdOfLight;
			angFac *= angFac;
			float avpMagSqr = dot(_avp.xyz, _avp.xyz);
			float3 angVec = float3(0, 0, 0);
			if (avpMagSqr > divByZeroCutoff) {
				angVec = 2 * angFac / (_spdOfLight * avpMagSqr) * _avp.xyz;
			}

			float4x4 metric = {
				-1, 0, 0, -angVec.x,
				0, -1, 0, -angVec.y,
				0, 0, -1, -angVec.z,
				-angVec.x, -angVec.y, -angVec.z, (linFac * (1 - angFac) - angFac)
			};

			//Lorentz boost back to world frame;
			metric = mul(transpose(_invVpcLorentzMatrix), mul(metric, _invVpcLorentzMatrix));

			//We'll also Lorentz transform the vectors:

			//Apply Lorentz transform;
			metric = mul(transpose(_viwLorentzMatrix), mul(metric, _viwLorentzMatrix));
			float4 aiwTransformed = mul(_viwLorentzMatrix, _aiw);
			//Translate in time:
			float4 riwTransformed = mul(_viwLorentzMatrix, riw);
			float tisw = riwTransformed.w;
			riwTransformed.w = 0;

			//(When we "dot" four-vectors, always do it with the metric at that point in space-time, like we do so here.)
			float riwDotRiw = -dot(riwTransformed, mul(metric, riwTransformed));
			o.aiwt = mul(metric, aiwTransformed);
			float aiwDotAiw = -dot(aiwTransformed, o.aiwt);

			if (_IsStatic) {
				float sqrtArg = riwDotRiw / _spdOfLightSqrd;

				float t2 = 0;
				if (sqrtArg > 0)
				{
					t2 = -sqrt(sqrtArg);
				}
				tisw += t2;
			}
			else {
				float riwDotAiw = -dot(riwTransformed, o.aiwt);

				float sqrtArg = riwDotRiw * (_spdOfLightSqrd - riwDotAiw + aiwDotAiw * riwDotRiw / (4 * _spdOfLightSqrd)) / ((_spdOfLightSqrd - riwDotAiw) * (_spdOfLightSqrd - riwDotAiw));
				float aiwMag = length(aiwTransformed.xyz);
				float t2 = 0;
				if (sqrtArg > 0)
				{
					t2 = -sqrt(sqrtArg);
				}
				tisw += t2;
				//add the position offset due to acceleration
				if (aiwMag > divByZeroCutoff)
				{
					riwTransformed.xyz -= aiwTransformed.xyz / aiwMag * _spdOfLightSqrd * (sqrt(1 + (aiwMag * t2 / _spdOfLight) * (aiwMag * t2 / _spdOfLight)) - 1);
				}
			}
			riwTransformed.w = tisw;

			//Inverse Lorentz transform the position:
			riw = mul(_invViwLorentzMatrix, riwTransformed);
			tisw = riw.w;
			riw = float4(riw.xyz + tisw * _spdOfLight * _viw.xyz, 0);

			float newz = speed * _spdOfLight * tisw;

			if (speed > divByZeroCutoff) {
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

			o.normal = float4(UnityObjectToWorldNormal(v.normal), 0);

#if defined(VERTEXLIGHT_ON)
			// Red/blue shift light due to gravity
			float4 lightColor = CAST_LIGHTCOLOR0;
			if (aiwDotAiw > divByZeroCutoff) {
				float4 posTransformed = mul(_viwLorentzMatrix, _WorldSpaceLightPos0.xyz);
				float posDotAiw = -dot(posTransformed, o.aiwt);

				float shift = 1.0f + posDotAiw / (sqrt(aiwDotAiw) * _spdOfLightSqrd);
				lightColor = float4(DopplerShift(lightColor, lightColor.r * rFac, lightColor.b * bFac, shift), lightColor.w);
			}

			// dot product between normal and light direction for
			// standard diffuse (Lambert) lighting
			float nl = dot(o.normal.xyz, _WorldSpaceLightPos0.xyz);
			nl = max(0, nl);
			// factor in the light color
			o.diff = nl * lightColor;
			// add ambient light
#if SHADOW_OR_SPOT
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
					unity_4LightPosZ0[index], 1.0f);
				vertexToLightSource =
					mul(_viwLorentzMatrix, _WorldSpaceLightPos0.xyz) - o.pos2.xyz;
				squaredDistance =
					dot(vertexToLightSource, mul(metric, vertexToLightSource));

				// Red/blue shift light due to gravity
				float4 lightColor = CAST_LIGHTCOLOR0;
				if (aiwDotAiw > divByZeroCutoff) {
					float posDotAiw = -dot(vertexToLightSource, o.aiwt);
					float shift = 1.0f + posDotAiw / (sqrt(aiwDotAiw) * _spdOfLightSqrd);
					lightColor = float4(DopplerShift(lightColor, lightColor.r * rFac, lightColor.b * bFac, shift), lightColor.a);
				}

				if (unity_SpotDirection[index].z != 1) // directional light?
				{
					attenuation = 1.0f; // no attenuation
					lightDirection =
						normalize(unity_SpotDirection[index]);
				}
				else {
					attenuation = 1.0f / (1.0f +
						unity_4LightAtten0[index] * squaredDistance);
					lightDirection = normalize(vertexToLightSource);
				}
				diffuseReflection = attenuation
					* lightColor * _Color.rgb
					* max(0.0f, dot(o.normal.xyz, lightDirection));

				o.diff.rgb += diffuseReflection;
			}
#elif defined(LIGHTMAP_ON)
			//half3 lms = DecodeLightmap(UNITY_SAMPLE_TEX2DARRAY_LOD(unity_Lightmap, o.uv2, 200));
			//o.diff = float4((float3)lms, 0);
			o.diff = 0;
#else 
			// Red/blue shift light due to gravity
			float4 lightColor = CAST_LIGHTCOLOR0;
			if (aiwDotAiw > divByZeroCutoff) {
				float4 posTransformed = mul(_viwLorentzMatrix, _WorldSpaceLightPos0.xyz) - riwTransformed;
				float posDotAiw = -dot(posTransformed, o.aiwt);

				float shift = 1.0f + posDotAiw / (sqrt(aiwDotAiw) * _spdOfLightSqrd);
				lightColor = float4(DopplerShift(lightColor, lightColor.r * rFac, lightColor.b * bFac, shift), lightColor.w);
			}

			// dot product between normal and light direction for
			// standard diffuse (Lambert) lighting
			float nl = dot(o.normal.xyz, _WorldSpaceLightPos0.xyz);
			nl = max(0, nl);
			// factor in the light color
			o.diff = nl * lightColor;
			// add ambient light
#if SHADOW_OR_SPOT
			o.ambient = float4(max(0, ShadeSH9(half4(o.normal))), 0);
#else
			o.diff.rgb += max(0, ShadeSH9(half4(o.normal)));
#endif
#endif

#if SHADOW_OR_SPOT
			TRANSFER_SHADOW(o)
#endif

#if defined(POINT)
			float4 vtl = float4(mul(_viwLorentzMatrix, _WorldSpaceLightPos0.xyz) - o.pos2.xyz, 0);
			o.vtlt = mul(metric, vtl);
#endif

			return o;
		}

		//Per pixel shader, does color modifications
		float4 frag(v2f i) : COLOR
		{
			//Used to maintian a square scale ( adjust for screen aspect ratio )
			float3 x1y1z1 = i.pos2 * (float3)(2 * xs, 2 * xs / xyr, 1);

			// ( 1 - (v/c)cos(theta) ) / sqrt ( 1 - (v/c)^2 )
			float shift = (1 - dot(x1y1z1, _vr.xyz) / sqrt(dot(x1y1z1, x1y1z1))) / i.svc;
			if (_colorShift == 0)
			{
				shift = 1.0f;
			}

			//The Doppler factor is squared for (diffuse or specular) reflected light.
			//The light color is given in the world frame. That is, the Doppler-shifted "photons" in the world frame
			//are indistinguishable from photons of the same color emitted from a source at rest with respect to the world frame.
			//Assume the albedo is an intrinsic reflectance property. The reflection spectrum should not be frame dependent.

			//Get initial color 
			float3 viewDir = i.pos2.xyz - _WorldSpaceCameraPos.xyz;
			i.normal /= length(i.normal);
			float3 reflDir = reflect(viewDir, i.normal);
			reflDir = BoxProjection(
				reflDir, i.pos2,
				unity_SpecCube0_ProbePosition,
				unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax
			);
			float4 envSample = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflDir);
			float4 data = tex2D(_MainTex, i.uv1).rgba;
			float UV = tex2D(_UVTex, i.uv1).r;
			float IR = tex2D(_IRTex, i.uv1).r;

#if defined(UNITY_PASS_FORWARDBASE) && _EMISSION
			float3 rgbEmission = DopplerShift((tex2D(_EmissionMap, i.uv3) * _EmissionMultiplier) * _EmissionColor, UV, IR, shift);
#endif

			//Apply lighting in world frame:
			float3 rgb = data.xyz;
			float3 rgbFinal = DopplerShift(rgb, UV, IR, shift);

#if defined(LIGHTMAP_ON)
			half3 lms = DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, i.uv2));
			i.diff = float4((float3)lms, 0);
#elif defined(POINT)
			// We have to compromise on accuracy for relativity in curved backgrounds, here, unfortunately.
			// Ideally, we'd pass the metric tensor into the fragment shader, for use here, to calculate
			// inner products, but that's a little computationally extreme, for the pipeline.
			// It's not theoretically correct, for point lights, but we assume that the frame is flat and
			// inertial, up to acceleration of the fragment.

			float3 normalDirection = normalize(i.normal.xyz);
			float3 lightDirection;
			float attenuation;
			if (0.0 == _WorldSpaceLightPos0.w) // directional light?
			{
				attenuation = 1.0f; // no attenuation
				lightDirection =
					normalize(_WorldSpaceLightPos0.xyz);
			}
			else // point or spot light
			{
				float3 vertexToLightSource =
					mul(_viwLorentzMatrix, _WorldSpaceLightPos0.xyz).xyz - i.pos2.xyz;
				float squaredDistance = dot(vertexToLightSource, i.vtlt);
				attenuation = 1.0f / (1.0f + 0.0005f * squaredDistance);
				lightDirection = normalize(vertexToLightSource);
			}
			float nl = max(0, dot(normalDirection, lightDirection));

			float aiwDotAiw = -dot(i.aiwt, i.aiwt);
			float4 lightColor = CAST_LIGHTCOLOR0;
			if (aiwDotAiw > divByZeroCutoff) {
				float4 posTransformed = mul(_viwLorentzMatrix, _WorldSpaceLightPos0.xyz);
				float posDotAiw = -dot(posTransformed, i.aiwt);

				float shift = sqrt(aiwDotAiw) * _spdOfLightSqrd;
				if (shift < divByZeroCutoff) {
					shift = 1.0f;
				}

				shift = 1.0f + posDotAiw / shift;

				lightColor = float4(DopplerShift(lightColor, lightColor.r * rFac, lightColor.b * bFac, shift), lightColor.w);
			}

			i.diff = float4(attenuation * lightColor.rgb * _Color.rgb * nl, 1);
#endif

			//Apply lighting:
#if SHADOW_OR_SPOT
			fixed shadow = SHADOW_ATTENUATION(i);
			rgbFinal *= (i.diff * shadow + i.ambient);
#else
			rgbFinal *= i.diff;
#endif

#if SPECULAR
			//Apply specular reflectance
			//(Assume surrounding medium has an index of refraction of 1)
			float specFactor;
			if (_Specular >= 1.0f) {
				specFactor = 1.0f;
			}
			else if (_Specular <= 0.0f) {
				specFactor = 0.0f;
			}

			float indexRefrac = sqrt(1 - _Specular);
			indexRefrac = (1.0f + indexRefrac) / (1.0f - indexRefrac);
			float angle = acos(dot(viewDir, i.normal) / length(viewDir));
			float cosAngle = cos(angle);
			float sinFac = sin(angle) / indexRefrac;
			sinFac *= sinFac;
			sinFac = sqrt(1 - sinFac);
			float reflecS = (cosAngle - indexRefrac * sinFac) / (cosAngle + indexRefrac * sinFac);
			reflecS *= reflecS;
			float reflecP = (sinFac - indexRefrac * cosAngle) / (sinFac + indexRefrac * cosAngle);
			reflecP *= reflecP;
			specFactor = (reflecS + reflecP) / 2;

			float3 specRgb, specFinal;
			if (specFactor > 0.0f) {
				specRgb = DecodeHDR(envSample, unity_SpecCube0_HDR) * specFactor;
				specFinal = DopplerShift(specRgb, specRgb.r * rFac, specRgb.b * bFac, shift);
			}
			else {
				specRgb = 0;
				specFinal = 0;
			}

			// Specular reflection is added after lightmap and shadow
			if (specFactor > 0.0f) {
				rgbFinal += specFinal;
			}
#endif

#if defined(UNITY_PASS_FORWARDBASE) && _EMISSION
			//Doppler factor should be squared for reflected light:
			rgbFinal = DopplerShift(rgbFinal, UV, IR, shift);

			//Add emission:
			rgbFinal += rgbEmission;

	#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
			// We're approximating a volumetric effect for a fog that's stationary relative
			// to the untransformed world coordinates, so we just use those.
			rgbFinal = ApplyFog(rgbFinal, shift, i.pos3);
	#endif
#else
	#if FORWARD_FOG
			// Doppler shift can be combined into a single step, if there's no emission
			rgbFinal = ApplyFog(rgbFinal, shift, i.pos3);
	#else
			//Doppler factor should be squared for reflected light:
			rgbFinal = DopplerShift(rgbFinal, UV, IR, shift);
	#endif
#endif

			return float4(rgbFinal.rgb, data.a); //use me for any real build
		}

		ENDCG

		Subshader {

			Pass{
				//Shader properties, for things such as transparency
				ZWrite On
				ZTest LEqual
				Tags{ "LightMode" = "ForwardBase" }
				LOD 100

				AlphaTest Greater[_Cutoff]
				Blend SrcAlpha OneMinusSrcAlpha

				CGPROGRAM

				#pragma fragmentoption ARB_precision_hint_nicest
				#pragma multi_compile_fwdbase
				#pragma multi_compile_fog
				#pragma shader_feature SPECULAR
			    #pragma shader_feature _EMISSION

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
				#pragma multi_compile_fog
				#pragma shader_feature SPECULAR
				#pragma shader_feature _EMISSION

				#pragma vertex vert
				#pragma fragment frag
				#pragma target 3.0

				ENDCG
			}

			Pass
			{
				Name "META"
				Tags{ "LightMode" = "Meta" }
				CGPROGRAM

				#include "UnityStandardMeta.cginc"

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
#if _EMISSION
					o.Emission = (tex2D(_EmissionMap, i.uv3) * _EmissionMultiplier) * _EmissionColor;
#else
					o.Emission = 0;
#endif
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

		FallBack "Relativity/Unlit/ColorLorentz"
}
