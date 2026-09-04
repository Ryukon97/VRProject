// URP 툰 셰이더 (VR / 스테레오 인스턴싱 대응)
//
// 목표: 제한된 명암 단계 + 채도를 유지한 색 그림자 + 억제된 스페큘러.
// 얼굴은 _Flatten을 높여 명암 경계를 거의 없애고, 머리카락/의상은 낮춰 단계를 살린다.
//
// VR 관련:
//  - 모든 패스에 스테레오 인스턴싱 매크로가 들어 있다. 이게 빠지면 한쪽 눈만 이상하게 나온다.
//  - 아웃라인 폭은 화면 기준으로 일정하게 유지되며, 가까이 갔을 때 부풀지 않도록 상한이 걸려 있다.
//  - 모든 프로퍼티가 UnityPerMaterial CBUFFER에 있어 SRP Batcher와 호환된다.

Shader "VRProject/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1,1,1,1)

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5

        [Header(Toon Ramp)][Space(4)]
        _ShadowThreshold("Shadow Threshold", Range(0,1)) = 0.5
        _ShadowFeather("Shadow Feather", Range(0.001,0.4)) = 0.05
        _ShadowTint("Shadow Tint (albedo multiplier)", Color) = (0.72,0.70,0.86,1)
        _Flatten("Flatten (face = high)", Range(0,1)) = 0
        _ReceiveShadowStrength("Receive Shadow Strength", Range(0,1)) = 0.55

        [Header(Second Step)][Space(4)]
        [Toggle(_SECONDSTEP_ON)] _SecondStep("Enable 3rd Tone", Float) = 0
        _ShadowThreshold2("Deep Shadow Threshold", Range(0,1)) = 0.28
        _ShadowTint2("Deep Shadow Tint", Color) = (0.55,0.53,0.72,1)

        [Header(Character Light Override)][Space(4)]
        // 씬의 디렉셔널 라이트를 무시하고 캐릭터 전용 광원을 쓴다.
        // 애니메이션풍 게임이 한낮 야외에서도 툰으로 보이는 핵심 장치.
        // 캐릭터가 어디에 서 있든, 해가 어느 방향이든 얼굴이 일정하게 밝다.
        [Toggle(_CHARACTERLIGHT_ON)] _CharacterLight("Use Character Light", Float) = 0
        _CharacterLightDir("Character Light Dir (world)", Vector) = (0.35,0.45,-0.82,0)
        _CharacterLightColor("Character Light Color", Color) = (1,1,1,1)

        [Header(Ambient)][Space(4)]
        _AmbientStrength("Ambient Strength", Range(0,2)) = 1.0
        _AmbientFlatten("Ambient Flatten", Range(0,1)) = 0.6
        // 환경광(SH/스카이박스)이 캐릭터에 미치는 영향. 리얼한 느낌의 상당 부분이 여기서 온다.
        // 툰으로 갈수록 낮춘다.
        _EnvironmentInfluence("Environment Influence", Range(0,1)) = 1.0

        [Header(Rim)][Space(4)]
        _RimColor("Rim Color", Color) = (0.78,0.85,1,1)
        _RimPower("Rim Power", Range(0.5,16)) = 6
        _RimIntensity("Rim Intensity", Range(0,1)) = 0.18
        _RimLightAlign("Rim Follows Light", Range(0,1)) = 0.6

        [Header(Specular)][Space(4)]
        _SpecColor2("Specular Color", Color) = (1,1,1,1)
        _SpecPower("Specular Power", Range(1,128)) = 40
        _SpecIntensity("Specular Intensity", Range(0,1)) = 0.12
        _SpecFeather("Specular Feather", Range(0.001,0.5)) = 0.08

        [Header(Emission)][Space(4)]
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,0)
        _EmissionMap("Emission Map", 2D) = "white" {}

        [Header(Additional Lights)][Space(4)]
        _AdditionalLightIntensity("Additional Light Intensity", Range(0,2)) = 1.0

        [Header(Outline)][Space(4)]
        [Toggle(_OUTLINE_ON)] _OutlineEnabled("Enable Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0.13,0.15,0.30,1)
        _OutlineWidth("Outline Width (screen px approx)", Range(0,8)) = 1.4
        _OutlineMaxWidth("Outline Max Width (world m)", Range(0.0005,0.05)) = 0.006
        _OutlineTintByAlbedo("Tint By Albedo", Range(0,1)) = 0.35

        [Header(Depth Offset)][Space(4)]
        // 얼굴 위에 얹힌 표정 파츠(눈/눈썹/입)가 얼굴 메시와 깊이가 거의 같아
        // Z-파이팅을 일으킨다. 파츠를 카메라 쪽으로 살짝 당겨 항상 위에 오게 한다.
        _ZOffset("Z Offset (toward camera)", Range(0,0.1)) = 0
        // 아웃라인 헐은 반대로 뒤로 밀어야 표면 디테일을 덮지 않는다.
        _OutlineZOffset("Outline Z Offset (away)", Range(0,0.1)) = 0

        [Header(Surface)][Space(4)]
        // 0 = 불투명, 1 = 투명 데칼.
        //
        // MMD/VRChat 모델의 표정 파츠(눈·눈썹·볼 붉힘·눈물·세로줄)는 얼굴 표면과
        // 사실상 같은 평면에 놓인 데칼이다. 불투명으로 그리면 깊이 경쟁에서
        // Z-파이팅이 나므로, 원본 모델은 투명 오버레이로 나중에 덧그린다.
        // 데칼 모드는 그 동작을 되살린다.
        [Enum(Opaque,0,Transparent Decal,1)] _Surface("Surface Type", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0

        [Header(Rendering)][Space(4)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Enum(Off,0,On,1)] _ZWrite("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _EmissionMap_ST;
            half4  _BaseColor;
            half4  _ShadowTint;
            half4  _ShadowTint2;
            half4  _RimColor;
            half4  _SpecColor2;
            half4  _EmissionColor;
            half4  _OutlineColor;
            half4  _CharacterLightColor;
            float4 _CharacterLightDir;
            half   _EnvironmentInfluence;
            float  _CharacterLight;
            half   _Cutoff;
            half   _ShadowThreshold;
            half   _ShadowThreshold2;
            half   _ShadowFeather;
            half   _Flatten;
            half   _ReceiveShadowStrength;
            half   _AmbientStrength;
            half   _AmbientFlatten;
            half   _RimPower;
            half   _RimIntensity;
            half   _RimLightAlign;
            half   _SpecPower;
            half   _SpecIntensity;
            half   _SpecFeather;
            half   _AdditionalLightIntensity;
            half   _OutlineWidth;
            half   _OutlineMaxWidth;
            half   _OutlineTintByAlbedo;
            float  _ZOffset;
            float  _OutlineZOffset;
            float  _AlphaClip;
            float  _SecondStep;
            float  _OutlineEnabled;
            float  _Surface;
            float  _SrcBlend;
            float  _DstBlend;
            float  _Cull;
            float  _ZWrite;
        CBUFFER_END

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);  SAMPLER(sampler_EmissionMap);

        // 명암을 단계로 자른다. 완전히 딱딱하지 않고 좁은 범위에서 부드럽게 넘어간다.
        half ToonStep(half value, half threshold, half feather)
        {
            return smoothstep(threshold - feather, threshold + feather, value);
        }

        // 정점을 뷰 공간에서 카메라 쪽으로 당긴다(offset > 0).
        //
        // 실제 위치는 그대로 두고 깊이만 바꾸기 때문에, 얼굴 위에 얹힌
        // 표정 파츠가 얼굴과 Z-파이팅하지 않고 항상 위에 그려진다.
        // 지오메트리를 실제로 띄우면 옆에서 봤을 때 파츠가 얼굴에서 떠 보인다.
        // offset > 0 이면 카메라 쪽으로 당기고, < 0 이면 뒤로 민다.
        float4 ApplyZOffset(float4 positionCS, float offset)
        {
            if (offset == 0.0) return positionCS;

            if (unity_OrthoParams.w == 0.0)   // 원근 투영
            {
                // positionCS.w는 -viewZ와 같다. 뷰 공간에서 카메라 앞은 z가 음수이므로,
                // 카메라 쪽으로 당기려면 viewZ를 0에 가깝게(덜 음수로) 만들어야 한다.
                float viewZ = -positionCS.w;

                // 근평면 앞으로 넘어가면 잘려 사라지고 나눗셈도 터진다. 막아둔다.
                float modifiedVS_Z = min(viewZ + offset, -1e-4);

                float modifiedCS_Z = modifiedVS_Z * UNITY_MATRIX_P._m22 + UNITY_MATRIX_P._m23;
                positionCS.z = modifiedCS_Z * positionCS.w / (-modifiedVS_Z);
            }
            else                               // 직교 투영
            {
            #if UNITY_REVERSED_Z
                // 리버스드 Z에서는 가까울수록 z가 크다.
                positionCS.z += offset / _ProjectionParams.z;
            #else
                positionCS.z -= offset / _ProjectionParams.z;
            #endif
            }
            return positionCS;
        }
        ENDHLSL

        // ────────────────────────────────────────────────────────────────
        // 아웃라인 (인버티드 헐). 캐릭터에만 쓰고 배경에는 끄는 편이 좋다.
        // Quest 단독 구동이면 드로우콜이 2배가 된다.
        // ────────────────────────────────────────────────────────────────
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            #ifdef _OUTLINE_ON
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS   = normalize(TransformObjectToWorldNormal(v.normalOS));

                // 화면상 폭을 일정하게 유지한다. VR은 플레이어가 다가오고 물러나므로
                // 고정 월드 폭이면 가까울 때 선이 굵어진다.
                float dist = distance(GetCameraPositionWS(), positionWS);
                float fovFactor = 2.0 / max(1e-4, abs(UNITY_MATRIX_P._m11));
                float width = _OutlineWidth * 0.0012 * dist * fovFactor;

                // 아주 가까이 붙었을 때 선이 폭발하는 것을 막는다.
                width = min(width, _OutlineMaxWidth);

                positionWS += normalWS * width;

                // 아웃라인 헐은 뒤로 민다. 안 그러면 확장된 얼굴 헐이
                // 그 위에 얹힌 눈·눈썹 파츠를 덮어버린다.
                o.positionCS = ApplyZOffset(TransformWorldToHClip(positionWS), -_OutlineZOffset);
            #else
                // 아웃라인이 꺼지면 클립 밖으로 보내 완전히 제거한다.
                o.positionCS = float4(0, 0, -10, 1);
            #endif

                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 OutlineFrag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

            #ifdef _ALPHATEST_ON
                clip(albedo.a - _Cutoff);
            #endif

                // 선 색을 알베도 쪽으로 조금 당기면 부위마다 선이 따로 놀지 않는다.
                half3 col = lerp(_OutlineColor.rgb, _OutlineColor.rgb * albedo.rgb, _OutlineTintByAlbedo);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ────────────────────────────────────────────────────────────────
        // 메인 툰 라이팅
        // ────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // 불투명 모드에서는 C#이 _SrcBlend=One, _DstBlend=Zero, _ZWrite=1을 넣어
            // 사실상 블렌딩이 없는 상태가 된다.
            // 데칼 모드에서는 SrcAlpha/OneMinusSrcAlpha + ZWrite Off가 되어
            // 깊이 경쟁 없이 얼굴 위에 합성된다.
            Blend [_SrcBlend] [_DstBlend]
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex ToonVert
            #pragma fragment ToonFrag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SECONDSTEP_ON
            #pragma shader_feature_local_fragment _CHARACTERLIGHT_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            // Forward+ (URP 17에서 _FORWARD_PLUS를 대체). 이게 없으면
            // PC_Renderer(Forward+)에서 추가 광원이 통째로 무시된다.
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS);

                o.positionCS = ApplyZOffset(pos.positionCS, _ZOffset);
                o.positionWS = pos.positionWS;
                o.normalWS   = nrm.normalWS;
                o.uv         = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogCoord   = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 ToonFrag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 albedo = baseTex.rgb;

            #ifdef _ALPHATEST_ON
                clip(baseTex.a - _Cutoff);
            #endif

                float3 N = normalize(i.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));

                // Forward+의 LIGHT_LOOP_BEGIN 매크로가 inputData를 직접 참조하므로
                // 이름과 필드를 그대로 맞춰줘야 한다.
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = V;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

                // ── 메인 라이트 ──────────────────────────────────────────
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.shadowCoord = shadowCoord;
                Light mainLight = GetMainLight(shadowCoord);

                // 캐릭터 전용 광원. 씬의 해가 어디에 있든 얼굴 조명이 흔들리지 않는다.
                // 배경은 실제 태양광을 받고 캐릭터만 별도 조명을 받는 이중 구조가 되며,
                // 이게 애니메이션풍 게임의 야외 씬이 리얼로 무너지지 않는 이유다.
                float3 lightDirWS = mainLight.direction;
                half3  lightColor = mainLight.color;
            #ifdef _CHARACTERLIGHT_ON
                lightDirWS = normalize(_CharacterLightDir.xyz);
                lightColor = _CharacterLightColor.rgb;
            #endif

                half NdotL = dot(N, lightDirWS);

                // 하프 램버트로 0..1에 매핑. 임계값 0.5가 정확히 명암 경계가 된다.
                half lambert = NdotL * 0.5h + 0.5h;

                // 얼굴은 여기를 높여 명암을 평평하게 만든다.
                // 진짜 SDF 페이스 섀도우는 아니지만 코 옆 그림자가 지저분해지는 것을 막아준다.
                lambert = lerp(lambert, 1.0h, _Flatten);

                // 드리운 그림자를 그대로 곱하면 너무 진하다. 강도를 낮춰 옅게 받는다.
                // 원본 영상의 "접촉 그림자는 있지만 매우 부드럽고 옅다"에 해당하는 부분.
                half castShadow = lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadowStrength);
                half lightTerm = lambert * castShadow;

                half ramp = ToonStep(lightTerm, _ShadowThreshold, _ShadowFeather);

                // 그림자에 검정을 곱하지 않는다. 채도를 유지한 색으로 눌러야 애니메이션처럼 보인다.
                half3 shadowCol = albedo * _ShadowTint.rgb;

            #ifdef _SECONDSTEP_ON
                half ramp2 = ToonStep(lightTerm, _ShadowThreshold2, _ShadowFeather);
                shadowCol = lerp(albedo * _ShadowTint2.rgb, shadowCol, ramp2);
            #endif

                half3 diffuse = lerp(shadowCol, albedo, ramp) * lightColor * mainLight.distanceAttenuation;

                // ── 앰비언트 ────────────────────────────────────────────
                // 완전한 SH는 방향성이 강해 툰과 잘 안 맞는다. 위쪽 SH와 섞어 평평하게 만든다.
                // 환경광은 리얼한 느낌의 주된 출처라, 툰으로 갈수록 영향력을 줄인다.
                half3 shDirectional = SampleSH(N);
                half3 shFlat = SampleSH(float3(0, 1, 0));
                half3 envSH = lerp(shDirectional, shFlat, _AmbientFlatten);
                envSH = lerp(half3(1, 1, 1) * Luminance(envSH), envSH, _EnvironmentInfluence);
                half3 ambient = envSH * albedo * _AmbientStrength * lerp(0.35h, 1.0h, _EnvironmentInfluence);

            #ifdef _SCREEN_SPACE_OCCLUSION
                AmbientOcclusionFactor aoFactor =
                    GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                ambient *= aoFactor.indirectAmbientOcclusion;
            #endif

                // ── 스페큘러 (억제) ─────────────────────────────────────
                half3 H = normalize(lightDirWS + V);
                half NdotH = saturate(dot(N, H));
                half specRaw = pow(NdotH, _SpecPower);
                half spec = ToonStep(specRaw, 0.5h, _SpecFeather) * _SpecIntensity * ramp;
                half3 specular = spec * _SpecColor2.rgb * lightColor;

                // ── 림 라이트 ───────────────────────────────────────────
                half fresnel = pow(1.0h - saturate(dot(N, V)), _RimPower);
                // 빛이 오는 쪽에서만 림이 보이게 하면 덜 인위적이다.
                half rimMask = lerp(1.0h, ramp, _RimLightAlign);
                half3 rim = fresnel * rimMask * _RimIntensity * _RimColor.rgb;

                // ── 추가 광원 (따뜻한 포인트/스팟 조명) ──────────────────
                half3 additional = 0;
            #ifdef _ADDITIONAL_LIGHTS
                // LIGHT_LOOP_BEGIN/END는 Forward와 Forward+ 양쪽을 알아서 처리한다.
                // 직접 for 루프를 돌면 Forward+에서 클러스터 광원을 놓친다.
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1, 1, 1, 1));

                    half lNdotL = dot(N, l.direction) * 0.5h + 0.5h;
                    lNdotL = lerp(lNdotL, 1.0h, _Flatten);

                    half lRamp = ToonStep(lNdotL, _ShadowThreshold, _ShadowFeather);
                    half atten = l.distanceAttenuation *
                                 lerp(1.0h, l.shadowAttenuation, _ReceiveShadowStrength);

                    additional += albedo * l.color * lRamp * atten;
                LIGHT_LOOP_END
                additional *= _AdditionalLightIntensity;
            #endif

                // ── 합산 ────────────────────────────────────────────────
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap,
                                     TRANSFORM_TEX(i.uv, _EmissionMap)).rgb * _EmissionColor.rgb;

                half3 color = diffuse + ambient + specular + rim + additional + emission;
                color = MixFog(color, i.fogCoord);

                return half4(color, baseTex.a);
            }
            ENDHLSL
        }

        // ────────────────────────────────────────────────────────────────
        // 그림자 캐스터
        // ────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target
            {
            #ifdef _ALPHATEST_ON
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        // ────────────────────────────────────────────────────────────────
        // DepthOnly / DepthNormals — SSAO와 뎁스 기반 효과에 필요
        // ────────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = ApplyZOffset(TransformObjectToHClip(v.positionOS.xyz), _ZOffset);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 DepthFrag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            #ifdef _ALPHATEST_ON
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = ApplyZOffset(TransformObjectToHClip(v.positionOS.xyz), _ZOffset);
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 DepthNormalsFrag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            #ifdef _ALPHATEST_ON
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
            #endif
                return half4(normalize(i.normalWS) * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"

    // 머티리얼 인스펙터를 섹션으로 정리하고 토글↔키워드를 동기화한다.
    // 이게 없으면 41개 프로퍼티가 한 줄로 늘어서고, _AlphaClip 같은 값을
    // 손으로 바꿨을 때 키워드가 따라가지 않아 조용히 동작하지 않는다.
    CustomEditor "VRProject.EditorTools.ToonLitShaderGUI"
}
