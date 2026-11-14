// POKER TOWER - 엔진 독립 규칙 코어 (UnityEngine 참조 없음)
// 이 폴더의 코드는 Godot(C#) 쪽과도 같은 규칙을 비교 검증할 수 있도록 순수 C#으로 유지합니다.
using System;
using System.Collections.Generic;

namespace PokerTower.Core
{
    /// <summary>7속성. 기획서 05.1 [확정] 화 수 목 금 토 일 월.</summary>
    public enum Element { Fire = 0, Water = 1, Wood = 2, Metal = 3, Earth = 4, Sun = 5, Moon = 6 }

    /// <summary>족보. 순서는 기획서 06.2 특성 계수 C의 오름차순과 동일 (STRAIGHT &lt; FULL HOUSE &lt; FLUSH).</summary>
    public enum HandType
    {
        None = 0,
        Pair,
        TwoPair,
        Triple,
        Straight,
        FullHouse,
        Flush,
        FourOfAKind,
        StraightFlush,
        RoyalFlush,
        Pentagram
    }

    /// <summary>에너미 등급 (v1.2 [확정]): 10의 배수 층 보스, 그 외 5의 배수 층 엘리트, 나머지 일반.</summary>
    public enum EnemyTier { Normal = 0, Elite, Boss }

    public static class EnemyTierInfo
    {
        public static string Korean(EnemyTier t)
        {
            switch (t)
            {
                case EnemyTier.Elite: return "엘리트";
                case EnemyTier.Boss: return "보스";
            }
            return "일반";
        }
    }

    public static class ElementInfo
    {
        public static readonly Element[] All =
            { Element.Fire, Element.Water, Element.Wood, Element.Metal, Element.Earth, Element.Sun, Element.Moon };

        /// <summary>월 속성이 복제할 수 있는 6속성 (06.2 [확정]).</summary>
        public static readonly Element[] MoonCopyPool =
            { Element.Fire, Element.Water, Element.Wood, Element.Metal, Element.Earth, Element.Sun };

        public static string Korean(Element e)
        {
            switch (e)
            {
                case Element.Fire: return "화";
                case Element.Water: return "수";
                case Element.Wood: return "목";
                case Element.Metal: return "금";
                case Element.Earth: return "토";
                case Element.Sun: return "일";
                case Element.Moon: return "월";
            }
            return "?";
        }

        public static string Hanja(Element e)
        {
            switch (e)
            {
                case Element.Fire: return "火";
                case Element.Water: return "水";
                case Element.Wood: return "木";
                case Element.Metal: return "金";
                case Element.Earth: return "土";
                case Element.Sun: return "日";
                case Element.Moon: return "月";
            }
            return "?";
        }

        public static string EffectName(Element e)
        {
            switch (e)
            {
                case Element.Fire: return "화상";
                case Element.Water: return "일시 속도 증가";
                case Element.Wood: return "생명 흡수";
                case Element.Metal: return "방어력 감소";
                case Element.Earth: return "쉴드";
                case Element.Sun: return "족보 추가 피해";
                case Element.Moon: return "특수 효과 복제";
            }
            return "";
        }
    }

    public static class HandInfo
    {
        public static string Name(HandType h)
        {
            switch (h)
            {
                case HandType.Pair: return "PAIR";
                case HandType.TwoPair: return "TWO PAIR";
                case HandType.Triple: return "TRIPLE";
                case HandType.Straight: return "STRAIGHT";
                case HandType.FullHouse: return "FULL HOUSE";
                case HandType.Flush: return "FLUSH";
                case HandType.FourOfAKind: return "FOUR OF A KIND";
                case HandType.StraightFlush: return "STRAIGHT FLUSH";
                case HandType.RoyalFlush: return "ROYAL FLUSH";
                case HandType.Pentagram: return "PENTAGRAM";
            }
            return "족보 없음";
        }

        public static bool IsTrueDamage(HandType h)
        {
            return h == HandType.RoyalFlush || h == HandType.Pentagram;
        }
    }

    /// <summary>랭크 값: 2~10, J=11, Q=12, K=13, A=14 (07.4 [확정] Σr 정의).</summary>
    public static class RankInfo
    {
        public const int Min = 2;
        public const int Max = 14;

        public static string Label(int rank)
        {
            switch (rank)
            {
                case 10: return "T";
                case 11: return "J";
                case 12: return "Q";
                case 13: return "K";
                case 14: return "A";
            }
            return rank.ToString();
        }
    }

    /// <summary>카드 개체. Id는 개체 ID(같은 정의 카드가 여러 장이어도 구분), 속성·랭크는 정의 값.</summary>
    public sealed class Card
    {
        public readonly int Id;
        public readonly Element Element;
        public readonly int Rank;

        public Card(int id, Element element, int rank)
        {
            if (rank < RankInfo.Min || rank > RankInfo.Max) throw new ArgumentOutOfRangeException(nameof(rank));
            Id = id;
            Element = element;
            Rank = rank;
        }

        public override string ToString()
        {
            return ElementInfo.Korean(Element) + RankInfo.Label(Rank);
        }
    }

    /// <summary>시드 고정 난수. 같은 시드면 같은 전투가 재현됩니다 (23.2 재현 가능성 대비).</summary>
    public sealed class GameRng
    {
        private readonly Random _random;
        public readonly int Seed;

        public GameRng(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        /// <summary>[min, maxExclusive)</summary>
        public int Range(int min, int maxExclusive) { return _random.Next(min, maxExclusive); }

        public double Value01() { return _random.NextDouble(); }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
