// Restrained post-processing for the Built-in render pipeline, applied by
// Scripts/Core/PostEffects.cs through OnRenderImage: a cheap bloom (threshold,
// quarter-resolution blur), color grading (exposure, contrast, saturation,
// shadow/highlight tint) and a mild vignette. No motion blur, depth of
// field, grain or chromatic aberration. In the pixel-art style it also draws
// one-pixel dark outlines where an object stands in front of something
// farther away (from the depth texture) and reduces the palette with a
// 2x2 ordered dither, so 3D models read like sprites.
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
    sampler2D _CameraDepthTexture;
    float _PixelArt;
    float _Levels;
    float _Outline;
    float _OutlineDepth;

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

    // Depth from 0 (near) to 1 (far). Linear for the orthographic pixel-art camera.
    float Depth01(float2 uv)
    {
        float d = tex2D(_CameraDepthTexture, uv).r;
        #if defined(UNITY_REVERSED_Z)
        d = 1.0 - d;
        #endif
        return d;
    }

    float4 fragComposite (v2f i) : SV_Target
    {
        float4 src = tex2D(_MainTex, i.uv);
        float2 bloomUV = i.uv;
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0.0) bloomUV.y = 1.0 - bloomUV.y;
        #endif
        float3 c = src.rgb + tex2D(_BloomTex, bloomUV).rgb * _BloomIntensity;

        if (_Outline > 0.0)
        {
            // A pixel is on an object's rim when a neighbour is clearly farther away.
            float2 t = abs(_MainTex_TexelSize.xy);
            float d = Depth01(bloomUV);
            float n = max(max(Depth01(bloomUV + float2(t.x, 0.0)), Depth01(bloomUV - float2(t.x, 0.0))),
                          max(Depth01(bloomUV + float2(0.0, t.y)), Depth01(bloomUV - float2(0.0, t.y))));
            float rim = step(_OutlineDepth, n - d);
            c = lerp(c, c * 0.3 + float3(0.01, 0.012, 0.02), rim * _Outline);
        }

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

        if (_PixelArt > 0.5)
        {
            // Fewer colour steps with a 2x2 ordered dither for a hand-pixelled feel.
            float2 cell = fmod(floor(i.uv * abs(_MainTex_TexelSize.zw)), 2.0);
            float bayer = (cell.x * 2.0 + cell.y * 3.0 - 4.0 * cell.x * cell.y + 0.5) * 0.25;
            c = floor(saturate(c) * _Levels + bayer) / _Levels;
        }
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
