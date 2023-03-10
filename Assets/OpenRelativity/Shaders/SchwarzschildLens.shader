Shader "Relativity/SchwarzschildLens"
{
	Properties
	{
		_MainTex("Source", 2D) = "white" {}
		_playerDist("Player Distance", float) = 0
		_playerAngle("Player Angle", float) = 0
		_lensRadius("Lens Schwarzschild Radius", float) = 0
		_lensUPos("Lens Position (U)", float) = 0
		_lensVPos("Lens Position (V)", float) = 0
		_frustumWidth("Frustum Width", float) = 0
		_frustumHeight("Frustum Height", float) = 0
		_lensSpinFrac("Lens Spin Extremal Fraction", float) = 0
		_lensSpinColat("Lens Spin Colatitude", float) = 0
		_lensSpinTilt("Lens Spin Tilt", float) = 0
		_lensTex("Lens-Pass Texture", 2D) = "black" {}
		[Toggle] _isMirror("Gravity Mirror", float) = 0
		[Toggle] _hasEventHorizon("Block event horizon", float) = 0
		_cameraScale("Camera Scale", float) = 1
	}

	CGINCLUDE
#pragma exclude_renderers xbox360
#pragma glsl

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

	sampler2D _MainTex;
	sampler2D _lensTex;
	float _playerDist, _playerAngle, _lensRadius;
	float _lensSpinFrac, _lensSpinColat, _lensSpinTilt;
	float _lensUPos, _lensVPos;
	float _frustumWidth, _frustumHeight;
	float _isMirror;
	float _hasEventHorizon;
	float _cameraScale;

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

	float4 frag(Interpolators i) : SV_Target{
		float3 sourceColor = float3(0, 0, 0);

		float2 lensUVPos = float2(_lensUPos, _lensVPos);
		float2 frustumSize = float2(_frustumWidth, _frustumHeight);
		float2 lensPlaneCoords = (i.uv - lensUVPos) * frustumSize;
		float r = length(lensPlaneCoords);

		// Minimum impact paramater should be the Schwarzschild radius. Anything less would be trapped.
		if (!_hasEventHorizon || r > _lensRadius || _playerAngle > PI_2) {
			float sourceAngle = atan2(r, _playerDist);
			float deflectionAngle = 2 * (_lensRadius / r) * cos(_playerAngle / 2) / _cameraScale;
			float spinAngle = deflectionAngle * _lensSpinFrac;

			float cosTilt = cos(_lensSpinTilt);
			float sinTilt = sin(_lensSpinTilt);
			float rProjTilt = dot(lensPlaneCoords, float2(cosTilt, sinTilt));
			// If spin is up, observing right-hand rule, -x side is spinning TOWARD player. This causes it to deflect MORE; this is -tan(sourceAngle) * spinAngle,
			// projected on the equator.
			float spinSourceAngleAdj = (rProjTilt / _playerDist) * spinAngle;

			spinAngle *= cos(_lensSpinColat);
			spinSourceAngleAdj *= sin(_lensSpinColat) * cosTilt;

			uint inversionCount = abs(deflectionAngle) / PI_2;
			if ((_playerAngle > PI_2 ||
				!(_hasEventHorizon && deflectionAngle >= PI_2))
				&& inversionCount % 2 == (_isMirror < 0.5 ? 0 : 1))
			{
				lensPlaneCoords = _playerDist * tan(sourceAngle + spinSourceAngleAdj - deflectionAngle)  * lensPlaneCoords / r;
				float cosSpin = cos(spinAngle);
				float sinSpin = sin(spinAngle);
				lensPlaneCoords = float2(cosSpin * lensPlaneCoords.x - sinSpin * lensPlaneCoords.y, sinSpin * lensPlaneCoords.x + cosSpin * lensPlaneCoords.y);
				float2 uvProj = lensPlaneCoords / frustumSize;
				float scale = length(i.uv - lensUVPos) / length(uvProj);
				uvProj = (uvProj + lensUVPos);
				uvProj = (uvProj - float2(0.5f, 0.5f)) * _cameraScale + float2(0.5f, 0.5f);
				uvProj *= scale;
				float4 s = float4(uvProj, 0, scale);
				sourceColor = tex2Dproj(_MainTex, UNITY_PROJ_COORD(s)).rgb;
			}
			else if (_isMirror >= 0.5f) {
				sourceColor = tex2D(_lensTex, i.uv).rgb;
			}
		}

		float shift = sqrt(1 - _lensRadius / _playerDist);

		//Color shift due to doppler, go from RGB -> XYZ, shift, then back to RGB.
		// Ignore IR and UV
		// Doppler shift due to player velocity is handled in skybox shader
		
		float3 xyz = RGBToXYZC(sourceColor);
		float3 weights = weightFromXYZCurves(xyz);
		float3 rParam, gParam, bParam, UVParam, IRParam;
		rParam.x = weights.x; rParam.y = (float)615; rParam.z = (float)8;
		gParam.x = weights.y; gParam.y = (float)550; gParam.z = (float)4;
		bParam.x = weights.z; bParam.y = (float)463; bParam.z = (float)5;

		xyz.x = getXFromCurve(rParam, shift) + getXFromCurve(gParam, shift) + getXFromCurve(bParam, shift);
		xyz.y = getYFromCurve(rParam, shift) + getYFromCurve(gParam, shift) + getYFromCurve(bParam, shift);
		xyz.z = getZFromCurve(rParam, shift) + getZFromCurve(gParam, shift) + getZFromCurve(bParam, shift);
		// See this link for criticism that suggests this should be the fifth power, rather than the third:
		// https://physics.stackexchange.com/questions/43695/how-realistic-is-the-game-a-slower-speed-of-light#answer-587149
		sourceColor = XYZToRGBC(pow(1 / shift, 5) * xyz);

		return float4(sourceColor, 1);
	}

	ENDCG

	Subshader {
		Pass{
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
}
