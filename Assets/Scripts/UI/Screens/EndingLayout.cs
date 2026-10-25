using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 엔딩 화면의 부품을 코드로 만든다 [임시: 프리팹 없이. 나중에 프리팹으로 바뀌면 이 클래스가 프리팹 참조로 바뀐다].
    // 배경, 가운데에 쌓인 문구 줄, 아래에서 올라올 크레딧, 감사 문구와 안내. 연출(단계·시간·입력)은 EndingScreen이 맡고, 여기는 만들어 넘기기만 한다.
    internal sealed class EndingLayout
    {
        // 배치(기준 해상도 1920×1080) [임시].
        private const float LineSize = 46f;
        private const float LineGap = 34f;
        private const float LineWidth = 1500f;
        private const float CreditsTitleSize = 84f;
        private const float RoleSize = 30f;
        private const float NameSize = 44f;
        private const float RoleGap = 14f;
        private const float NameGap = 8f;
        private const float SectionGap = 96f;
        private const float TitleGap = 140f;
        private const float ThanksSize = 60f;
        private const float PromptSize = 28f;

        private static readonly Color TextColor = new(0.96f, 0.94f, 1f, 1f);
        private static readonly Color RoleColor = new(0.68f, 0.62f, 0.86f, 1f);
        private static readonly Color PromptColor = new(0.8f, 0.78f, 0.9f, 1f);

        // 검은 바탕. 덮기 연출이 알파를 올린다. 레이캐스트를 받아 아래 화면의 입력을 막는다.
        public Image Background { get; private set; }
        // 엔딩 문구 줄(처음엔 투명). 줄 묶음의 그룹.
        public IReadOnlyList<TMP_Text> Lines => _lines;
        public CanvasGroup LinesGroup { get; private set; }
        // 크레딧 묶음(화면 아래 기준, pivot 위)과 전체 높이. 그룹.
        public RectTransform Credits { get; private set; }
        public CanvasGroup CreditsGroup { get; private set; }
        public float CreditsHeight { get; private set; }
        // 감사 문구 그룹과 안내 글자(처음엔 투명).
        public CanvasGroup ThanksGroup { get; private set; }
        public TMP_Text Prompt { get; private set; }

        private readonly List<TMP_Text> _lines = new();

        // rect: 화면 루트(캔버스에 꽉 채워진 상태). title·body: 글꼴(없으면 TMP 기본 글꼴). scale: 글자 크기 설정 배율.
        public EndingLayout(RectTransform rect, EndingTexts texts, TMP_FontAsset titleFont, TMP_FontAsset bodyFont, float scale)
        {
            Background = rect.gameObject.AddComponent<Image>();
            Background.raycastTarget = true;

            // 엔딩 문구: 화면 가운데에 줄을 쌓는다.
            RectTransform lines = UIParts.NewRect("Lines", rect);
            UIParts.Stretch(lines);
            LinesGroup = lines.gameObject.AddComponent<CanvasGroup>();
            Quiet(LinesGroup);

            float lineHeight = (LineSize + LineGap) * scale;
            float top = (texts.Lines.Count - 1) * lineHeight * 0.5f;

            for (int i = 0; i < texts.Lines.Count; i++)
            {
                TMP_Text line = NewText("Line " + (i + 1), lines, bodyFont, LineSize * scale, TextColor, TextAlignmentOptions.Center);
                RectTransform lineRect = (RectTransform)line.transform;
                lineRect.anchorMin = lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                lineRect.sizeDelta = new Vector2(LineWidth, lineHeight);
                lineRect.anchoredPosition = new Vector2(0, top - i * lineHeight);
                line.text = texts.Lines[i];
                line.alpha = 0;
                _lines.Add(line);
            }

            // 크레딧: 위에서부터 쌓은 뒤 화면 아래에서 위로 올린다.
            Credits = UIParts.NewRect("Credits", rect);
            Credits.anchorMin = Credits.anchorMax = new Vector2(0.5f, 0f);
            Credits.pivot = new Vector2(0.5f, 1f);
            CreditsGroup = Credits.gameObject.AddComponent<CanvasGroup>();
            Quiet(CreditsGroup);

            float y = 0;
            y = AddBlock(Credits, "Title", texts.CreditsTitle, titleFont, CreditsTitleSize * scale, TextColor, y) - TitleGap * scale;

            for (int i = 0; i < texts.Credits.Count; i++)
            {
                EndingCredit credit = texts.Credits[i];
                y = AddBlock(Credits, "Role " + (i + 1), credit.Role, bodyFont, RoleSize * scale, RoleColor, y) - RoleGap * scale;

                foreach (string name in credit.Names)
                    y = AddBlock(Credits, "Name", name, bodyFont, NameSize * scale, TextColor, y) - NameGap * scale;

                y -= SectionGap * scale;
            }

            CreditsHeight = -y;
            Credits.sizeDelta = new Vector2(LineWidth, CreditsHeight);
            Credits.anchoredPosition = new Vector2(0, 0);

            // 감사 문구와 안내.
            RectTransform thanks = UIParts.NewRect("Thanks", rect);
            UIParts.Stretch(thanks);
            ThanksGroup = thanks.gameObject.AddComponent<CanvasGroup>();
            Quiet(ThanksGroup);

            TMP_Text thanksText = NewText("Thanks Text", thanks, titleFont, ThanksSize * scale, TextColor, TextAlignmentOptions.Center);
            RectTransform thanksRect = (RectTransform)thanksText.transform;
            thanksRect.anchorMin = thanksRect.anchorMax = new Vector2(0.5f, 0.5f);
            thanksRect.sizeDelta = new Vector2(LineWidth, ThanksSize * scale * 2);
            thanksRect.anchoredPosition = new Vector2(0, 30 * scale);
            thanksText.text = texts.Thanks;

            Prompt = NewText("Prompt", thanks, bodyFont, PromptSize * scale, PromptColor, TextAlignmentOptions.Center);
            RectTransform promptRect = (RectTransform)Prompt.transform;
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.sizeDelta = new Vector2(LineWidth, PromptSize * scale * 2);
            promptRect.anchoredPosition = new Vector2(0, 120 * scale);
            Prompt.text = texts.Prompt;
            Prompt.alpha = 0;
        }

        // 글자 한 덩이를 위(y)에서 아래로 놓고, 다음 덩이를 놓을 y를 돌려준다. 빈 글자는 놓지 않는다.
        private static float AddBlock(RectTransform parent, string name, string text, TMP_FontAsset font, float size, Color color, float y)
        {
            if (string.IsNullOrEmpty(text))
                return y;

            TMP_Text block = NewText(name, parent, font, size, color, TextAlignmentOptions.Top);
            RectTransform rect = (RectTransform)block.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            block.text = text;
            float height = Mathf.Max(size * 1.25f, block.GetPreferredValues(text, LineWidth, 0).y);
            rect.sizeDelta = new Vector2(LineWidth, height);
            rect.anchoredPosition = new Vector2(0, y);
            return y - height;
        }

        private static void Quiet(CanvasGroup group)
        {
            group.alpha = 0;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        // 여러 줄로 감싸는 글자(Normal).
        private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment) =>
            UIParts.NewText(name, parent, font, size, color, alignment, TextWrappingModes.Normal);
    }
}
