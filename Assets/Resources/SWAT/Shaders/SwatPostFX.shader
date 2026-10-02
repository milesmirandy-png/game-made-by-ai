// Restrained post-processing for the Built-in render pipeline, applied by
// Scripts/Core/PostEffects.cs through OnRenderImage: a cheap bloom (threshold,
// quarter-resolution blur), color grading (exposure, contrast, saturation,
// shadow/highlight tint) and a mild vignette. No motion blur, depth of
// field, grain or chromatic aberration.
Shader "Hidden/SWAT/PostFX"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float _Threshold;
    float _BloomIntensity;
    float _BlurOffset;
    float _Exposure;
    float _Contrast;
    float _Saturation;
    float4 _Lift;
    float4 _Gain;
    float _VignetteStrength;
    float _VignetteSize;

    struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

    v2f vert (appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
    }

    float Luma(float3 c)
    {
        return dot(c, float3(0.2126, 0.7152, 0.0722));
    }

    // Downsample with four taps and keep only the bright parts.
    float4 fragPrefilter (v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        float3 c = tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).rgb;
        c += tex2D(_MainTex, i.uv + float2(d.x, -d.y)).rgb;
        c += tex2D(_MainTex, i.uv + float2(-d.x, d.y)).rgb;
        c += tex2D(_MainTex, i.uv + float2(d.x, d.y)).rgb;
        c *= 0.25;
        float bright = max(Luma(c) - _Threshold, 0.0) / max(Luma(c), 0.0001);
        return float4(c * bright, 1.0);
    }

    float4 fragBlur (v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy * _BlurOffset;
        float3 c = tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).rgb;
        c += tex2D(_MainTex, i.uv + float2(d.x, -d.y)).rgb;
        c += tex2D(_MainTex, i.uv + float2(-d.x, d.y)).rgb;
        c += tex2D(_MainTex, i.uv + float2(d.x, d.y)).rgb;
        return float4(c * 0.25, 1.0);
    }

    float4 fragComposite (v2f i) : SV_Target
    {
        float4 src = tex2D(_MainTex, i.uv);
        float2 bloomUV = i.uv;
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0.0) bloomUV.y = 1.0 - bloomUV.y;
        #endif
        float3 c = src.rgb + tex2D(_BloomTex, bloomUV).rgb * _BloomIntensity;

        c *= _Exposure;
        // Tint shadows and highlights separately (cool shadows, neutral highlights).
        float l = saturate(Luma(c));
        c = c * lerp(_Lift.rgb, _Gain.rgb, l);
        c = (c - 0.5) * _Contrast + 0.5;
        float gray = Luma(c);
        c = lerp(float3(gray, gray, gray), c, _Saturation);

        float2 centered = i.uv - 0.5;
        float vignette = 1.0 - _VignetteStrength * smoothstep(_VignetteSize, 1.0, length(centered) * 1.414);
        c *= vignette;
        return float4(saturate(c), src.a);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            ENDCG
        }
    }
    Fallback Off
}
