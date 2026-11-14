using System;
using System.Collections.Generic;

namespace PokerTower.Core
{
    /// <summary>화상 1회분.</summary>
    public struct BurnTick
    {
        public int Amount;
        public BurnTick(int amount) { Amount = amount; }
    }

    /// <summary>
    /// 화상 상태 (06.2 [확정] 갱신 규칙).
    /// 같은 대상에 중첩 객체를 쌓지 않고, 남은 '회차 목록'으로 관리합니다.
    ///  - 새 화상이 현재 맨 앞 회차보다 같거나 강하면 → 새 화상 2회로 교체
    ///  - 약한 화상: 기존이 2회 남았으면 무시, 1회 남았으면 [기존 1회, 약 1회]
    /// [가정] '강함'은 1회당 피해량으로 비교. 같은 세기는 갱신(2회로 교체)으로 처리.
    /// </summary>
    public sealed class BurnState
    {
        private readonly List<BurnTick> _ticks = new List<BurnTick>();
        public IReadOnlyList<BurnTick> Ticks { get { return _ticks; } }
        public bool Active { get { return _ticks.Count > 0; } }

        public void Apply(int perTick)
        {
            if (perTick <= 0) return;
            if (_ticks.Count == 0 || perTick >= _ticks[0].Amount)
            {
                _ticks.Clear();
                for (int i = 0; i < GameConfig.FireBurnTicks; i++) _ticks.Add(new BurnTick(perTick));
                return;
            }
            // 더 약한 화상
            if (_ticks.Count >= 2) return;
            _ticks.Add(new BurnTick(perTick));
        }

        /// <summary>적 행동 종료 직전 1회 소모하여 피해량 반환.</summary>
        public int Consume()
        {
            if (_ticks.Count == 0) return 0;
            int amount = _ticks[0].Amount;
            _ticks.RemoveAt(0);
            return amount;
        }

        public void Clear() { _ticks.Clear(); }
    }

    /// <summary>
    /// 금 방깎 상태 (11.2 [최우선 최신 규칙]).
    /// 원본 DEF는 Combatant.BaseDef에 그대로 두고, 감소 배율(Multiplier)만 보관합니다.
    /// 유효 DEF = BaseDef × Multiplier → 해제 시 배율만 1로 돌리면 영구 손실이 없습니다.
    /// </summary>
    public sealed class MetalDebuff
    {
        public bool Active;
        /// <summary>현재 적용 중인 마지막 금 공격의 감소율 (강도 비교용).</summary>
        public double Rate;
        /// <summary>누적 DEF 배율 (예: 0.8 × 0.74).</summary>
        public double Multiplier = 1.0;
        /// <summary>디버프가 걸린 뒤 에너미가 한 번이라도 행동했는지.</summary>
        public bool EnemyActedSinceApplied;

        public void Clear()
        {
            Active = false;
            Rate = 0;
            Multiplier = 1.0;
            EnemyActedSinceApplied = false;
        }
    }

    public sealed class Shield
    {
        public int Amount;
        /// <summary>남은 '플레이어 턴 종료' 횟수.</summary>
        public int TurnsLeft;
        public bool Active { get { return Amount > 0 && TurnsLeft > 0; } }
        public void Clear() { Amount = 0; TurnsLeft = 0; }
    }

    public abstract class Combatant
    {
        public string Name = "";
        public int MaxHp;
        public int Hp;
        public double BaseDef;
        public double BaseSpeed;
        /// <summary>CTB 남은 행동 대기량. 0에 먼저 도달한 유닛이 행동합니다.</summary>
        public double ActionValue;

        public bool IsDead { get { return Hp <= 0; } }

        public virtual double EffectiveDef { get { return BaseDef; } }

        /// <summary>HP를 깎고 실제 감소량을 반환.</summary>
        public int LoseHp(int amount)
        {
            if (amount <= 0) return 0;
            int actual = Math.Min(amount, Hp);
            Hp -= actual;
            return actual;
        }

        public int Heal(int amount)
        {
            if (amount <= 0) return 0;
            int actual = Math.Min(amount, MaxHp - Hp);
            Hp += actual;
            return actual;
        }
    }

    public sealed class PlayerUnit : Combatant
    {
        public readonly Shield Shield = new Shield();
        /// <summary>수 속성 일시 SPD 증가율 (다음 행동 대기 계산에 1회 적용 후 소모).</summary>
        public double PendingSpeedBuff;
        /// <summary>스탯 '데미지 증가' 배율 (1 + 2% × 포인트).</summary>
        public double DamageMultiplier = 1.0;
        /// <summary>스탯 '카드 추가 드로우' 확률 (보충 드로우 시 1장 더).</summary>
        public double ExtraDrawChance;
        /// <summary>[v1.3] 스탯 '치명타율' (공격 1회당 판정, 상한 100%).</summary>
        public double CritChance;
        /// <summary>속성 강화 중첩 수 (Element 순서). 대표 카드 속성의 피해 +5% × 중첩.</summary>
        public int[] ElementBoosts = new int[ElementInfo.All.Length];
    }

    public sealed class EnemyUnit : Combatant
    {
        public int Atk;
        /// <summary>전투 시작 시 ATK (버프 표시용).</summary>
        public int BaseAtk;
        public EnemyTier Tier;
        public bool IsBoss { get { return Tier == EnemyTier.Boss; } }
        /// <summary>치명타율 (엘리트·보스 버프로만 증가).</summary>
        public double CritChance;
        public readonly BurnState Burn = new BurnState();
        public readonly MetalDebuff Metal = new MetalDebuff();

        public override double EffectiveDef
        {
            get { return Math.Max(0.0, BaseDef * (Metal.Active ? Metal.Multiplier : 1.0)); }
        }

        public bool HasDebuff { get { return Burn.Active || Metal.Active; } }
    }
}
