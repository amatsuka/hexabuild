using System.Collections;
using System.Collections.Generic;
using Game.Economy;
using Game.Roads;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Визуал плитки: ландшафт, рельеф, декор биома и модельки месторождений.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        // Земля — плоскость XZ, высота — ось Y. Порядок «кто поверх кого» стал порядком по
        // высоте: русло лежит на крышке плитки, дорога выше русла.
        const float RiverHeight = 0.012f;
        const float RiverBankHeight = 0.006f;

        /// <summary>
        /// Урез воды в мировых координатах. Ноль выбран не для красоты: по этой же плоскости
        /// `TilePicker` отвечает, когда спуск по рельефу не встретил ни одной плитки, — то есть
        /// клик мимо суши попадает ровно в поверхность моря. Одно число на рельеф, клик и подложку.
        /// </summary>
        public const float WaterSurface = 0f;

        /// <summary>
        /// Куда садится берег. Суша ниже уреза не опускается: отмель, пробитая перевалом сквозь
        /// воду, обязана выйти над водой, иначе по ней предлагалось бы строить дорогу под водой.
        ///
        /// Взято не «чуть выше нуля», а выше глубины фаски `HexMeshBuilder.BevelDrop`: между
        /// двумя соседними плитками борта сходятся канавкой на эту глубину, и на первом замере
        /// самая низкая суша пустила в неё воду. Каждая плитка получила бирюзовую обводку из
        /// пены — ровно ту обводку, которую M16 убирала как ложащуюся поперёк объёма.
        /// </summary>
        public const float ShoreHeight = 0.09f;

        /// <summary>Дно самой глубокой воды. От него же меряется юбка: дно у поля общее.</summary>
        public const float SeaFloor = -0.26f;

        /// <summary>
        /// Самая мелкая вода. Выше неё дно не поднимается: вода обязана остаться водой — и
        /// обязана остаться видимой водой, а это уже число, а не принцип.
        ///
        /// Прежние `-0.02` формально были под урезом, но `Game/Water` меряет не знак высоты, а
        /// толщину слоя вдоль взгляда. При наклоне камеры 55° два сантиметра дна дают 0.024
        /// юнита толщины — меньше ширины пены `_FoamWidth` 0.032, — и плитка целиком попадала
        /// в кромку прибоя: ровная бледная заливка поверх серого дна вместо воды. А шум ставит
        /// почти всю воду вплотную к порогу `WaterCeiling`, то есть ровно на эту отметку.
        ///
        /// Отметка — половина высоты карты над урезом: суша стоит на 0.09–0.48, низины на
        /// 0.09–0.24, и `0.12` лежит между половинами обоих отсчётов. Толщина слоя выходит
        /// 0.15, то есть две трети `_DepthRange`. Замер по кадру: ядро залива на `-0.08` дало
        /// бирюзу (69, 154, 153) при открытом море (0, 84, 148), на этой отметке — (40, 131, 156),
        /// то есть разрыв сузился примерно вдвое. До конца он и не должен сходиться: мель у
        /// берега светлее моря и в природе. Дно глубокой впадины опущено до `SeaFloor`, где
        /// толщина перекрывает диапазон целиком, — вот она приходит ровно к тому синему,
        /// которым залита вода вокруг карты.
        /// </summary>
        public const float SeaEdge = -0.12f;

        /// <summary>
        /// На сколько плитка с рекой садится ниже своего рельефа. Русло рисуется лентой по
        /// крышке, и утопить саму ленту нельзя — крышка непрозрачная и лежит над ней. Тонет
        /// поэтому вся плитка: соседи остаются выше, и лента читается со дна канавы.
        /// </summary>
        const float RiverSink = 0.05f;

        /// <summary>
        /// Ниже этого русло не садится: река течёт над водой, а не под ней — и канавка фаски
        /// у неё тоже, иначе плитка с рекой обзаведётся той самой пенной обводкой.
        /// </summary>
        const float RiverFloor = 0.075f;

        /// <summary>Огранка стоит перед телом модельки. Моделька плоская, поэтому это её локальный z.</summary>
        const float AccentDepth = -0.005f;

        const int DecorCountSalt = 11;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int StateFogId = Shader.PropertyToID("_StateFog");
        static readonly int StateFadeId = Shader.PropertyToID("_StateFade");

        // Высота плитки идёт из того же шума, что и биом. Ноль возвращает прежнее плоское поле,
        // поэтому весь рельеф откатывается одним числом `heightScale`.
        [Header("Разведка 3D")]
        [Tooltip("Общий множитель высоты. 0 — плоское поле, как было")]
        [SerializeField, Range(0f, 1f)] float heightScale = 1f;
        [Tooltip("Глубина юбки под самой низкой плиткой: без неё у края поля нет борта")]
        [SerializeField] float baseSkirt = 0.14f;

        [Header("Ландшафт")]
        [SerializeField] BiomePalette biomes = new();
        [Tooltip("Не синий: рядом с водой синий гекс читается заливом, а не городом")]
        [SerializeField] Color metropolisColor = new(0.70f, 0.56f, 0.42f);
        [SerializeField, Range(0f, 0.3f)] float shadeStrength = 0.08f;

        // Состояние плитки выражает шейдер `Game/TileState`: `_StateFog` подмешивает цвет дымки
        // поверх освещения, `_StateFade` уводит альбедо в серый. Раньше и то и другое было
        // умножением цвета на CPU — оно чернило плитку и на текстурированной модели не сработает.
        [Header("Состояния плитки")]
        [Tooltip("Скрытая плитка: сколько её съела дымка")]
        [SerializeField, Range(0f, 1f)] float hiddenFog = 0.78f;
        [Tooltip("Насколько цвет биома уводится в серый под туманом: 1 — полностью серый")]
        [SerializeField, Range(0f, 1f)] float hiddenFade = 0.85f;
        [SerializeField, Range(0f, 1f)] float availableFog = 0.45f;
        [SerializeField, Range(0f, 1f)] float availableFade = 0.45f;
        [Tooltip("Истощённая плитка уже открыта, туман на неё не возвращается — она выцветает")]
        [SerializeField, Range(0f, 1f)] float depletedFog = 0.20f;
        [SerializeField, Range(0f, 1f)] float depletedFade = 0.85f;

        [Header("Река")]
        [SerializeField] Color riverColor = new(0.28f, 0.52f, 0.74f);
        [Tooltip("Шире полотна дороги: иначе на переправе русло не читается вовсе")]
        [SerializeField] float riverWidth = 0.22f;
        [Tooltip("Светлая кромка берега под лентой русла: без неё канава читается дырой")]
        [SerializeField] Color riverBankColor = new(0.70f, 0.66f, 0.53f);
        [SerializeField] float riverBankWidth = 0.30f;

        [Header("Цвета месторождений")]
        [SerializeField] ResourcePalette resources = new();
        [Tooltip("Ствол дерева и пенёк: зелёный цвет ресурса тут не годится")]
        [SerializeField] Color trunkColor = new(0.36f, 0.25f, 0.17f);
        [Tooltip("Насколько огранка светлее тела: блик на валуне, грань кристалла")]
        [SerializeField, Range(0f, 1f)] float accentLift = 0.34f;

        [Header("Модельки месторождений")]
        [SerializeField] float depositScale = 0.42f;
        [SerializeField] float depositOffset = 0.21f;
        [Tooltip("Материал с палитрой ресурсов: у стеков своя текстура, не та, что у деревьев")]
        [SerializeField] Material resourceMaterial;
        [Tooltip("Штабель брёвен. Пусто — процедурная фигура, как было")]
        [SerializeField] Mesh woodDepositModel;
        [Tooltip("Груда камня")]
        [SerializeField] Mesh stoneDepositModel;
        [Tooltip("Стопка слитков")]
        [SerializeField] Mesh oreDepositModel;
        [Tooltip("Наибольший габарит стека в юнитах")]
        [SerializeField, Range(0.1f, 0.8f)] float depositModelSize = 0.30f;
        [Tooltip("Во сколько усыхает выработанный стек: форма у модели одна на оба состояния, и без этого истощение видно только дымкой")]
        [SerializeField, Range(0.2f, 1f)] float spentModelScale = 0.55f;

        [Header("Добыча")]
        [SerializeField] float extractionSeconds = 0.25f;
        [Tooltip("На сколько моделька приседает перед прыжком, доля высоты")]
        [SerializeField, Range(0f, 0.6f)] float extractionSquat = 0.26f;
        [SerializeField] float extractionHop = 0.07f;
        [SerializeField] float sparkScale = 0.08f;
        [SerializeField] float sparkRise = 0.24f;

        // Модели KayKit развёрнуты на палитровый атлас: цвет грани задаёт её UV, а не код.
        // Поэтому у модельной части свой материал и белый `_BaseColor` — умножать цвет биома
        // на зелёный из текстуры нельзя, дерево почернеет.
        [Header("Модели")]
        [Tooltip("Материал с палитровым атласом. Пусто — весь декор рисуется процедурными фигурами")]
        [SerializeField] Material paletteMaterial;
        [Tooltip("Деревья леса. Вариант выбирается хешем, пустой список — процедурная фигура")]
        [SerializeField] Mesh[] treeModels;
        [Tooltip("Камни скал и гор. Вариант выбирается хешем, пустой список — процедурная фигура")]
        [SerializeField] Mesh[] rockModels;
        [Tooltip("Наибольший габарит дерева в юнитах. Инрадиус гекса — 0.5")]
        [SerializeField, Range(0.1f, 1f)] float treeSize = 0.44f;
        [Tooltip("Наибольший габарит камня в юнитах")]
        [SerializeField, Range(0.05f, 0.6f)] float rockSize = 0.24f;
        [Tooltip("Здание Метрополии. Пусто — гекс остаётся голым песчаниковым, как до M21")]
        [SerializeField] Mesh metropolisModel;
        [Tooltip("След здания по земле в юнитах. Выше 0.669 угол основания уходит за плоский верх")]
        [SerializeField, Range(0.2f, 1f)] float metropolisFootprint = 0.65f;

        [Header("Декор биома")]
        [SerializeField, Range(0, 8)] int decorMin = 3;
        [SerializeField, Range(1, 10)] int decorMax = 6;
        [SerializeField] float decorScale = 0.32f;
        [SerializeField, Range(0f, 0.6f)] float decorScaleJitter = 0.30f;
        [SerializeField] float decorInnerRadius = 0.13f;
        [SerializeField] float decorOuterRadius = 0.33f;
        [SerializeField, Range(0f, 30f)] float decorTilt = 9f;
        [SerializeField, Range(0f, 0.4f)] float decorTintJitter = 0.14f;

        readonly List<DepositView> deposits = new();
        readonly List<DecorPart> decor = new();

        MeshRenderer river;
        MeshRenderer riverBank;
        MeshRenderer metropolis;
        MeshRenderer meshRenderer;
        MeshRenderer spark;
        MaterialPropertyBlock propertyBlock;

        public HexCoord Coord { get; private set; }

        /// <summary>
        /// Мировая высота крышки плитки. По этой плоскости бьёт луч клика: по земле `y = 0` он
        /// промахивается мимо приподнятой плитки на «высота / tg(pitch)».
        /// </summary>
        public float SurfaceHeight => transform.position.y;

        public void Bind(TileData tile)
        {
            Coord = tile.Coord;
            name = $"Hex {tile.Coord}";
            // Высота плитки — это подъём её корня по Y. Дети едут вместе с ней и сохраняют свою
            // раскладку, а юбка добирает вниз до общего дна поля.
            var height = HeightOf(tile) * heightScale;
            var plane = tile.Coord.ToPlane();
            transform.localPosition = new Vector3(plane.x, height, plane.y);
            // Юбка меряется от общего дна поля, а не от нуля: с водой самая низкая крышка ушла
            // под урез, и «height + baseSkirt» дал бы у неё юбку отрицательной длины.
            var skirt = height - SeaFloor * heightScale + baseSkirt;
            GetComponent<MeshFilter>().sharedMesh = skirt > 0f
                ? HexMeshBuilder.Prism(skirt)
                : HexMeshBuilder.Shared;

            CreateRiver(tile);
            CreateMetropolis(tile);
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

            var decorColor = Shaded(biomes.Decor(tile.Biome), tile.Shade);
            var modelColor = Shaded(Color.white, tile.Shade);
            for (var i = 0; i < decor.Count; i++)
                SetTile(decor[i].Renderer, Scaled(decor[i].Textured ? modelColor : decorColor, decor[i].Tint), state);

            if (metropolis != null)
                SetTile(metropolis, modelColor, state);

            if (riverBank != null)
                SetTile(riverBank, Shaded(riverBankColor, tile.Shade), state);

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

                // Приседание тянет фигуру вниз только у процедурной модельки: она построена
                // с центром в нуле, и сжатие по высоте поднимает её основание над землёй.
                // У модели пивот в основании, компенсировать нечего.
                var rest = deposit.Rest;
                var sink = deposit.Textured ? 0f : (1f - squat) * 0.5f * rest;
                deposit.Root.localScale = new Vector3(
                    rest * (1f + (1f - squat) * 0.5f), rest * squat, rest);
                deposit.Root.localPosition = deposit.Home + new Vector3(0f, hop - sink, 0f);

                var fading = sparkColor;
                fading.a = 1f - progress;
                SetColor(spark, fading);
                spark.transform.localPosition = new Vector3(
                    deposit.Home.x,
                    deposit.Home.y + deposit.Rest * 0.5f + progress * sparkRise,
                    deposit.Home.z);

                yield return null;
            }

            // Возвращаемся к текущему покою, а не к полному размеру: последняя добыча могла
            // исчерпать месторождение, и `Apply` уже усадил стек.
            deposit.Root.localScale = Vector3.one * deposit.Rest;
            deposit.Root.localPosition = deposit.Home;
            spark.gameObject.SetActive(false);
        }

        /// <summary>
        /// Высота плитки в юнитах из шума, который выбрал ей биом. Ступени по биому давали не
        /// рельеф, а пять плато: весь лес стоял на одном уровне. Кривая ломаная, и её узлы —
        /// ровно пороги биомов: внутри биома высота идёт непрерывно, а на границе прибавляет
        /// крутизны, поэтому горы всё так же возвышаются, а низины остаются плоскими.
        /// Метрополия высоты не выбирает: город на своём холме сидел бы в яме между скал.
        /// </summary>
        public static float HeightOf(TileData tile)
        {
            var height = TerrainHeight(tile.Elevation);

            // Вода — единственный биом, которому позволено лежать под урезом: она и есть дно.
            // И обязана там остаться: перевал меняет биом, но не высоту, и обратная замена
            // суши на воду вынесла бы дно на поверхность.
            if (tile.Biome == BiomeType.Water)
                return Mathf.Min(height, SeaEdge);

            // Суша всплывает на берег. Перевал, пробитый сквозь залив, оставляет плитке её
            // низинную высоту, и без этого он вышел бы отмелью под водой — проходимой по
            // правилам и невидимой на экране.
            height = Mathf.Max(height, ShoreHeight);

            return tile.HasRiver ? Mathf.Max(height - RiverSink, RiverFloor) : height;
        }

        /// <summary>
        /// Узлы кривой: порог биома — высота его верхней кромки в юнитах. Низины ушли под урез
        /// вместе с водой, поэтому кривая начинается со дна моря, а не с нуля; берег стартует
        /// сразу над урезом, и переход через воду — единственный разрыв на всей кривой.
        /// </summary>
        public static float TerrainHeight(float elevation)
        {
            if (elevation < MapGenerator.WaterCeiling)
                return Mathf.Lerp(SeaFloor, SeaEdge, Ratio(elevation, 0f, MapGenerator.WaterCeiling));
            if (elevation < MapGenerator.SandCeiling)
                return Mathf.Lerp(ShoreHeight, 0.12f, Ratio(elevation, MapGenerator.WaterCeiling, MapGenerator.SandCeiling));
            if (elevation < MapGenerator.MeadowCeiling)
                return Mathf.Lerp(0.12f, 0.17f, Ratio(elevation, MapGenerator.SandCeiling, MapGenerator.MeadowCeiling));
            if (elevation < MapGenerator.ForestCeiling)
                return Mathf.Lerp(0.17f, 0.24f, Ratio(elevation, MapGenerator.MeadowCeiling, MapGenerator.ForestCeiling));
            if (elevation < MapGenerator.RocksCeiling)
                return Mathf.Lerp(0.24f, 0.33f, Ratio(elevation, MapGenerator.ForestCeiling, MapGenerator.RocksCeiling));

            return Mathf.Lerp(0.33f, 0.48f, Ratio(elevation, MapGenerator.RocksCeiling, 1f));
        }

        static float Ratio(float value, float from, float to) => Mathf.Clamp01((value - from) / (to - from));

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

        /// <summary>
        /// Русло идёт по поверхности плитки: лента выходит из центра к серединам граней,
        /// отмеченных в маске. Развилка получается сама: три бита в маске дают три рукава.
        /// Дорога такой лентой была до M20 и делила с руслом один билдер; теперь она объёмная
        /// насыпь, а плоская лента осталась руслу одному — `RiverMeshBuilder`.
        /// </summary>
        void CreateRiver(TileData tile)
        {
            // Устье впадает в воду: на самой водной плитке ленту рисовать нечем и незачем —
            // она лежала бы на дне под морем.
            if (tile.RiverMask == 0 || tile.Biome == BiomeType.Water)
                return;

            // Два слоя, как у дороги, только наоборот: широкая светлая кромка берега снизу,
            // узкая тёмная вода поверх. Кромка и делает канаву руслом, а не дырой в плитке.
            riverBank = CreatePart(
                transform,
                "RiverBank",
                RiverMeshBuilder.Get(tile.RiverMask, riverBankWidth),
                new Vector3(0f, RiverBankHeight, 0f),
                Vector3.one);

            river = CreatePart(
                transform,
                "River",
                RiverMeshBuilder.Get(tile.RiverMask, riverWidth),
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
        /// Здание Метрополии стоит на своей плитке одной моделью. Масштаб считается **по следу
        /// на земле**, а не по наибольшему габариту, как у декора и стеков: замок вдвое выше
        /// своего основания, и приведение по высоте оставило бы от него башенку в треть плитки.
        /// Ограничение здесь — крышка гекса, а не рост.
        ///
        /// Потолок следа — 0.669, и он посчитан, а не подобран. Плоский верх после фаски это
        /// шестиугольник с инрадиусом 0.422 по X и вершиной 0.487 по Z; основание замка — самая
        /// широкая его часть (полуширины 0.950 и 1.097 при габарите 2.26), свесить за кромку
        /// одни крыши не выйдет. Выше потолка угол основания встаёт над фаской и висит в воздухе.
        /// </summary>
        void CreateMetropolis(TileData tile)
        {
            if (!tile.IsMetropolis || metropolisModel == null || paletteMaterial == null)
                return;

            metropolis = CreatePart(
                transform,
                "Metropolis",
                metropolisModel,
                Vector3.zero,
                Vector3.one * FootprintScale(metropolisModel, metropolisFootprint),
                paletteMaterial);
        }

        /// <summary>
        /// Приведение следа модели на земле к заданной ширине. Отличается от `ModelScale` тем,
        /// что смотрит только на X и Z: рост зданию оставлен свой.
        /// </summary>
        static float FootprintScale(Mesh model, float target)
        {
            var size = model.bounds.size;
            var footprint = Mathf.Max(size.x, size.z);
            return footprint > 0.0001f ? target / footprint : target;
        }

        /// <summary>
        /// Декор биома раскладывается по хешу координаты: количество, угол, радиус, наклон,
        /// масштаб и оттенок — независимые потоки от разных солей. Один seed даёт одну и ту же
        /// карту, как требует спека, но соседние плитки одного биома больше не близнецы.
        /// </summary>
        void CreateDecor(TileData tile)
        {
            // Дну декор не положен: под водой его всё равно не разглядеть, а торчащая сквозь
            // урез верхушка холмика читалась бы мусором посреди моря.
            if (tile.IsMetropolis || tile.Biome == BiomeType.Water)
                return;

            var coord = tile.Coord;
            var count = decorMin + (int)(coord.Hash01(DecorCountSalt) * (decorMax - decorMin + 1));

            // Стек месторождения занимает середину плитки. Декор не выбрасывается, а отходит
            // к ободу: иначе плитка с месторождением остаётся голой, а она как раз главная.
            var keepOut = DepositKeepOut(tile);
            var inner = Mathf.Max(decorInnerRadius, keepOut);
            var outer = Mathf.Max(decorOuterRadius, keepOut + 0.10f);

            for (var i = 0; i < count; i++)
            {
                var angle = coord.Hash01(ItemSalt(i, 0)) * Mathf.PI * 2f;
                var radius = Mathf.Lerp(inner, outer, coord.Hash01(ItemSalt(i, 1)));
                var jitter = Mathf.Lerp(
                    1f - decorScaleJitter, 1f + decorScaleJitter, coord.Hash01(ItemSalt(i, 2)));
                var scale = decorScale * jitter;
                var tilt = Mathf.Lerp(-decorTilt, decorTilt, coord.Hash01(ItemSalt(i, 3)));
                var shape = BiomeDecor(tile.Biome, coord.Hash01(ItemSalt(i, 4)));

                var spot = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                if (InsideRiver(tile, spot, riverWidth * 0.5f + decorScale * 0.5f))
                    continue;

                // Месторождение на плитке — главный объект, и оно занимает ту же середину, куда
                // ложится декор. Пока обе стороны были плоскими фигурами, наложение читалось как
                // кустик за камнем; объёмные модели врастают друг в друга.
                if (OccupiedByDeposit(tile, spot, keepOut))
                    continue;

                // Габарит модели из пака приводится к заданному размеру, холмик и плоская
                // фигура построены в единичном квадрате и берут `decorScale` как есть.
                var model = DecorModel(shape, coord.Hash01(ItemSalt(i, 7)));
                // Объёмная фигура стоит основанием в нуле — и модель из пака, и процедурный
                // холмик. Плоская фигура построена в квадрате с центром в нуле, её надо поднять
                // на половину роста, иначе она наполовину утоплена в землю.
                var grounded = model != null || ShapeMeshes.StandsOnGround(shape);
                var part = CreatePart(
                    transform,
                    $"Decor {i}",
                    model != null ? model : ShapeMeshes.Decor(shape),
                    new Vector3(spot.x, grounded ? 0f : scale * 0.5f, spot.y),
                    Vector3.one * (model != null ? ModelScale(model, DecorTarget(shape)) * jitter : scale),
                    model != null ? paletteMaterial : null);

                // У плоской фигуры наклон был креном в плоскости экрана. У объёмной такой крен
                // валит дерево набок, поэтому он раскладывается по осям земли, а тот же хеш
                // вдобавок разворачивает фигуру вокруг своей оси: иначе все деревья — близнецы.
                part.transform.localRotation = grounded
                    ? Quaternion.Euler(tilt * 0.5f, coord.Hash01(ItemSalt(i, 6)) * 360f, tilt * 0.5f)
                    : Quaternion.Euler(0f, 0f, tilt);

                decor.Add(new DecorPart
                {
                    Renderer = part,
                    Tint = Mathf.Lerp(1f - decorTintJitter, 1f + decorTintJitter, coord.Hash01(ItemSalt(i, 5))),
                    Textured = model != null
                });
            }
        }

        /// <summary>
        /// Радиус, который стек месторождения держит за собой. Ноль — месторождений на модели
        /// нет, и декор раскладывается как раньше. Множитель не половина суммы габаритов: полное
        /// расстояние разносит объекты слишком далеко, а лёгкое перекрытие силуэтов на глаз
        /// читается как заросли, а не как ошибка.
        /// </summary>
        float DepositKeepOut(TileData tile)
        {
            for (var i = 0; i < tile.Deposits.Count; i++)
                if (DepositModel(tile.Deposits[i].Type) != null)
                    return (depositModelSize + treeSize) * 0.4f;

            return 0f;
        }

        /// <summary>Точка занята стеком месторождения: декор туда не ставим.</summary>
        bool OccupiedByDeposit(TileData tile, Vector2 point, float keepOut)
        {
            if (keepOut <= 0f)
                return false;

            for (var i = 0; i < tile.Deposits.Count; i++)
            {
                if (DepositModel(tile.Deposits[i].Type) == null)
                    continue;

                var home = DepositPosition(i, tile.Deposits.Count, 0f);
                if (Vector2.Distance(point, new Vector2(home.x, home.z)) < keepOut)
                    return true;
            }

            return false;
        }

        /// <summary>Своя соль на каждый элемент и на каждое его свойство: иначе они ходили бы вместе.</summary>
        static int ItemSalt(int index, int channel) => 101 + index * 8 + channel;

        /// <summary>
        /// Форма декора по биому. На песке и лугу часть слотов отдана валунам: без них
        /// низины остаются почти пустыми рядом с лесом и скалами, где стоят модели.
        /// </summary>
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
                    return roll < 0.25f ? DecorShape.LayeredPeak : DecorShape.Dune;
                default:
                    return roll < 0.18f ? DecorShape.LayeredPeak : DecorShape.Tussock;
            }
        }

        /// <summary>Модель под форму декора. Null — рисуется процедурная фигура, как было.</summary>
        Mesh DecorModel(DecorShape shape, float roll)
        {
            if (paletteMaterial == null)
                return null;

            switch (shape)
            {
                case DecorShape.Conifer:
                case DecorShape.Broadleaf:
                    return Pick(treeModels, roll);
                case DecorShape.LayeredPeak:
                case DecorShape.Ridge:
                    return Pick(rockModels, roll);
                default:
                    return null;
            }
        }

        float DecorTarget(DecorShape shape) =>
            shape is DecorShape.LayeredPeak or DecorShape.Ridge ? rockSize : treeSize;

        static Mesh Pick(Mesh[] models, float roll) =>
            models == null || models.Length == 0 ? null : models[Mathf.Min((int)(roll * models.Length), models.Length - 1)];

        /// <summary>
        /// Модели пака сделаны под гекс радиуса около единицы, а у нас инрадиус 0.5, и дерево
        /// с камнем отличаются габаритом впятеро. Поэтому масштаб не множитель, а приведение
        /// наибольшего габарита меша к заданному размеру: одно правило на любую модель.
        /// </summary>
        static float ModelScale(Mesh model, float target)
        {
            var size = model.bounds.size;
            var largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            return largest > 0.0001f ? target / largest : target;
        }

        /// <summary>Стек ресурса под тип месторождения. Null — процедурная фигура, как было.</summary>
        Mesh DepositModel(ResourceType type)
        {
            if (resourceMaterial == null)
                return null;

            switch (type)
            {
                case ResourceType.Wood:
                    return woodDepositModel;
                case ResourceType.Stone:
                    return stoneDepositModel;
                case ResourceType.Ore:
                    return oreDepositModel;
                default:
                    return null;
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
                // Модель стека стоит основанием в нуле, процедурная фигура построена в квадрате
                // с центром в нуле — ей нужен подъём на половину роста.
                var model = DepositModel(tile.Deposits[i].Type);
                var size = model != null ? ModelScale(model, depositModelSize) : depositScale;
                var home = DepositPosition(i, tile.Deposits.Count, model != null ? 0f : depositScale * 0.5f);
                var root = new GameObject($"Deposit {i}").transform;
                root.SetParent(transform, false);
                root.localPosition = home;
                root.localScale = Vector3.one * size;

                deposits.Add(new DepositView
                {
                    Root = root,
                    Home = home,
                    Size = size,
                    Rest = size,
                    Textured = model != null,
                    Body = CreatePart(root, "Body", model, Vector3.zero, Vector3.one, model != null ? resourceMaterial : null),
                    // Огранка — приём плоской фигуры: у модели рельеф свой, второй меш не нужен.
                    Accent = model != null
                        ? null
                        : CreatePart(root, "Accent", null, new Vector3(0f, 0f, AccentDepth), Vector3.one)
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

                if (view.Textured)
                {
                    // Цвет стека лежит в атласе, а форма у него одна на оба состояния: выработанный
                    // стек уменьшается — дымки одной мало, чтобы прочитать «здесь уже пусто».
                    view.Rest = spent ? view.Size * spentModelScale : view.Size;
                    view.Root.localScale = Vector3.one * view.Rest;
                    SetTile(view.Body, Color.white, depositState);
                    continue;
                }

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
        Vector3 DepositPosition(int index, int count, float stand)
        {
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

        MeshRenderer CreatePart(
            Transform parent,
            string partName,
            Mesh mesh,
            Vector3 localPosition,
            Vector3 localScale,
            Material material = null)
        {
            var part = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;

            var partRenderer = part.GetComponent<MeshRenderer>();
            partRenderer.sharedMaterial = material != null ? material : Renderer.sharedMaterial;
            // Разведка 3D: декор и модельки лежат чуть выше земли, и под наклонным светом
            // ровно они дают единственную тень на поле. Ради неё тени и включены.
            partRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            partRenderer.receiveShadows = true;
            return partRenderer;
        }

        /// <summary>
        /// Цвет без состояния: искра добычи туманом не гасится. Нули пишутся явно, а не
        /// полагаются на дефолт материала: блок переиспользуется между рендерерами.
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

        /// <summary>
        /// Элемент декора. Текстурированной части цвет задаёт атлас, и код красит её белым
        /// с разбросом тона; процедурная берёт цвет биома, как раньше.
        /// </summary>
        sealed class DecorPart
        {
            public MeshRenderer Renderer;
            public float Tint;
            public bool Textured;
        }

        /// <summary>Моделька одного месторождения: тело и огранка под общим корнем.</summary>
        sealed class DepositView
        {
            public Transform Root;
            public Vector3 Home;
            public float Size;
            /// <summary>Масштаб покоя: полный размер или усохший, если месторождение выработано.</summary>
            public float Rest;
            public bool Textured;
            public MeshRenderer Body;
            public MeshRenderer Accent;
        }
    }
}
