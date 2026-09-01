using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Скруглённые спрайты панелей. Рисуются кодом, как и остальной арт проекта: панель — это
    /// скруглённый прямоугольник, и упереться тут не во что. Заодно палитра и радиус остаются
    /// числами, а не пикселями, — их пересчитывают вместе с гаммой, не перепекая файлы.
    ///
    /// Текстура строится ровно под 9-slice: сторона `радиус * 2 + 2`, бордюр `радиус` со всех
    /// сторон. Тянется только средняя полоса в два пикселя, и она внутри фигуры — целиком
    /// непрозрачная, поэтому растяжение не размывает кромку.
    /// </summary>
    public static class UiSprites
    {
        /// <summary>Середина спрайта, которую 9-slice тянет. Меньше двух пикселей билинейка мылит.</summary>
        const int StretchBand = 2;

        const float PixelsPerUnit = 100f;

        static readonly Dictionary<int, Sprite> Cache = new();

        /// <summary>
        /// Скруглённый прямоугольник радиуса `radius` в пикселях. Один спрайт на радиус: панелей
        /// в HUD полтора десятка, а радиусов у них два-три.
        /// </summary>
        public static Sprite Rounded(int radius)
        {
            radius = Mathf.Max(0, radius);
            if (Cache.TryGetValue(radius, out var cached) && cached != null)
                return cached;

            var side = radius * 2 + StretchBand;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
            {
                name = $"UiRounded{radius}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[side * side];
            var half = side * 0.5f;
            for (var y = 0; y < side; y++)
            for (var x = 0; x < side; x++)
                pixels[y * side + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(
                    Coverage(x + 0.5f - half, y + 0.5f - half, half, radius) * 255f));

            texture.SetPixels32(pixels);
            texture.Apply(false);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, side, side),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            Cache[radius] = sprite;
            return sprite;
        }

        /// <summary>
        /// Сколько пикселя попало внутрь скруглённого прямоугольника. Считается по расстоянию до
        /// кромки, а не выборкой: кромка получается с полупикселем сглаживания и без ступенек.
        /// </summary>
        static float Coverage(float x, float y, float half, int radius)
        {
            var inner = half - radius;
            var qx = Mathf.Abs(x) - inner;
            var qy = Mathf.Abs(y) - inner;
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            var distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            return Mathf.Clamp01(0.5f - distance);
        }
    }
}
