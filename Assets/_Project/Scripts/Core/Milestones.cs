using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Вехи партии: доли потолка, за прохождение которых игрок получает награду. Шкала у них
    /// своя — 25, 50 и 75%, — и со шкалой звёзд (50 / 75 / 100%) она не совпадает намеренно:
    /// звёзды подводят итог, а веха подбадривает по дороге.
    ///
    /// Каждая веха срабатывает один раз за партию. Памятью о пройденном пренебречь нельзя:
    /// счёт умеет проседать штрафом за потерю, и без неё одна и та же веха платила бы дважды.
    ///
    /// Потолок неизвестен (ноль и ниже) — вех нет вовсе: делить счёт не на что.
    /// </summary>
    public sealed class Milestones
    {
        readonly float[] shares;
        readonly int ceiling;

        public Milestones(IReadOnlyList<float> milestoneShares, int ceiling)
        {
            this.ceiling = ceiling;

            if (milestoneShares == null || ceiling <= 0)
            {
                shares = Array.Empty<float>();
                return;
            }

            // Непозитивная доля сработала бы на нулевом счёте в первый же тик, то есть до того,
            // как игрок что-то сделал. Порядок в инспекторе не гарантирован, а срабатывать вехи
            // обязаны снизу вверх — здесь и то и другое приводится к нормальному виду.
            var sorted = new List<float>(milestoneShares.Count);
            foreach (var share in milestoneShares)
                if (share > 0f)
                    sorted.Add(share);

            sorted.Sort();
            shares = sorted.ToArray();
        }

        /// <summary>Пройдена веха: её доля от потолка. Награду выдаёт тот, кто подписался.</summary>
        public event Action<float> Reached;

        /// <summary>Сколько вех у партии. Ноль — потолка нет, бара и наград тоже.</summary>
        public int Count => shares.Length;

        /// <summary>Сколько вех уже пройдено.</summary>
        public int Passed { get; private set; }

        /// <summary>Доля вехи по номеру: бару она нужна, чтобы поставить отметку.</summary>
        public float ShareAt(int index) => shares[index];

        /// <summary>
        /// Счёт изменился. Один обмен способен перешагнуть сразу две вехи — тогда обе и
        /// срабатывают, по очереди снизу вверх, а не одна старшая.
        /// </summary>
        public void Report(int total)
        {
            while (Passed < shares.Length && total >= ceiling * shares[Passed])
            {
                Passed++;
                Reached?.Invoke(shares[Passed - 1]);
            }
        }
    }
}
