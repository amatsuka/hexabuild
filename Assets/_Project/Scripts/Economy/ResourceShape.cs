using System.Collections.Generic;
using Game.Grid;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>
    /// Иконка ресурса: бревно, валун, кристалл, доска, щебень, слиток. Полигон один и тот же
    /// и на поле, и на складе — иначе едущий по дороге ресурс и лежащий в клетке выглядели бы
    /// разными предметами. Фигуры лежат в квадрате 1×1 с центром в нуле.
    /// </summary>
    public static class ResourceShape
    {
        static readonly Dictionary<ResourceType, FlatMesh> outlines = new();
        static readonly Dictionary<ResourceType, Mesh> icons = new();

        /// <summary>Сырой полигон: его разбирает `ResourceIcon` в канвасе.</summary>
        public static FlatMesh Outline(ResourceType type)
        {
            if (outlines.TryGetValue(type, out var cached))
                return cached;

            var outline = Build(type);
            outlines[type] = outline;
            return outline;
        }

        /// <summary>Тот же полигон мешем: его везёт `ResourceMover` по дороге.</summary>
        public static Mesh Icon(ResourceType type)
        {
            // Проверка на null, а не только на наличие ключа: выход из Play Mode уничтожает меши,
            // а статический кэш переживает это и остался бы со ссылками на мёртвые объекты.
            if (icons.TryGetValue(type, out var cached) && cached != null)
                return cached;

            var mesh = Outline(type).Bake($"{type}Icon");
            icons[type] = mesh;
            return mesh;
        }

        static FlatMesh Build(ResourceType type)
        {
            switch (type)
            {
                // Бревно: лежит поперёк, торцы срезаны — восьмиугольник вытянут по горизонтали.
                case ResourceType.Wood:
                    return new FlatMesh().Polygon(
                        new Vector2(-0.44f, -0.13f), new Vector2(-0.44f, 0.13f),
                        new Vector2(-0.34f, 0.23f), new Vector2(0.34f, 0.23f),
                        new Vector2(0.44f, 0.13f), new Vector2(0.44f, -0.13f),
                        new Vector2(0.34f, -0.23f), new Vector2(-0.34f, -0.23f));

                // Валун: неправильная глыба, ни одна сторона не повторяет другую.
                case ResourceType.Stone:
                    return new FlatMesh().Polygon(
                        new Vector2(-0.35f, -0.09f), new Vector2(-0.27f, 0.19f),
                        new Vector2(-0.04f, 0.33f), new Vector2(0.23f, 0.27f),
                        new Vector2(0.37f, 0.02f), new Vector2(0.30f, -0.23f),
                        new Vector2(-0.15f, -0.31f));

                // Кристалл: остриё вверх, огранённый низ.
                case ResourceType.Ore:
                    return new FlatMesh().Polygon(
                        new Vector2(-0.21f, -0.09f), new Vector2(-0.21f, 0.15f),
                        new Vector2(0f, 0.42f), new Vector2(0.21f, 0.15f),
                        new Vector2(0.21f, -0.09f), new Vector2(0f, -0.38f));

                // Доска: тонкая ровная планка, чуть расширяющаяся, — не спутать с бревном.
                case ResourceType.Board:
                    return new FlatMesh().Polygon(
                        new Vector2(-0.45f, -0.10f), new Vector2(-0.45f, 0.10f),
                        new Vector2(0.45f, 0.14f), new Vector2(0.45f, -0.14f));

                // Щебень: три осколка врозь — сыпучий, а не цельный.
                case ResourceType.Gravel:
                    return new FlatMesh()
                        .Polygon(new Vector2(-0.34f, -0.20f), new Vector2(-0.38f, 0.05f),
                            new Vector2(-0.12f, 0.15f), new Vector2(-0.06f, -0.11f))
                        .Polygon(new Vector2(0.01f, 0.05f), new Vector2(0.08f, 0.31f),
                            new Vector2(0.31f, 0.24f), new Vector2(0.26f, 0.02f))
                        .Polygon(new Vector2(0.04f, -0.33f), new Vector2(0f, -0.08f),
                            new Vector2(0.27f, -0.03f), new Vector2(0.36f, -0.24f));

                // Слиток: трапеция с приподнятой площадкой — отливка в форме.
                default:
                    return new FlatMesh()
                        .Polygon(new Vector2(-0.42f, -0.22f), new Vector2(-0.30f, 0.06f),
                            new Vector2(0.30f, 0.06f), new Vector2(0.42f, -0.22f))
                        .Polygon(new Vector2(-0.28f, 0.11f), new Vector2(-0.21f, 0.28f),
                            new Vector2(0.21f, 0.28f), new Vector2(0.28f, 0.11f));
            }
        }
    }
}
