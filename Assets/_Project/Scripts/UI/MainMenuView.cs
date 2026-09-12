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

        /// <summary>Фон меню лежит в `Resources` рядом со шрифтами и грузится так же.</summary>
        const string BackgroundAsset = "main_menu_bg";

        const float BannerWidth = 880f;
        const float BannerHeight = 200f;
        const float ButtonWidth = 560f;
        const float ButtonHeight = 130f;
        const float ButtonGap = 26f;

        const float KeypadWidth = 546f;
        const float LevelSize = 150f;
        const float LevelGap = 22f;
        const int LevelColumns = 3;
        const float LevelStarSize = 26f;
        const float LevelStarGap = 6f;
        const float KeySize = 150f;
        const float KeyGap = 18f;
        const float DisplayHeight = 100f;
        const float StartButtonHeight = 120f;

        [SerializeField] UiTheme theme = new();
        [Tooltip("Затемнение под меню: поля ещё нет, экран стоит на пустой сцене")]
        [SerializeField] Color backdropColor = new(0.03f, 0.05f, 0.09f, 0.92f);
        [Tooltip("Затемнение поверх фоновой картинки: под ним читаются банер и подписи кнопок")]
        [SerializeField] Color backgroundScrim = new(0.03f, 0.06f, 0.11f, 0.55f);
        [Tooltip("Объект партии (`GameSession`): включается, когда игрок выбрал карту")]
        [SerializeField] GameObject gameRoot;
        [Tooltip("Кампания: список уровней по порядку. Пусто — пункта «Кампания» в меню нет")]
        [SerializeField] CampaignConfig campaign;
        [SerializeField] GameInput input;

        readonly StringBuilder digits = new();

        GameObject mainButtonsRoot;
        GameObject keypadRoot;
        GameObject levelsRoot;
        TextMeshProUGUI digitsDisplay;
#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>Закрыть вкладку. Реализация — в `Assets/Plugins/WebGL/HexColony.jslib`.</summary>
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void HexColonyCloseTab();
#endif

        UiButton[] mainButtons;
        UiButton[] keypadButtons;
        UiButton[] levelButtons;

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
            BuildBackground();
            BuildBanner();
            mainButtonsRoot = BuildMainButtons();
            keypadRoot = BuildKeypad();
            keypadRoot.SetActive(false);
            levelsRoot = BuildLevels();
            levelsRoot.SetActive(false);
        }

        void OnEnable()
        {
            input.Clicked += HandleClick;
            input.Pressed += HandlePress;
            input.PressEnded += ReleasePress;
        }

        void OnDisable()
        {
            input.Clicked -= HandleClick;
            input.Pressed -= HandlePress;
            input.PressEnded -= ReleasePress;
        }

        /// <summary>
        /// Единственный подписчик на клик, пока партии нет: разбирает его сам, попаданием
        /// в прямоугольник, — тот же приём, что у `GameOverView.HandleClick`.
        /// </summary>
        void HandleClick(Vector2 screenPosition)
        {
            foreach (var button in Shown())
                if (button.TryClick(screenPosition))
                    return;
        }

        /// <summary>Палец лёг на экран: кнопка под ним проседает.</summary>
        void HandlePress(Vector2 screenPosition)
        {
            foreach (var button in Shown())
                if (button.TryPress(screenPosition))
                    return;
        }

        /// <summary>Палец снят: всё прижатое возвращается.</summary>
        void ReleasePress()
        {
            foreach (var button in Shown())
                button.Release();
        }

        /// <summary>Кнопки того экрана меню, который сейчас на виду: клавиатура, уровни или корень.</summary>
        UiButton[] Shown() =>
            keypadRoot.activeSelf ? keypadButtons
            : levelsRoot.activeSelf ? levelButtons
            : mainButtons;

        /// <summary>
        /// Фоновая картинка под меню. Кадр обрезается по короткой стороне (`EnvelopeParent`),
        /// а не вписывается: полей у фона не должно быть ни в портрете, ни в ландшафте.
        /// Поверх — затемнение: середина кадра светлая, и без него банер и подписи кнопок
        /// на ней теряются. Картинки может не оказаться — тогда меню остаётся на ровном фоне.
        /// Строится первым, до банера и кнопок: в uGUI порядок рисования — это порядок детей.
        /// </summary>
        void BuildBackground()
        {
            var sprite = Resources.Load<Sprite>(BackgroundAsset);
            if (sprite == null)
                return;

            var picture = NewImage("Background", sprite, Color.white).rectTransform;
            picture.anchorMin = picture.anchorMax = picture.pivot = new Vector2(0.5f, 0.5f);
            picture.anchoredPosition = Vector2.zero;

            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;

            NewImage("Scrim", null, backgroundScrim).rectTransform.Stretch();
        }

        /// <summary>Прямоугольник картинкой или ровным цветом: спрайта нет — рисуется заливка.</summary>
        Image NewImage(string name, Sprite sprite, Color color)
        {
            var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            created.transform.SetParent(transform, false);

            var image = created.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
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

            var buttons = new List<UiButton>(4);
            var y = (ButtonHeight + ButtonGap) * (campaign != null && campaign.Count > 0 ? 1.5f : 1f);

            // Кампания — главный путь, поэтому она первая и тёплая; свободная игра остаётся
            // рядом. Кампании нет в ассете — нет и пункта: мёртвая кнопка хуже её отсутствия.
            if (campaign != null && campaign.Count > 0)
            {
                var levels = UiButton.Create("Campaign", root, theme, theme.ButtonPrimary,
                    "Кампания", 40f, OpenLevels);
                Place(levels.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y),
                    new Vector2(ButtonWidth, ButtonHeight));
                buttons.Add(levels);
                y -= ButtonHeight + ButtonGap;
            }

            var random = UiButton.Create("Random", root, theme,
                campaign != null && campaign.Count > 0 ? theme.ButtonSecondary : theme.ButtonPrimary,
                "Случайная карта", 40f, StartRandom);
            Place(random.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));
            buttons.Add(random);
            y -= ButtonHeight + ButtonGap;

            var seed = UiButton.Create("Seed", root, theme, theme.ButtonSecondary,
                "Ввести сид", 40f, OpenKeypad);
            Place(seed.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));
            buttons.Add(seed);
            y -= ButtonHeight + ButtonGap;

            // Кнопка есть везде, включая веб: решение человека 09.09.2026. До 09.09.2026 в
            // WebGL её не собирали — `Application.Quit()` там вкладку не закрывает, и кнопка
            // молчала бы. Теперь выход идёт через браузер, см. `Quit`.
            var quit = UiButton.Create("Quit", root, theme, theme.ButtonSecondary, "Выход", 40f, Quit);
            Place(quit.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));
            buttons.Add(quit);

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
                        KeyAction(label), label == "Назад" ? "sfx_ui_back" : "sfx_ui_keypad");
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

        /// <summary>
        /// Выбор уровня: сетка кнопок с номером и звёздами под ним. Закрытые уровни нарисованы
        /// приглушённо и клик не принимают — прогресс живёт в `CampaignProgress` и переживает
        /// перезапуск. Экран строится один раз в `Awake`: между его открытиями прогресс не
        /// меняется, партия для этого должна успеть кончиться, а она перезагружает сцену.
        /// </summary>
        GameObject BuildLevels()
        {
            var root = UiPanel.NewRect("Levels", transform);
            Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(KeypadWidth, 1f));

            var buttons = new List<UiButton>();
            var count = campaign != null ? campaign.Count : 0;
            var rows = Mathf.CeilToInt(count / (float)LevelColumns);
            var step = LevelSize + LevelGap;
            var top = (rows - 1) * step * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var index = i;
                var unlocked = CampaignProgress.IsUnlocked(index);
                var style = unlocked ? theme.ButtonSecondary : theme.SlotEmpty;
                var button = UiButton.Create($"Level {index + 1}", root, theme, style,
                    (index + 1).ToString(), 44f, () => StartLevel(index));
                Place(button.Rect, new Vector2(0.5f, 0.5f),
                    new Vector2((index % LevelColumns - 1) * step, top - index / LevelColumns * step),
                    new Vector2(LevelSize, LevelSize));

                var stars = StarGraphic.Row("Stars", button.Rect, unlocked ? CampaignProgress.StarsAt(index) : 0, 3,
                    LevelStarSize, LevelStarGap, theme.Gold, theme.Divider.FillTop);
                Place(stars, new Vector2(0.5f, 0f), new Vector2(0f, 26f),
                    new Vector2(LevelStarSize * 3f + LevelStarGap * 2f, LevelStarSize));

                buttons.Add(button);
            }

            var back = UiButton.Create(
                "Back", root, theme, theme.ButtonSecondary, "Назад", 36f, CloseLevels, "sfx_ui_back");
            Place(back.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, top - rows * step - 30f),
                new Vector2(KeypadWidth, StartButtonHeight));
            buttons.Add(back);

            levelButtons = buttons.ToArray();
            return root.gameObject;
        }

        void OpenLevels()
        {
            mainButtonsRoot.SetActive(false);
            levelsRoot.SetActive(true);
        }

        void CloseLevels()
        {
            levelsRoot.SetActive(false);
            mainButtonsRoot.SetActive(true);
        }

        /// <summary>Уровень кампании: закрытый молчит, открытый заказывает свой сид и партию.</summary>
        void StartLevel(int index)
        {
            if (!CampaignProgress.IsUnlocked(index))
                return;

            CampaignSession.Begin(campaign, index);
            BeginGame();
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
            CampaignSession.Clear();
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

            CampaignSession.Clear();
            SessionSeed.Repeat(seed);
            BeginGame();
        }

        /// <summary>
        /// Гасим себя раньше, чем включаем партию: `SetActive(false)` синхронно отписывает
        /// этот экран от `input.Clicked` до того, как `GameSession.OnEnable` на него подпишется.
        /// </summary>
        void BeginGame()
        {
            Game.Audio.GameAudio.Play("sfx_level_start", 0.8f);
            gameObject.SetActive(false);
            gameRoot.SetActive(true);
        }

        /// <summary>
        /// Выход. В вебе это закрытие вкладки через `Assets/Plugins/WebGL/HexColony.jslib`:
        /// из C# браузер иначе не позвать, а `Application.Quit()` вкладку не трогает. Браузер
        /// закрывает только то окно, которое открыл сам скрипт, поэтому на обычной вкладке
        /// закрытие не сработает — `Application.Quit()` после него останавливает партию,
        /// чтобы нажатие не осталось совсем без ответа.
        /// </summary>
        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
#if UNITY_WEBGL
            HexColonyCloseTab();
#endif
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
