using System;
using PokerTower.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PokerTower.Game
{
    /// <summary>코드로 UGUI를 만드는 도우미. (씬·프리팹 없이 Play만 누르면 화면이 구성되도록)</summary>
    public static class Ui
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    try
                    {
                        _font = Font.CreateDynamicFontFromOSFont(
                            new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
                    }
                    catch (Exception) { _font = null; }
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        public static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.12f, 0.86f);
        public static readonly Color PanelLight = new Color(0.16f, 0.18f, 0.25f, 0.95f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        public static readonly Color PlayerColor = new Color(0.35f, 0.75f, 1f);
        public static readonly Color EnemyColor = new Color(1f, 0.38f, 0.38f);
        public static readonly Color TextDim = new Color(0.72f, 0.75f, 0.82f);

        private static readonly Sprite[] _cardArt = new Sprite[7];
        private static readonly bool[] _cardArtTried = new bool[7];

        /// <summary>
        /// 속성별 카드 배경 (랭크 없는 v2). 원본은 Assets/disign, 게임용은 바깥 회색 배경만 투명 처리한 사본
        /// Resources/PokerTowerCards/{fire…moon}.png (1086×1448, 3:4, Sprite). 없으면 오류를 남기고 null.
        /// </summary>
        public static Sprite CardArt(Element e)
        {
            int i = (int)e;
            if (!_cardArtTried[i])
            {
                _cardArtTried[i] = true;
                string path = "PokerTowerCards/" + e.ToString().ToLowerInvariant();
                _cardArt[i] = Resources.Load<Sprite>(path);
                if (_cardArt[i] == null) Debug.LogError("[PokerTower] 카드 배경 이미지가 없습니다: Resources/" + path + ".png (Sprite로 임포트 필요)");
            }
            return _cardArt[i];
        }

        private static readonly Sprite[] _emblem = new Sprite[7];

        /// <summary>카드 그림 하단의 속성 문양 부분 (HUD 아이콘용). 같은 텍스처의 일부 영역이라 새 이미지 파일 없음.</summary>
        public static Sprite CardEmblem(Element e)
        {
            int i = (int)e;
            if (_emblem[i] == null)
            {
                var art = CardArt(e);
                if (art == null) return null;
                var tex = art.texture;
                // 1086×1448 기준 하단 패널 중앙 560×560 (위에서 760~1320 → 아래에서 128~688)
                float sx = tex.width / 1086f, sy = tex.height / 1448f;
                _emblem[i] = Sprite.Create(tex, new Rect(263 * sx, 128 * sy, 560 * sx, 560 * sy), new Vector2(0.5f, 0.5f));
            }
            return _emblem[i];
        }

        private static Font _rankFont;

        /// <summary>
        /// 랭크용 굵은 세리프. 원본 카드의 'A'와 비슷한 고전 세리프를 OS 설치 폰트에서 찾음 (폰트 파일을 게임에 포함하지 않음).
        /// 숫자가 대문자와 같은 높이인(라이닝 숫자) 서체만 씀 — Georgia는 숫자 높이가 제각각(올드스타일)이라 제외.
        /// 하나도 없으면 기본 UI 폰트.
        /// </summary>
        public static Font RankFont
        {
            get
            {
                if (_rankFont == null)
                {
                    try
                    {
                        _rankFont = Font.CreateDynamicFontFromOSFont(
                            new[] { "Palatino Linotype", "Book Antiqua", "Times New Roman", "Times", "Liberation Serif", "DejaVu Serif" }, 64);
                    }
                    catch (Exception) { _rankFont = null; }
                    if (_rankFont == null) _rankFont = Font;
                }
                return _rankFont;
            }
        }

        public static Color ElementColor(Element e)
        {
            switch (e)
            {
                case Element.Fire: return new Color(0.90f, 0.28f, 0.22f);
                case Element.Water: return new Color(0.22f, 0.52f, 0.95f);
                case Element.Wood: return new Color(0.25f, 0.70f, 0.32f);
                case Element.Metal: return new Color(0.72f, 0.72f, 0.78f);
                case Element.Earth: return new Color(0.62f, 0.44f, 0.25f);
                case Element.Sun: return new Color(1.00f, 0.72f, 0.15f);
                case Element.Moon: return new Color(0.58f, 0.40f, 0.90f);
            }
            return Color.gray;
        }

        public static RectTransform MakeRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>부모를 가득 채우는 RectTransform.</summary>
        public static RectTransform Stretch(Transform parent, string name, float inset = 0f)
        {
            var rt = MakeRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Panel(RectTransform rt, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color,
                                 TextAnchor align = TextAnchor.MiddleCenter, float inset = 0f)
        {
            var rt = Stretch(parent, name, inset);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        public static void AddOutline(Graphic g, float dist = 2f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.85f);
            o.effectDistance = new Vector2(dist, -dist);
        }

        public static Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos,
                                    Vector2 size, Color color, int fontSize, Action onClick)
        {
            var rt = MakeRect(parent, name, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);
            var img = Panel(rt, color, true);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            b.colors = cb;
            var t = Label(rt, "Label", label, fontSize, Color.white);
            AddOutline(t, 1.5f);
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        public static void SetButtonLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        /// <summary>가로 막대 게이지 (HP 등). 반환한 Image의 fillAmount로 조절.</summary>
        public static Image Bar(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                                Color fill, out Text label)
        {
            var bg = MakeRect(parent, name, anchor, anchor, pivot, pos, size);
            Panel(bg, new Color(0, 0, 0, 0.6f));
            var fillRt = Stretch(bg, "Fill", 3f);
            var img = Panel(fillRt, fill);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.fillAmount = 1f;
            // Filled 타입은 스프라이트가 있어야 동작하므로 기본 UI 스프라이트 대신 1px 흰 스프라이트 사용
            img.sprite = WhiteSprite;
            label = Label(bg, "Text", "", 22, Color.white);
            AddOutline(label, 1.5f);
            return img;
        }

        private static Sprite _white;
        public static Sprite WhiteSprite
        {
            get
            {
                if (_white == null)
                {
                    var tex = new Texture2D(4, 4);
                    var px = new Color[16];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    tex.SetPixels(px);
                    tex.Apply();
                    _white = Sprite.Create(tex, new UnityEngine.Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                }
                return _white;
            }
        }

        public static string Hex(Color c) { return ColorUtility.ToHtmlStringRGB(c); }

        public static string Colored(string s, Color c) { return "<color=#" + Hex(c) + ">" + s + "</color>"; }
    }
}
