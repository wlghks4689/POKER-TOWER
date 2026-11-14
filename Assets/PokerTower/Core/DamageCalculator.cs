using System;
using System.Collections.Generic;

namespace PokerTower.Core
{
    /// <summary>플레이어 공격 1회의 계산 결과. UI·로그·테스트가 모두 이 값을 봅니다.</summary>
    public sealed class AttackReport
    {
        public HandResult Hand;
        /// <summary>제출한 카드 장수.</summary>
        public int CardCount;
        /// <summary>'교체' 제출이었는지 (피해·효과 없음). 족보가 없거나, [사용자 결정 2026-11-14 A1] 족보가 있어도 교체를 고른 경우.</summary>
        public bool IsExchange;
        /// <summary>대표 카드(사용 카드 존 1번)의 속성 = 공격 속성.</summary>
        public Element AttackElement;
        /// <summary>실제로 발동한 특수 효과 속성 (월이면 복제된 속성).</summary>
        public Element EffectElement;
        public bool IsMainElementAttack;
        public double Coefficient;

        public double DefBefore;
        public double DefUsed;          // 이번 피해 계산에 사용한 유효 DEF
        public bool MetalCompounded;    // 연속 강한 금 공격으로 기존 유효 DEF에 재적용했는지
        public bool MetalRemovedBefore; // 이번 공격 전에 기존 방깎을 해제했는지

        public int TrueDamage;          // DEF 무시 피해
        public int NormalDamage;        // DEF 경감 적용 피해
        public int TotalDamage { get { return TrueDamage + NormalDamage; } }
        public int ActualHpLoss;        // 적 HP에 실제로 들어간 양

        /// <summary>[v1.3] 플레이어 치명타 발동 (최종 피해 전체 × 1.5).</summary>
        public bool Critical;
        public int BurnPerTick;
        public int Healed;
        public int ShieldGranted;
        public double SpeedBuff;
    }

    /// <summary>
    /// 피해와 속성 효과 계산 (기획서 08.2 [최종 피해 공식 / 확정 2026-11-12]).
    ///
    /// 일반 족보:  floor( (B + 일 추가 B×0.3C + 메인 B×0.2) × 100 / (100 + DEF_effective) )
    /// True 족보:  350 (+70 메인 ROYAL) 는 DEF 무시. 일 추가 피해는 별도 일반 피해로 산정.
    /// 금:         방깎을 먼저 적용한 뒤 계산.
    /// [가정 v1.2] 스탯 데미지 증가 배율 M을 DEF 경감 전에 곱함 (메인 +20%와 별도 배율). v1.3: 치명타면 M × 1.5:
    ///   일반 floor( (B + 일 + 메인) × M × 100 / (100 + DEF) ),  True floor( (350 | 420) × M )
    /// </summary>
    public static class DamageCalculator
    {
        public static int NormalDamage(double raw, double def)
        {
            if (raw <= 0) return 0;
            double d = Math.Max(0.0, def);
            return (int)Math.Floor(raw * GameConfig.DefenseConstant / (GameConfig.DefenseConstant + d) + 1e-9);
        }

        /// <param name="stageMain">스테이지 메인 속성</param>
        /// <param name="moonPick">월 속성이 복제할 속성. null이면 rng로 추첨.</param>
        public static AttackReport ResolvePlayerAttack(
            IList<Card> orderedCards, Element stageMain, PlayerUnit player, EnemyUnit enemy,
            GameRng rng, Element? moonPick = null, bool exchange = false)
        {
            var hand = HandEvaluator.Evaluate(orderedCards);

            var r = new AttackReport();
            r.Hand = hand;
            r.CardCount = orderedCards.Count;
            r.AttackElement = orderedCards[0].Element;
            r.Coefficient = hand.Coefficient;

            if (exchange || !hand.IsValid)
            {
                // 교체: 피해·효과 없음, 상태 변화 없음
                r.IsExchange = true;
                r.EffectElement = r.AttackElement;
                r.DefBefore = r.DefUsed = enemy.EffectiveDef;
                return r;
            }
            // [가정] 메인 +20% 대상: 대표 카드 속성 == 스테이지 메인 속성 (06.3 판단 기준 미정)
            r.IsMainElementAttack = r.AttackElement == stageMain;

            if (r.AttackElement == Element.Moon)
                r.EffectElement = moonPick ?? ElementInfo.MoonCopyPool[rng.Range(0, ElementInfo.MoonCopyPool.Length)];
            else
                r.EffectElement = r.AttackElement;

            double c = r.Coefficient;
            r.DefBefore = enemy.EffectiveDef;

            // ── 1) 금 방깎 처리 (피해 계산 전) ──
            if (r.EffectElement == Element.Metal)
            {
                double rate = GameConfig.MetalDefDownRate * c;
                var m = enemy.Metal;
                if (m.Active && !m.EnemyActedSinceApplied && rate > m.Rate)
                {
                    // [확정] 적 턴 없이 더 강한 금 연속 공격 → 이미 감소된 DEF에 새 비율 재적용
                    m.Multiplier *= (1.0 - rate);
                    m.Rate = rate;
                    r.MetalCompounded = true;
                }
                else
                {
                    // [가정] 같거나 약한 금, 또는 적 턴이 지난 뒤의 금 → 기존 방깎 해제 후 원본 DEF에 새로 적용
                    if (m.Active) r.MetalRemovedBefore = true;
                    m.Clear();
                    m.Active = true;
                    m.Rate = rate;
                    m.Multiplier = 1.0 - rate;
                }
                m.EnemyActedSinceApplied = false;
            }
            else if (enemy.Metal.Active)
            {
                // [확정] 연속 플레이어 행동의 비금 공격 → 계산 전에 방깎 제거
                // [가정] 적 턴이 지난 뒤의 비금 공격도 동일하게 제거 (적 턴 후 자동 만료 여부 미정)
                enemy.Metal.Clear();
                r.MetalRemovedBefore = true;
            }

            r.DefUsed = enemy.EffectiveDef;

            // ── 2) 피해 ──
            double b = hand.BaseDamage;
            double mult = player.DamageMultiplier;
            // 속성 강화: 대표 카드 속성의 중첩 수만큼 별도 배율 (GameConfig.ElementBoostPerStack 참고)
            mult *= 1.0 + GameConfig.ElementBoostPerStack * player.ElementBoosts[(int)r.AttackElement];
            // [확정 v1.3] 플레이어 치명타 150%. [가정] 공격 1회당 1번 판정, 데미지 증가와 곱하고 DEF 경감 전에 적용,
            //            True 피해 포함. 치명타율 0이면 난수를 쓰지 않음 (기존 시드 재현 유지). 교체 턴은 위에서 이미 반환.
            if (player.CritChance > 0 && rng.Value01() < player.CritChance)
            {
                r.Critical = true;
                mult *= GameConfig.CritMultiplier;
            }
            double sunExtra = r.EffectElement == Element.Sun ? b * GameConfig.SunExtraRate * c : 0.0;

            if (hand.IsTrueDamage)
            {
                int trueBase = GameConfig.TrueDamageBase;
                if (hand.Type == HandType.RoyalFlush && hand.FlushElement.HasValue && hand.FlushElement.Value == stageMain)
                    trueBase += GameConfig.RoyalMainBonus;
                r.TrueDamage = (int)Math.Floor(trueBase * mult + 1e-9);
                // PENTAGRAM은 메인 보너스 항상 제외. 일 추가 피해는 일반 피해로 독립 산정.
                r.NormalDamage = NormalDamage(sunExtra * mult, r.DefUsed);
            }
            else
            {
                double mainBonus = r.IsMainElementAttack ? b * GameConfig.MainElementBonus : 0.0;
                r.NormalDamage = NormalDamage((b + sunExtra + mainBonus) * mult, r.DefUsed);
            }

            r.ActualHpLoss = enemy.LoseHp(r.TotalDamage);

            // ── 3) 피해 후 속성 효과 ──
            switch (r.EffectElement)
            {
                case Element.Fire:
                    // [확정] 총 화상 = 최종 피해 × 0.3C, 2회 분할. [가정] 1회분 = floor(총량 / 2)
                    double total = r.TotalDamage * GameConfig.FireBurnRate * c;
                    r.BurnPerTick = (int)Math.Floor(total / GameConfig.FireBurnTicks + 1e-9);
                    if (!enemy.IsDead) enemy.Burn.Apply(r.BurnPerTick);
                    break;
                case Element.Water:
                    r.SpeedBuff = GameConfig.WaterSpeedRate * c;
                    player.PendingSpeedBuff = r.SpeedBuff; // 비누적 갱신
                    break;
                case Element.Wood:
                    int heal = (int)Math.Floor(r.ActualHpLoss * GameConfig.WoodLifestealRate * c + 1e-9);
                    r.Healed = player.Heal(heal);
                    break;
                case Element.Earth:
                    r.ShieldGranted = (int)Math.Floor(player.MaxHp * GameConfig.EarthShieldRate * c + 1e-9);
                    // 실제 쉴드 부여는 플레이어 턴 종료 처리에서 (기존 쉴드 차감 후 교체)
                    break;
            }

            return r;
        }

        /// <summary>
        /// 에너미 → 플레이어 피해. 방어 경감 후 쉴드가 먼저 흡수. 반환: (쉴드 흡수, HP 손실).
        /// rawDamage는 치명타 배율(×1.5)까지 곱한 값을 소수 그대로 받습니다 → floor(원 피해 × 1.5 × 100 / (100 + DEF)).
        /// </summary>
        public static void ResolveEnemyHit(double rawDamage, PlayerUnit player, out int absorbed, out int hpLoss)
        {
            int dmg = NormalDamage(rawDamage, player.BaseDef);
            absorbed = 0;
            if (player.Shield.Active)
            {
                absorbed = Math.Min(player.Shield.Amount, dmg);
                player.Shield.Amount -= absorbed;
                dmg -= absorbed;
                if (player.Shield.Amount <= 0) player.Shield.Clear(); // 소진 시 즉시 소멸
            }
            hpLoss = player.LoseHp(dmg);
        }
    }
}
