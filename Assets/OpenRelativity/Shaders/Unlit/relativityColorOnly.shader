Shader "Relativity/Unlit/ColorOnly"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "" {} //Visible Spectrum Texture ( RGB )
		_dopplerIntensity("UV/IR mix-in intensity", Range(0,1)) = 1
		[Toggle(DOPPLER_MIX)] _dopplerMix("Responsive Doppler shift intensity", Range(0,1)) = 0
		[Toggle(UV_IR_TEXTURES)] _UVAndIRTextures("UV and IR textures", Range(0, 1)) = 1
		_UVTex("UV", 2D) = "" {} //UV texture
		_IRTex("IR",2D) = "" {} //IR texture
		_Cutoff("Base Alpha cutoff", Range(0,.9)) = 0.1 //Used to determine when not to render alpha materials
		[HideInInspector] _lastUpdateSeconds("_lastUpdateSeconds", Range(0, 2500000)) = 0
	}

		CGINCLUDE

#pragma exclude_renderers xbox360
#pragma glsl
#include "UnityCG.cginc"

#define M_PI 3.14159265358979323846f

			//Color shift variables, used to make guassians for XYZ curves
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

		//Prevent NaN and Inf
#define FLT_EPSILON 1.192092896e-07F

	//This is the data sent from the vertex shader to the fragment shader
	struct v2f
	{
		float4 pos : POSITION; //internal, used for display
		float4 pos2 : TEXCOORD0; //Position in world, relative to player position in world
		float2 uv1 : TEXCOORD1; //Used to specify what part of the texture to grab in the fragment shader(not relativity specific, general shader variable)
		float svc : TEXCOORD2; //sqrt( 1 - (v-c)^2), calculated in vertex shader to save operations in fragment. It's a term used often in lorenz and doppler shift calculations, so we need to keep it cached to save computing
	};

						   //Variables that we use to access texture data
	sampler2D _MainTex;
	uniform float4 _MainTex_ST;
	sampler2D _IRTex;
	uniform float4 _IRTex_ST;
	sampler2D _UVTex;
	uniform float4 _UVTex_ST;

	float _dopplerIntensity;

	float4 _viw = float4(0, 0, 0, 0); //velocity of object in world
	float4 _vpc = float4(0, 0, 0, 0); //velocity of player
	float4 _avp = float4(0, 0, 0, 0); //angular velocity of player
	float4 _playerOffset = float4(0, 0, 0, 0); //player position in world
	float4 _vr;
	float _spdOfLight = 100; //current speed of light

	float xyr = 1; // xy ratio
	float xs = 1; // x scale

	uniform float4 _MainTex_TexelSize;

	//Per vertex operations
	v2f vert(appdata_img v)
	{
		v2f o;

		o.uv1.xy = (v.texcoord + _MainTex_ST.zw) * _MainTex_ST.xy; //get the UV coordinate for the current vertex, will be passed to fragment shader
																   //You need this otherwise the screen flips and weird stuff happens
#ifdef SHADER_API_D3D9
		if (_MainTex_TexelSize.y < 0)
			o.uv1.y = 1 - o.uv1.y;
#endif 

		float4 piw = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1));
		piw = float4(piw.xyz / piw.w - _playerOffset.xyz, 0);

		float speed = length(_vpc.xyz);
		//vw + vp/(1+vw*vp/c^2)

		//relative speed
		float speedRSqr = dot(_vr.xyz, _vr.xyz);
		o.svc = sqrt(1 - speedRSqr); // To decrease number of operations in fragment shader, we're storing this value

		//Transform the vertex back into local space for the mesh to use
		o.pos = UnityObjectToClipPos(v.vertex.xyz);
		o.pos2 = float4(piw.xyz, 0);

		return o;
	}

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
		if (bottom2 == 0) {
			bottom2 = 1;
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
		if (bottom == 0) {
			bottom = 1;
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
		if (bottom == 0) {
			bottom = 1;
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

	float3 DopplerShift(float3 rgb, float UV, float IR, float shift) {
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
	}

	//Per pixel shader, does color modifications
	float4 frag(v2f i) : COLOR
	{
		// ( 1 - (v/c)cos(theta) ) / sqrt ( 1 - (v/c)^2 )
		float shift;
		if ((i.svc <= FLT_EPSILON) || (dot(_vr.xyz, _vr.xyz) <= FLT_EPSILON)) {
			shift = 1;
		}
		else
		{
			shift = (1 - dot(normalize(i.pos2), _vr.xyz)) / i.svc;
		}

		//This is a debatable and stylistic point,
		// but, if we think of the albedo as due to (diffuse) reflectance, we should do this:
		shift *= shift;
		// Reflectance squares the effective Doppler shift. Unsquared, the shift
		// would be appropriate for a black body or spectral emission spectrum.
		// The factor can thought of as due to the apparent velocity of a (static with respect to world coordinates) source image,
		// which is twice as much as the velocity of the (diffuse) "mirror." (See: https://arxiv.org/pdf/physics/0605100.pdf )
		// The point is, most of the colors of common objects that humans see are due to reflectance.
		// Light directly from a light bulb, or flame, or LED, would not receive this Doppler factor squaring.

		//Get initial color 
		float4 data = tex2D(_MainTex, i.uv1);

#if UV_IR_TEXTURES
		float UV = tex2D(_UVTex, i.uv1).r;
		float IR = tex2D(_IRTex, i.uv1).r;

		return float4(DopplerShift(data.rgb, UV, IR, shift), data.a);
#else
		return float4(DopplerShift(data.rgb, data.b * bFac, data.r * rFac, shift), data.a);
#endif
	}

		ENDCG

		Subshader {

			Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" }

			Pass{
				//Shader properties, for things such as transparency
				Cull Off ZWrite On
				ZTest LEqual
				Fog{ Mode off } //Fog does not shift properly and there is no way to do so with this fog

				AlphaTest Greater[_Cutoff]
				Blend SrcAlpha OneMinusSrcAlpha

				CGPROGRAM

				#pragma fragmentoption ARB_precision_hint_nicest

				#pragma shader_feature UV_IR_TEXTURES
				#pragma shader_feature DOPPLER_MIX

				#pragma vertex vert
				#pragma fragment frag
				#pragma target 3.0

				ENDCG
			}
	}

	CustomEditor "ColorOnlyRelativityGUI"

	Fallback "Unlit/Transparent"

} // shader

