using System;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>Цвета ресурсов. Живёт на префабах как `[SerializeField]`, не в `GameConfig`.</summary>
    [Serializable]
    public sealed class ResourcePalette
    {
        [SerializeField] Color wood = new(0.22f, 0.60f, 0.25f);
        [SerializeField] Color stone = new(0.45f, 0.45f, 0.47f);
        [SerializeField] Color ore = new(0.76f, 0.55f, 0.16f);
        [SerializeField] Color board = new(0.55f, 0.82f, 0.50f);
        [SerializeField] Color gravel = new(0.78f, 0.78f, 0.80f);
        [SerializeField] Color ingot = new(0.95f, 0.72f, 0.42f);

        public Color Get(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood:
                    return wood;
                case ResourceType.Ore:
                    return ore;
                case ResourceType.Board:
                    return board;
                case ResourceType.Gravel:
                    return gravel;
                case ResourceType.Ingot:
                    return ingot;
                default:
                    return stone;
            }
        }
    }
}
