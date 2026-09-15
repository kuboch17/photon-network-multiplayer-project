Shader "Tzar/Advanced Vertical Fog"
{
	Properties
	{	[Header(MAIN)]
		[Header(Texture and Color)]
		[Space]
		_MainTex ("Main Texture", 2D) = "white" {}
		_Color("Color", Color) = (1, 1, 1, 0.5)
		[PowerSlider(1.0)]_IntersectionThresholdMax("Fog Intensity", Range(0, 2)) = 0.5
		[Space]
		_Alpha("Alpha", Range(-1.0, 1)) = 1
		[Toggle]_Invert("Invert Color", Float) = 0
		[Space]


		[Header(Cookie)]
		[Toggle]_UseCookie("Enable Cookie", Float) = 0
		_Cookie("Cookie", 2D) = "white" {}
		_CookieStrength("Cookie Alpha", Range(0, 1)) = 1
		[Space]


		[Header(Movement)]
		[Toggle]_Rotation("Rotate", Float) = 0
		_RotationSpeed("Rotation Speed", Range(-1, 1)) = 0
		_OriginX("Origin X", Range(-2, 2)) = 0.0									
		_OriginY("Origin Y", Range(-2, 2)) = 0.0
		[Space(30)]

		[Header(DISTORTION)]
		[Header(Distortion Texture)]
		_DistortTex("Texture", 2D) = "white" {}		
		[Space]
		[Toggle]_UseMainDistort("Override Texture - Use Main Texture", Float) = 0
		_MainDistortAmount("Main Texture Distortion Amount", Range(0, 1)) = 1
		[Space]

		[Toggle]_DistortCookie("Override Texture - Use Cookie Texture", Float) = 0
		_DistortCookieAmount("Cookie Distortion Amount", Range(0, 1)) = 1
		[Space]
		[Toggle]_MainDistort("Distort Main Texture", Float) = 0
		[Header(Distortion Values)]
	
		_Magnitude("Magnitude", Range(0, 10)) = 0
		[PowerSlider(2.0)]_DistortSpeed("Speed", Range(0, 2)) = 0									
		_Period("Period", Range(-3, 3)) = 1
		_Offset("Period Offset", Range(0, 15)) = 0		
		[Space]
																	
		[Header(Distortion Movement)]
		[Toggle]_DistortionRotation("Rotate", Float) = 0
		_DistortRotationSpeed("Rotation Speed", Range(-2, 2)) = 0
		[Space]
		[Toggle]_Translate("Move", Float) = 0								
					
		_SpeedX("X Speed", Range(-0.5, 0.5)) = 0

		_SpeedY("Y Speed", Range(-0.5, 0.5)) = 0
		[Header(Debug)]
		[Toggle]_TestDistortion("Show Distortion Texture", Float) = 0


		/*[Header(Misc)]
		[Toggle]_FogOrder("Apply Fog Before Main Texture?", Float) = 0			*/
		
		
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		LOD 100

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;				
				float2 uv2 : TEXCOORD1;				
													
			};										

			struct v2f
			{
				
				float2 uv : TEXCOORD0;
				float2 uv2 : TEXCOORD1;
				float4 scrPos : TEXCOORD2;			
				UNITY_FOG_COORDS(3)					
				float4 vertex : SV_POSITION;
				
				
			};

			float2 sinusoid(float2 x, float2 m, float2 M, float2 p)		
			{
				float2 e = M - m;
				float2 c = 3.1415 * 2.0 / p;
				return e / 2.0 * (1.0 + sin(x * c)) + m;
			}

			

			sampler2D _CameraDepthTexture;
			float4 _Color;
			float _IntersectionThresholdMax;
			float _Magnitude;
			float _Period;
			float _Offset;
			float _DistortRotationSpeed;
			float _Translate;
			float _TestDistortion;
			float _FogOrder;
			float _MainDistort;
			float _DistortSpeed;
			float _OriginX;
			float _OriginY;
			float _Invert;
			float _Alpha;
			float _CookieStrength;
			float _UseCookie;
			float _Rotation;
			float _DistortionRotation;
			float _RotationSpeed;
			float _MainDistortAmount;
			float _DistortCookie;
			float _DistortCookieAmount;
			float _SpeedX;
			float _SpeedY;
			float _UseMainDistort;
			
			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			sampler2D _Cookie;
			
							
			sampler2D _DistortTex;
			float4 _DistortTex_ST;
		
			v2f vert (appdata v)
			{
				
				v2f o;
				
				o.vertex = UnityObjectToClipPos(v.vertex);
				
				if(_Translate == 1.0)						
				{												

					
					float2 scrollCoords = float2(_SpeedX, _SpeedY) * _Time[1];
					
					if(_UseMainDistort == 1.0)
					{
						_MainTex_ST.zw = scrollCoords;
					}
					else
					{
						_DistortTex_ST.zw = scrollCoords;				
					}
				}

				if(_Rotation == 1.0)					
				{
					float SinX = sin(_RotationSpeed * _Time[1]);		
					float CosX = cos(_RotationSpeed * _Time[1]);
				

					float2x2 rotationMatrix = float2x2( CosX, -SinX, SinX, CosX);	

					v.uv.x += (_OriginX + 0.5) * -1.0;			
					v.uv.y += (_OriginY + 0.5) * -1.0;			

					v.uv.xy = mul ( v.uv.xy, rotationMatrix );	
				}
				
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);

				if(_DistortionRotation == 1.0)				
				{															
					float sinX = sin(_DistortRotationSpeed * _Time[1]);		
					float cosX = cos(_DistortRotationSpeed * _Time[1]);
				

					float2x2 rotationMatrix = float2x2( cosX, -sinX, sinX, cosX);	

					v.uv2.x += (_OriginX + 0.5) * -1.0;
					v.uv2.y += (_OriginY + 0.5) * -1.0;

					v.uv2.xy = mul ( v.uv2.xy, rotationMatrix );
				}
				
				if(_UseMainDistort == 1.0)
				{
					o.uv2 = TRANSFORM_TEX(v.uv2, _MainTex);		
				}
				else
				{
					o.uv2 = TRANSFORM_TEX(v.uv2, _DistortTex);
				}

				o.scrPos = ComputeScreenPos(o.vertex);			
				UNITY_TRANSFER_FOG(o, o.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				if(_Rotation == 1.0)
				{
					i.uv.x += 0.5;		
					i.uv.y += 0.5;		
				}
				
				if (_DistortionRotation == 1.0)
				{
					i.uv2.x += 0.5;
					i.uv2.y += 0.5;
				}

				fixed4 distort;
				
				if (_UseMainDistort == 1.0)
				{
					distort = tex2D(_MainTex, i.uv2);
				}
				else
				{
					distort = tex2D(_DistortTex, i.uv2);
				}

				if(_DistortCookie == 1.0)
				{
					fixed4 DistortCookie = tex2D(_Cookie, i.uv2);
					distort *= saturate(DistortCookie + (1.0 - _DistortCookieAmount));
				}
				fixed4 MainTex;			
							
				
				float time1 = sin(_Time[1] * 5.0 * _DistortSpeed) + _Offset;	
				float time2 = cos(_Time[1] * 5.0 * _DistortSpeed) + _Offset;	
				float MainX;
				float MainY;

				if(_MainDistort == 1.0)		
				{
					MainTex = tex2D(_MainTex, i.uv2);
					MainX = MainTex.x * _MainDistortAmount;
					MainY = MainTex.y * _MainDistortAmount;
				}
				else
				{
					MainX = 0.0;
					MainY = 0.0;
				}


				float2 Displacement = sinusoid
				(
					float2(time1 * (distort.x + MainX), time2 * (distort.y + MainY)),	
					float2(-_Magnitude * 0.001, -_Magnitude * 0.001),					
					float2(_Magnitude * 0.001, _Magnitude * 0.001),						
					float2(_Period, _Period)
				);

				
				i.uv.xy += Displacement;					
				MainTex = tex2D(_MainTex, i.uv);				

				
				if(_Invert == 1.0)								
					{
						MainTex.r = 1.0 - MainTex.r;
						MainTex.g = 1.0 - MainTex.g;
						MainTex.b = 1.0 - MainTex.b;
					}
				
				if(_UseCookie == 1.0)
				{
					fixed4 Cookie = tex2D(_Cookie, i.uv); 
					
					MainTex.rgb *= saturate(Cookie + (1.0 - _CookieStrength));	
																				
				}	

				float depth = LinearEyeDepth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.scrPos)));	
				float diff = saturate(_IntersectionThresholdMax * (depth - i.scrPos.w));					
																											

				fixed4 col = lerp(fixed4(_Color.rgb, 0.0), _Color, diff * diff * diff * diff * (diff * (6 * diff - 15) + 10));	

				if(_FogOrder == 1.0)
				{
					UNITY_APPLY_FOG(i.fogCoord, col);	
				}

				if(_TestDistortion == 1)				
				{
					col.rgb *= distort.rgb;
				}
				else
				{										
										
					
					float lum = Luminance(MainTex.rgb);
					
					col.rgb *= MainTex.rgb;					

					col.a *= saturate(lum + _Alpha);

				}

				if(_FogOrder == 0.0)
				{
					UNITY_APPLY_FOG(i.fogCoord, col);	
				}

				return col;
			}
			
			ENDCG
		}
	}
}
