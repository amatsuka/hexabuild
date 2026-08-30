using System.Collections;
using System.Collections.Generic;
using Game.Economy;
using Game.Roads;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Визуал плитки: ландшафт, обводка, декор биома и модельки месторождений.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        // Земля — плоскость XZ, высота — ось Y. Порядок «кто поверх кого» стал порядком по
        // высоте: обводка и русло лежат на крышке плитки, ободок подсветки выше дороги.
        const float OutlineHeight = 0.003f;
        const float RiverHeight = 0.012f;
        const float HighlightHeight = 0.05f;

        /// <summary>Огранка стоит перед телом модельки. Моделька плоская, поэтому это её локальный z.</summary>
        const float AccentDepth = -0.005f;

        const int DecorCountSalt = 11;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int StateFogId = Shader.PropertyToID("_StateFog");
        static readonly int StateFadeId = Shader.PropertyToID("_StateFade");

        // Разведка 3D: высота плитки по биому. Ноль возвращает прежнее плоское поле, поэтому
        // весь объём откатывается одним числом `heightScale`.
        [Header("Разведка 3D")]
        [Tooltip("Общий множитель высоты. 0 — плоское поле, как было")]
        [SerializeField, Range(0f, 1f)] float heightScale = 1f;
        [Tooltip("Глубина юбки под самой низкой плиткой: без неё у края поля нет борта")]
        [SerializeField] float baseSkirt = 0.14f;

        [Header("Ландшафт")]
        [SerializeField] BiomePalette biomes = new();
        [SerializeField] Color metropolisColor = new(0.20f, 0.45f, 0.85f);
        [SerializeField, Range(0f, 0.3f)] float shadeStrength = 0.08f;

        // Состояние плитки выражает шейдер `Game/TileState`: `_StateFog` подмешивает цвет дымки
        // поверх освещения, `_StateFade` уводит альбедо в серый. Раньше и то и другое было
        // умножением цвета на CPU — оно чернило плитку и на текстурированной модели не сработает.
        [Header("Состояния плитки")]
        [Tooltip("Скрытая плитка: сколько её съела дымка")]
        [SerializeField, Range(0f, 1f)] float hiddenFog = 0.50f;
        [Tooltip("Насколько цвет биома уводится в серый под туманом: 1 — полностью серый")]
        [SerializeField, Range(0f, 1f)] float hiddenFade = 0.72f;
        [SerializeField, Range(0f, 1f)] float availableFog = 0.20f;
        [SerializeField, Range(0f, 1f)] float availableFade = 0.34f;
        [Tooltip("Истощённая плитка уже открыта, туман на неё не возвращается — она выцветает")]
        [SerializeField, Range(0f, 1f)] float depletedFog = 0.18f;
        [SerializeField, Range(0f, 1f)] float depletedFade = 0.55f;

        [Header("Река")]
        [SerializeField] Color riverColor = new(0.28f, 0.52f, 0.74f);
        [Tooltip("Шире полотна дороги: иначе на переправе русло не читается вовсе")]
        [SerializeField] float riverWidth = 0.22f;

        [Header("Обводка")]
        [SerializeField] Color outlineColor = new(0.07f, 0.07f, 0.09f);
        [SerializeField] float outlineScale = 1.06f;

        [Header("Цвета месторождений")]
        [SerializeField] ResourcePalette resources = new();
        [Tooltip("Ствол дерева и пенёк: зелёный цвет ресурса тут не годится")]
        [SerializeField] Color trunkColor = new(0.36f, 0.25f, 0.17f);
        [Tooltip("Насколько огранка светлее тела: блик на валуне, грань кристалла")]
        [SerializeField, Range(0f, 1f)] float accentLift = 0.34f;

        [Header("Модельки месторождений")]
        [SerializeField] float depositScale = 0.42f;
        [SerializeField] float depositOffset = 0.21f;

        [Header("Добыча")]
        [SerializeField] float extractionSeconds = 0.25f;
        [Tooltip("На сколько моделька приседает перед прыжком, доля высоты")]
        [SerializeField, Range(0f, 0.6f)] float extractionSquat = 0.26f;
        [SerializeField] float extractionHop = 0.07f;
        [SerializeField] float sparkScale = 0.08f;
        [SerializeField] float sparkRise = 0.24f;

        [Header("Декор биома")]
        [SerializeField, Range(0, 6)] int decorMin = 2;
        [SerializeField, Range(1, 8)] int decorMax = 5;
        [SerializeField] float decorScale = 0.26f;
        [SerializeField, Range(0f, 0.6f)] float decorScaleJitter = 0.30f;
        [SerializeField] float decorInnerRadius = 0.13f;
        [SerializeField] float decorOuterRadius = 0.33f;
        [SerializeField, Range(0f, 30f)] float decorTilt = 9f;
        [SerializeField, Range(0f, 0.4f)] float decorTintJitter = 0.14f;

        readonly List<DepositView> deposits = new();
        readonly List<MeshRenderer> decor = new();
        readonly List<float> decorTints = new();

        MeshRenderer river;
        MeshRenderer meshRenderer;
        MeshRenderer outline;
        MeshRenderer highlight;
        MeshRenderer spark;
        MaterialPropertyBlock propertyBlock;

        public HexCoord Coord { get; private set; }

        public void Bind(TileData tile)
        {
            Coord = tile.Coord;
            name = $"Hex {tile.Coord}";
            // Высота плитки — это подъём её корня по Y. Дети едут вместе с ней и сохраняют свою
            // раскладку, а юбка добирает вниз до общего дна поля.
            var height = BiomeHeight(tile) * heightScale;
            var plane = tile.Coord.ToPlane();
            transform.localPosition = new Vector3(plane.x, height, plane.y);
            GetComponent<MeshFilter>().sharedMesh = height > 0f || baseSkirt > 0f
                ? HexMeshBuilder.Prism(height + baseSkirt)
                : HexMeshBuilder.Shared;

            CreateOutline();
            CreateRiver(tile);
            CreateDecor(tile);
            CreateDeposits(tile);
            Apply(tile);
        }

        /// <summary>Перерисовать плитку под её текущее состояние.</summary>
        public void Apply(TileData tile)
        {
            // Гора не прячется туманом: она не секрет, а стена, и игрок должен видеть её сразу,
            // иначе он раз за разом тратит клики на плитку, которая всё равно не откроется.
            var state = tile.IsPassable ? StateOf(tile.State) : Vector2.zero;

            SetTile(Renderer, GroundColor(tile), state);
            SetTile(outline, outlineColor, state);

            var decorColor = Shaded(biomes.Decor(tile.Biome), tile.Shade);
            for (var i = 0; i < decor.Count; i++)
                SetTile(decor[i], Scaled(decorColor, decorTints[i]), state);

            if (river != null)
                SetTile(river, Shaded(riverColor, tile.Shade), state);

            ApplyDeposits(tile, state);
        }

        /// <summary>
        /// Добыча на плитке: моделька приседает и подпрыгивает, из неё вылетает искра цвета
        /// ресурса. Без этого выдача ресурса раз в три секунды никак не видна на поле.
        /// </summary>
        public void PlayExtraction(TileData tile, ResourceType type)
        {
            if (!isActiveAndEnabled)
                return;

            for (var i = 0; i < tile.Deposits.Count && i < deposits.Count; i++)
                if (tile.Deposits[i].Type == type)
                {
                    StartCoroutine(Extract(deposits[i], resources.Get(type)));
                    return;
                }
        }

        /// <summary>Ободок подсветки обучения поверх плитки. Прозрачный цвет прячет его.</summary>
        public void SetHighlight(Color color)
        {
            if (color.a <= 0f)
            {
                if (highlight != null)
                    highlight.gameObject.SetActive(false);

                return;
            }

            highlight ??= CreatePart(
                transform, "Highlight", ShapeMeshes.HexRing, new Vector3(0f, HighlightHeight, 0f), Vector3.one);

            highlight.gameObject.SetActive(true);
            SetColor(highlight, color);
        }

        MeshRenderer Renderer => meshRenderer != null ? meshRenderer : meshRenderer = GetComponent<MeshRenderer>();

        /// <summary>
        /// Приседание, прыжок и гаснущая искра за 0.25 секунды. Моделька построена в квадрате
        /// с центром в нуле, поэтому сжатие по высоте поднимает её основание — опускаем корень
        /// ровно на столько же, иначе моделька отрывается от земли.
        /// </summary>
        IEnumerator Extract(DepositView deposit, Color sparkColor)
        {
            spark ??= CreatePart(transform, "Spark", HexMeshBuilder.Shared, Vector3.zero, Vector3.one * sparkScale);
            spark.gameObject.SetActive(true);

            for (var elapsed = 0f; elapsed < extractionSeconds; elapsed += Time.deltaTime)
            {
                var progress = elapsed / extractionSeconds;

                // Первая четверть — присед, остальные три — прыжок и посадка.
                var squat = progress < 0.25f
                    ? Mathf.Lerp(1f, 1f - extractionSquat, progress * 4f)
                    : Mathf.Lerp(1f - extractionSquat, 1f, (progress - 0.25f) / 0.75f);
                var hop = progress < 0.25f
                    ? 0f
                    : Mathf.Sin((progress - 0.25f) / 0.75f * Mathf.PI) * extractionHop;

                deposit.Root.localScale = new Vector3(
                    depositScale * (1f + (1f - squat) * 0.5f), depositScale * squat, depositScale);
                deposit.Root.localPosition = deposit.Home
                    + new Vector3(0f, hop - (1f - squat) * 0.5f * depositScale, 0f);

                var fading = sparkColor;
                fading.a = 1f - progress;
                SetColor(spark, fading);
                spark.transform.localPosition = new Vector3(
                    deposit.Home.x,
                    deposit.Home.y + depositScale * 0.5f + progress * sparkRise,
                    deposit.Home.z);

                yield return null;
            }

            deposit.Root.localScale = Vector3.one * depositScale;
            deposit.Root.localPosition = deposit.Home;
            spark.gameObject.SetActive(false);
        }

        /// <summary>Разведка 3D: гора выше скал, скалы выше леса, луг и песок лежат внизу.</summary>
        static float BiomeHeight(TileData tile)
        {
            if (tile.IsMetropolis)
                return 0.06f;

            switch (tile.Biome)
            {
                case BiomeType.Mountains:
                    return 0.44f;
                case BiomeType.Rocks:
                    return 0.20f;
                case BiomeType.Forest:
                    return 0.07f;
                case BiomeType.Sand:
                    return 0.02f;
                default:
                    return 0.04f;
            }
        }

        /// <summary>
        /// Состояние плитки для шейдера: x — доля дымки, y — обесцвечивание. Скрытая плитка не
        /// исчезает и не чернеет: рельеф под дымкой угадывается, но цветом с открытым полем
        /// не спорит.
        /// </summary>
        Vector2 StateOf(TileState state)
        {
            switch (state)
            {
                case TileState.Hidden:
                    return new Vector2(hiddenFog, hiddenFade);
                case TileState.Available:
                    return new Vector2(availableFog, availableFade);
                case TileState.Depleted:
                    return new Vector2(depletedFog, depletedFade);
                default:
                    return Vector2.zero;
            }
        }

        Color GroundColor(TileData tile) =>
            tile.IsMetropolis ? metropolisColor : Shaded(biomes.Ground(tile.Biome), tile.Shade);

        /// <summary>Разброс тона внутри биома. Это разнообразие ландшафта, а не состояние плитки.</summary>
        Color Shaded(Color color, float shade) => Scaled(color, 1f + shade * shadeStrength);

        static Color Scaled(Color color, float factor) =>
            new(color.r * factor, color.g * factor, color.b * factor, color.a);

        void CreateOutline()
        {
            if (outline != null)
                return;

            // На плоском поле обводкой был увеличенный гекс позади плитки. В 3D «позади» — это
            // «ниже», и на ровном участке его целиком закрывают крышки соседей: граница пропадает
            // там, где она нужнее всего. Поэтому обводка стала ободком, лежащим на крышке.
            outline = CreatePart(transform, "Outline", ShapeMeshes.HexRing,
                new Vector3(0f, OutlineHeight, 0f), Vector3.one * outlineScale);
        }

        /// <summary>
        /// Русло идёт по поверхности плитки, как дорога: лента выходит из центра к серединам
        /// граней, отмеченных в маске. Геометрия у неё та же, что у дороги, — берём готовую
        /// `RoadMeshBuilder`, второй копии кривых не нужно. Развилка получается сама: три бита
        /// в маске дают три рукава.
        /// </summary>
        void CreateRiver(TileData tile)
        {
            if (tile.RiverMask == 0)
                return;

            river = CreatePart(
                transform,
                "River",
                RoadMeshBuilder.Get(tile.RiverMask, riverWidth),
                new Vector3(0f, RiverHeight, 0f),
                Vector3.one);
        }

        /// <summary>Точка лежит в русле: декор туда ставить нельзя, дерево росло бы в воде.</summary>
        static bool InsideRiver(TileData tile, Vector2 point, float width)
        {
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
            {
                if ((tile.RiverMask & (1 << direction)) == 0)
                    continue;

                // Рукав — отрезок от центра плитки до середины грани.
                var edge = HexCoord.Directions[direction].ToPlane() * 0.5f;
                var t = Mathf.Clamp01(Vector2.Dot(point, edge) / edge.sqrMagnitude);
                if (Vector2.Distance(point, edge * t) < width)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Декор биома раскладывается по хешу координаты: количество, угол, радиус, наклон,
        /// масштаб и оттенок — независимые потоки от разных солей. Один seed даёт одну и ту же
        /// карту, как требует спека, но соседние плитки одного биома больше не близнецы.
        /// </summary>
        void CreateDecor(TileData tile)
        {
            if (tile.IsMetropolis)
                return;

            var coord = tile.Coord;
            var count = decorMin + (int)(coord.Hash01(DecorCountSalt) * (decorMax - decorMin + 1));

            for (var i = 0; i < count; i++)
            {
                var angle = coord.Hash01(ItemSalt(i, 0)) * Mathf.PI * 2f;
                var radius = Mathf.Lerp(decorInnerRadius, decorOuterRadius, coord.Hash01(ItemSalt(i, 1)));
                var scale = decorScale * Mathf.Lerp(
                    1f - decorScaleJitter, 1f + decorScaleJitter, coord.Hash01(ItemSalt(i, 2)));
                var tilt = Mathf.Lerp(-decorTilt, decorTilt, coord.Hash01(ItemSalt(i, 3)));
                var shape = BiomeDecor(tile.Biome, coord.Hash01(ItemSalt(i, 4)));

                var spot = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                if (InsideRiver(tile, spot, riverWidth * 0.5f + decorScale * 0.5f))
                    continue;

                // Фигура декора построена в квадрате с центром в нуле и стоит вертикально —
                // поднимаем её на половину роста, иначе она наполовину утоплена в землю.
                var part = CreatePart(
                    transform,
                    $"Decor {i}",
                    ShapeMeshes.Decor(shape),
                    new Vector3(spot.x, scale * 0.5f, spot.y),
                    Vector3.one * scale);
                part.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

                decor.Add(part);
                decorTints.Add(Mathf.Lerp(1f - decorTintJitter, 1f + decorTintJitter, coord.Hash01(ItemSalt(i, 5))));
            }
        }

        /// <summary>Своя соль на каждый элемент и на каждое его свойство: иначе они ходили бы вместе.</summary>
        static int ItemSalt(int index, int channel) => 101 + index * 8 + channel;

        static DecorShape BiomeDecor(BiomeType biome, float roll)
        {
            switch (biome)
            {
                case BiomeType.Forest:
                    return roll < 0.62f ? DecorShape.Conifer : DecorShape.Broadleaf;
                case BiomeType.Rocks:
                    return DecorShape.LayeredPeak;
                case BiomeType.Mountains:
                    return DecorShape.Ridge;
                case BiomeType.Sand:
                    return DecorShape.Dune;
                default:
                    return DecorShape.Tussock;
            }
        }

        /// <summary>
        /// Моделька на каждое месторождение: тело и огранка под общим корнем, чтобы анимировать
        /// их одним трансформом. Позиции фиксируются здесь и `Apply` их не трогает — иначе
        /// перерисовка плитки сбивала бы анимацию добычи.
        /// </summary>
        void CreateDeposits(TileData tile)
        {
            for (var i = 0; i < tile.Deposits.Count; i++)
            {
                var home = DepositPosition(i, tile.Deposits.Count);
                var root = new GameObject($"Deposit {i}").transform;
                root.SetParent(transform, false);
                root.localPosition = home;
                root.localScale = Vector3.one * depositScale;

                deposits.Add(new DepositView
                {
                    Root = root,
                    Home = home,
                    Body = CreatePart(root, "Body", null, Vector3.zero, Vector3.one),
                    Accent = CreatePart(root, "Accent", null, new Vector3(0f, 0f, AccentDepth), Vector3.one)
                });
            }
        }

        void ApplyDeposits(TileData tile, Vector2 state)
        {
            var visible = tile.State is TileState.Revealed or TileState.Depleted;

            for (var i = 0; i < deposits.Count; i++)
            {
                var view = deposits[i];
                view.Root.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                var deposit = tile.Deposits[i];
                var spent = deposit.IsExhausted;

                // Исчерпанное месторождение выцветает, даже если сама плитка ещё нет: на плитке
                // с двумя месторождениями одно может кончиться раньше другого.
                var depositState = spent
                    ? Vector2.Max(state, new Vector2(depletedFog, depletedFade))
                    : state;

                view.Body.GetComponent<MeshFilter>().sharedMesh = ShapeMeshes.Deposit(deposit.Type, spent, false);
                view.Accent.GetComponent<MeshFilter>().sharedMesh = ShapeMeshes.Deposit(deposit.Type, spent, true);
                SetTile(view.Body, DepositColor(deposit.Type, spent, false), depositState);
                SetTile(view.Accent, DepositColor(deposit.Type, spent, true), depositState);
            }
        }

        /// <summary>
        /// У дерева огранка — ствол, он коричневый, а не зелёный; у пенька телом становится тот же
        /// ствол, а огранкой — светлый срез. У камня и руды огранка просто светлее тела.
        /// </summary>
        Color DepositColor(ResourceType type, bool exhausted, bool accent)
        {
            if (type == ResourceType.Wood)
            {
                if (!exhausted)
                    return accent ? trunkColor : resources.Get(type);

                return accent ? Color.Lerp(trunkColor, Color.white, accentLift) : trunkColor;
            }

            var body = resources.Get(type);
            return accent ? Color.Lerp(body, Color.white, accentLift) : body;
        }

        /// <summary>
        /// Одна моделька в центре, две в ряд, три треугольником. Раскладка идёт по земле (x, z),
        /// а Y поднимает модельку на половину роста, чтобы она стояла, а не тонула.
        /// </summary>
        Vector3 DepositPosition(int index, int count)
        {
            var stand = depositScale * 0.5f;

            if (count == 1)
                return new Vector3(0f, stand, 0f);

            if (count == 2)
                return new Vector3(index == 0 ? -depositOffset : depositOffset, stand, 0f);

            switch (index)
            {
                case 0:
                    return new Vector3(0f, stand, depositOffset * 1.15f);
                case 1:
                    return new Vector3(-depositOffset, stand, -depositOffset * 0.66f);
                default:
                    return new Vector3(depositOffset, stand, -depositOffset * 0.66f);
            }
        }

        MeshRenderer CreatePart(Transform parent, string partName, Mesh mesh, Vector3 localPosition, Vector3 localScale)
        {
            var part = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;

            var partRenderer = part.GetComponent<MeshRenderer>();
            partRenderer.sharedMaterial = Renderer.sharedMaterial;
            // Разведка 3D: декор и модельки лежат чуть выше земли, и под наклонным светом
            // ровно они дают единственную тень на поле. Ради неё тени и включены.
            partRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            partRenderer.receiveShadows = true;
            return partRenderer;
        }

        /// <summary>
        /// Цвет без состояния: подсветка обучения и искра добычи туманом не гасятся. Нули пишутся
        /// явно, а не полагаются на дефолт материала: блок переиспользуется между рендерерами.
        /// </summary>
        void SetColor(MeshRenderer target, Color color) => SetTile(target, color, Vector2.zero);

        /// <summary>Цвет вместе с состоянием: дымку и обесцвечивание накладывает шейдер.</summary>
        void SetTile(MeshRenderer target, Color color, Vector2 state)
        {
            propertyBlock ??= new MaterialPropertyBlock();

            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetFloat(StateFogId, state.x);
            propertyBlock.SetFloat(StateFadeId, state.y);
            target.SetPropertyBlock(propertyBlock);
        }

        /// <summary>Моделька одного месторождения: тело и огранка под общим корнем.</summary>
        sealed class DepositView
        {
            public Transform Root;
            public Vector3 Home;
            public MeshRenderer Body;
            public MeshRenderer Accent;
        }
    }
}
