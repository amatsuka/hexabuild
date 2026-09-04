// Второй шейдер проекта. Процедурное решение упёрлось вот во что: вода — это не цвет плитки,
// а то, сколько её между глазом и дном. Ни цветом материала, ни мешем такого не задать —
// нужна разница глубин между поверхностью и сценой под ней, а она есть только в кадре.
// Отсюда и всё остальное: пена появляется там, где эта разница мала, прозрачность падает
// там, где она велика, а блики бегут по времени. Плоскость непрозрачной быть не может —
// иначе дно и кромка берега не просвечивают, и вода читается синим полом.
Shader "Game/Water"
{
    Properties
    {
        _ShallowColor ("Мелководье", Color) = (0.36, 0.78, 0.80, 1)
        _DeepColor ("Глубина", Color) = (0.07, 0.31, 0.56, 1)
        // Толщина слоя, на которой мелководье доходит до цвета открытого моря. 0.42 стояло,
        // пока глубина была только за краем поля: там под водой нет дна, и цвет всё равно
        // упирался в потолок. Внутри поля дно есть, и на таком диапазоне залив не успевал
        // уйти из бирюзы — вода в гексах читалась мелью, а не морем.
        [Tooltip] _DepthRange ("Глубина полного цвета", Range(0.02, 2)) = 0.22
        _ShallowAlpha ("Прозрачность у берега", Range(0, 1)) = 0.55
        _DeepAlpha ("Плотность на глубине", Range(0, 1)) = 0.97

        _FoamColor ("Цвет пены", Color) = (0.82, 0.95, 0.98, 1)
        _FoamWidth ("Ширина пены", Range(0, 0.5)) = 0.032
        _FoamWobble ("Дрожь кромки", Range(0, 0.06)) = 0.022
        _FoamSpeed ("Скорость кромки", Range(0, 4)) = 0.7

        _SparkleColor ("Цвет бликов", Color) = (1, 1, 0.94, 1)
        _SparkleScale ("Частота бликов", Range(0.5, 20)) = 3.6
        _SparkleSpeed ("Скорость бликов", Range(0, 3)) = 0.45
        _SparkleStrength ("Сила бликов", Range(0, 1)) = 0.1
        _SwellStrength ("Сила зыби", Range(0, 0.2)) = 0.04
        _Gloss ("Резкость солнечного пятна", Range(4, 256)) = 48
        _GlossStrength ("Сила солнечного пятна", Range(0, 2)) = 0.6

        // Те же рычаги, что у `Game/TileState`: вода обязана стоять в одной гамме с полем,
        // иначе тонмаппинг разведёт их в разные картинки.
        _LightWrap ("Заворот света", Range(0, 1)) = 1
        _ShadowColor ("Цвет тени", Color) = (0.13, 0.17, 0.26, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            // Без этих двух ключевых слов `GetMainLight` в Forward+ возвращает чёрный свет:
            // вся история M12–M13, повторять её на втором шейдере незачем.
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _DepthRange;
                half _ShallowAlpha;
                half _DeepAlpha;
                half4 _FoamColor;
                half _FoamWidth;
                half _FoamWobble;
                half _FoamSpeed;
                half4 _SparkleColor;
                half _SparkleScale;
                half _SparkleSpeed;
                half _SparkleStrength;
                half _SwellStrength;
                half _Gloss;
                half _GlossStrength;
                half _LightWrap;
                half4 _ShadowColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                // Расстояние вдоль взгляда до самой поверхности. У ортокамеры это по-прежнему
                // линейная величина, поэтому её и берём — с ней сравнивается глубина сцены.
                output.eyeDepth = -TransformWorldToView(output.positionWS).z;
                return output;
            }

            /// Глубина сцены вдоль взгляда. Камера проекта ортографическая, и `LinearEyeDepth`
            /// для неё врёт: там обратная функция перспективы. Ветку с перспективой оставляем —
            /// стоит она один lerp, а без неё шейдер молча ломается в любом другом окне.
            float SceneEyeDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float normalized = 1.0 - raw;
                #else
                    float normalized = raw;
                #endif

                float ortho = lerp(_ProjectionParams.y, _ProjectionParams.z, normalized);
                float perspective = LinearEyeDepth(raw, _ZBufferParams);
                return lerp(perspective, ortho, unity_OrthoParams.w);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // Сколько воды между глазом и дном. Всё в шейдере растёт отсюда.
                float thickness = max(SceneEyeDepth(screenUV) - input.eyeDepth, 0.0);
                half depth01 = saturate(thickness / max(_DepthRange, 1e-3));

                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);
                half alpha = lerp(_ShallowAlpha, _DeepAlpha, depth01);

                float2 flow = input.positionWS.xz;
                float time = _Time.y;

                // Кромка дрожит: без этого пена — ровный поясок по геометрии берега, и вода
                // выглядит вырезанной ножницами.
                float wobble = (sin(flow.x * 9.0 + time * _FoamSpeed) +
                                sin(flow.y * 11.0 - time * _FoamSpeed * 1.3)) * 0.5;
                float foamEdge = _FoamWidth + wobble * _FoamWobble;
                half foam = 1.0h - smoothstep(0.0, max(foamEdge, 1e-3), thickness);

                float3 normalWS = float3(0, 1, 0);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = dot(normalWS, mainLight.direction);
                half wrapped = saturate(lerp(ndl, ndl * 0.5h + 0.5h, _LightWrap));
                half key = wrapped * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half3 fill = SampleSH(normalWS) + _ShadowColor.rgb * (1.0h - key);

                half3 lit = water * (mainLight.color.rgb * key + fill);
                lit = lerp(lit, _FoamColor.rgb, foam);
                alpha = max(alpha, foam);

                // Солнечное пятно по гладкой воде плюс мелкая рябь: одно даёт большой блик,
                // второе — бегущие искры. Порознь получается либо зеркало, либо шум.
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfDir = normalize(mainLight.direction + viewWS);
                half gloss = pow(saturate(dot(normalWS, halfDir)), _Gloss) * _GlossStrength;

                // Три волны под несоизмеримыми углами. Двух по осям хватало на решётку из
                // одинаковых точек — на замере это был не блеск воды, а обои в горошек; третья
                // волна поперёк и разные периоды ломают решётку в неповторяющийся гребень.
                float2 waveA = float2(0.94, 0.34);
                float2 waveB = float2(-0.42, 0.91);
                float2 waveC = float2(0.71, -0.70);
                float ripple =
                    sin(dot(flow, waveA) * _SparkleScale + time * _SparkleSpeed) +
                    sin(dot(flow, waveB) * _SparkleScale * 1.63 - time * _SparkleSpeed * 0.77) * 0.8 +
                    sin(dot(flow, waveC) * _SparkleScale * 2.41 + time * _SparkleSpeed * 1.31) * 0.55;
                // Зыбь красит саму воду, гребень кладётся поверх. Порознь получается либо
                // ровная синяя заливка, либо белые кляксы по решётке: замер на первой версии
                // дал именно кляксы, потому что весь блеск сидел в одном пороге.
                lit *= 1.0h + ripple * _SwellStrength;
                // Гребни выходят пятнами, а не сплошной решёткой: медленная широкая волна
                // гасит их на половине поля и уплывает. Без неё три синуса всё равно читаются
                // повторяющимся узором — глаз ловит период раньше, чем видит воду.
                float patch = saturate(0.35 + 0.75 * sin(dot(flow, float2(0.6, 0.8)) * 0.28 + time * 0.11));
                half sparkle = smoothstep(2.05h, 2.32h, ripple) * _SparkleStrength * patch;

                lit += _SparkleColor.rgb * (gloss + sparkle) * mainLight.shadowAttenuation;
                alpha = saturate(alpha + sparkle);

                return half4(lit, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
