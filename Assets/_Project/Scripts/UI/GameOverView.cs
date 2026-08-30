using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Финальный экран: из чего сложился счёт и как он смотрится на фоне рекорда. Тапы он не
    /// ловит — партия к этому моменту уже не принимает ввод, а перезапуск делается перезагрузкой.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class GameOverView : MonoBehaviour
    {
        [SerializeField] Color backdropColor = new(0.04f, 0.04f, 0.06f, 0.88f);
        [SerializeField] Color titleColor = new(0.96f, 0.92f, 0.76f);
        [SerializeField] Color scoreColor = new(1f, 0.84f, 0.30f);
        [SerializeField] Color textColor = new(0.86f, 0.86f, 0.90f);
        [SerializeField] Color recordColor = new(0.55f, 0.90f, 0.60f);
        [SerializeField] int titleSize = 56;
        [SerializeField] int scoreSize = 96;
        [SerializeField] int fontSize = 36;
        [SerializeField] float sideMargin = 48f;

        Text title;
        Text score;
        Text breakdown;
        Text record;

        /// <summary>Показать итог партии. Второй раз не вызывается: партия кончается один раз.</summary>
        public void Show(FinalScore final)
        {
            Build();

            var isRecord = HighScore.Submit(final.Total);

            title.text = final.IsPerfect ? "Поле пройдено" : "Заработать больше нечем";
            score.text = final.Total.ToString();

            breakdown.text =
                $"Заработано за партию: {final.Earned}\n" +
                $"Потеряно на переполнении: {final.Lost} → {Signed(-final.LostPenalty)}\n" +
                $"Плитки открыты: {final.OpenedTiles}/{final.FieldTiles} → {Signed(final.FieldBonus)}\n" +
                $"Месторождения выработаны: {final.ExhaustedDeposits}/{final.FieldDeposits} → {Signed(final.DepositBonus)}";

            record.text = isRecord ? $"Новый рекорд: {final.Total}" : $"Рекорд: {HighScore.Best}";
            record.color = isRecord ? recordColor : textColor;

            gameObject.SetActive(true);
        }

        /// <summary>Слагаемое со знаком: «−0» в строке итога читается как опечатка.</summary>
        static string Signed(int value) => value switch
        {
            0 => "0",
            > 0 => $"+{value}",
            _ => $"−{-value}"
        };

        void Build()
        {
            if (title != null)
                return;

            GetComponent<Image>().color = backdropColor;

            title = CreateLine("Title", titleColor, titleSize, 0.62f, titleSize * 1.4f);
            score = CreateLine("Score", scoreColor, scoreSize, 0.52f, scoreSize * 1.3f);
            breakdown = CreateLine("Breakdown", textColor, fontSize, 0.34f, fontSize * 5.6f);
            record = CreateLine("Record", textColor, fontSize, 0.22f, fontSize * 1.4f);
        }

        /// <summary>Строка во всю ширину экрана: якорь по высоте задаёт её место в столбце.</summary>
        Text CreateLine(string lineName, Color color, int size, float anchorY, float height)
        {
            var line = new GameObject(lineName, typeof(RectTransform), typeof(Text));
            line.transform.SetParent(transform, false);

            var rect = (RectTransform)line.transform;
            rect.anchorMin = new Vector2(0f, anchorY);
            rect.anchorMax = new Vector2(1f, anchorY);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-sideMargin * 2f, height);
            rect.anchoredPosition = Vector2.zero;

            var text = line.GetComponent<Text>();
            text.font = UiFont.Shared;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.UpperCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
