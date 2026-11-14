using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerTower.Core
{
    /// <summary>족보 판정 결과.</summary>
    public sealed class HandResult
    {
        public HandType Type;
        /// <summary>07장 공식으로 산출한 기본 피해 B. True Damage 족보는 350.</summary>
        public int BaseDamage;
        /// <summary>FLUSH 계열 족보의 속성 (ROYAL FLUSH 메인 판정에 사용).</summary>
        public Element? FlushElement;
        /// <summary>사람이 읽을 판정 설명 (예: "QQ", "5-하이").</summary>
        public string Detail = "";

        public double Coefficient { get { return GameConfig.Coefficient(Type); } }
        public bool IsTrueDamage { get { return HandInfo.IsTrueDamage(Type); } }
        public bool IsValid { get { return Type != HandType.None; } }
    }

    /// <summary>
    /// 족보 판정과 기본 피해 (기획서 07장 v0.3).
    /// [가정] 제출한 카드 전체를 하나의 조합으로 판정합니다. 족보에 쓰이지 않은 카드(키커)도 함께 묘지로 갑니다.
    ///        제출 카드 중 일부만 골라 더 높은 족보를 찾는 '자동 선택'은 하지 않습니다 (07.4 미정).
    /// [확정] 배치 순서는 STRAIGHT·FLUSH 판정에 영향 없음.
    /// </summary>
    public static class HandEvaluator
    {
        /// <summary>07.2 PAIR 기본 피해 P(r).</summary>
        public static int PairDamage(int rank)
        {
            switch (rank)
            {
                case 2: case 3: case 4: case 5: return 5;
                case 6: return 6;
                case 7: return 7;
                case 8: return 8;
                case 9: return 9;
                case 10: return 10;
                case 11: return 12;
                case 12: return 15;
                case 13: return 18;
                case 14: return 20;
            }
            throw new ArgumentOutOfRangeException(nameof(rank));
        }

        public static HandResult Evaluate(IList<Card> cards)
        {
            var result = new HandResult { Type = HandType.None, BaseDamage = 0 };
            if (cards == null || cards.Count < GameConfig.MinSubmit || cards.Count > GameConfig.MaxSubmit)
                return result;

            int n = cards.Count;
            // 랭크별 장수 (내림차순: 장수 → 랭크)
            var groups = cards.GroupBy(c => c.Rank)
                              .Select(g => new { Rank = g.Key, Count = g.Count() })
                              .OrderByDescending(g => g.Count).ThenByDescending(g => g.Rank)
                              .ToList();

            bool isFive = n == 5;
            bool allSameElement = cards.All(c => c.Element == cards[0].Element);
            bool isFlush = isFive && allSameElement;
            int straightHigh = isFive ? StraightHigh(cards) : 0;
            bool isStraight = straightHigh > 0;
            int rankSum = cards.Sum(c => c.Rank);

            // PENTAGRAM: 같은 랭크 5장 · 5개 속성
            if (isFive && groups[0].Count == 5)
            {
                int distinctElements = cards.Select(c => c.Element).Distinct().Count();
                if (distinctElements == 5)
                {
                    result.Type = HandType.Pentagram;
                    result.BaseDamage = GameConfig.TrueDamageBase;
                    result.Detail = RankInfo.Label(groups[0].Rank) + " ×5";
                    return result;
                }
                // 같은 랭크 5장이지만 속성이 겹치는 경우는 CopiesPerCard=1에서는 발생 불가. 발생 시 FOUR OF A KIND로 처리.
            }

            if (isFlush && isStraight)
            {
                result.FlushElement = cards[0].Element;
                if (straightHigh == 14)
                {
                    result.Type = HandType.RoyalFlush;
                    result.BaseDamage = GameConfig.TrueDamageBase;
                    result.Detail = "T-J-Q-K-A";
                    return result;
                }
                result.Type = HandType.StraightFlush;
                // 200 + Σr. 휠(A-2-3-4-5)은 A를 1로 계산 → 215
                int sum = straightHigh == 5 ? rankSum - 14 + 1 : rankSum;
                result.BaseDamage = 200 + sum;
                result.Detail = straightHigh == 5 ? "A-2-3-4-5" : RankInfo.Label(straightHigh) + "-하이";
                return result;
            }

            if (groups[0].Count >= 4)
            {
                result.Type = HandType.FourOfAKind;
                result.BaseDamage = 160 + 2 * PairDamage(groups[0].Rank);
                result.Detail = RankInfo.Label(groups[0].Rank) + " ×4";
                return result;
            }

            if (isFive && groups[0].Count == 3 && groups[1].Count == 2)
            {
                result.Type = HandType.FullHouse;
                result.BaseDamage = 80 + 3 * PairDamage(groups[0].Rank) + PairDamage(groups[1].Rank);
                result.Detail = RankInfo.Label(groups[0].Rank) + "×3 + " + RankInfo.Label(groups[1].Rank) + "×2";
                return result;
            }

            if (isFlush)
            {
                result.Type = HandType.Flush;
                result.FlushElement = cards[0].Element;
                result.BaseDamage = 100 + rankSum;
                result.Detail = ElementInfo.Korean(cards[0].Element) + " Σ" + rankSum;
                return result;
            }

            if (isStraight)
            {
                result.Type = HandType.Straight;
                result.BaseDamage = 50 + 5 * straightHigh;
                result.Detail = straightHigh == 5 ? "A-2-3-4-5" : RankInfo.Label(straightHigh) + "-하이";
                return result;
            }

            if (groups[0].Count == 3)
            {
                result.Type = HandType.Triple;
                result.BaseDamage = 10 + 3 * PairDamage(groups[0].Rank);
                result.Detail = RankInfo.Label(groups[0].Rank) + " ×3";
                return result;
            }

            if (groups[0].Count == 2 && groups.Count > 1 && groups[1].Count == 2)
            {
                result.Type = HandType.TwoPair;
                result.BaseDamage = PairDamage(groups[0].Rank) + PairDamage(groups[1].Rank);
                result.Detail = RankInfo.Label(groups[0].Rank) + RankInfo.Label(groups[0].Rank) + " + "
                              + RankInfo.Label(groups[1].Rank) + RankInfo.Label(groups[1].Rank);
                return result;
            }

            if (groups[0].Count == 2)
            {
                result.Type = HandType.Pair;
                result.BaseDamage = PairDamage(groups[0].Rank);
                result.Detail = RankInfo.Label(groups[0].Rank) + RankInfo.Label(groups[0].Rank);
                return result;
            }

            return result; // 족보 없음
        }

        /// <summary>5장이 연속 랭크면 최고 랭크(휠은 5), 아니면 0. [확정] A-2-3-4-5만 A 순환 허용, 그 외 순환은 미정이라 불허.</summary>
        public static int StraightHigh(IList<Card> cards)
        {
            if (cards.Count != 5) return 0;
            var ranks = cards.Select(c => c.Rank).Distinct().OrderBy(r => r).ToList();
            if (ranks.Count != 5) return 0;
            if (ranks[4] - ranks[0] == 4) return ranks[4];
            if (ranks[0] == 2 && ranks[1] == 3 && ranks[2] == 4 && ranks[3] == 5 && ranks[4] == 14) return 5;
            return 0;
        }
    }
}
