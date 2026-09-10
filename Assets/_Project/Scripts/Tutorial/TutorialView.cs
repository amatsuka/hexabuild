using System;
using System.Collections.Generic;
using Game.Grid;
using Game.Storage;
using Game.UI;
using UnityEngine;

namespace Game.Tutorial
{
    /// <summary>
    /// Лицо обучения: подсказка над целью и подсветка самой цели. Подсказка — та же карточка
    /// `UiPanel`, что и попап подтверждения, и живёт в том же слое попапов: она не гаснет
    /// по таймеру, а ждёт, пока игрок сделает шаг. Тапы ловит только «Пропустить» на первой
    /// подсказке — за карточкой кликается поле, ввод обучение не блокирует.
    /// </summary>
    public sealed class TutorialView : MonoBehaviour
    {
        /// <summary>Насколько ободок цели притухает между вспышками: он дышит яркостью.</summary>
        const float HighlightFloor = 0.35f;

        const float HighlightSpeed = 3.2f;

        /// <summary>
        /// Цвет ободка. Пульсирует яркостью, а не альфой: материал поля непрозрачный, и альфа
        /// в нём ничего не значит — она здесь только флаг «подсветка есть».
        /// </summary>
        static readonly Color HighlightColor = new(1f, 0.84f, 0.30f);

        static readonly int[] NoCells = Array.Empty<int>();

        readonly List<HexCoord> highlighted = new();

        TutorialSystem tutorial;
        IReadOnlyDictionary<HexCoord, TileView> tiles;
        StorageView storage;
        HudView hud;

        /// <summary>Какой шаг сейчас на экране: карточка пересобирается только на смене шага.</summary>
        TutorialStep shown = TutorialStep.Done;

        /// <summary>
        /// Слой обучения заводится кодом, как и весь интерфейс проекта: префабов под панели
        /// в проекте нет, а карточку подсказки рисует слой попапов.
        /// </summary>
        public static TutorialView Create(
            TutorialSystem system,
            HudView hudView,
            StorageView storageView,
            IReadOnlyDictionary<HexCoord, TileView> tileViews)
        {
            var view = UiPanel.NewRect("Tutorial", hudView.transform).gameObject.AddComponent<TutorialView>();
            view.tutorial = system;
            view.hud = hudView;
            view.storage = storageView;
            view.tiles = tileViews;

            system.Changed += view.Redraw;
            view.Redraw();
            return view;
        }

        void OnDestroy()
        {
            if (tutorial != null)
                tutorial.Changed -= Redraw;
        }

        /// <summary>Ободок цели дышит: он то разгорается, то притухает.</summary>
        void Update()
        {
            if (highlighted.Count == 0)
                return;

            var glow = Mathf.Lerp(HighlightFloor, 1f, (Mathf.Sin(Time.time * HighlightSpeed) + 1f) * 0.5f);
            var color = HighlightColor * glow;
            color.a = 1f;

            foreach (var coord in highlighted)
                if (tiles.TryGetValue(coord, out var view))
                    view.SetHighlight(color);
        }

        void Redraw()
        {
            ClearTiles();

            if (!tutorial.IsRunning)
            {
                storage.Highlight(NoCells);
                hud.Popups.HideHint();
                shown = TutorialStep.Done;
                return;
            }

            if (tutorial.Aim == TutorialAim.Tiles)
                highlighted.AddRange(tutorial.TargetTiles);

            storage.Highlight(tutorial.Aim == TutorialAim.Cells ? tutorial.TargetCells : NoCells);

            // Цель шага может ездить по складу, а сама карточка стоит на месте: пересобирать её
            // на каждое изменение склада значило бы пересчитывать всплытие по десять раз за шаг.
            if (shown == tutorial.Step)
                return;

            shown = tutorial.Step;
            hud.Popups.ShowHint(TextOf(shown), Anchor(), tutorial.Skippable ? tutorial.Skip : null);
        }

        void ClearTiles()
        {
            foreach (var coord in highlighted)
                if (tiles.TryGetValue(coord, out var view))
                    view.SetHighlight(Color.clear);

            highlighted.Clear();
        }

        /// <summary>
        /// Над чем висит подсказка. Плитка везётся паном камеры, карточки HUD и склад стоят
        /// в канвасе — попапу обе привязки одинаковы.
        /// </summary>
        PopupView.Anchor Anchor()
        {
            switch (tutorial.Aim)
            {
                case TutorialAim.Ceiling:
                    return PopupView.Anchor.On(hud.CeilingCard);
                case TutorialAim.Contract:
                    return PopupView.Anchor.On(hud.ContractCard);
                case TutorialAim.SellButton:
                    return PopupView.Anchor.On(storage.SellRect);
                case TutorialAim.Cells:
                case TutorialAim.Storage:
                    return PopupView.Anchor.On(storage.PanelRect);
                default:
                    return tutorial.TargetTiles.Count > 0
                           && tiles.TryGetValue(tutorial.TargetTiles[0], out var view)
                        ? PopupView.Anchor.OnField(view.transform.position)
                        : PopupView.Anchor.On(storage.PanelRect);
            }
        }

        /// <summary>
        /// Что говорит шаг. Числа в текстах — те же, что в `GameConfig` и в уровне: цена моста,
        /// резерв кнопки, пороги слияния и доли звёзд.
        /// </summary>
        static string TextOf(TutorialStep step) => step switch
        {
            TutorialStep.OpenStone =>
                "Тапни подсвеченную плитку — она покажет цену.\nВторой тап по ней же открывает её",
            TutorialStep.BuildRoad =>
                "Без дороги до Метрополии плитка молчит.\nТапни её ещё раз: щебень ляжет насыпью",
            TutorialStep.WatchDelivery =>
                "Камень едет по дороге на склад.\nПлитка добывает, пока путь до Метрополии цел",
            TutorialStep.Merge =>
                "Три одинаковых — тап по любому из них.\nПятёрка даёт два: копить выгоднее",
            TutorialStep.Convert =>
                "Крафт — единственный источник очков.\nТап по щебню меняет его на очки с множителем",
            TutorialStep.Goal =>
                "Бар наверху — цель партии: столько набрал бот.\nЗвёзды идут на 50, 75 и 100 %",
            TutorialStep.Contract =>
                "Метрополия просит доски, три уже на складе.\nТапни каждую: награда идёт с множителем",
            TutorialStep.CraftBoard =>
                "Доски кончились, а мост их просит.\nОткрой лес и слей три бревна в доску",
            TutorialStep.SellButton =>
                "Кнопка меняет весь крафт на очки разом.\nРезерв она не трогает: 4 щебня и 4 доски",
            TutorialStep.Heat =>
                "Накал — третье слагаемое множителя.\nДержится 2.5 с и утекает за 2: паузу он не прощает",
            TutorialStep.Wall =>
                "Горы не открываются никогда.\nОбходи гряду — за ней река",
            TutorialStep.Bridge =>
                "Река переходится мостом: 2 щебня и 2 доски.\nТапни речную плитку ещё раз",
            _ =>
                "Разгребёшь склад до двух клеток на полном накале — заплатят премию.\n" +
                "Кнопка до двух не дочищает: резерв доскребают руками"
        };
    }
}
