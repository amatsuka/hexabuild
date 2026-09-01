using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Геометрия скруглённого спрайта панелей. Спрайт рисуется под 9-slice, и вся эта раскладка
    /// держится на двух вещах: бордюр совпадает с радиусом, а тянущаяся середина непрозрачна
    /// насквозь. Разъедься одно из них — панели поплывут кромкой, и в редакторе это заметно не
    /// сразу.
    /// </summary>
    public sealed class UiSpritesTests
    {
        static readonly int[] Radii = { 0, 3, 18, 26 };

        [TestCaseSource(nameof(Radii))]
        public void TheSprite_CarriesTheRadiusAsItsNineSliceBorder(int radius)
        {
            var sprite = UiSprites.Rounded(radius);

            Assert.That(sprite.border, Is.EqualTo(new Vector4(radius, radius, radius, radius)));
            Assert.That(sprite.texture.width, Is.EqualTo(radius * 2 + 2), "сторона — два радиуса и полоса растяжения");
            Assert.That(sprite.texture.height, Is.EqualTo(sprite.texture.width), "спрайт квадратный");
        }

        /// <summary>
        /// Средняя полоса — единственное, что 9-slice тянет. Просвети её хоть на пиксель, и
        /// растянутая панель поедет полосами по всей своей ширине.
        /// </summary>
        [TestCaseSource(nameof(Radii))]
        public void TheStretchedBand_StaysOpaqueAcrossTheWholeSprite(int radius)
        {
            var texture = UiSprites.Rounded(radius).texture;
            var side = texture.width;

            for (var band = 0; band < 2; band++)
            for (var i = 0; i < side; i++)
            {
                Assert.That(texture.GetPixel(i, radius + band).a, Is.EqualTo(1f).Within(0.005f),
                    $"строка растяжения, радиус {radius}, x {i}");
                Assert.That(texture.GetPixel(radius + band, i).a, Is.EqualTo(1f).Within(0.005f),
                    $"столбец растяжения, радиус {radius}, y {i}");
            }
        }

        [Test]
        public void TheCorner_StaysOutsideAndTheCentreInside()
        {
            var texture = UiSprites.Rounded(26).texture;

            Assert.That(texture.GetPixel(0, 0).a, Is.EqualTo(0f).Within(0.01f), "угол за скруглением");
            Assert.That(texture.GetPixel(texture.width / 2, texture.height / 2).a, Is.EqualTo(1f).Within(0.005f));
        }

        /// <summary>
        /// Кромка идёт полупрозрачными пикселями, а не ступенькой: на радиусе обязан найтись
        /// пиксель, попавший внутрь фигуры лишь частично.
        /// </summary>
        [Test]
        public void TheEdge_IsAntialiased()
        {
            var texture = UiSprites.Rounded(26).texture;
            var partial = 0;

            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
            {
                var alpha = texture.GetPixel(x, y).a;
                if (alpha > 0.05f && alpha < 0.95f)
                    partial++;
            }

            Assert.That(partial, Is.GreaterThan(8), "кромка нарисована ступенькой");
        }

        [TestCaseSource(nameof(Radii))]
        public void TheSilhouette_IsSymmetric(int radius)
        {
            var texture = UiSprites.Rounded(radius).texture;
            var side = texture.width;

            for (var y = 0; y < side; y++)
            for (var x = 0; x < side; x++)
            {
                var alpha = texture.GetPixel(x, y).a;
                Assert.That(texture.GetPixel(side - 1 - x, y).a, Is.EqualTo(alpha).Within(0.005f), $"по горизонтали {x},{y}");
                Assert.That(texture.GetPixel(x, side - 1 - y).a, Is.EqualTo(alpha).Within(0.005f), $"по вертикали {x},{y}");
            }
        }

        /// <summary>Один спрайт на радиус: панелей полтора десятка, а радиусов у них два-три.</summary>
        [Test]
        public void TheSameRadius_ReusesOneSprite()
        {
            Assert.That(UiSprites.Rounded(26), Is.SameAs(UiSprites.Rounded(26)));
            Assert.That(UiSprites.Rounded(26), Is.Not.SameAs(UiSprites.Rounded(18)));
        }
    }
}
