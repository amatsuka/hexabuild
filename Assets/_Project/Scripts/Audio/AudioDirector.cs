using Game.Core;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Storage;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Звук партии: одна подписка на все события систем вместо звонка из каждого обработчика.
    /// Партия о звуке не знает — `GameSession` только заводит директора и отдаёт ему системы.
    ///
    /// Интерфейс сюда не ходит: клик кнопки звучит в самой `UiButton`, финальный экран — в
    /// `GameOverView`. Здесь только то, что происходит с миром и складом.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        /// <summary>С этой секунды контракта идёт тревожный слой, а с половины её — тиканье.</summary>
        const float UrgencySeconds = 10f;

        const string UrgencyLoop = "mus_contract_urgency_loop";
        const string DrainLoop = "sfx_heat_drain";

        GameState state;
        ProductionSystem production;
        DeliverySystem deliveries;
        MergeSystem merges;
        ContractSystem contracts;
        Milestones milestones;
        GameEndSystem end;
        ScoreMultiplier multiplier;

        /// <summary>Что уже отзвучало открытием: `TileChanged` приходит и на каждую добычу.</summary>
        readonly System.Collections.Generic.HashSet<HexCoord> opened = new();

        /// <summary>Дороги, которые уже слышали: событие сети не говорит, какая из них новая.</summary>
        readonly System.Collections.Generic.HashSet<HexCoord> roads = new();

        float heat;
        int lastTick;

        public void Bind(
            GameState gameState,
            ProductionSystem productionSystem,
            DeliverySystem deliverySystem,
            MergeSystem mergeSystem,
            ContractSystem contractSystem,
            Milestones gameMilestones,
            GameEndSystem endSystem)
        {
            state = gameState;
            production = productionSystem;
            deliveries = deliverySystem;
            merges = mergeSystem;
            contracts = contractSystem;
            milestones = gameMilestones;
            end = endSystem;
            multiplier = state.Multiplier;

            state.TileChanged += OnTileChanged;
            state.ActionRefused += OnRefused;
            state.Roads.Changed += OnRoadsChanged;
            state.Storage.ResourceLost += OnResourceLost;
            multiplier.Changed += OnMultiplierChanged;
            multiplier.Burned += OnBurned;
            production.Produced += OnProduced;
            production.TileDepleted += OnDepleted;
            deliveries.Arrived += OnArrived;
            merges.Refused += OnRefused;
            merges.Merged += OnMerged;
            merges.Converted += OnConverted;
            merges.Swept += OnSwept;
            contracts.Issued += OnContractIssued;
            contracts.Progressed += OnContractProgressed;
            contracts.Completed += OnContractCompleted;
            contracts.Failed += OnContractFailed;
            milestones.Reached += OnMilestone;
            end.Ended += OnEnded;

            // ponytail: амбиенс фиксированной громкостью. Бриф просит воду по доле воды в кадре,
            // а гул Метрополии позиционно — обе связки стоят своей возни, а слышно их одинаково.
            GameAudio.Loop("amb_field_loop", 0.18f);
            GameAudio.Loop("amb_water_loop", 0.14f);
            GameAudio.Loop("amb_metropolis_loop", 0.12f);
        }

        void OnDestroy()
        {
            if (state == null)
                return;

            state.TileChanged -= OnTileChanged;
            state.ActionRefused -= OnRefused;
            state.Roads.Changed -= OnRoadsChanged;
            state.Storage.ResourceLost -= OnResourceLost;
            multiplier.Changed -= OnMultiplierChanged;
            multiplier.Burned -= OnBurned;
            production.Produced -= OnProduced;
            production.TileDepleted -= OnDepleted;
            deliveries.Arrived -= OnArrived;
            merges.Refused -= OnRefused;
            merges.Merged -= OnMerged;
            merges.Converted -= OnConverted;
            merges.Swept -= OnSwept;
            contracts.Issued -= OnContractIssued;
            contracts.Progressed -= OnContractProgressed;
            contracts.Completed -= OnContractCompleted;
            contracts.Failed -= OnContractFailed;
            milestones.Reached -= OnMilestone;
            end.Ended -= OnEnded;
        }

        /// <summary>
        /// Партия кончилась: тревожный слой и утечка обязаны замолчать, а дальше директору
        /// слушать нечего — счёт подводит финальный экран, и звучит он сам.
        /// </summary>
        void OnEnded(FinalScore score)
        {
            GameAudio.StopLoop(UrgencyLoop);
            GameAudio.StopLoop(DrainLoop);
            enabled = false;
        }

        /// <summary>
        /// Два состояния, у которых нет своего события: утечка накала и последние секунды
        /// контракта. Оба — не момент, а полоса времени, и слышны они лупом.
        /// </summary>
        void Update()
        {
            if (state == null)
                return;

            if (multiplier.HeatLeaking)
                GameAudio.Loop(DrainLoop, 0.3f * multiplier.HeatShare);
            else
                GameAudio.StopLoop(DrainLoop);

            if (!contracts.IsActive || contracts.SecondsLeft > UrgencySeconds)
            {
                GameAudio.StopLoop(UrgencyLoop);
                lastTick = 0;
                return;
            }

            GameAudio.Loop(UrgencyLoop, 0.35f);

            // Тиканье идёт на целой секунде и только под самый конец: вместе с лупом всю
            // десятку оно читалось бы дребезгом, а не отсчётом.
            var second = Mathf.CeilToInt(contracts.SecondsLeft);
            if (second <= UrgencySeconds * 0.5f && second != lastTick)
            {
                lastTick = second;
                GameAudio.Play("sfx_contract_tick", 0.5f);
            }
        }

        /// <summary>
        /// Плитка открыта: удар земли, волна по полю и хвост её содержимого. Событие приходит
        /// и на добычу, и на соседей, ставших доступными, — открытие узнаётся по состоянию
        /// и по тому, что эту плитку ещё не слышали. Метрополия открыта с начала партии
        /// и звучать не должна.
        /// </summary>
        void OnTileChanged(TileData tile)
        {
            if (tile.IsMetropolis || tile.State != TileState.Revealed || !opened.Add(tile.Coord))
                return;

            GameAudio.Play("sfx_tile_open", 0.9f, Random.Range(0.96f, 1.04f));
            GameAudio.Play("sfx_tile_open_wave", 0.4f);

            var tail = Tail(tile);
            if (tail != null)
                GameAudio.Play(tail, 0.5f);
        }

        /// <summary>Хвост по содержимому плитки: руда громче биома, её и слышно.</summary>
        static string Tail(TileData tile)
        {
            foreach (var deposit in tile.Deposits)
                if (deposit.Type == ResourceType.Ore)
                    return "sfx_tile_open_ore";

            return tile.Biome switch
            {
                BiomeType.Forest => "sfx_tile_open_forest",
                BiomeType.Rocks or BiomeType.Mountains => "sfx_tile_open_rock",
                BiomeType.Water => "sfx_tile_open_water",
                _ => null
            };
        }

        /// <summary>
        /// Сеть дорог не говорит, какая дорога новая, — её находит разница с прошлым разом.
        /// Мост слышен отдельно: он стоит втрое дороже и строится через реку.
        /// </summary>
        void OnRoadsChanged()
        {
            foreach (var coord in state.Roads.Roads)
            {
                if (!roads.Add(coord))
                    continue;

                if (state.Map.TryGetTile(coord, out var tile) && tile.HasRiver)
                    GameAudio.Play(tile.Biome == BiomeType.Rocks
                        ? "sfx_bridge_build_stone"
                        : "sfx_bridge_build_wood", 0.8f);
                else
                    GameAudio.Play("sfx_road_build", 0.7f, Random.Range(0.95f, 1.05f));
            }
        }

        void OnProduced(TileData tile, ResourceType type)
        {
            var clip = type switch
            {
                ResourceType.Wood => "sfx_extract_wood",
                ResourceType.Stone => "sfx_extract_stone",
                _ => "sfx_extract_ore"
            };

            // Самый частый звук в игре: тихо и с разбросом питча, иначе поздние уровни гудят.
            GameAudio.Play(clip, 0.35f, Random.Range(0.92f, 1.08f));
        }

        void OnDepleted(TileData tile) => GameAudio.Play("sfx_deposit_depleted", 0.5f);

        void OnArrived(Delivery delivery) => GameAudio.Play(
            "sfx_delivery_arrive", 0.45f, Random.Range(0.95f, 1.05f));

        /// <summary>Крупное слияние звучит ударом: оно же останавливает время.</summary>
        void OnMerged(MergeReport report) => GameAudio.Play(
            report.ResultCells.Count > 1 ? "sfx_merge_big" : "sfx_merge_small", 0.8f);

        /// <summary>Обмен крафта: короткий и тихий — на поздних уровнях он идёт по десять раз в секунду.</summary>
        void OnConverted(int cell, ResourceType type, int points) => GameAudio.Play(
            "sfx_convert", 0.45f, 1f + 0.15f * multiplier.HeatShare);

        void OnSwept(int cell, int points) => GameAudio.Play("sfx_storage_sweep", 0.9f);

        void OnResourceLost(ResourceType type) => GameAudio.Play("sfx_storage_lost");

        void OnBurned() => GameAudio.Play("sfx_heat_burn");

        void OnRefused(string reason) => GameAudio.Play("sfx_refused", 0.5f);

        /// <summary>
        /// Множитель меняют и открытая плитка, и серия контрактов, и накал. Ступенью звучит
        /// только накал вверх: лестница питча и есть то, чем накал слышен раньше, чем виден.
        /// </summary>
        void OnMultiplierChanged()
        {
            var grown = multiplier.Heat > heat + 0.001f;
            heat = multiplier.Heat;

            if (!grown)
                return;

            var step = multiplier.HeatStep > 0f ? Mathf.RoundToInt(heat / multiplier.HeatStep) : 0;
            GameAudio.Play("sfx_heat_step", 0.4f, Mathf.Pow(1.0595f, Mathf.Min(step, 12)));
        }

        void OnContractIssued() => GameAudio.Play("sfx_contract_issued", 0.7f);

        void OnContractProgressed() => GameAudio.Play(
            "sfx_contract_progress", 0.6f, 1f + 0.08f * contracts.Delivered);

        void OnContractCompleted(int reward)
        {
            GameAudio.StopLoop(UrgencyLoop);
            GameAudio.Play("sfx_contract_complete", 0.9f, 1f + 0.05f * multiplier.Streak);
        }

        void OnContractFailed()
        {
            GameAudio.StopLoop(UrgencyLoop);
            GameAudio.Play("sfx_contract_failed", 0.7f);
        }

        void OnMilestone(float share) => GameAudio.Play("sfx_milestone", 0.8f, 1f + share * 0.3f);
    }
}
