using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Конфетти над финальным экраном: клочки бумаги падают, кувыркаются и уходят за нижнюю
    /// кромку, чтобы появиться сверху снова. Экран не закрывается сам, поэтому и праздник
    /// не кончается — иначе через пару секунд от него остался бы пустой тёмный фон.
    ///
    /// Всё это один рендерер: клочки рисуются общим мешем в `OnPopulateMesh`, как ореол иконки
    /// и карточка HUD. Отдельный объект на клочок дал бы полсотни рендереров ради мусора,
    /// который никто не разглядывает.
    /// </summary>
    public sealed class Confetti : MaskableGraphic
    {
        [SerializeField] int pieces = 64;
        [SerializeField] float minSize = 14f;
        [SerializeField] float maxSize = 30f;
        [Tooltip("Скорость падения, пиксели канваса в секунду")]
        [SerializeField] float minFallSpeed = 90f;
        [SerializeField] float maxFallSpeed = 230f;
        [Tooltip("Размах бокового покачивания, пиксели")]
        [SerializeField] float sway = 42f;
        [Tooltip("Кувырок вокруг своей оси, оборотов в секунду")]
        [SerializeField] float minSpin = 0.4f;
        [SerializeField] float maxSpin = 1.6f;

        /// <summary>Цвета референса: холодные и тёплые вперемешку, все насыщенные.</summary>
        [SerializeField] Color[] palette =
        {
            new(1f, 0.83f, 0.29f), new(1f, 0.53f, 0.24f), new(0.36f, 0.78f, 1f),
            new(0.55f, 0.93f, 0.55f), new(1f, 0.45f, 0.62f), new(0.72f, 0.58f, 1f)
        };

        Piece[] flakes;
        float height;

        void Update()
        {
            if (flakes == null)
                return;

            var delta = Time.deltaTime;
            for (var i = 0; i < flakes.Length; i++)
            {
                flakes[i].Y -= flakes[i].Fall * delta;
                flakes[i].Phase += flakes[i].Spin * delta;

                // Ушёл за нижнюю кромку — возвращается над верхней. Запас в размер клочка
                // прячет момент подмены: иначе он вспыхивает прямо на кромке.
                if (flakes[i].Y < -maxSize)
                    flakes[i].Y += height + maxSize * 2f;
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();

            var rect = GetPixelAdjustedRect();
            EnsurePieces(rect);

            foreach (var piece in flakes)
            {
                // Кувырок вокруг вертикальной оси: клочок сплющивается по ширине и снова
                // разворачивается. Полноценного поворота в плоскости экрана мало — бумага,
                // которая не показывает ребро, читается наклейкой.
                var turn = Mathf.Cos(piece.Phase * Mathf.PI * 2f);
                var half = new Vector2(piece.Size * 0.5f * turn, piece.Size * 0.35f);
                var centre = new Vector2(
                    rect.xMin + piece.X + Mathf.Sin(piece.Phase * Mathf.PI) * sway,
                    rect.yMin + piece.Y);

                // Ребро повёрнутого клочка темнее: то же удешевление объёма, что у фаски панели.
                var tint = piece.Tint * Mathf.Lerp(0.55f, 1f, Mathf.Abs(turn));
                tint.a = piece.Tint.a;

                var start = helper.currentVertCount;
                helper.AddVert(new Vector3(centre.x - half.x, centre.y - half.y), tint, Vector2.zero);
                helper.AddVert(new Vector3(centre.x - half.x, centre.y + half.y), tint, Vector2.zero);
                helper.AddVert(new Vector3(centre.x + half.x, centre.y + half.y), tint, Vector2.zero);
                helper.AddVert(new Vector3(centre.x + half.x, centre.y - half.y), tint, Vector2.zero);
                helper.AddTriangle(start, start + 1, start + 2);
                helper.AddTriangle(start + 2, start + 3, start);
            }
        }

        /// <summary>
        /// Раскладка клочков по экрану. Считается один раз: она зависит от размера канваса, а
        /// он на телефоне не меняется, и пересчёт каждый кадр только дёргал бы их с места.
        /// </summary>
        void EnsurePieces(Rect rect)
        {
            if (flakes != null && Mathf.Approximately(height, rect.height))
                return;

            height = rect.height;
            flakes = new Piece[Mathf.Max(pieces, 0)];

            var random = new System.Random(20260901);
            for (var i = 0; i < flakes.Length; i++)
                flakes[i] = new Piece
                {
                    X = (float)random.NextDouble() * rect.width,
                    Y = (float)random.NextDouble() * rect.height,
                    Size = Mathf.Lerp(minSize, maxSize, (float)random.NextDouble()),
                    Fall = Mathf.Lerp(minFallSpeed, maxFallSpeed, (float)random.NextDouble()),
                    Spin = Mathf.Lerp(minSpin, maxSpin, (float)random.NextDouble()),
                    Phase = (float)random.NextDouble(),
                    Tint = palette[random.Next(palette.Length)]
                };
        }

        struct Piece
        {
            public float X;
            public float Y;
            public float Size;
            public float Fall;
            public float Spin;
            public float Phase;
            public Color Tint;
        }
    }
}
