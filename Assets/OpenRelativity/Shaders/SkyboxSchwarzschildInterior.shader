Shader "Relativity/Skybox/SchwarzschildInterior/6 Sided" {
    Properties{
        _Tint("Tint Color", Color) = (.5, .5, .5, .5)
        [Gamma] _Exposure("Exposure", Range(0, 8)) = 1.0
        _Rotation("Rotation", Range(0, 360)) = 0
        [NoScaleOffset] _FrontTex("Front [+Z]   (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _BackTex("Back [-Z]   (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _LeftTex("Left [+X]   (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _RightTex("Right [-X]   (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _UpTex("Up [+Y]   (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _DownTex("Down [-Y]   (HDR)", 2D) = "grey" {}
        _playerDist("Player Distance", float) = .5
        _lensRadius("Lens Schwarzschild Radius", float) = 1.0
    }

    CGINCLUDE
#pragma exclude_renderers xbox360
#pragma glsl

#include "UnityCG.cginc"

#define PI_2 1.57079632679489661923

//Color shift variables, used to make Gaussians for XYZ curves
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

#define bFac 0.5f
#define rFac 0.9f

        half4 _Tint;
        half _Exposure;
        float _Rotation;
        float _playerDist;
        float _lensRadius;

        struct VertexData {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Interpolators {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Interpolators vert(VertexData v) {
            Interpolators i;
            i.pos = UnityObjectToClipPos(v.vertex);
            i.uv = v.uv;
            return i;
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
            float paramYShift = param.y * shift;

            float top1 = param.x * xla * exp(-(((paramYShift - xlb) * (paramYShift - xlb))
                / (2 * (bottom2 + (xlc * xlc))))) * sqrt2Pi;
            float bottom1 = sqrt(1 / bottom2 + 1 / (xlc * xlc));

            float top2 = param.x * xha * exp(-(((paramYShift - xhb) * (paramYShift - xhb))
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
                / (2 * (bottom + zc * zc)))) * sqrt2Pi;
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

        }

        float3 RotateAroundYInDegrees(float3 vertex, float degrees)
        {
            float alpha = degrees * UNITY_PI / 180.0;
            float sina, cosa;
            sincos(alpha, sina, cosa);
            float2x2 m = float2x2(cosa, -sina, sina, cosa);
            return float3(mul(m, vertex.xz), vertex.y).xzy;
        }

        struct appdata_t {
            float4 vertex : POSITION;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct v2f {
            float4 vertex : SV_POSITION;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        v2f vert(appdata_t v)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 rotated = RotateAroundYInDegrees(v.vertex, _Rotation);
            o.vertex = UnityObjectToClipPos(rotated);
            o.texcoord = v.texcoord;
            return o;
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

			if (shift == 0) {
				shift = 1;
			}

			float3 xyz = RGBToXYZC(rgb);
			float3 weights = weightFromXYZCurves(xyz);
			float3 rParam, gParam, bParam, UVParam, IRParam;
			rParam = float3(weights.x, 615, 8);
			gParam = float3(weights.y, 550, 4);
			bParam = float3(weights.z, 463, 5);
			UVParam = float3(0.02f, UV_START + UV_RANGE * UV, 5);
			IRParam = float3(0.02f, IR_START + IR_RANGE * IR, 5);

			xyz = float3(
				(getXFromCurve(rParam, shift) + getXFromCurve(gParam, shift) + getXFromCurve(bParam, shift) + getXFromCurve(IRParam, shift) + getXFromCurve(UVParam, shift)),
				(getYFromCurve(rParam, shift) + getYFromCurve(gParam, shift) + getYFromCurve(bParam, shift) + getYFromCurve(IRParam, shift) + getYFromCurve(UVParam, shift)),
				(getZFromCurve(rParam, shift) + getZFromCurve(gParam, shift) + getZFromCurve(bParam, shift) + getZFromCurve(IRParam, shift) + getZFromCurve(UVParam, shift)));

            // See this link for criticism that suggests this should be the fifth power, rather than the third:
		    // https://physics.stackexchange.com/questions/43695/how-realistic-is-the-game-a-slower-speed-of-light#answer-587149
			return constrainRGB(XYZToRGBC(pow(1 / shift, 5) * xyz));
		}

        float4 skybox_frag(v2f i, sampler2D smp, half4 smpDecode) {
            half4 tex = tex2D(smp, i.texcoord);
            half3 c = DecodeHDR(tex, smpDecode);
            c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb;
            c *= _Exposure;

            float shift = 1 / sqrt(_lensRadius / _playerDist - 1);

            return float4(DopplerShift(c, bFac * c.b, rFac * c.r, shift), tex.w);
        }

        ENDCG

        Subshader {
            Tags{ "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
            Cull Off ZWrite Off
            
            Pass {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0
                sampler2D _FrontTex;
                half4 _FrontTex_HDR;
                half4 frag(v2f i) : SV_Target { return skybox_frag(i,_FrontTex, _FrontTex_HDR); }
                ENDCG
            }
            Pass{
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0
                sampler2D _BackTex;
                half4 _BackTex_HDR;
                half4 frag(v2f i) : SV_Target { return skybox_frag(i,_BackTex, _BackTex_HDR); }
                ENDCG
            }
            Pass{
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0
                sampler2D _LeftTex;
                half4 _LeftTex_HDR;
                half4 frag(v2f i) : SV_Target { return skybox_frag(i,_LeftTex, _LeftTex_HDR); }
                ENDCG
            }
            Pass{
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0
                sampler2D _RightTex;
                half4 _RightTex_HDR;
                half4 frag(v2f i) : SV_Target { return skybox_frag(i,_RightTex, _RightTex_HDR); }
                ENDCG
            }
            Pass{
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0
                sampler2D _UpTex;
                half4 _UpTex_HDR;
                half4 frag(v2f i) : SV_Target { return skybox_frag(i,_UpTex, _UpTex_HDR); }
                ENDCG
            }
            Pass{
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0
                sampler2D _DownTex;
                half4 _DownTex_HDR;
                half4 frag(v2f i) : SV_Target { return skybox_frag(i,_DownTex, _DownTex_HDR); }
                ENDCG
            }
        }
}