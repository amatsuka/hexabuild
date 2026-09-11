using System.Collections.Generic;
using Game.Economy;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Карта первого уровня кампании — рукотворная, а не сгенерированная (решение человека
    /// 11.09.2026). Обучение ведёт по ней за руку, и «примерно предсказуемой» карты для этого
    /// мало: шум давал то лишнее месторождение, то второй обход гряды, то реку не там, и каждый
    /// шаг приходилось закрывать эмерджентным условием вместо одного разрешённого действия.
    ///
    /// Форма поля остаётся общей — тот же раструб <see cref="HexMap.CoordsInFlare"/>, — рукотворно
    /// только содержимое: биом, месторождение и русло. Ниже гряды поле сведено в коридор без
    /// выбора, выше неё — обычная суша с месторождениями: там обучение уже отпускает игрока.
    ///
    /// Числа месторождений здесь литеральные и <c>ReserveScale</c> уровня не слушают: щедрость
    /// карты — рычаг генератора, а у рукотворной карты рычаг один, и это сами числа.
    /// </summary>
    public static class TutorialMap
    {
        /// <summary>Сосед Метрополии с камнем: с него начинается партия.</summary>
        public static readonly HexCoord StoneTile = new(-1, 1);

        /// <summary>Второй сосед, с лесом: из него доски на первый мост.</summary>
        public static readonly HexCoord WoodTile = new(0, 1);

        /// <summary>Единственный проход сквозь гряду: он же цель шага про обход.</summary>
        public static readonly HexCoord BypassTile = new(-2, 2);

        /// <summary>Единственная переправа через реку. Скала, значит мост будет каменной аркой.</summary>
        public static readonly HexCoord RiverTile = new(-2, 3);

        /// <summary>Гряда во втором ряду: стена прямо перед игроком, обход от неё слева.</summary>
        public static readonly HexCoord[] Ridge = { new(-1, 2), new(0, 2) };

        /// <summary>Русло третьего ряда. Течёт слева направо, проходима в нём одна плитка.</summary>
        static readonly HexCoord[] River = { new(-3, 3), new(-2, 3), new(-1, 3), new(0, 3) };

        /// <summary>
        /// Запас камня и дерева у Метрополии. Эти два месторождения — топливо всего обучения,
        /// и оно обязано покрывать его собственные траты **с запасом**: на двадцати единицах
        /// сценарий выедал их досуха (три дороги, мост и контракт), и кнопка «Продать всё»
        /// не приходила вовсе — ей нужен крафт сверх резерва 4/4.
        /// </summary>
        const int NeighborReserve = 36;

        /// <summary>
        /// Запас месторождения на свободном поле за рекой и доля плиток, которым оно достаётся.
        /// Уровень в кривую кампании не встроен (решение человека 11.09.2026) — он показывает
        /// механику и даёт потыкать игру, — но за мостом игроку должно быть чем заняться:
        /// на скупом поле бот вставал в тупик через полминуты.
        /// </summary>
        const int FieldReserve = 32;

        const float DepositChance = 0.85f;

        /// <summary>Отметки высоты в середине своей полосы биома: биом и рельеф идут из одного числа.</summary>
        const float MeadowLevel = 0.43f;
        const float ForestLevel = 0.55f;
        const float RocksLevel = 0.62f;
        const float MountainLevel = 0.70f;

        /// <summary>Разброс высоты внутри полосы биома: без него поле ложится плоскими террасами.</summary>
        const float LevelJitter = 0.02f;

        const int JitterSalt = 41;
        const int ShadeSalt = 43;
        const int DepositSalt = 47;

        public static HexMap Build(int rows)
        {
            var biomes = new Dictionary<HexCoord, BiomeType>();
            foreach (var coord in HexMap.CoordsInFlare(rows))
                biomes[coord] = BiomeOf(coord);

            var (masks, downMasks, flow) = CarveRiver(biomes);

            var tiles = new List<TileData>(biomes.Count);
            foreach (var pair in biomes)
            {
                var coord = pair.Key;
                tiles.Add(new TileData(
                    coord,
                    coord == HexCoord.Zero,
                    DepositsOf(coord, pair.Value, masks.ContainsKey(coord)),
                    pair.Value,
                    coord == HexCoord.Zero ? 0f : coord.Hash01(ShadeSalt),
                    masks.GetValueOrDefault(coord),
                    Elevation(coord, pair.Value),
                    flow.GetValueOrDefault(coord),
                    downMasks.GetValueOrDefault(coord)));
            }

            return new HexMap(rows, tiles);
        }

        /// <summary>
        /// Ландшафт коридора. Ниже гряды выбора нет вовсе: два соседа Метрополии под первые
        /// уроки, гряда с единственным проходом, река с единственной переправой. Выше реки —
        /// обычный лес с луговыми проплешинами, там игрок уже играет сам.
        /// </summary>
        static BiomeType BiomeOf(HexCoord coord)
        {
            if (coord == HexCoord.Zero)
                return BiomeType.Meadow;

            if (coord == StoneTile || coord == WoodTile)
                return BiomeType.Forest;

            if (coord == BypassTile)
                return BiomeType.Meadow;

            foreach (var ridge in Ridge)
                if (coord == ridge)
                    return BiomeType.Mountains;

            // Третий ряд — русло: проходима в нём только переправа, остальное гряда.
            if (coord.R == 3)
                return coord == RiverTile ? BiomeType.Rocks : BiomeType.Mountains;

            return coord.Hash01(DepositSalt) < 0.3f ? BiomeType.Meadow : BiomeType.Forest;
        }

        /// <summary>
        /// Месторождения. У соседей Метрополии — ровно по одному и большому: три одинаковых
        /// ресурса подряд и есть единственное место, где обучение ждёт, и два месторождения
        /// вперемешку растянули бы это ожидание вдвое. За рекой месторождения обычные.
        /// </summary>
        static IReadOnlyList<Deposit> DepositsOf(HexCoord coord, BiomeType biome, bool hasRiver)
        {
            if (coord == StoneTile)
                return new[] { new Deposit(ResourceType.Stone, NeighborReserve) };

            if (coord == WoodTile)
                return new[] { new Deposit(ResourceType.Wood, NeighborReserve) };

            if (coord == HexCoord.Zero || hasRiver || !TileData.IsPassableBiome(biome) || coord.R < 4)
                return null;

            // За рекой — обычное поле: тип по хэшу координаты, то есть карта та же при каждом запуске.
            var roll = coord.Hash01(DepositSalt + coord.R);
            if (roll > DepositChance)
                return null;

            var type = roll < 0.34f ? ResourceType.Wood : roll < 0.62f ? ResourceType.Stone : ResourceType.Ore;
            return new[] { new Deposit(type, FieldReserve) };
        }

        static float Elevation(HexCoord coord, BiomeType biome)
        {
            var band = biome switch
            {
                BiomeType.Mountains => MountainLevel,
                BiomeType.Rocks => RocksLevel,
                BiomeType.Forest => ForestLevel,
                _ => MeadowLevel
            };

            return band + (coord.Hash01(JitterSalt) - 0.5f) * 2f * LevelJitter;
        }

        /// <summary>
        /// Русло третьего ряда, слева направо. Маска у плитки — биты тех граней, через которые
        /// река уходит к соседу; у соседа стоит встречный бит. `downMask` помечает грань вниз
        /// по течению, `flow` растёт от истока к устью — по нему считается ширина ленты.
        /// </summary>
        static (Dictionary<HexCoord, int> Masks, Dictionary<HexCoord, int> Down, Dictionary<HexCoord, int> Flow)
            CarveRiver(IReadOnlyDictionary<HexCoord, BiomeType> biomes)
        {
            var masks = new Dictionary<HexCoord, int>();
            var down = new Dictionary<HexCoord, int>();
            var flow = new Dictionary<HexCoord, int>();

            for (var i = 0; i < River.Length; i++)
            {
                if (!biomes.ContainsKey(River[i]))
                    continue;

                masks.TryAdd(River[i], 0);
                flow[River[i]] = i + 1;

                if (i + 1 >= River.Length || !biomes.ContainsKey(River[i + 1]))
                    continue;

                var direction = DirectionTo(River[i], River[i + 1]);
                if (direction < 0)
                    continue;

                masks[River[i]] = masks.GetValueOrDefault(River[i]) | (1 << direction);
                down[River[i]] = down.GetValueOrDefault(River[i]) | (1 << direction);
                masks[River[i + 1]] = masks.GetValueOrDefault(River[i + 1]) | (1 << Opposite(direction));
            }

            return (masks, down, flow);
        }

        /// <summary>Индекс направления от одной плитки к соседней. −1 — плитки не соседи.</summary>
        static int DirectionTo(HexCoord from, HexCoord to)
        {
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                if (from.Neighbor(direction) == to)
                    return direction;

            return -1;
        }

        static int Opposite(int direction) => (direction + 3) % 6;
    }
}
