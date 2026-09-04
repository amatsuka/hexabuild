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
        const float GainWidth = 236f;
        const float AskWidth = 232f;
        const float MinHeight = 84f;
        const float IconSize = 46f;
        const float ConfirmSize = 60f;

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
        Camera fieldCamera;

        Mesh coinMesh;
        Material coinMaterial;
        Vector3 coinAngles;

        /// <summary>Попап подтверждения: он один, срока жизни не имеет и ждёт клика.</summary>
        Popup asking;

        UiButton confirm;
        Action confirmed;

        /// <summary>Ждём ли мы сейчас подтверждения: пока ждём, клик принадлежит попапу.</summary>
        public bool IsAsking => asking != null;

        /// <summary>
        /// Слой попапов поверх HUD. Заводится кодом последним ребёнком, чтобы попап шёл поверх
        /// карточек: весь интерфейс проекта собирается так же, префабов под панели нет.
        /// </summary>
        public static PopupView Create(RectTransform parent, UiTheme theme, StorageView icons)
        {
            var layer = UiPanel.NewRect("Popups", parent);
            layer.Stretch();

            var view = layer.gameObject.AddComponent<PopupView>();
            view.theme = theme;
            view.icons = icons;
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

        /// <summary>Прибавка очков: «✓ +30» и иконка того, за что заплатили.</summary>
        public void ShowGain(int points, ResourceType source, in Anchor anchor)
        {
            if (!anchor.Exists)
                return;

            var popup = Push(anchor, GainWidth);

            var value = UiText.Bold("Value", popup.Rect, theme, 40f, theme.Good, TextAlignmentOptions.Left);
            Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(Padding, 0f),
                new Vector2(GainWidth - Padding * 2f - IconSize - 8f, 52f));
            value.text = "✓ " + HudFormat.Gain(points);

            var icon = ResourceIcon.Create("Icon", popup.Rect, IconSize);
            Place((RectTransform)icon.transform, new Vector2(1f, 0.5f), new Vector2(-Padding, 0f),
                new Vector2(IconSize, IconSize));
            icons.ShowIcon(icon, source);

            Show(popup, true);
        }

        /// <summary>
        /// Открытие плитки подтверждают: над самой плиткой встаёт попап с ценой и зелёной
        /// галочкой. Он один на экране и сам не гаснет — его закрывает либо галочка, либо клик
        /// мимо. Хватает очков или нет, здесь не спрашивают: цена показана, а отказ придёт
        /// от правила, когда игрок согласится, и встанет на то же место.
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
            Place(price.rectTransform, new Vector2(0f, 0.5f), new Vector2(Padding + IconSize + 8f, 0f),
                new Vector2(AskWidth - Padding * 2f - IconSize - ConfirmSize - 16f, 52f));
            price.text = cost.ToString();

            // Кнопка плотная и зелёная: стеклом она не читается нажимаемой, а на стекле попапа
            // зелёная галочка без подложки тонет в карте, которая сквозь него видна.
            confirm = UiButton.Create("Confirm", popup.Rect, theme, theme.ButtonConfirm, "✓", 40f, Accept);
            Place(confirm.Rect, new Vector2(1f, 0.5f), new Vector2(-Padding * 0.7f, 0f),
                new Vector2(ConfirmSize, ConfirmSize));

            asking = popup;
            confirmed = accepted;
            Show(popup, false);
        }

        /// <summary>
        /// Клик, пока висит подтверждение, принадлежит ему: по галочке — согласие, мимо — отмена.
        /// Мимо клик отменой и остаётся: выполнить заодно то, по чему попали, значило бы открыть
        /// соседнюю плитку тем же тапом, которым игрок передумал.
        /// </summary>
        public bool TryClick(Vector2 screenPosition)
        {
            if (asking == null)
                return false;

            if (!confirm.TryClick(screenPosition))
                CancelAsk();

            return true;
        }

        /// <summary>Подтверждение снято: попап уходит, ничего не выполнив.</summary>
        public void CancelAsk()
        {
            if (asking != null)
                Remove(asking);
        }

        /// <summary>Партия кончилась: над полем не остаётся ничего, финальный экран сам по себе.</summary>
        public void Clear()
        {
            for (var i = live.Count - 1; i >= 0; i--)
                Remove(live[i]);
        }

        void Accept()
        {
            var accepted = confirmed;
            CancelAsk();
            accepted?.Invoke();
        }

        /// <summary>
        /// Место считается каждый кадр: камеру панят, а карточка контракта в это время может
        /// ещё выезжать. Считать один раз значило бы оставить попап висеть в стороне от объекта.
        /// </summary>
        void LateUpdate()
        {
            for (var i = 0; i < live.Count; i++)
                Place(live[i]);
        }

        void Place(Popup popup)
        {
            var rect = popup.Rect;
            if (rect == null)
                return;

            rect.position = popup.Where.TopEdge(FieldCamera);

            // Сдвиг вверх — в единицах канваса, а не в пикселях экрана: `position` их уже
            // развёл масштабом канваса, и складывать одно с другим нельзя.
            var half = rect.rect.size * 0.5f;
            var raised = rect.anchoredPosition + new Vector2(0f, half.y + AnchorGap);

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

            var card = UiPanel.Create("Popup", transform, theme).rectTransform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(width, MinHeight);

            var popup = new Popup
            {
                Rect = card,
                Fade = card.gameObject.AddComponent<CanvasGroup>(),
                Where = anchor,
            };

            live.Add(popup);
            return popup;
        }

        void Remove(Popup popup)
        {
            live.Remove(popup);

            if (popup == asking)
            {
                asking = null;
                confirm = null;
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
        }
    }
}
