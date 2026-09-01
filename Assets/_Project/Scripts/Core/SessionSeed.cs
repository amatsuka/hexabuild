namespace Game.Core
{
    /// <summary>
    /// Сид следующей партии. Рестарт с финального экрана — это перезагрузка сцены, а сцена
    /// заводит всё заново, поэтому «Повторить карту» иначе выдала бы другую карту: при сиде 0
    /// в конфиге он каждый раз случайный. Пережить перезагрузку сцены здесь может только
    /// статика — сохранения партии в проекте нет и не заводится.
    ///
    /// Сохранением это не является: поле живёт до конца процесса, а не между запусками.
    /// Единственное, что переживает запуск, по-прежнему рекорд (<see cref="HighScore"/>).
    /// </summary>
    public static class SessionSeed
    {
        /// <summary>Сид, заказанный кнопкой. Ноль — заказа нет, партия решает сама.</summary>
        static int requested;

        /// <summary>Сид партии: заказанный кнопкой, иначе из конфига, иначе случайный.</summary>
        public static int Take(int configured)
        {
            var seed = requested != 0 ? requested : configured != 0 ? configured : Fresh();
            requested = 0;
            return seed;
        }

        /// <summary>«Повторить карту»: следующая партия идёт по тому же сиду.</summary>
        public static void Repeat(int seed) => requested = seed;

        /// <summary>
        /// «Новая карта»: следующая партия идёт по свежему сиду. Заданный в конфиге сид ей не
        /// указ — кнопка обязана делать то, что на ней написано, а не повторять ту же карту.
        /// </summary>
        public static void Renew() => requested = Fresh();

        /// <summary>Случайный сид. Ноль исключён: он и значит «сида нет».</summary>
        static int Fresh() => UnityEngine.Random.Range(1, int.MaxValue);
    }
}
