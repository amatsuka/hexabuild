using System;
using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using Game.Merge;

namespace Game.Core
{
    /// <summary>
    /// Конец партии и финальный счёт. Партия кончается, когда заработать больше нечем: очки
    /// приходят только из обмена крафта, крафт — только с подключённой плитки, а подключает её
    /// дорога за щебень. Обрыв любого звена без возможности его починить — это и есть конец.
    ///
    /// Отдельного условия «всё открыто и всё выработано» в коде нет намеренно: пройденное поле
    /// само приводит к тому, что зарабатывать не на чем. Проверять его отдельно значило бы
    /// обрывать партию у аккуратного игрока с полным складом ресурсов, ещё не обменянных на очки.
    /// Пройдено поле или игрок встал в тупик — видно по `FinalScore.IsPerfect`.
    /// </summary>
    public sealed class GameEndSystem
    {
        readonly GameState state;
        readonly MergeRules rules;
        readonly DeliverySystem deliveries;
        readonly List<TileData> reachable;
        readonly int lossPenalty;
        readonly int fieldBonus;
        readonly int depositBonus;

        public GameEndSystem(
            GameState state,
            MergeRules rules,
            DeliverySystem deliveries,
            int lossPenalty,
            int fieldBonus,
            int depositBonus)
        {
            this.state = state;
            this.rules = rules;
            this.deliveries = deliveries;
            this.lossPenalty = lossPenalty;
            this.fieldBonus = fieldBonus;
            this.depositBonus = depositBonus;
            reachable = state.Map.ReachableFromMetropolis();
        }

        /// <summary>Партия закончена, счёт посчитан.</summary>
        public event Action<FinalScore> Ended;

        public bool HasEnded { get; private set; }

        public FinalScore Score { get; private set; }

        /// <summary>Проверка идёт по тику: состояний, из которых складывается конец, слишком много.</summary>
        public void Tick()
        {
            if (HasEnded || CanStillEarn())
                return;

            HasEnded = true;
            Score = BuildScore();
            Ended?.Invoke(Score);
        }

        /// <summary>Счёт на текущий момент партии: нужен и финальному экрану, и тестам.</summary>
        public FinalScore BuildScore()
        {
            var total = 0;
            var exhausted = 0;

            foreach (var tile in reachable)
                foreach (var deposit in tile.Deposits)
                {
                    total++;
                    if (deposit.IsExhausted)
                        exhausted++;
                }

            return new FinalScore(
                state.Wallet.TotalEarned,
                state.Storage.LostCount,
                lossPenalty,
                state.OpenedTiles,
                reachable.Count - 1,
                exhausted,
                total,
                fieldBonus,
                depositBonus);
        }

        /// <summary>
        /// На поле не осталось хода: ничего не едет, ни одна подключённая плитка не производит,
        /// открыть не на что и построить некуда. Всё, что ещё стоит очков, лежит на складе.
        ///
        /// Это одно состояние, а не два. Пройденное поле — его частный случай: когда каждая
        /// достижимая плитка открыта и выработана, производить нечему и открывать нечего.
        /// Тупик — второй: поле осталось, но ни очков на открытие, ни щебня на дорогу больше
        /// не будет, потому что взяться им неоткуда. Раньше в коде жил только первый, и кнопка
        /// продажи не пришла бы туда, где доклик раздражает сильнее всего.
        /// </summary>
        public bool NothingLeftOnField =>
            deliveries.Active.Count == 0 && !CanOpenTile() && !CanStillReachReserve();

        /// <summary>
        /// Ещё есть действие, способное дать очки. Отдельного вопроса «а хватит ли на дорогу или
        /// на открытие» здесь нет, и он не нужен: щебень на дорогу — тот же щебень, что меняется
        /// на очки, поэтому любой запас на постройку уже виден как обмениваемый ресурс. Нет
        /// ресурсов и нет работающей плитки — значит и построить нечем, сколько бы очков ни было.
        /// </summary>
        bool CanStillEarn() =>
            deliveries.Active.Count > 0 || HasCashableResource() || HasProducingTile();

        /// <summary>Крафт на складе меняется на очки сразу, базовый — после слияния.</summary>
        public bool HasCashableResource()
        {
            for (var index = 0; index < state.Storage.Capacity; index++)
            {
                var content = state.Storage[index];
                if (!content.HasValue)
                    continue;

                if (!rules.CanMerge(content.Value))
                    return true;

                if (state.Storage.CountOf(content.Value) >= rules.SmallCount)
                    return true;
            }

            return false;
        }

        /// <summary>Очков хватает на открытие, и есть что открывать.</summary>
        bool CanOpenTile()
        {
            if (state.Wallet.Points < state.NextTileCost)
                return false;

            foreach (var tile in state.Map.Tiles.Values)
                if (tile.State == TileState.Available && tile.IsPassable)
                    return true;

            return false;
        }

        /// <summary>
        /// Запас на поле, до которого ещё можно дотянуться. Подключённая плитка с остатком везёт
        /// сама; к неподключённой нужна дорога, а на дорогу — щебень, который либо лежит на
        /// складе, либо получится из камня. Ни того ни другого — и плитка с запасом всё равно
        /// что выработана: пути к ней больше нет.
        ///
        /// Цена дороги здесь не считается по шагам намеренно: путь к плитке ищется заново после
        /// каждой постройки, и точный ответ «хватит ли до конца» стоил бы обхода на каждый кадр.
        /// Ошибаться этот ответ будет в одну сторону — «ход ещё есть», — а это лишь отложит
        /// подсказку до следующего прихода кнопки по таймеру, а не оборвёт партию раньше времени.
        /// </summary>
        bool CanStillReachReserve()
        {
            var waiting = false;

            foreach (var tile in reachable)
            {
                if (tile.State != TileState.Revealed || tile.IsExhausted)
                    continue;

                if (state.Roads.IsConnected(tile.Coord))
                    return true;

                waiting = true;
            }

            return waiting && CanStillGetGravel();
        }

        /// <summary>Щебень есть или получится: камня на складе хватает на слияние.</summary>
        bool CanStillGetGravel() =>
            state.Storage.CountOf(ResourceType.Gravel) > 0
            || state.Storage.CountOf(ResourceType.Stone) >= rules.SmallCount;

        /// <summary>Подключённая открытая плитка с остатком запаса: ресурс поедет сам.</summary>
        bool HasProducingTile()
        {
            foreach (var coord in state.Roads.Roads)
            {
                if (!state.Roads.IsConnected(coord) || !state.Map.TryGetTile(coord, out var tile))
                    continue;

                if (tile.State == TileState.Revealed && !tile.IsExhausted)
                    return true;
            }

            return false;
        }
    }
}
