using System;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>
    /// Модели ресурсов: три добываемых — бревно, кирпич, слиток, и три крафтовых — доски,
    /// щебень, слитки. Незаполненное поле возвращает null, и такой ресурс рисуется процедурным
    /// полигоном, как раньше. Живёт на префабах `[SerializeField]`, рядом с `ResourcePalette`.
    /// </summary>
    [Serializable]
    public sealed class ResourceModels
    {
        [Tooltip("Материал с палитрой ресурсов. Пусто — ресурс рисуется полигоном, как было")]
        [SerializeField] Material material;
        [Header("Добываемые")]
        [SerializeField] Mesh wood;
        [SerializeField] Mesh stone;
        [SerializeField] Mesh ore;

        [Header("Крафтовые")]
        [SerializeField] Mesh board;
        [SerializeField] Mesh gravel;
        [SerializeField] Mesh ingot;

        public Material Material => material;

        /// <summary>Меш ресурса. Null — модели нет: поле пусто или материал не назначен.</summary>
        public Mesh Get(ResourceType type)
        {
            if (material == null)
                return null;

            switch (type)
            {
                case ResourceType.Wood:
                    return wood;
                case ResourceType.Stone:
                    return stone;
                case ResourceType.Ore:
                    return ore;
                case ResourceType.Board:
                    return board;
                case ResourceType.Gravel:
                    return gravel;
                default:
                    return ingot;
            }
        }

        /// <summary>
        /// Масштаб, приводящий наибольший габарит меша к заданному размеру. То же правило, что
        /// у моделей на плитке (`TileView.ModelScale`): модели пака сделаны под свой размер мира,
        /// и множитель тут не годится — у бревна и кирпича габариты разные вдвое.
        /// </summary>
        public static float ScaleFor(Mesh model, float target)
        {
            var size = model.bounds.size;
            var largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            return largest > 0.0001f ? target / largest : target;
        }
    }
}
