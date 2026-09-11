using System.Collections.Generic;
using Game.Economy;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Карта первого уровня кампании — рукотворная, а не сгенерированная (решение человека
    /// 11.09.2026). Уровень знакомит с механиками, и «примерно предсказуемой» карты для этого
    /// мало: шум давал то лишнее месторождение, то второй обход гряды, то реку не там, и первые
    /// минуты партии выходили у каждого игрока разными.
    ///
    /// Форма поля остаётся общей — тот же раструб <see cref="HexMap.CoordsInFlare"/>, — рукотворно
    /// только содержимое: биом, месторождение и русло. Ниже гряды поле сведено в коридор без
    /// выбора: камень, дерево, обход гор и переправа встречаются по одному разу и по порядку.
    /// Выше неё — обычная суша с месторождениями, там игрок уже сам по себе.
    ///
    /// Числа месторождений здесь литеральные и <c>ReserveScale</c> уровня не слушают: щедрость
    /// карты — рычаг генератора, а у рукотворной карты рычаг один, и это сами числа.
    /// </summary>
    public static class TutorialMap
    {
        /// <summary>Сосед Метрополии с камнем: с него начинается партия.</summary>
        public static readonly HexCoord StoneTile = new(-1, 1);

        /// <summary>Второй сосед, с лесом: из него доски на мост.</summary>
        public static readonly HexCoord WoodTile = new(0, 1);

        /// <summary>
        /// Подход к реке: две плитки, которые обучение открывает одним шагом. Стен между ними
        /// нет — дорогу держит само обучение, а горы внизу стоят рядом, чтобы их было видно.
        /// </summary>
        public static readonly HexCoord BypassTile = new(0, 2);

        public static readonly HexCoord PassTile = new(-1, 3);

        /// <summary>
        /// Брод посреди карты, на реке, спускающейся с верхних гор. Здесь игрок и строит мост.
        /// </summary>
        public static readonly HexCoord RiverTile = new(-1, 4);

        /// <summary>
        /// Две горы в нижней части — показать игроку стену вблизи. Рек с них не спускается
        /// намеренно: русла идут с верхней гряды, и мост ставится на них, а не под носом.
        /// </summary>
        public static readonly HexCoord[] Ridge = { new(-1, 2), new(-2, 3) };

        /// <summary>Верхняя гряда: она и есть исток всех рек карты.</summary>
        static readonly HexCoord[] Peaks =
        {
            new(-5, 7), new(-4, 7), new(-2, 7), new(-1, 7), new(-5, 6), new(-2, 6)
        };

        /// <summary>
        /// Русла. Левое течёт с `(-5,7)`, к нему у `(-3,3)` сходится приток с `(-2,6)` — это
        /// и есть развилка; правое спускается с `(-1,7)` через брод `(-1,4)` к краю поля.
        /// Все три идут сверху вниз: река, начатая в нижнем ряду, читалась обрубком.
        /// </summary>
        static readonly HexCoord[][] Streams =
        {
            new[] { new HexCoord(-5, 7), new(-5, 6), new(-5, 5), new(-4, 4), new(-3, 3) },
            new[] { new HexCoord(-2, 6), new(-2, 5), new(-2, 4), new(-3, 4), new(-3, 3) },
            new[] { new HexCoord(-1, 7), new(-1, 6), new(-1, 5), new(-1, 4), new(0, 3) }
        };

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
        const float WaterLevel = 0.17f;
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

            if (Holds(Ridge, coord) || Holds(Peaks, coord))
                return BiomeType.Mountains;

            // Брод — скала: видно, что переходят реку по камню, а не вброд по траве.
            if (coord == RiverTile)
                return BiomeType.Rocks;

            var roll = coord.Hash01(DepositSalt);
            if (roll < 0.32f)
                return BiomeType.Meadow;

            return roll < 0.88f ? BiomeType.Forest : BiomeType.Rocks;
        }

        static bool Holds(HexCoord[] coords, HexCoord coord)
        {
            foreach (var known in coords)
                if (known == coord)
                    return true;

            return false;
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

            // Русло идёт по поверхности плитки, как дорога: месторождению на ней уже не место.
            // Плитки, по которым обучение ведёт игрока, тоже пустые — они транзит, а не добыча.
            if (coord == HexCoord.Zero || hasRiver || !TileData.IsPassableBiome(biome)
                || coord == BypassTile || coord == PassTile)
                return null;

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
                BiomeType.Water => WaterLevel,
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

            foreach (var stream in Streams)
                Carve(biomes, stream, masks, down, flow);
            return (masks, down, flow);
        }

        /// <summary>
        /// Один поток. Приток начинается на плитке главного русла и там же складывает свой расход
        /// с чужим: на развилке лента обязана быть шире каждого из рукавов.
        /// </summary>
        static void Carve(
            IReadOnlyDictionary<HexCoord, BiomeType> biomes,
            HexCoord[] course,
            Dictionary<HexCoord, int> masks,
            Dictionary<HexCoord, int> down,
            Dictionary<HexCoord, int> flow)
        {
            for (var i = 0; i < course.Length; i++)
            {
                if (!biomes.ContainsKey(course[i]))
                    continue;

                masks.TryAdd(course[i], 0);
                flow[course[i]] = flow.GetValueOrDefault(course[i]) + i + 1;

                if (i + 1 >= course.Length || !biomes.ContainsKey(course[i + 1]))
                    continue;

                var direction = DirectionTo(course[i], course[i + 1]);
                if (direction < 0)
                    continue;

                masks[course[i]] = masks.GetValueOrDefault(course[i]) | (1 << direction);
                down[course[i]] = down.GetValueOrDefault(course[i]) | (1 << direction);
                masks[course[i + 1]] = masks.GetValueOrDefault(course[i + 1]) | (1 << Opposite(direction));
            }
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
