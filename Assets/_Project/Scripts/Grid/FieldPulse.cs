using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Движение поля: дыхание живых плиток, волна от открытой плитки и нагрев ободка от накала
    /// склада. Всё это делает вершинами и пикселями шейдер `Game/TileState`, а отсюда он получает
    /// три глобала — по одному вызову на клик и на кадр.
    ///
    /// Глобалы, а не свойства на плитку: анимация общая для всего поля, а инстанс-буфер плитки
    /// стоит замера инстансинга (§5) и без нужды не растёт. Дымку шейдер и так берёт из буфера,
    /// поэтому «живая плитка» опознаётся без единого нового поля.
    ///
    /// Глобалы переживают перезагрузку сцены — рестарт партии в этом проекте именно она, — и
    /// поэтому <see cref="Reset"/> зовётся на старте партии: иначе новое поле началось бы
    /// разогретым с прошлой и с недоигранной волной.
    /// </summary>
    public static class FieldPulse
    {
        static readonly int WaveId = Shader.PropertyToID("_FieldWave");
        static readonly int HeatId = Shader.PropertyToID("_FieldHeat");
        static readonly int PulseId = Shader.PropertyToID("_FieldPulse");

        /// <summary>
        /// Насколько давно должна была пройти волна, чтобы её не было вовсе. Жизнь волны — доли
        /// секунды, и сутки в минусе гарантируют, что поле стоит ровно с первого кадра.
        /// </summary>
        const float LongAgo = -86400f;

        /// <summary>Поле замерло и стоит холодным: партия начинается с чистого листа.</summary>
        public static void Reset()
        {
            Shader.SetGlobalVector(WaveId, new Vector4(0f, 0f, 0f, LongAgo));
            Shader.SetGlobalFloat(HeatId, 0f);
            Shader.SetGlobalFloat(PulseId, 1f);
        }

        /// <summary>
        /// Плитку открыли: от неё расходится волна. Время берётся тем же, каким его видит
        /// шейдер в `_Time.y`, — это секунды с загрузки сцены, а не с запуска игры: рестарт
        /// перезагружает сцену, и `Time.time` ушёл бы вперёд шейдерного времени навсегда.
        /// </summary>
        public static void Wave(Vector3 origin) => Shader.SetGlobalVector(
            WaveId, new Vector4(origin.x, origin.y, origin.z, Time.timeSinceLevelLoad));

        /// <summary>Доля накала склада: на ней ободок поля уползает в тёплое.</summary>
        public static void Heat(float share) => Shader.SetGlobalFloat(HeatId, Mathf.Clamp01(share));

        /// <summary>
        /// Общий рубильник движения. Гасится на время выпечки иконки: `ResourceIconBaker` печёт
        /// снимок тем же материалом, что и поле, снимок делается один раз и навсегда, — а волна
        /// или вдох, пришедшие ровно в этот кадр, впеклись бы в иконку вместе с моделью.
        /// </summary>
        public static void Motion(bool enabled) => Shader.SetGlobalFloat(PulseId, enabled ? 1f : 0f);
    }
}
