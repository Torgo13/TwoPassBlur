// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

Shader "Hidden/RGSample/2PassBlur"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Never 
        ZWrite OFf 
        Cull Off
        Blend One Zero

        Pass
        {
            Name "DownSample Vertical"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            // Core.hlsl for XR dependencies
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlurSize;
            
            static const int BLUR_SAMPLE_COUNT = 3;
            static const float BLUR_KERNEL[BLUR_SAMPLE_COUNT] = {
                0.25, 0.5, 0.25
            };
            static const float BLUR_KERNEL_OFFSET[BLUR_SAMPLE_COUNT] = {
                -1, 0, 1
            };

            half4 Fragment(Varyings input) : SV_Target 
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(SHADER_API_MOBILE)
                half2 scale = half2(_BlurSize, 0);

                half4 color = 0;
                for (int i = 0; i < BLUR_SAMPLE_COUNT; i++)
                {
                    float2 ofs = BLUR_KERNEL_OFFSET[i] * scale;
                    half4 blurColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + ofs);
                    color.rgb += blurColor.rgb * BLUR_KERNEL[i];
                }
#else
                //https://github.com/microsoft/MixedReality-GraphicsTools-Unity/blob/7d9f9160d8c615f4f456024478a84df8bd75469e/com.microsoft.mrtk.graphicstools.unity/Runtime/Experimental/Acrylic/Shaders/AcrylicDualBlur.shader#L58
                float2 _AcrylicBlurOffset = _BlurSize;
                float2 _AcrylicHalfPixel = 0.5;

                half4 color;                
                color.rgb = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb * 4.0;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + _AcrylicHalfPixel * _AcrylicBlurOffset).rgb;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord - _AcrylicHalfPixel * _AcrylicBlurOffset).rgb;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-_AcrylicHalfPixel.x, _AcrylicHalfPixel.y) * _AcrylicBlurOffset).rgb;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(_AcrylicHalfPixel.x, -_AcrylicHalfPixel.y) * _AcrylicBlurOffset).rgb;

                color.rgb *= 0.125;
                color.a = 1.0;
#endif // SHADER_API_MOBILE

                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DownSample Horizontal"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment 
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            // Core.hlsl for XR dependencies
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlurSize;

            static const int BLUR_SAMPLE_COUNT = 3;
            static const float BLUR_KERNEL[BLUR_SAMPLE_COUNT] = {
                0.25, 0.5, 0.25
            };
            static const float BLUR_KERNEL_OFFSET[BLUR_SAMPLE_COUNT] = {
                -1, 0, 1
            };

            half4 Fragment(Varyings input) : SV_Target 
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(SHADER_API_MOBILE)
                float2 scale = float2(0, _BlurSize);

                half4 color = 0; 
                for (int i = 0; i < BLUR_SAMPLE_COUNT; i++)
                {
                    float2 ofs = BLUR_KERNEL_OFFSET[i] * scale;
                    half4 blurColor = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord + ofs, 0);
                    color.rgb += blurColor.rgb * BLUR_KERNEL[i];
                }
#else
                //https://github.com/microsoft/MixedReality-GraphicsTools-Unity/blob/7d9f9160d8c615f4f456024478a84df8bd75469e/com.microsoft.mrtk.graphicstools.unity/Runtime/Experimental/Acrylic/Shaders/AcrylicDualBlur.shader#L73
                float2 _AcrylicBlurOffset = _BlurSize;
                float2 _AcrylicHalfPixel = 0.5;

                half4 color;
                color.rgb = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-_AcrylicHalfPixel.x * 2.0, 0.0) * _AcrylicBlurOffset).rgb;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-_AcrylicHalfPixel.x, _AcrylicHalfPixel.y) * _AcrylicBlurOffset).rgb * 2.0;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(0.0, _AcrylicHalfPixel.y * 2.0) * _AcrylicBlurOffset).rgb;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(_AcrylicHalfPixel.x, _AcrylicHalfPixel.y) * _AcrylicBlurOffset).rgb * 2.0;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(_AcrylicHalfPixel.x * 2.0, 0.0) * _AcrylicBlurOffset).rgb;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(_AcrylicHalfPixel.x, -_AcrylicHalfPixel.y) * _AcrylicBlurOffset).rgb * 2.0;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(0.0, -_AcrylicHalfPixel.y * 2.0) * _AcrylicBlurOffset).rgb;
                color.rgb += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-_AcrylicHalfPixel.x, -_AcrylicHalfPixel.y) * _AcrylicBlurOffset).rgb * 2.0;

                color.rgb *= (1.0 / 12.0);
                color.a = 1.0;
#endif // SHADER_API_MOBILE

                return color;
            }
            ENDHLSL
        }
    }
}
