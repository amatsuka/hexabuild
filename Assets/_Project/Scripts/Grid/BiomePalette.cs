using System;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Цвета ландшафта. Живёт на префабе плитки как `[SerializeField]`, не в `GameConfig`.</summary>
    [Serializable]
    public sealed class BiomePalette
    {
        [SerializeField] Color sand = new(0.85f, 0.78f, 0.55f);
        [SerializeField] Color meadow = new(0.55f, 0.70f, 0.40f);
        [SerializeField] Color forest = new(0.35f, 0.56f, 0.34f);
        [SerializeField] Color rocks = new(0.55f, 0.54f, 0.52f);
        [Tooltip("Горы непроходимы: цвет должен читаться как стена, а не как ещё одна скала")]
        [SerializeField] Color mountains = new(0.33f, 0.32f, 0.36f);

        [Header("Декор")]
        [SerializeField] Color sandDecor = new(0.72f, 0.63f, 0.40f);
        [SerializeField] Color meadowDecor = new(0.47f, 0.62f, 0.33f);
        [SerializeField] Color forestDecor = new(0.18f, 0.34f, 0.20f);
        [SerializeField] Color rocksDecor = new(0.38f, 0.37f, 0.36f);
        [SerializeField] Color mountainsDecor = new(0.74f, 0.74f, 0.79f);

        public Color Ground(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Sand:
                    return sand;
                case BiomeType.Forest:
                    return forest;
                case BiomeType.Rocks:
                    return rocks;
                case BiomeType.Mountains:
                    return mountains;
                default:
                    return meadow;
            }
        }

        public Color Decor(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Sand:
                    return sandDecor;
                case BiomeType.Forest:
                    return forestDecor;
                case BiomeType.Rocks:
                    return rocksDecor;
                case BiomeType.Mountains:
                    return mountainsDecor;
                default:
                    return meadowDecor;
            }
        }
    }
}
