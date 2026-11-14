using System.Collections.Generic;
using System.Linq;

namespace PokerTower.Core
{
    /// <summary>
    /// 손패 조합 탐색 도우미. 자동 플레이와 '낼 수 있는 족보가 있는가' 판정에 사용합니다.
    /// 부분집합 전수 탐색(2^n) 대신 족보별 후보만 만들어 판정합니다 (손패 장수에 비례하는 비용 → 화면 갱신마다 불러도 됨, 캐시 불필요).
    /// 전수 탐색(Subsets)은 테스트에서 결과 대조용으로만 씁니다.
    /// </summary>
    public static class SimpleAi
    {
        /// <summary>전수 탐색용 부분집합 열거 (n이 크면 매우 느림 — 대조 테스트 전용).</summary>
        public static IEnumerable<List<Card>> Subsets(IList<Card> hand, int minSize, int maxSize)
        {
            int n = hand.Count;
            for (int mask = 1; mask < (1 << n); mask++)
            {
                int bits = 0;
                for (int i = 0; i < n; i++) if ((mask & (1 << i)) != 0) bits++;
                if (bits < minSize || bits > maxSize) continue;
                var list = new List<Card>(bits);
                for (int i = 0; i < n; i++) if ((mask & (1 << i)) != 0) list.Add(hand[i]);
                yield return list;
            }
        }

        /// <summary>
        /// 족보를 하나라도 만들 수 있는지: 같은 랭크 2장(PAIR 이상) / 같은 속성 5장(FLUSH 이상) / 연속 랭크 5개(STRAIGHT).
        /// 나머지 족보(TWO PAIR·TRIPLE·FULL HOUSE·FOUR·PENTAGRAM)는 모두 같은 랭크 2장을 포함하므로 첫 조건에 들어갑니다.
        /// </summary>
        public static bool HasAnyValidHand(IList<Card> hand)
        {
            if (hand.Count < GameConfig.MinSubmit) return false;
            var rankCount = new int[RankInfo.Max + 1];
            var elementCount = new int[ElementInfo.All.Length];
            foreach (var c in hand)
            {
                if (++rankCount[c.Rank] >= 2) return true;
                if (++elementCount[(int)c.Element] >= 5) return true;
            }
            return StraightHighs(rankCount).Count > 0;
        }

        /// <summary>
        /// 기본 피해가 가장 큰 조합 (같은 피해면 적은 장수). 족보가 없으면 가장 낮은 카드 2장(교체용).
        /// 족보별 최선 후보만 만들어 HandEvaluator로 판정합니다:
        ///   랭크 그룹(PAIR·TRIPLE·FOUR·PENTAGRAM), 상위 두 PAIR, FULL HOUSE 조합,
        ///   속성별 상위 5장(FLUSH), 속성별 연속 5랭크(STRAIGHT FLUSH·ROYAL), 연속 5랭크(STRAIGHT).
        /// </summary>
        public static List<Card> BestSubmission(IList<Card> hand)
        {
            if (hand.Count < GameConfig.MinSubmit) return null;
            List<Card> best = null;
            int bestScore = -1;
            foreach (var s in Candidates(hand))
            {
                var h = HandEvaluator.Evaluate(s);
                if (!h.IsValid) continue;
                int score = Score(h, s.Count);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best != null) return best;
            var sorted = hand.OrderBy(c => c.Rank).ThenBy(c => c.Id).ToList();
            return new List<Card> { sorted[0], sorted[1] };
        }

        /// <summary>BestSubmission의 비교 기준 (테스트 대조용으로 공개).</summary>
        public static int Score(HandResult h, int cardCount) { return h.BaseDamage * 10 - cardCount; }

        /// <summary>
        /// [자동 플레이] 버릴 카드: 짝 없는(같은 랭크가 손패에 1장뿐인) 카드 중 낮은 랭크부터 count장.
        /// exact면(강제 버리기) 짝 없는 카드가 모자랄 때 나머지 카드 중 낮은 랭크부터 채워 정확히 count장.
        /// </summary>
        public static List<Card> ChooseDiscards(IList<Card> hand, int count = GameConfig.MaxDiscard, bool exact = false)
        {
            var counts = new int[RankInfo.Max + 1];
            foreach (var c in hand) counts[c.Rank]++;
            var pick = hand.Where(c => counts[c.Rank] == 1).OrderBy(c => c.Rank).ThenBy(c => c.Id).Take(count).ToList();
            if (exact && pick.Count < count)
                pick.AddRange(hand.Where(c => !pick.Contains(c)).OrderBy(c => c.Rank).ThenBy(c => c.Id).Take(count - pick.Count));
            return pick;
        }

        /// <summary>자동 플레이 1턴 (공격 + 버리기 + [v1.3] 강제 버리기). 시뮬레이션·테스트 공용.</summary>
        public static AttackReport PlayTurn(Battle b)
        {
            var report = b.Submit(BestSubmission(b.Piles.Hand));
            if (b.Phase == BattlePhase.PlayerDiscard) b.EndPlayerTurn(ChooseDiscards(b.Piles.Hand.Where(c => !b.Piles.IsExtra(c)).ToList()));
            if (b.Phase == BattlePhase.PlayerForcedDiscard) b.ForceDiscard(ChooseDiscards(b.Piles.Hand, b.ForcedDiscardCount, true));
            return report;
        }

        // ───────── 후보 생성 ─────────

        private static IEnumerable<List<Card>> Candidates(IList<Card> hand)
        {
            var byRank = new List<Card>[RankInfo.Max + 1];
            var rankCount = new int[RankInfo.Max + 1];
            foreach (var c in hand)
            {
                if (byRank[c.Rank] == null) byRank[c.Rank] = new List<Card>();
                byRank[c.Rank].Add(c);
                rankCount[c.Rank]++;
            }

            // 같은 랭크 2~5장 (PAIR · TRIPLE · FOUR · 5장이면 PENTAGRAM 후보)
            var pairRanks = new List<int>();
            for (int r = RankInfo.Min; r <= RankInfo.Max; r++)
            {
                var g = byRank[r];
                if (g == null || g.Count < 2) continue;
                pairRanks.Add(r);
                for (int k = 2; k <= System.Math.Min(5, g.Count); k++) yield return g.Take(k).ToList();
            }

            // TWO PAIR: 가장 높은 두 랭크
            if (pairRanks.Count >= 2)
            {
                int r1 = pairRanks[pairRanks.Count - 1], r2 = pairRanks[pairRanks.Count - 2];
                var s = byRank[r1].Take(2).ToList();
                s.AddRange(byRank[r2].Take(2));
                yield return s;
            }

            // FULL HOUSE: 3장 랭크 × 2장 랭크 모든 조합
            foreach (int t in pairRanks)
            {
                if (byRank[t].Count < 3) continue;
                foreach (int p in pairRanks)
                {
                    if (p == t) continue;
                    var s = byRank[t].Take(3).ToList();
                    s.AddRange(byRank[p].Take(2));
                    yield return s;
                }
            }

            // 속성별: FLUSH(상위 5장) + 연속 5랭크(STRAIGHT FLUSH · ROYAL)
            foreach (var group in hand.GroupBy(c => c.Element))
            {
                var cards = group.ToList();
                if (cards.Count < 5) continue;
                yield return cards.OrderByDescending(c => c.Rank).ThenBy(c => c.Id).Take(5).ToList();
                var elemByRank = new Card[RankInfo.Max + 1];
                var elemCount = new int[RankInfo.Max + 1];
                foreach (var c in cards) { if (elemByRank[c.Rank] == null) elemByRank[c.Rank] = c; elemCount[c.Rank]++; }
                foreach (int high in StraightHighs(elemCount)) yield return StraightCards(high, elemByRank);
            }

            // STRAIGHT: 랭크별 아무 카드 1장씩
            var anyByRank = new Card[RankInfo.Max + 1];
            for (int r = RankInfo.Min; r <= RankInfo.Max; r++) if (byRank[r] != null) anyByRank[r] = byRank[r][0];
            foreach (int high in StraightHighs(rankCount)) yield return StraightCards(high, anyByRank);
        }

        /// <summary>연속 5랭크가 성립하는 최고 랭크 목록 (휠 A-2-3-4-5는 5).</summary>
        private static List<int> StraightHighs(int[] rankCount)
        {
            var list = new List<int>();
            for (int high = 6; high <= RankInfo.Max; high++)
            {
                bool ok = true;
                for (int r = high - 4; r <= high; r++) if (rankCount[r] == 0) { ok = false; break; }
                if (ok) list.Add(high);
            }
            if (rankCount[14] > 0 && rankCount[2] > 0 && rankCount[3] > 0 && rankCount[4] > 0 && rankCount[5] > 0) list.Add(5);
            return list;
        }

        private static List<Card> StraightCards(int high, Card[] pick)
        {
            if (high == 5) return new List<Card> { pick[14], pick[2], pick[3], pick[4], pick[5] };
            var s = new List<Card>(5);
            for (int r = high - 4; r <= high; r++) s.Add(pick[r]);
            return s;
        }
    }
}
