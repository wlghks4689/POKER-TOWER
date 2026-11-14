using PokerTower.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PokerTower.Game
{
    /// <summary>
    /// 메인 속성 HUD. 카드 밖, 화면 오른쪽에 현재 메인 속성의 문양·이름을 보여 줍니다.
    /// 상태를 따로 갖지 않고, 호출하는 쪽이 원래 데이터(Battle.Setup.MainElement)를 넘겨 갱신합니다.
    /// </summary>
    public sealed class MainElementHud
    {
        public readonly RectTransform Root;
        private readonly Image _bg;
        private readonly Outline _edge;
        private readonly Image _iconFrame;
        private readonly Image _icon;
        private readonly Text _name;

        public MainElementHud(Transform parent, string name, Vector2 anchor, Vector2 pos)
        {
            Root = Ui.MakeRect(parent, name, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(300, 108));
            _bg = Ui.Panel(Root, Ui.PanelColor);
            _edge = Root.gameObject.AddComponent<Outline>();
            _edge.effectDistance = new Vector2(2, -2);

            var title = Ui.Label(Root, "Title", "메인 속성", 22, Ui.TextDim, TextAnchor.UpperLeft);
            ((RectTransform)title.transform).offsetMin = new Vector2(108, 0);
            ((RectTransform)title.transform).offsetMax = new Vector2(-10, -10);

            var iconRt = Ui.MakeRect(Root, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                                     new Vector2(12, 0), new Vector2(84, 84));
            _iconFrame = Ui.Panel(iconRt, Color.black);
            var inner = Ui.Stretch(iconRt, "Emblem", 3f);
            _icon = inner.gameObject.AddComponent<Image>();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;

            _name = Ui.Label(Root, "Name", "", 34, Color.white, TextAnchor.LowerLeft);
            ((RectTransform)_name.transform).offsetMin = new Vector2(108, 14);
            ((RectTransform)_name.transform).offsetMax = new Vector2(-10, 0);
            Ui.AddOutline(_name, 2f);
            Set(null);
        }

        /// <summary>null이면 '미지정'.</summary>
        public void Set(Element? main)
        {
            if (!main.HasValue)
            {
                _edge.effectColor = new Color(1, 1, 1, 0.15f);
                _iconFrame.color = new Color(1, 1, 1, 0.08f);
                _icon.enabled = false;
                _name.text = "미지정";
                _name.color = Ui.TextDim;
                return;
            }
            var e = main.Value;
            Color accent = HudColor(e);
            _edge.effectColor = accent;
            _iconFrame.color = accent;
            _icon.sprite = Ui.CardEmblem(e);
            _icon.enabled = _icon.sprite != null;
            _name.text = ElementInfo.Korean(e) + "속성";
            _name.color = TextColor(e);
        }

        /// <summary>속성 이름 글자색. 월은 테두리가 짙은 남색이라 글자는 밝게.</summary>
        public static Color TextColor(Element e)
        {
            return e == Element.Moon ? new Color(0.78f, 0.82f, 1f) : HudColor(e);
        }

        /// <summary>HUD 전용 표시 색 (게임 내부 속성 색은 그대로).</summary>
        public static Color HudColor(Element e)
        {
            switch (e)
            {
                case Element.Sun: return new Color(1f, 0.9f, 0.62f);     // 아이보리·금
                case Element.Fire: return new Color(1f, 0.45f, 0.2f);    // 주황·붉은
                case Element.Water: return new Color(0.35f, 0.65f, 1f);  // 파랑
                case Element.Wood: return new Color(0.45f, 0.85f, 0.4f); // 초록
                case Element.Metal: return new Color(0.86f, 0.88f, 0.93f); // 백금·은
                case Element.Earth: return new Color(0.82f, 0.62f, 0.3f);  // 황토
                case Element.Moon: return new Color(0.16f, 0.2f, 0.45f);   // 짙은 남색
            }
            return Color.white;
        }
    }
}
