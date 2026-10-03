// "Sprite" lighting for characters in the pixel-art style (Built-in pipeline):
// three flat light bands instead of smooth shading, cool-tinted shadows, a
// brighter top face (heads and shoulders read well from above) and a thin rim
// light. Lamps, flashlights and muzzle flashes are banded too. Shadows come
// from the VertexLit fallback's shadow caster pass.
Shader "SWAT/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ShadowTint ("Shadow tint", Color) = (0.62,0.66,0.86,1)
        _TopLight ("Top face highlight", Range(0,1)) = 0.22
        _Rim ("Rim light", Range(0,1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        CGINCLUDE
        fixed4 _Color;
        fixed4 _ShadowTint;
        float _TopLight;
        float _Rim;

        // Hard steps: lit, half-lit and shade, so each face of a block gets one flat tone.
        float Band(float amount)
        {
            return amount > 0.5 ? 1.0 : (amount > 0.12 ? 0.55 : 0.0);
        }
        ENDCG

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                half3 ambient : TEXCOORD2;
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.ambient = ShadeSH9(half4(o.worldNormal, 1.0));
                TRANSFER_SHADOW(o)
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float shadow = SHADOW_ATTENUATION(i);
                float band = Band(saturate(dot(n, l)) * shadow);
                float3 view = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float rim = pow(1.0 - saturate(dot(n, view)), 4.0) * _Rim;
                float top = n.y > 0.7 ? _TopLight : 0.0;
                float3 ambient = i.ambient * lerp(_ShadowTint.rgb, float3(1.0, 1.0, 1.0), band * 0.6);
                float3 light = _LightColor0.rgb * band;
                float3 c = _Color.rgb * (ambient + light) * (1.0 + top) + (ambient + light) * rim;
                fixed4 color = fixed4(c, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ForwardAdd" }
            Blend One One
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                LIGHTING_COORDS(2, 3)
                UNITY_FOG_COORDS(4)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 l = normalize(UnityWorldSpaceLightDir(i.worldPos));
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                float band = Band(saturate(dot(n, l)) * atten);
                fixed4 color = fixed4(_Color.rgb * _LightColor0.rgb * band, 1.0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, color, fixed4(0.0, 0.0, 0.0, 0.0));
                return color;
            }
            ENDCG
        }
    }
    FallBack "VertexLit"
}
