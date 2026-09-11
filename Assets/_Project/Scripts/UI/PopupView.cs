using System;
using System.Collections;
using System.Collections.Generic;
using Game.Economy;
using Game.Storage;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Попапы над тем, с чем игрок работает: прибавка очков над клеткой склада, отказ над
    /// плиткой или клеткой, награда над карточкой контракта и подтверждение открытия плитки —
    /// цена и зелёная галочка. Столбца плашек в углу больше нет: сообщение без места теряло
    /// связь с тем, по чему кликнули, и игрок читал текст, не понимая, к чему он относится.
    ///
    /// Панель попапа — та же карточка `UiPanel`, что и весь интерфейс: попап не отдельный стиль,
    /// а та же стеклянная плашка, только над объектом.
    /// </summary>
    public sealed class PopupView : MonoBehaviour
    {
        /// <summary>Отступ содержимого от кромки попапа.</summary>
        const float Padding = 20f;

        /// <summary>Просвет между низом попапа и самим объектом.</summary>
        const float AnchorGap = 18f;

        const float MessageWidth = 430f;
        const float GainWidth = 470f;
        /// <summary>
        /// Потолок ценника: монета, зазор и цена в четыре знака. Фактическая ширина считается
        /// по самому числу — цена растёт по ходу партии, и ценник ужимается под неё.
        /// </summary>
        const float AskWidth = 190f;
        const float MinHeight = 84f;

        /// <summary>
        /// Плашка прибавки в три строки: прибавка, множитель, его разбор. Ширина — по самой
        /// длинной третьей строке; с накалом (M29) она стала «колония ×4.00 · серия +2.00 ·
        /// накал +1.00», и 360 px её уже не вмещали. Высоты строк — их `preferredHeight`
        /// с запасом. Числа промерены `preferredWidth`/`preferredHeight` в редакторе.
        /// </summary>
        const float GainHeight = 160f;
        const float GainValueHeight = 50f;
        const float GainFactorHeight = 42f;
        const float GainDetailHeight = 28f;
        const float IconSize = 46f;

        /// <summary>Плашка вехи в две строки: «Веха 50%» и что за неё дали. Иконка справа, как у прибавки.</summary>
        const float MilestoneWidth = 300f;
        const float MilestoneHeight = 116f;
        const float MilestoneTitleHeight = 44f;
        const float MilestoneRewardHeight = 38f;

        /// <summary>Плашка премии за чистый склад: те же две строки, но без иконки — иконки у неё нет.</summary>
        const float SweepWidth = 260f;

        /// <summary>
        /// Подсказка обучения: две строки текста и крестик «пропустить шаг» в правом верхнем
        /// углу. Ширина — потолок; высота считается по самому тексту, как у отказа.
        /// </summary>
        const float HintWidth = 520f;
        const float HintFontSize = 26f;

        /// <summary>Кнопка «Дальше» на карточке: ею закрываются шаги, которым делать нечего.</summary>
        const float NextWidth = 200f;
        const float NextHeight = 52f;
        const float NextFontSize = 26f;

        /// <summary>Сколько живёт попап, который никто не закрывает: прибавка, награда, отказ.</summary>
        const float ShowSeconds = 2f;

        const float AppearSeconds = 0.18f;
        const float FadeSeconds = 0.3f;

        /// <summary>С какого масштаба попап всплывает: он вырастает над объектом, а не мигает.</summary>
        const float MinScale = 0.78f;

        /// <summary>
        /// Больше попапов на экране не держим. Каждый привязан к своему объекту, стопкой они
        /// не встают, но при частых кликах экран иначе зарастает плашками.
        /// </summary>
        const int MaxPopups = 4;

        readonly List<Popup> live = new();

        UiTheme theme;
        StorageView icons;
        ScoreMultiplier multiplier;
        Camera fieldCamera;

        Mesh coinMesh;
        Material coinMaterial;
        Vector3 coinAngles;

        /// <summary>Ценник открытия: он один, срока жизни не имеет и ждёт ответа.</summary>
        Popup asking;

        Action confirmed;

        /// <summary>
        /// Подсказка обучения: она тоже одна и тоже не гаснет сама, но живёт вне списка живых —
        /// прибавки и отказы не должны вытеснять её ни числом, ни общей привязкой.
        /// </summary>
        Popup hint;

        /// <summary>«Дальше» на подсказке: единственное место самой карточки, ловящее тап.</summary>
        UiButton next;

        /// <summary>
        /// Плашки прибавки, премии и вехи молчат. Ставится на время обучения: волна продажи
        /// выбрасывает их пачкой, и вместо одной подсказки на экране каша. Отказы не глушатся —
        /// они и есть ответ на действие, а не празднование.
        /// </summary>
        public bool Quiet { get; set; }

        /// <summary>
        /// Висит ли сейчас ценник: пока висит, клик принадлежит ему, а разбирает клик
        /// <c>GameSession</c> — по той же плитке согласие, мимо отмена.
        /// </summary>
        public bool IsAsking => asking != null;

        /// <summary>
        /// Слой попапов поверх HUD. Заводится кодом последним ребёнком, чтобы попап шёл поверх
        /// карточек: весь интерфейс проекта собирается так же, префабов под панели нет.
        /// </summary>
        public static PopupView Create(
            RectTransform parent, UiTheme theme, StorageView icons, ScoreMultiplier multiplier)
        {
            var layer = UiPanel.NewRect("Popups", parent);
            layer.Stretch();

            var view = layer.gameObject.AddComponent<PopupView>();
            view.theme = theme;
            view.icons = icons;
            view.multiplier = multiplier;
            return view;
        }

        /// <summary>Монета для цены открытия: та же модель, что и на карточке очков.</summary>
        public void BindCoin(Mesh mesh, Material material, Vector3 angles)
        {
            coinMesh = mesh;
            coinMaterial = material;
            coinAngles = angles;
        }

        /// <summary>Отказ: красная строка над тем, по чему кликнули.</summary>
        public void ShowMessage(string text, in Anchor anchor)
        {
            if (!anchor.Exists)
                return;

            var popup = Push(anchor, MessageWidth);
            var label = UiText.Label("Text", popup.Rect, theme, 28f, theme.Bad, TextAlignmentOptions.Center)
                .Stretch(Padding, Padding, Padding * 0.6f, Padding * 0.6f);
            label.text = text;

            // Высота — по тексту: отказы бывают и в строку, и в две, а карточка фиксированной
            // высоты либо резала длинный, либо зияла пустотой под коротким.
            label.ForceMeshUpdate();
            popup.Rect.sizeDelta = new Vector2(
                MessageWidth, Mathf.Max(MinHeight, label.preferredHeight + Padding * 1.4f));

            Show(popup, true);
        }

        /// <summary>
        /// Прибавка очков в три строки, как плашка референса: «✓ +30», итоговый множитель
        /// «×1.35» и его разбор «колония ×1.10 · серия +0.25»; справа — иконка того, за что
        /// заплатили. Множитель читается в момент показа: прибавка уже посчитана по нему.
        /// </summary>
        public void ShowGain(int points, ResourceType source, in Anchor anchor)
        {
            if (!anchor.Exists || Quiet)
                return;

            var popup = Push(anchor, GainWidth);
            popup.Rect.sizeDelta = new Vector2(GainWidth, GainHeight);
            var textWidth = GainWidth - Padding * 2f - IconSize - 8f;

            var value = UiText.Bold("Value", popup.Rect, theme, 40f, theme.Good, TextAlignmentOptions.Left);
            Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -Padding),
                new Vector2(textWidth, GainValueHeight));
            value.text = "✓ " + HudFormat.Gain(points);

            var factor = UiText.Bold("Factor", popup.Rect, theme, 34f, theme.Gold, TextAlignmentOptions.Left);
            Place(factor.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -Padding - GainValueHeight),
                new Vector2(textWidth, GainFactorHeight));
            factor.text = HudFormat.Multiplier(multiplier.Total);

            var detail = UiText.Label("Detail", popup.Rect, theme, 18f, theme.Muted, TextAlignmentOptions.Left);
            Place(detail.rectTransform, new Vector2(0f, 1f),
                new Vector2(Padding, -Padding - GainValueHeight - GainFactorHeight),
                new Vector2(textWidth, GainDetailHeight));
            detail.text = $"колония {HudFormat.Multiplier(multiplier.Colony)}" +
                          $" · серия {HudFormat.Bonus(multiplier.Streak)}" +
                          $" · накал {HudFormat.Bonus(multiplier.Heat)}";

            var icon = ResourceIcon.Create("Icon", popup.Rect, IconSize);
            Place((RectTransform)icon.transform, new Vector2(1f, 0.5f), new Vector2(-Padding, 0f),
                new Vector2(IconSize, IconSize));
            icons.ShowIcon(icon, source);

            Show(popup, true);
        }

        /// <summary>
        /// Премия за чистый склад: две строки без иконки — «Чистый склад» и прибавка. Своей
        /// анимации у неё нет, празднует сам склад вспышкой: третий вид всплывающего сообщения
        /// игроку не нужен, а плашка прибавки уже читается как «тебе что-то начислили».
        /// </summary>
        public void ShowSweep(int points, in Anchor anchor)
        {
            if (!anchor.Exists || Quiet)
                return;

            var popup = Push(anchor, SweepWidth);
            popup.Rect.sizeDelta = new Vector2(SweepWidth, MilestoneHeight);
            var textWidth = SweepWidth - Padding * 2f;

            var title = UiText.Bold("Title", popup.Rect, theme, 34f, theme.Gold, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -Padding),
                new Vector2(textWidth, MilestoneTitleHeight));
            title.text = "Чистый склад";

            var reward = UiText.Bold("Reward", popup.Rect, theme, 30f, theme.Good, TextAlignmentOptions.Center);
            Place(reward.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -Padding - MilestoneTitleHeight),
                new Vector2(textWidth, MilestoneRewardHeight));
            reward.text = HudFormat.Gain(points);

            Show(popup, true);
        }

        /// <summary>
        /// Пройдена веха: над баром встаёт плашка в две строки — какая доля потолка взята и
        /// сколько щебня за это дали. Своей анимации у вехи нет, празднование идёт этим попапом
        /// и вспышкой самого бара: третий вид всплывающего сообщения игроку не нужен, а плашка
        /// прибавки уже читается как «тебе что-то начислили».
        /// </summary>
        public void ShowMilestone(float share, int gravel, in Anchor anchor)
        {
            if (!anchor.Exists || Quiet)
                return;

            var popup = Push(anchor, MilestoneWidth);
            popup.Rect.sizeDelta = new Vector2(MilestoneWidth, MilestoneHeight);
            var textWidth = MilestoneWidth - Padding * 2f - IconSize - 8f;

            var title = UiText.Bold("Title", popup.Rect, theme, 34f, theme.Gold, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -Padding),
                new Vector2(textWidth, MilestoneTitleHeight));
            title.text = "Веха " + HudFormat.Percent(share);

            var reward = UiText.Bold("Reward", popup.Rect, theme, 30f, theme.Good, TextAlignmentOptions.Left);
            Place(reward.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -Padding - MilestoneTitleHeight),
                new Vector2(textWidth, MilestoneRewardHeight));
            reward.text = HudFormat.Gain(gravel) + " щебня";

            var icon = ResourceIcon.Create("Icon", popup.Rect, IconSize);
            Place((RectTransform)icon.transform, new Vector2(1f, 0.5f), new Vector2(-Padding, 0f),
                new Vector2(IconSize, IconSize));
            icons.ShowIcon(icon, ResourceType.Gravel);

            Show(popup, true);
        }

        /// <summary>
        /// Открытие плитки подтверждают вторым тапом по ней же, а попап — только ценник: монета
        /// и число над самой плиткой. Кнопки на нём нет, соглашаются в гекс, а не в попап. Он
        /// один на экране и сам не гаснет — его снимает либо согласие, либо клик мимо. Хватает
        /// очков или нет, здесь не спрашивают: цена показана, а отказ придёт от правила, когда
        /// игрок согласится, и встанет на то же место.
        /// </summary>
        public void Ask(int cost, in Anchor anchor, Action accepted)
        {
            if (!anchor.Exists)
                return;

            CancelAsk();

            var popup = Push(anchor, AskWidth);

            var coin = ResourceIcon.Create("Coin", popup.Rect, IconSize);
            Place((RectTransform)coin.transform, new Vector2(0f, 0.5f), new Vector2(Padding, 0f),
                new Vector2(IconSize, IconSize));
            icons.ShowIcon(coin, coinMesh, coinMaterial, coinAngles);

            var price = UiText.Bold("Cost", popup.Rect, theme, 40f, theme.Gold, TextAlignmentOptions.Left);
            price.text = cost.ToString();

            // Ширина — по самому числу, как у отказа по его тексту: цена идёт от двух знаков
            // к четырём, и ценник фиксированной ширины зиял бы пустотой под коротким.
            price.ForceMeshUpdate();
            var digits = Mathf.Ceil(price.preferredWidth);
            Place(price.rectTransform, new Vector2(0f, 0.5f), new Vector2(Padding + IconSize + 8f, 0f),
                new Vector2(digits, 52f));
            popup.Rect.sizeDelta = new Vector2(Padding * 2f + IconSize + 8f + digits, MinHeight);

            asking = popup;
            confirmed = accepted;
            Show(popup, false);
        }

        /// <summary>
        /// Согласие: игрок тапнул по той же плитке ещё раз. Кому принадлежит клик, пока висит
        /// ценник, решает <c>GameSession</c> — попап только выполняет то, о чём спросил.
        /// </summary>
        public void AcceptAsk()
        {
            if (asking == null)
                return;

            var accepted = confirmed;
            CancelAsk();
            accepted?.Invoke();
        }

        /// <summary>Подтверждение снято: попап уходит, ничего не выполнив.</summary>
        public void CancelAsk()
        {
            if (asking != null)
                Remove(asking);
        }

        /// <summary>
        /// Подсказка обучения: та же карточка над той же целью, но без срока жизни — она ждёт,
        /// пока игрок сделает шаг. Своего слоя обучению не нужно: попап и есть «сообщение,
        /// привязанное к объекту», а два одинаковых на вид слоя разошлись бы на первой же правке.
        /// <paramref name="proceed"/> вешает на карточку кнопку «Дальше» — ею закрываются шаги,
        /// которым делать нечего. У шага с действием её нет: он ждёт самого действия.
        /// </summary>
        public void ShowHint(string text, in Anchor anchor, Action proceed, float lift, bool below)
        {
            HideHint();

            if (!anchor.Exists)
                return;

            var popup = NewCard(anchor, HintWidth, theme.Hint);
            popup.Lift = lift;
            popup.Below = below;

            var textWidth = HintWidth - Padding * 2f;

            var label = UiText.Label("Text", popup.Rect, theme, HintFontSize, theme.Text, TextAlignmentOptions.Center);
            Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -Padding * 0.7f),
                new Vector2(textWidth, MinHeight));
            label.text = text;

            // Высота — по тексту, как у отказа: подсказки бывают и в строку, и в три.
            label.ForceMeshUpdate();
            var textHeight = label.preferredHeight;
            label.rectTransform.sizeDelta = new Vector2(textWidth, textHeight);

            var height = textHeight + Padding * 1.4f + (proceed != null ? NextHeight + Padding * 0.6f : 0f);
            popup.Rect.sizeDelta = new Vector2(HintWidth, Mathf.Max(MinHeight, height));

            if (proceed != null)
            {
                next = UiButton.Create(
                    "Next", popup.Rect, theme, theme.ButtonSecondary, "Дальше", NextFontSize, proceed);
                Place(next.Rect, new Vector2(0.5f, 0f), new Vector2(0f, Padding * 0.5f),
                    new Vector2(NextWidth, NextHeight));
            }

            hint = popup;
            Show(popup, false);
        }

        /// <summary>Подсказки больше нет: шаг закрылся, обучение кончилось или партия встала.</summary>
        public void HideHint()
        {
            if (hint == null)
                return;

            next = null;

            if (hint.Rect != null)
                Destroy(hint.Rect.gameObject);

            hint = null;
        }

        /// <summary>Палец лёг на «Дальше»: дальше нажатие разбирать не надо.</summary>
        public bool TryHintPress(Vector2 screenPosition) => next != null && next.TryPress(screenPosition);

        /// <summary>Палец снят. Ненажатая кнопка молчит, поэтому звать можно всегда.</summary>
        public void ReleaseHintPress() => next?.Release();

        /// <summary>Клик попал в «Дальше»: шаг прочитан, обучение идёт к следующему.</summary>
        public bool TryHintClick(Vector2 screenPosition) => next != null && next.TryClick(screenPosition);

        /// <summary>Партия кончилась: над полем не остаётся ничего, финальный экран сам по себе.</summary>
        public void Clear()
        {
            for (var i = live.Count - 1; i >= 0; i--)
                Remove(live[i]);

            HideHint();
        }

        /// <summary>
        /// Место считается каждый кадр: камеру панят, а карточка контракта в это время может
        /// ещё выезжать. Считать один раз значило бы оставить попап висеть в стороне от объекта.
        /// </summary>
        void LateUpdate()
        {
            for (var i = 0; i < live.Count; i++)
                Place(live[i]);

            if (hint != null)
                Place(hint);
        }

        void Place(Popup popup)
        {
            var rect = popup.Rect;
            if (rect == null)
                return;

            rect.position = popup.Below
                ? popup.Where.BottomEdge(FieldCamera)
                : popup.Where.TopEdge(FieldCamera);

            // Сдвиг — в единицах канваса, а не в пикселях экрана: `position` их уже развёл
            // масштабом канваса, и складывать одно с другим нельзя.
            var half = rect.rect.size * 0.5f;
            var step = new Vector2(0f, half.y + AnchorGap + popup.Lift);
            var raised = popup.Below ? rect.anchoredPosition - step : rect.anchoredPosition + step;

            // Попап не уходит за кромку кадра: над плиткой у верхнего края экрана он иначе
            // наполовину срезается.
            var frame = ((RectTransform)rect.parent).rect;
            rect.anchoredPosition = new Vector2(
                Mathf.Clamp(raised.x, frame.xMin + half.x, frame.xMax - half.x),
                Mathf.Clamp(raised.y, frame.yMin + half.y, frame.yMax - half.y));
        }

        Camera FieldCamera => fieldCamera != null ? fieldCamera : fieldCamera = Camera.main;

        /// <summary>Новый попап на месте объекта. Прежний попап того же объекта уходит.</summary>
        Popup Push(in Anchor anchor, float width)
        {
            for (var i = live.Count - 1; i >= 0; i--)
                if (live[i].Where.SameAs(anchor))
                    Remove(live[i]);

            while (live.Count >= MaxPopups)
                Remove(live[0]);

            var popup = NewCard(anchor, width);
            live.Add(popup);
            return popup;
        }

        /// <summary>
        /// Пустая карточка на месте объекта. Списком живых она не учитывается: подсказка
        /// обучения не гаснет по сроку и не должна вытесняться прибавками и отказами.
        /// </summary>
        Popup NewCard(in Anchor anchor, float width) => NewCard(anchor, width, theme.Card);

        Popup NewCard(in Anchor anchor, float width, in UiPanelStyle style)
        {
            var card = UiPanel.Create("Popup", transform, theme, style).rectTransform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(width, MinHeight);

            var popup = new Popup
            {
                Rect = card,
                Fade = card.gameObject.AddComponent<CanvasGroup>(),
                Where = anchor,
            };

            return popup;
        }

        void Remove(Popup popup)
        {
            live.Remove(popup);

            if (popup == asking)
            {
                asking = null;
                confirmed = null;
            }

            if (popup.Rect != null)
                Destroy(popup.Rect.gameObject);
        }

        /// <summary>
        /// Попап встаёт на место и всплывает масштабом. На выключенном слое корутины не
        /// заводятся: тогда он просто стоит, иначе застыл бы прозрачным и навсегда.
        /// </summary>
        void Show(Popup popup, bool transient)
        {
            Place(popup);

            if (!isActiveAndEnabled)
                return;

            StartCoroutine(Appear(popup));
            if (transient)
                StartCoroutine(Expire(popup));
        }

        IEnumerator Appear(Popup popup)
        {
            for (var elapsed = 0f; elapsed < AppearSeconds; elapsed += Time.deltaTime)
            {
                if (popup.Rect == null)
                    yield break;

                var progress = Mathf.Clamp01(elapsed / AppearSeconds);
                popup.Fade.alpha = progress;
                popup.Rect.localScale = Vector3.one * Mathf.Lerp(MinScale, 1f, Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            if (popup.Rect == null)
                yield break;

            popup.Fade.alpha = 1f;
            popup.Rect.localScale = Vector3.one;
        }

        IEnumerator Expire(Popup popup)
        {
            yield return new WaitForSeconds(ShowSeconds);

            for (var elapsed = 0f; elapsed < FadeSeconds; elapsed += Time.deltaTime)
            {
                if (popup.Rect == null)
                    yield break;

                popup.Fade.alpha = 1f - elapsed / FadeSeconds;
                yield return null;
            }

            Remove(popup);
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// К чему привязан попап. Плитка живёт на поле, и её точку двигает пан камеры; клетка
        /// склада и карточка контракта живут в канвасе. Для попапа обе привязки одинаковы: он
        /// каждый кадр спрашивает у привязки её верхнюю кромку в пикселях экрана.
        /// </summary>
        public readonly struct Anchor
        {
            /// <summary>Углы берутся в общий буфер: место считается каждый кадр для каждого попапа.</summary>
            static readonly Vector3[] Corners = new Vector3[4];

            readonly Vector3 fieldPoint;
            readonly RectTransform card;
            readonly bool onField;

            Anchor(Vector3 fieldPoint, RectTransform card, bool onField)
            {
                this.fieldPoint = fieldPoint;
                this.card = card;
                this.onField = onField;
            }

            /// <summary>Точка поля: крышка плитки, по которой кликнули.</summary>
            public static Anchor OnField(Vector3 worldPoint) => new(worldPoint, null, true);

            /// <summary>Прямоугольник интерфейса: клетка склада, карточка контракта.</summary>
            public static Anchor On(RectTransform rect) => new(Vector3.zero, rect, false);

            /// <summary>Привязки нет: попап показывать не над чем, и его не будет.</summary>
            public bool Exists => onField || card != null;

            /// <summary>
            /// Верхняя кромка объекта в пикселях экрана. У карточки берутся углы, а не
            /// `position`: пивот карточек HUD стоит в углу, и `position` у них — угол, а не центр.
            /// </summary>
            public Vector3 TopEdge(Camera fieldCamera)
            {
                if (!onField)
                {
                    card.GetWorldCorners(Corners);
                    return (Corners[1] + Corners[2]) * 0.5f;
                }

                if (fieldCamera == null)
                    return Vector3.zero;

                // Глубина камеры обнуляется: в экранном канвасе она ничего не значит, зато
                // уезжает в `position` попапа и уводит его из плоскости интерфейса.
                var screenPoint = fieldCamera.WorldToScreenPoint(fieldPoint);
                screenPoint.z = 0f;
                return screenPoint;
            }

            /// <summary>
            /// Нижняя кромка объекта в пикселях экрана: под ней встают подсказки, которым нельзя
            /// вставать над целью, — карточки HUD прижаты к верху кадра, и попап над ними кламп
            /// кадра всё равно опустил бы им на голову. У точки поля кромки нет: она и есть точка,
            /// и место над плиткой задаёт сам вызывающий, сдвигая точку в мировых координатах.
            /// </summary>
            public Vector3 BottomEdge(Camera fieldCamera)
            {
                if (onField)
                    return TopEdge(fieldCamera);

                card.GetWorldCorners(Corners);
                return (Corners[0] + Corners[3]) * 0.5f;
            }

            /// <summary>Тот же объект: попап на нём заменяется, а не копится стопкой.</summary>
            public bool SameAs(in Anchor other) =>
                onField == other.onField && card == other.card
                && (!onField || fieldPoint == other.fieldPoint);
        }

        /// <summary>Живой попап: карточка, её прозрачность и то, над чем она висит.</summary>
        sealed class Popup
        {
            public RectTransform Rect;
            public CanvasGroup Fade;
            public Anchor Where;

            /// <summary>Насколько выше обычного стоит попап: подсказке нужно обойти саму цель.</summary>
            public float Lift;

            /// <summary>Попап стоит под целью, а не над ней.</summary>
            public bool Below;
        }
    }
}
