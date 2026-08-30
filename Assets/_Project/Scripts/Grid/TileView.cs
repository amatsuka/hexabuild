using System.Collections;
using System.Collections.Generic;
using Game.Economy;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Визуал плитки: ландшафт, обводка, декор биома и модельки месторождений.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        // Камера смотрит вдоль +Z: чем меньше z, тем ближе к зрителю. Модельки месторождений
        // лежат ближе дороги — по ним игрок читает плитку, дорога их перекрывать не должна.
        const float OutlineDepth = 0.01f;
        const float RiverDepth = -0.015f;
        const float DecorDepth = -0.03f;
        const float DepositDepth = -0.09f;
        const float AccentDepth = -0.005f;
        const float HighlightDepth = -0.10f;
        const float SparkDepth = -0.11f;

        const int DecorCountSalt = 11;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Ландшафт")]
        [SerializeField] BiomePalette biomes = new();
        [SerializeField] Color metropolisColor = new(0.20f, 0.45f, 0.85f);
        [SerializeField, Range(0f, 1f)] float hiddenDim = 0.26f;
        [SerializeField, Range(0f, 1f)] float availableDim = 0.62f;
        [Tooltip("Насколько цвет биома уводится в серый под туманом: 1 — полностью серый")]
        [SerializeField, Range(0f, 1f)] float hiddenDesaturation = 0.88f;
        [SerializeField, Range(0f, 1f)] float availableDesaturation = 0.45f;
        [SerializeField, Range(0f, 1f)] float depletedAlpha = 0.35f;
        [SerializeField, Range(0f, 0.3f)] float shadeStrength = 0.08f;

        [Header("Река")]
        [SerializeField] Color riverColor = new(0.28f, 0.52f, 0.74f);
        [SerializeField] float riverWidth = 0.17f;

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
        readonly List<MeshRenderer> river = new();

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
            transform.localPosition = tile.Coord.ToWorld();
            GetComponent<MeshFilter>().sharedMesh = HexMeshBuilder.Shared;

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
            var dim = tile.IsPassable ? StateDim(tile.State) : 1f;
            var fade = tile.IsPassable ? StateDesaturation(tile.State) : 0f;
            SetColor(Renderer, GroundColor(tile, dim, fade));

            var decorColor = Shaded(biomes.Decor(tile.Biome), tile.Shade, dim, fade);
            if (tile.State == TileState.Depleted)
                decorColor.a = depletedAlpha;

            for (var i = 0; i < decor.Count; i++)
                SetColor(decor[i], Scaled(decorColor, decorTints[i]));

            var stream = Shaded(riverColor, tile.Shade, dim, fade);
            foreach (var band in river)
                SetColor(band, stream);

            ApplyDeposits(tile);
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
                transform, "Highlight", ShapeMeshes.HexRing, new Vector3(0f, 0f, HighlightDepth), Vector3.one);

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
                    depositScale * (1f + (1f - squat) * 0.5f), depositScale * squat, 1f);
                deposit.Root.localPosition = deposit.Home
                    + new Vector3(0f, hop - (1f - squat) * 0.5f * depositScale, 0f);

                var fading = sparkColor;
                fading.a = 1f - progress;
                SetColor(spark, fading);
                spark.transform.localPosition = new Vector3(
                    deposit.Home.x,
                    deposit.Home.y + depositScale * 0.5f + progress * sparkRise,
                    SparkDepth);

                yield return null;
            }

            deposit.Root.localScale = Vector3.one * depositScale;
            deposit.Root.localPosition = deposit.Home;
            spark.gameObject.SetActive(false);
        }

        /// <summary>Скрытая плитка не прячет ландшафт полностью — она просто сильно затемнена.</summary>
        float StateDim(TileState state)
        {
            switch (state)
            {
                case TileState.Hidden:
                    return hiddenDim;
                case TileState.Available:
                    return availableDim;
                default:
                    return 1f;
            }
        }

        /// <summary>Под туманом биом почти уходит в серый: рельеф угадывается, но не бросается в глаза.</summary>
        float StateDesaturation(TileState state)
        {
            switch (state)
            {
                case TileState.Hidden:
                    return hiddenDesaturation;
                case TileState.Available:
                    return availableDesaturation;
                default:
                    return 0f;
            }
        }

        Color GroundColor(TileData tile, float dim, float fade)
        {
            if (tile.IsMetropolis)
                return metropolisColor;

            var ground = Shaded(biomes.Ground(tile.Biome), tile.Shade, dim, fade);
            if (tile.State == TileState.Depleted)
                ground.a = depletedAlpha;

            return ground;
        }

        Color Shaded(Color color, float shade, float dim, float fade)
        {
            var grey = color.grayscale;
            var faded = new Color(
                Mathf.Lerp(color.r, grey, fade),
                Mathf.Lerp(color.g, grey, fade),
                Mathf.Lerp(color.b, grey, fade),
                color.a);

            return Scaled(faded, dim * (1f + shade * shadeStrength));
        }

        static Color Scaled(Color color, float factor) =>
            new(color.r * factor, color.g * factor, color.b * factor, color.a);

        void CreateOutline()
        {
            if (outline != null)
                return;

            outline = CreatePart(transform, "Outline", HexMeshBuilder.Shared,
                new Vector3(0f, 0f, OutlineDepth), Vector3.one * outlineScale);
            SetColor(outline, outlineColor);
        }

        /// <summary>
        /// Река рисуется вдоль ребра, а не по плитке: каждая из двух соседних плиток кладёт свою
        /// половину ленты со своей стороны, вместе получается сплошное русло. У края поля видна
        /// одна половина — там соседа просто нет.
        /// </summary>
        void CreateRiver(TileData tile)
        {
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
            {
                if (!tile.HasRiver(direction))
                    continue;

                var toNeighbor = HexCoord.Directions[direction].ToWorld();
                var half = riverWidth * 0.5f;
                var band = CreatePart(
                    transform,
                    $"River {direction}",
                    ShapeMeshes.Bar,
                    new Vector3(toNeighbor.x * (0.5f - half * 0.5f), toNeighbor.y * (0.5f - half * 0.5f), RiverDepth),
                    // Длинная сторона вдоль грани, короткая поперёк; с запасом, чтобы соседние
                    // отрезки русла смыкались на углах гекса.
                    new Vector3(half, HexCoord.Size * 1.08f, 1f));
                band.transform.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Atan2(toNeighbor.y, toNeighbor.x) * Mathf.Rad2Deg);

                river.Add(band);
            }
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

                var part = CreatePart(
                    transform,
                    $"Decor {i}",
                    ShapeMeshes.Decor(shape),
                    new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, DecorDepth),
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

        void ApplyDeposits(TileData tile)
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
                var body = DepositColor(deposit.Type, spent, false);
                var accent = DepositColor(deposit.Type, spent, true);

                if (spent)
                {
                    body.a = depletedAlpha;
                    accent.a = depletedAlpha;
                }

                view.Body.GetComponent<MeshFilter>().sharedMesh = ShapeMeshes.Deposit(deposit.Type, spent, false);
                view.Accent.GetComponent<MeshFilter>().sharedMesh = ShapeMeshes.Deposit(deposit.Type, spent, true);
                SetColor(view.Body, body);
                SetColor(view.Accent, accent);
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

        /// <summary>Одна моделька в центре, две в ряд, три треугольником.</summary>
        Vector3 DepositPosition(int index, int count)
        {
            if (count == 1)
                return new Vector3(0f, 0f, DepositDepth);

            if (count == 2)
                return new Vector3(index == 0 ? -depositOffset : depositOffset, 0f, DepositDepth);

            switch (index)
            {
                case 0:
                    return new Vector3(0f, depositOffset * 1.15f, DepositDepth);
                case 1:
                    return new Vector3(-depositOffset, -depositOffset * 0.66f, DepositDepth);
                default:
                    return new Vector3(depositOffset, -depositOffset * 0.66f, DepositDepth);
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
            partRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            partRenderer.receiveShadows = false;
            return partRenderer;
        }

        void SetColor(MeshRenderer target, Color color)
        {
            propertyBlock ??= new MaterialPropertyBlock();

            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
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
