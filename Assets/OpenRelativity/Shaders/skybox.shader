// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Relativity/SkyboxShift" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "" {}
		_UVTex("UV",2D) = "" {}
		_IRTex("IR",2D) = "" {}
		_viw ("viw", float)=0
	}
	
	CGINCLUDE
// Upgrade NOTE: excluded shader from Xbox360; has structs without semantics (struct v2f members pos2,uv1,svc,vr)	
#pragma exclude_renderers xbox360
	
	#pragma glsl
	#include "UnityCG.cginc"
	
	#define xla 0.39842970153455692
	#define xlb 444.50376680864167
	#define xlc -20.212233772937985

	#define xha 1.1305579611073924
	#define xhb 593.23109259420676
	#define xhc 34.446036264605638

	#define ya 1.0104130954965003
	#define yb 556.12431133891937
	#define yc 46.102600601714499

	#define za 2.0586397904795373
	#define zb 448.35859770333445
	#define zc -22.546254030641482

	#define IR_RANGE 400
	#define IR_START 700
	#define UV_RANGE 380
	#define UV_START 0

	 
	
	struct v2f {
		float4 pos : POSITION;
		float4 pos2 : TEXCOORD0;
		float2 uv1  : TEXCOORD1;
		float svc  : TEXCOORD2; 
		float4 vr  : TEXCOORD3;
	};
		
	sampler2D _MainTex;
	sampler2D _IRTex;
	sampler2D _UVTex;
	sampler2D _CameraDepthTexture;
	
	float4 _viw = float4(0,0,0,0); //velocity of object
	float4 _vpc = float4(0,0,0,0);
	float _spdOfLight = 100	;
	float4 _playerOffset = float4(0,0,0,0);
	float _colorShift = 1;
	
	float cx = 1; //cos x
	float cy = 1; // cos y
	float cz = 1; // cos z  
	float sx = 0; // sin x
	float sy = 0;  //  sin yv
	float sz = 0;  // sin z
	float xyr = 1; // xy ratio
	float xs = 1; // x scale
	 
	uniform float4 _MainTex_TexelSize;
	uniform float4 _CameraDepthTexture_ST;
		
	//For the screen, this function is called a total of 4 times, as the screen renders to a square of 4 verticies.
	v2f vert( appdata_img v ) {
		v2f o;
	   
		
		o.pos = UnityObjectToClipPos(v.vertex);
		o.pos2 = mul(unity_ObjectToWorld, v.vertex);
		
		o.pos2 -= _playerOffset;


		o.uv1.xy = v.texcoord;

	    float4 vr = _vpc - _viw;
		o.vr = vr;
		float s = sqrt( pow((vr.x),2) + pow((vr.y),2) + pow((vr.z),2));
		o.svc = sqrt( 1 - s * s); // To decrease number of operations in fragment shader 
		
		//You need this otherwise the screen flips and weird stuff happens
		#ifdef SHADER_API_D3D9
		if (_MainTex_TexelSize.y < 0)
			 o.uv1.y = 1.0- o.uv1.y;
		#endif 
		
		return o;
	}
	
	
	float3 RGBToXYZC(  float r,  float g,  float b)
	{
		float3 xyz;
		xyz.x = 0.135134*r + 0.120531*g + 0.0570346*b;
		xyz.y = 0.0669015*r + 0.23295*g + 0.0291481*b;
		xyz.z = 0.0*r + 0.0000247454*g + 0.358275*b;
		return xyz;
	}
	float3 XYZToRGBC(  float x,  float y,  float z)
	{
		float3 rgb;
		rgb.x = 9.94832*x -5.14725*y - 1.16493*z;
		rgb.y = -2.91664*x + 5.85296*y - 0.0379474*z;
		rgb.z = 0.000197335*x - 0.000398597*y + 2.79115*z;

		return rgb;
	}
	float3 weightFromXYZCurves(float3 xyz)
	{
		float3 returnVal;
		returnVal.x = 0.0735764 * xyz.x -0.0380683 * xyz.y - 0.00861569 * xyz.z;
		returnVal.y = -0.0665233 * xyz.x +  0.13437 * xyz.y - 0.000341907 * xyz.z;
		returnVal.z = 0.00000345602 * xyz.x - 0.0000069808 * xyz.y + 0.0485362 * xyz.z;
		return returnVal;
	}
	
	 float getXFromCurve(float3 param,  float shift)
	{
		 float top1 = param.x * xla * exp( (float)(-(pow((param.y*shift) - xlb,2)
			/(2*(pow(param.z*shift,2)+pow(xlc,2))))))*sqrt( (float)(float(2)*(float)3.14159265358979323));
		 float bottom1 = sqrt((float)(1/pow(param.z*shift,2))+(1/pow(xlc,2))); 

		 float top2 = param.x * xha * exp( float(-(pow((param.y*shift) - xhb,2)
			/(2*(pow(param.z*shift,2)+pow(xhc,2))))))*sqrt( (float)(float(2)*(float)3.14159265358979323));
		 float bottom2 = sqrt((float)(1/pow(param.z*shift,2))+(1/pow(xhc,2)));

		return (top1/bottom1) + (top2/bottom2);
	}
	 float getYFromCurve(float3 param,  float shift)
	{
		 float top = param.x * ya * exp( float(-(pow((param.y*shift) - yb,2)
			/(2*(pow(param.z*shift,2)+pow(yc,2))))))*sqrt( float(float(2)*(float)3.14159265358979323));
		 float bottom = sqrt((float)(1/pow(param.z*shift,2))+(1/pow(yc,2))); 

		return top/bottom;
	}

	 float getZFromCurve(float3 param,  float shift)
	{
		 float top = param.x * za * exp( float(-(pow((param.y*shift) - zb,2)
			/(2*(pow(param.z*shift,2)+pow(zc,2))))))*sqrt( float(float(2)*(float)3.14159265358979323));
		 float bottom = sqrt((float)(1/pow(param.z*shift,2))+(1/pow(zc,2)));

		return top/bottom;
	}
	
	float3 constrainRGB( float r,  float g,  float b)
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
		w = ( w < g) ? g : w;
		w = ( w < b) ? b : w;

		if ( w > 1 )
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
			
	//Per pixel or something like that
	float4 frag(v2f i) : COLOR 
	{
	
		float x1 = i.pos2.x * 2*xs;
		float y1 = i.pos2.y * 2*xs/xyr; 
		float z1 = i.pos2.z;
		
		float svc = (1-((x1*i.vr.x + y1*i.vr.y + z1*i.vr.z)/sqrt( x1 * x1 + y1 * y1 + z1 * z1)))/i.svc; 
	
		if(_colorShift == 0)
		{
			svc = 1.0f;
		}
		//Get initial color
		float3 rgb = tex2D (_MainTex, i.uv1).rgb;  
		float UV = tex2D( _UVTex, i.uv1).r;
		float IR = tex2D( _IRTex, i.uv1).r;
		
		float3 xyz = RGBToXYZC(float(rgb.x),float(rgb.y),float(rgb.z));
		float3 weights = weightFromXYZCurves(xyz);
		float3 rParam,gParam,bParam,UVParam,IRParam;
		rParam.x = weights.x; rParam.y = ( float) 615; rParam.z = ( float)8;
		gParam.x = weights.y; gParam.y = ( float) 550; gParam.z = ( float)4;
		bParam.x = weights.z; bParam.y = ( float) 463; bParam.z = ( float)5; 
		UVParam.x = 0.1; UVParam.y = UV_START + UV_RANGE*UV; UVParam.z = (float)1;
		IRParam.x = 0.1; IRParam.y = IR_START + IR_RANGE*IR; IRParam.z = (float)1;
		
		float xf = pow((1/svc),3)*(getXFromCurve(rParam, svc) + getXFromCurve(gParam,svc) + getXFromCurve(bParam,svc) + getXFromCurve(IRParam,svc) + getXFromCurve(UVParam,svc));
		float yf = pow((1/svc),3)*(getYFromCurve(rParam, svc) + getYFromCurve(gParam,svc) + getYFromCurve(bParam,svc) + getYFromCurve(IRParam,svc) + getYFromCurve(UVParam,svc));
		float zf = pow((1/svc),3)*(getZFromCurve(rParam, svc) + getZFromCurve(gParam,svc) + getZFromCurve(bParam,svc) + getZFromCurve(IRParam,svc) + getZFromCurve(UVParam,svc));
		
		float3 rgbFinal = XYZToRGBC(xf,yf,zf);
		//rgbFinal = constrainRGB(rgbFinal.x,rgbFinal.y, rgbFinal.z);

  		float4x4 temp  = mul(1.0*unity_ObjectToWorld, unity_WorldToObject);
		float4 temp2 = mul( temp,float4( (float)rgbFinal.x,(float)rgbFinal.y,(float)rgbFinal.z,1));
		//float4 temp2 =float4( (float)rgbFinal.x,(float)rgbFinal.y,(float)rgbFinal.z,1);
		return temp2; 
		//float a = sizeof(float);
		//return float4( yf - 0.1,0,0,1);

		
	}

	ENDCG
	
Subshader {
	
 Pass {
	  Cull Off ZWrite On
	  Fog { Mode off }     
	  Blend SrcAlpha OneMinusSrcAlpha

      CGPROGRAM
      
      #pragma fragmentoption ARB_precision_hint_nicest
      
      #pragma vertex vert
      #pragma fragment frag
	  #pragma target 3.0
      
      ENDCG
  }
}

Fallback "Unlit/Texture"
	
} // shader