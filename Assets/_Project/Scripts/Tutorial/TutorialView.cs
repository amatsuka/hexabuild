using System;
using System.Collections.Generic;
using Game.Grid;
using Game.Storage;
using Game.UI;
using UnityEngine;

namespace Game.Tutorial
{
    /// <summary>
    /// Лицо обучения: подсказка над целью, подсветка самой цели и затенение всего, что на этом
    /// шаге закрыто. Подсказка — та же карточка
    /// `UiPanel`, что и попап подтверждения, и живёт в том же слое попапов: она не гаснет
    /// по таймеру, а ждёт, пока игрок сделает шаг. Тапы на самой карточке ловит только крестик
    /// в её углу; всё, что шаг закрыл, гасится затенением и тапа не принимает.
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

        /// <summary>
        /// Куда подсказке смотреть над плиткой: дальний угол крышки плюс небольшая высота под
        /// модельки месторождений, которые на ней стоят. Сдвиг задан в мире, а не в пикселях,
        /// намеренно — экранный размер гекса меняется зумом, и постоянный отступ в пикселях
        /// ложился бы плашкой на плитку, как только игрок приблизит камеру.
        /// </summary>
        static readonly Vector3 TileHintOffset = new(0f, 0.3f, HexCoord.Size);

        /// <summary>
        /// Насколько выше обычного встаёт подсказка про склад. Над панелью склада живут число
        /// множителя (70 px), кнопка продажи и кнопка «Пропустить» над ней: подсказка обязана
        /// пройти над всеми тремя, иначе она садится ровно на накал, о котором сама и говорит.
        /// Замер кадра: полоса кончается на 460, подсказка с этим числом встаёт на 472.
        /// </summary>
        const float StorageLift = 110f;

        /// <summary>
        /// Кнопка «Пропустить» над складом: она снимает всё обучение целиком, в отличие от
        /// крестика на самой подсказке, который пропускает один шаг. Стоит справа над складом,
        /// **над** кнопкой продажи: место у правого края уже занято ею, и делить его нельзя —
        /// с девятого шага обе на экране одновременно.
        /// </summary>
        const float SkipWidth = 200f;
        const float SkipHeight = 50f;
        const float SkipGap = 10f;
        const float SkipFontSize = 26f;

        static readonly int[] NoCells = Array.Empty<int>();

        readonly List<HexCoord> highlighted = new();

        TutorialSystem tutorial;
        IReadOnlyDictionary<HexCoord, TileView> tiles;
        StorageView storage;
        HudView hud;
        UiButton skip;

        /// <summary>Шаг, чья карточка сейчас на экране.</summary>
        TutorialStep shown;

        /// <summary>Есть ли карточка на экране. Шаг может идти и без неё — пока цели негде встать.</summary>
        bool cardUp;

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

            view.BuildSkip();
            system.Changed += view.Redraw;
            view.Redraw();
            return view;
        }

        /// <summary>
        /// Кнопка на экране: обучение идёт, а партия не встала. На паузе и после конца партии
        /// её быть не должно — там же пропадает и кнопка продажи, под которой она стоит.
        /// </summary>
        public void ShowSkip(bool allowed)
        {
            if (skip == null)
                return;

            var visible = allowed && tutorial.IsRunning;
            if (skip.Visible != visible)
                skip.Visible = visible;
        }

        /// <summary>Палец лёг на «Пропустить»: дальше нажатие разбирать не надо.</summary>
        public bool TrySkipPress(Vector2 screenPosition) => skip != null && skip.TryPress(screenPosition);

        /// <summary>Палец снят. Ненажатая кнопка молчит, поэтому звать можно всегда.</summary>
        public void ReleaseSkipPress() => skip?.Release();

        /// <summary>Клик попал в «Пропустить»: обучение снимается целиком.</summary>
        public bool TrySkipClick(Vector2 screenPosition) => skip != null && skip.TryClick(screenPosition);

        /// <summary>
        /// Кнопка живёт на складе, а не на этом слое: она встаёт над кнопкой продажи и обязана
        /// ездить вместе со складом, как ездят число множителя и полоса утечки.
        /// </summary>
        void BuildSkip()
        {
            var sell = storage.SellRect;
            if (sell == null)
                return;

            skip = UiButton.Create(
                "SkipTutorial", sell.parent, storage.Theme, storage.Theme.ButtonSecondary,
                "Пропустить обучение", SkipFontSize, tutorial.Skip);

            var rect = skip.Rect;
            rect.anchorMin = rect.anchorMax = sell.anchorMin;
            rect.pivot = sell.pivot;
            rect.sizeDelta = new Vector2(SkipWidth, SkipHeight);
            rect.anchoredPosition = sell.anchoredPosition + new Vector2(0f, sell.sizeDelta.y + SkipGap);
        }

        void OnDestroy()
        {
            if (tutorial != null)
                tutorial.Changed -= Redraw;
        }

        void Update()
        {
            Pulse();

            // Цель шага может прийти позже самого шага: кнопка «Продать всё» приходит, когда
            // на складе набралось сверх резерва, а карточка контракта выезжает анимацией.
            // Подсказка ждёт свою цель и встаёт вместе с ней — висеть над пустым местом ей нечего.
            if (tutorial.IsRunning && !cardUp)
                TryShowCard();
        }

        /// <summary>Ободок цели дышит: он то разгорается, то притухает.</summary>
        void Pulse()
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
                Restrict();
                HideCard();
                return;
            }

            if (tutorial.Aim == TutorialAim.Tiles)
                highlighted.AddRange(tutorial.TargetTiles);

            storage.Highlight(tutorial.Aim == TutorialAim.Cells ? tutorial.TargetCells : NoCells);
            Restrict();

            // Цель шага может ездить по складу, а сама карточка стоит на месте: пересобирать её
            // на каждое изменение склада значило бы пересчитывать всплытие по десять раз за шаг.
            if (cardUp && shown != tutorial.Step)
                HideCard();

            TryShowCard();
        }

        void TryShowCard()
        {
            if (cardUp || !AimIsOnScreen())
                return;

            shown = tutorial.Step;
            cardUp = true;
            hud.Popups.ShowHint(
                TextOf(shown),
                Anchor(),
                tutorial.Waits(TutorialTrigger.Next) ? tutorial.Next : null,
                Lift(),
                Below());
        }

        void HideCard()
        {
            hud.Popups.HideHint();
            cardUp = false;
        }

        /// <summary>Цель шага уже на экране: подсказке есть над чем встать.</summary>
        bool AimIsOnScreen() => tutorial.Aim switch
        {
            TutorialAim.Contract => IsShown(hud.ContractCard),
            TutorialAim.Ceiling => IsShown(hud.CeilingCard),
            TutorialAim.Tiles => tutorial.TargetTiles.Count > 0 && tiles.ContainsKey(tutorial.TargetTiles[0]),
            _ => true
        };

        static bool IsShown(RectTransform rect) => rect != null && rect.gameObject.activeInHierarchy;

        /// <summary>Насколько подсказка поднимается над целью сверх обычного зазора попапа.</summary>
        float Lift() =>
            tutorial.Aim is TutorialAim.Cells or TutorialAim.Storage or TutorialAim.SellButton
                ? StorageLift
                : 0f;

        /// <summary>
        /// Карточки бара и контракта прижаты к верху кадра, и подсказка над ними всё равно легла
        /// бы им на голову — кламп кадра опустил бы её обратно. Под ними места сколько угодно.
        /// </summary>
        bool Below() => tutorial.Aim is TutorialAim.Ceiling or TutorialAim.Contract;

        /// <summary>
        /// Затенить всё, что шаг закрыл. Плитку гасит её собственный шейдер состояния, клетку
        /// склада — плёнка поверх: обучение не заводит ни своего слоя, ни своего затемнения кадра.
        /// </summary>
        void Restrict()
        {
            var running = tutorial.IsRunning;

            foreach (var pair in tiles)
                pair.Value.SetDimmed(running && !tutorial.AllowsTile(pair.Key));

            storage.Restrict(tutorial.AllowedCells, running);
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
                // Подсказка про кнопку висит над складом, а не над самой кнопкой: до её прихода
                // игрок копит крафт, и всё это время ему надо что-то читать. Кнопка приходит
                // туда же, прямо над складом.
                case TutorialAim.SellButton:
                case TutorialAim.Cells:
                case TutorialAim.Storage:
                    return PopupView.Anchor.On(storage.PanelRect);
                default:
                    return tutorial.TargetTiles.Count > 0
                           && tiles.TryGetValue(tutorial.TargetTiles[0], out var view)
                        ? PopupView.Anchor.OnField(view.transform.position + TileHintOffset)
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
                "Камень поехал по дороге на склад.\nПлитка добывает сама, пока путь до Метрополии цел",
            TutorialStep.Merge =>
                "Три одинаковых — тап по любому из них.\nПятёрка даёт два: копить выгоднее",
            TutorialStep.Convert =>
                "Крафт — единственный источник очков.\nТапни щебень: он обменяется на очки с множителем",
            TutorialStep.Goal =>
                "Бар наверху — цель партии: столько набрал бот.\nЗвёзды идут на 50, 75 и 100 %",
            TutorialStep.Contract =>
                "Метрополия просит доски, три уже на складе.\nТапни каждую: награда идёт сверх обычной цены",
            TutorialStep.OpenWood =>
                "Доски кончились, а мост их попросит.\nОткрой лес и проведи к нему дорогу",
            TutorialStep.CraftBoard =>
                "Три бревна дают доску — тапни любое из них",
            TutorialStep.Wall =>
                "Впереди гряда. Горы не открываются никогда:\nих обходят, а не пробивают",
            TutorialStep.Bypass =>
                "Проход слева. Открой плитку и проведи дорогу",
            TutorialStep.Bridge =>
                "За проходом река. Мост стоит 2 щебня и 2 доски:\nоткрой речную плитку и тапни ещё раз",
            TutorialStep.SellButton =>
                "Копи крафт — кнопка «Продать всё» придёт сама.\nОна обменяет всё разом, кроме 4 щебня и 4 досок",
            TutorialStep.Heat =>
                "Накал — третье слагаемое множителя.\nДержится 2.5 с и утекает за 2: паузу он не прощает",
            _ =>
                "Разгребёшь склад до двух клеток на полном накале — заплатят премию.\n" +
                "Дальше поле твоё: открывай, строй, меняй"
        };
    }
}
