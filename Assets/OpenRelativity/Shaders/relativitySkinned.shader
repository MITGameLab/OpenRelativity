// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

Shader "Relativity/ColorOnly"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "" {} //Visible Spectrum Texture ( RGB )
		_UVTex("UV",2D) = "" {} //UV texture
		_IRTex("IR",2D) = "" {} //IR texture
		_piw("piw", Vector) = (0,0,0,0) //Vector that represents object's position in world frame
		_viw("viw", Vector) = (0,0,0,0) //Vector that represents object's velocity in world frame
		_aviw("aviw", Vector) = (0,0,0,0) //Vector that represents object's angular velocity times the object's world scale
										  //_gtt("gtt", float) = 0 //float that represents 00 component of metric due to player acceleration
		_strtTime("strtTime", float) = 0 //For moving objects, when they created, this variable is set to current world time
		_Cutoff("Base Alpha cutoff", Range(0,.9)) = 0.1 //Used to determine when not to render alpha materials
	}

		CGINCLUDE

#pragma exclude_renderers xbox360
#pragma glsl
#include "UnityCG.cginc"

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

		//Quaternion math
#define quaternion float4
#define PI_F 3.14159265f;

		//Prevent NaN and Inf
#define divByZeroCutoff 1e-8f

		//Quaternion rotation
		//https://blog.molecular-matters.com/2013/05/24/a-faster-quaternion-vector-multiplication/
		inline float3 rot3(quaternion q, float3 v) {
		if (dot(q.xyz, q.xyz) == 0.0f) return v;
		float3 t = 2.0f * cross(q.xyz, v);
		return v + q.w * t + cross(q.xyz, t);
	}

	inline float4 rot4(quaternion q, float4 v) {
		if (dot(q.xyz, q.xyz) == 0.0f) return v;
		float3 t = 2.0f * cross(q.xyz, v.xyz);
		t = v.xyz + q.w * t + cross(q.xyz, t);
		return quaternion(t.x, t.y, t.z, 0.0f);
	}

	inline quaternion makeRotQ(float angle, float3 direction) {
		return quaternion(sin(angle / 2.0f) * direction, cos(angle / 2.0f));
	}

	inline quaternion inverse(quaternion q) {
		return q / dot(q, q);
	}


	//This is the data sent from the vertex shader to the fragment shader
	struct v2f
	{
		float4 pos : POSITION; //internal, used for display
		float2 uv1 : TEXCOORD1; //Used to specify what part of the texture to grab in the fragment shader(not relativity specific, general shader variable)
		float svc : TEXCOORD2; //sqrt( 1 - (v-c)^2), calculated in vertex shader to save operations in fragment. It's a term used often in lorenz and doppler shift calculations, so we need to keep it cached to save computing
		float4 vr : TEXCOORD3; //Relative velocity of object vpc - viw
		float draw : TEXCOORD4; //Draw the vertex?  Used to not draw objects that are calculated to be seen before they were created. Object's start time is used to determine this. If something comes out of a building, it should not draw behind the building.
	};


	//Variables that we use to access texture data
	sampler2D _MainTex;
	sampler2D _IRTex;
	sampler2D _UVTex;
	sampler2D _CameraDepthTexture;

	float4 _piw = float4(0, 0, 0, 0); //position of object in world
	float4 _viw = float4(0, 0, 0, 0); //velocity of object in world
	float4 _aviw = float4(0, 0, 0, 0); //scaled angular velocity
	float4 _vpc = float4(0, 0, 0, 0); //velocity of player
									  //float _gtt = 1; //velocity of player
	float4 _playerOffset = float4(0, 0, 0, 0); //player position in world
	float _spdOfLight = 100; //current speed of light
	float _wrldTime = 0; //current time in world
	float _strtTime = 0; //starting time in world
	float _colorShift = 1; //actually a boolean, should use color effects or not ( doppler + spotlight). 

	float xyr = 1; // xy ratio
	float xs = 1; // x scale

	uniform float4 _MainTex_TexelSize;
	uniform float4 _CameraDepthTexture_ST;

	//Per vertex operations
	v2f vert(appdata_img v)
	{
		v2f o;
		o.pos = v.vertex;
		o.uv1.xy = v.texcoord; //get the UV coordinate for the current vertex, will be passed to fragment shade

		float speed = sqrt(dot(_vpc, _vpc));
		//vw + vp/(1+vw*vp/c^2)


		float vuDot = dot(_vpc, _viw); //Get player velocity dotted with velocity of the object.
		float4 uparra;
		//IF our speed is zero, this parallel velocity component will be NaN, so we have a check here just to be safe
		if (speed > divByZeroCutoff)
		{
			uparra = (vuDot / (speed*speed)) * _vpc; //Get the parallel component of the object's velocity
		}
		//If our speed is nearly zero, it could lead to infinities, so treat is as exactly zero, and set parallel velocity to zero
		else
		{
			speed = 0;
			uparra = _viw;
		}
		//Get the perpendicular component of our velocity, just by subtraction
		float4 uperp = _viw - uparra;
		//relative velocity calculation
		float4 vr = (_vpc - uparra - (sqrt(1 - speed*speed))*uperp) / (1 + vuDot);

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
		o.draw = 1;

		o.pos = UnityObjectToClipPos(o.pos);

		return o;
	}

	//Color functions, there's no check for division by 0 which may cause issues on
	//some graphics cards.
	float3 RGBToXYZC(float r, float g, float b)
	{
		float3 xyz;
		xyz.x = 0.13514*r + 0.120432*g + 0.057128*b;
		xyz.y = 0.0668999*r + 0.232706*g + 0.0293946*b;
		xyz.z = 0.0*r + 0.0000218959*g + 0.358278*b;
		return xyz;
	}
	float3 XYZToRGBC(float x, float y, float z)
	{
		float3 rgb;
		rgb.x = 9.94845*x - 5.1485*y - 1.16389*z;
		rgb.y = -2.86007*x + 5.77745*y - 0.0179627*z;
		rgb.z = 0.000174791*x - 0.000353084*y + 2.79113*z;

		return rgb;
	}
	float3 weightFromXYZCurves(float3 xyz)
	{
		float3 returnVal;
		returnVal.x = 0.0735806 * xyz.x - 0.0380793 * xyz.y - 0.00860837 * xyz.z;
		returnVal.y = -0.0665378 * xyz.x + 0.134408 * xyz.y - 0.000417865 * xyz.z;
		returnVal.z = 0.00000299624 * xyz.x - 0.00000605249 * xyz.y + 0.0484424 * xyz.z;
		return returnVal;
	}

	float getXFromCurve(float3 param, float shift)
	{
		float top1 = param.x * xla * exp((float)(-(pow((param.y*shift) - xlb, 2)
			/ (2 * (pow(param.z*shift, 2) + pow(xlc, 2))))))*sqrt((float)(float(2)*(float)3.14159265358979323));
		float bottom1 = sqrt((float)(1 / pow(param.z*shift, 2)) + (1 / pow(xlc, 2)));

		float top2 = param.x * xha * exp(float(-(pow((param.y*shift) - xhb, 2)
			/ (2 * (pow(param.z*shift, 2) + pow(xhc, 2))))))*sqrt((float)(float(2)*(float)3.14159265358979323));
		float bottom2 = sqrt((float)(1 / pow(param.z*shift, 2)) + (1 / pow(xhc, 2)));

		return (top1 / bottom1) + (top2 / bottom2);
	}
	float getYFromCurve(float3 param, float shift)
	{
		float top = param.x * ya * exp(float(-(pow((param.y*shift) - yb, 2)
			/ (2 * (pow(param.z*shift, 2) + pow(yc, 2))))))*sqrt(float(float(2)*(float)3.14159265358979323));
		float bottom = sqrt((float)(1 / pow(param.z*shift, 2)) + (1 / pow(yc, 2)));

		return top / bottom;
	}

	float getZFromCurve(float3 param, float shift)
	{
		float top = param.x * za * exp(float(-(pow((param.y*shift) - zb, 2)
			/ (2 * (pow(param.z*shift, 2) + pow(zc, 2))))))*sqrt(float(float(2)*(float)3.14159265358979323));
		float bottom = sqrt((float)(1 / pow(param.z*shift, 2)) + (1 / pow(zc, 2)));

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
		float x1 = i.pos.x * 2 * xs;
		float y1 = i.pos.y * 2 * xs / xyr;
		float z1 = i.pos.z;

		// ( 1 - (v/c)cos(theta) ) / sqrt ( 1 - (v/c)^2 )
		float shift = (1 - ((x1*i.vr.x + y1*i.vr.y + z1*i.vr.z) / sqrt(x1 * x1 + y1 * y1 + z1 * z1))) / i.svc;
		if (_colorShift == 0)
		{
			shift = 1.0f;
		}
		//Get initial color 
		float4 data = tex2D(_MainTex, i.uv1).rgba;
		float UV = tex2D(_UVTex, i.uv1).r;
		float IR = tex2D(_IRTex, i.uv1).r;

		//Set alpha of drawing pixel to 0 if vertex shader has determined it should not be drawn.
		data.a = i.draw ? data.a : 0;

		float3 rgb = data.xyz;




		//Color shift due to doppler, go from RGB -> XYZ, shift, then back to RGB.
		float3 xyz = RGBToXYZC(float(rgb.x),float(rgb.y),float(rgb.z));
		float3 weights = weightFromXYZCurves(xyz);
		float3 rParam,gParam,bParam,UVParam,IRParam;
		rParam.x = weights.x; rParam.y = (float)615; rParam.z = (float)8;
		gParam.x = weights.y; gParam.y = (float)550; gParam.z = (float)4;
		bParam.x = weights.z; bParam.y = (float)463; bParam.z = (float)5;
		UVParam.x = 0.02; UVParam.y = UV_START + UV_RANGE*UV; UVParam.z = (float)5;
		IRParam.x = 0.02; IRParam.y = IR_START + IR_RANGE*IR; IRParam.z = (float)5;

		float xf = pow((1 / shift),3)*(getXFromCurve(rParam, shift) + getXFromCurve(gParam,shift) + getXFromCurve(bParam,shift) + getXFromCurve(IRParam,shift) + getXFromCurve(UVParam,shift));
		float yf = pow((1 / shift),3)*(getYFromCurve(rParam, shift) + getYFromCurve(gParam,shift) + getYFromCurve(bParam,shift) + getYFromCurve(IRParam,shift) + getYFromCurve(UVParam,shift));
		float zf = pow((1 / shift),3)*(getZFromCurve(rParam, shift) + getZFromCurve(gParam,shift) + getZFromCurve(bParam,shift) + getZFromCurve(IRParam,shift) + getZFromCurve(UVParam,shift));

		float3 rgbFinal = XYZToRGBC(xf,yf,zf);
		rgbFinal = constrainRGB(rgbFinal.x,rgbFinal.y, rgbFinal.z); //might not be needed

		return float4((float)rgbFinal.x,(float)rgbFinal.y,(float)rgbFinal.z,data.a); //use me for any real build
	}

		ENDCG

		Subshader {

		Pass{
			//Shader properties, for things such as transparency
			Cull Off ZWrite On
			ZTest LEqual
			Fog{ Mode off } //Fog does not shift properly and there is no way to do so with this fog
			Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" }

			AlphaTest Greater[_Cutoff]
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM

#pragma fragmentoption ARB_precision_hint_nicest
#pragma vertex vert
#pragma fragment frag
#pragma target 3.0

			ENDCG
		}
	}

	Fallback "Unlit/Transparent"

} // shader
