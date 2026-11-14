// 기획서 v1.3 (2026-11-14): 등급별 스탯 포인트 · 레벨별 투자 비용 · 플레이어 치명타 · 손패 최대 10장(강제 버리기)
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PokerTower.Core;

namespace PokerTower.Tests
{
    public class RuleTestsV13
    {
        private const Element F = Element.Fire, W = Element.Water, Wd = Element.Wood, M = Element.Metal, E = Element.Earth;
        private static int _id = 20000;
        private static Card C(Element e, int r) { return new Card(_id++, e, r); }

        // ───────── 변경 1. 포인트 지급 ─────────

        [Test] public void Points_ByTier_FirstClearOnly()
        {
            Assert.AreEqual(1, GameConfig.StatPointsFor(4));
            Assert.AreEqual(2, GameConfig.StatPointsFor(5));
            Assert.AreEqual(3, GameConfig.StatPointsFor(10));
            Assert.AreEqual(2, GameConfig.StatPointsFor(15));
            Assert.AreEqual(3, GameConfig.StatPointsFor(20));

            var p = new PlayerProgress();
            int total = 0;
            foreach (int f in new[] { 4, 5, 10, 15, 20 }) total += p.AwardClear(f);
            Assert.AreEqual(1 + 2 + 3 + 2 + 3, total);
            Assert.AreEqual(0, p.AwardClear(20), "같은 층 재클리어 0점");
            Assert.AreEqual(0, p.AwardClear(10), "이전 층 재클리어 0점");
            Assert.AreEqual(11, p.UnspentPoints);
        }

        // ───────── 변경 2. 레벨별 비용 ─────────

        [Test] public void Cost_IncreasesPerLevel_RefundsExactly()
        {
            Assert.AreEqual(1, PlayerStats.CostBetween(0, 1));
            Assert.AreEqual(2, PlayerStats.CostBetween(1, 2));
            Assert.AreEqual(6, PlayerStats.CostBetween(0, 3));
            Assert.AreEqual(-5, PlayerStats.CostBetween(3, 1), "3→1레벨 되돌리기 = 3 + 2 환불");

            var p = new PlayerProgress { UnspentPoints = 6 };
            string reason;
            // 미리보기: 체력 +3레벨 = 6점, 그 상태에서 다른 스탯 1레벨은 부족
            Assert.AreEqual(6, p.CostOf(new[] { 3, 0, 0, 0, 0, 0 }));
            Assert.IsTrue(p.CanCommit(new[] { 3, 0, 0, 0, 0, 0 }, out reason));
            Assert.IsFalse(p.CanCommit(new[] { 3, 1, 0, 0, 0, 0 }, out reason), "7점 필요 → 거부");
            // 미리보기에서 3레벨 → 1레벨로 내리면 1점만 쓰고 5점이 남음 (스탯마다 따로 셈: 속도 1레벨은 1점)
            Assert.AreEqual(1, p.CostOf(new[] { 1, 0, 0, 0, 0, 0 }));
            Assert.AreEqual(2, p.CostOf(new[] { 1, 1, 0, 0, 0, 0 }));
            Assert.AreEqual(2, p.NextLevelCost(StatKind.MaxHp, 1), "1레벨 미리보기 상태에서 다음(2레벨) 비용 2");

            p.Commit(new[] { 2, 0, 0, 0, 0, 0 });                 // 1 + 2 = 3점
            Assert.AreEqual(3, p.UnspentPoints);
            Assert.AreEqual(3, p.NextLevelCost(StatKind.MaxHp, 0), "확정 후 체력 3레벨 비용 3");
            Assert.AreEqual(1, p.NextLevelCost(StatKind.Speed, 0));
            Assert.IsFalse(p.CanCommit(new[] { 2, 0, 0, 0, 0, 0 }, out reason), "2→4레벨은 7점 → 부족");
        }

        // ───────── 변경 3. 플레이어 치명타 ─────────

        private static AttackReport Attack(PlayerStats s, List<Card> cards, Element main, double def = 0)
        {
            var en = new EnemyUnit { MaxHp = 100000, Hp = 100000, BaseDef = def };
            return DamageCalculator.ResolvePlayerAttack(cards, main, Battle.CreatePlayer(s), en, new GameRng(1));
        }

        [Test] public void Crit_100Percent_150Damage_MultipliesWithDamageStat()
        {
            var s = new PlayerStats(); s.Levels[(int)StatKind.Crit] = 20; // 100%
            Assert.AreEqual(1.0, s.CritChance, 1e-9);
            var pair = Attack(s, new List<Card> { C(F, 14), C(W, 14) }, Element.Sun);
            Assert.IsTrue(pair.Critical);
            Assert.AreEqual(30, pair.NormalDamage, "PAIR AA 20 → 30");
            Assert.AreEqual(4, pair.BurnPerTick, "화상은 치명타 반영 후 최종 피해 기준: floor(30 × 0.3 × 1.0 / 2) = 4");
            var penta = Attack(s, new List<Card> { C(F, 7), C(W, 7), C(Wd, 7), C(M, 7), C(E, 7) }, F, 500);
            Assert.AreEqual(525, penta.TrueDamage, "True 350 → 525");

            s.Levels[(int)StatKind.Damage] = 5; // +10%
            Assert.AreEqual(33, Attack(s, new List<Card> { C(F, 14), C(W, 14) }, Element.Sun).NormalDamage, "20 × 1.1 × 1.5 = 33");

            s.Levels[(int)StatKind.Crit] = 30; // 150% → 상한 100%
            Assert.AreEqual(1.0, s.CritChance, 1e-9);
        }

        [Test] public void Crit_ZeroChance_NoCrit_ExchangeNotRolled()
        {
            var none = Attack(new PlayerStats(), new List<Card> { C(F, 14), C(W, 14) }, Element.Sun);
            Assert.IsFalse(none.Critical);
            Assert.AreEqual(20, none.NormalDamage);

            var s = new PlayerStats(); s.Levels[(int)StatKind.Crit] = 20;
            var exchange = Attack(s, new List<Card> { C(F, 2), C(W, 9) }, Element.Sun); // 족보 없음
            Assert.IsTrue(exchange.IsExchange);
            Assert.IsFalse(exchange.Critical);
            Assert.AreEqual(0, exchange.TotalDamage);
        }

        [Test] public void Crit_SameSeed_SameResult()
        {
            var s = new PlayerStats(); s.Levels[(int)StatKind.Crit] = 10; // 50%
            var seq1 = new List<bool>(); var seq2 = new List<bool>();
            foreach (var seq in new[] { seq1, seq2 })
            {
                var rng = new GameRng(77);
                for (int i = 0; i < 50; i++)
                    seq.Add(DamageCalculator.ResolvePlayerAttack(new List<Card> { C(F, 9), C(W, 9) }, Element.Sun, Battle.CreatePlayer(s),
                        new EnemyUnit { MaxHp = 100000, Hp = 100000 }, rng).Critical);
            }
            CollectionAssert.AreEqual(seq1, seq2);
            Assert.That(seq1.Count(x => x), Is.InRange(10, 40), "50% 근처");
        }

        // ───────── 변경 4. 손패 최대 10장 · 강제 버리기 ─────────

        [Test] public void HandCap_ExtraDrawOverflow_ForcedDiscardOne()
        {
            var s = new PlayerStats(); s.Levels[(int)StatKind.ExtraDraw] = 25; // 4%p × 25 = 100%
            var b = new Battle(new StageSetup { Floor = 1, Seed = 3, MainElement = F, Elements = new List<Element> { F, W, Wd, M, E } }, s);
            b.Begin();
            Assert.AreEqual(Actor.Player, b.NextTurn());
            b.Enemy.MaxHp = b.Enemy.Hp = 100000;
            // 손패를 10장으로 맞춤 (덱에서 3장 더)
            while (b.Piles.Hand.Count < 10) { var c = b.Piles.Deck[0]; b.Piles.Deck.RemoveAt(0); b.Piles.Hand.Add(c); }
            Assert.IsTrue(SimpleAi.HasAnyValidHand(b.Piles.Hand));
            b.Submit(SimpleAi.BestSubmission(b.Piles.Hand));
            Assert.AreEqual(BattlePhase.PlayerDiscard, b.Phase);
            b.EndPlayerTurn(null);                                 // 보충 + 추가 드로우 100% → 11장
            Assert.AreEqual(BattlePhase.PlayerForcedDiscard, b.Phase);
            Assert.AreEqual(11, b.Piles.Hand.Count);
            Assert.AreEqual(1, b.ForcedDiscardCount);

            string reason;
            Assert.IsFalse(b.CanForceDiscard(new List<Card>(), out reason), "0장 불가");
            Assert.IsFalse(b.CanForceDiscard(b.Piles.Hand.Take(2).ToList(), out reason), "넘는 장수보다 많이 불가");
            int deckBefore = b.Piles.Deck.Count, graveBefore = b.Piles.Graveyard.Count;
            var victim = b.Piles.Hand[0];
            b.ForceDiscard(new List<Card> { victim });
            Assert.AreEqual(10, b.Piles.Hand.Count);
            Assert.AreEqual(deckBefore, b.Piles.Deck.Count, "강제 버림으로 보충 없음");
            Assert.AreEqual(graveBefore + 1, b.Piles.Graveyard.Count);
            Assert.IsTrue(b.Piles.Graveyard.Contains(victim));
            Assert.AreEqual(65, b.Piles.TotalCards);
            Assert.AreEqual(0, b.Piles.InPlay.Count);
            Assert.AreEqual(BattlePhase.BetweenTurns, b.Phase, "강제 버림 뒤 턴 종료 처리까지 완료");
        }

        [Test] public void HandCap_NoForcedDiscardAtOrBelow10()
        {
            var b = new Battle(new StageSetup { Floor = 1, Seed = 4, MainElement = F, Elements = new List<Element> { F, W, Wd, M, E } });
            b.Begin();
            Assert.AreEqual(Actor.Player, b.NextTurn());
            b.Enemy.MaxHp = b.Enemy.Hp = 100000;
            SimpleAi.PlayTurn(b);
            Assert.AreEqual(BattlePhase.BetweenTurns, b.Phase);
            Assert.AreEqual(GameConfig.HandSize, b.Piles.Hand.Count);
        }

        [Test] public void AutoForcedDiscard_ExactCount_UnpairedLowFirst()
        {
            var hand = new List<Card> { C(F, 9), C(W, 9), C(F, 3), C(W, 3), C(Wd, 12) };
            CollectionAssert.AreEqual(new[] { 12 }, SimpleAi.ChooseDiscards(hand, 1, true).Select(c => c.Rank).ToArray());
            // 짝 없는 카드가 1장뿐이라 나머지는 낮은 랭크부터 채움
            CollectionAssert.AreEqual(new[] { 12, 3, 3 }, SimpleAi.ChooseDiscards(hand, 3, true).Select(c => c.Rank).ToArray());
            Assert.AreEqual(1, SimpleAi.ChooseDiscards(hand, 3).Count, "일반 버리기는 짝 없는 카드만");
        }
    }
}
