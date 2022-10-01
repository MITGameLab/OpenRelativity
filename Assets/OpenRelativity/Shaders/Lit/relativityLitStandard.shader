// Upgrade NOTE: replaced 'UNITY_PASS_TEXCUBE(unity_SpecCube1)' with 'UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1,unity_SpecCube0)'

//NOTE: For correct relativistic behavior, all light sources must be static
// with respect to world coordinates! General constant velocity lights are more complicated,
// and lights that accelerate might not be at all feasible.

Shader "Relativity/Lit/Standard" {

	Properties{
		[Toggle(LORENTZ_TRANSFORM)] _Lorentz("Lorentz Transform", Range(0, 1)) = 1
		[Toggle(IS_STATIC)] _IsStatic("Light map static", Range(0, 1)) = 0
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo", 2D) = "white" {}
		[Toggle(DOPPLER_SHIFT)] _dopplerShift("Doppler shift", Range(0,1)) = 1
		_dopplerIntensity("UV/IR mix-in intensity", Range(0,1)) = 1
		[Toggle(DOPPLER_MIX)] _dopplerMix("Responsive Doppler shift intensity", Range(0,1)) = 0
		[Toggle(UV_IR_TEXTURES)] _UVAndIRTextures("UV and IR textures", Range(0, 1)) = 1
		_UVTex("UV", 2D) = "" {} //UV texture
		_IRTex("IR",2D) = "" {} //IR texture
		[Toggle(SPECULAR)] _SpecularOn("Specular reflections", Range(0, 1)) = 0
		_Smoothness("Smoothness", Range(0, 1)) = 0
		_Metallic("Metallic", Range(0, 1)) = 0
		_Attenuation("Attenuation", Range(0, 10)) = 1
		[Toggle(_EMISSION)] _EmissionOn("Emission lighting", Range(0, 1)) = 0
		_EmissionMap("Emission map", 2D) = "black" {}
		[HDR] _EmissionColor("Emission color", Color) = (0,0,0)
		_EmissionMultiplier("Emission multiplier", Range(0,10)) = 1
		_pap("pap", Vector) = (0,0,0,0) //Vector that represents the player's proper acceleration
		_Cutoff("Base alpha cutoff", Range(0,.9)) = 0.1
		[HideInInspector] _lastUpdateSeconds("_lastUpdateSeconds", Range(0, 2500000)) = 0
	}
		CGINCLUDE

#pragma exclude_renderers xbox360
#pragma glsl
#include "UnityStandardCore.cginc"

#define M_PI 3.14159265358979323846f

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

//Prevent NaN and Inf
#define FLT_EPSILON 1.192092896e-07F

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
			float2 albedoUV : TEXCOORD1; //Used to specify what part of the texture to grab in the fragment shader(not relativity specific, general shader variable)
			float2 svc : TEXCOORD2; //sqrt( 1 - (v-c)^2), calculated in vertex shader to save operations in fragment. It's a term used often in lorenz and doppler shift calculations, so we need to keep it cached to save computing
			float4 diff : COLOR0; //Diffuse lighting color in world rest frame
			float4 normal : NORMAL; //normal in world

            // This section is a mess, but the problem is that shader semantics are "prime real estate."
            // We want to use the bare minimum of TEXCOORD instances we can get away with, to support
            // the oldest and most limited possible hardware.

            // TODO: Prettify the syntax of this section.
#if LIGHTMAP_ON
			float2 lightmapUV : TEXCOORD3; //Lightmap TEXCOORD
    #if FORWARD_FOG
			float4 pos3 : TEXCOORD4; //Untransformed position in world, relative to player position in world
	    #if _EMISSION
			float2 emissionUV : TEXCOORD5; //EmisionMap TEXCOORD
		    #if defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD6;
		    #endif
	    #elif defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD5;
	    #endif
    #elif _EMISSION
			float2 emissionUV : TEXCOORD4; //EmisionMap TEXCOORD
	    #if defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD5;
	    #endif
    #elif defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD4;
    #endif
#else
	#if defined(SPOT)
			float4 ambient : TEXCOORD3;
	    #if FORWARD_FOG
			float4 pos3 : TEXCOORD4; //Untransformed position in world, relative to player position in world
		    #if _EMISSION
			float2 emissionUV : TEXCOORD5; //EmisionMap TEXCOORD
			    #if defined(POINT)
			float4 lstv : TEXCOORD6;
			    #endif
		    #elif defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD7;
		    #endif
	    #elif _EMISSION
			float2 emissionUV : TEXCOORD4; //EmisionMap TEXCOORD
		    #if defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD5;
		    #endif
	    #elif defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD4;
	    #endif
    #elif FORWARD_FOG
			float4 pos3 : TEXCOORD3; //Untransformed position in world, relative to player position in world
	    #if _EMISSION
			float2 emissionUV : TEXCOORD4; //EmisionMap TEXCOORD
		    #if defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD5;
		    #endif
	    #elif defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD4;
	    #endif
    #elif _EMISSION
			float2 emissionUV : TEXCOORD3; //EmisionMap TEXCOORD
	    #if defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD4;
	    #endif
    #elif defined(POINT) || SPECULAR
			float4 lstv : TEXCOORD3;
    #endif
#endif
		};

		struct f2o {
#if defined(UNITY_PASS_DEFERRED)
			float4 gBuffer0 : SV_TARGET0;
			float4 gBuffer1 : SV_TARGET1;
			float4 gBuffer2 : SV_TARGET2;
			float4 gBuffer3 : SV_TARGET3;
#else
			float4 color : SV_TARGET;
#endif
		};

		//Variables that we use to access texture data
		sampler2D _IRTex;
		uniform float4 _IRTex_ST;
		sampler2D _UVTex;
		uniform float4 _UVTex_ST;

		uniform float4 _EmissionMap_ST;
		float _EmissionMultiplier;

		float _Smoothness;

		float _Attenuation;

		float _dopplerIntensity;

		//Lorentz transforms from player to world and from object to world are the same for all points in an object,
		// so it saves redundant GPU time to calculate them beforehand.
		float4x4 _vpcLorentzMatrix;
		float4x4 _viwLorentzMatrix;
		float4x4 _invVpcLorentzMatrix;
		float4x4 _invViwLorentzMatrix;
		// For the original author's purposes, potentially, we have metric torsion.
		// Otherwise, the inverse would be the transpose.
		float4x4 _intrinsicMetric;
		float4x4 _invIntrinsicMetric;

		float4 _viw = float4(0, 0, 0, 0); //velocity of object in synchronous coordinates
		float4 _vr = float4(0, 0, 0, 0); //velocity of object relative to player
		float4 _pao = float4(0, 0, 0, 0); //acceleration of object in world coordinates
		float4 _aviw = float4(0, 0, 0, 0); //scaled angular velocity
		float4 _vpc = float4(0, 0, 0, 0); //velocity of player
		float4 _avp = float4(0, 0, 0, 0); //angular velocity of player
		float4 _pap = float4(0, 0, 0, 0); //proper acceleration of object
		float4 _playerOffset = float4(0, 0, 0, 0); //player position in world
		float _spdOfLight = 100; //current speed of light;
		float _spdOfLightSqrd = 10000;

		float xyr = 1; // xy ratio
		float xs = 1; // x scale

		uniform float4 _MainTex_TexelSize;

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
			const float sqrt2Pi = sqrt(2 * M_PI);

			//Re-use memory to save per-vertex operations:
			float bottom2 = param.z * shift;
			bottom2 *= bottom2;
			if (bottom2 < FLT_EPSILON) {
				bottom2 = FLT_EPSILON;
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
			const float sqrt2Pi = sqrt(2 * M_PI);

			//Re-use memory to save per-vertex operations:
			float bottom = param.z * shift;
			bottom *= bottom;
			if (bottom < FLT_EPSILON) {
				bottom = FLT_EPSILON;
			}

			float top = param.x * ya * exp(-((((param.y * shift) - yb) * ((param.y * shift) - yb))
				/ (2 * (bottom + ycSqr)))) * sqrt2Pi;
			bottom = sqrt(1 / bottom + 1 / ycSqr);

			return top / bottom;
		}
		float getZFromCurve(float3 param, float shift)
		{
			//Use constant memory, or let the compiler optimize constants, where we can get away with it:
			const float sqrt2Pi = sqrt(2 * M_PI);

			//Re-use memory to save per-vertex operations:
			float bottom = param.z * shift;
			bottom *= bottom;
			if (bottom < FLT_EPSILON) {
				bottom = FLT_EPSILON;
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
#if DOPPLER_SHIFT
			//Color shift due to doppler, go from RGB -> XYZ, shift, then back to RGB.
			if (shift < FLT_EPSILON) {
				shift = FLT_EPSILON;
			}

			float mixIntensity = _dopplerIntensity;

#if DOPPLER_MIX
			// This isn't a physical effect, but we might want to normalize the material color to albedo/light map
			// for the case of 0 relative velocity. Unless we responsively reduce the effect of UV and IR,
			// this isn't the case, with this Doppler shift function. This "mixIntensity" makes it so.
		    mixIntensity *= abs((2 / M_PI) * atan(log(shift) / log(2)));
#endif

			float3 xyz = RGBToXYZC(rgb);
			float3 weights = weightFromXYZCurves(xyz);
			float3 rParam, gParam, bParam, UVParam, IRParam;
			rParam = float3(weights.x, 615, 8);
			gParam = float3(weights.y, 550, 4);
			bParam = float3(weights.z, 463, 5);
			UVParam = float3(0.02f, UV_START + UV_RANGE * UV, 5);
			IRParam = float3(0.02f, IR_START + IR_RANGE * IR, 5);

			xyz = float3(
				(getXFromCurve(rParam, shift) + getXFromCurve(gParam, shift) + getXFromCurve(bParam, shift) + mixIntensity * (getXFromCurve(IRParam, shift) + getXFromCurve(UVParam, shift))),
				(getYFromCurve(rParam, shift) + getYFromCurve(gParam, shift) + getYFromCurve(bParam, shift) + mixIntensity * (getYFromCurve(IRParam, shift) + getYFromCurve(UVParam, shift))),
				(getZFromCurve(rParam, shift) + getZFromCurve(gParam, shift) + getZFromCurve(bParam, shift) + mixIntensity * (getZFromCurve(IRParam, shift) + getZFromCurve(UVParam, shift))));

            // See this link for criticism that suggests this should be the fifth power, rather than the third:
			// https://physics.stackexchange.com/questions/43695/how-realistic-is-the-game-a-slower-speed-of-light#answer-587149
			return constrainRGB(XYZToRGBC(pow(1 / shift, 5) * xyz));
#else
			return rgb;
#endif
		}

		float3 ApplyFog(float3 color, float UV, float IR, float shift, float3 pos) {
			UNITY_CALC_FOG_FACTOR_RAW(length(pos));
#if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_DEFERRED)
	#if _EMISSION
			float3 fogColor = DopplerShift(unity_FogColor.rgb, unity_FogColor.b * bFac, unity_FogColor.r * rFac, shift);
			return lerp(fogColor, color, saturate(unityFogFactor));
	#else
			float saturatedFogFactor = saturate(unityFogFactor);
			return DopplerShift(lerp(unity_FogColor.rgb, color, saturatedFogFactor), lerp(unity_FogColor.b * bFac, UV, saturatedFogFactor), lerp(unity_FogColor.r * rFac, IR, saturatedFogFactor), shift);
	#endif
#else
			return lerp((float3)0, color, saturate(unityFogFactor));
#endif
		}

		float FadeShadows(v2f i, float attenuation) {
#if HANDLE_SHADOWS_BLENDING_IN_GI
			float3 pos = i.pos2.xyz + _playerOffset.xyz
			float3 offset = mul(_viwLorentzMatrix, float4(_WorldSpaceCameraPos - pos, 0)).xyz;
			float viewZ =
				dot(offset, UNITY_MATRIX_V[2].xyz);
			float shadowFadeDistance =
				UnityComputeShadowFadeDistance(pos, viewZ);
			float shadowFade = UnityComputeShadowFade(shadowFadeDistance);
			float bakedAttenuation =
				UnitySampleBakedOcclusion(i.lightmapUV, pos);
			attenuation = UnityMixRealtimeAndBakedShadows(
				attenuation, bakedAttenuation, shadowFade
			);
#endif
			return attenuation;
		}

		//Per vertex operations
		v2f vert(appdata v)
		{
			v2f o;

			o.albedoUV.xy = (v.texcoord + _MainTex_ST.zw) * _MainTex_ST.xy; //get the UV coordinate for the current vertex, will be passed to fragment shader
#ifdef LIGHTMAP_ON
			o.lightmapUV = v.texcoord1 * unity_LightmapST.xy + unity_LightmapST.zw;
#endif
#if _EMISSION
			o.emissionUV = float2(0, 0);
#endif
			//You need this otherwise the screen flips and weird stuff happens
#ifdef SHADER_API_D3D9
			if (_MainTex_TexelSize.y < 0)
				o.albedoUV.y = 1 - o.albedoUV.y;
#endif 

#if defined(UNITY_PASS_FORWARDBASE) && _EMISSION
			o.emissionUV.xy = (v.texcoord + _EmissionMap_ST.zw) * _EmissionMap_ST.xy;
#endif

			float4 tempPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1));
#if FORWARD_FOG
			o.pos3 = float4(tempPos.xyz / tempPos.w - _playerOffset.xyz, 0);
			o.pos = o.pos3;
#else
			o.pos = float4(tempPos.xyz / tempPos.w - _playerOffset.xyz, 0);
#endif

            float speedSqr = dot(_vpc.xyz, _vpc.xyz);
			float speed = sqrt(speedSqr);
			_spdOfLightSqrd = _spdOfLight * _spdOfLight;

			//relative speed
			float speedRSqr = dot(_vr.xyz, _vr.xyz);
			float pSpeedRSqr = dot(_viw.xyz, _viw.xyz);
			// To decrease number of operations in fragment shader, we're storing this value:
			o.svc = sqrt(float2(1 - speedRSqr, 1 - pSpeedRSqr));

			//riw = location in world, for reference
			float4 riw = float4(o.pos.xyz, 0); //Position that will be used in the output

#if LORENTZ_TRANSFORM || defined(POINT) || SPECULAR
			//Boost to rest frame of player:
			float4 riwForMetric = mul(_vpcLorentzMatrix, riw);

			//Find metric based on player acceleration and rest frame:
			float linFac = 1 + dot(_pap.xyz, riwForMetric.xyz) / _spdOfLightSqrd;
			linFac *= linFac;
			float angFac = dot(_avp.xyz, riwForMetric.xyz) / _spdOfLight;
			angFac *= angFac;
			float avpMagSqr = dot(_avp.xyz, _avp.xyz);
			float3 angVec = float3(0, 0, 0);
			if (avpMagSqr > FLT_EPSILON) {
				angVec = 2 * angFac / (_spdOfLight * avpMagSqr) * _avp.xyz;
			}

			float4x4 metric = {
				-1, 0, 0, -angVec.x,
				0, -1, 0, -angVec.y,
				0, 0, -1, -angVec.z,
				-angVec.x, -angVec.y, -angVec.z, (linFac * (1 - angFac) - angFac)
			};

			//Lorentz boost back to world frame:
			metric = mul(transpose(_invVpcLorentzMatrix), mul(metric, _invVpcLorentzMatrix));

			// Apply world coordinates intrinsic curvature:
			metric = mul(_intrinsicMetric, mul(metric, _invIntrinsicMetric));

			//(We'll also Lorentz transform the vectors.)
			//Apply Lorentz transform:
			metric = mul(transpose(_viwLorentzMatrix), mul(metric, _viwLorentzMatrix));
#endif

#if LORENTZ_TRANSFORM
			float4 paoTransformed = mul(_viwLorentzMatrix, _pao);
			float4 riwTransformed = mul(_viwLorentzMatrix, riw);
			//Translate in time:
			float tisw = riwTransformed.w;
			riwTransformed.w = 0;

			//(When we "dot" four-vectors, always do it with the metric at that point in space-time, like we do so here.)
			float riwDotRiw = -dot(riwTransformed, mul(metric, riwTransformed));
		    float4 paot = mul(metric, paoTransformed);
			float paoDotpao = -dot(paoTransformed, paot);

    #if IS_STATIC
			float sqrtArg = riwDotRiw / _spdOfLightSqrd;
			tisw += (sqrtArg > 0) ? -sqrt(sqrtArg) : 0;
    #else
			float riwDotpao = -dot(riwTransformed, paot);

			float sqrtArg = riwDotRiw * (_spdOfLightSqrd - riwDotpao + paoDotpao * riwDotRiw / (4 * _spdOfLightSqrd)) / ((_spdOfLightSqrd - riwDotpao) * (_spdOfLightSqrd - riwDotpao));
			float paoMagSqr = dot(paoTransformed.xyz, paoTransformed.xyz);
			float paoMag = sqrt(paoMagSqr);
			tisw += (sqrtArg > 0) ? -sqrt(sqrtArg) : 0;
			//add the position offset due to acceleration
			if (paoMag > FLT_EPSILON)
			{
				riwTransformed.xyz -= paoTransformed.xyz / paoMag * _spdOfLightSqrd * (sqrt(1 + sqrtArg * paoMagSqr / _spdOfLightSqrd) - 1);
			}
    #endif
			riwTransformed.w = tisw;

			//Inverse Lorentz transform the position:
			riw = mul(_invViwLorentzMatrix, riwTransformed);
			tisw = riw.w;
			riw = float4(riw.xyz + tisw * _spdOfLight * _viw.xyz, 0);

			float newz = speed * _spdOfLight * tisw;

			if (speed > FLT_EPSILON) {
				float3 vpcUnit = _vpc.xyz / speed;
				newz = (dot(riw.xyz, vpcUnit) + newz) / sqrt(1 - speedSqr);
				riw += (newz - dot(riw.xyz, vpcUnit)) * float4(vpcUnit, 0);
			}

			riw += float4(_playerOffset.xyz, 0);

			//Transform the vertex back into local space for the mesh to use
			tempPos = mul(unity_WorldToObject, float4(riw.xyz, 1));
			o.pos = float4(tempPos.xyz / tempPos.w, 0);

			o.pos2 = float4(riw.xyz - _playerOffset.xyz, 0);

			o.pos = UnityObjectToClipPos(o.pos);
#else
			//Transform the vertex back into local space for the mesh to use
			o.pos2 = o.pos;
			o.pos = UnityObjectToClipPos(v.vertex.xyz);
#endif

			o.normal = float4(UnityObjectToWorldNormal(v.normal), 0);
			o.normal = normalize(float4(mul(_viwLorentzMatrix, float4(o.normal.xyz, 0)).xyz, 0));

#if defined(VERTEXLIGHT_ON)
			// Red/blue shift light due to gravity
			float4 lightColor = CAST_LIGHTCOLOR0;

			// dot product between normal and light direction for
			// standard diffuse (Lambert) lighting
			float nl = dot(o.normal.xyz, _WorldSpaceLightPos0.xyz);
			nl = max(0, nl);
			// factor in the light color
			o.diff = nl * lightColor;
			// add ambient light
	#if defined(SPOT)
			o.ambient = float4(max(0, ShadeSH9(half4(o.normal))), 0);
	#else
			o.diff.rgb += max(0, ShadeSH9(half4(o.normal)));
	#endif

			float4 lightPosition;
			float3 lightSourceToVertex, lightDirection, diffuseReflection;
			float squaredDistance, attenuation, shift;
			for (int index = 0; index < 4; ++index)
			{
				lightPosition = float4(unity_4LightPosX0[index],
					unity_4LightPosY0[index],
					unity_4LightPosZ0[index], 1);
				lightSourceToVertex =
					mul(_viwLorentzMatrix, float4((o.pos2.xyz + _playerOffset.xyz) - _WorldSpaceLightPos0.xyz, 0));
				squaredDistance = -dot(lightSourceToVertex.xyz, mul(metric, lightSourceToVertex).xyz);
				if (squaredDistance < 0) {
					squaredDistance = 0;
				}

				// Red/blue shift light due to gravity
				lightColor = CAST_LIGHTCOLOR0;

				if (unity_SpotDirection[index].z != 1) // directional light?
				{
					attenuation = 1; // no attenuation
					lightDirection =
						normalize(unity_SpotDirection[index]);
				}
				else {
					attenuation = 1 / (1 +
						unity_4LightAtten0[index] * squaredDistance * _Attenuation);
					lightDirection = normalize(-lightSourceToVertex);
				}
				diffuseReflection = attenuation
					* lightColor * _Color.rgb
					* max(0, dot(o.normal.xyz, lightDirection));

				o.diff.rgb += diffuseReflection;
			}

#else
			o.diff = 0;

	#if defined(POINT) || SPECULAR
			o.lstv = mul(metric, mul(_viwLorentzMatrix, float4(riw.xyz - _WorldSpaceLightPos0.xyz, 0)));
	#endif
#endif

			return o;
		}

		//Per pixel shader, does color modifications
		f2o frag(v2f i)
		{
			float shift = 1;
#if DOPPLER_SHIFT
			// ( 1 - (v/c)cos(theta) ) / sqrt ( 1 - (v/c)^2 )
			if ((i.svc.x > FLT_EPSILON) && (dot(_vr.xyz, _vr.xyz) > FLT_EPSILON)) {
				shift = (1 - dot(normalize(i.pos2), _vr.xyz)) / i.svc.x;
			}
#endif

			//The Doppler factor is squared for (diffuse or specular) reflected light.
			//The light color is given in the world frame. That is, the Doppler-shifted "photons" in the world frame
			//are indistinguishable from photons of the same color emitted from a source at rest with respect to the world frame.
			//Assume the albedo is an intrinsic reflectance property. The reflection spectrum should not be frame dependent.

			//Get initial color 
			float3 viewDir = normalize(mul(_viwLorentzMatrix, float4(_WorldSpaceCameraPos.xyz - i.pos2.xyz, 0)).xyz);
			i.normal /= length(i.normal);
			float4 albedo = tex2D(_MainTex, i.albedoUV);
			albedo = float4(albedo.rgb * _Color.xyz, albedo.a);

#if UV_IR_TEXTURES
			float UV = tex2D(_UVTex, i.albedoUV).r * _Color.b;
			float IR = tex2D(_IRTex, i.albedoUV).r * _Color.r;

	#if (defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_DEFERRED)) && _EMISSION
			float3 rgbEmission = DopplerShift((tex2D(_EmissionMap, i.emissionUV) * _EmissionMultiplier) * _EmissionColor, UV, IR, shift);
	#endif
#else
	#if (defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_DEFERRED)) && _EMISSION
			float3 rgbEmission = (tex2D(_EmissionMap, i.emissionUV) * _EmissionMultiplier) * _EmissionColor;
			rgbEmission = DopplerShift(rgbEmission, rgbEmission.b * bFac, rgbEmission.r * rFac, shift);
	#endif
#endif

			//Apply lighting in world frame:
			float3 rgbFinal = albedo.xyz;

#if !defined(UNITY_PASS_DEFERRED)
			float3 lightDirection;
			float attenuation;
	#if defined(LIGHTMAP_ON)
			half3 lms = DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, i.lightmapUV));

		#if defined(DIRLIGHTMAP_COMBINED)
			attenuation = 1;

			lightDirection = UNITY_SAMPLE_TEX2D_SAMPLER(
				unity_LightmapInd, unity_Lightmap, i.lightmapUV
			).xyz;

			lightDirection = lightDirection * 2 - 1;

			// The length of the direction vector is the light's "directionality", i.e. 1 for all light coming from this direction,
			// lower values for more spread out, ambient light.
			float directionality = max(0.001f, length(lightDirection));
			lightDirection /= directionality;

			lightDirection = normalize(mul(_viwLorentzMatrix, float4(lightDirection, 0)).xyz);

			// Split light into the directional and ambient parts, according to the directionality factor.
			i.diff = float4(lms * (1 - directionality), 0);
			lms = lms * directionality;

			float nl = max(0, dot(i.normal, lightDirection));

			float3 lightRgb = DopplerShift(lms, lms.b * bFac, lms.r * rFac, shift);
			lightRgb = FadeShadows(i, attenuation) * lightRgb * _Color.rgb * nl;

			#if SPECULAR
			// Apply specular reflectance
			// (Schlick's approximation)
			// (Assume surrounding medium has an index of refraction of 1)

			float halfAngle = dot(normalize(lightDirection + viewDir), i.normal);
			float specFactor = (_Smoothness + (1 - _Smoothness) * pow(1 - halfAngle, 5)) * _Metallic;

			// Specular reflection is added after lightmap and shadow
			specFactor = min(1, specFactor);
			lightRgb *= 1 - specFactor;
			lightRgb += lightRgb * specFactor;
			#endif

			// Technically this is incorrect, but helps hide jagged light edge at the object silhouettes and
			// makes normalmaps show up.
			lightRgb *= saturate(dot(i.normal, lightDirection));

			i.diff += float4(lightRgb, 0);
		#else
			i.diff = float4((float3)lms, 0);
		#endif
	#elif defined(POINT)
			if (0 == _WorldSpaceLightPos0.w) // directional light?
			{
				attenuation = 1; // no attenuation
				lightDirection = normalize(_WorldSpaceLightPos0.xyz);
			}
			else // point or spot light
			{
				float3 lightSourceToVertex =
					mul(_viwLorentzMatrix, float4((i.pos2.xyz + _playerOffset.xyz) - _WorldSpaceLightPos0.xyz, 0));
				float squaredDistance = -dot(lightSourceToVertex.xyz, i.lstv.xyz);
				if (squaredDistance < 0) {
					squaredDistance = 0;
				}
				attenuation = 1 / (1 + squaredDistance * _Attenuation);
				lightDirection = normalize(-lightSourceToVertex);
			}
			float nl = max(0, dot(i.normal, lightDirection));

			// We're "cheating" a bit with the Doppler shift, here.
			// The idea is that the object's albedo should reflect proportional to the color of light that the object sees.
			// We could shift the light color according to the object's velocity relative to world coordinates,
			// (assuming that all lights must be static and stationary relative to world coordinates, which we generally require,)
			// and Doppler shift back to world coordinates after "reflecting," ultimately Doppler shifting according to player
			// perspective at the end.
			//
			// However, the Doppler shift method we have from the original OpenRelativity project doesn't actually self-invert
			// when applying the inverse of the shift parameter, which the Doppler shift should do. (This is a reasonable limitation.)
			// Assuming the Doppler shift were perfect, this should behave similarly to INVERSE Doppler shifting the albedo color
			// to calculate the reflectance, but this probably isn't physically meaningful: the albedo is a proper intrinsic property.

			float pShift = 1;
			if ((i.svc.y > FLT_EPSILON) && (dot(_viw.xyz, _viw.xyz) > FLT_EPSILON)) {
				pShift = (1 - dot(lightDirection, _viw.xyz)) / i.svc.x;
			}

			float3 albedoColor = _Color.rgb;

			albedoColor = DopplerShift(albedoColor, albedoColor.b * bFac, albedoColor.r * rFac, 1 / pShift);

			float3 lightRgb = FadeShadows(i, attenuation) * CAST_LIGHTCOLOR0.rgb * albedoColor * nl;

		#if SPECULAR
			// Apply specular reflectance
			// (Schlick's approximation)
			// (Assume surrounding medium has an index of refraction of 1)

			float halfAngle = dot(normalize(lightDirection + viewDir), i.normal);
			float specFactor = (_Smoothness + (1 - _Smoothness) * pow(1 - halfAngle, 5)) * _Metallic;

			// Specular reflection is added after lightmap and shadow
			specFactor = min(1, specFactor);
			lightRgb *= 1 - specFactor;
			lightRgb += lightRgb * specFactor;
		#endif
			i.diff += float4(lightRgb, 0);
	#endif
#endif



			//Apply lighting:
#if !defined(LIGHTMAP_ON) && defined(SPOT)
			rgbFinal *= (i.diff + i.ambient);
#else
			rgbFinal *= i.diff;
#endif

#if SPECULAR
			// Apply specular reflectance
			// (Schlick's approximation)
			// (Assume surrounding medium has an index of refraction of 1)
			// WARNING: Real-time reflections will be wrong. Use baked.

			float3 reflectionDir = reflect(-viewDir, i.normal);
			float cosAngle = dot(viewDir, i.normal);
			float specFactor2 = (_Smoothness + (1 - _Smoothness) * pow(1 - cosAngle, 5)) * _Metallic;

			// Specular reflection is added after lightmap and shadow
			specFactor2 = min(1, specFactor2);
			rgbFinal *= 1 - specFactor2;

	#if !(defined(UNITY_PASS_DEFERRED) && UNITY_ENABLE_REFLECTION_BUFFERS)
			Unity_GlossyEnvironmentData envData;
			envData.roughness = 1 - _Smoothness;

			envData.reflUVW = BoxProjection(
				reflectionDir, i.pos2,
				unity_SpecCube0_ProbePosition,
				unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax
			);
			float3 probe0 = Unity_GlossyEnvironment(
				UNITY_PASS_TEXCUBE(unity_SpecCube0), unity_SpecCube0_HDR, envData
			);

			envData.reflUVW = BoxProjection(
				reflectionDir, i.pos2,
				unity_SpecCube1_ProbePosition,
				unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax
			);
			float3 probe1 = Unity_GlossyEnvironment(
				UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1,unity_SpecCube0), unity_SpecCube0_HDR, envData
			);

			rgbFinal += lerp(probe1, probe0, unity_SpecCube0_BoxMin.w) * specFactor2;
	#endif
#endif

#if (defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_DEFERRED)) && _EMISSION
			//Doppler factor should be squared for reflected light:
	#if UV_IR_TEXTURES
			rgbFinal = DopplerShift(rgbFinal, UV, IR, shift);
	#else
			rgbFinal = DopplerShift(rgbFinal, rgbFinal.b * bFac, rgbFinal.r * rFac, shift);
	#endif

			//Add emission:
			rgbFinal += rgbEmission;

	#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
			// We're approximating a volumetric effect for a fog that's stationary relative
			// to the untransformed world coordinates, so we just use those.
		#if UV_IR_TEXTURES
			rgbFinal = ApplyFog(rgbFinal, UV, IR, shift, i.pos3);
		#else
			rgbFinal = ApplyFog(rgbFinal, rgbFinal.b * bFac, rgbFinal.r * rFac, shift, i.pos3);
		#endif
	#endif
#else
			// Reflections (diffuse and specular) have effectively twice the velocity, for a Doppler shift.
			shift *= shift;

	#if FORWARD_FOG
			// Doppler shift can be combined into a single step, if there's no emission
		#if UV_IR_TEXTURES
			rgbFinal = ApplyFog(rgbFinal, UV, IR, shift, i.pos3);
		#else
			rgbFinal = ApplyFog(rgbFinal, rgbFinal.b * bFac, rgbFinal.r * rFac, shift, i.pos3);
		#endif
	#else
		#if UV_IR_TEXTURES
			rgbFinal = DopplerShift(rgbFinal, UV, IR, shift);
		#else
			rgbFinal = DopplerShift(rgbFinal, rgbFinal.b * bFac, rgbFinal.r * rFac, shift);
		#endif
	#endif
#endif

			f2o output;
#if defined(UNITY_PASS_DEFERRED)
			output.gBuffer0.rgb = albedo.rgb;
			output.gBuffer0.a = 1; // No occlusion
			output.gBuffer1.rgb = float3(1, 1, 1);
			output.gBuffer1.a = _Smoothness;
			output.gBuffer2 = float4((1 + i.normal.xyz) / 2, 1);
			output.gBuffer3 = float4(rgbFinal.rgb, albedo.a);
#if !defined(UNITY_HDR_ON)
			output.gBuffer3.rgb = exp2(-output.gBuffer3.rgb);
#endif
#else
			output.color = float4(rgbFinal.rgb, albedo.a);
#endif

			return output;
		}

		ENDCG

		Subshader {

			Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" }

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
				#pragma shader_feature IS_STATIC
				#pragma shader_feature LORENTZ_TRANSFORM
			    #pragma shader_feature DOPPLER_SHIFT
				#pragma shader_feature UV_IR_TEXTURES
				#pragma shader_feature DOPPLER_MIX
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
				ZWrite Off
				LOD 200

				CGPROGRAM

				#pragma fragmentoption ARB_precision_hint_nicest
				#pragma multi_compile_fwdadd
				#pragma multi_compile_fog
				#pragma shader_feature IS_STATIC
				#pragma shader_feature LORENTZ_TRANSFORM
				#pragma shader_feature DOPPLER_SHIFT
				#pragma shader_feature UV_IR_TEXTURES
				#pragma shader_feature DOPPLER_MIX
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
				Cull Off
				CGPROGRAM

				#include "UnityStandardMeta.cginc"

				sampler2D _GIAlbedoTex;
				fixed4 _GIAlbedoColor;
				float4 frag_meta2(v2f_meta i) : SV_Target
				{
					// We're interested in diffuse & specular colors
					// and surface roughness to produce final albedo.

					FragmentCommonData data = UNITY_SETUP_BRDF_INPUT(float4(i.uv.xy,0,0));
					UnityMetaInput o;
					UNITY_INITIALIZE_OUTPUT(UnityMetaInput, o);
					fixed4 c = tex2D(_GIAlbedoTex, i.uv);
#if _EMISSION
					o.Emission = (tex2D(_EmissionMap, i.uv) * _EmissionMultiplier) * _EmissionColor;
#else
					o.Emission = 0;
#endif
					o.Albedo = fixed3(c.rgb * _GIAlbedoColor.rgb);
					
					return UnityMetaFragment(o);
				}

				#pragma vertex vert_meta
				#pragma fragment frag_meta2
				#pragma shader_feature IS_STATIC
				#pragma shader_feature LORENTZ_TRANSFORM
				#pragma shader_feature DOPPLER_SHIFT
				#pragma shader_feature UV_IR_TEXTURES
				#pragma shader_feature DOPPLER_MIX
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

			Pass{
				
				ZWrite On
				ZTest LEqual
				Tags { "LightMode" = "Deferred" }
				LOD 100

				AlphaTest Greater[_Cutoff]
				Blend SrcAlpha OneMinusSrcAlpha

				CGPROGRAM

				#pragma exclude_renderers nomrt

				#pragma fragmentoption ARB_precision_hint_nicest
				#pragma multi_compile_fwdbase
				#pragma multi_compile_fog
				#pragma multi_compile _ UNITY_HDR_ON
				#pragma shader_feature IS_STATIC
				#pragma shader_feature LORENTZ_TRANSFORM
				#pragma shader_feature DOPPLER_SHIFT
				#pragma shader_feature UV_IR_TEXTURES
				#pragma shader_feature DOPPLER_MIX
				#pragma shader_feature SPECULAR
				#pragma shader_feature _EMISSION

				#pragma vertex vert
				#pragma fragment frag
				#pragma target 3.0

				ENDCG
			}
		}

		CustomEditor "StandardRelativityGUI"

		FallBack "Relativity/Unlit/ColorLorentz"
}
