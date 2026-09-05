using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Единственный источник геометрии русла: точки ворот и опорная точка поворота. Раньше их
    /// считали независимо `RiverMeshBuilder` (меш) и `TileView.InsideRiver` (запрет декора) —
    /// после меандра M22 они разошлись бы, и дерево выросло бы посреди воды. Теперь оба зовут
    /// этот класс.
    /// </summary>
    public static class RiverCourse
    {
        /// <summary>Отрезков в дуге поворота: на восьми излом уже не читается ни на каком зуме.</summary>
        public const int CurveSegments = 8;

        /// <summary>
        /// Снос точки входа реки вдоль грани от её середины. Ломает прямой сквозной проток —
        /// без него вход всегда приходится точно в середину грани, и три плитки подряд с одной
        /// парой граней дают линейку.
        /// </summary>
        public const float GateDrift = 0.10f;

        /// <summary>
        /// Снос опорной точки дуги от центра плитки. Опорная точка — не сам центр, а точка рядом
        /// с ним: ровно она ломает прямой стык двух отрезков в дугу с изгибом не по центру.
        /// </summary>
        public const float BendDrift = 0.10f;

        /// <summary>
        /// Снос ворот клампится по худшему случаю ширины берега (устье), а не по фактической
        /// ширине в этих воротах: ширина на двух концах шва может законно разойтись на слиянии
        /// притока, и клампом по фактической ширине точка на одной стороне сдвинулась бы иначе,
        /// чем на другой, — шов бы разошёлся. Клампом по общему худшему случаю ворота остаются
        /// чистой функцией пары плиток, не зависящей от течения.
        /// </summary>
        static readonly float WorstBankHalfWidth = RiverWidth.Bank(RiverWidth.FlowFull) * 0.5f;

        const int GateSalt = 71;
        const int BendSalt = 73;

        /// <summary>
        /// Точка входа реки через грань в направлении <paramref name="direction"/>, в локальных
        /// координатах плитки. Считается от середины грани, а не от плитки: снос идёт вдоль
        /// грани и хэшируется от канонически упорядоченной пары координат, поэтому соседняя
        /// плитка, считающая те же ворота в обратном направлении, получает ту же мировую точку.
        /// </summary>
        public static Vector2 Gate(HexCoord tile, int direction)
        {
            var neighbor = tile.Neighbor(direction);
            var edgeMid = HexCoord.Directions[direction].ToPlane() * 0.5f;

            // Полудлина грани минус зазор до угла минус худший случай полуширины берега.
            var maxDrift = HexCoord.Size * 0.5f - WorstBankHalfWidth - 0.02f;
            var raw = (HexCoord.Hash01(tile, neighbor, GateSalt) * 2f - 1f) * GateDrift;
            var drift = Mathf.Clamp(raw, -maxDrift, maxDrift);

            return edgeMid + EdgeTangent(direction) * drift;
        }

        /// <summary>Опорная точка дуги вместо центра плитки — именно она ломает прямой проток.</summary>
        public static Vector2 Bend(HexCoord tile)
        {
            var angle = tile.Hash01(BendSalt) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BendDrift;
        }

        /// <summary>
        /// Точка на дуге между воротами <paramref name="fromDirection"/> и
        /// <paramref name="toDirection"/> плитки <paramref name="tile"/>, `t` от 0 до 1. Меш русла
        /// и проверка «декор внутри русла» обязаны ходить по одной и той же дуге, иначе они
        /// разойдутся при первом же меандре.
        /// </summary>
        public static Vector2 Point(HexCoord tile, int fromDirection, int toDirection, float t) =>
            Bezier(Gate(tile, fromDirection), Bend(tile), Gate(tile, toDirection), t);

        /// <summary>
        /// Квадратичная кривая Безье общего вида. Публична: борта ленты — свои две кривые через
        /// ворота, сдвинутые вдоль грани на полуширину (см. `EdgeTangent`), а не отступ от
        /// осевой линии по нормали дуги — той хватало на прямом протоке, а у ворот со сносом
        /// поперечная нормаль дуги смотрит не вдоль грани, и борт срезал бы угол на соседа.
        /// Оба конца бортовой кривой и опорная точка лежат внутри шестиугольника — а значит, по
        /// выпуклости, и вся кривая целиком, что и держит инвариант «лента не вылезает за грань».
        /// </summary>
        public static Vector2 Bezier(Vector2 from, Vector2 control, Vector2 to, float t)
        {
            var inverse = 1f - t;
            return inverse * inverse * from + 2f * inverse * t * control + t * t * to;
        }

        /// <summary>
        /// Направление вдоль грани, а не поперёк нормали. Направление и противоположное ему
        /// описывают одну и ту же физическую грань, поэтому берётся канонический представитель
        /// пары (0, 1 или 2) — иначе тангенс с двух сторон шва смотрел бы в разные стороны, и
        /// снос при переводе в мир не сходился бы в одну точку.
        /// </summary>
        public static Vector2 EdgeTangent(int direction)
        {
            var canonical = direction < 3 ? direction : direction - 3;
            var radial = HexCoord.Directions[canonical].ToPlane();
            return new Vector2(-radial.y, radial.x).normalized;
        }
    }
}
