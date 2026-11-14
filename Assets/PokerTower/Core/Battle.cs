using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerTower.Core
{
    /// <summary>
    /// PlayerDiscard = 공격 처리 후 '버릴 카드 선택' 단계 (v1.2 [확정]).
    /// PlayerForcedDiscard = 보충 후 손패가 10장을 넘어 넘는 장수만큼 버리는 단계 (v1.3 [확정]).
    /// BetweenTurns = 턴이 끝나고 NextTurn() 대기.
    /// </summary>
    public enum BattlePhase { NotStarted, PlayerTurn, PlayerDiscard, PlayerForcedDiscard, BetweenTurns, EnemyTurn, Victory, Defeat }

    public enum EnemyActionKind { NormalAttack, SkillAttack, Heal, DefUp, AtkUp, Cleanse, CritUp }

    public sealed class EnemyActionReport
    {
        public EnemyActionKind Kind;
        public int RawDamage;
        public int ShieldAbsorbed;
        public int HpLoss;
        public int Healed;
        public double DefGained;
        public int AtkGained;
        public double CritGained;
        /// <summary>이번 공격에 치명타가 발동했는지 (원 피해 × 1.5).</summary>
        public bool Critical;
        public bool CleansedBurn;
        public bool CleansedMetal;
        public int BurnDamage;     // 행동 종료 직전 화상 피해
        public int BurnTicksLeft;
    }

    /// <summary>스테이지 1개의 설정 (층, 5속성, 메인 속성).</summary>
    public sealed class StageSetup
    {
        public int Floor = 1;
        public List<Element> Elements = new List<Element>();
        public Element MainElement;
        public int Seed;

        /// <summary>
        /// [확정 2026-11-14 사용자 결정] 스테이지 진입 시 시스템이 7속성 중 5개를 무작위로 고르고, 그중 메인 속성도 무작위로 정함.
        /// 같은 시드면 같은 결과 (덱 섞기와 다른 난수열을 쓰도록 시드를 변형).
        /// </summary>
        public static StageSetup CreateRandom(int floor, int seed)
        {
            var rng = new GameRng(seed ^ 0x5BD1E995);
            var all = ElementInfo.All.ToList();
            rng.Shuffle(all);
            var picked = all.Take(GameConfig.StageElementCount).OrderBy(e => (int)e).ToList();
            return new StageSetup { Floor = floor, Seed = seed, Elements = picked, MainElement = picked[rng.Range(0, picked.Count)] };
        }

        public void Validate()
        {
            if (Elements.Count != GameConfig.StageElementCount || Elements.Distinct().Count() != GameConfig.StageElementCount)
                throw new ArgumentException("스테이지 속성은 서로 다른 5개여야 합니다.");
            if (!Elements.Contains(MainElement))
                throw new ArgumentException("메인 속성은 선택한 5속성 중 하나여야 합니다.");
        }
    }

    /// <summary>
    /// 전투 1회의 상태와 진행. UI는 이 클래스의 결과만 표시하고 규칙 계산은 하지 않습니다.
    /// 진행: Begin() → 반복 { NextTurn() → Player면 Submit() [→ PlayerDiscard면 EndPlayerTurn(버릴 카드)],
    ///                          Enemy면 RunEnemyTurn() } → Victory/Defeat
    /// </summary>
    public sealed class Battle
    {
        public readonly StageSetup Setup;
        public readonly PlayerUnit Player;
        public readonly EnemyUnit Enemy;
        public readonly CardPiles Piles;
        public readonly TurnScheduler Scheduler;
        public readonly GameRng Rng;
        public readonly List<string> Log = new List<string>();

        public BattlePhase Phase { get; private set; }
        public int PlayerTurnCount { get; private set; }
        public int EnemyTurnCount { get; private set; }
        /// <summary>마지막 행동 유닛 (연속 행동 표시용).</summary>
        public Actor? LastActor { get; private set; }
        public bool IsOver { get { return Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat; } }

        private int _pendingShield; // 이번 플레이어 턴 종료에 부여할 토 쉴드
        private int _usedThisAction; // 이번 행동에 사용한 장수 (턴 종료 보충량 계산)

        /// <param name="stats">스탯 포인트. null이면 투자 없음.</param>
        public Battle(StageSetup setup, PlayerStats stats = null)
        {
            setup.Validate();
            Setup = setup;
            Rng = new GameRng(setup.Seed);
            Player = CreatePlayer(stats);
            Enemy = CreateEnemy(setup.Floor);
            Piles = new CardPiles(setup.Elements, Rng);
            Scheduler = new TurnScheduler(Player, Enemy);
            Phase = BattlePhase.NotStarted;
        }

        /// <summary>[확정 v1.2] 초기 HP 200 / DEF 10, 스탯 포인트 반영. [가정] SPD 100.</summary>
        public static PlayerUnit CreatePlayer(PlayerStats stats = null)
        {
            var s = stats ?? new PlayerStats();
            return new PlayerUnit
            {
                Name = "플레이어",
                MaxHp = s.MaxHp,
                Hp = s.MaxHp,
                BaseDef = s.Def,
                BaseSpeed = s.Speed,
                DamageMultiplier = s.DamageMultiplier,
                ExtraDrawChance = s.ExtraDrawChance,
                CritChance = s.CritChance,
                ElementBoosts = (int[])s.ElementBoosts.Clone()
            };
        }

        /// <summary>[확정] 1층 HP300/ATK10/DEF0, 층마다 증가. [가정] 성장률·엘리트·보스 배율은 GameConfig.</summary>
        public static EnemyUnit CreateEnemy(int floor)
        {
            int f = Math.Max(1, floor) - 1;
            var tier = GameConfig.TierOf(floor);
            double hp = GameConfig.EnemyBaseHp * (1.0 + GameConfig.EnemyHpGrowthPerFloor * f);
            double atk = GameConfig.EnemyBaseAtk * (1.0 + GameConfig.EnemyAtkGrowthPerFloor * f);
            double def = GameConfig.EnemyBaseDef + GameConfig.EnemyDefGrowthPerFloor * f;
            if (tier == EnemyTier.Boss)
            {
                hp *= GameConfig.BossHpMultiplier;
                atk *= GameConfig.BossAtkMultiplier;
            }
            else if (tier == EnemyTier.Elite)
            {
                hp *= GameConfig.EliteHpMultiplier;
                atk *= GameConfig.EliteAtkMultiplier;
            }
            int maxHp = (int)Math.Round(hp);
            int atkInt = (int)Math.Round(atk);
            return new EnemyUnit
            {
                Name = floor + "층 에너미", // 등급은 화면에서 [엘리트]·[보스]로 붙여 표시
                Tier = tier,
                MaxHp = maxHp,
                Hp = maxHp,
                Atk = atkInt,
                BaseAtk = atkInt,
                BaseDef = def,
                BaseSpeed = tier == EnemyTier.Boss ? GameConfig.BossSpeed : GameConfig.EnemyBaseSpeed
            };
        }

        /// <summary>[가정] 전투 시작 시 7장 드로우 후 첫 행동권 결정.</summary>
        public void Begin()
        {
            if (Phase != BattlePhase.NotStarted) throw new InvalidOperationException("이미 시작된 전투입니다.");
            Piles.Draw(GameConfig.HandSize);
            AddLog(Setup.Floor + "층 전투 시작. 메인 속성: " + ElementInfo.Korean(Setup.MainElement));
            Phase = BattlePhase.BetweenTurns; // NextTurn()에서 실제 행동권 결정
        }

        /// <summary>다음 행동권을 결정합니다. Player면 입력 대기, Enemy면 RunEnemyTurn()을 호출하세요.</summary>
        public Actor NextTurn()
        {
            if (IsOver) throw new InvalidOperationException("전투가 끝났습니다.");
            Actor a = Scheduler.Advance();
            Phase = a == Actor.Player ? BattlePhase.PlayerTurn : BattlePhase.EnemyTurn;
            return a;
        }

        public List<Actor> PreviewTurns(int count) { return Scheduler.Preview(count); }

        // ───────────────────── 플레이어 ─────────────────────

        /// <summary>제출 가능 여부와 불가 사유.</summary>
        /// <param name="exchange">[사용자 결정 2026-11-14 A1] '교체' 제출. 족보가 있어도 언제나 허용 (피해 0, 효과 없음, 버리기 단계 없음).</param>
        public bool CanSubmit(IList<Card> ordered, out string reason, bool exchange = false)
        {
            reason = "";
            if (Phase != BattlePhase.PlayerTurn) { reason = "플레이어 차례가 아닙니다."; return false; }
            if (ordered == null || ordered.Count < GameConfig.MinSubmit) { reason = "카드를 2장 이상 올려 주세요."; return false; }
            if (ordered.Count > GameConfig.MaxSubmit) { reason = "최대 5장까지 낼 수 있습니다."; return false; }
            if (ordered.Distinct().Count() != ordered.Count) { reason = "같은 카드가 중복되었습니다."; return false; }
            foreach (var c in ordered)
                if (!Piles.InHand(c)) { reason = "손패에 없는 카드가 있습니다."; return false; }
            if (!exchange && !GameConfig.AllowNoHandSubmit && !HandEvaluator.Evaluate(ordered).IsValid)
            {
                if (GameConfig.AllowNoHandSubmitWhenStuck && !SimpleAi.HasAnyValidHand(Piles.Hand)) return true;
                reason = "족보가 성립하지 않습니다.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 공격(또는 교체)만 처리합니다. 사용한 카드는 '사용 중' 영역으로 가고, 보충은 턴 종료에 합니다.
        /// 이후 Phase: 일반 공격 → PlayerDiscard (EndPlayerTurn 호출 필요)
        ///             교체 턴 → 버리기 없이 바로 턴 종료 (BetweenTurns)
        ///             처치 → Victory (버리기·보충 없음)
        /// </summary>
        /// <param name="ordered">사용 카드 존 순서. [0] = 대표 카드(1번 자리).</param>
        public AttackReport Submit(IList<Card> ordered, Element? moonPickForTest = null, bool exchange = false)
        {
            string reason;
            if (!CanSubmit(ordered, out reason, exchange)) throw new InvalidOperationException(reason);

            var used = ordered.ToList();
            var report = DamageCalculator.ResolvePlayerAttack(used, Setup.MainElement, Player, Enemy, Rng, moonPickForTest, exchange);
            if (report.ShieldGranted > 0) _pendingShield = report.ShieldGranted;

            // [사용자 결정 2026-11-14 A8] 추가 드로우 카드는 보충 장수에 세지 않음 → 턴이 끝나면 손패가 기본 장수로 돌아옴
            _usedThisAction = used.Count(c => !Piles.IsExtra(c));
            Piles.MoveToInPlay(used);
            PlayerTurnCount++;
            LastActor = Actor.Player;

            AddLog(DescribeAttack(report));

            if (Enemy.IsDead)
            {
                // [확정] 처치한 턴에는 버리기 단계 없음. 전투가 끝나므로 보충도 하지 않고 사용 카드만 묘지로.
                Piles.FinishTurn(0);
                Phase = BattlePhase.Victory;
                AddLog("승리!");
                return report;
            }
            if (report.IsExchange)
            {
                // [확정] 교체한 턴에는 버리기 단계 없음
                FinishPlayerTurn(new List<Card>());
                return report;
            }
            Phase = BattlePhase.PlayerDiscard;
            return report;
        }

        /// <summary>턴 종료 버리기 가능 여부. 0~2장, 손패에 있는 카드만 (방금 사용한 카드는 이미 손패에 없음).</summary>
        public bool CanDiscard(IList<Card> discards, out string reason)
        {
            reason = "";
            if (Phase != BattlePhase.PlayerDiscard) { reason = "버리기 단계가 아닙니다."; return false; }
            if (discards == null) return true;
            if (discards.Count > GameConfig.MaxDiscard) { reason = "최대 " + GameConfig.MaxDiscard + "장까지 버릴 수 있습니다."; return false; }
            if (discards.Distinct().Count() != discards.Count) { reason = "같은 카드가 중복되었습니다."; return false; }
            foreach (var c in discards)
            {
                if (!Piles.InHand(c)) { reason = "손패에 없는 카드입니다 (사용한 카드는 버릴 수 없습니다)."; return false; }
                if (Piles.IsExtra(c)) { reason = "추가 카드는 턴 종료 때 자동으로 버려집니다."; return false; }
            }
            return true;
        }

        /// <summary>
        /// 플레이어 턴 종료 (v1.2 [확정]): 버림 → (사용 + 버린 장수 [+ 추가 드로우 1]) 보충 → 사용·버린 카드 묘지로
        /// → [v1.3] 손패 10장 초과면 PlayerForcedDiscard에서 멈춤 (ForceDiscard 필요)
        /// → 토 쉴드 턴 종료 처리 → 행동 대기 재충전(수 속성 SPD 반영). 반환: 새로 뽑은 카드.
        /// </summary>
        public List<Card> EndPlayerTurn(IList<Card> discards)
        {
            string reason;
            if (!CanDiscard(discards, out reason)) throw new InvalidOperationException(reason);
            return FinishPlayerTurn(discards ?? new List<Card>());
        }

        /// <summary>
        /// [확정 v1.3] 순서: 버림 → (사용 + 버린 장수) 보충 → 추가 드로우 판정 → 손패 10장 초과 시 강제 버리기 → 턴 종료 처리.
        /// 강제 버리기가 필요하면 Phase = PlayerForcedDiscard로 멈추고, ForceDiscard()에서 턴 종료 처리를 이어 감.
        /// </summary>
        private List<Card> FinishPlayerTurn(IList<Card> discards)
        {
            Piles.MoveToInPlay(discards);
            // [사용자 결정 2026-11-14 A8] 이번 턴에 쓰지 않은 추가 카드는 보충 전에 묘지로 (보충 없음, 방금 쓴 카드처럼 재섞기에도 안 섞임)
            var expired = Piles.Hand.Where(Piles.IsExtra).ToList();
            Piles.MoveToInPlay(expired);
            var drawn = Piles.Draw(_usedThisAction + discards.Count);
            // [확정] 보충 드로우 시 확률에 따라 1장 더. 확률 0이면 난수를 쓰지 않음 (기존 시드 재현 유지)
            bool extra = Player.ExtraDrawChance > 0 && Rng.Value01() < Player.ExtraDrawChance;
            if (extra)
            {
                var add = Piles.Draw(1);
                foreach (var c in add) Piles.Extra.Add(c);
                drawn.AddRange(add);
            }
            Piles.FinishTurn(0); // 사용·버린 카드는 보충이 다 끝난 뒤 묘지로 (재섞기에 섞이지 않음)
            _usedThisAction = 0;
            if (discards.Count > 0) AddLog("버림 " + discards.Count + "장");
            if (expired.Count > 0) AddLog("추가 카드 " + expired.Count + "장 버려짐");
            if (extra) AddLog("추가 드로우 +1");

            if (Piles.Hand.Count > GameConfig.MaxHandSize)
            {
                Phase = BattlePhase.PlayerForcedDiscard;
                AddLog("손패 " + Piles.Hand.Count + "장 → " + ForcedDiscardCount + "장 강제 버리기");
                return drawn;
            }
            CompletePlayerTurn();
            return drawn;
        }

        /// <summary>강제 버리기로 버려야 하는 장수 (그 단계가 아니면 0).</summary>
        public int ForcedDiscardCount
        {
            get { return Phase == BattlePhase.PlayerForcedDiscard ? Math.Max(0, Piles.Hand.Count - GameConfig.MaxHandSize) : 0; }
        }

        public bool CanForceDiscard(IList<Card> cards, out string reason)
        {
            reason = "";
            if (Phase != BattlePhase.PlayerForcedDiscard) { reason = "강제 버리기 단계가 아닙니다."; return false; }
            int need = ForcedDiscardCount;
            if (cards == null || cards.Count != need) { reason = need + "장을 골라야 합니다."; return false; }
            if (cards.Distinct().Count() != cards.Count) { reason = "같은 카드가 중복되었습니다."; return false; }
            foreach (var c in cards)
                if (!Piles.InHand(c)) { reason = "손패에 없는 카드입니다."; return false; }
            return true;
        }

        /// <summary>[확정 v1.3] 강제 버리기: 넘는 장수만큼 묘지로. 턴 종료 버리기 2장과 별개이고 보충 드로우 없음. 이어서 턴 종료 처리.</summary>
        public void ForceDiscard(IList<Card> cards)
        {
            string reason;
            if (!CanForceDiscard(cards, out reason)) throw new InvalidOperationException(reason);
            Piles.SendToGraveyard(cards);
            AddLog("강제 버림 " + cards.Count + "장");
            CompletePlayerTurn();
        }

        private void CompletePlayerTurn()
        {
            EndPlayerTurnEffects();
            Scheduler.Reset(Actor.Player);
            Phase = BattlePhase.BetweenTurns;
        }

        private void EndPlayerTurnEffects()
        {
            // 기존 토 쉴드: 플레이어 턴 종료마다 1회 차감
            if (Player.Shield.Active)
            {
                Player.Shield.TurnsLeft--;
                if (Player.Shield.TurnsLeft <= 0) { Player.Shield.Clear(); AddLog("쉴드 만료"); }
            }
            // 새 토 쉴드: [확정] 현재 쉴드량과 무관하게 교체, 다음 플레이어 턴 종료 2회까지
            if (_pendingShield > 0)
            {
                Player.Shield.Amount = _pendingShield;
                Player.Shield.TurnsLeft = GameConfig.EarthShieldTurns;
                _pendingShield = 0;
            }
        }

        // ───────────────────── 에너미 ─────────────────────

        public EnemyActionReport RunEnemyTurn()
        {
            if (Phase != BattlePhase.EnemyTurn) throw new InvalidOperationException("에너미 차례가 아닙니다.");
            var r = new EnemyActionReport();
            r.Kind = ChooseEnemyAction();
            EnemyTurnCount++;
            LastActor = Actor.Enemy;

            switch (r.Kind)
            {
                case EnemyActionKind.NormalAttack:
                case EnemyActionKind.SkillAttack:
                    double mult = r.Kind == EnemyActionKind.SkillAttack ? GameConfig.EnemySkillMultiplier : 1.0;
                    r.RawDamage = (int)Math.Floor(Enemy.Atk * mult);
                    double raw = r.RawDamage;
                    // 치명타율 0이면 난수를 쓰지 않음 (일반 에너미는 항상 0)
                    if (Enemy.CritChance > 0 && Rng.Value01() < Enemy.CritChance)
                    {
                        r.Critical = true;
                        raw *= GameConfig.CritMultiplier;
                    }
                    DamageCalculator.ResolveEnemyHit(raw, Player, out r.ShieldAbsorbed, out r.HpLoss);
                    AddLog((r.Kind == EnemyActionKind.SkillAttack ? "에너미 스킬 공격 " : "에너미 공격 ")
                           + (r.Critical ? "CRITICAL " : "")
                           + r.HpLoss + " 피해" + (r.ShieldAbsorbed > 0 ? " (쉴드 " + r.ShieldAbsorbed + " 흡수)" : ""));
                    break;
                case EnemyActionKind.Heal:
                    int lost = Enemy.MaxHp - Enemy.Hp;
                    double rate = GameConfig.EnemyHealMinRate
                                + (GameConfig.EnemyHealMaxRate - GameConfig.EnemyHealMinRate) * Rng.Value01();
                    r.Healed = Enemy.Heal((int)Math.Floor(lost * rate));
                    AddLog("에너미 회복 +" + r.Healed);
                    break;
                case EnemyActionKind.DefUp:
                    int add = Math.Min(GameConfig.EnemyDefUpMax, Math.Max(GameConfig.EnemyDefUpMin, Setup.Floor));
                    Enemy.BaseDef += add;
                    r.DefGained = add;
                    AddLog("에너미 방어력 +" + add);
                    break;
                case EnemyActionKind.AtkUp:
                    int atkAdd = Math.Max(1, (int)Math.Round(Enemy.Atk * GameConfig.EnemyAtkUpRate));
                    Enemy.Atk += atkAdd;
                    r.AtkGained = atkAdd;
                    AddLog("에너미 공격력 +" + atkAdd);
                    break;
                case EnemyActionKind.CritUp:
                    double before = Enemy.CritChance;
                    Enemy.CritChance = Math.Min(GameConfig.EnemyCritMax, Enemy.CritChance + GameConfig.EnemyCritUpRate);
                    r.CritGained = Enemy.CritChance - before;
                    AddLog("에너미 치명타율 " + PlayerStats.Percent(Enemy.CritChance) + "%");
                    break;
                case EnemyActionKind.Cleanse:
                    r.CleansedBurn = Enemy.Burn.Active;
                    r.CleansedMetal = Enemy.Metal.Active;
                    Enemy.Burn.Clear();
                    Enemy.Metal.Clear();
                    AddLog("에너미 디버프 제거");
                    break;
            }

            // [확정] 금 방깎은 적 턴 중 유지. 이후 '연속 강화' 조건에서 빠짐.
            if (Enemy.Metal.Active) Enemy.Metal.EnemyActedSinceApplied = true;

            // [확정] 화상: 적 행동 종료 직전 1회 발동·소모, DEF로 경감되지 않음
            if (Enemy.Burn.Active)
            {
                int burn = Enemy.Burn.Consume();
                r.BurnDamage = Enemy.LoseHp(burn);
                r.BurnTicksLeft = Enemy.Burn.Ticks.Count;
                AddLog("화상 피해 " + r.BurnDamage);
            }

            Scheduler.Reset(Actor.Enemy);

            if (Enemy.IsDead) { Phase = BattlePhase.Victory; AddLog("승리!"); }
            else if (Player.IsDead) { Phase = BattlePhase.Defeat; AddLog("패배..."); }
            return r;
        }

        /// <summary>
        /// [확정] 행동권마다 공격 또는 비공격 하나만. [확정 v1.2] 일반 에너미는 디버프 제거·자기 버프 불가,
        /// 엘리트·보스만 디버프 제거와 자기 버프(ATK 증가·치명타율 증가). [가정] 가중치와 조건은 GameConfig.
        /// </summary>
        private EnemyActionKind ChooseEnemyAction()
        {
            bool special = Enemy.Tier != EnemyTier.Normal;
            var nonAttack = new List<EnemyActionKind>();
            if (special && Enemy.HasDebuff) nonAttack.Add(EnemyActionKind.Cleanse);
            if (Enemy.Hp < Enemy.MaxHp * 0.7) nonAttack.Add(EnemyActionKind.Heal);
            nonAttack.Add(EnemyActionKind.DefUp);
            if (special)
            {
                nonAttack.Add(EnemyActionKind.AtkUp);
                if (Enemy.CritChance < GameConfig.EnemyCritMax - 1e-9) nonAttack.Add(EnemyActionKind.CritUp);
            }

            int wNormal = special ? GameConfig.SpecialWeightNormalAttack : GameConfig.EnemyWeightNormalAttack;
            int wSkill = special ? GameConfig.SpecialWeightSkillAttack : GameConfig.EnemyWeightSkillAttack;
            int wNon = special ? GameConfig.SpecialWeightNonAttack : GameConfig.EnemyWeightNonAttack;
            int roll = Rng.Range(0, wNormal + wSkill + wNon);
            if (roll < wNormal) return EnemyActionKind.NormalAttack;
            roll -= wNormal;
            if (roll < wSkill) return EnemyActionKind.SkillAttack;
            return nonAttack[Rng.Range(0, nonAttack.Count)];
        }

        // ───────────────────── 표시 ─────────────────────

        public static string DescribeAttack(AttackReport r)
        {
            if (r.IsExchange) return "카드 교체 " + r.CardCount + "장 (피해 없음)";
            var s = HandInfo.Name(r.Hand.Type) + "(" + r.Hand.Detail + ") "
                  + ElementInfo.Korean(r.AttackElement) + " 공격: " + (r.Critical ? "CRITICAL " : "");
            if (r.TrueDamage > 0) s += "True " + r.TrueDamage + (r.NormalDamage > 0 ? " + " + r.NormalDamage : "");
            else s += r.NormalDamage.ToString();
            // True Damage 족보는 메인 +20%를 받지 않음: PENTAGRAM은 메인 보너스 없음, 메인 ROYAL은 True +70
            if (r.IsMainElementAttack && !r.Hand.IsTrueDamage) s += " [메인+20%]";
            else if (r.IsMainElementAttack && r.Hand.Type == HandType.RoyalFlush) s += " [메인 ROYAL +" + GameConfig.RoyalMainBonus + "]";
            if (r.AttackElement == Element.Moon) s += " / 월→" + ElementInfo.Korean(r.EffectElement) + " 복제";
            if (r.BurnPerTick > 0) s += " / 화상 " + r.BurnPerTick + "×2";
            if (r.Healed > 0) s += " / 회복 " + r.Healed;
            if (r.ShieldGranted > 0) s += " / 쉴드 " + r.ShieldGranted;
            if (r.SpeedBuff > 0) s += " / SPD +" + Math.Round(r.SpeedBuff * 100) + "%";
            if (r.EffectElement == Element.Metal) s += " / DEF " + r.DefBefore.ToString("0.#") + "→" + r.DefUsed.ToString("0.#");
            return s;
        }

        private void AddLog(string line)
        {
            Log.Add(line);
            if (Log.Count > 200) Log.RemoveAt(0);
        }
    }
}
