using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerTower.Core
{
    /// <summary>
    /// 덱 · 손패 · 묘지 · 사용 중 영역. 카드는 항상 네 영역 중 정확히 한 곳에만 있습니다.
    /// 사용 중(InPlay) = 이번 플레이어 턴에 사용·버린 카드. 턴 종료 보충이 끝난 뒤 묘지로 갑니다.
    /// </summary>
    public sealed class CardPiles
    {
        public readonly List<Card> Deck = new List<Card>();
        public readonly List<Card> Hand = new List<Card>();
        public readonly List<Card> Graveyard = new List<Card>();
        public readonly List<Card> InPlay = new List<Card>();
        /// <summary>[사용자 결정 2026-11-14 A8] 손패 중 '카드 추가 드로우'로 받은 카드. 보충 장수에 세지 않고, 그 턴에 안 쓰면 턴 종료 때 묘지로.</summary>
        public readonly HashSet<Card> Extra = new HashSet<Card>();
        private readonly GameRng _rng;

        public int ReshuffleCount { get; private set; }

        public CardPiles(IEnumerable<Element> elements, GameRng rng)
        {
            _rng = rng;
            int id = 0;
            foreach (var e in elements)
                for (int rank = RankInfo.Min; rank <= RankInfo.Max; rank++)
                    for (int k = 0; k < GameConfig.CopiesPerCard; k++)
                        Deck.Add(new Card(id++, e, rank));
            _rng.Shuffle(Deck);
        }

        public int TotalCards { get { return Deck.Count + Hand.Count + Graveyard.Count + InPlay.Count; } }

        /// <summary>최대 count장 드로우. 실제 뽑은 카드 반환.</summary>
        public List<Card> Draw(int count)
        {
            var drawn = new List<Card>();
            for (int i = 0; i < count; i++)
            {
                if (Deck.Count == 0)
                {
                    if (!GameConfig.ReshuffleGraveyardWhenDeckEmpty || Graveyard.Count == 0) break;
                    Deck.AddRange(Graveyard);
                    Graveyard.Clear();
                    _rng.Shuffle(Deck);
                    ReshuffleCount++;
                }
                var c = Deck[Deck.Count - 1];
                Deck.RemoveAt(Deck.Count - 1);
                Hand.Add(c);
                drawn.Add(c);
            }
            return drawn;
        }

        /// <summary>제출 카드를 손패에서 묘지로 이동 ([확정] 대표·서브 모두 묘지).</summary>
        public void SendToGraveyard(IEnumerable<Card> cards)
        {
            foreach (var c in cards)
            {
                if (!Hand.Remove(c)) throw new InvalidOperationException("손패에 없는 카드입니다: " + c);
                Extra.Remove(c);
                Graveyard.Add(c);
            }
        }

        /// <summary>손패 → 사용 중 영역 (사용·버린 카드).</summary>
        public void MoveToInPlay(IEnumerable<Card> cards)
        {
            foreach (var c in cards)
            {
                if (!Hand.Remove(c)) throw new InvalidOperationException("손패에 없는 카드입니다: " + c);
                Extra.Remove(c);
                InPlay.Add(c);
            }
        }

        /// <summary>
        /// 턴 종료: drawCount장 보충 드로우 → 그 뒤에 사용 중 카드를 묘지로.
        /// 보충 중 재섞기가 일어나도 방금 쓰거나 버린 카드는 섞이지 않습니다 ([확정] 즉시 재사용 금지).
        /// </summary>
        public List<Card> FinishTurn(int drawCount)
        {
            var drawn = Draw(drawCount);
            Graveyard.AddRange(InPlay);
            InPlay.Clear();
            return drawn;
        }

        /// <summary>사용 → 같은 장수 보충 → 묘지를 한 번에 (버리기 없는 처리).</summary>
        public List<Card> CommitSubmission(IList<Card> used)
        {
            MoveToInPlay(used);
            return FinishTurn(used.Count);
        }

        public bool InHand(Card c) { return Hand.Contains(c); }
        public bool IsExtra(Card c) { return Extra.Contains(c); }
    }
}
