// Unity: Window > General > Test Runner > EditMode > Run All
// 기획서 24장 QA 기대값을 그대로 옮긴 테스트입니다.
using System.Collections.Generic;
using NUnit.Framework;
using PokerTower.Core;

namespace PokerTower.Tests
{
    public class RuleTests
    {
        private static int _id;
        private static Card C(Element e, int rank) { return new Card(_id++, e, rank); }

        private static HandResult Eval(params Card[] cards) { return HandEvaluator.Evaluate(cards); }

        private static readonly Element F = Element.Fire, W = Element.Water, Wd = Element.Wood,
                                        M = Element.Metal, E = Element.Earth, S = Element.Sun, Mo = Element.Moon;

        // ───────── 07장 기본 피해 ─────────
        [Test] public void QA09_PairTable()
        {
            int[] expected = { 5, 5, 5, 5, 6, 7, 8, 9, 10, 12, 15, 18, 20 };
            for (int r = 2; r <= 14; r++)
                Assert.AreEqual(expected[r - 2], Eval(C(F, r), C(W, r)).BaseDamage, "rank " + r);
        }

        [Test] public void QA10_TwoPairAndTriple()
        {
            Assert.AreEqual(33, Eval(C(F, 13), C(W, 13), C(F, 12), C(W, 12)).BaseDamage);
            Assert.AreEqual(25, Eval(C(F, 2), C(W, 2), C(Wd, 2)).BaseDamage);
            Assert.AreEqual(70, Eval(C(F, 14), C(W, 14), C(Wd, 14)).BaseDamage);
        }

        [Test] public void QA11_ElementDoesNotChangeBaseDamage()
        {
            Assert.AreEqual(Eval(C(F, 9), C(W, 9)).BaseDamage, Eval(C(S, 9), C(Mo, 9)).BaseDamage);
        }

        [Test] public void QADMG02_Straight()
        {
            Assert.AreEqual(75, Eval(C(F, 14), C(W, 2), C(Wd, 3), C(M, 4), C(E, 5)).BaseDamage);
            Assert.AreEqual(120, Eval(C(F, 10), C(W, 11), C(Wd, 12), C(M, 13), C(E, 14)).BaseDamage);
            var h = Eval(C(M, 8), C(F, 5), C(W, 7), C(Wd, 6), C(E, 9)); // 배치 순서 무관
            Assert.AreEqual(HandType.Straight, h.Type);
            Assert.AreEqual(95, h.BaseDamage);
        }

        [Test] public void QADMG03_FullHouse()
        {
            Assert.AreEqual(100, Eval(C(F, 2), C(W, 2), C(Wd, 2), C(M, 3), C(E, 3)).BaseDamage);
            Assert.AreEqual(158, Eval(C(F, 14), C(W, 14), C(Wd, 14), C(M, 13), C(E, 13)).BaseDamage);
        }

        [Test] public void QADMG04_Flush()
        {
            Assert.AreEqual(142, Eval(C(F, 14), C(F, 11), C(F, 8), C(F, 5), C(F, 4)).BaseDamage);
        }

        [Test] public void QADMG05_FourOfAKind()
        {
            Assert.AreEqual(170, Eval(C(F, 2), C(W, 2), C(Wd, 2), C(M, 2)).BaseDamage);
            Assert.AreEqual(200, Eval(C(F, 14), C(W, 14), C(Wd, 14), C(M, 14)).BaseDamage);
        }

        [Test] public void QADMG06_StraightFlush()
        {
            Assert.AreEqual(215, Eval(C(F, 14), C(F, 2), C(F, 3), C(F, 4), C(F, 5)).BaseDamage);
            Assert.AreEqual(255, Eval(C(F, 9), C(F, 10), C(F, 11), C(F, 12), C(F, 13)).BaseDamage);
        }

        [Test] public void QADMG07_08_TrueDamageHands()
        {
            Assert.AreEqual(HandType.RoyalFlush, Eval(C(S, 10), C(S, 11), C(S, 12), C(S, 13), C(S, 14)).Type);
            Assert.AreEqual(HandType.Pentagram, Eval(C(F, 7), C(W, 7), C(Wd, 7), C(M, 7), C(E, 7)).Type);
        }

        [Test] public void NoHand_IsInvalid()
        {
            Assert.IsFalse(Eval(C(F, 2), C(W, 9)).IsValid);
        }

        // ───────── 08장 최종 피해 ─────────
        private static EnemyUnit Enemy(double def, int hp = 100000)
        {
            return new EnemyUnit { MaxHp = hp, Hp = hp, BaseDef = def, BaseSpeed = 90, Atk = 10 };
        }

        private static PlayerUnit Player() { return Battle.CreatePlayer(); }

        [Test] public void QADMG07_Royal_MainAndDefIgnored()
        {
            var en = Enemy(500);
            var cards = new List<Card> { C(S, 10), C(S, 11), C(S, 12), C(S, 13), C(S, 14) };
            // 메인 = 일 → 420. 일 효과의 추가 피해는 일반 피해로 별도(DEF 500 적용).
            var r = DamageCalculator.ResolvePlayerAttack(cards, Element.Sun, Player(), en, new GameRng(1));
            Assert.AreEqual(420, r.TrueDamage);
            Assert.AreEqual(DamageCalculator.NormalDamage(350 * 0.3 * 4.0, 500), r.NormalDamage);
        }

        [Test] public void QADMG08_Pentagram_NoMainBonus()
        {
            var en = Enemy(500);
            var cards = new List<Card> { C(F, 7), C(W, 7), C(Wd, 7), C(M, 7), C(E, 7) };
            var r = DamageCalculator.ResolvePlayerAttack(cards, Element.Fire, Player(), en, new GameRng(1));
            Assert.AreEqual(350, r.TrueDamage);
        }

        [Test] public void QAATTR04_SunAndMainAreAdditive()
        {
            var en = Enemy(0);
            // PAIR AA = 20, 일 + 메인(일): 20 × (1 + 0.3 + 0.2) = 30
            var r = DamageCalculator.ResolvePlayerAttack(new List<Card> { C(S, 14), C(F, 14) }, Element.Sun, Player(), en, new GameRng(1));
            Assert.AreEqual(30, r.NormalDamage);
        }

        [Test] public void QAGOLD01_02_05_MetalCompound()
        {
            var en = Enemy(100);
            var p = Player();
            // 원페어 금: DEF 100 → 80
            var r1 = DamageCalculator.ResolvePlayerAttack(new List<Card> { C(M, 9), C(F, 9) }, Element.Fire, p, en, new GameRng(1));
            Assert.AreEqual(80.0, r1.DefUsed, 1e-9);
            Assert.AreEqual(55, DamageCalculator.NormalDamage(100, r1.DefUsed)); // QA-GOLD-01

            // 적 턴 없이 트리플 금(26%): 80 × 0.74 = 59.2
            var r2 = DamageCalculator.ResolvePlayerAttack(new List<Card> { C(M, 5), C(F, 5), C(W, 5) }, Element.Fire, p, en, new GameRng(1));
            Assert.IsTrue(r2.MetalCompounded);
            Assert.AreEqual(59.2, r2.DefUsed, 1e-9);
            Assert.AreEqual(62, DamageCalculator.NormalDamage(100, r2.DefUsed)); // QA-GOLD-02

            // 비금 공격 → 방깎 해제, 원본 DEF 100 복귀 (QA-GOLD-04, 05)
            DamageCalculator.ResolvePlayerAttack(new List<Card> { C(F, 3), C(W, 3) }, Element.Fire, p, en, new GameRng(1));
            Assert.IsFalse(en.Metal.Active);
            Assert.AreEqual(100.0, en.EffectiveDef, 1e-9);
            Assert.AreEqual(100.0, en.BaseDef, 1e-9);
        }

        [Test] public void QAGOLD03_MetalPersistsThroughEnemyTurn_NoCompoundAfter()
        {
            var en = Enemy(100);
            var p = Player();
            DamageCalculator.ResolvePlayerAttack(new List<Card> { C(M, 9), C(F, 9) }, Element.Fire, p, en, new GameRng(1));
            en.Metal.EnemyActedSinceApplied = true; // 적 턴 경과
            Assert.IsTrue(en.Metal.Active);
            Assert.AreEqual(80.0, en.EffectiveDef, 1e-9);
            var r = DamageCalculator.ResolvePlayerAttack(new List<Card> { C(M, 5), C(F, 5), C(W, 5) }, Element.Fire, p, en, new GameRng(1));
            Assert.IsFalse(r.MetalCompounded);
            Assert.AreEqual(74.0, r.DefUsed, 1e-9); // [가정] 원본 DEF에 새로 적용
        }

        [Test] public void QAATTR02_BurnRefreshRules()
        {
            var b = new BurnState();
            b.Apply(10);              // 강 2회
            b.Apply(5);               // 약: 2회 남았으므로 무시
            Assert.AreEqual(2, b.Ticks.Count); Assert.AreEqual(10, b.Ticks[0].Amount); Assert.AreEqual(10, b.Ticks[1].Amount);
            Assert.AreEqual(10, b.Consume());
            b.Apply(5);               // 강 1회 + 약 → [강1, 약1]
            Assert.AreEqual(10, b.Ticks[0].Amount); Assert.AreEqual(5, b.Ticks[1].Amount);
            Assert.AreEqual(10, b.Consume());
            b.Apply(8);               // 약 1회 남음 + 강 → 강 2회
            Assert.AreEqual(2, b.Ticks.Count); Assert.AreEqual(8, b.Ticks[0].Amount);
        }

        [Test] public void QAATTR03_WoodHealsFromActualHpLoss()
        {
            var en = Enemy(0, hp: 10);
            var p = Player(); p.Hp = 50;
            // TRIPLE AAA = 70 피해, 실제 HP 감소 10 → floor(10 × 0.1 × 1.3) = 1
            var r = DamageCalculator.ResolvePlayerAttack(new List<Card> { C(Wd, 14), C(F, 14), C(W, 14) }, Element.Fire, p, en, new GameRng(1));
            Assert.AreEqual(10, r.ActualHpLoss);
            Assert.AreEqual(1, r.Healed);
        }

        [Test] public void QAATTR05_MoonCopiesEffectButKeepsMoonForMain()
        {
            var en = Enemy(0);
            var p = Player();
            // 월 메인, 복제=일 → 20 × (1 + 0.3 + 0.2) = 30
            var r = DamageCalculator.ResolvePlayerAttack(new List<Card> { C(Mo, 14), C(F, 14) }, Element.Moon, p, en, new GameRng(1), Element.Sun);
            Assert.AreEqual(Element.Moon, r.AttackElement);
            Assert.AreEqual(Element.Sun, r.EffectElement);
            Assert.AreEqual(30, r.NormalDamage);
            // 월 공격·메인=일: 복제된 일이 메인이어도 +20% 없음 → 20 × 1.3 = 26
            var en2 = Enemy(0);
            var r2 = DamageCalculator.ResolvePlayerAttack(new List<Card> { C(Mo, 14), C(F, 14) }, Element.Sun, p, en2, new GameRng(1), Element.Sun);
            Assert.AreEqual(26, r2.NormalDamage);
        }

        // ───────── 속도 ─────────
        [Test] public void QASPD01_DoubleSpeedActsTwice()
        {
            var p = Player(); p.BaseSpeed = 200;
            var en = Enemy(0); en.BaseSpeed = 100;
            var s = new TurnScheduler(p, en);
            var preview = s.Preview(6);
            var actual = new List<Actor>();
            for (int i = 0; i < 6; i++) { var a = s.Advance(); actual.Add(a); s.Reset(a); }
            CollectionAssert.AreEqual(preview, actual); // 속도 바 예측 == 실제
            int playerTurns = actual.FindAll(a => a == Actor.Player).Count;
            Assert.AreEqual(4, playerTurns);
        }

        [Test] public void QAATTR01_WaterBuffAppliesOnceAndRefreshes()
        {
            var p = Player();
            var en = Enemy(0);
            var s = new TurnScheduler(p, en);
            p.PendingSpeedBuff = 0.10;
            p.PendingSpeedBuff = 0.13; // 연속 수 공격: 누적 아닌 갱신
            s.Reset(Actor.Player);
            Assert.AreEqual(10000.0 / (100 * 1.13), p.ActionValue, 1e-9);
            Assert.AreEqual(0.0, p.PendingSpeedBuff);
            s.Reset(Actor.Player);
            Assert.AreEqual(100.0, p.ActionValue, 1e-9); // 1회 후 원래 속도
        }

        // ───────── 덱/묘지 ─────────
        [Test] public void QA08_UsedCardsGoToGraveyard_NotRedrawnImmediately()
        {
            var piles = new CardPiles(new[] { F, W, Wd, M, E }, new GameRng(3));
            Assert.AreEqual(65, piles.TotalCards);
            piles.Draw(7);
            var used = new List<Card> { piles.Hand[0], piles.Hand[1] };
            piles.CommitSubmission(used);
            Assert.AreEqual(7, piles.Hand.Count);
            Assert.AreEqual(65, piles.TotalCards);
            CollectionAssert.IsSubsetOf(used, piles.Graveyard);
            foreach (var c in used) Assert.IsFalse(piles.Hand.Contains(c));
        }

        [Test] public void Battle_FullSimulation_Invariants()
        {
            // 단순 AI로 전투를 끝까지 돌려 상태가 깨지지 않는지 확인
            for (int seed = 0; seed < 30; seed++)
            {
                var setup = new StageSetup { Floor = 1 + seed % 12, Seed = seed, MainElement = F,
                    Elements = new List<Element> { F, W, Wd, M, E } };
                var battle = new Battle(setup);
                battle.Begin();
                int guard = 0;
                while (!battle.IsOver && guard++ < 500)
                {
                    var actor = battle.NextTurn();
                    if (actor == Actor.Enemy) { battle.RunEnemyTurn(); continue; }
                    SimpleAi.PlayTurn(battle); // v1.2: 공격 + 턴 종료 버리기
                    Assert.AreEqual(65, battle.Piles.TotalCards);
                    Assert.LessOrEqual(battle.Player.Hp, battle.Player.MaxHp);
                    Assert.GreaterOrEqual(battle.Enemy.EffectiveDef, 0.0);
                }
                Assert.IsTrue(battle.IsOver, "전투가 끝나지 않음 seed=" + seed);
            }
        }
    }
}
