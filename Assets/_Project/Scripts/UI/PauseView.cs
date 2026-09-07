using System;
using Game.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Пауза партии: круглая кнопка-шестерёнка, всегда видимая рядом со складом, и скрытая до
    /// тапа карточка с тремя кнопками. Та же карточка `UiPanel`, что у финального экрана
    /// (`GameOverView`), — банер, кнопки, ручной hit-test по прямоугольнику, без `EventSystem`.
    ///
    /// В отличие от `GameOverView` эта карточка не разовая: закрывается «Продолжить» и открывается
    /// снова тем же тапом по шестерёнке — партия без этого не имела бы пути назад из паузы.
    /// </summary>
    public sealed class PauseView : MonoBehaviour
    {
        const float GearDiameter = 108f;
        const float GearMargin = 24f;
        const float GearIconSize = 60f;

        const float BannerWidth = 760f;
        const float BannerHeight = 170f;
        const float ButtonWidth = 620f;
        const float ButtonHeight = 130f;
        const float ButtonGap = 24f;

        [SerializeField] UiTheme theme = new();
        [Tooltip("Затемнение под карточкой паузы")]
        [SerializeField] Color backdropColor = new(0.03f, 0.05f, 0.09f, 0.82f);
        [Tooltip("Материал шестерёнки: сам меш строит `GearMesh` кодом")]
        [SerializeField] Material gearMaterial;
        [SerializeField] Vector3 gearAngles = new(-14f, 24f, 0f);

        StorageView storage;
        UiPanelGraphic gearPanel;
        RectTransform gearRect;
        GameObject cardRoot;
        UiButton resumeButton;
        UiButton restartButton;
        UiButton exitButton;

        /// <summary>Игрок просит рестарт той же карты — тем же путём, что «Повторить карту».</summary>
        public event Action RestartRequested;

        /// <summary>Игрок просит выйти в главное меню.</summary>
        public event Action ExitRequested;

        /// <summary>Карточка паузы открыта: поле и камера должны стоять.</summary>
        public bool IsOpen => cardRoot != null && cardRoot.activeSelf;

        /// <summary>Строит шестерёнку и (скрытую) карточку. Вызывается один раз из `GameSession.Awake`.</summary>
        public void Bind(StorageView storageView)
        {
            storage = storageView;
            ((RectTransform)transform).Stretch();
            BuildGearButton();
            BuildCard();
            cardRoot.SetActive(false);
        }

        /// <summary>
        /// Клик по шестерёнке переключает паузу; пока она открыта, клик разбирают только её
        /// кнопки — мимо них он гаснет здесь и на поле не уходит, как у `GameOverView`.
        /// </summary>
        public bool HandleClick(Vector2 screenPosition)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(gearRect, screenPosition))
            {
                SetOpen(!IsOpen);
                return true;
            }

            if (!IsOpen)
                return false;

            if (!resumeButton.TryClick(screenPosition) && !restartButton.TryClick(screenPosition))
                exitButton.TryClick(screenPosition);

            return true;
        }

        /// <summary>
        /// Нажатие разбирается тем же порядком, что и клик, и с тем же ответом: true значит,
        /// что нажатие принадлежит паузе и полю под карточкой не достаётся.
        /// </summary>
        public bool HandlePress(Vector2 screenPosition)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(gearRect, screenPosition))
            {
                PressPulse.HoldCard(gearPanel);
                return true;
            }

            if (!IsOpen)
                return false;

            if (!resumeButton.TryPress(screenPosition) && !restartButton.TryPress(screenPosition))
                exitButton.TryPress(screenPosition);

            return true;
        }

        /// <summary>Палец снят. Ненажатое молчит, поэтому отпускаем всё разом, не разбирая.</summary>
        public void ReleasePress()
        {
            PressPulse.Release(gearPanel);
            resumeButton.Release();
            restartButton.Release();
            exitButton.Release();
        }

        void BuildGearButton()
        {
            gearPanel = UiPanel.Create("Gear", transform, theme, theme.RoundButton(GearDiameter));
            gearRect = gearPanel.rectTransform;
            Place(gearRect, new Vector2(1f, 0f), new Vector2(-GearMargin, GearMargin),
                new Vector2(GearDiameter, GearDiameter));

            var icon = CreateIcon("GearIcon", gearRect, GearIconSize);
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(GearIconSize, GearIconSize));
            storage.ShowIcon(icon, GearMesh.Shared, gearMaterial, gearAngles);
        }

        void BuildCard()
        {
            cardRoot = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)cardRoot.transform;
            rect.SetParent(transform, false);
            rect.Stretch();
            cardRoot.GetComponent<Image>().color = backdropColor;

            var banner = UiPanel.Create("Banner", rect, theme).rectTransform;
            Place(banner, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(BannerWidth, BannerHeight));

            var title = UiText.Bold("Title", banner, theme, 64f, theme.Gold, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BannerWidth - 56f, 96f));
            title.text = "Пауза";

            var y = 60f;
            resumeButton = UiButton.Create("Resume", rect, theme, theme.ButtonPrimary, "Продолжить", 42f, Close);
            Place(resumeButton.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));
            y -= ButtonHeight + ButtonGap;

            restartButton = UiButton.Create("Restart", rect, theme, theme.ButtonSecondary, "Рестарт карты", 40f,
                () => Raise(RestartRequested));
            Place(restartButton.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));
            y -= ButtonHeight + ButtonGap;

            exitButton = UiButton.Create("Exit", rect, theme, theme.ButtonSecondary, "Выход в главное меню", 36f,
                () => Raise(ExitRequested));
            Place(exitButton.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));
        }

        void SetOpen(bool open) => cardRoot.SetActive(open);

        void Close() => SetOpen(false);

        /// <summary>
        /// Рестарт и выход закрывают карточку перед тем, как перезагрузить сцену: без этого
        /// следующая партия (или меню) стартовала бы с открытой паузой поверх себя.
        /// </summary>
        void Raise(Action requested)
        {
            Close();
            requested?.Invoke();
        }

        ResourceIcon CreateIcon(string name, Transform parent, float size)
        {
            var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ResourceIcon));
            var rect = (RectTransform)created.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(size, size);

            var icon = created.GetComponent<ResourceIcon>();
            icon.raycastTarget = false;
            return icon;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
