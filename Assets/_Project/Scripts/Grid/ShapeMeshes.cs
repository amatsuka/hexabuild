using System.Collections.Generic;
using Game.Economy;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Меши декора плиток и моделек месторождений: всё строится кодом, ассетов в проекте нет.
    /// Моделька месторождения — два меша: тело в цвете ресурса и огранка другим цветом (ствол,
    /// блик на валуне, светлая грань кристалла). Меши общие на всё поле и живут в статике.
    /// </summary>
    public static class ShapeMeshes
    {
        /// <summary>Толщина ободка гекса в долях его радиуса.</summary>
        const float RingWidth = 0.12f;

        static Mesh triangle;
        static Mesh bar;
        static Mesh hexRing;

        static readonly Dictionary<(ResourceType Type, bool Exhausted, bool Accent), Mesh> deposits = new();
        static readonly Dictionary<DecorShape, Mesh> decor = new();

        /// <summary>Равнобедренный треугольник высотой 1 и шириной 1.</summary>
        public static Mesh Triangle => triangle != null
            ? triangle
            : triangle = new FlatMesh().Triangle(Vector2.zero, 1f, 1f).Bake("Triangle");

        /// <summary>Квадрат со стороной 1.</summary>
        public static Mesh Bar => bar != null
            ? bar
            : bar = new FlatMesh().Quad(Vector2.zero, 1f, 1f).Bake("Bar");

        /// <summary>
        /// Ободок по границе гекса. Увеличенный гекс позади плитки не годится: соседние плитки
        /// вплотную закрывают его, и виден только край поля.
        /// </summary>
        public static Mesh HexRing => hexRing != null ? hexRing : hexRing = BuildHexRing();

        /// <summary>
        /// Моделька месторождения в квадрате 1×1: дерево, кучка валунов или кристаллы.
        /// Истощённая отличается формой, а не только прозрачностью: пенёк, осевший валун,
        /// обломанные кристаллы. <paramref name="accent"/> — вторая, иначе окрашенная часть.
        /// </summary>
        public static Mesh Deposit(ResourceType type, bool exhausted, bool accent)
        {
            var key = (type, exhausted, accent);

            // Проверка на null, а не только на наличие ключа: выход из Play Mode уничтожает меши,
            // а статический кэш переживает это и остался бы со ссылками на мёртвые объекты.
            if (deposits.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var mesh = BuildDeposit(type, exhausted, accent);
            deposits[key] = mesh;
            return mesh;
        }

        /// <summary>Один элемент декора биома в квадрате 1×1.</summary>
        public static Mesh Decor(DecorShape shape)
        {
            if (decor.TryGetValue(shape, out var cached) && cached != null)
                return cached;

            var mesh = BuildDecor(shape);
            decor[shape] = mesh;
            return mesh;
        }

        static Mesh BuildDeposit(ResourceType type, bool exhausted, bool accent)
        {
            var name = $"{type}{(exhausted ? "Spent" : string.Empty)}{(accent ? "Accent" : "Body")}";

            switch (type)
            {
                case ResourceType.Wood:
                    return (exhausted ? SpentTree(accent) : Tree(accent)).Bake(name);
                case ResourceType.Ore:
                    return (exhausted ? SpentOre(accent) : Ore(accent)).Bake(name);
                default:
                    return (exhausted ? SpentStone(accent) : Stone(accent)).Bake(name);
            }
        }

        /// <summary>Ель: две кроны хвои и ствол. Ствол и есть огранка — он другого цвета.</summary>
        static FlatMesh Tree(bool accent) =>
            accent
                ? new FlatMesh().Quad(new Vector2(0f, -0.34f), 0.15f, 0.32f)
                : new FlatMesh()
                    .Triangle(new Vector2(0f, 0.19f), 0.50f, 0.44f)
                    .Triangle(new Vector2(0f, -0.06f), 0.70f, 0.48f);

        /// <summary>Пенёк: от ели остался ствол, огранка — светлый срез на нём.</summary>
        static FlatMesh SpentTree(bool accent) =>
            accent
                ? new FlatMesh().Ellipse(new Vector2(0f, -0.14f), 0.17f, 0.06f, 8)
                : new FlatMesh().Quad(new Vector2(0f, -0.28f), 0.34f, 0.34f);

        /// <summary>Кучка валунов: два мелких и один крупный сверху, блик на крупном.</summary>
        static FlatMesh Stone(bool accent) =>
            accent
                ? new FlatMesh().Ellipse(new Vector2(-0.05f, 0.10f), 0.13f, 0.08f, 6)
                : new FlatMesh()
                    .Ellipse(new Vector2(-0.19f, -0.24f), 0.19f, 0.15f, 6)
                    .Ellipse(new Vector2(0.19f, -0.26f), 0.16f, 0.13f, 6)
                    .Ellipse(new Vector2(0.01f, 0.01f), 0.27f, 0.23f, 6);

        /// <summary>Осевший валун: от кучки остался один плоский камень.</summary>
        static FlatMesh SpentStone(bool accent) =>
            accent
                ? new FlatMesh().Ellipse(new Vector2(-0.08f, -0.19f), 0.12f, 0.05f, 6)
                : new FlatMesh().Ellipse(new Vector2(0f, -0.26f), 0.31f, 0.14f, 6);

        /// <summary>Кристаллы: три штуки разной высоты, у каждого светлая грань слева.</summary>
        static FlatMesh Ore(bool accent) =>
            accent
                ? new FlatMesh()
                    .Crystal(new Vector2(-0.245f, -0.10f), 0.08f, 0.42f, 0.11f)
                    .Crystal(new Vector2(0.115f, -0.17f), 0.07f, 0.32f, 0.09f)
                    .Crystal(new Vector2(-0.075f, 0.03f), 0.09f, 0.58f, 0.15f)
                : new FlatMesh()
                    .Crystal(new Vector2(-0.21f, -0.10f), 0.21f, 0.42f, 0.11f)
                    .Crystal(new Vector2(0.15f, -0.17f), 0.19f, 0.32f, 0.09f)
                    .Crystal(new Vector2(-0.02f, 0.03f), 0.25f, 0.58f, 0.15f);

        /// <summary>Обломанные кристаллы: остриё сбито, срез косой.</summary>
        static FlatMesh SpentOre(bool accent) =>
            accent
                ? new FlatMesh().Broken(new Vector2(-0.145f, -0.30f), 0.07f, 0.24f)
                : new FlatMesh()
                    .Broken(new Vector2(-0.17f, -0.30f), 0.20f, 0.24f)
                    .Broken(new Vector2(0.13f, -0.33f), 0.17f, 0.18f);

        static Mesh BuildDecor(DecorShape shape)
        {
            switch (shape)
            {
                case DecorShape.Conifer:
                    return new FlatMesh()
                        .Quad(new Vector2(0f, -0.38f), 0.11f, 0.24f)
                        .Triangle(new Vector2(0f, 0.20f), 0.48f, 0.44f)
                        .Triangle(new Vector2(0f, -0.06f), 0.66f, 0.46f)
                        .Bake("Conifer");

                case DecorShape.Broadleaf:
                    return new FlatMesh()
                        .Quad(new Vector2(0f, -0.32f), 0.10f, 0.36f)
                        .Ellipse(new Vector2(0f, 0.13f), 0.35f, 0.33f, 9)
                        .Bake("Broadleaf");

                // Слоистый пик: три яруса с зазорами, зазоры и читаются как слои породы.
                case DecorShape.LayeredPeak:
                    return new FlatMesh()
                        .Polygon(new Vector2(-0.50f, -0.50f), new Vector2(-0.31f, -0.17f),
                            new Vector2(0.31f, -0.17f), new Vector2(0.50f, -0.50f))
                        .Polygon(new Vector2(-0.30f, -0.11f), new Vector2(-0.17f, 0.15f),
                            new Vector2(0.17f, 0.15f), new Vector2(0.30f, -0.11f))
                        .Triangle(new Vector2(0f, 0.35f), 0.34f, 0.30f)
                        .Bake("LayeredPeak");

                // Гряда: три острых пика вразнобой. Выше и жёстче слоистого пика скал —
                // гора должна читаться стеной, а не крупным камнем.
                case DecorShape.Ridge:
                    return new FlatMesh()
                        .Triangle(new Vector2(-0.26f, -0.10f), 0.44f, 0.62f)
                        .Triangle(new Vector2(0.24f, -0.16f), 0.40f, 0.52f)
                        .Triangle(new Vector2(-0.01f, 0.02f), 0.50f, 0.92f)
                        .Bake("Ridge");

                case DecorShape.Ripple:
                    return new FlatMesh()
                        .Quad(new Vector2(-0.09f, 0.13f), 0.52f, 0.07f)
                        .Quad(new Vector2(0.11f, -0.11f), 0.36f, 0.07f)
                        .Bake("Ripple");

                // Кувшинка: круг с узким вырезом. Широкий читался пакманом, а не листом.
                case DecorShape.Lily:
                    return new FlatMesh().Fan(Vector2.zero, 0.40f, 0.36f, 12, 11f, 349f).Bake("Lily");

                case DecorShape.Tussock:
                    return new FlatMesh()
                        .Blade(-0.17f, -0.13f, 0.09f, 0.72f)
                        .Blade(0.02f, 0.06f, 0.10f, 0.92f)
                        .Blade(0.18f, 0.15f, 0.08f, 0.64f)
                        .Bake("Tussock");

                default:
                    return new FlatMesh().Fan(new Vector2(0f, -0.24f), 0.50f, 0.30f, 10, 0f, 180f).Bake("Dune");
            }
        }

        static Mesh BuildHexRing()
        {
            var vertices = new Vector3[12];
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (60f * i - 30f);
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices[i] = direction * HexCoord.Size;
                vertices[i + 6] = direction * (HexCoord.Size * (1f + RingWidth));
            }

            // Обход по часовой стрелке, как у остальных мешей: внутренний угол, следующий
            // внутренний, следующий внешний, внешний.
            var triangles = new int[36];
            for (var i = 0; i < 6; i++)
            {
                var next = (i + 1) % 6;
                triangles[i * 6] = i;
                triangles[i * 6 + 1] = next;
                triangles[i * 6 + 2] = next + 6;
                triangles[i * 6 + 3] = i;
                triangles[i * 6 + 4] = next + 6;
                triangles[i * 6 + 5] = i + 6;
            }

            var mesh = new Mesh { name = "HexRing" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
