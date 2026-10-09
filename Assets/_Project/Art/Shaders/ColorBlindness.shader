// Full-screen red-green colour blindness simulation, drawn by a Full Screen Pass Renderer Feature.
// Strength and type are global properties driven by APX.Masks.ColorBlindnessFilter (0 strength = no change,
// which is also the default outside Play mode).
// Matrices: Machado, Oliveira & Fernandes (2009), severity 1.0, applied to linear RGB.
Shader "APX/Color Blindness"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ColorBlindness"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // 0 = off, 1 = full simulation.
            float _APX_ColorBlindStrength;
            // 0 = deuteranopia (green cones missing), 1 = protanopia (red cones missing).
            float _APX_ColorBlindProtan;

            static const float3x3 Deuteranopia = float3x3(
                 0.367322, 0.860646, -0.227968,
                 0.280085, 0.672501,  0.047413,
                -0.011820, 0.042940,  0.968881);

            static const float3x3 Protanopia = float3x3(
                 0.152286, 1.052583, -0.204868,
                 0.114503, 0.786281,  0.099216,
                -0.003882, -0.048116, 1.051998);

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                float3 deutan = mul(Deuteranopia, color.rgb);
                float3 protan = mul(Protanopia, color.rgb);
                float3 simulated = saturate(lerp(deutan, protan, _APX_ColorBlindProtan));

                color.rgb = lerp(color.rgb, simulated, _APX_ColorBlindStrength);
                return color;
            }
            ENDHLSL
        }
    }
}
