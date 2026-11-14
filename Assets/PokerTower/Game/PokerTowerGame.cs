using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PokerTower.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PokerTower.Game
{
    /// <summary>
    /// POKER TOWER Unity 프로토타입 진입점.
    /// 아무 씬에서나 Play를 누르면 자동으로 생성되어 타이틀 → 속성 편성 → 전투 → 결과 흐름을 실행합니다.
    /// 규칙 계산은 전부 PokerTower.Core가 하고, 이 클래스는 입력·표시·연출만 담당합니다.
    /// </summary>
    public sealed class PokerTowerGame : MonoBehaviour
    {
        private const string SaveKeyFloor = "PokerTower.Floor";
        private const string SaveKeyProgress = "PokerTower.Progress";
        private static PokerTowerGame _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (_instance != null) return;
            new GameObject("PokerTower").AddComponent<PokerTowerGame>();
        }

        // ───── 공통 ─────
        private RectTransform _canvas;
        private BattleStage3D _stage;
        private RectTransform _titleScreen, _revealScreen, _battleScreen, _resultScreen, _graveScreen;
        private Text _banner;
        private Coroutine _bannerRoutine;

        // ───── 진행 ─────
        private int _floor = 1;
        private PlayerProgress _progress = new PlayerProgress();
        private Battle _battle;

        // ───── 타이틀 ─────
        private Text _titleFloor;
        private Button _continueButton;

        // ───── 스테이지 속성 공개 ─────
        private Text _revealTitle, _revealCountdown;
        private bool _revealSkip;
        private readonly CardWidget[] _revealCards = new CardWidget[GameConfig.StageElementCount];
        private readonly Text[] _revealNames = new Text[GameConfig.StageElementCount];
        private readonly Text[] _revealMainTags = new Text[GameConfig.StageElementCount];
        private MainElementHud _battleHud; // 메인 속성 HUD: 카드 밖 오른쪽. Battle.Setup.MainElement만 참조

        // ───── 전투 ─────
        private Text _floorText, _stageElementsText, _enemyName, _enemyStats, _enemyStatus;
        private Image _enemyHpFill, _playerHpFill, _playerShieldFill;
        // 체력바 표시값 (규칙 값을 따라가는 표시 전용). 쉴드가 먼저 줄고 그다음 HP가 줄어드는 연출에 사용
        private float _barHp, _barShield;
        private bool _barSnap = true;
        private Text _enemyHpText, _playerHpText, _playerStatus, _pileText, _logText, _previewText, _reasonText, _seedText;
        private readonly List<Image> _speedSlotBg = new List<Image>();
        private readonly List<Text> _speedSlotText = new List<Text>();
        private Image _useZoneBg;
        // v1.2: 손패 장수 제한 없음 → 보유 카드 존 위젯·슬롯은 장수에 맞춰 늘어남. 슬롯의 null = 빈 자리(카드가 사용 존에 올라감).
        private RectTransform _handZone;
        private readonly List<CardWidget> _handWidgets = new List<CardWidget>();
        private readonly List<Card> _handSlots = new List<Card>();
        private readonly CardWidget[] _useWidgets = new CardWidget[GameConfig.MaxSubmit];
        private readonly Card[] _useSlots = new Card[GameConfig.MaxSubmit];
        private bool _awaitingDiscard;
        private readonly List<Card> _discardMarks = new List<Card>();
        private readonly Dictionary<int, int> _originSlot = new Dictionary<int, int>();
        private Button _submitButton, _exchangeButton, _graveButton, _autoButton;
        private bool _submitExchange; // 이번 제출이 '교체' 버튼으로 들어왔는지
        private bool _awaitingInput;
        private List<Card> _submission;
        private bool _autoPlay;

        // ───── 결과·묘지 ─────
        private Text _resultTitle, _resultBody;
        private Button _resultPrimary, _resultSecondary, _resultStats;
        private RectTransform _graveGrid;
        private Text _graveTitle;

        // ───── 스탯 ─────
        private RectTransform _statScreen;
        private Text _statRemain;
        private readonly Text[] _statRows = new Text[PlayerStats.Count];
        private readonly int[] _statPending = new int[PlayerStats.Count];
        private Button _statConfirm;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _progress = PlayerProgress.Parse(PlayerPrefs.GetString(SaveKeyProgress, ""));

            _stage = new GameObject("BattleStage3D").AddComponent<BattleStage3D>();
            _stage.transform.SetParent(transform, false);
            _stage.Build();

            BuildCanvas();
            BuildTitle();
            BuildReveal();
            BuildBattle();
            BuildResult();
            BuildTower();
            BuildGraveyard();
            BuildStats();
            ShowTitle();
        }

        private void Update()
        {
            if (_battle != null && _battleScreen.gameObject.activeSelf) UpdatePlayerBar();
        }

        /// <summary>
        /// 플레이어 체력바 (표시 전용). 표시값이 규칙 값을 따라가며, 쉴드 변화가 먼저 끝난 뒤 HP가 움직여
        /// '흰색 구간이 먼저 줄어드는' 모습이 보임. 막대 전체 길이 = max(최대 HP, HP + 쉴드).
        /// </summary>
        private void UpdatePlayerBar()
        {
            var p = _battle.Player;
            float targetShield = p.Shield.Active ? p.Shield.Amount : 0;
            float speed = p.MaxHp * 0.9f * Time.unscaledDeltaTime; // 최대 HP의 90%/초
            if (!Mathf.Approximately(_barShield, targetShield)) _barShield = Mathf.MoveTowards(_barShield, targetShield, speed);
            else _barHp = Mathf.MoveTowards(_barHp, p.Hp, speed);
            float denom = Mathf.Max(p.MaxHp, _barHp + _barShield);
            _playerHpFill.fillAmount = _barHp / denom;
            _playerShieldFill.fillAmount = (_barHp + _barShield) / denom;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ═══════════════════════════ 캔버스 ═══════════════════════════

        private void BuildCanvas()
        {
            var go = new GameObject("PokerTowerCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _canvas = (RectTransform)go.transform;

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
                AddInputModule(es);
            }

            _banner = Ui.Label(_canvas, "Banner", "", 58, Color.white);
            ((RectTransform)_banner.transform).anchorMin = new Vector2(0, 0.6f);
            ((RectTransform)_banner.transform).anchorMax = new Vector2(1, 0.72f);
            Ui.AddOutline(_banner, 3f);
        }

        /// <summary>프로젝트가 새 Input System을 쓰든 구 Input Manager를 쓰든 동작하도록 입력 모듈을 고릅니다.</summary>
        private static void AddInputModule(GameObject es)
        {
#if ENABLE_INPUT_SYSTEM
            var t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) { es.AddComponent(t); return; }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        private RectTransform MakeScreen(string name)
        {
            var rt = Ui.Stretch(_canvas, name);
            rt.SetSiblingIndex(0);
            rt.gameObject.SetActive(false);
            return rt;
        }

        private void ShowOnly(RectTransform screen)
        {
            _titleScreen.gameObject.SetActive(screen == _titleScreen);
            _revealScreen.gameObject.SetActive(screen == _revealScreen);
            _battleScreen.gameObject.SetActive(screen == _battleScreen);
            _towerScreen.gameObject.SetActive(screen == _towerScreen);
            _boostScreen.gameObject.SetActive(false);
            _resultScreen.gameObject.SetActive(false);
            _graveScreen.gameObject.SetActive(false);
        }

        // ═══════════════════════════ 타이틀 (03.1 [확정]) ═══════════════════════════

        private void BuildTitle()
        {
            _titleScreen = MakeScreen("Title");
            Ui.Panel(_titleScreen, new Color(0.03f, 0.03f, 0.06f, 0.78f));
            var t = Ui.Label(_titleScreen, "Logo", "POKER TOWER", 120, Ui.Gold);
            ((RectTransform)t.transform).anchorMin = new Vector2(0, 0.62f);
            Ui.AddOutline(t, 4f);
            var sub = Ui.Label(_titleScreen, "Sub", "포커 족보 기반 탑 등반 로그라이크", 34, Ui.TextDim);
            ((RectTransform)sub.transform).anchorMin = new Vector2(0, 0.52f);
            ((RectTransform)sub.transform).anchorMax = new Vector2(1, 0.62f);
            _titleFloor = Ui.Label(_titleScreen, "Floor", "", 44, Color.white);
            ((RectTransform)_titleFloor.transform).anchorMin = new Vector2(0, 0.42f);
            ((RectTransform)_titleFloor.transform).anchorMax = new Vector2(1, 0.5f);

            var center = new Vector2(0.5f, 0.3f);
            _continueButton = Ui.MakeButton(_titleScreen, "Continue", "이어하기", center, new Vector2(-170, 0), new Vector2(300, 90),
                new Color(0.2f, 0.45f, 0.8f), 36, () =>
                {
                    _floor = Mathf.Max(1, PlayerPrefs.GetInt(SaveKeyFloor, 1));
                    ShowTower();
                });
            Ui.MakeButton(_titleScreen, "NewGame", "새로하기", center, new Vector2(170, 0), new Vector2(300, 90),
                new Color(0.55f, 0.25f, 0.25f), 36, () =>
                {
                    _floor = 1;
                    PlayerPrefs.SetInt(SaveKeyFloor, 1);
                    _progress.Reset(); // [가정] 새로하기 시 스탯 포인트 초기화
                    SaveProgress();
                    ShowTower();
                });
            var ver = Ui.Label(_titleScreen, "Ver", "Unity 프로토타입 v0.2 · 기획서 v1.2 (2026-11-14) 기준", 22, Ui.TextDim, TextAnchor.LowerRight, 20f);
            ver.raycastTarget = false;
        }

        private void ShowTitle()
        {
            ShowOnly(_titleScreen);
            bool hasSave = PlayerPrefs.HasKey(SaveKeyFloor);
            int f = PlayerPrefs.GetInt(SaveKeyFloor, 1);
            _titleFloor.text = hasSave ? "현재 " + FloorLabel(f) : "진행 기록 없음";
            _continueButton.interactable = hasSave;
            _stage.SpawnEnemy(hasSave ? f : 1, GameConfig.IsBossFloor(f));
        }

        // ═══════════════════ 스테이지 속성 공개 (사용자 결정 2026-11-14) ═══════════════════
        // 스테이지에 들어갈 때 시스템이 7속성 중 5개와 메인 속성을 무작위로 정하고(Core: StageSetup.CreateRandom),
        // 이 화면에서 5초 동안 보여 준 뒤 전투를 시작합니다. 여기서는 보여 주기만 하고 규칙 판정은 하지 않습니다.

        private const float StageRevealSeconds = 5f;

        private void BuildReveal()
        {
            _revealScreen = MakeScreen("StageReveal");
            var revealBg = Ui.Panel(_revealScreen, new Color(0.02f, 0.02f, 0.04f, 0.93f), true);
            var skip = _revealScreen.gameObject.AddComponent<Button>(); // 화면 아무 곳이나 클릭하면 바로 전투 시작
            skip.targetGraphic = revealBg;
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(() => _revealSkip = true);
            _revealTitle = Ui.Label(_revealScreen, "Title", "", 56, Color.white);
            ((RectTransform)_revealTitle.transform).anchorMin = new Vector2(0, 0.86f);
            ((RectTransform)_revealTitle.transform).anchorMax = new Vector2(1, 0.96f);
            Ui.AddOutline(_revealTitle, 3f);
            var sub = Ui.Label(_revealScreen, "Sub", "이번 스테이지 덱 속성 · " + GameConfig.StageElementCount * 13 * GameConfig.CopiesPerCard + "장",
                               30, Ui.TextDim);
            ((RectTransform)sub.transform).anchorMin = new Vector2(0, 0.8f);
            ((RectTransform)sub.transform).anchorMax = new Vector2(1, 0.86f);

            for (int i = 0; i < GameConfig.StageElementCount; i++)
            {
                float x = (i - (GameConfig.StageElementCount - 1) / 2f) * 290f;
                var anchor = new Vector2(0.5f, 0.52f);
                _revealCards[i] = new CardWidget(_revealScreen, "Card" + i, anchor, new Vector2(x, 0), 340f, null);
                var nameRt = Ui.MakeRect(_revealScreen, "Name" + i, anchor, anchor, new Vector2(0.5f, 0.5f), new Vector2(x, -205), new Vector2(270, 46));
                _revealNames[i] = Ui.Label(nameRt, "T", "", 34, Color.white);
                Ui.AddOutline(_revealNames[i], 2f);
                var tagRt = Ui.MakeRect(_revealScreen, "MainTag" + i, anchor, anchor, new Vector2(0.5f, 0.5f), new Vector2(x, -252), new Vector2(270, 40));
                _revealMainTags[i] = Ui.Label(tagRt, "T", "", 28, Ui.Gold);
                Ui.AddOutline(_revealMainTags[i], 2f);
            }

            _revealCountdown = Ui.Label(_revealScreen, "Countdown", "", 34, Color.white);
            ((RectTransform)_revealCountdown.transform).anchorMin = new Vector2(0, 0.05f);
            ((RectTransform)_revealCountdown.transform).anchorMax = new Vector2(1, 0.13f);
        }

        /// <summary>스테이지 진입: 속성 결정(Core) → 5초 공개 → 전투 시작.</summary>
        private void StartStage()
        {
            var setup = StageSetup.CreateRandom(_floor, System.Environment.TickCount ^ (_floor * 7919));
            StopAllCoroutines();
            _revealSkip = false;
            ShowOnly(_revealScreen);
            _stage.SpawnEnemy(_floor, GameConfig.IsBossFloor(_floor));
            _revealTitle.text = FloorLabel(_floor) + " 스테이지";
            for (int i = 0; i < _revealCards.Length; i++)
            {
                var e = setup.Elements[i];
                bool main = e == setup.MainElement;
                _revealCards[i].SetElementOnly(e);
                _revealNames[i].text = ElementInfo.Korean(e) + "속성";
                _revealNames[i].color = MainElementHud.TextColor(e);
                _revealMainTags[i].text = "";
                _revealMainTags[i].gameObject.SetActive(main);
                _revealCards[i].Root.localScale = Vector3.zero;
            }
            StartCoroutine(RevealThenBattle(setup));
        }

        private IEnumerator RevealThenBattle(StageSetup setup)
        {
            float t = 0f;
            int mainIndex = setup.Elements.IndexOf(setup.MainElement);
            while (t < StageRevealSeconds && !_revealSkip)
            {
                // 카드가 차례로 나타나고(0.12초 간격), 1.2초에 메인 속성 표시
                for (int i = 0; i < _revealCards.Length; i++)
                {
                    float k = Mathf.Clamp01((t - i * 0.12f) / 0.25f);
                    float scale = Mathf.SmoothStep(0f, 1f, k) * (i == mainIndex && t > 1.2f ? 1.07f : 1f);
                    _revealCards[i].Root.localScale = Vector3.one * scale;
                }
                if (t > 1.2f) _revealMainTags[mainIndex].text = "★ 메인 속성";
                _revealCountdown.text = "전투 시작까지 " + Mathf.CeilToInt(StageRevealSeconds - t) + "초"
                                      + Ui.Colored("   ·   화면을 클릭하면 바로 시작", Ui.TextDim);
                t += Time.deltaTime;
                yield return null;
            }
            StartBattle(setup);
        }

        // ═══════════════════════════ 전투 화면 (18장) ═══════════════════════════

        private void BuildBattle()
        {
            _battleScreen = MakeScreen("Battle");

            // 상단 바
            var top = Ui.MakeRect(_battleScreen, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 70));
            Ui.Panel(top, Ui.PanelColor);
            _floorText = Ui.Label(top, "Floor", "", 32, Color.white, TextAnchor.MiddleLeft, 24f);
            _stageElementsText = Ui.Label(top, "Elements", "", 30, Color.white, TextAnchor.MiddleCenter);
            _seedText = Ui.Label(top, "Seed", "", 18, Ui.TextDim, TextAnchor.MiddleRight, 24f);

            // 에너미 정보 (18.1 [확정] 체력·공격력·방어력 표시)
            var enemyPanel = Ui.MakeRect(_battleScreen, "EnemyPanel", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                                         new Vector2(0, -82), new Vector2(720, 150));
            Ui.Panel(enemyPanel, Ui.PanelColor);
            _enemyName = Ui.Label(enemyPanel, "Name", "", 30, Ui.EnemyColor, TextAnchor.UpperCenter, 8f);
            _enemyHpFill = Ui.Bar(enemyPanel, "HP", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(660, 34),
                                  new Color(0.85f, 0.22f, 0.25f), out _enemyHpText);
            _enemyStats = Ui.Label(enemyPanel, "Stats", "", 26, Color.white, TextAnchor.LowerLeft, 14f);
            _enemyStatus = Ui.Label(enemyPanel, "Status", "", 24, Ui.Gold, TextAnchor.LowerRight, 14f);

            // 속도 바 (04.1 [확정] 화면 좌측, 현재·이후 예상 턴 순서)
            const float slotStep = 46f; // 8칸 × 46 + 머리 60 = 428 → 화면 위에서 518까지. 플레이어 패널(아래에서 536까지)과 겹치지 않음
            var speed = Ui.MakeRect(_battleScreen, "SpeedBar", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                                    new Vector2(20, -90), new Vector2(170, 60 + GameConfig.SpeedBarPreviewCount * slotStep));
            Ui.Panel(speed, Ui.PanelColor);
            var st = Ui.Label(speed, "Title", "행동 순서", 24, Ui.Gold, TextAnchor.UpperCenter, 8f);
            Ui.AddOutline(st, 1.5f);
            for (int i = 0; i < GameConfig.SpeedBarPreviewCount; i++)
            {
                var slot = Ui.MakeRect(speed, "Slot" + i, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                                       new Vector2(0, -48 - i * slotStep), new Vector2(i == 0 ? 150 : 130, 40));
                _speedSlotBg.Add(Ui.Panel(slot, Color.gray));
                var tx = Ui.Label(slot, "T", "", i == 0 ? 24 : 22, Color.white);
                Ui.AddOutline(tx, 1.5f);
                _speedSlotText.Add(tx);
            }

            // 플레이어 정보
            var pp = Ui.MakeRect(_battleScreen, "PlayerPanel", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                                 new Vector2(20, 296), new Vector2(330, 240));
            Ui.Panel(pp, Ui.PanelColor);
            var pn = Ui.Label(pp, "Name", "플레이어", 28, Ui.PlayerColor, TextAnchor.UpperLeft, 12f);
            Ui.AddOutline(pn, 1.5f);
            _playerHpFill = Ui.Bar(pp, "HP", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(300, 32),
                                   new Color(0.25f, 0.7f, 0.4f), out _playerHpText);
            // 토 쉴드: 초록 HP 바로 뒤에 이어지는 흰색 구간 (HP 막대 뒤에 깔고 HP+쉴드 길이까지 채움)
            var shieldRt = Ui.Stretch(_playerHpFill.transform.parent, "Shield", 3f);
            shieldRt.SetSiblingIndex(_playerHpFill.transform.GetSiblingIndex());
            _playerShieldFill = Ui.Panel(shieldRt, new Color(0.95f, 0.97f, 1f));
            _playerShieldFill.type = Image.Type.Filled;
            _playerShieldFill.fillMethod = Image.FillMethod.Horizontal;
            _playerShieldFill.fillOrigin = 0;
            _playerShieldFill.sprite = _playerHpFill.sprite;
            _playerShieldFill.fillAmount = 0f;
            _playerStatus = Ui.Label(pp, "Status", "", 21, Color.white, TextAnchor.LowerLeft, 12f);

            // 로그
            var logRt = Ui.MakeRect(_battleScreen, "Log", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                                    new Vector2(-20, -90), new Vector2(470, 450));
            Ui.Panel(logRt, Ui.PanelColor);
            _logText = Ui.Label(logRt, "Text", "", 20, Ui.TextDim, TextAnchor.LowerLeft, 12f);

            // 사용 카드 존 (최대 5장, 1번 = 대표 카드)
            var useZone = Ui.MakeRect(_battleScreen, "UseZone", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                                      new Vector2(0, 285), new Vector2(5 * 160 + 30, 250));
            _useZoneBg = Ui.Panel(useZone, new Color(1, 1, 1, 0.08f));
            for (int i = 0; i < GameConfig.MaxSubmit; i++)
            {
                int idx = i;
                var slotLabel = Ui.MakeRect(useZone, "SlotNo" + i, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                                            new Vector2((i - 2) * 160f, -2), new Vector2(150, 26));
                var sl = Ui.Label(slotLabel, "T", i == 0 ? "1 · 대표" : (i + 1).ToString(), 20, new Color(1, 1, 1, 0.55f));
                sl.raycastTarget = false;
                _useWidgets[i] = new CardWidget(useZone, "Use" + i, new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 160f, -12),
                                                200f, () => OnUseCardClicked(idx));
            }
            // 미리보기: 사용 존 폭 안에서 한 줄 고정 (줄바꿈하면 에너미 모델·로그 쪽으로 번져 겹침)
            var preview = Ui.MakeRect(_battleScreen, "Preview", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                                      new Vector2(0, 545), new Vector2(5 * 160 + 30, 64));
            _previewText = Ui.Label(preview, "T", "", 30, Color.white, TextAnchor.MiddleCenter);
            _previewText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _previewText.verticalOverflow = VerticalWrapMode.Truncate;
            Ui.AddOutline(_previewText, 2.5f);
            // 안내 문구는 미리보기 위쪽 (아래쪽은 사용 존 칸 번호와 겹침)
            _reasonText = Ui.Label(preview, "Reason", "", 22, new Color(1f, 0.75f, 0.5f), TextAnchor.LowerCenter);
            ((RectTransform)_reasonText.transform).offsetMin = new Vector2(0, 66);
            ((RectTransform)_reasonText.transform).offsetMax = new Vector2(0, 30);
            _reasonText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.AddOutline(_reasonText, 1.5f);

            // 보유 카드 존 (7장까지는 간격 160, 그 이상은 간격을 줄여 겹치며 한 줄에 모두 표시)
            _handZone = Ui.MakeRect(_battleScreen, "HandZone", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                                    new Vector2(0, 20), new Vector2(HandZoneWidth, 240));
            Ui.Panel(_handZone, Ui.PanelColor);

            // 버튼
            _submitButton = Ui.MakeButton(_battleScreen, "Submit", "공격", new Vector2(1, 0), new Vector2(-150, 225), new Vector2(240, 90),
                new Color(0.75f, 0.3f, 0.2f), 42, OnSubmitClicked);
            // [사용자 결정 2026-11-14 A1] 족보가 있어도 언제든 교체 (피해 0, 효과 없음)
            _exchangeButton = Ui.MakeButton(_battleScreen, "Exchange", "교체", new Vector2(1, 0), new Vector2(-150, 142), new Vector2(240, 60),
                new Color(0.32f, 0.38f, 0.5f), 28, OnExchangeClicked);
            _graveButton = Ui.MakeButton(_battleScreen, "Grave", "묘지", new Vector2(1, 0), new Vector2(-150, 75), new Vector2(240, 60),
                new Color(0.28f, 0.28f, 0.36f), 28, ShowGraveyard);
            // 메인 속성 HUD: 로그 패널(위)과 자동 버튼(아래) 사이 빈 칸. 카드 존·버튼과 겹치지 않음
            _battleHud = new MainElementHud(_battleScreen, "MainHud", new Vector2(1, 0), new Vector2(-170, 455));
            _autoButton = Ui.MakeButton(_battleScreen, "Auto", "자동 OFF", new Vector2(1, 0), new Vector2(-150, 320), new Vector2(150, 50),
                new Color(0.22f, 0.22f, 0.3f), 22, () => { _autoPlay = !_autoPlay; Ui.SetButtonLabel(_autoButton, _autoPlay ? "자동 ON" : "자동 OFF"); });
            var pile = Ui.MakeRect(_battleScreen, "Pile", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                                   new Vector2(-20, 20), new Vector2(260, 30));
            _pileText = Ui.Label(pile, "T", "", 20, Ui.TextDim, TextAnchor.MiddleCenter);
        }

        private void StartBattle(StageSetup setup)
        {
            _battle = new Battle(setup, _progress.Stats);
            _battle.Begin();
            _handSlots.Clear();
            for (int i = 0; i < _useSlots.Length; i++) _useSlots[i] = null;
            _originSlot.Clear();
            _awaitingDiscard = false;
            _discardMarks.Clear();
            _barSnap = true; // 새 전투: 체력바 애니메이션 없이 바로 맞춤
            SyncHandSlots();
            _stage.SpawnEnemy(_floor, _battle.Enemy.IsBoss);
            ShowOnly(_battleScreen);
            RefreshBattle();
            StopAllCoroutines();
            StartCoroutine(BattleLoop());
        }

        // ───── 전투 진행 루프: 상태 판정은 Core, 여기서는 기다림·연출만 ─────
        private IEnumerator BattleLoop()
        {
            yield return new WaitForSeconds(0.4f);
            while (!_battle.IsOver)
            {
                Actor? prev = _battle.LastActor;
                Actor actor = _battle.NextTurn();
                bool consecutive = prev.HasValue && prev.Value == actor;
                RefreshBattle();

                if (actor == Actor.Enemy)
                {
                    ShowBanner(consecutive ? "에너미 연속 행동!" : "에너미 행동", Ui.EnemyColor);
                    yield return new WaitForSeconds(0.55f);
                    var rep = _battle.RunEnemyTurn();
                    yield return AnimateEnemy(rep);
                    RefreshBattle();
                    yield return new WaitForSeconds(0.3f);
                }
                else
                {
                    ShowBanner(consecutive ? "연속 행동!" : "내 차례", Ui.PlayerColor);
                    _submission = null;
                    _submitExchange = false;
                    _awaitingInput = true;
                    RefreshBattle();
                    float autoTimer = 0f;
                    while (_awaitingInput)
                    {
                        if (_autoPlay)
                        {
                            autoTimer += Time.deltaTime;
                            if (autoTimer > 0.6f) AutoSelectAndSubmit();
                        }
                        yield return null;
                    }

                    var used = _submission;
                    for (int i = 0; i < _useSlots.Length; i++) _useSlots[i] = null;
                    var rep = _battle.Submit(used, null, _submitExchange);
                    SyncHandSlots();
                    RefreshBattle();
                    yield return AnimatePlayer(rep);

                    // v1.2 턴 종료 버리기. 교체 턴·처치 턴은 Core가 이 단계를 건너뜀 (Phase로만 판단)
                    if (_battle.Phase == BattlePhase.PlayerDiscard)
                    {
                        yield return WaitForDiscardChoice("버릴 카드 선택 (최대 " + GameConfig.MaxDiscard + "장)");
                        _battle.EndPlayerTurn(_discardMarks.ToList());
                        _discardMarks.Clear();
                        SyncHandSlots();
                        RefreshBattle();
                    }
                    // v1.3 보충 후 손패가 10장을 넘으면 강제 버리기 (교체 턴 뒤에도 생길 수 있음)
                    if (_battle.Phase == BattlePhase.PlayerForcedDiscard)
                    {
                        yield return WaitForDiscardChoice("손패 " + GameConfig.MaxHandSize + "장 초과 · " + _battle.ForcedDiscardCount + "장 버리기");
                        _battle.ForceDiscard(_discardMarks.ToList());
                        _discardMarks.Clear();
                    }
                    SyncHandSlots();
                    RefreshBattle();
                    yield return new WaitForSeconds(0.25f);
                }
            }
            if (_battle.Phase == BattlePhase.Victory) yield return _stage.PlayEnemyDeath();
            yield return new WaitForSeconds(0.5f);
            ShowResult();
        }

        /// <summary>버릴 카드 고르기를 기다림 (턴 종료 버리기·강제 버리기 공용). 확정은 OnEndTurnClicked.</summary>
        private IEnumerator WaitForDiscardChoice(string banner)
        {
            _discardMarks.Clear();
            _awaitingDiscard = true;
            ShowBanner(banner, ForcedDiscard ? Ui.EnemyColor : Ui.PlayerColor);
            RefreshBattle();
            float timer = 0f;
            while (_awaitingDiscard)
            {
                if (_autoPlay)
                {
                    timer += Time.deltaTime;
                    if (timer > 0.6f) AutoDiscard();
                }
                yield return null;
            }
        }

        private void AutoSelectAndSubmit()
        {
            // 디버그용 자동 플레이: 손패를 모두 회수한 뒤 기본 피해가 가장 큰 조합 제출
            for (int i = 0; i < _useSlots.Length; i++) if (_useSlots[i] != null) ReturnToHand(i);
            var best = SimpleAi.BestSubmission(_battle.Piles.Hand);
            if (best == null) return;
            foreach (var c in best)
            {
                int slot = _handSlots.IndexOf(c);
                if (slot >= 0) MoveToUseZone(slot);
            }
            OnSubmitClicked();
        }

        // ───── 카드 조작 (18.2 [확정]) ─────

        private bool InputOpen { get { return _awaitingInput && _battle != null && _battle.Phase == BattlePhase.PlayerTurn; } }
        private bool ForcedDiscard { get { return _battle != null && _battle.Phase == BattlePhase.PlayerForcedDiscard; } }
        private bool DiscardOpen
        {
            get { return _awaitingDiscard && _battle != null && (_battle.Phase == BattlePhase.PlayerDiscard || ForcedDiscard); }
        }
        /// <summary>고를 수 있는 최대 장수: 턴 종료 버리기 2장, 강제 버리기는 넘는 장수.</summary>
        private int DiscardLimit { get { return ForcedDiscard ? _battle.ForcedDiscardCount : GameConfig.MaxDiscard; } }

        private void OnHandCardClicked(int slot)
        {
            if (slot >= _handSlots.Count || _handSlots[slot] == null) return;
            if (DiscardOpen)
            {
                // 버릴 카드 표시/해제 (턴 종료 최대 2장 · 강제 버리기는 넘는 장수만큼)
                var c = _handSlots[slot];
                if (!ForcedDiscard && _battle.Piles.IsExtra(c)) { ShowBanner("추가 카드는 자동으로 버려집니다", Ui.Gold); return; }
                if (_discardMarks.Contains(c)) _discardMarks.Remove(c);
                else if (_discardMarks.Count < DiscardLimit) _discardMarks.Add(c);
                else ShowBanner((ForcedDiscard ? DiscardLimit + "장만" : "최대 " + DiscardLimit + "장까지") + " 버릴 수 있습니다", Ui.Gold);
                RefreshBattle();
                return;
            }
            if (!InputOpen) return;
            MoveToUseZone(slot);
            RefreshBattle();
        }

        private void OnEndTurnClicked()
        {
            if (!DiscardOpen) return;
            string reason;
            bool ok = ForcedDiscard ? _battle.CanForceDiscard(_discardMarks, out reason) : _battle.CanDiscard(_discardMarks, out reason);
            if (!ok) { ShowBanner(reason, Ui.Gold); return; }
            _awaitingDiscard = false; // 중복 입력 방지: 즉시 잠금
            RefreshBattle();
        }

        private void AutoDiscard()
        {
            // 자동 플레이: 짝 없는 카드 중 낮은 랭크부터 (턴 종료 최대 2장 · 강제 버리기는 정확히 넘는 장수)
            _discardMarks.Clear();
            _discardMarks.AddRange(ForcedDiscard
                ? SimpleAi.ChooseDiscards(_battle.Piles.Hand, _battle.ForcedDiscardCount, true)
                : SimpleAi.ChooseDiscards(_battle.Piles.Hand.Where(c => !_battle.Piles.IsExtra(c)).ToList()));
            OnEndTurnClicked();
        }

        private void MoveToUseZone(int handSlot)
        {
            int target = System.Array.IndexOf(_useSlots, null);
            if (target < 0) { ShowBanner("최대 5장까지 올릴 수 있습니다", Ui.Gold); return; }
            var c = _handSlots[handSlot];
            _useSlots[target] = c;      // 가장 앞의 빈 자리. 1번이 비어 있으면 1번(대표)이 됩니다.
            _handSlots[handSlot] = null;
            _originSlot[c.Id] = handSlot;
        }

        private void OnUseCardClicked(int slot)
        {
            if (!InputOpen || _useSlots[slot] == null) return;
            ReturnToHand(slot);
            RefreshBattle();
        }

        private void ReturnToHand(int useSlot)
        {
            var c = _useSlots[useSlot];
            _useSlots[useSlot] = null; // 해당 자리만 비고 나머지는 자리 유지. 1번 회수 시 공격 속성·색 초기화.
            int origin;
            if (!_originSlot.TryGetValue(c.Id, out origin) || origin >= _handSlots.Count || _handSlots[origin] != null)
                origin = _handSlots.IndexOf(null);
            if (origin < 0) _handSlots.Add(c);
            else _handSlots[origin] = c;
        }

        private List<Card> CurrentSubmission()
        {
            if (_useSlots[0] == null) return null; // 대표 카드 없음
            return _useSlots.Where(c => c != null).ToList();
        }

        private void OnSubmitClicked()
        {
            if (DiscardOpen) { OnEndTurnClicked(); return; } // 버리기 단계에서는 같은 버튼이 '턴 종료'
            if (!InputOpen) return;
            var sub = CurrentSubmission();
            string reason;
            if (sub == null || !_battle.CanSubmit(sub, out reason)) return;
            _submission = sub;
            _awaitingInput = false; // 중복 입력 방지: 즉시 잠금
            RefreshBattle();
        }

        private void OnExchangeClicked()
        {
            if (!InputOpen) return;
            var sub = CurrentSubmission();
            string reason;
            if (sub == null || !_battle.CanSubmit(sub, out reason, true)) return;
            _submission = sub;
            _submitExchange = true;
            _awaitingInput = false;
            RefreshBattle();
        }

        /// <summary>
        /// Core 손패와 화면 슬롯을 맞춥니다. 빈 자리에 새로 뽑은 카드를 채우고, 남는 카드는 뒤에 붙입니다(장수 제한 없음).
        /// 턴이 끝나 사용 존이 비어 있으면 남은 빈 자리를 정리합니다.
        /// (버리기 단계에서는 정리하지 않음 → 카드 위치가 그대로이고, 보충 카드가 사용·버린 자리를 채움)
        /// </summary>
        private void SyncHandSlots()
        {
            var hand = _battle.Piles.Hand;
            for (int i = 0; i < _handSlots.Count; i++)
                if (_handSlots[i] != null && !hand.Contains(_handSlots[i])) _handSlots[i] = null;
            foreach (var c in hand)
            {
                if (_handSlots.Contains(c) || System.Array.IndexOf(_useSlots, c) >= 0) continue;
                int empty = _handSlots.IndexOf(null);
                if (empty < 0) _handSlots.Add(c);
                else _handSlots[empty] = c;
            }
            if (_useSlots.All(c => c == null) && _battle.Phase != BattlePhase.PlayerDiscard)
            {
                _handSlots.RemoveAll(c => c == null);
                _originSlot.Clear();
            }
        }

        private const float HandZoneWidth = 1200f;   // 화면 1920 기준 x 360~1560: 왼쪽 플레이어 패널·오른쪽 버튼과 겹치지 않음
        private const float HandCardHeight = 200f;   // 카드는 항상 3:4 (폭 150)
        private const float MinVisibleStep = 0.68f;  // 겹칠 때 다음 카드와의 간격 ≥ 카드 폭의 68% → 상단 중앙 랭크가 가려지지 않음

        private void EnsureHandWidgets(int count)
        {
            while (_handWidgets.Count < count)
            {
                int idx = _handWidgets.Count;
                _handWidgets.Add(new CardWidget(_handZone, "Hand" + idx, new Vector2(0.5f, 0.5f), Vector2.zero,
                                                HandCardHeight, () => OnHandCardClicked(idx), false, true));
            }
        }

        /// <summary>
        /// 보유 카드 존 배치. 카드가 적으면 원래 크기(150×200)·간격 160으로 전체 그림을 보여 주고,
        /// 많아지면 간격 ≥ 폭의 68%를 지키도록 카드 크기를 3:4 그대로 줄여 한 줄에 맞춤.
        /// v1.3부터 손패 최대 10장(강제 버리기 직전 11장): 10장이면 150×200 그대로 간격 113 (살짝 겹침).
        /// 겹친 카드는 마우스를 올리면 확대되어 맨 위로 올라옴.
        /// </summary>
        private void LayoutHand(int count)
        {
            float usable = HandZoneWidth - 30f;
            float w = HandCardHeight * CardWidget.Aspect;
            if (count > 1) w = Mathf.Min(w, usable / (1f + MinVisibleStep * (count - 1)));
            float step = count <= 1 ? 0f : Mathf.Min(160f, (usable - w) / (count - 1));
            for (int i = 0; i < _handWidgets.Count; i++)
            {
                bool on = i < count;
                _handWidgets[i].Root.gameObject.SetActive(on);
                if (!on) continue;
                _handWidgets[i].SetSize(w / CardWidget.Aspect);
                _handWidgets[i].SetPosition(new Vector2((i - (count - 1) / 2f) * step, 0));
            }
        }

        // ───── 화면 갱신 ─────

        private void RefreshBattle()
        {
            if (_battle == null) return;
            var b = _battle;
            var main = b.Setup.MainElement;

            _floorText.text = b.Setup.Floor + "층" + (b.Enemy.Tier != EnemyTier.Normal
                ? " · " + Ui.Colored(EnemyTierInfo.Korean(b.Enemy.Tier), TierColor(b.Enemy.Tier)) : "");
            _battleHud.Set(b.Setup.MainElement);
            _stageElementsText.text = string.Join("  ", b.Setup.Elements.Select(e =>
                Ui.Colored((e == main ? "★" : "") + ElementInfo.Hanja(e) + ElementInfo.Korean(e), e == main ? Ui.Gold : Ui.ElementColor(e))));
            _seedText.text = "seed " + b.Setup.Seed;

            // 에너미
            var en = b.Enemy;
            _enemyName.text = (en.Tier != EnemyTier.Normal ? Ui.Colored("[" + EnemyTierInfo.Korean(en.Tier) + "] ", TierColor(en.Tier)) : "") + en.Name;
            _enemyHpFill.fillAmount = en.MaxHp > 0 ? (float)en.Hp / en.MaxHp : 0f;
            _enemyHpText.text = "HP " + en.Hp + " / " + en.MaxHp;
            string def = en.Metal.Active
                ? Ui.Colored(en.EffectiveDef.ToString("0.#"), new Color(0.7f, 0.85f, 1f)) + " (원본 " + en.BaseDef.ToString("0.#") + ")"
                : en.BaseDef.ToString("0.#");
            string atk = en.Atk > en.BaseAtk ? Ui.Colored(en.Atk + " (+" + (en.Atk - en.BaseAtk) + ")", BuffColor) : en.Atk.ToString();
            _enemyStats.text = "ATK " + atk + "   DEF " + def + "   SPD " + en.BaseSpeed.ToString("0");
            var st = new List<string>();
            if (en.IsDead) { } // 쓰러진 에너미에는 남은 화상·방깎을 표시하지 않음
            else if (en.Burn.Active) st.Add(Ui.Colored("화상 " + string.Join("→", en.Burn.Ticks.Select(t => t.Amount.ToString())), Ui.ElementColor(Element.Fire)));
            if (!en.IsDead && en.CritChance > 0) st.Add(Ui.Colored("치명타 " + PlayerStats.Percent(en.CritChance) + "%", BuffColor));
            if (!en.IsDead && en.Metal.Active) st.Add(Ui.Colored("방깎 ×" + en.Metal.Multiplier.ToString("0.###"), Ui.ElementColor(Element.Metal)));
            _enemyStatus.text = string.Join("  ", st);

            // 플레이어
            var p = b.Player;
            int shield = p.Shield.Active ? p.Shield.Amount : 0;
            _playerHpText.text = "HP " + p.Hp + " / " + p.MaxHp + (shield > 0 ? " (+" + shield + ")" : "");
            if (_barSnap) { _barHp = p.Hp; _barShield = shield; _barSnap = false; }
            UpdatePlayerBar();
            var ps = new List<string>
            {
                "SPD " + p.BaseSpeed.ToString("0.#") + "   DEF " + p.BaseDef.ToString("0.#"),
                "데미지 +" + PlayerStats.Percent(p.DamageMultiplier - 1.0) + "%   치명타 " + PlayerStats.Percent(p.CritChance) + "%",
                "추가 드로우 " + PlayerStats.Percent(p.ExtraDrawChance) + "%"
            };
            var boosts = ElementInfo.All.Where(e => p.ElementBoosts[(int)e] > 0)
                .Select(e => Ui.Colored(ElementInfo.Korean(e) + "+" + PlayerStats.Percent(GameConfig.ElementBoostPerStack * p.ElementBoosts[(int)e]) + "%", Ui.ElementColor(e)));
            if (boosts.Any()) ps.Add("속성 강화 " + string.Join(" ", boosts));
            if (p.Shield.Active) ps.Add(Ui.Colored("쉴드 " + p.Shield.Amount + " (" + p.Shield.TurnsLeft + "턴)", Ui.ElementColor(Element.Earth)));
            if (p.PendingSpeedBuff > 0) ps.Add(Ui.Colored("다음 대기 SPD +" + Mathf.RoundToInt((float)p.PendingSpeedBuff * 100) + "%", Ui.ElementColor(Element.Water)));
            _playerStatus.text = string.Join("\n", ps);

            // 속도 바
            var preview = b.IsOver ? new List<Actor>() : b.PreviewTurns(GameConfig.SpeedBarPreviewCount);
            for (int i = 0; i < _speedSlotBg.Count; i++)
            {
                if (i >= preview.Count) { _speedSlotBg[i].color = new Color(0, 0, 0, 0.2f); _speedSlotText[i].text = ""; continue; }
                bool me = preview[i] == Actor.Player;
                Color c = me ? Ui.PlayerColor : Ui.EnemyColor;
                _speedSlotBg[i].color = i == 0 ? c : Color.Lerp(c, Color.black, 0.45f);
                _speedSlotText[i].text = (i == 0 ? "지금 · " : "") + (me ? "나" : "적");
            }

            _logText.text = string.Join("\n", b.Log.Skip(Mathf.Max(0, b.Log.Count - 14)));
            _pileText.text = "덱 " + b.Piles.Deck.Count + " · 손패 " + b.Piles.Hand.Count + " · 묘지 " + b.Piles.Graveyard.Count;
            Ui.SetButtonLabel(_graveButton, "묘지 (" + b.Piles.Graveyard.Count + ")");

            // 카드
            bool open = InputOpen;
            bool discard = DiscardOpen;
            EnsureHandWidgets(_handSlots.Count);
            LayoutHand(_handSlots.Count);
            for (int i = 0; i < _handSlots.Count; i++)
            {
                _handWidgets[i].SetCard(_handSlots[i]);
                _handWidgets[i].SetExtra(_handSlots[i] != null && b.Piles.IsExtra(_handSlots[i]));
                _handWidgets[i].SetInteractable(open || discard);
                _handWidgets[i].SetMarked(_handSlots[i] != null && _discardMarks.Contains(_handSlots[i]));
            }
            for (int i = 0; i < _useWidgets.Length; i++)
            {
                _useWidgets[i].SetCard(_useSlots[i]);
                _useWidgets[i].SetInteractable(open);
            }
            _useZoneBg.color = _useSlots[0] != null
                ? new Color(Ui.ElementColor(_useSlots[0].Element).r, Ui.ElementColor(_useSlots[0].Element).g, Ui.ElementColor(_useSlots[0].Element).b, 0.35f)
                : new Color(1, 1, 1, 0.08f);

            RefreshPreview(open);
        }

        private void RefreshPreview(bool open)
        {
            var b = _battle;
            _exchangeButton.interactable = false;
            if (DiscardOpen && ForcedDiscard)
            {
                int need = _battle.ForcedDiscardCount;
                _previewText.text = Ui.Colored("<size=50>버릴 카드 " + (need - _discardMarks.Count) + "장 남음</size>", Ui.EnemyColor);
                _reasonText.text = "손패는 최대 " + GameConfig.MaxHandSize + "장 · 넘는 " + need + "장을 골라 버리세요 (보충 없음)";
                _submitButton.interactable = _discardMarks.Count == need;
                Ui.SetButtonLabel(_submitButton, "버리기 " + _discardMarks.Count + "/" + need);
                return;
            }
            if (DiscardOpen)
            {
                _previewText.text = "버릴 카드를 고르세요 (" + _discardMarks.Count + "/" + GameConfig.MaxDiscard + ")  ·  0장도 가능";
                _reasonText.text = "턴 종료 시 사용·버린 장수만큼 보충합니다"
                                 + (b.Piles.Hand.Any(b.Piles.IsExtra) ? " · 쓰지 않은 추가 카드는 버려집니다" : "");
                _submitButton.interactable = true;
                Ui.SetButtonLabel(_submitButton, "턴 종료");
                return;
            }
            var sub = CurrentSubmission();
            bool any = _useSlots.Any(c => c != null);
            bool stuck = GameConfig.AllowNoHandSubmitWhenStuck && !SimpleAi.HasAnyValidHand(b.Piles.Hand);

            if (!any)
            {
                _previewText.text = open ? (stuck ? Ui.Colored("<size=44>교체</size>", Ui.Gold) : "카드를 눌러 사용 카드 존에 올리세요 (2~5장)") : "";
                _reasonText.text = open && stuck ? "낼 수 있는 족보가 없습니다 · 2~5장을 골라 '교체'하세요" : "";
                _submitButton.interactable = false;
                Ui.SetButtonLabel(_submitButton, "공격");
                return;
            }
            if (sub == null)
            {
                _previewText.text = "1번(대표) 자리가 비어 있습니다";
                _reasonText.text = "다음에 고르는 카드가 대표 카드가 됩니다";
                _submitButton.interactable = false;
                return;
            }

            var h = HandEvaluator.Evaluate(sub);
            string reason;
            bool can = b.CanSubmit(sub, out reason);
            string exReason;
            _exchangeButton.interactable = open && b.CanSubmit(sub, out exReason, true);
            // '공격'은 족보가 될 때만. 족보 없는 조합은 '교체' 버튼으로 (막힌 턴 포함)
            _submitButton.interactable = open && can && h.IsValid;
            Ui.SetButtonLabel(_submitButton, "공격");

            // [기획서 18.3 확정] 족보 이름만 크게. 피해·계수·효과·메인 보너스 같은 계산 세부는 로그에만 남김
            if (h.IsValid) _previewText.text = Ui.Colored("<size=50>" + HandInfo.Name(h.Type) + "</size>", Ui.Gold);
            else if (stuck) _previewText.text = Ui.Colored("<size=44>교체</size>", Ui.Gold); // 막힌 턴: 족보 없이 교체
            else _previewText.text = "<size=40>족보 없음</size>";
            _reasonText.text = h.IsValid ? (can ? "" : reason)
                             : sub.Count < GameConfig.MinSubmit ? reason
                             : "'교체'를 누르면 피해 없이 카드를 바꿉니다";
        }

        // ───── 연출 (규칙 결과를 받아서 보여주기만) ─────

        private IEnumerator AnimatePlayer(AttackReport r)
        {
            if (r.IsExchange)
            {
                ShowBanner("카드 교체", Ui.TextDim);
                yield return new WaitForSeconds(0.5f);
                yield break;
            }
            Color ec = Ui.ElementColor(r.AttackElement);
            // 족보 이름 배너는 타격 직전에 짧게
            ShowBanner(HandInfo.Name(r.Hand.Type) + "!", r.Coefficient >= 2 ? Ui.Gold : ec, 0.32f); // 타격 순간에는 사라짐
            yield return new WaitForSeconds(0.3f);
            float strength = Mathf.Clamp((float)r.Coefficient, 1f, 3f) * (r.Critical ? 1.4f : 1f);
            StartCoroutine(_stage.ShakeCamera(0.04f * strength, 0.25f));
            StartCoroutine(_stage.PlayEnemyHit(Color.Lerp(ec, Color.white, 0.4f), strength));
            // 메인 피해 숫자: 에너미 위에 크게. True + 일반이 함께면 좌우로 나눠 겹치지 않게
            Vector3 hit = _stage.EnemyScreenPoint(1.0f); // 에너미 몸통 위
            bool both = r.TrueDamage > 0 && r.NormalDamage > 0;
            float sep = UnityEngine.Screen.width * 0.09f;
            if (r.TrueDamage > 0)
                DamagePopup(hit + new Vector3(both ? -sep : 0, 0, 0), r.TrueDamage, true, r.Critical, ec);
            if (r.NormalDamage > 0 || r.TrueDamage == 0)
                DamagePopup(hit + new Vector3(both ? sep : 0, both ? -UnityEngine.Screen.height * 0.05f : 0, 0), r.NormalDamage, false, r.Critical, ec);
            // 부가 효과: 더 작게, 조금 늦게, 메인 숫자와 다른 위치 (에너미 오른쪽 아래 / 플레이어 쪽)
            yield return new WaitForSeconds(0.35f);
            Vector3 side = _stage.EnemyScreenPoint(0.7f) + new Vector3(UnityEngine.Screen.width * 0.13f, 0, 0);
            float line = UnityEngine.Screen.height * 0.045f;
            int n = 0;
            if (r.AttackElement == Element.Moon)
                Popup(side - new Vector3(0, line * n++, 0), "월 → " + ElementInfo.Korean(r.EffectElement) + " 복제", Ui.ElementColor(Element.Moon), 28);
            if (r.BurnPerTick > 0) Popup(side - new Vector3(0, line * n++, 0), "화상 " + r.BurnPerTick + " ×2", Ui.ElementColor(Element.Fire), 28);
            if (r.EffectElement == Element.Metal) Popup(side - new Vector3(0, line * n++, 0), "DEF " + r.DefBefore.ToString("0.#") + " → " + r.DefUsed.ToString("0.#"), Ui.ElementColor(Element.Metal), 28);
            if (r.Healed > 0) PopupAtPlayer("+" + r.Healed, new Color(0.4f, 1f, 0.5f));
            if (r.ShieldGranted > 0) PopupAtPlayer("쉴드 +" + r.ShieldGranted, new Color(0.95f, 0.97f, 1f));
            if (r.SpeedBuff > 0) PopupAtPlayer("SPD +" + Mathf.RoundToInt((float)r.SpeedBuff * 100) + "%", Ui.ElementColor(Element.Water), 1);
            yield return new WaitForSeconds(0.45f);
        }

        private IEnumerator AnimateEnemy(EnemyActionReport r)
        {
            switch (r.Kind)
            {
                case EnemyActionKind.NormalAttack:
                case EnemyActionKind.SkillAttack:
                    yield return _stage.PlayEnemyAttack();
                    StartCoroutine(_stage.ShakeCamera(r.Kind == EnemyActionKind.SkillAttack ? 0.12f : 0.07f, 0.3f));
                    if (r.ShieldAbsorbed > 0) PopupAtPlayer("-" + r.ShieldAbsorbed, new Color(0.95f, 0.97f, 1f), 1); // 쉴드가 막은 양
                    if (r.HpLoss > 0 || r.ShieldAbsorbed == 0) PopupAtPlayer("-" + r.HpLoss, Ui.EnemyColor);
                    if (r.Critical)
                        Popup(new Vector3(UnityEngine.Screen.width * 0.16f, UnityEngine.Screen.height * 0.62f, 0), "CRITICAL", BuffColor, 60);
                    if (r.Kind == EnemyActionKind.SkillAttack) ShowBanner(r.Critical ? "스킬 공격 · CRITICAL" : "스킬 공격", Ui.EnemyColor);
                    break;
                case EnemyActionKind.Heal:
                    yield return _stage.PlayEnemyBuff(new Color(0.4f, 1f, 0.5f));
                    Popup(_stage.EnemyScreenPoint(), "+" + r.Healed, new Color(0.4f, 1f, 0.5f), 44);
                    break;
                case EnemyActionKind.DefUp:
                    yield return _stage.PlayEnemyBuff(new Color(0.6f, 0.8f, 1f));
                    Popup(_stage.EnemyScreenPoint(), "DEF +" + r.DefGained, new Color(0.6f, 0.8f, 1f), 40);
                    break;
                case EnemyActionKind.AtkUp:
                    yield return _stage.PlayEnemyBuff(new Color(1f, 0.5f, 0.3f));
                    Popup(_stage.EnemyScreenPoint(), "ATK +" + r.AtkGained, new Color(1f, 0.5f, 0.3f), 40);
                    break;
                case EnemyActionKind.CritUp:
                    yield return _stage.PlayEnemyBuff(BuffColor);
                    Popup(_stage.EnemyScreenPoint(), "치명타율 " + PlayerStats.Percent(_battle.Enemy.CritChance) + "%", BuffColor, 40);
                    break;
                case EnemyActionKind.Cleanse:
                    yield return _stage.PlayEnemyBuff(Color.white);
                    Popup(_stage.EnemyScreenPoint(), "디버프 제거", Color.white, 40);
                    break;
            }
            if (r.BurnDamage > 0)
            {
                yield return new WaitForSeconds(0.3f);
                StartCoroutine(_stage.PlayEnemyHit(Ui.ElementColor(Element.Fire), 0.6f));
                Popup(_stage.EnemyScreenPoint(1.0f), "화상 " + r.BurnDamage, Ui.ElementColor(Element.Fire), 40);
            }
            yield return new WaitForSeconds(0.35f);
        }

        /// <param name="lane">0 = 기본 위치, 1 = 조금 오른쪽 (두 숫자가 겹치지 않게)</param>
        private void PopupAtPlayer(string text, Color color, int lane = 0)
        {
            // 플레이어 패널 위쪽 (화면 좌하단)
            var screenPos = new Vector3(UnityEngine.Screen.width * (0.09f + 0.07f * lane), UnityEngine.Screen.height * 0.5f, 0);
            Popup(screenPos, text, color, 40);
        }

        /// <summary>
        /// 메인 피해 숫자 (기획서 18.3). 피해량 단계별 크기(50 미만 / 50~149 / 150 이상), True = 금색 + 'TRUE',
        /// 치명타 = 더 크고 붉게 + 'CRITICAL', 외곽선은 대표 카드 속성 색. 스케일 펀치 후 위로 떠오르며 사라짐.
        /// 결과값을 보여 주기만 하며, 진행은 이 연출을 기다리지 않음.
        /// </summary>
        private void DamagePopup(Vector3 screenPos, int amount, bool trueDamage, bool crit, Color elementColor)
        {
            int size = amount >= 150 ? 108 : amount >= 50 ? 84 : 64;
            if (crit) size = Mathf.RoundToInt(size * 1.25f);
            Color color = trueDamage ? Ui.Gold : crit ? new Color(1f, 0.32f, 0.26f) : Color.white;
            string caption = crit && trueDamage ? "CRITICAL · TRUE" : crit ? "CRITICAL" : trueDamage ? "TRUE" : null;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screenPos, null, out local);
            // 화면 밖으로 나가지 않고, 떠오른 뒤에도 위쪽 에너미 정보 패널(화면 위에서 232)과 겹치지 않게 제한
            var half = _canvas.rect.size * 0.5f;
            float h = size * 1.2f + (caption != null ? 44 : 0);
            float top = half.y - EnemyPanelBottomFromTop - DamageFloatHeight - h * 0.5f - 8;
            local.x = Mathf.Clamp(local.x, -half.x + 260, half.x - 260);
            local.y = Mathf.Clamp(local.y, -half.y + h, top);

            var rt = Ui.MakeRect(_canvas, "Damage", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), local, new Vector2(520, h));
            var num = Ui.Label(rt, "N", amount.ToString(), size, color, TextAnchor.LowerCenter);
            num.horizontalOverflow = HorizontalWrapMode.Overflow;
            var edge = num.gameObject.AddComponent<Outline>();   // 속성 색 외곽선
            edge.effectColor = new Color(elementColor.r, elementColor.g, elementColor.b, 0.95f);
            edge.effectDistance = new Vector2(4, -4);
            Ui.AddOutline(num, 2f);                               // 안쪽 검은 외곽선 (가독성)
            var texts = new System.Collections.Generic.List<Text> { num };
            if (caption != null)
            {
                var cap = Ui.Label(rt, "C", caption, Mathf.RoundToInt(size * 0.38f), crit ? new Color(1f, 0.32f, 0.26f) : Ui.Gold, TextAnchor.UpperCenter);
                cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                Ui.AddOutline(cap, 2f);
                texts.Add(cap);
            }
            StartCoroutine(PunchFloatFade(rt, texts));
            Destroy(rt.gameObject, 1.8f); // 전투 재시작(StopAllCoroutines)으로 연출이 끊겨도 남지 않게
        }

        private const float EnemyPanelBottomFromTop = 82 + 150; // BuildBattle의 EnemyPanel 위치·높이
        private const float DamageFloatHeight = 80f;

        private static IEnumerator PunchFloatFade(RectTransform rt, System.Collections.Generic.List<Text> texts)
        {
            const float dur = 1.3f;
            float time = 0f;
            Vector2 start = rt.anchoredPosition;
            var c0 = texts.ConvertAll(t => t.color);
            while (time < dur)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / dur);
                // 스케일 펀치: 1.7배로 나타나 0.14초 만에 0.92 → 0.26초에 1.0
                float scale = time < 0.14f ? Mathf.Lerp(1.7f, 0.92f, time / 0.14f)
                            : time < 0.26f ? Mathf.Lerp(0.92f, 1f, (time - 0.14f) / 0.12f) : 1f;
                rt.localScale = Vector3.one * scale;
                rt.anchoredPosition = start + new Vector2(0, DamageFloatHeight * (1f - (1f - k) * (1f - k))); // 감속하며 떠오름
                float a = 1f - Mathf.Clamp01((k - 0.55f) / 0.45f);
                for (int i = 0; i < texts.Count; i++) texts[i].color = new Color(c0[i].r, c0[i].g, c0[i].b, a);
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        private void Popup(Vector3 screenPos, string text, Color color, int size)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screenPos, null, out local);
            var rt = Ui.MakeRect(_canvas, "Popup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), local, new Vector2(600, 90));
            var t = Ui.Label(rt, "T", text, size, color);
            Ui.AddOutline(t, 3f);
            StartCoroutine(FloatAndFade(rt, t));
            Destroy(rt.gameObject, 1.6f); // 전투 재시작(StopAllCoroutines)으로 연출이 끊겨도 남지 않게
        }

        private static IEnumerator FloatAndFade(RectTransform rt, Text t)
        {
            float time = 0f;
            Vector2 start = rt.anchoredPosition;
            Color c0 = t.color;
            while (time < 1.1f)
            {
                time += Time.deltaTime;
                float k = time / 1.1f;
                rt.anchoredPosition = start + new Vector2(0, 90f * k);
                t.color = new Color(c0.r, c0.g, c0.b, 1f - Mathf.Clamp01((k - 0.5f) * 2f));
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        private void ShowBanner(string text, Color color, float duration = 1f)
        {
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(BannerRoutine(text, color, duration));
        }

        private IEnumerator BannerRoutine(string text, Color color, float duration)
        {
            _banner.transform.SetAsLastSibling();
            _banner.text = text;
            float t = 0f;
            float fadeIn = duration * 0.15f, fadeOutStart = duration * 0.6f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = t < fadeIn ? t / fadeIn : 1f - Mathf.Clamp01((t - fadeOutStart) / (duration - fadeOutStart));
                _banner.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }
            _banner.text = "";
        }

        // ═══════════════════════════ 결과 ═══════════════════════════

        private void BuildResult()
        {
            _resultScreen = Ui.Stretch(_canvas, "Result");
            Ui.Panel(_resultScreen, new Color(0, 0, 0, 0.72f), true);
            var box = Ui.MakeRect(_resultScreen, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  Vector2.zero, new Vector2(900, 520));
            Ui.Panel(box, Ui.PanelLight);
            _resultTitle = Ui.Label(box, "Title", "", 64, Ui.Gold, TextAnchor.UpperCenter, 30f);
            Ui.AddOutline(_resultTitle, 3f);
            _resultBody = Ui.Label(box, "Body", "", 28, Color.white, TextAnchor.MiddleCenter, 40f);
            ((RectTransform)_resultBody.transform).offsetMin = new Vector2(40, 215);
            ((RectTransform)_resultBody.transform).offsetMax = new Vector2(-40, -110);
            _resultPrimary = Ui.MakeButton(box, "Primary", "", new Vector2(0.5f, 0), new Vector2(-170, 80), new Vector2(300, 84),
                new Color(0.2f, 0.55f, 0.3f), 32, null);
            _resultSecondary = Ui.MakeButton(box, "Secondary", "", new Vector2(0.5f, 0), new Vector2(170, 80), new Vector2(300, 84),
                new Color(0.35f, 0.3f, 0.4f), 32, null);
            _resultStats = Ui.MakeButton(box, "Stats", "스탯", new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(640, 64),
                new Color(0.45f, 0.35f, 0.15f), 30, ShowStats);
            _resultScreen.gameObject.SetActive(false);

            // 속성 강화 선택 (보스 첫 처치 보상): 결과 상자 위에 불투명하게 덮음. 고르기 전에는 다음 층으로 갈 수 없음
            _boostScreen = Ui.Stretch(_canvas, "Boost");
            Ui.Panel(_boostScreen, new Color(0.02f, 0.02f, 0.04f, 1f), true);
            var bt = Ui.Label(_boostScreen, "Title", "보스 처치 보상 · 속성 강화", 56, Ui.Gold, TextAnchor.UpperCenter, 60f);
            Ui.AddOutline(bt, 3f);
            var bs = Ui.Label(_boostScreen, "Sub", "1개를 고르세요 · 그 속성 카드를 대표 카드로 공격하면 피해 +"
                              + PlayerStats.Percent(GameConfig.ElementBoostPerStack) + "% (중첩 가능)", 30, Ui.TextDim, TextAnchor.UpperCenter, 150f);
            for (int i = 0; i < GameConfig.ElementBoostChoices; i++)
            {
                int idx = i;
                var slot = Ui.MakeRect(_boostScreen, "Choice" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2((i - 1) * 420, -20), new Vector2(360, 520));
                var btn = slot.gameObject.AddComponent<Button>();
                btn.targetGraphic = Ui.Panel(slot, Ui.PanelLight, true);
                btn.onClick.AddListener(() => OnBoostChosen(idx));
                _boostCards[i] = new CardWidget(slot, "Card", new Vector2(0.5f, 1), new Vector2(0, -190), 340f, null);
                _boostLabels[i] = Ui.Label(slot, "Label", "", 30, Color.white, TextAnchor.LowerCenter, 24f);
                Ui.AddOutline(_boostLabels[i], 2f);
            }
            _boostScreen.gameObject.SetActive(false);
        }

        private RectTransform _boostScreen;
        private readonly CardWidget[] _boostCards = new CardWidget[GameConfig.ElementBoostChoices];
        private readonly Text[] _boostLabels = new Text[GameConfig.ElementBoostChoices];

        private void RefreshBoostChoice()
        {
            var pend = _progress.PendingElementChoices;
            _boostScreen.gameObject.SetActive(pend.Count > 0);
            if (pend.Count == 0) return;
            _boostScreen.SetAsLastSibling();
            for (int i = 0; i < _boostCards.Length; i++)
            {
                var e = pend[i];
                int now = _progress.Stats.ElementBoosts[(int)e];
                _boostCards[i].SetElementOnly(e);
                _boostLabels[i].text = Ui.Colored(ElementInfo.Korean(e) + "속성", Ui.ElementColor(e)) + "\n피해 +"
                    + PlayerStats.Percent(GameConfig.ElementBoostPerStack * now) + "% → "
                    + Ui.Colored("+" + PlayerStats.Percent(GameConfig.ElementBoostPerStack * (now + 1)) + "%", Ui.Gold);
            }
        }

        private void OnBoostChosen(int idx)
        {
            var pend = _progress.PendingElementChoices;
            if (idx >= pend.Count) return;
            var e = pend[idx];
            _progress.ChooseElementBoost(e);
            SaveProgress();
            RefreshBoostChoice();
            RefreshTower();
            ShowBanner(ElementInfo.Korean(e) + "속성 강화!", Ui.ElementColor(e));
            _banner.transform.SetAsLastSibling();
        }

        private void ShowResult()
        {
            _resultScreen.gameObject.SetActive(true);
            _resultScreen.SetAsLastSibling();
            var b = _battle;
            _resultPrimary.onClick.RemoveAllListeners();
            _resultSecondary.onClick.RemoveAllListeners();
            string stats = "내 행동 " + b.PlayerTurnCount + "회 · 적 행동 " + b.EnemyTurnCount + "회 · 남은 HP " + b.Player.Hp + "/" + b.Player.MaxHp;

            if (b.Phase == BattlePhase.Victory)
            {
                _resultTitle.text = b.Setup.Floor + "층 클리어!";
                int awarded = _progress.AwardClear(b.Setup.Floor, b.Setup.Seed); // [확정 v1.3] 일반 1 · 엘리트 2 · 보스 3, 처음 클리어할 때만 (보스는 속성 강화 후보도)
                SaveProgress();
                string tier = b.Enemy.Tier == EnemyTier.Normal ? "" : " (" + EnemyTierInfo.Korean(b.Enemy.Tier) + " 처치)";
                _resultBody.text = stats + "\n"
                    + (awarded > 0 ? Ui.Colored("스탯 포인트 +" + awarded + tier, Ui.Gold) : Ui.Colored("이미 포인트를 받은 층입니다", Ui.TextDim))
                    + "\n" + Ui.Colored("보유 포인트 " + _progress.UnspentPoints + "점 · 쓰지 않은 포인트는 계속 쌓입니다", Ui.TextDim);
                _resultStats.gameObject.SetActive(true);
                RefreshStatsButton(_resultStats, "스탯");
                _floor = b.Setup.Floor + 1;
                PlayerPrefs.SetInt(SaveKeyFloor, _floor);
                PlayerPrefs.Save();
                Ui.SetButtonLabel(_resultPrimary, "탑으로");
                _resultPrimary.onClick.AddListener(ShowTower);
                Ui.SetButtonLabel(_resultSecondary, "타이틀");
                _resultSecondary.onClick.AddListener(ShowTitle);
            }
            else
            {
                _resultTitle.text = b.Setup.Floor + "층에서 쓰러졌습니다";
                // 편성 화면이 없어져 스탯은 결과 화면에서만 배분 → 패배 때도 버튼 표시
                _resultStats.gameObject.SetActive(true);
                RefreshStatsButton(_resultStats, "스탯");
                _resultTitle.color = Ui.EnemyColor;
                _resultBody.text = stats + "\n\n" + Ui.Colored("재도전 규칙은 기획서에서 미정이라, 같은 층을 다시 시작합니다. (속성은 새로 정해집니다)", Ui.TextDim);
                Ui.SetButtonLabel(_resultPrimary, "다시 도전");
                _resultPrimary.onClick.AddListener(StartStage);
                Ui.SetButtonLabel(_resultSecondary, "탑으로");
                _resultSecondary.onClick.AddListener(ShowTower);
            }
            if (b.Phase == BattlePhase.Victory) _resultTitle.color = Ui.Gold;
        }

        // ═══════════════════════════ 묘지 (09.3 [확정] 클릭 시 전체 확인) ═══════════════════════════

        private void BuildGraveyard()
        {
            _graveScreen = Ui.Stretch(_canvas, "Graveyard");
            Ui.Panel(_graveScreen, new Color(0.02f, 0.02f, 0.04f, 1f), true); // 불투명: Linear 색 공간에서는 반투명이면 뒤 화면이 비쳐 보임
            _graveTitle = Ui.Label(_graveScreen, "Title", "", 44, Color.white, TextAnchor.UpperCenter, 30f);
            _graveGrid = Ui.MakeRect(_graveScreen, "Grid", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                     new Vector2(0, -10), new Vector2(1500, 800));
            Ui.MakeButton(_graveScreen, "Close", "닫기", new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(240, 70),
                new Color(0.3f, 0.3f, 0.4f), 30, () => _graveScreen.gameObject.SetActive(false));
            _graveScreen.gameObject.SetActive(false);
        }

        private void ShowGraveyard()
        {
            if (_battle == null) return;
            for (int i = _graveGrid.childCount - 1; i >= 0; i--) Destroy(_graveGrid.GetChild(i).gameObject);
            // [가정] 정렬: 속성 순 → 랭크 순 (09.3 정렬 기준 미정)
            var cards = _battle.Piles.Graveyard.OrderBy(c => (int)c.Element).ThenBy(c => c.Rank).ToList();
            _graveTitle.text = "묘지 " + cards.Count + "장";
            const int perRow = 13;
            for (int i = 0; i < cards.Count; i++)
            {
                int row = i / perRow, col = i % perRow;
                var w = new CardWidget(_graveGrid, "G" + i, new Vector2(0, 1), new Vector2(60 + col * 112, -80 - row * 152),
                                       140f, null, true, true);
                w.SetCard(cards[i]);
            }
            _graveScreen.gameObject.SetActive(true);
            _graveScreen.SetAsLastSibling();
        }
        // ═══════════════════════════ 스탯 (v1.2) ═══════════════════════════

        private static readonly Color BuffColor = new Color(1f, 0.55f, 0.25f);       // 에너미 버프·에너미 치명타 (주황)

        private static Color TierColor(EnemyTier t)
        {
            return t == EnemyTier.Boss ? Ui.EnemyColor : t == EnemyTier.Elite ? new Color(0.85f, 0.55f, 1f) : Color.white;
        }

        private static string FloorLabel(int floor)
        {
            var t = GameConfig.TierOf(floor);
            return floor + "층" + (t != EnemyTier.Normal ? " · " + EnemyTierInfo.Korean(t) : "");
        }

        private void SaveProgress()
        {
            PlayerPrefs.SetString(SaveKeyProgress, _progress.Serialize());
            PlayerPrefs.Save();
        }

        private void RefreshStatsButton(Button b, string label)
        {
            int n = _progress.UnspentPoints;
            Ui.SetButtonLabel(b, n > 0 ? label + " (포인트 " + n + ")" : label);
            b.GetComponent<Image>().color = n > 0 ? new Color(0.75f, 0.55f, 0.15f) : new Color(0.32f, 0.3f, 0.36f);
        }

        private readonly Button[] _statPlus = new Button[PlayerStats.Count];
        private readonly Button[] _statMinus = new Button[PlayerStats.Count];

        private void BuildStats()
        {
            _statScreen = Ui.Stretch(_canvas, "Stats");
            Ui.Panel(_statScreen, new Color(0.02f, 0.02f, 0.04f, 1f), true); // 불투명: Linear 색 공간에서는 반투명이면 뒤 글자가 비쳐 보임
            var title = Ui.Label(_statScreen, "Title", "스탯", 56, Ui.Gold, TextAnchor.UpperCenter, 40f);
            Ui.AddOutline(title, 3f);
            _statRemain = Ui.Label(_statScreen, "Remain", "", 30, Color.white);
            ((RectTransform)_statRemain.transform).anchorMin = new Vector2(0, 0.78f);
            ((RectTransform)_statRemain.transform).anchorMax = new Vector2(1, 0.87f);

            for (int i = 0; i < PlayerStats.Count; i++)
            {
                int idx = i;
                var row = Ui.MakeRect(_statScreen, "Row" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      new Vector2(0, 235 - i * 88), new Vector2(1400, 78));
                Ui.Panel(row, Ui.PanelLight);
                _statRows[i] = Ui.Label(row, "Text", "", 26, Color.white, TextAnchor.MiddleLeft, 24f);
                _statMinus[i] = Ui.MakeButton(row, "Minus", "−", new Vector2(1, 0.5f), new Vector2(-170, 0), new Vector2(84, 64),
                    new Color(0.35f, 0.3f, 0.4f), 40, () => { _statPending[idx]--; RefreshStats(); });
                _statPlus[i] = Ui.MakeButton(row, "Plus", "+", new Vector2(1, 0.5f), new Vector2(-66, 0), new Vector2(84, 64),
                    new Color(0.2f, 0.55f, 0.3f), 40, () => { _statPending[idx]++; RefreshStats(); });
            }

            var bottom = new Vector2(0.5f, 0.1f);
            Ui.MakeButton(_statScreen, "Cancel", "취소", bottom, new Vector2(-180, 0), new Vector2(300, 80),
                new Color(0.35f, 0.3f, 0.4f), 32, CloseStats);
            _statConfirm = Ui.MakeButton(_statScreen, "Confirm", "확정", bottom, new Vector2(180, 0), new Vector2(300, 80),
                new Color(0.75f, 0.55f, 0.15f), 34, ConfirmStats);
            _statScreen.gameObject.SetActive(false);
        }

        private void ShowStats()
        {
            System.Array.Clear(_statPending, 0, _statPending.Length);
            RefreshStats();
            _statScreen.gameObject.SetActive(true);
            _statScreen.SetAsLastSibling();
        }

        /// <summary>
        /// [확정 v1.3] N레벨에 N점. 미리보기 비용은 매번 Core(PlayerProgress.CostOf)로 처음부터 다시 계산하므로
        /// +/−를 여러 번 오가도 레벨별 비용이 정확히 쓰이고 환불됨.
        /// </summary>
        private void RefreshStats()
        {
            int spent = _progress.CostOf(_statPending);
            int remain = _progress.UnspentPoints - spent;
            _statRemain.text = "남은 포인트 " + Ui.Colored(remain.ToString(), Ui.Gold)
                             + (spent > 0 ? "   ·   이번 배분 " + spent + "점" : "")
                             + "\n" + Ui.Colored("같은 스탯은 레벨마다 비용이 1점씩 늘어납니다 (N레벨 = N점). '확정'하면 되돌릴 수 없습니다.", Ui.TextDim);
            var now = _progress.Stats;
            var after = now.With(_statPending);
            var green = new Color(0.5f, 1f, 0.6f);
            foreach (var k in PlayerStats.All)
            {
                int i = (int)k;
                int next = _progress.NextLevelCost(k, _statPending[i]);
                bool canBuy = remain >= next;
                string level = "Lv " + now[k] + (_statPending[i] > 0 ? Ui.Colored(" → " + after[k], green) : "");
                string value = _statPending[i] > 0 ? now.Value(k) + " → " + Ui.Colored(after.Value(k), green) : now.Value(k);
                string cost = canBuy ? "다음 레벨 " + next + "점"
                                     : Ui.Colored("포인트 부족 (다음 레벨 " + next + "점)", Ui.EnemyColor);
                _statRows[i].text = Ui.Colored(PlayerStats.Name(k), Ui.Gold) + " (레벨당 " + PlayerStats.PerPoint(k) + ")"
                                  + "    " + level + "    현재 " + value + "    " + cost;
                _statPlus[i].interactable = canBuy;
                _statMinus[i].interactable = _statPending[i] > 0;
            }
            _statConfirm.interactable = spent > 0;
        }

        private void ConfirmStats()
        {
            string reason;
            if (!_progress.CanCommit(_statPending, out reason)) { ShowBanner(reason, Ui.Gold); return; }
            _progress.Commit((int[])_statPending.Clone());
            SaveProgress();
            CloseStats();
        }

        private void CloseStats()
        {
            System.Array.Clear(_statPending, 0, _statPending.Length);
            _statScreen.gameObject.SetActive(false);
            RefreshStatsButton(_resultStats, "스탯");
            if (_towerScreen.gameObject.activeSelf) RefreshTower();
        }

        // ═══════════════════════════ 탑 로드맵 ═══════════════════════════
        // 층을 탑 모양으로 쌓아 보여 주고, 층을 누르면 그 층 에너미 정보를 보여 줌. 도전은 현재 층만.
        // 보스 보상(속성 강화)을 고르지 않았으면 이 화면에 들어올 때 먼저 고르게 함 → 다음 층 진입 전에 반드시 선택.

        private const int TowerSection = 10; // 한 화면에 보이는 층 수 (보스 주기와 같음)
        private RectTransform _towerScreen;
        private readonly Button[] _towerFloors = new Button[TowerSection];
        private readonly Text[] _towerFloorText = new Text[TowerSection];
        private Text _towerInfo, _towerHeader;
        private Button _towerChallenge, _towerStats, _towerPrev;
        private int _towerBase = 1;      // 지금 보이는 구간의 첫 층
        private int _towerSelected = 1;

        private void BuildTower()
        {
            _towerScreen = MakeScreen("Tower");
            Ui.Panel(_towerScreen, new Color(0.03f, 0.03f, 0.06f, 1f), true);
            _towerHeader = Ui.Label(_towerScreen, "Title", "", 52, Ui.Gold, TextAnchor.UpperCenter, 30f);
            Ui.AddOutline(_towerHeader, 3f);

            // 탑: 아래가 낮은 층. 위로 갈수록 살짝 좁아지는 계단 모양
            var c = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < TowerSection; i++)
            {
                int idx = i;
                float w = 560 - i * 18;
                _towerFloors[i] = Ui.MakeButton(_towerScreen, "Floor" + i, "", c, new Vector2(-430, -380 + i * 80), new Vector2(w, 72),
                                                Ui.PanelLight, 28, () => { _towerSelected = _towerBase + idx; RefreshTower(); });
                _towerFloorText[i] = _towerFloors[i].GetComponentInChildren<Text>();
                _towerFloorText[i].supportRichText = true;
            }
            Ui.MakeButton(_towerScreen, "Next", "▲ 위 10층", c, new Vector2(-430, 430), new Vector2(240, 48),
                new Color(0.25f, 0.25f, 0.35f), 24, () => { _towerBase += TowerSection; _towerSelected = _towerBase; RefreshTower(); });
            _towerPrev = Ui.MakeButton(_towerScreen, "Prev", "▼ 아래 10층", c, new Vector2(-430, -470), new Vector2(240, 48),
                new Color(0.25f, 0.25f, 0.35f), 24, () => { _towerBase -= TowerSection; _towerSelected = _towerBase + TowerSection - 1; RefreshTower(); });

            // 층 정보
            var info = Ui.MakeRect(_towerScreen, "Info", c, c, c, new Vector2(400, 60), new Vector2(860, 680));
            Ui.Panel(info, Ui.PanelLight);
            _towerInfo = Ui.Label(info, "Text", "", 30, Color.white, TextAnchor.UpperLeft, 36f);

            _towerChallenge = Ui.MakeButton(_towerScreen, "Challenge", "도전", c, new Vector2(400, -385), new Vector2(420, 100),
                new Color(0.75f, 0.3f, 0.2f), 40, () => { if (_progress.PendingElementChoices.Count == 0 && _towerSelected == _floor) StartStage(); });
            _towerStats = Ui.MakeButton(_towerScreen, "Stats", "스탯", c, new Vector2(790, -385), new Vector2(300, 80),
                new Color(0.45f, 0.35f, 0.15f), 28, ShowStats);
            Ui.MakeButton(_towerScreen, "Title", "타이틀", c, new Vector2(30, -385), new Vector2(240, 80),
                new Color(0.35f, 0.3f, 0.4f), 28, ShowTitle);
        }

        private void ShowTower()
        {
            ShowOnly(_towerScreen);
            _towerBase = (_floor - 1) / TowerSection * TowerSection + 1;
            _towerSelected = _floor;
            RefreshTower();
            RefreshBoostChoice(); // 보스 보상을 아직 안 골랐으면 먼저 선택 (껐다 켠 경우 포함)
        }

        private void RefreshTower()
        {
            _towerHeader.text = "탑 · " + _towerBase + "~" + (_towerBase + TowerSection - 1) + "층";
            _towerPrev.gameObject.SetActive(_towerBase > 1);
            for (int i = 0; i < TowerSection; i++)
            {
                int f = _towerBase + i;
                var tier = GameConfig.TierOf(f);
                bool cleared = f <= _progress.HighestClearedFloor;
                bool current = f == _floor;
                string state = cleared ? Ui.Colored("   클리어", new Color(0.5f, 1f, 0.6f))
                             : current ? Ui.Colored("   ◀ 도전", Ui.Gold) : Ui.Colored("   잠김", Ui.TextDim);
                _towerFloorText[i].text = Ui.Colored(FloorLabel(f), TierColor(tier)) + state;
                _towerFloors[i].GetComponent<Image>().color = f == _towerSelected ? new Color(0.18f, 0.3f, 0.55f)
                    : current ? new Color(0.32f, 0.22f, 0.12f)
                    : cleared ? new Color(0.14f, 0.22f, 0.17f) : new Color(0.13f, 0.14f, 0.19f);
            }
            _towerInfo.text = FloorInfo(_towerSelected);
            bool pending = _progress.PendingElementChoices.Count > 0;
            _towerChallenge.interactable = _towerSelected == _floor && !pending;
            Ui.SetButtonLabel(_towerChallenge, _towerSelected == _floor ? _floor + "층 도전" : "현재 층만 도전 가능");
            RefreshStatsButton(_towerStats, "스탯");
        }

        /// <summary>층 정보 (표시 전용). 수치는 Core의 에너미 생성 규칙(Battle.CreateEnemy)을 그대로 씀.</summary>
        private string FloorInfo(int floor)
        {
            var en = Battle.CreateEnemy(floor);
            bool special = en.Tier != EnemyTier.Normal;
            var sb = new System.Text.StringBuilder();
            string tierTag = special ? "[" + EnemyTierInfo.Korean(en.Tier) + "] " : "";
            sb.Append(Ui.Colored("<size=42>" + tierTag + en.Name + "</size>", TierColor(en.Tier))).Append("\n\n");
            sb.Append("HP " + en.MaxHp + "    ATK " + en.Atk + "    DEF " + en.BaseDef.ToString("0.#") + "    SPD " + en.BaseSpeed.ToString("0")).Append("\n\n");
            sb.Append(Ui.Colored("행동", Ui.Gold)).Append("\n");
            sb.Append("· 일반 공격 · 스킬 공격 (ATK ×" + GameConfig.EnemySkillMultiplier.ToString("0.#") + ")\n");
            sb.Append("· 회복 (HP 70% 미만일 때) · 방어력 증가\n");
            if (special)
                sb.Append("· 디버프 제거 · 공격력 증가 · 치명타율 증가 (최대 " + PlayerStats.Percent(GameConfig.EnemyCritMax) + "%)\n");
            sb.Append("\n").Append(Ui.Colored("첫 클리어 보상", Ui.Gold)).Append("\n");
            if (floor <= _progress.HighestClearedFloor) sb.Append(Ui.Colored("이미 받음", Ui.TextDim));
            else
            {
                sb.Append("스탯 포인트 +" + GameConfig.StatPointsFor(floor));
                if (en.Tier == EnemyTier.Boss) sb.Append("  ·  속성 강화 (3개 중 1개 선택)");
            }
            sb.Append("\n\n").Append(Ui.Colored("스테이지 속성 5개와 메인 속성은 입장할 때 무작위로 정해집니다.", Ui.TextDim));
            return sb.ToString();
        }
    }
}
