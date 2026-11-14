using System;
using System.Collections.Generic;
using System.Text;

namespace PokerTower.Core
{
    /// <summary>
    /// 테스트 프레임워크 없이 돌릴 수 있는 규칙 셀프체크 (에디터 메뉴 PokerTower > Run Self Check).
    /// 기획서 24장 QA 기대값 일부와 전투 자동 시뮬레이션 불변식을 확인합니다.
    /// </summary>
    public static class SelfCheck
    {
        private static int _id = 100000;

        /// <summary>추천 분산 투자: 레벨이 가장 낮은 스탯부터 한 레벨씩(같으면 스탯 순서) 살 수 있을 때까지. 비용은 레벨별 증가 규칙.</summary>
        public static PlayerStats SpreadEvenly(int points)
        {
            var s = new PlayerStats();
            while (true)
            {
                int pick = 0;
                for (int i = 1; i < PlayerStats.Count; i++) if (s.Levels[i] < s.Levels[pick]) pick = i;
                int cost = PlayerStats.LevelCost(s.Levels[pick] + 1);
                if (cost > points) return s;
                points -= cost;
                s.Levels[pick]++;
            }
        }
        private static Card C(Element e, int r) { return new Card(_id++, e, r); }

        public static string Run()
        {
            var sb = new StringBuilder();
            int fail = 0, total = 0;
            Action<string, object, object> check = (name, got, exp) =>
            {
                total++;
                bool ok = Equals(got, exp);
                if (!ok) fail++;
                sb.AppendLine((ok ? "PASS " : "FAIL ") + name + "  got=" + got + " exp=" + exp);
            };
            const Element F = Element.Fire, W = Element.Water, Wd = Element.Wood, M = Element.Metal, E = Element.Earth, S = Element.Sun;

            check("TRIPLE 222", HandEvaluator.Evaluate(new[] { C(F, 2), C(W, 2), C(Wd, 2) }).BaseDamage, 25);
            check("TRIPLE AAA", HandEvaluator.Evaluate(new[] { C(F, 14), C(W, 14), C(Wd, 14) }).BaseDamage, 70);
            check("STRAIGHT wheel", HandEvaluator.Evaluate(new[] { C(F, 14), C(W, 2), C(Wd, 3), C(M, 4), C(E, 5) }).BaseDamage, 75);
            check("STRAIGHT broadway", HandEvaluator.Evaluate(new[] { C(F, 10), C(W, 11), C(Wd, 12), C(M, 13), C(E, 14) }).BaseDamage, 120);
            check("FULL HOUSE 22233", HandEvaluator.Evaluate(new[] { C(F, 2), C(W, 2), C(Wd, 2), C(M, 3), C(E, 3) }).BaseDamage, 100);
            check("FULL HOUSE AAAKK", HandEvaluator.Evaluate(new[] { C(F, 14), C(W, 14), C(Wd, 14), C(M, 13), C(E, 13) }).BaseDamage, 158);
            check("FLUSH AJ854", HandEvaluator.Evaluate(new[] { C(F, 14), C(F, 11), C(F, 8), C(F, 5), C(F, 4) }).BaseDamage, 142);
            check("FOUR 2222", HandEvaluator.Evaluate(new[] { C(F, 2), C(W, 2), C(Wd, 2), C(M, 2) }).BaseDamage, 170);
            check("SF A2345", HandEvaluator.Evaluate(new[] { C(F, 14), C(F, 2), C(F, 3), C(F, 4), C(F, 5) }).BaseDamage, 215);
            check("SF 9TJQK", HandEvaluator.Evaluate(new[] { C(F, 9), C(F, 10), C(F, 11), C(F, 12), C(F, 13) }).BaseDamage, 255);
            check("ROYAL type", HandEvaluator.Evaluate(new[] { C(S, 10), C(S, 11), C(S, 12), C(S, 13), C(S, 14) }).Type, HandType.RoyalFlush);

            var en = new EnemyUnit { MaxHp = 99999, Hp = 99999, BaseDef = 100, BaseSpeed = 90, Atk = 10 };
            var p = Battle.CreatePlayer();
            var r1 = DamageCalculator.ResolvePlayerAttack(new List<Card> { C(M, 9), C(F, 9) }, F, p, en, new GameRng(1));
            check("QA-GOLD-01 DEF", Math.Round(r1.DefUsed, 6), 80.0);
            check("QA-GOLD-01 dmg", DamageCalculator.NormalDamage(100, r1.DefUsed), 55);
            var r2 = DamageCalculator.ResolvePlayerAttack(new List<Card> { C(M, 5), C(F, 5), C(W, 5) }, F, p, en, new GameRng(1));
            check("QA-GOLD-02 DEF", Math.Round(r2.DefUsed, 6), 59.2);
            check("QA-GOLD-02 dmg", DamageCalculator.NormalDamage(100, r2.DefUsed), 62);

            var en2 = new EnemyUnit { MaxHp = 99999, Hp = 99999, BaseDef = 500, BaseSpeed = 90, Atk = 10 };
            var r3 = DamageCalculator.ResolvePlayerAttack(new List<Card> { C(S, 10), C(S, 11), C(S, 12), C(S, 13), C(S, 14) }, S, p, en2, new GameRng(1));
            check("QA-DMG-07 ROYAL main true", r3.TrueDamage, 420);

            // v1.2: 스탯 · 치명타 · 등급
            var boosted = new PlayerStats(); boosted.Levels[(int)StatKind.Damage] = 5; // +10%
            var pb = Battle.CreatePlayer(boosted);
            check("v1.2 데미지+10% PAIR AA", DamageCalculator.ResolvePlayerAttack(new List<Card> { C(F, 14), C(W, 14) }, S, pb, new EnemyUnit { MaxHp = 9999, Hp = 9999 }, new GameRng(1)).NormalDamage, 22);
            check("v1.2 데미지+10% True", DamageCalculator.ResolvePlayerAttack(new List<Card> { C(F, 7), C(W, 7), C(Wd, 7), C(M, 7), C(E, 7) }, F, pb, new EnemyUnit { MaxHp = 9999, Hp = 9999 }, new GameRng(1)).TrueDamage, 385);
            int absorbed, loss;
            DamageCalculator.ResolveEnemyHit(10, Battle.CreatePlayer(), out absorbed, out loss);
            check("v1.2 DEF10 피격 ATK10", loss, 9);
            DamageCalculator.ResolveEnemyHit(10 * GameConfig.CritMultiplier, Battle.CreatePlayer(), out absorbed, out loss);
            check("v1.2 치명타 ATK10", loss, 13);
            check("v1.2 등급 5/10/15/20", GameConfig.TierOf(5) + "/" + GameConfig.TierOf(10) + "/" + GameConfig.TierOf(15) + "/" + GameConfig.TierOf(20),
                  "Elite/Boss/Elite/Boss");

            // v1.3: 포인트 · 비용 · 플레이어 치명타
            check("v1.3 포인트 4/5/10/15/20층", GameConfig.StatPointsFor(4) + "/" + GameConfig.StatPointsFor(5) + "/" + GameConfig.StatPointsFor(10)
                  + "/" + GameConfig.StatPointsFor(15) + "/" + GameConfig.StatPointsFor(20), "1/2/3/2/3");
            check("v1.3 비용 0→3레벨", PlayerStats.CostBetween(0, 3), 6);
            var crit = new PlayerStats(); crit.Levels[(int)StatKind.Crit] = 20; // 100%
            check("v1.3 치명타 PAIR AA", DamageCalculator.ResolvePlayerAttack(new List<Card> { C(F, 14), C(W, 14) }, S, Battle.CreatePlayer(crit), new EnemyUnit { MaxHp = 9999, Hp = 9999 }, new GameRng(1)).NormalDamage, 30);
            crit.Levels[(int)StatKind.Damage] = 5;
            check("v1.3 치명타+데미지10% PAIR AA", DamageCalculator.ResolvePlayerAttack(new List<Card> { C(F, 14), C(W, 14) }, S, Battle.CreatePlayer(crit), new EnemyUnit { MaxHp = 9999, Hp = 9999 }, new GameRng(1)).NormalDamage, 33);

            // 전투 시뮬레이션: 스탯 투자 없음 vs 추천 분산 투자(그 층 전까지 받은 포인트를 6개 스탯에 고르게)
            // 자동 플레이 = 최고 기본 피해 + 짝 없는 낮은 카드 버리기. 속성 구성은 게임과 같이 무작위 (StageSetup.CreateRandom)
            int broken = 0;
            foreach (int floor in new[] { 1, 5, 9, 10 })
            {
                int earned = 0;
                for (int f = 1; f < floor; f++) earned += GameConfig.StatPointsFor(f);
                var spread = SpreadEvenly(earned);
                string line = "SIM " + floor + "층(" + EnemyTierInfo.Korean(GameConfig.TierOf(floor)) + ")";
                foreach (var stats in new[] { (PlayerStats)null, spread })
                {
                    int wins = 0, battles = 0, stuckTurns = 0, playerTurns = 0;
                    for (int seed = 0; seed < 200; seed++)
                    {
                        var b = new Battle(StageSetup.CreateRandom(floor, seed), stats);
                        b.Begin();
                        int guard = 0;
                        while (!b.IsOver && guard++ < 1000)
                        {
                            if (b.NextTurn() == Actor.Enemy) { b.RunEnemyTurn(); continue; }
                            if (!SimpleAi.HasAnyValidHand(b.Piles.Hand)) stuckTurns++;
                            SimpleAi.PlayTurn(b);
                            playerTurns++;
                            if (b.Piles.TotalCards != 65 || b.Piles.InPlay.Count != 0) broken++;
                            if (!b.IsOver && (b.Piles.Hand.Count > GameConfig.MaxHandSize || b.Piles.Hand.Count < GameConfig.HandSize)) broken++;
                        }
                        battles++;
                        if (b.Phase == BattlePhase.Victory) wins++;
                        if (!b.IsOver) broken++;
                    }
                    line += (stats == null ? " | 투자 없음: 승률 " : " | 분산 투자(" + earned + "점 " + string.Join(",", spread.Levels) + "): 승률 ")
                          + (100.0 * wins / battles).ToString("0.0") + "% · 족보 없는 턴 " + (100.0 * stuckTurns / System.Math.Max(1, playerTurns)).ToString("0.0") + "%";
                }
                sb.AppendLine(line);
            }
            check("시뮬레이션 상태 불변식 위반 수", broken, 0);
            sb.Insert(0, "selfcheck total=" + total + " fail=" + fail + "\n");
            return sb.ToString();
        }
    }
}
