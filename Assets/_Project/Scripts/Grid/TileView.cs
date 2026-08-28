using System.Collections.Generic;
using Game.Economy;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Визуал плитки: цвет гекса по состоянию и точки месторождений.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Цвета плитки")]
        [SerializeField] Color hiddenColor = new(0.18f, 0.18f, 0.20f);
        [SerializeField] Color availableColor = new(0.55f, 0.55f, 0.58f);
        [SerializeField] Color revealedColor = new(0.87f, 0.80f, 0.63f);
        [SerializeField] Color metropolisColor = new(0.20f, 0.45f, 0.85f);
        [SerializeField, Range(0f, 1f)] float depletedAlpha = 0.35f;

        [Header("Цвета месторождений")]
        [SerializeField] Color woodColor = new(0.22f, 0.60f, 0.25f);
        [SerializeField] Color stoneColor = new(0.45f, 0.45f, 0.47f);
        [SerializeField] Color oreColor = new(0.76f, 0.55f, 0.16f);

        [Header("Точки месторождений")]
        [SerializeField] float dotScale = 0.22f;
        [SerializeField] float dotOffset = 0.2f;

        readonly List<MeshRenderer> dots = new();

        MeshRenderer meshRenderer;
        MaterialPropertyBlock propertyBlock;

        public HexCoord Coord { get; private set; }

        public void Bind(TileData tile)
        {
            Coord = tile.Coord;
            name = $"Hex {tile.Coord}";
            transform.localPosition = tile.Coord.ToWorld();
            GetComponent<MeshFilter>().sharedMesh = HexMeshBuilder.Shared;
            Apply(tile);
        }

        /// <summary>Перерисовать плитку под её текущее состояние.</summary>
        public void Apply(TileData tile)
        {
            SetColor(Renderer, TileColor(tile));
            ApplyDeposits(tile);
        }

        MeshRenderer Renderer => meshRenderer != null ? meshRenderer : meshRenderer = GetComponent<MeshRenderer>();

        Color TileColor(TileData tile)
        {
            if (tile.IsMetropolis)
                return metropolisColor;

            switch (tile.State)
            {
                case TileState.Available:
                    return availableColor;
                case TileState.Revealed:
                    return revealedColor;
                case TileState.Depleted:
                    var faded = revealedColor;
                    faded.a = depletedAlpha;
                    return faded;
                default:
                    return hiddenColor;
            }
        }

        Color DepositColor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood:
                    return woodColor;
                case ResourceType.Ore:
                    return oreColor;
                default:
                    return stoneColor;
            }
        }

        void ApplyDeposits(TileData tile)
        {
            var visible = tile.State is TileState.Revealed or TileState.Depleted ? tile.Deposits.Count : 0;

            while (dots.Count < visible)
                dots.Add(CreateDot(dots.Count));

            for (var i = 0; i < dots.Count; i++)
            {
                var shown = i < visible;
                dots[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;

                dots[i].transform.localPosition = DotPosition(i, visible);

                var deposit = tile.Deposits[i];
                var color = DepositColor(deposit.Type);
                if (deposit.IsExhausted)
                    color.a = depletedAlpha;
                SetColor(dots[i], color);
            }
        }

        MeshRenderer CreateDot(int index)
        {
            var dot = new GameObject($"Deposit {index}", typeof(MeshFilter), typeof(MeshRenderer));
            dot.transform.SetParent(transform, false);
            dot.transform.localScale = Vector3.one * dotScale;
            dot.GetComponent<MeshFilter>().sharedMesh = HexMeshBuilder.Shared;

            var dotRenderer = dot.GetComponent<MeshRenderer>();
            dotRenderer.sharedMaterial = Renderer.sharedMaterial;
            dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dotRenderer.receiveShadows = false;
            return dotRenderer;
        }

        /// <summary>Одна точка в центре, две в ряд, три треугольником.</summary>
        Vector3 DotPosition(int index, int count)
        {
            const float depth = -0.05f;

            if (count == 1)
                return new Vector3(0f, 0f, depth);

            if (count == 2)
                return new Vector3(index == 0 ? -dotOffset : dotOffset, 0f, depth);

            switch (index)
            {
                case 0:
                    return new Vector3(0f, dotOffset * 1.15f, depth);
                case 1:
                    return new Vector3(-dotOffset, -dotOffset * 0.66f, depth);
                default:
                    return new Vector3(dotOffset, -dotOffset * 0.66f, depth);
            }
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
