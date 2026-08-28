using System.Collections.Generic;
using Game.Economy;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Визуал плитки: ландшафт, обводка, декор биома и точки месторождений.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        const float OutlineDepth = 0.01f;
        const float DecorDepth = -0.03f;
        const float DotDepth = -0.05f;

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

        [Header("Обводка")]
        [SerializeField] Color outlineColor = new(0.07f, 0.07f, 0.09f);
        [SerializeField] float outlineScale = 1.06f;

        [Header("Цвета месторождений")]
        [SerializeField] ResourcePalette resources = new();

        [Header("Точки месторождений")]
        [SerializeField] float dotScale = 0.22f;
        [SerializeField] float dotOffset = 0.2f;

        readonly List<MeshRenderer> dots = new();
        readonly List<MeshRenderer> decor = new();

        MeshRenderer meshRenderer;
        MeshRenderer outline;
        MaterialPropertyBlock propertyBlock;

        public HexCoord Coord { get; private set; }

        public void Bind(TileData tile)
        {
            Coord = tile.Coord;
            name = $"Hex {tile.Coord}";
            transform.localPosition = tile.Coord.ToWorld();
            GetComponent<MeshFilter>().sharedMesh = HexMeshBuilder.Shared;

            CreateOutline();
            CreateDecor(tile);
            Apply(tile);
        }

        /// <summary>Перерисовать плитку под её текущее состояние.</summary>
        public void Apply(TileData tile)
        {
            var dim = StateDim(tile.State);
            var fade = StateDesaturation(tile.State);
            SetColor(Renderer, GroundColor(tile, dim, fade));

            var decorColor = Shaded(biomes.Decor(tile.Biome), tile.Shade, dim, fade);
            if (tile.State == TileState.Depleted)
                decorColor.a = depletedAlpha;

            foreach (var part in decor)
                SetColor(part, decorColor);

            ApplyDeposits(tile);
        }

        MeshRenderer Renderer => meshRenderer != null ? meshRenderer : meshRenderer = GetComponent<MeshRenderer>();

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

            var factor = dim * (1f + shade * shadeStrength);
            return new Color(faded.r * factor, faded.g * factor, faded.b * factor, faded.a);
        }

        void CreateOutline()
        {
            if (outline != null)
                return;

            outline = CreatePart("Outline", HexMeshBuilder.Shared,
                new Vector3(0f, 0f, OutlineDepth), Vector3.one * outlineScale);
            SetColor(outline, outlineColor);
        }

        /// <summary>Пара-тройка примитивов по биому: ёлки, скалы, полоски воды или песка.</summary>
        void CreateDecor(TileData tile)
        {
            if (tile.IsMetropolis)
                return;

            switch (tile.Biome)
            {
                case BiomeType.Forest:
                    AddDecor(ShapeMeshes.Triangle, new Vector2(-0.19f, -0.06f), new Vector2(0.26f, 0.30f));
                    AddDecor(ShapeMeshes.Triangle, new Vector2(0.01f, 0.13f), new Vector2(0.24f, 0.28f));
                    AddDecor(ShapeMeshes.Triangle, new Vector2(0.19f, -0.09f), new Vector2(0.22f, 0.26f));
                    break;
                case BiomeType.Rocks:
                    AddDecor(ShapeMeshes.Triangle, new Vector2(-0.14f, -0.04f), new Vector2(0.36f, 0.26f));
                    AddDecor(ShapeMeshes.Triangle, new Vector2(0.15f, 0.06f), new Vector2(0.28f, 0.20f));
                    break;
                case BiomeType.Water:
                    AddDecor(ShapeMeshes.Bar, new Vector2(-0.06f, 0.12f), new Vector2(0.34f, 0.06f));
                    AddDecor(ShapeMeshes.Bar, new Vector2(0.07f, -0.09f), new Vector2(0.28f, 0.06f));
                    break;
                case BiomeType.Sand:
                    AddDecor(ShapeMeshes.Bar, new Vector2(-0.10f, 0.10f), new Vector2(0.20f, 0.05f));
                    AddDecor(ShapeMeshes.Bar, new Vector2(0.09f, -0.11f), new Vector2(0.16f, 0.05f));
                    break;
                default:
                    AddDecor(ShapeMeshes.Bar, new Vector2(-0.12f, -0.03f), new Vector2(0.14f, 0.04f));
                    AddDecor(ShapeMeshes.Bar, new Vector2(0.11f, 0.09f), new Vector2(0.12f, 0.04f));
                    break;
            }
        }

        void AddDecor(Mesh mesh, Vector2 position, Vector2 size)
        {
            decor.Add(CreatePart(
                $"Decor {decor.Count}",
                mesh,
                new Vector3(position.x, position.y, DecorDepth),
                new Vector3(size.x, size.y, 1f)));
        }

        void ApplyDeposits(TileData tile)
        {
            var visible = tile.State is TileState.Revealed or TileState.Depleted ? tile.Deposits.Count : 0;

            while (dots.Count < visible)
                dots.Add(CreatePart($"Deposit {dots.Count}", HexMeshBuilder.Shared, Vector3.zero, Vector3.one * dotScale));

            for (var i = 0; i < dots.Count; i++)
            {
                var shown = i < visible;
                dots[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;

                dots[i].transform.localPosition = DotPosition(i, visible);

                var deposit = tile.Deposits[i];
                var color = resources.Get(deposit.Type);
                if (deposit.IsExhausted)
                    color.a = depletedAlpha;
                SetColor(dots[i], color);
            }
        }

        /// <summary>Одна точка в центре, две в ряд, три треугольником.</summary>
        Vector3 DotPosition(int index, int count)
        {
            if (count == 1)
                return new Vector3(0f, 0f, DotDepth);

            if (count == 2)
                return new Vector3(index == 0 ? -dotOffset : dotOffset, 0f, DotDepth);

            switch (index)
            {
                case 0:
                    return new Vector3(0f, dotOffset * 1.15f, DotDepth);
                case 1:
                    return new Vector3(-dotOffset, -dotOffset * 0.66f, DotDepth);
                default:
                    return new Vector3(dotOffset, -dotOffset * 0.66f, DotDepth);
            }
        }

        MeshRenderer CreatePart(string partName, Mesh mesh, Vector3 localPosition, Vector3 localScale)
        {
            var part = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(transform, false);
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
    }
}
