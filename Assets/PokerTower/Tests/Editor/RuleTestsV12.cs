// 기획서 v1.2 (2026-11-14): 턴 종료 버리기 · 스탯 포인트 · 손패 장수 제한 없음 · 에너미 등급과 치명타
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using PokerTower.Core;

namespace PokerTower.Tests
{
    public class RuleTestsV12
    {
        private const Element F = Element.Fire, W = Element.Water, Wd = Element.Wood, M = Element.Metal, E = Element.Earth;

        /// <summary>1층 전투를 시작하고 첫 플레이어 턴까지 진행 (SPD 100 > 90 → 플레이어 선공).</summary>
        private static Battle StartAtPlayerTurn(int seed = 1, int floor = 1, PlayerStats stats = null)
        {
            var b = new Battle(new StageSetup { Floor = floor, Seed = seed, MainElement = F, Elements = new List<Element> { F, W, Wd, M, E } }, stats);
            b.Begin();
            Assert.AreEqual(Actor.Player, b.NextTurn());
            return b;
        }

        /// <summary>손패를 지정한 카드로 바꿉니다 (덱에서 꺼내 옴, 65장 유지).</summary>
        private static List<Card> SetHand(Battle b, params (Element e, int r)[] spec)
        {
            var piles = b.Piles;
            piles.Deck.AddRange(piles.Hand);
            piles.Hand.Clear();
            foreach (var (e, r) in spec)
            {
                var c = piles.Deck.FirstOrDefault(x => x.Element == e && x.Rank == r);
                if (c != null) piles.Deck.Remove(c);
                else { c = piles.Graveyard.First(x => x.Element == e && x.Rank == r); piles.Graveyard.Remove(c); }
                piles.Hand.Add(c);
            }
            return piles.Hand.ToList();
        }

        private static Card Find(Battle b, Element e, int r) { return b.Piles.Hand.First(x => x.Element == e && x.Rank == r); }

        private static readonly (Element, int)[] PairHand = { (F, 2), (W, 2), (F, 5), (W, 7), (Wd, 9), (M, 11), (E, 13) };

        // ───────── 1. 턴 종료 버리기 ─────────

        [Test] public void Discard_MaxTwo_ZeroAllowed_NoDuplicates()
        {
            var b = StartAtPlayerTurn();
            SetHand(b, PairHand);
            b.Submit(new List<Card> { Find(b, F, 2), Find(b, W, 2) });
            Assert.AreEqual(BattlePhase.PlayerDiscard, b.Phase);
            string reason;
            var h = b.Piles.Hand;
            Assert.IsFalse(b.CanDiscard(new List<Card> { h[0], h[1], h[2] }, out reason), "3장은 불가");
            Assert.IsTrue(b.CanDiscard(new List<Card> { h[0], h[1] }, out reason));
            Assert.IsTrue(b.CanDiscard(new List<Card>(), out reason), "0장 가능");
            Assert.IsFalse(b.CanDiscard(new List<Card> { h[0], h[0] }, out reason), "중복 불가");
        }

        [Test] public void Discard_RejectsJustUsedCards()
        {
            var b = StartAtPlayerTurn();
            SetHand(b, PairHand);
            var used = new List<Card> { Find(b, F, 2), Find(b, W, 2) };
            b.Submit(used);
            string reason;
            Assert.IsFalse(b.CanDiscard(new List<Card> { used[0] }, out reason));
            Assert.Throws<InvalidOperationException>(() => b.EndPlayerTurn(new List<Card> { used[1] }));
        }

        [Test] public void Refill_HappensAtTurnEnd_Total65()
        {
            var b = StartAtPlayerTurn();
            SetHand(b, PairHand);
            b.Submit(new List<Card> { Find(b, F, 2), Find(b, W, 2) });
            Assert.AreEqual(5, b.Piles.Hand.Count, "공격 직후에는 보충하지 않음");
            Assert.AreEqual(2, b.Piles.InPlay.Count);
            Assert.AreEqual(65, b.Piles.TotalCards);

            var drawn = b.EndPlayerTurn(new List<Card> { Find(b, F, 5), Find(b, W, 7) });
            Assert.AreEqual(4, drawn.Count, "사용 2 + 버림 2 보충");
            Assert.AreEqual(7, b.Piles.Hand.Count);
            Assert.AreEqual(0, b.Piles.InPlay.Count);
            Assert.AreEqual(4, b.Piles.Graveyard.Count);
            Assert.AreEqual(65, b.Piles.TotalCards);
            Assert.AreEqual(BattlePhase.BetweenTurns, b.Phase);
        }

        [Test] public void UsedAndDiscarded_NotRedrawnByReshuffle()
        {
            var b = StartAtPlayerTurn();
            SetHand(b, PairHand);
            // 덱을 비워 보충 중 재섞기가 일어나게 함
            b.Piles.Graveyard.AddRange(b.Piles.Deck);
            b.Piles.Deck.Clear();
            var used = new List<Card> { Find(b, F, 2), Find(b, W, 2) };
            var discarded = new List<Card> { Find(b, F, 5), Find(b, W, 7) };
            b.Submit(used);
            b.EndPlayerTurn(discarded);
            Assert.AreEqual(1, b.Piles.ReshuffleCount);
            foreach (var c in used.Concat(discarded))
            {
                Assert.IsFalse(b.Piles.Hand.Contains(c), "방금 쓰거나 버린 카드가 다시 뽑힘: " + c);
                Assert.IsTrue(b.Piles.Graveyard.Contains(c));
            }
            Assert.AreEqual(7, b.Piles.Hand.Count);
            Assert.AreEqual(65, b.Piles.TotalCards);
        }

        [Test] public void ExchangeTurn_HasNoDiscardPhase()
        {
            var b = StartAtPlayerTurn();
            SetHand(b, (F, 2), (W, 4), (Wd, 6), (M, 8), (E, 10), (F, 12), (W, 14)); // 족보 없음
            Assert.IsFalse(SimpleAi.HasAnyValidHand(b.Piles.Hand));
            var r = b.Submit(new List<Card> { Find(b, F, 2), Find(b, W, 4) });
            Assert.IsTrue(r.IsExchange);
            Assert.AreEqual(BattlePhase.BetweenTurns, b.Phase);
            Assert.AreEqual(7, b.Piles.Hand.Count);
            Assert.AreEqual(65, b.Piles.TotalCards);
            StringAssert.Contains("카드 교체", b.Log.Last());
            StringAssert.DoesNotContain("공격", b.Log.Last());
        }

        [Test] public void KillTurn_HasNoDiscardPhase()
        {
            var b = StartAtPlayerTurn();
            SetHand(b, PairHand);
            b.Enemy.Hp = 1;
            b.Submit(new List<Card> { Find(b, F, 2), Find(b, W, 2) });
            Assert.AreEqual(BattlePhase.Victory, b.Phase);
            Assert.AreEqual(0, b.Piles.InPlay.Count);
            Assert.AreEqual(65, b.Piles.TotalCards);
        }

        [Test] public void ConsecutiveActions_EachHasDiscardPhase()
        {
            var fast = new PlayerStats(); fast.Levels[(int)StatKind.Speed] = 40; // SPD 300 vs 90
            var b = StartAtPlayerTurn(stats: fast);
            b.Enemy.MaxHp = b.Enemy.Hp = 100000;
            int consecutive = 0;
            Actor? prev = null;
            for (int i = 0; i < 12; i++)
            {
                Actor a = i == 0 ? Actor.Player : b.NextTurn();
                if (a == Actor.Enemy) { b.RunEnemyTurn(); prev = a; continue; }
                if (prev == Actor.Player) consecutive++;
                SetHand(b, PairHand);
                b.Submit(new List<Card> { Find(b, F, 2), Find(b, W, 2) });
                Assert.AreEqual(BattlePhase.PlayerDiscard, b.Phase, "행동마다 버리기 단계");
                b.EndPlayerTurn(null);
                prev = a;
            }
            Assert.Greater(consecutive, 0, "연속 행동이 한 번은 나와야 함");
        }

        // ───────── 2. 스탯 ─────────

        [Test] public void Stats_InitialAndPerPoint()
        {
            var p0 = Battle.CreatePlayer();
            Assert.AreEqual(200, p0.MaxHp);
            Assert.AreEqual(200, p0.Hp);
            Assert.AreEqual(10.0, p0.BaseDef, 1e-9);
            Assert.AreEqual(100.0, p0.BaseSpeed, 1e-9);
            Assert.AreEqual(0.0, p0.ExtraDrawChance, 1e-9);
            Assert.AreEqual(1.0, p0.DamageMultiplier, 1e-9);

            var s = new PlayerStats();
            for (int i = 0; i < PlayerStats.Count; i++) s.Levels[i] = 2;
            var p = Battle.CreatePlayer(s);
            Assert.AreEqual(220, p.MaxHp);
            Assert.AreEqual(110.0, p.BaseSpeed, 1e-9);
            Assert.AreEqual(0.08, p.ExtraDrawChance, 1e-9);
            Assert.AreEqual(16.0, p.BaseDef, 1e-9);
            Assert.AreEqual(1.04, p.DamageMultiplier, 1e-9);
        }

        [Test] public void Stats_DamagePlus10Percent()
        {
            var s = new PlayerStats(); s.Levels[(int)StatKind.Damage] = 5;
            var p = Battle.CreatePlayer(s);
            var en = new EnemyUnit { MaxHp = 100000, Hp = 100000, BaseDef = 0 };
            var pairAA = DamageCalculator.ResolvePlayerAttack(new List<Card> { new Card(9001, F, 14), new Card(9002, W, 14) }, Element.Sun, p, en, new GameRng(1));
            Assert.AreEqual(22, pairAA.NormalDamage, "PAIR AA 20 → 22");
            var penta = DamageCalculator.ResolvePlayerAttack(
                new List<Card> { new Card(9003, F, 7), new Card(9004, W, 7), new Card(9005, Wd, 7), new Card(9006, M, 7), new Card(9007, E, 7) },
                F, p, new EnemyUnit { MaxHp = 100000, Hp = 100000, BaseDef = 500 }, new GameRng(1));
            Assert.AreEqual(385, penta.TrueDamage, "True 350 → 385");
            // 화상은 배율이 적용된 최종 피해 기준: floor(22 × 0.3 × 1.0 / 2) = 3
            Assert.AreEqual(3, pairAA.BurnPerTick);
        }

        [Test] public void Stats_Def10_ReducesEnemyAtk10To9()
        {
            int absorbed, loss;
            DamageCalculator.ResolveEnemyHit(10, Battle.CreatePlayer(), out absorbed, out loss);
            Assert.AreEqual(9, loss);
        }

        // [사용자 결정 2026-11-14 A8] 추가 카드는 보충 장수에 안 세고, 안 쓰면 턴 종료 때 버려짐 → 손패가 불어나지 않음 (기본 7 + 추가 1)
        [Test] public void Stats_ExtraDraw100_HandStaysBasePlusOne()
        {
            var s = new PlayerStats(); s.Levels[(int)StatKind.ExtraDraw] = 25; // 4%p × 25 = 100%
            var b = StartAtPlayerTurn(stats: s);
            b.Enemy.MaxHp = b.Enemy.Hp = 100000;
            int expected = GameConfig.HandSize;
            for (int turn = 0; turn < 6; turn++)
            {
                if (turn > 0) while (b.NextTurn() != Actor.Player) b.RunEnemyTurn();
                b.Submit(SimpleAi.BestSubmission(b.Piles.Hand));
                if (b.Phase == BattlePhase.PlayerDiscard) b.EndPlayerTurn(null);
                if (b.Phase == BattlePhase.PlayerForcedDiscard) b.ForceDiscard(SimpleAi.ChooseDiscards(b.Piles.Hand, b.ForcedDiscardCount, true));
                Assert.AreEqual(expected + 1, b.Piles.Hand.Count, "turn " + turn);
                Assert.AreEqual(1, b.Piles.Extra.Count, "추가 카드는 늘 1장만");
                Assert.AreEqual(65, b.Piles.TotalCards);
            }
        }

        [Test] public void Progress_SaveLoad_NewGameReset_NoDuplicateAward()
        {
            var prog = new PlayerProgress();
            Assert.AreEqual(1, prog.AwardClear(1));
            Assert.AreEqual(0, prog.AwardClear(1), "같은 층 재클리어 시 중복 지급 없음");
            Assert.AreEqual(1, prog.AwardClear(2));
            Assert.AreEqual(2, prog.UnspentPoints);

            string reason;
            Assert.IsFalse(prog.CanCommit(new[] { 2, 0, 0, 0, 0, 0 }, out reason), "0→2레벨은 3점 → 부족");
            Assert.IsFalse(prog.CanCommit(new[] { -1, 0, 0, 0, 1, 0 }, out reason), "음수 불가");
            prog.Commit(new[] { 1, 0, 0, 1, 0, 0 });           // 1점 + 1점
            Assert.AreEqual(0, prog.UnspentPoints);

            var loaded = PlayerProgress.Parse(prog.Serialize());
            CollectionAssert.AreEqual(prog.Stats.Levels, loaded.Stats.Levels);
            Assert.AreEqual(prog.UnspentPoints, loaded.UnspentPoints);
            Assert.AreEqual(2, loaded.HighestClearedFloor);
            Assert.AreEqual(0, loaded.AwardClear(2), "불러온 뒤에도 중복 지급 없음");

            loaded.Reset(); // 새로하기
            CollectionAssert.AreEqual(new int[PlayerStats.Count], loaded.Stats.Levels);
            Assert.AreEqual(0, loaded.UnspentPoints);
            Assert.AreEqual(0, loaded.HighestClearedFloor);

            var broken = PlayerProgress.Parse("garbage");
            Assert.AreEqual(0, broken.UnspentPoints);
            var v1 = PlayerProgress.Parse("v1;1,0,2,0,0;3;4");    // v1.2 저장(스탯 5개)도 읽음 → 치명타 0
            CollectionAssert.AreEqual(new[] { 1, 0, 2, 0, 0, 0 }, v1.Stats.Levels);
            Assert.AreEqual(3, v1.UnspentPoints);
        }

        // ───────── 4. 에너미 등급 · 치명타 ─────────

        [Test] public void Tier_ByFloor()
        {
            Assert.AreEqual(EnemyTier.Normal, GameConfig.TierOf(1));
            Assert.AreEqual(EnemyTier.Normal, GameConfig.TierOf(4));
            Assert.AreEqual(EnemyTier.Elite, GameConfig.TierOf(5));
            Assert.AreEqual(EnemyTier.Boss, GameConfig.TierOf(10));
            Assert.AreEqual(EnemyTier.Elite, GameConfig.TierOf(15));
            Assert.AreEqual(EnemyTier.Boss, GameConfig.TierOf(20));

            var elite = Battle.CreateEnemy(5);
            Assert.AreEqual(EnemyTier.Elite, elite.Tier);
            Assert.AreEqual((int)Math.Round(300 * 1.4 * 1.6), elite.MaxHp);
            Assert.AreEqual((int)Math.Round(10 * 1.32 * 1.15), elite.Atk);
            Assert.IsTrue(Battle.CreateEnemy(10).IsBoss);
        }

        private static Dictionary<EnemyActionKind, int> RunBattlesCountingActions(IEnumerable<int> floors, int seeds, Action<Battle> afterEnemy = null)
        {
            var count = new Dictionary<EnemyActionKind, int>();
            foreach (int floor in floors)
                for (int seed = 0; seed < seeds; seed++)
                {
                    var b = new Battle(new StageSetup { Floor = floor, Seed = seed, MainElement = F, Elements = new List<Element> { F, W, Wd, M, E } });
                    b.Begin();
                    int guard = 0;
                    while (!b.IsOver && guard++ < 1000)
                    {
                        if (b.NextTurn() == Actor.Player) { SimpleAi.PlayTurn(b); continue; }
                        var r = b.RunEnemyTurn();
                        count[r.Kind] = (count.ContainsKey(r.Kind) ? count[r.Kind] : 0) + 1;
                        if (afterEnemy != null) afterEnemy(b);
                    }
                }
            return count;
        }

        // [사용자 결정 2026-11-15] 일반 에너미 비공격: ATK 증가·DEF 증가·회복만
        [Test] public void NormalEnemy_OnlyAtkDefHeal_1000Seeds()
        {
            var count = RunBattlesCountingActions(new[] { 1, 2, 3, 4 }, 250); // 일반 층 × 250 = 1,000 시드
            Assert.IsFalse(count.ContainsKey(EnemyActionKind.Cleanse), "디버프 제거");
            Assert.IsFalse(count.ContainsKey(EnemyActionKind.SpeedUp), "SPD 증가");
            Assert.IsFalse(count.ContainsKey(EnemyActionKind.CritUp), "치명타율 증가");
            Assert.IsTrue(count.ContainsKey(EnemyActionKind.Heal) && count.ContainsKey(EnemyActionKind.DefUp) && count.ContainsKey(EnemyActionKind.AtkUp));
        }

        // [사용자 결정 2026-11-15] 엘리트·보스 비공격 6종, 치명타율 상한 100%
        [Test] public void EliteEnemy_UsesAllSixBuffs_CritUpTo100()
        {
            double maxCrit = 0;
            var count = RunBattlesCountingActions(new[] { 5 }, 300, b => maxCrit = Math.Max(maxCrit, b.Enemy.CritChance));
            foreach (var k in new[] { EnemyActionKind.CritUp, EnemyActionKind.AtkUp, EnemyActionKind.DefUp, EnemyActionKind.Heal, EnemyActionKind.SpeedUp, EnemyActionKind.Cleanse })
                Assert.IsTrue(count.ContainsKey(k), k.ToString());
            Assert.AreEqual(1.0, GameConfig.EnemyCritMax, 1e-9);
            Assert.LessOrEqual(maxCrit, 1.0 + 1e-9);
        }

        // [사용자 결정 2026-11-15] 수치: ATK +5, DEF +2~5, SPD +2~5. 후보끼리 같은 확률
        [Test] public void EnemyBuffAmounts_AndEqualChance()
        {
            var atk = new HashSet<int>(); var def = new HashSet<int>(); var spd = new HashSet<int>();
            var count = new Dictionary<EnemyActionKind, int>();
            for (int seed = 0; seed < 400; seed++)
            {
                var b = new Battle(new StageSetup { Floor = 5, Seed = seed, MainElement = F, Elements = new List<Element> { F, W, Wd, M, E } });
                b.Begin();
                b.Player.MaxHp = b.Player.Hp = 100000; // 오래 버티며 행동을 많이 모음
                for (int t = 0; t < 60 && !b.IsOver; t++)
                {
                    if (b.NextTurn() == Actor.Player) { SimpleAi.PlayTurn(b); continue; }
                    b.Enemy.Hp = b.Enemy.MaxHp;                      // 회복 제외 → 후보 고정
                    b.Enemy.Burn.Clear(); b.Enemy.Metal.Clear();     // 디버프 없음 → 디버프 제거 제외
                    var r = b.RunEnemyTurn();
                    if (r.Kind == EnemyActionKind.AtkUp) atk.Add(r.AtkGained);
                    if (r.Kind == EnemyActionKind.DefUp) def.Add((int)r.DefGained);
                    if (r.Kind == EnemyActionKind.SpeedUp) spd.Add(r.SpeedGained);
                    if (r.Kind == EnemyActionKind.AtkUp || r.Kind == EnemyActionKind.DefUp || r.Kind == EnemyActionKind.SpeedUp)
                        count[r.Kind] = (count.ContainsKey(r.Kind) ? count[r.Kind] : 0) + 1;
                }
            }
            CollectionAssert.AreEquivalent(new[] { 5 }, atk);
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, def);
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, spd);
            // 치명타율 증가가 100%에 닿기 전까지는 4개 후보 → ATK·DEF·SPD 횟수가 서로 비슷해야 함 (±25%)
            double avg = count.Values.Average();
            foreach (var kv in count) Assert.That(kv.Value, Is.InRange(avg * 0.75, avg * 1.25), kv.Key.ToString());
        }

        [Test] public void Crit_Damage_Is150Percent_BeforeDef()
        {
            // floor(원 피해 × 1.5 × 100 / (100 + DEF))
            int absorbed, loss;
            var p = Battle.CreatePlayer(); // DEF 10
            DamageCalculator.ResolveEnemyHit(10 * 1.5, p, out absorbed, out loss);
            Assert.AreEqual((int)Math.Floor(10 * 1.5 * 100 / 110.0), loss); // 13

            // 실제 전투: 치명타율 100%로 고정한 에너미의 공격
            var b = StartAtPlayerTurn();
            b.Enemy.MaxHp = b.Enemy.Hp = 100000;
            b.Enemy.CritChance = 1.0;
            SimpleAi.PlayTurn(b);
            int attacks = 0, guard = 0;
            while (attacks < 5 && guard++ < 200)
            {
                if (b.NextTurn() == Actor.Player) { SimpleAi.PlayTurn(b); continue; }
                b.Player.Shield.Clear();
                b.Player.Hp = b.Player.MaxHp;
                var r = b.RunEnemyTurn();
                if (r.Kind != EnemyActionKind.NormalAttack && r.Kind != EnemyActionKind.SkillAttack) continue;
                attacks++;
                Assert.IsTrue(r.Critical);
                Assert.AreEqual((int)Math.Floor(r.RawDamage * 1.5 * 100 / (100 + b.Player.BaseDef) + 1e-9), r.HpLoss);
            }
            Assert.AreEqual(5, attacks);
        }

        // ───────── 3. 손패 탐색 속도·정확성 ─────────

        private static List<Card> FullDeck()
        {
            var deck = new List<Card>();
            int id = 0;
            foreach (var e in new[] { F, W, Wd, M, E })
                for (int r = 2; r <= 14; r++) deck.Add(new Card(id++, e, r));
            return deck;
        }

        private static IEnumerable<List<Card>> Combos(IList<Card> hand, int k, int start = 0, List<Card> cur = null)
        {
            cur = cur ?? new List<Card>();
            if (cur.Count == k) { yield return new List<Card>(cur); yield break; }
            for (int i = start; i < hand.Count; i++)
            {
                cur.Add(hand[i]);
                foreach (var c in Combos(hand, k, i + 1, cur)) yield return c;
                cur.RemoveAt(cur.Count - 1);
            }
        }

        /// <summary>기존 방식: 2~5장 모든 조합 전수 탐색.</summary>
        private static int BruteBestScore(IList<Card> hand, out bool any)
        {
            int best = -1;
            for (int k = GameConfig.MinSubmit; k <= GameConfig.MaxSubmit; k++)
                foreach (var s in Combos(hand, k))
                {
                    var h = HandEvaluator.Evaluate(s);
                    if (h.IsValid) best = Math.Max(best, SimpleAi.Score(h, s.Count));
                }
            any = best >= 0;
            return best;
        }

        private static void CompareWithBruteForce(int handSize, int hands, int seed)
        {
            var rng = new GameRng(seed);
            for (int i = 0; i < hands; i++)
            {
                var deck = FullDeck();
                rng.Shuffle(deck);
                var hand = deck.Take(handSize).ToList();
                bool any;
                int brute = BruteBestScore(hand, out any);
                Assert.AreEqual(any, SimpleAi.HasAnyValidHand(hand), "HasAnyValidHand: " + string.Join(" ", hand));
                if (!any) continue;
                var fast = SimpleAi.BestSubmission(hand);
                var h = HandEvaluator.Evaluate(fast);
                Assert.IsTrue(h.IsValid);
                Assert.AreEqual(brute, SimpleAi.Score(h, fast.Count), "BestSubmission: " + string.Join(" ", hand));
            }
        }

        [Test] public void Search_MatchesBruteForce_7Cards() { CompareWithBruteForce(7, 3000, 11); }

        [Test] public void Search_MatchesBruteForce_20Cards() { CompareWithBruteForce(20, 20, 12); }

        [Test] public void Search_CraftedHands()
        {
            // 플러시·스트레이트·휠·풀하우스·로열이 섞인 손패
            var hands = new[]
            {
                new[] { (F, 14), (F, 13), (F, 12), (F, 11), (F, 10), (W, 14), (Wd, 14) },          // ROYAL
                new[] { (F, 14), (W, 2), (Wd, 3), (M, 4), (E, 5), (F, 9), (W, 11) },               // 휠
                new[] { (F, 9), (W, 9), (Wd, 9), (M, 4), (E, 4), (F, 2), (F, 3), (F, 6), (F, 8) }, // FULL HOUSE vs FLUSH
                new[] { (F, 7), (W, 7), (Wd, 7), (M, 7), (E, 7), (F, 2) },                         // PENTAGRAM
            };
            foreach (var spec in hands)
            {
                var hand = spec.Select((x, i) => new Card(5000 + i, x.Item1, x.Item2)).ToList();
                bool any;
                int brute = BruteBestScore(hand, out any);
                var fast = SimpleAi.BestSubmission(hand);
                Assert.AreEqual(brute, SimpleAi.Score(HandEvaluator.Evaluate(fast), fast.Count), string.Join(" ", hand));
            }
        }

        [Test] public void Search_20Cards_IsFast()
        {
            var rng = new GameRng(99);
            var hands = new List<List<Card>>();
            for (int i = 0; i < 200; i++) { var d = FullDeck(); rng.Shuffle(d); hands.Add(d.Take(20).ToList()); }
            var sw = Stopwatch.StartNew();
            foreach (var h in hands) { SimpleAi.HasAnyValidHand(h); SimpleAi.BestSubmission(h); }
            sw.Stop();
            TestContext.WriteLine("20장 손패 200개: " + sw.ElapsedMilliseconds + "ms");
            Assert.Less(sw.ElapsedMilliseconds, 1000, "20장 손패 200개 탐색이 1초를 넘음");
        }

        [Test] public void Log_MainBonusLabel_OnlyForNormalHands()
        {
            var en = new EnemyUnit { MaxHp = 100000, Hp = 100000 };
            var penta = DamageCalculator.ResolvePlayerAttack(
                new List<Card> { new Card(1, F, 14), new Card(2, W, 14), new Card(3, Wd, 14), new Card(4, M, 14), new Card(5, E, 14) },
                F, Battle.CreatePlayer(), en, new GameRng(1));
            StringAssert.DoesNotContain("메인", Battle.DescribeAttack(penta), "PENTAGRAM은 메인 보너스 없음");

            var royal = DamageCalculator.ResolvePlayerAttack(
                new List<Card> { new Card(6, F, 10), new Card(7, F, 11), new Card(8, F, 12), new Card(9, F, 13), new Card(10, F, 14) },
                F, Battle.CreatePlayer(), en, new GameRng(1));
            var royalLog = Battle.DescribeAttack(royal);
            StringAssert.DoesNotContain("메인+20%", royalLog);
            StringAssert.Contains("메인 ROYAL +70", royalLog);

            var pair = DamageCalculator.ResolvePlayerAttack(new List<Card> { new Card(11, F, 9), new Card(12, W, 9) }, F, Battle.CreatePlayer(), en, new GameRng(1));
            StringAssert.Contains("메인+20%", Battle.DescribeAttack(pair));
        }

        [Test] public void Stage_RandomFiveElementsAndMain()
        {
            var mainCount = new int[7];
            var elementCount = new int[7];
            for (int seed = 0; seed < 2000; seed++)
            {
                var s = StageSetup.CreateRandom(3, seed);
                Assert.DoesNotThrow(s.Validate);                       // 서로 다른 5속성 + 메인은 그중 하나
                Assert.AreEqual(3, s.Floor);
                Assert.AreEqual(seed, s.Seed);
                var again = StageSetup.CreateRandom(3, seed);
                CollectionAssert.AreEqual(s.Elements, again.Elements); // 같은 시드 → 같은 결과
                Assert.AreEqual(s.MainElement, again.MainElement);
                mainCount[(int)s.MainElement]++;
                foreach (var e in s.Elements) elementCount[(int)e]++;
            }
            // 7속성 모두 메인·구성으로 고르게 나옴 (기대값: 메인 2000/7 ≈ 286, 구성 2000×5/7 ≈ 1429)
            for (int i = 0; i < 7; i++)
            {
                Assert.That(mainCount[i], Is.InRange(200, 380), "메인 " + (Element)i);
                Assert.That(elementCount[i], Is.InRange(1300, 1560), "구성 " + (Element)i);
            }
        }

        [Test] public void AutoDiscard_PicksUnpairedLowest()
        {
            var hand = new List<Card> { new Card(1, F, 9), new Card(2, W, 9), new Card(3, F, 3), new Card(4, W, 12), new Card(5, Wd, 5) };
            var d = SimpleAi.ChooseDiscards(hand);
            CollectionAssert.AreEqual(new[] { 3, 5 }, d.Select(c => c.Rank).ToArray());
        }
    }
}
