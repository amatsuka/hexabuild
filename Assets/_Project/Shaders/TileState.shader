// Состояние плитки — это не другой цвет, а другая видимость. Поэтому туман кладётся ПОВЕРХ
// освещения, а не в альбедо: умножение альбедо чернит плитку и убивает рельеф вместе с цветом,
// а подмешивание цвета дымки оставляет и то и другое. Плюс на текстурированной модели умножать
// альбедо нечем — рычага бы просто не стало.
Shader "Game/TileState"
{
    Properties
    {
        [MainColor] _BaseColor ("Цвет", Color) = (1, 1, 1, 1)
        // Палитровый атлас моделей. Процедурные меши UV не пишут вовсе, у них uv = (0,0),
        // и белая заглушка возвращает им ровно прежний цвет: слот просто остаётся пустым.
        [MainTexture] _BaseMap ("Палитра", 2D) = "white" {}
        _FogColor ("Цвет дымки", Color) = (0.15, 0.16, 0.20, 1)
        _StateFog ("Туман", Range(0, 1)) = 0
        _StateFade ("Обесцвечивание", Range(0, 1)) = 0

        // Голый ламберт гасит неосвещённую грань в ноль, и мультяшный объём на этом кончается.
        // Заворот тянет свет за терминатор: 0 — прежний ламберт, 1 — полный half-lambert.
        _LightWrap ("Заворот света", Range(0, 1)) = 1
        // В тени цвет уходит не в чёрный, а в холодный подтон: так тень остаётся цветом,
        // а не дырой. Прибавляется, а не умножается, иначе это просто ещё одно затемнение.
        _ShadowColor ("Цвет тени", Color) = (0.13, 0.17, 0.26, 1)
        // Ободок по силуэту. Ради него он и заведён: тёмное дерево на тёмной плитке
        // отличается от неё только контуром.
        _RimColor ("Цвет ободка", Color) = (0.62, 0.78, 0.95, 1)
        // Куда уползает ободок на полном накале склада: поле греется вместе с ним.
        _RimHotColor ("Цвет ободка на накале", Color) = (1.0, 0.62, 0.28, 1)
        _RimPower ("Резкость ободка", Range(0.5, 8)) = 3
        _RimStrength ("Сила ободка", Range(0, 1)) = 0.22

        // Дыхание поля. Смещение всегда вверх: ловушка «суша ниже 0.09 пускает воду в канавку
        // между фасками» — отрицательная полуволна обвела бы пеной каждую береговую плитку.
        _BreathHeight ("Высота дыхания", Range(0, 0.05)) = 0.015
        _BreathSpeed ("Вдохов в секунду", Range(0, 3)) = 0.55
        _BreathScale ("Фаза на юнит", Range(0.05, 3)) = 0.7
        // Насколько дымка глушит дыхание: 2.5 — доступная плитка (0.45) уже неподвижна.
        _BreathFog ("Глушение дымкой", Range(0.5, 6)) = 2.5

        // Волна открытия. Кольцо расходится от той плитки, по которой заплатили.
        _WaveHeight ("Высота волны", Range(0, 0.2)) = 0.06
        _WaveSpeed ("Скорость волны", Range(1, 30)) = 7
        _WaveWidth ("Ширина гребня", Range(0.1, 4)) = 1.4
        _WaveSeconds ("Жизнь волны", Range(0.1, 4)) = 1.1
        _WaveRange ("Докуда доходит", Range(1, 30)) = 7
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // Смещение вершин обязано быть одинаковым во всех проходах: считай его только
        // в ForwardLit — и тень с SSAO остались бы от недвинутой геометрии. Отсюда общий
        // блок: он вставляется в каждый проход, и инстанс-буфер объявлен здесь же.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Глобалы поля — их ставит `Game.Grid.FieldPulse`. Свойством на плитку это не сделать:
        // анимация общая для всего поля, а инстанс-буфер стоит замера (§5).
        // xyz — откуда пошла волна, w — когда (в секундах `_Time.y`).
        float4 _FieldWave;
        // Доля накала склада: поле греется вместе с карточкой.
        half _FieldHeat;
        // Общий рубильник движения. Ноль — поле стоит: `ResourceIconBaker` печёт иконки тем же
        // материалом, и волна, пришедшая в момент выпечки, впеклась бы в иконку навсегда.
        half _FieldPulse;

        half _BreathHeight;
        half _BreathSpeed;
        half _BreathScale;
        half _BreathFog;
        half _WaveHeight;
        half _WaveSpeed;
        half _WaveWidth;
        half _WaveSeconds;
        half _WaveRange;

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DEFINE_INSTANCED_PROP(float4, _FogColor)
            UNITY_DEFINE_INSTANCED_PROP(float, _StateFog)
            UNITY_DEFINE_INSTANCED_PROP(float, _StateFade)
            // Сила ободка живёт в буфере ради одного: стек месторождения накаляется к своей
            // выдаче, и каждый стек идёт по своему таймеру. Замер инстансинга — в записи M30.
            UNITY_DEFINE_INSTANCED_PROP(float, _RimStrength)
        UNITY_INSTANCING_BUFFER_END(Props)

        /// Насколько поднята вершина. Фаза берётся из мировой позиции, а не из позиции объекта:
        /// так крышка, лента русла, берег, декор, стеки и дорога едут одним куском — русло лежит
        /// в 0.012 над крышкой, и раздельное движение съело бы ленту первым же вдохом.
        float FieldLift(float3 positionWS, half fog)
        {
            // Дыхание: живая плитка дышит, доступная и скрытая стоят. Дымка уже лежит
            // в инстанс-буфере, и нового поля на это не нужно.
            half alive = saturate(1.0h - fog * _BreathFog);
            float phase = positionWS.x * _BreathScale + positionWS.z * _BreathScale * 0.73;
            float breath = (0.5 + 0.5 * sin(_Time.y * _BreathSpeed * 6.2831853 + phase))
                * _BreathHeight * alive;

            // Волна: кольцо уходит от точки открытия и гаснет и по времени, и по расстоянию.
            float age = _Time.y - _FieldWave.w;
            float spread = length(positionWS.xz - _FieldWave.xz);
            float crest = abs(spread - age * _WaveSpeed);
            float ring = 1.0 - smoothstep(0.0, max(_WaveWidth, 1e-3), crest);
            float life = saturate(1.0 - age / max(_WaveSeconds, 1e-3)) * step(0.0, age);
            float reach = saturate(1.0 - spread / max(_WaveRange, 1e-3));
            float wave = ring * life * reach * _WaveHeight;

            return (breath + wave) * _FieldPulse;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            // Без этих двух ключевых слов шейдер в режиме Forward+ получает от `GetMainLight`
            // чёрный свет: рендерер раскладывает источники по кластерам, а вариант шейдера
            // собран под старую схему. Поле освещалось одним ambient. `_FORWARD_PLUS` — имя
            // из URP 14–16, `_CLUSTER_LIGHT_LOOP` — то же самое в URP 17 (Unity 6).
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Текстура одна на материал и в инстанс-буфер не кладётся: там только то, что
            // меняется от рендерера к рендереру.
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Тон материала одинаков для всех плиток и в инстанс-буфер не кладётся: там
            // только то, что меняется от рендерера к рендереру. Отдельным `UnityPerMaterial`
            // они тоже не объявлены — неполный буфер сбил бы SRP Batcher с толку, а батчинг
            // тут держит инстансинг.
            half _LightWrap;
            half4 _ShadowColor;
            half4 _RimColor;
            half4 _RimHotColor;
            half _RimPower;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS.y += FieldLift(
                    output.positionWS, UNITY_ACCESS_INSTANCED_PROP(Props, _StateFog));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor)
                    * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 fogColor = UNITY_ACCESS_INSTANCED_PROP(Props, _FogColor);
                half fog = UNITY_ACCESS_INSTANCED_PROP(Props, _StateFog);
                half fade = UNITY_ACCESS_INSTANCED_PROP(Props, _StateFade);

                // Обесцвечивание идёт по альбедо: под туманом биом угадывается, но не спорит
                // цветом с открытой частью поля.
                half3 albedo = lerp(baseColor.rgb, Luminance(baseColor.rgb).xxx, fade);

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                half ndl = dot(normalWS, mainLight.direction);
                // Заворот: ламберт `saturate(ndl)` при wrap = 0, half-lambert при wrap = 1.
                half wrapped = saturate(lerp(ndl, ndl * 0.5h + 0.5h, _LightWrap));
                half attenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half key = wrapped * attenuation;

                // Заливка: небо сверху плюс окрашенная тень там, где ключа нет.
                half3 fill = SampleSH(normalWS) + _ShadowColor.rgb * (1.0h - key);
                half3 lit = albedo * (mainLight.color.rgb * key + fill);

                // Ободок гасится в тумане вместе со всем остальным: `lerp` ниже общий.
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower)
                    * UNITY_ACCESS_INSTANCED_PROP(Props, _RimStrength);
                // Накал склада не остаётся на складе: ободок всего поля уползает в тёплое.
                // Множится на общий рубильник — иначе разогретый ободок впёкся бы в иконку.
                half3 rimColor = lerp(_RimColor.rgb, _RimHotColor.rgb, saturate(_FieldHeat * _FieldPulse));
                lit += rimColor * rim;

                return half4(lerp(lit, fogColor.rgb, fog), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS.y += FieldLift(positionWS, UNITY_ACCESS_INSTANCED_PROP(Props, _StateFog));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS.y += FieldLift(positionWS, UNITY_ACCESS_INSTANCED_PROP(Props, _StateFog));
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 DepthFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Нужен экранному ambient occlusion: без этого прохода SSAO из рендерера не видит плитки.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS.y += FieldLift(positionWS, UNITY_ACCESS_INSTANCED_PROP(Props, _StateFog));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFragment(Varyings input) : SV_Target
            {
                return half4(normalize(input.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
