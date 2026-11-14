// 열린 기획 결정 회신 (2026-11-14): A1 교체 언제나 허용 · A8 추가 드로우 카드는 그 턴에만
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PokerTower.Core;

namespace PokerTower.Tests
{
    public class RuleTestsDecisions
    {
        private const Element F = Element.Fire, W = Element.Water, Wd = Element.Wood, M = Element.Metal, E = Element.Earth;

        private static Battle Start(PlayerStats s = null, int seed = 5)
        {
            var b = new Battle(new StageSetup { Floor = 1, Seed = seed, MainElement = F, Elements = new List<Element> { F, W, Wd, M, E } }, s);
            b.Begin();
            Assert.AreEqual(Actor.Player, b.NextTurn());
            b.Enemy.MaxHp = b.Enemy.Hp = 100000;
            return b;
        }

        private static PlayerStats AlwaysExtra()
        {
            var s = new PlayerStats(); s.Levels[(int)StatKind.ExtraDraw] = 25; // 100%
            return s;
        }

        [Test] public void A1_Exchange_AllowedEvenWithValidHand_NoDamageNoDiscardPhase()
        {
            var b = Start();
            var best = SimpleAi.BestSubmission(b.Piles.Hand);
            Assert.IsTrue(HandEvaluator.Evaluate(best).IsValid);
            int hp = b.Enemy.Hp;
            var r = b.Submit(best, null, true);
            Assert.IsTrue(r.IsExchange);
            Assert.AreEqual(0, r.TotalDamage);
            Assert.AreEqual(hp, b.Enemy.Hp);
            Assert.AreEqual(BattlePhase.BetweenTurns, b.Phase, "교체 턴은 버리기 단계 없음");
            Assert.AreEqual(GameConfig.HandSize, b.Piles.Hand.Count);
            Assert.AreEqual(65, b.Piles.TotalCards);
        }

        [Test] public void A1_Attack_StillRequiresValidHandWhenNotStuck()
        {
            var b = Start();
            Assert.IsTrue(SimpleAi.HasAnyValidHand(b.Piles.Hand));
            var two = b.Piles.Hand.GroupBy(c => c.Rank).Where(g => g.Count() == 1).SelectMany(g => g).Take(2).ToList();
            Assume.That(two.Count == 2 && !HandEvaluator.Evaluate(two).IsValid);
            string reason;
            Assert.IsFalse(b.CanSubmit(two, out reason));
            Assert.IsTrue(b.CanSubmit(two, out reason, true));
        }

        [Test] public void A8_UnusedExtra_ExpiresAtTurnEnd_NoRefill_NotDiscardable()
        {
            var b = Start(AlwaysExtra());
            b.Submit(SimpleAi.BestSubmission(b.Piles.Hand));
            b.EndPlayerTurn(null);
            Assert.AreEqual(1, b.Piles.Extra.Count);
            var extra = b.Piles.Extra.Single();
            Assert.IsTrue(b.Piles.InHand(extra));

            while (b.NextTurn() != Actor.Player) b.RunEnemyTurn();
            var sub = SimpleAi.BestSubmission(b.Piles.Hand.Where(c => c != extra).ToList()); // 추가 카드는 안 씀
            b.Submit(sub);
            Assert.AreEqual(BattlePhase.PlayerDiscard, b.Phase);
            string reason;
            Assert.IsFalse(b.CanDiscard(new List<Card> { extra }, out reason), "추가 카드는 버리기 단계에서 고를 수 없음");
            b.EndPlayerTurn(null);
            Assert.IsTrue(b.Piles.Graveyard.Contains(extra), "안 쓴 추가 카드는 묘지로");
            Assert.AreEqual(GameConfig.HandSize + 1, b.Piles.Hand.Count, "기본 7장 + 새 추가 1장");
            Assert.AreEqual(1, b.Piles.Extra.Count);
            Assert.IsFalse(b.Piles.IsExtra(extra));
            Assert.AreEqual(65, b.Piles.TotalCards);
        }

        [Test] public void A8_UsedExtra_NotRefilled()
        {
            var b = Start(AlwaysExtra());
            b.Submit(SimpleAi.BestSubmission(b.Piles.Hand));
            b.EndPlayerTurn(null);
            var extra = b.Piles.Extra.Single();
            while (b.NextTurn() != Actor.Player) b.RunEnemyTurn();
            // 추가 카드 + 아무 카드 1장으로 교체 → 일반 카드 1장만 보충
            var other = b.Piles.Hand.First(c => c != extra);
            int deck = b.Piles.Deck.Count;
            b.Submit(new List<Card> { extra, other }, null, true);
            Assert.AreEqual(deck - 1 - 1, b.Piles.Deck.Count, "보충 1장 + 새 추가 드로우 1장");
            Assert.AreEqual(GameConfig.HandSize + 1, b.Piles.Hand.Count);
            Assert.AreEqual(65, b.Piles.TotalCards);
        }
    
        // ───────── 속성 강화 (보스 클리어 보상, 수치 미확정 5%) ─────────

        [Test] public void Boost_BossFirstClear_ThreeDistinctChoices_Stackable()
        {
            var p = new PlayerProgress();
            for (int f = 1; f <= 9; f++) p.AwardClear(f, 1);
            Assert.AreEqual(0, p.PendingElementChoices.Count, "보스가 아니면 후보 없음");
            Assert.AreEqual(3, p.AwardClear(10, 1));
            Assert.AreEqual(3, p.PendingElementChoices.Distinct().Count());
            var e = p.PendingElementChoices[0];
            Assert.Throws<System.InvalidOperationException>(() => p.ChooseElementBoost(ElementInfo.All.First(x => !p.PendingElementChoices.Contains(x))));
            p.ChooseElementBoost(e);
            Assert.AreEqual(1, p.Stats.ElementBoosts[(int)e]);
            Assert.AreEqual(0, p.PendingElementChoices.Count);
            Assert.AreEqual(0, p.AwardClear(10, 2), "같은 보스 재클리어는 보상 없음");
            Assert.AreEqual(0, p.PendingElementChoices.Count);
            p.PendingElementChoices.Add(e);               // 다음 보스에서 같은 속성을 또 골랐다고 가정
            p.ChooseElementBoost(e);
            Assert.AreEqual(2, p.Stats.ElementBoosts[(int)e], "중첩");
        }

        [Test] public void Boost_AppliesOnlyToRepresentativeElement()
        {
            var s = new PlayerStats(); s.ElementBoosts[(int)F] = 2; // +10%
            var player = Battle.CreatePlayer(s);
            var fire = DamageCalculator.ResolvePlayerAttack(new List<Card> { new Card(900, F, 14), new Card(901, W, 14) }, Element.Sun, player,
                new EnemyUnit { MaxHp = 100000, Hp = 100000 }, new GameRng(1));
            Assert.AreEqual(22, fire.NormalDamage, "PAIR AA 20 × 1.1");
            var water = DamageCalculator.ResolvePlayerAttack(new List<Card> { new Card(902, W, 14), new Card(903, F, 14) }, Element.Sun, player,
                new EnemyUnit { MaxHp = 100000, Hp = 100000 }, new GameRng(1));
            Assert.AreEqual(20, water.NormalDamage, "대표 카드가 수면 강화 없음");
        }

        [Test] public void Boost_SaveLoad_V3_AndOldV2Readable()
        {
            var p = new PlayerProgress();
            for (int f = 1; f <= 10; f++) p.AwardClear(f, 7);
            var pend = p.PendingElementChoices.ToList();
            p.Stats.ElementBoosts[(int)M] = 3;
            var q = PlayerProgress.Parse(p.Serialize());
            CollectionAssert.AreEqual(p.Stats.ElementBoosts, q.Stats.ElementBoosts);
            CollectionAssert.AreEqual(pend, q.PendingElementChoices, "고르기 전에 껐다 켜도 후보 유지");
            Assert.AreEqual(p.UnspentPoints, q.UnspentPoints);

            var old = PlayerProgress.Parse("v2;1,1,1,1,1,2;0;7");
            Assert.AreEqual(2, old.Stats[StatKind.Crit]);
            Assert.AreEqual(7, old.HighestClearedFloor);
            Assert.AreEqual(0, old.Stats.ElementBoosts.Sum());

            q.Reset();
            Assert.AreEqual(0, q.Stats.ElementBoosts.Sum(), "새로하기 시 초기화");
            Assert.AreEqual(0, q.PendingElementChoices.Count);
        }
}
}
