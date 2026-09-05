using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Главное меню: первое, что видит игрок. Та же карточка `UiPanel`, что и финальный экран
    /// (`GameOverView`) — банер и кнопки, — но здесь она живёт сама по себе: партии ещё нет,
    /// и знать о ней некому. Экран строит себя в `Awake` и сам же читает клики, а не ждёт,
    /// когда его позовёт `GameSession` — до выбора карты объекта `GameSession` в сцене нет.
    ///
    /// Выбор карты включает `gameRoot` (объект `GameSession`) и гасит себя; кнопка «Выход»
    /// компилируется только вне веб-сборки — `Application.Quit()` в WebGL ничего не делает,
    /// и мёртвая кнопка хуже её отсутствия.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class MainMenuView : MonoBehaviour
    {
        /// <summary>С запасом ниже `int.MaxValue` (10 цифр), чтобы `int.Parse` не переполнился.</summary>
        const int MaxSeedDigits = 9;

        const float BannerWidth = 880f;
        const float BannerHeight = 200f;
        const float ButtonWidth = 560f;
        const float ButtonHeight = 130f;
        const float ButtonGap = 26f;

        const float KeypadWidth = 546f;
        const float KeySize = 150f;
        const float KeyGap = 18f;
        const float DisplayHeight = 100f;
        const float StartButtonHeight = 120f;

        [SerializeField] UiTheme theme = new();
        [Tooltip("Затемнение под меню: поля ещё нет, экран стоит на пустой сцене")]
        [SerializeField] Color backdropColor = new(0.03f, 0.05f, 0.09f, 0.92f);
        [Tooltip("Объект партии (`GameSession`): включается, когда игрок выбрал карту")]
        [SerializeField] GameObject gameRoot;
        [SerializeField] GameInput input;

        readonly StringBuilder digits = new();

        GameObject mainButtonsRoot;
        GameObject keypadRoot;
        TextMeshProUGUI digitsDisplay;
        UiButton[] mainButtons;
        UiButton[] keypadButtons;

        void Awake()
        {
            // «Рестарт карты»/«Новая карта» с финального экрана или из паузы тоже перезагружают
            // сцену — сид уже выбран, и показывать меню поверх него незачем.
            if (SessionSeed.SkipMenu)
            {
                SessionSeed.SkipMenu = false;
                gameObject.SetActive(false);
                gameRoot.SetActive(true);
                return;
            }

            GetComponent<Image>().color = backdropColor;
            BuildBanner();
            mainButtonsRoot = BuildMainButtons();
            keypadRoot = BuildKeypad();
            keypadRoot.SetActive(false);
        }

        void OnEnable() => input.Clicked += HandleClick;

        void OnDisable() => input.Clicked -= HandleClick;

        /// <summary>
        /// Единственный подписчик на клик, пока партии нет: разбирает его сам, попаданием
        /// в прямоугольник, — тот же приём, что у `GameOverView.HandleClick`.
        /// </summary>
        void HandleClick(Vector2 screenPosition)
        {
            var buttons = keypadRoot.activeSelf ? keypadButtons : mainButtons;
            foreach (var button in buttons)
                if (button.TryClick(screenPosition))
                    return;
        }

        void BuildBanner()
        {
            var banner = UiPanel.Create("Banner", transform, theme).rectTransform;
            Place(banner, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(BannerWidth, BannerHeight));

            var title = UiText.Bold("Title", banner, theme, 76f, theme.Gold, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f),
                new Vector2(BannerWidth - 56f, 96f));
            title.text = "Hex Colony";

            var subtitle = UiText.Label("Subtitle", banner, theme, 34f, theme.Muted, TextAlignmentOptions.Center);
            Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -134f),
                new Vector2(BannerWidth - 56f, 48f));
            subtitle.text = "Новая партия";
        }

        GameObject BuildMainButtons()
        {
            var root = UiPanel.NewRect("MainButtons", transform);
            Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ButtonWidth, ButtonHeight));

            var buttons = new List<UiButton>(3);
            var y = ButtonHeight + ButtonGap;

            var random = UiButton.Create("Random", root, theme, theme.ButtonPrimary,
                "Случайная карта", 40f, StartRandom);
            Place(random.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));
            buttons.Add(random);
            y -= ButtonHeight + ButtonGap;

            var seed = UiButton.Create("Seed", root, theme, theme.ButtonSecondary,
                "Ввести сид", 40f, OpenKeypad);
            Place(seed.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));
            buttons.Add(seed);
            y -= ButtonHeight + ButtonGap;

            // WebGL-сборка публикуется на gh-pages: `Application.Quit()` там не делает ничего,
            // и молчаливо бездействующая кнопка хуже её отсутствия. В редакторе и на десктопе
            // кнопка остаётся — там она работает.
#if !UNITY_WEBGL || UNITY_EDITOR
            var quit = UiButton.Create("Quit", root, theme, theme.ButtonSecondary, "Выход", 40f, Quit);
            Place(quit.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));
            buttons.Add(quit);
#endif

            mainButtons = buttons.ToArray();
            return root.gameObject;
        }

        /// <summary>
        /// Ввод сида: цифровая клавиатура вместо текстового поля — в проекте нет `EventSystem`
        /// и системной клавиатуры ему взяться неоткуда, а телефонный ввод тапами по цифрам
        /// тому же портретному экрану подходит не хуже.
        /// </summary>
        GameObject BuildKeypad()
        {
            var root = UiPanel.NewRect("Keypad", transform);
            Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(KeypadWidth, 1f));

            var display = UiPanel.Create("Display", root, theme, theme.SlotEmpty).rectTransform;
            Place(display, new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(KeypadWidth, DisplayHeight));

            digitsDisplay = UiText.Bold("Value", display, theme, 56f, theme.Text, TextAlignmentOptions.Center);
            digitsDisplay.Stretch(16f, 16f, 8f, 8f);

            string[,] keys =
            {
                { "1", "2", "3" },
                { "4", "5", "6" },
                { "7", "8", "9" },
                { "Назад", "0", "<" }
            };

            var colOffset = KeySize + KeyGap;
            var buttons = new List<UiButton>(11);

            for (var row = 0; row < 4; row++)
            {
                var rowY = 280f - row * (KeySize + KeyGap);
                for (var col = 0; col < 3; col++)
                {
                    var label = keys[row, col];
                    // «Назад» — единственная многобуквенная подпись на клавиатуре: на её ширину
                    // размер остальных цифр не годится, иначе она уходит в перенос строки.
                    var fontSize = label.Length > 1 ? 26f : 44f;
                    var button = UiButton.Create($"Key {label}", root, theme, theme.ButtonSecondary, label, fontSize,
                        KeyAction(label));
                    Place(button.Rect, new Vector2(0.5f, 0.5f), new Vector2((col - 1) * colOffset, rowY),
                        new Vector2(KeySize, KeySize));
                    buttons.Add(button);
                }
            }

            var start = UiButton.Create("Start", root, theme, theme.ButtonPrimary, "Начать", 44f, StartWithSeed);
            Place(start.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, -380f),
                new Vector2(KeypadWidth, StartButtonHeight));
            buttons.Add(start);

            keypadButtons = buttons.ToArray();
            return root.gameObject;
        }

        Action KeyAction(string label) => label switch
        {
            "Назад" => CloseKeypad,
            "<" => Backspace,
            _ => () => AppendDigit(label[0])
        };

        void OpenKeypad()
        {
            digits.Clear();
            UpdateDisplay();
            mainButtonsRoot.SetActive(false);
            keypadRoot.SetActive(true);
        }

        void CloseKeypad()
        {
            keypadRoot.SetActive(false);
            mainButtonsRoot.SetActive(true);
        }

        void AppendDigit(char digit)
        {
            if (digits.Length >= MaxSeedDigits)
                return;

            digits.Append(digit);
            UpdateDisplay();
        }

        void Backspace()
        {
            if (digits.Length > 0)
                digits.Length -= 1;

            UpdateDisplay();
        }

        void UpdateDisplay() => digitsDisplay.text = digits.Length > 0 ? digits.ToString() : "— — —";

        /// <summary>«Случайная карта» обязана значить это буквально, а не «сид из конфига».</summary>
        void StartRandom()
        {
            SessionSeed.Renew();
            BeginGame();
        }

        /// <summary>Пустой ввод или один ноль — сигнал «сид случайный» в `GameConfig`, а не сид.</summary>
        void StartWithSeed()
        {
            if (digits.Length == 0)
                return;

            var seed = int.Parse(digits.ToString());
            if (seed == 0)
                return;

            SessionSeed.Repeat(seed);
            BeginGame();
        }

        /// <summary>
        /// Гасим себя раньше, чем включаем партию: `SetActive(false)` синхронно отписывает
        /// этот экран от `input.Clicked` до того, как `GameSession.OnEnable` на него подпишется.
        /// </summary>
        void BeginGame()
        {
            gameObject.SetActive(false);
            gameRoot.SetActive(true);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
