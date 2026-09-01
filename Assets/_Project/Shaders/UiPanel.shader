// Карточка интерфейса одним проходом: скруглённый прямоугольник считается по расстоянию до
// кромки прямо в шейдере. Спрайт и 9-slice для этого не нужны — а значит, нет и запрета на
// градиент: у 9-slice тянется только средняя полоса, и градиент по вертикали разъезжался бы
// стыком с угловыми полосами. Заодно радиус, толщина кромки, блик и свечение стали числами.
Shader "Game/UiPanel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _FillTop ("Заливка сверху", Color) = (0.16, 0.34, 0.50, 0.78)
        _FillBottom ("Заливка снизу", Color) = (0.05, 0.14, 0.25, 0.92)
        _EdgeColor ("Кромка", Color) = (0.62, 0.86, 1.0, 0.90)
        _GlowColor ("Свечение наружу", Color) = (0.01, 0.03, 0.07, 0.55)

        _Radius ("Радиус скругления, px", Float) = 26
        _EdgeWidth ("Толщина кромки, px", Float) = 3
        _GlowSize ("Размах свечения, px", Float) = 20
        _GlowOffset ("Снос свечения вниз, px", Float) = 9
        _Highlight ("Блик по верхней кромке", Range(0, 2)) = 0.55
        _Sheen ("Глубина фаски от кромки внутрь, px", Float) = 16
        _BackLight ("Нижняя фаска: свет, прошедший сквозь толщу", Range(0, 1)) = 0.45
        _Spec ("Отражение источника в стекле", Range(0, 1)) = 0.18
        _LightDir ("Направление света", Vector) = (-0.45, 0.89, 0, 0)
        _Darken ("Насколько стекло гасит фон", Range(0, 1)) = 0.55
        _HighlightSpread ("Насколько блик сползает к бокам", Range(0.05, 1)) = 0.40

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]


        // Проход первый: стекло гасит фон под собой. Без него контраст контента зависел бы от
        // того, что позади: над тёмной картой панель тёмная, над яркой водой — светлая, и белый
        // текст на ней плыл. Обычным альфа-смешением так не сделать, нужно умножение.
        Pass
        {
            Name "Darken"
            Blend DstColor Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 shape : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _FillBottom;
            fixed4 _GlowColor;
            float _Radius;
            float _GlowSize;
            float _GlowOffset;
            float _Darken;
            float4 _ClipRect;

            float RoundBox(float2 pixel, float2 halfSize, float radius)
            {
                float2 q = abs(pixel) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(output.worldPosition);
                output.shape = input.texcoord;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 pixel = input.shape.xy;
                float2 halfSize = input.shape.zw;
                float radius = min(_Radius, min(halfSize.x, halfSize.y));

                float silhouette = 1.0 - smoothstep(-0.5, 0.5, RoundBox(pixel, halfSize, radius));
                float glowDistance = RoundBox(pixel + float2(0.0, _GlowOffset), halfSize, radius);
                float glowFalloff = 1.0 - smoothstep(0.0, max(_GlowSize, 0.001), glowDistance);

                // Тень и сама панель гасят фон одной кривой, а не двумя. Складывать их нельзя:
                // за кромкой тень гасила сильнее, чем панель под собой, и по контуру шло тёмное
                // кольцо — жёсткая линия там, где должен быть мягкий переход.
                float shade = max(silhouette, glowFalloff * _GlowColor.a);
                float amount = saturate(shade * _Darken) * input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                amount *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                // Тон гашения берём у глубокого края градиента: свой цвет заводить незачем.
                return fixed4(lerp(fixed3(1, 1, 1), _FillBottom.rgb, amount), 1);
            }
            ENDCG
        }

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                // xy — пиксели от центра карточки, zw — её полуразмер. Меш шире карточки на запас
                // под свечение, поэтому размер приходится нести с собой, а не брать из UV 0..1.
                float4 shape : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _FillTop;
            fixed4 _FillBottom;
            fixed4 _EdgeColor;
            fixed4 _GlowColor;
            float _Radius;
            float _EdgeWidth;
            float _GlowSize;
            float _GlowOffset;
            float _Highlight;
            float _HighlightSpread;
            float _Sheen;
            float _BackLight;
            float _Spec;
            float4 _LightDir;
            float4 _ClipRect;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(output.worldPosition);
                output.shape = input.texcoord;
                output.color = input.color;
                return output;
            }

            // Расстояние со знаком до кромки скруглённого прямоугольника: внутри отрицательное,
            // снаружи положительное, ровно в пикселях. На нём держится и сглаживание, и свечение.
            float RoundBox(float2 pixel, float2 halfSize, float radius)
            {
                float2 q = abs(pixel) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 pixel = input.shape.xy;
                float2 halfSize = input.shape.zw;
                float radius = min(_Radius, min(halfSize.x, halfSize.y));

                float edgeDistance = RoundBox(pixel, halfSize, radius);

                // Силуэт карточки и её внутренность под кромкой.
                float silhouette = 1.0 - smoothstep(-0.5, 0.5, edgeDistance);
                float inner = 1.0 - smoothstep(-0.5, 0.5, edgeDistance + _EdgeWidth);

                // Куда смотрит поверхность в этой точке и насколько она повёрнута к свету.
                // Свет один на весь интерфейс, поэтому панели освещены согласованно, а не каждая
                // симметрично сама по себе — симметричная панель читается неосвещённой.
                float2 unit = pixel / max(halfSize, float2(1.0, 1.0));
                float2 facingDir = normalize(unit + float2(1e-5, 1e-5));
                float facing = dot(facingDir, normalize(_LightDir.xy));

                // Базовый тон: градиент снизу вверх.
                float vertical = saturate(unit.y * 0.5 + 0.5);
                fixed4 fill = lerp(_FillBottom, _FillTop, vertical);

                // Две фаски вдоль внутренней кромки — это и есть выпуклость. Профиль яркости
                // поперёк панели получается немонотонным: светло у верхнего края, темнее к
                // середине, снова светлее у нижнего. Линейный градиент глаз читает наклонной
                // плоскостью, а такой профиль — объёмом. Нижняя фаска слабее: это не прямой свет,
                // а прошедший сквозь толщу.
                float depth = max(-edgeDistance - _EdgeWidth, 0.0);
                float bevel = 1.0 - smoothstep(0.0, max(_Sheen, 0.001), depth);
                // Середина уходит в тень: без провала между двумя фасками профиль остаётся
                // пологим, и объём не читается.
                fill.rgb *= lerp(1.0, 0.82, smoothstep(0.0, max(_Sheen, 0.001) * 2.5, depth));

                float lit = bevel * pow(saturate(facing), 1.0 / max(_HighlightSpread, 0.05));
                float back = bevel * saturate(-facing) * _BackLight;
                fill.rgb += (lit + back) * _Highlight;

                // Отражение источника: мягкое пятно, смещённое к свету. Именно пятном, а не
                // рампой по направлению: рампа на широкой карточке делит её пополам и читается
                // складкой, а не бликом.
                float2 toBlob = unit - normalize(_LightDir.xy) * 0.55;
                fill.rgb += exp(-dot(toBlob, toBlob) * 1.8) * _Spec;

                // Кромка тоже освещена: со стороны света почти белая, с теневой приглушена.
                fixed3 edgeRgb = _EdgeColor.rgb * lerp(0.60, 1.0, saturate(facing * 0.5 + 0.5))
                                 + saturate(facing) * _Highlight;

                // Свечение наружу: та же фигура, снесённая вниз и размытая по расстоянию.
                float glowDistance = RoundBox(pixel + float2(0.0, _GlowOffset), halfSize, radius);
                float glow = (1.0 - smoothstep(0.0, max(_GlowSize, 0.001), glowDistance))
                             * _GlowColor.a * (1.0 - silhouette);

                fixed4 result = fixed4(_GlowColor.rgb, glow);
                result.rgb = lerp(result.rgb, edgeRgb, silhouette * _EdgeColor.a);
                result.a = lerp(result.a, _EdgeColor.a, silhouette);
                result.rgb = lerp(result.rgb, fill.rgb, inner * fill.a);
                result.a = lerp(result.a, fill.a, inner);

                result *= input.color;

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
