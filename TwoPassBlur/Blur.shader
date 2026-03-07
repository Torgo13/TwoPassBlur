Shader "Hidden/RGSample/2PassBlur"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Pass
        {
            Name "DonwSample Verttical"
            ZTest Never 
            ZWrite OFf 
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            // Core.hlsl for XR dependencies
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlurSize;
            
            static const int BLUR_SAMPLE_COUNT=3;
            static const float BLUR_KERNEL[BLUR_SAMPLE_COUNT] = {
                0.25, 0.5, 0.25
            };
            static const float BLUR_KERNEL_OFFSET[BLUR_SAMPLE_COUNT] = {
                -1,0,1
            };

            half4 Fragment(Varyings input) : SV_Target 
            {
                // return half4(1, 0, 0, 1);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half2 scale = half2(_BlurSize, 0);

                half4 color = 0;
                for(int i=0; i<BLUR_SAMPLE_COUNT; i++)
                {
                    float2 ofs = BLUR_KERNEL_OFFSET[i] * scale;
                    half4 blurColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord+ofs);
                    color.rgb += blurColor.rgb * BLUR_KERNEL[i];
                }
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DonwSample Horizontal"
            ZTest Never 
            ZWrite OFf 
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment 
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            // Core.hlsl for XR dependencies
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlurSize;

            static const int BLUR_SAMPLE_COUNT=3;
            static const float BLUR_KERNEL[BLUR_SAMPLE_COUNT] = {
                0.25, 0.5, 0.25
            };
            static const float BLUR_KERNEL_OFFSET[BLUR_SAMPLE_COUNT] = {
                -1,0,1
            };

            half4 Fragment(Varyings input) : SV_Target 
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 scale = float2(0, _BlurSize);

                half4 color = 0; 
                for(int i=0; i<BLUR_SAMPLE_COUNT; i++)
                {
                    float2 ofs = BLUR_KERNEL_OFFSET[i] * scale;
                    half4 blurColor = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord+ofs, 0);
                    color.rgb += blurColor.rgb * BLUR_KERNEL[i];
                }

                return color;
            }
            ENDHLSL
        }
    }
}
