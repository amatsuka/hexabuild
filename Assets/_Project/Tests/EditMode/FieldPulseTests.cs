using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Глобалы движения поля. Картинку тестами не проверить, но состояние — можно, и проверяется
    /// ровно то, на чём стоит шейдер: сброс гасит накал и уводит волну в прошлое (иначе рестарт
    /// начал бы партию разогретой и с недоигранной волной), волна помнит точку, накал зажат
    /// в долю, а рубильник глушит движение на время выпечки иконки.
    /// </summary>
    public sealed class FieldPulseTests
    {
        static readonly int WaveId = Shader.PropertyToID("_FieldWave");
        static readonly int HeatId = Shader.PropertyToID("_FieldHeat");
        static readonly int PulseId = Shader.PropertyToID("_FieldPulse");

        [TearDown]
        public void TearDown() => FieldPulse.Reset();

        [Test]
        public void Reset_StartsTheFieldColdAndStill()
        {
            FieldPulse.Heat(1f);
            FieldPulse.Wave(Vector3.zero);

            FieldPulse.Reset();

            Assert.That(Shader.GetGlobalFloat(HeatId), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(Shader.GetGlobalFloat(PulseId), Is.EqualTo(1f).Within(1e-5f));
            // Волна живёт доли секунды: время в глубоком прошлом и значит «волны нет».
            Assert.That(Shader.GetGlobalVector(WaveId).w, Is.LessThan(-1000f));
        }

        [Test]
        public void Wave_RemembersWhereItStarted()
        {
            FieldPulse.Reset();

            FieldPulse.Wave(new Vector3(3f, 0.4f, -2f));

            var wave = Shader.GetGlobalVector(WaveId);
            Assert.That(wave.x, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(wave.z, Is.EqualTo(-2f).Within(1e-5f));
            // Время шейдерное — секунды с загрузки сцены, а не с запуска игры.
            Assert.That(wave.w, Is.EqualTo(Time.timeSinceLevelLoad).Within(1e-3f));
        }

        [Test]
        public void Heat_KeepsTheShareInsideItsRange()
        {
            FieldPulse.Heat(2.5f);
            Assert.That(Shader.GetGlobalFloat(HeatId), Is.EqualTo(1f).Within(1e-5f));

            FieldPulse.Heat(-1f);
            Assert.That(Shader.GetGlobalFloat(HeatId), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Motion_StopsAndResumesTheWholeField()
        {
            FieldPulse.Motion(false);
            Assert.That(Shader.GetGlobalFloat(PulseId), Is.EqualTo(0f).Within(1e-5f));

            FieldPulse.Motion(true);
            Assert.That(Shader.GetGlobalFloat(PulseId), Is.EqualTo(1f).Within(1e-5f));
        }
    }
}
