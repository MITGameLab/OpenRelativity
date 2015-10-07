Shader "Colors/Desaturate" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "" {}
	}
	
	CGINCLUDE
// Upgrade NOTE: excluded shader from DX11 and Xbox360; has structs without semantics (struct v2f members uv1)
#pragma exclude_renderers d3d11 xbox360
		
	#include "UnityCG.cginc"
	
	struct v2f {
		float4 pos : POSITION;
		float2 uv1;
	};
		
	sampler2D _MainTex;
	sampler2D _CameraDepthTexture;	 
	uniform float4 _MainTex_TexelSize;
	uniform float4 _CameraDepthTexture_ST;

	float3 hsv_to_rgb(float3 HSV)
	{
		HSV.x /= 360;
		float var_h = HSV.x * 6;

		float var_1 = HSV.z * ( 1.0 - HSV.y );
		float var_2 = HSV.z * ( 1.0 - HSV.y * (var_h-floor( var_h )));
		float var_3 = HSV.z * ( 1.0 - HSV.y * (1-(var_h-floor( var_h ))));

		float3 RGB = float3(HSV.z, var_1, var_2);

		if (var_h < 5)    { RGB = float3(var_3, var_1, HSV.z); }
		if (var_h < 4)    { RGB = float3(var_1, var_2, HSV.z); }
		if (var_h < 3)    { RGB = float3(var_1, HSV.z, var_3); }
		if (var_h < 2)    { RGB = float3(var_2, HSV.z, var_1); }
		if (var_h < 1)    { RGB = float3(HSV.z, var_3, var_1); }

		return (RGB);
	}

	float3 rgb_to_hsv( float3 rgb )
	{
		float min, max, delta;
		float3 hsv;

	    min = rgb.x <= rgb.y ? rgb.x  : rgb.y;
		min = min <= rgb.z ? min : rgb.z;
		max = rgb.x >= rgb.y ? rgb.x : rgb.y;
		max = max >= rgb.z ? max : rgb.z;

		hsv.z = max; 
		delta = max - min;
		if( max != 0 )
		{
			hsv.y = delta / max;
		}
		else 
		{
			// r = g = b = 0  // s = 0, v is undefined
			hsv.y = 0;
			hsv.x = 0;
			return hsv;
		}
		if( rgb.x == max )
			hsv.x = ( rgb.y - rgb.z ) / delta;		// between yellow & magenta
		else if( rgb.y == max )
			hsv.x = 2 + ( rgb.z - rgb.x ) / delta;	// between cyan & yellow
		else
			hsv.x = 4 + ( rgb.x - rgb.y ) / delta;	// between magenta & cyan
		hsv.x *= 60;				// degrees
		if( hsv.x < 0 )
			hsv.x += 360;
		return hsv;
	}
		
	//For the screen, this function is called a total of 4 times, as the screen renders to a square of 4 verticies.
	v2f vert( appdata_img v ) {
		v2f o;
		
		o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
		o.uv1.xy = v.texcoord;
		
		//You need this otherwise the screen flips and weird stuff happens
		#ifdef SHADER_API_D3D9
		if (_MainTex_TexelSize.y < 0)
			 o.uv1.y =  o.uv1.y;
		#endif
		
		return o;
	}
	
	//Per pixel or something like that
	float4 frag(v2f i) : COLOR 
	{	
		//Grab initial color
		half4 color = (half4)tex2D(_MainTex, i.uv1).rgba;

		float3 hsv = rgb_to_hsv(float3(color.x,color.y,color.z));
		hsv.y *= 0.5;
		float3 rgb = hsv_to_rgb(hsv);
		
		
		
		
		return float4(rgb.x,rgb.y,rgb.z,0);
	}

	ENDCG
	
Subshader {
	
 Pass {
	  ZTest Always Cull Off ZWrite Off
	  Fog { Mode off }      

      CGPROGRAM
      
      #pragma fragmentoption ARB_precision_hint_fastest   
      
      #pragma vertex vert
      #pragma fragment frag
	  #pragma target 3.0
      
      ENDCG
  }
}

Fallback off
	
} // shader