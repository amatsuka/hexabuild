using System.Collections.Generic;
using Game.Grid;
using UnityEngine;

namespace Game.Roads
{
    /// <summary>
    /// Мост на речной плитке: деревянный настил на сваях или каменная арка.
    ///
    /// До шага 4 стадии M20 дорога переходила реку насыпью — та же земляная лента шла прямо
    /// по руслу. Мост эту насыпь на речной плитке заменяет, но не целиком: у каждого шва
    /// остаётся кусок насыпи длиной <see cref="AbutmentReach"/> — устой. Он не украшение,
    /// а стык: соседняя плитка доводит своё полотно ровно до середины общей грани и его торец
    /// нужно чем-то встретить, иначе под настилом видно, что насыпь полая.
    ///
    /// Между устоями идёт пролёт. Его середина поднята на <see cref="Camber"/> над самым
    /// высоким швом, и вместе с речной плиткой, утопленной на `RiverSink`, это и даёт арку:
    /// специально гнуть дугу не понадобилось, просвет под настилом набирается сам.
    ///
    /// Дерево и камень — два разных меша и два цвета: настил уходит в свой, арка ложится
    /// к обочным камням. Форму они делят: полотно, площадка на развилке, пролёты по лучам, —
    /// а различаются тем, чем это полотно держится. Дерево — балки, сваи в русло и перила;
    /// камень — свод с полукруглым подрезом снизу и парапет.
    /// </summary>
    public static class BridgeMeshBuilder
    {
        /// <summary>Длина устоя: кусок насыпи от шва внутрь плитки, на который ложится пролёт.</summary>
        public const float AbutmentReach = 0.10f;

        /// <summary>Расстояние от центра плитки до внутреннего торца устоя: там начинается пролёт.</summary>
        public const float DeckReach = 0.5f - AbutmentReach;

        /// <summary>
        /// Подъём середины настила над самым высоким швом. Речная плитка уже сидит ниже соседей,
        /// поэтому дуга набирается и без него; горб нужен, чтобы мост читался мостом сверху,
        /// а не плоской заплаткой на дороге.
        /// </summary>
        public const float Camber = 0.03f;

        /// <summary>Насколько настил шире полотна: доски и плиты свисают за кромку насыпи.</summary>
        public const float Overhang = 0.01f;

        /// <summary>Толщина досок и глубина продольных балок под ними.</summary>
        const float PlankThickness = 0.022f;

        const float BeamDepth = 0.04f;

        /// <summary>Шаг досок поперёк хода, зазор между ними и полуширина балки.</summary>
        const float PlankStep = 0.055f;

        const float PlankGap = 0.008f;

        const float BeamHalf = 0.016f;

        /// <summary>Отступ балки и сваи от оси: балок две, и сваи стоят ровно под ними.</summary>
        const float BeamOffset = 0.072f;

        /// <summary>Полуширина сваи и насколько она уходит в русло ниже дна.</summary>
        const float PileHalf = 0.018f;

        const float PileSink = 0.03f;

        /// <summary>Высота перил над настилом, сечение поручня и столбика, шаг столбиков.</summary>
        const float RailHeight = 0.07f;

        const float RailBarDepth = 0.016f;

        const float RailBarHalf = 0.011f;

        const float RailOverhang = 0.006f;

        const float PostStep = 0.13f;

        const float PostHalf = 0.014f;

        /// <summary>Толщина свода в замке: от настила вниз до подреза арки.</summary>
        const float VaultThickness = 0.055f;

        /// <summary>Насколько пята арки уходит ниже русла.</summary>
        const float SpringDrop = 0.03f;

        /// <summary>Отрезков в подрезе арки: на восьми дуга уже гладкая.</summary>
        const int ArchSegments = 8;

        /// <summary>Высота парапета над настилом и его полуширина.</summary>
        const float ParapetHeight = 0.05f;

        const float ParapetHalf = 0.012f;

        const int PlankSalt = 53;

        static readonly List<Vector3> ring = new(12);

        static List<Vector3> points;
        static List<int> faces;

        /// <summary>
        /// Достроить мост одной плитки в переданные буферы. <paramref name="bed"/> — высота
        /// крышки речной плитки, то есть дно под мостом; <paramref name="crown"/> — высота
        /// настила в середине; <paramref name="radius"/> — радиус площадки на развилке, тот же,
        /// каким узел разводит лучи насыпи; <paramref name="half"/> — полуширина полотна дороги.
        /// </summary>
        public static void Append(
            List<Vector3> target, List<int> targetFaces, BridgeKind kind,
            Vector2 center, float bed, float crown, float radius, float half, List<Span> spans)
        {
            points = target;
            faces = targetFaces;

            var stone = kind == BridgeKind.Stone;
            var deckHalf = half + Overhang;

            AppendPlatform(center, crown, radius, deckHalf, stone ? VaultThickness : PlankThickness + BeamDepth, spans);

            foreach (var span in spans)
            {
                if (stone)
                    AppendArch(center, bed, crown, radius, deckHalf, half, span, spans.Count == 1);
                else
                    AppendTimberSpan(center, bed, crown, radius, deckHalf, half, span);
            }
        }

        /// <summary>
        /// Площадка на развилке: та же роль, что у пятачка насыпи. Без неё пролёты, сходящиеся
        /// в центре плитки, легли бы друг на друга. У сквозного проезда радиус нулевой, площадки
        /// нет вовсе, и пролёты встречаются торцами ровно посередине.
        /// </summary>
        static void AppendPlatform(
            Vector2 center, float crown, float radius, float deckHalf, float thickness, List<Span> spans)
        {
            if (radius < 1e-4f)
                return;

            ring.Clear();
            foreach (var span in spans)
            {
                var side = new Vector2(-span.Forward.y, span.Forward.x);
                var foot = center + span.Forward * radius;

                // Кромки пролёта: сначала та, что по часовой, потом та, что против.
                ring.Add(new Vector3(foot.x - side.x * deckHalf, crown, foot.y - side.y * deckHalf));
                ring.Add(new Vector3(foot.x + side.x * deckHalf, crown, foot.y + side.y * deckHalf));
            }

            var floor = crown - thickness;

            // Верх — веер из центра, обход как у крышки гекса.
            var hub = points.Count;
            points.Add(new Vector3(center.x, crown, center.y));
            var first = points.Count;
            foreach (var point in ring)
                points.Add(point);

            for (var i = 0; i < ring.Count; i++)
            {
                faces.Add(hub);
                faces.Add(first + (i + 1) % ring.Count);
                faces.Add(first + i);
            }

            // Низ — тот же веер обратным обходом.
            var lowHub = points.Count;
            points.Add(new Vector3(center.x, floor, center.y));
            var lowFirst = points.Count;
            foreach (var point in ring)
                points.Add(new Vector3(point.x, floor, point.z));

            for (var i = 0; i < ring.Count; i++)
            {
                faces.Add(lowHub);
                faces.Add(lowFirst + i);
                faces.Add(lowFirst + (i + 1) % ring.Count);
            }

            // Бока закрываются все, а не только промежутки между пролётами: у деревянного моста
            // пролёт — это балки и доски, сплошного тела у него нет, и открытая сторона площадки
            // светила бы дырой. Каменный пролёт своим торцом эту грань закрывает сам.
            for (var i = 0; i < ring.Count; i++)
            {
                var next = (i + 1) % ring.Count;
                Quad(
                    ring[i],
                    new Vector3(ring[i].x, floor, ring[i].z),
                    new Vector3(ring[next].x, floor, ring[next].z),
                    ring[next]);
            }
        }

        /// <summary>
        /// Деревянный пролёт: две продольные балки, поперечные доски поверх них, пара свай
        /// в русло на середине и перила по обеим кромкам.
        ///
        /// Доски кладутся по шагу, но каждая берёт свою длину, сдвиг поперёк и подъём от хеша
        /// вдоль луча — тем же способом, что кладка насыпи. Разброс поэтому едет вдоль моста
        /// и не перекладывается при каждой новой дороге; короткая доска оставляет между собой
        /// и соседкой щель, сквозь которую видно балку, и настил читается сколоченным.
        /// </summary>
        static void AppendTimberSpan(
            Vector2 center, float bed, float crown, float radius, float deckHalf, float half, in Span span)
        {
            var forward = new Vector3(span.Forward.x, 0f, span.Forward.y);
            var side = new Vector3(-span.Forward.y, 0f, span.Forward.x);

            var near = Axis(center, span, radius, crown);
            var far = Axis(center, span, DeckReach, span.SeamY);
            var length = DeckReach - radius;

            var beamTop = Vector3.down * PlankThickness;
            for (var lane = -1; lane <= 1; lane += 2)
            {
                var offset = side * (BeamOffset * lane);
                Prism(near + offset + beamTop, far + offset + beamTop, side, BeamHalf, BeamDepth);
            }

            var planks = Mathf.Max(1, Mathf.RoundToInt(length / PlankStep));
            var stride = length / planks;
            for (var i = 0; i < planks; i++)
            {
                var axis = Vector3.Lerp(near, far, (i + 0.5f) / planks);
                var reach = forward * ((stride - PlankGap) * 0.5f * Mathf.Lerp(0.72f, 1f, Hash(span, i, 0)));
                var shift = side * ((Hash(span, i, 1) - 0.5f) * 0.012f);
                var lift = Vector3.up * (Hash(span, i, 2) * 0.004f);

                Prism(axis - reach + shift + lift, axis + reach + shift + lift, side, deckHalf, PlankThickness + 0.004f);
            }

            // Сваи стоят под балками: настил на них и держится.
            var pile = Vector3.Lerp(near, far, 0.5f) + Vector3.down * (PlankThickness + BeamDepth - 0.01f);
            for (var lane = -1; lane <= 1; lane += 2)
            {
                var seat = pile + side * (BeamOffset * lane);
                Prism(seat - forward * PileHalf, seat + forward * PileHalf, side, PileHalf, seat.y - (bed - PileSink));
            }

            for (var lane = -1; lane <= 1; lane += 2)
            {
                var rail = side * (lane * (half + RailOverhang)) + Vector3.up * RailHeight;
                Prism(near + rail, far + rail, side, RailBarHalf, RailBarDepth);

                // Столбики отсчитываются от устоя внутрь: у середины моста их ставить нельзя,
                // там встречный пролёт поставил бы свой в то же место.
                var posts = Mathf.Max(1, Mathf.FloorToInt(length / PostStep));
                for (var i = 0; i < posts; i++)
                {
                    var axis = Vector3.Lerp(near, far, 1f - (i + 0.5f) / posts) + rail;
                    Prism(axis - forward * PostHalf, axis + forward * PostHalf, side, PostHalf, RailHeight);
                }
            }
        }

        /// <summary>
        /// Каменный пролёт: сплошное тело, у которого верх — проезжая часть, а низ уходит
        /// полукругом от замка к пяте. Дуга и есть арка: у замка подрез плоский, у пяты почти
        /// отвесный, и на плитке со сквозным проездом два пролёта складываются в целый свод.
        /// Поверх кромок идёт парапет — каменные перила.
        /// </summary>
        static void AppendArch(
            Vector2 center, float bed, float crown, float radius, float deckHalf, float half,
            in Span span, bool capCrown)
        {
            var forward = new Vector3(span.Forward.x, 0f, span.Forward.y);
            var side = new Vector3(-span.Forward.y, 0f, span.Forward.x);
            var across = side * deckHalf;

            var vault = crown - VaultThickness;
            var spring = Mathf.Min(bed - SpringDrop, vault - 0.02f);

            Vector3[] previous = null;
            for (var i = 0; i <= ArchSegments; i++)
            {
                var u = i / (float)ArchSegments;
                var axis = Axis(center, span, Mathf.Lerp(radius, DeckReach, u), Mathf.Lerp(crown, span.SeamY, u));
                var floor = vault - (vault - spring) * (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)));

                var lowLeft = new Vector3(axis.x + across.x, floor, axis.z + across.z);
                var lowRight = new Vector3(axis.x - across.x, floor, axis.z - across.z);
                var section = new[] { axis + across, axis - across, lowRight, lowLeft };

                if (previous != null)
                    Connect(previous, section);
                else if (capCrown)
                    Cap(section, false);

                previous = section;
            }

            // Торец у пяты закрыт всегда: он прячется в теле устоя, но тело обязано быть замкнутым.
            Cap(previous, true);

            var near = Axis(center, span, radius, crown);
            var far = Axis(center, span, DeckReach, span.SeamY);
            for (var lane = -1; lane <= 1; lane += 2)
            {
                var parapet = side * (lane * half) + Vector3.up * ParapetHeight;
                Prism(near + parapet, far + parapet, side, ParapetHalf, ParapetHeight + 0.015f);
            }
        }

        /// <summary>Точка оси пролёта на заданном расстоянии от центра плитки и заданной высоте.</summary>
        static Vector3 Axis(Vector2 center, in Span span, float distance, float top) =>
            new(center.x + span.Forward.x * distance, top, center.y + span.Forward.y * distance);

        /// <summary>
        /// Брус: коробка, у которой верхняя грань идёт от <paramref name="from"/>
        /// к <paramref name="to"/> и раздаётся на <paramref name="halfAcross"/> в стороны,
        /// а низ опущен на <paramref name="depth"/>. Из него сделаны и балки, и доски,
        /// и сваи, и перила: отличаются они только тем, вдоль чего вытянуты.
        /// </summary>
        static void Prism(Vector3 from, Vector3 to, Vector3 side, float halfAcross, float depth)
        {
            var across = side * halfAcross;
            var drop = Vector3.down * depth;

            var a = from + across;
            var b = to + across;
            var c = to - across;
            var d = from - across;

            Quad(a, b, c, d);
            Quad(d + drop, c + drop, b + drop, a + drop);
            Quad(a, a + drop, b + drop, b);
            Quad(c, c + drop, d + drop, d);
            Quad(b, b + drop, c + drop, c);
            Quad(d, d + drop, a + drop, a);
        }

        /// <summary>Кусок каменного тела между двумя сечениями: верх, подрез и две щеки.</summary>
        static void Connect(Vector3[] near, Vector3[] far)
        {
            Quad(near[0], far[0], far[1], near[1]);
            Quad(near[3], near[2], far[2], far[3]);
            Quad(near[0], near[3], far[3], far[0]);
            Quad(near[1], far[1], far[2], near[2]);
        }

        /// <summary>Торец каменного тела. <paramref name="outward"/> — лицом от центра плитки.</summary>
        static void Cap(Vector3[] section, bool outward)
        {
            var first = points.Count;
            foreach (var point in section)
                points.Add(point);

            for (var i = 1; i < section.Length - 1; i++)
            {
                faces.Add(first);
                faces.Add(first + (outward ? i + 1 : i));
                faces.Add(first + (outward ? i : i + 1));
            }
        }

        static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var first = points.Count;
            points.Add(a);
            points.Add(b);
            points.Add(c);
            points.Add(d);

            faces.Add(first);
            faces.Add(first + 1);
            faces.Add(first + 2);
            faces.Add(first);
            faces.Add(first + 2);
            faces.Add(first + 3);
        }

        /// <summary>Разброс досок: тот же хеш вдоль луча, что и у кладки насыпи.</summary>
        static float Hash(in Span span, int index, int salt) =>
            RoadMeshBuilder.Hash01(span.Coord, span.Direction, index, PlankSalt + salt);

        /// <summary>Пролёт моста: от середины плитки к одному из её швов.</summary>
        public readonly struct Span
        {
            public Span(HexCoord coord, int direction, Vector2 forward, float seamY)
            {
                Coord = coord;
                Direction = direction;
                Forward = forward;
                SeamY = seamY;
            }

            /// <summary>Плитка и направление луча: по ним хеш раздаёт доскам их разброс.</summary>
            public HexCoord Coord { get; }

            public int Direction { get; }

            /// <summary>Направление пролёта по земле, единичное.</summary>
            public Vector2 Forward { get; }

            /// <summary>Высота полотна на шве: её задаёт старшая из двух плиток.</summary>
            public float SeamY { get; }
        }
    }
}
