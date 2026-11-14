using System;
using PokerTower.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PokerTower.Game
{
    /// <summary>
    /// 카드 1장 UI. 표시만 담당하고, 클릭 시 어떤 일이 일어날지는 소유자가 정합니다.
    /// 카드 안에는 랭크와 (그림 속) 속성 문양만 둡니다. 메인 속성 같은 게임 상태는 MainElementHud가 카드 밖에서 표시.
    /// 레이어: Root(클릭) → Frame(버림 표시 테두리) → Art(속성 배경 Sprite, 3:4) → Rank → 버림 글자.
    ///
    /// 랭크 규격 (모든 속성·랭크·카드 크기 공통):
    ///   - 위치: 카드 정규화 좌표 X 0.5, Y 0.75 (아래가 0). 대문자 높이 상자의 중심을 이 점에 맞추고 모든 랭크가 같은 기준선을 씀
    ///   - 크기: 글자 크기 = 카드 높이 × 0.40, 외곽선·그림자 두께 = 글자 크기에 비례 (자동 크기 맞춤 없음)
    ///   - 서체: 숫자도 대문자 높이인 고전 세리프 (Ui.RankFont)
    /// </summary>
    public sealed class CardWidget
    {
        /// <summary>카드 그림 원본 비율 (1086×1448 = 3:4).</summary>
        public const float Aspect = 3f / 4f;
        private const float RankCenterY = 0.75f;
        private const float RankSizeRatio = 0.40f;

        public readonly RectTransform Root;
        public Card Card { get; private set; }

        private readonly Image _hit;       // 클릭 영역 (투명)
        private readonly Image _frame;     // 버림 표시 테두리 · 빈 자리 표시. 카드 실루엣에 맞춤
        private readonly Image _art;
        private readonly RectTransform _rankRt;
        private readonly Text _rank;
        private readonly Outline _rankEdge, _rankGlow;
        private readonly Shadow _rankShadow;
        private readonly Text _discardMark;
        private readonly Text _extraTag;
        /// <summary>추가 드로우 카드 테두리 색 (청록).</summary>
        public static readonly Color ExtraColor = new Color(0.3f, 0.95f, 1f);
        private readonly Button _button;
        private readonly bool _small;

        private Vector2 _basePos;
        private bool _marked, _hovered;
        private int _siblingBeforeHover = -1;

        public CardWidget(Transform parent, string name, Vector2 anchor, Vector2 pos, float height, Action onClick,
                          bool small = false, bool hoverZoom = false)
        {
            _small = small;
            _basePos = pos;
            Root = Ui.MakeRect(parent, name, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(height * Aspect, height));
            _hit = Ui.Panel(Root, Color.clear, true);
            _button = Root.gameObject.AddComponent<Button>();
            _button.targetGraphic = _hit;
            _button.transition = Selectable.Transition.None;
            if (onClick != null) _button.onClick.AddListener(() => onClick());

            // 그림 좌우에 투명 여백이 있어(프레임 x 6~94%) 테두리를 실루엣 크기로 둠. 그림 뒤에 깔려 가장자리로만 보임
            var frameRt = Ui.MakeRect(Root, "Frame", new Vector2(0.035f, 0.005f), new Vector2(0.965f, 0.995f),
                                      new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _frame = Ui.Panel(frameRt, Color.clear);

            var artRt = Ui.Stretch(Root, "Art");
            _art = artRt.gameObject.AddComponent<Image>();
            _art.type = Image.Type.Simple;
            _art.preserveAspect = true;
            _art.raycastTarget = false;

            // 랭크: 모든 카드가 같은 앵커(0.5, 0.75)와 피벗(중앙). 세로 미세 보정은 SetSize에서 글리프 실측으로 계산
            _rankRt = Ui.MakeRect(Root, "Rank", new Vector2(0.5f, RankCenterY), new Vector2(0.5f, RankCenterY),
                                  new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _rank = _rankRt.gameObject.AddComponent<Text>();
            _rank.font = Ui.RankFont;
            _rank.fontStyle = FontStyle.Bold;
            _rank.alignment = TextAnchor.MiddleCenter;
            _rank.resizeTextForBestFit = false;                         // 글자마다 크기가 달라지지 않게
            _rank.horizontalOverflow = HorizontalWrapMode.Overflow;
            _rank.verticalOverflow = VerticalWrapMode.Overflow;
            _rank.raycastTarget = false;
            _rank.color = new Color(0.15f, 0.08f, 0.035f);              // 흑갈색
            _rankGlow = _rankRt.gameObject.AddComponent<Outline>();     // 바깥 금빛 번짐
            _rankGlow.effectColor = new Color(1f, 0.72f, 0.3f, 0.45f);
            _rankEdge = _rankRt.gameObject.AddComponent<Outline>();     // 얇은 금색 외곽선
            _rankEdge.effectColor = new Color(1f, 0.88f, 0.55f, 1f);
            _rankShadow = _rankRt.gameObject.AddComponent<Shadow>();    // 미세한 그림자
            _rankShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);

            _discardMark = Ui.Label(Root, "Discard", "", 40, Color.white, TextAnchor.MiddleCenter);
            Ui.AddOutline(_discardMark, 3f);
            _extraTag = Ui.Label(Root, "Extra", "", 26, Color.white, TextAnchor.LowerCenter); // 그림 위에서도 읽히게 흰 글자 + 검은 외곽선
            _extraTag.fontStyle = FontStyle.Bold;
            ((RectTransform)_extraTag.transform).offsetMin = new Vector2(0, 8);
            Ui.AddOutline(_extraTag, 3f);

            if (hoverZoom)
            {
                var hover = Root.gameObject.AddComponent<CardHover>();
                hover.Changed = on => { _hovered = on && Card != null; ApplyPose(); };
            }

            SetSize(height);
            SetCard(null);
        }

        /// <summary>카드 높이를 바꿉니다 (폭은 3:4로 자동). 랭크 글자·외곽선은 높이에 비례.</summary>
        public void SetSize(float height)
        {
            float width = height * Aspect;
            Root.sizeDelta = new Vector2(width, height);
            int fontSize = Mathf.Max(6, Mathf.RoundToInt(height * RankSizeRatio));
            _rank.fontSize = fontSize;
            _rankRt.sizeDelta = new Vector2(width * 0.92f, height * 0.44f);
            // 외곽선·그림자는 글자 크기에 비례 (작은 카드에서 글자를 뭉개지 않게), 아주 클 때만 상한
            float glow = Mathf.Min(fontSize * 0.026f, 4f), edge = Mathf.Min(fontSize * 0.015f, 2.2f);
            _rankGlow.effectDistance = new Vector2(glow, -glow);
            _rankEdge.effectDistance = new Vector2(edge, -edge);
            _rankShadow.effectDistance = new Vector2(Mathf.Min(fontSize * 0.022f, 4f), -Mathf.Min(fontSize * 0.028f, 5f));
            _rankRt.anchoredPosition = new Vector2(0, -CapCenterOffset());
            _discardMark.fontSize = Mathf.Max(6, Mathf.RoundToInt(height * 0.2f));
        }

        /// <summary>
        /// 대문자 높이 상자(기준 글자 'H')의 중심이 글자 영역 중심에서 얼마나 떨어져 그려지는지 실측 (로컬 단위).
        /// 이 값만큼 반대로 옮기면 모든 랭크가 같은 기준선을 쓰면서 대문자 높이의 중심이 Y 0.75에 옴.
        /// 글자별로 따로 맞추지 않으므로 Q의 꼬리 같은 디센더는 자연스럽게 기준선 아래로 내려감.
        /// </summary>
        private float CapCenterOffset()
        {
            var gen = new TextGenerator();
            var settings = _rank.GetGenerationSettings(_rankRt.sizeDelta);
            if (!gen.PopulateWithErrors("H", settings, _rank.gameObject)) return 0f;
            var verts = gen.verts;
            if (verts.Count == 0) return 0f;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in verts) { min = Mathf.Min(min, v.position.y); max = Mathf.Max(max, v.position.y); }
            float ppu = Mathf.Max(0.0001f, _rank.pixelsPerUnit);
            return (min + max) * 0.5f / ppu;
        }

        /// <summary>배치 위치 (호버 확대·버림 표시 들어올림은 이 위치 기준으로 더함).</summary>
        public void SetPosition(Vector2 pos)
        {
            _basePos = pos;
            ApplyPose();
        }

        public void SetCard(Card card)
        {
            Card = card;
            bool has = card != null;
            _art.gameObject.SetActive(has);
            _rank.gameObject.SetActive(has);
            _button.enabled = has;
            _marked = false;
            _discardMark.text = "";
            _extraTag.text = "";
            if (!has)
            {
                _hovered = false;
                _frame.color = new Color(1f, 1f, 1f, _small ? 0f : 0.06f); // 빈 자리 표시
                ApplyPose();
                return;
            }
            var art = Ui.CardArt(card.Element);
            _art.sprite = art;
            // 그림이 없으면 Ui.CardArt가 오류를 남김. 카드가 안 보이지 않도록 속성색만 깔아 둠
            _art.color = art != null ? Color.white : Ui.ElementColor(card.Element);
            _frame.color = Color.clear;
            _rank.text = RankInfo.Label(card.Rank);
            ApplyPose();
        }

        /// <summary>속성 소개용 (스테이지 속성 공개 화면): 같은 카드 디자인에 랭크 없이 속성 배경만.</summary>
        public void SetElementOnly(Element e)
        {
            Card = null;
            _button.enabled = false;
            _marked = false;
            _hovered = false;
            _discardMark.text = "";
            _frame.color = Color.clear;
            var art = Ui.CardArt(e);
            _art.gameObject.SetActive(true);
            _art.sprite = art;
            _art.color = art != null ? Color.white : Ui.ElementColor(e);
            _rank.gameObject.SetActive(false);
            ApplyPose();
        }

        public void SetInteractable(bool on)
        {
            _button.interactable = on && Card != null;
        }

        /// <summary>추가 드로우 카드 표시: 청록 테두리 + '추가'. 그 턴에 안 쓰면 버려짐. SetCard 다음에 호출하세요.</summary>
        public void SetExtra(bool on)
        {
            if (!on || Card == null) return;
            _frame.color = ExtraColor;
            _extraTag.text = "추가";
        }

        /// <summary>턴 종료 버리기 표시(선택 효과). SetCard 다음에 호출하세요 (색을 덮어씀).</summary>
        public void SetMarked(bool on)
        {
            _marked = on && Card != null;
            _discardMark.text = _marked ? "버림" : "";
            if (_marked)
            {
                _frame.color = Ui.EnemyColor;
                _art.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            }
            ApplyPose();
        }

        /// <summary>호버 시 확대해서 맨 위로(전체 비율 그대로 확대), 버림 표시 카드는 살짝 들어 올림.</summary>
        private void ApplyPose()
        {
            float h = Root.sizeDelta.y;
            float zoom = _hovered ? Mathf.Max(1.0f, Mathf.Min(1.9f, 300f / Mathf.Max(1f, h))) : 1f; // 확대 후 높이 약 300
            float lift = (_hovered ? h * (zoom - 1f) * 0.5f : 0f) + (_marked ? h * 0.1f : 0f);       // 아래로 화면 밖에 나가지 않게
            Root.localScale = Vector3.one * zoom;
            Root.anchoredPosition = _basePos + new Vector2(0, lift);
            if (_hovered && _siblingBeforeHover < 0)
            {
                _siblingBeforeHover = Root.GetSiblingIndex();
                Root.SetAsLastSibling();
            }
            else if (!_hovered && _siblingBeforeHover >= 0)
            {
                Root.SetSiblingIndex(_siblingBeforeHover);
                _siblingBeforeHover = -1;
            }
        }
    }
}
