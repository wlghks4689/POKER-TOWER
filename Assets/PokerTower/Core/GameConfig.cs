// 모든 밸런스 수치와 규칙 상수를 한곳에서 관리합니다 (기획 원칙: 수치를 코드 곳곳에 흩뜨리지 않음).
//
// 표기 규칙
//   [확정]  기획서에 확정된 값. 바꾸려면 기획서 변경 이력부터 수정하세요.
//   [가정]  기획서에 "미정"인 항목을 프로토타입을 돌리기 위해 임시로 정한 값.
//           기획서에서 결정되면 이 값을 고치면 됩니다. 확정 규칙이 아닙니다.
namespace PokerTower.Core
{
    public static class GameConfig
    {
        // ───────────── 카드와 덱 ─────────────
        /// [확정] 매 스테이지 5속성 덱.
        public const int StageElementCount = 5;
        /// [가정] 같은 속성·랭크 카드 매수 1장 → 5속성 × 13랭크 = 65장 (기획서 21장의 "65장 덱" 언급과 일치).
        public const int CopiesPerCard = 1;
        /// [확정] 첫 손패 7장. 손패 상한은 MaxHandSize (v1.3).
        public const int HandSize = 7;
        /// [확정 v1.2] 턴 종료 버리기 최대 장수 (0장 가능).
        public const int MaxDiscard = 2;
        /// [확정 v1.3] 손패 최대 10장. 턴 종료 보충·추가 드로우 후 넘으면 '강제 버리기'(넘는 장수만큼, 보충 없음, 묘지로).
        public const int MaxHandSize = 10;
        /// [확정] 제출 2~5장 (대표 1 + 서브 1~4).
        public const int MinSubmit = 2;
        public const int MaxSubmit = 5;
        /// [가정] 덱이 비면 묘지를 섞어 덱으로 되돌림 (09.3 재섞기 미정).
        public const bool ReshuffleGraveyardWhenDeckEmpty = true;
        /// 족보가 없는 조합은 '공격'으로 낼 수 없음. [사용자 결정 2026-11-14 A1] '교체'(피해 0, 효과 없음)는 족보 유무와 관계없이 언제나 가능 → Battle.Submit(exchange: true)
        public const bool AllowNoHandSubmit = false;
        /// 손패 어디에도 족보가 없을 때는 교체 표시 없이 제출해도 교체로 처리 (자동 플레이·시뮬레이션용).
        public const bool AllowNoHandSubmitWhenStuck = true;

        // ───────────── 피해 ─────────────
        /// [확정] 메인 속성 피해 +20% (속성 추가 피해와 합연산).
        public const double MainElementBonus = 0.20;
        /// [확정] DEF 경감: 피해 × 100 / (100 + DEF_effective).
        public const double DefenseConstant = 100.0;
        /// [확정] ROYAL FLUSH · PENTAGRAM 기본 True Damage.
        public const int TrueDamageBase = 350;
        /// [확정] 메인 속성으로 MADE한 ROYAL FLUSH 추가 True Damage (350 × 0.2).
        public const int RoyalMainBonus = 70;

        // ───────────── 속성 효과 기준율 (× 족보 계수 C) ─────────────
        public const double FireBurnRate = 0.30;   // [확정] 최종 피해 × 30% × C, 2회 분할
        public const int FireBurnTicks = 2;        // [확정]
        public const double WaterSpeedRate = 0.10; // [확정] SPD +10% × C
        public const double WoodLifestealRate = 0.10; // [확정] 실제 HP 피해 × 10% × C
        public const double MetalDefDownRate = 0.20;  // [확정] DEF 20% × C 감소
        public const double EarthShieldRate = 0.05;   // [확정] 최대 HP × 5% × C
        public const int EarthShieldTurns = 2;        // [확정] 다음 플레이어 턴 종료 2회까지
        public const double SunExtraRate = 0.30;      // [확정] 족보 피해 × 30% × C

        // ───────────── 속도 (CTB) ─────────────
        /// 행동 대기량 = ActionGauge / SPD. 행동 비용은 카드 수·족보와 무관하게 동일 ([확정]).
        public const double ActionGauge = 10000.0;
        /// [가정] 플레이어 기본 SPD.
        public const double PlayerBaseSpeed = 100.0;
        /// [가정] 일반 에너미 SPD, 보스 SPD.
        public const double EnemyBaseSpeed = 90.0;
        public const double BossSpeed = 105.0;
        /// [가정] 동시 도착 시 플레이어 우선.
        public const bool PlayerWinsTies = true;
        /// 속도 바에 보여줄 이후 턴 수.
        public const int SpeedBarPreviewCount = 8;

        // ───────────── 플레이어 ─────────────
        /// [확정 v1.2] 플레이어 초기 최대 HP·DEF. (SPD는 위 PlayerBaseSpeed [가정] 유지)
        public const int PlayerMaxHp = 200;
        public const double PlayerDef = 10;

        // ───────────── 스탯 포인트 (v1.2 · v1.3) ─────────────
        /// [확정 v1.3] 클리어 포인트: 일반 층 1점, 엘리트 처치 2점, 보스 처치 3점. 같은 층은 처음 클리어할 때 한 번만.
        public const int StatPointsNormal = 1;
        public const int StatPointsElite = 2;
        public const int StatPointsBoss = 3;
        /// [확정 v1.3] 같은 스탯을 N레벨로 올리는 데 N점 (1레벨 1점, 2레벨 2점…). [가정] 비용은 스탯마다 따로 셈.
        /// [확정] 1레벨당 효과. [가정] 치명타율 외 스탯 상한 없음.
        public const int StatHpPerPoint = 10;            // 최대 체력 +10
        public const double StatSpeedPerPoint = 5;       // 속도 +5
        public const double StatExtraDrawPerPoint = 0.04; // 카드 추가 드로우 확률 +4%p (보충 드로우 시 1장 더) [사용자 결정 2026-11-14: 5%p → 4%p]
        public const double StatDefPerPoint = 3;         // 방어력 +3
        public const double StatDamagePerPoint = 0.02;   // 데미지 증가 +2%
        public const double StatCritPerPoint = 0.05;     // [확정 v1.3] 치명타율 +5%p
        /// [가정 v1.3] 플레이어 치명타율 상한 100%. 치명타 판정은 공격 1회당 1번(교체 턴 제외),
        ///             발동 시 그 공격의 최종 피해 전체(일반 + True)에 × CritMultiplier, 데미지 증가 배율과 곱함 (DEF 경감 전).
        ///             화상량은 치명타 반영 후 최종 피해 기준. 난수는 전투 시드 RNG.
        public const double PlayerCritMax = 1.0;
        // [가정] 데미지 증가 적용 방식: 최종 피해 × (1 + 2% × 포인트). 메인 +20%와 합연산하지 않는 별도 배율,
        //        DEF 경감 전에 곱함. True Damage에도 적용. 화상량은 이 배율이 적용된 최종 피해 기준.

        // ───────────── 속성 강화 (보스 클리어 보상) ─────────────
        /// [사용자 결정 2026-11-14, 수치 미확정] 보스를 처음 처치하면 속성 3개 중 1개를 골라 강화. 같은 속성 중첩 가능.
        /// 강화한 속성 카드가 대표 카드(1번)인 공격의 피해 +5% × 중첩 수.
        /// [가정] 후보 3개는 7속성 중 무작위(서로 다름). 데미지 증가 스탯처럼 별도 배율로 곱함(메인 +20%와 합연산 아님),
        ///        DEF 경감 전, True 포함. 월 대표 카드는 월 강화를 따름. 새로하기 시 초기화.
        public const double ElementBoostPerStack = 0.05;
        public const int ElementBoostChoices = 3;

        // ───────────── 에너미 ─────────────
        /// [확정] 1라운드 일반 몬스터 HP 300 / ATK 10 / DEF 0.
        public const int EnemyBaseHp = 300;
        public const int EnemyBaseAtk = 10;
        public const double EnemyBaseDef = 0;
        /// [가정] 층당 성장률 (12.1: 증가는 확정, 수치는 미정).
        public const double EnemyHpGrowthPerFloor = 0.10;
        public const double EnemyAtkGrowthPerFloor = 0.08;
        public const double EnemyDefGrowthPerFloor = 2.0;   // 층당 +2 (덧셈)
        /// [확정] 10의 배수 층은 보스. [가정] 보스 배율.
        public const int BossFloorInterval = 10;
        public const double BossHpMultiplier = 2.5;
        public const double BossAtkMultiplier = 1.3;
        /// [확정 v1.2] 5의 배수 층 중 보스층을 뺀 층은 엘리트. [가정] 엘리트 배율. 엘리트 SPD는 일반(EnemyBaseSpeed)과 같게 둠 [가정].
        public const int EliteFloorInterval = 5;
        public const double EliteHpMultiplier = 1.6;
        public const double EliteAtkMultiplier = 1.15;

        /// [가정] 일반 에너미 행동 가중치. 12.2의 기존 확률안(일반 60/스킬 40)은 비공격 행동 추가 전 잠정안이라 재배분함.
        /// [가정 v1.2] 일반 에너미 비공격 후보: 회복(HP 70% 미만일 때), DEF 증가만 ([확정] 디버프 제거·자기 버프 불가).
        public const int EnemyWeightNormalAttack = 55;
        public const int EnemyWeightSkillAttack = 30;
        public const int EnemyWeightNonAttack = 15;
        /// [가정 v1.2] 엘리트·보스 가중치. 비공격 후보: 디버프 제거(디버프가 있을 때만), 회복(HP 70% 미만일 때), DEF 증가, ATK 증가, 치명타율 증가.
        public const int SpecialWeightNormalAttack = 45;
        public const int SpecialWeightSkillAttack = 30;
        public const int SpecialWeightNonAttack = 25;
        /// [확정 v1.2] 치명타가 발동한 공격은 150% 피해 (v1.3부터 플레이어도 적용).
        /// [가정] 에너미 치명타: 원 피해 × 1.5 → 플레이어 DEF 경감 → 쉴드 흡수.
        public const double CritMultiplier = 1.5;
        /// [가정 v1.2] 치명타율: 기본 0%, 치명타율 증가 1회당 +15%p, 최대 60%. 전투 끝까지 유지·누적.
        ///             최대치에 도달하면 치명타율 증가는 후보에서 뺌 [가정].
        public const double EnemyCritUpRate = 0.15;
        public const double EnemyCritMax = 0.60;
        /// [가정] 스킬 공격 배율.
        public const double EnemySkillMultiplier = 1.5;
        /// [확정] 하층 일반 몬스터 회복: 잃은 HP의 5~10%.
        public const double EnemyHealMinRate = 0.05;
        public const double EnemyHealMaxRate = 0.10;
        /// [확정] DEF 증가 행동 1~10 (스테이지 레벨 비례). [가정] 층 1 → 1, 층 10 이상 → 10.
        public const int EnemyDefUpMin = 1;
        public const int EnemyDefUpMax = 10;
        /// [가정] ATK 증가 행동량: 1회당 현재 ATK의 +10%. 전투 끝까지 유지·누적.
        public const double EnemyAtkUpRate = 0.10;

        /// <summary>족보 특성 계수 C ([확정] 2026-11-12).</summary>
        public static double Coefficient(HandType hand)
        {
            switch (hand)
            {
                case HandType.Pair: return 1.00;
                case HandType.TwoPair: return 1.20;
                case HandType.Triple: return 1.30;
                case HandType.Straight: return 1.50;
                case HandType.FullHouse: return 1.75;
                case HandType.Flush: return 2.00;
                case HandType.FourOfAKind: return 2.50;
                case HandType.StraightFlush: return 3.00;
                case HandType.RoyalFlush: return 4.00;
                case HandType.Pentagram: return 4.00;
            }
            return 0.0;
        }

        public static bool IsBossFloor(int floor) { return floor > 0 && floor % BossFloorInterval == 0; }

        /// <summary>[확정 v1.3] 층을 처음 클리어했을 때 받는 스탯 포인트.</summary>
        public static int StatPointsFor(int floor)
        {
            switch (TierOf(floor))
            {
                case EnemyTier.Boss: return StatPointsBoss;
                case EnemyTier.Elite: return StatPointsElite;
            }
            return StatPointsNormal;
        }

        /// <summary>[확정 v1.2] 층 → 에너미 등급.</summary>
        public static EnemyTier TierOf(int floor)
        {
            if (IsBossFloor(floor)) return EnemyTier.Boss;
            if (floor > 0 && floor % EliteFloorInterval == 0) return EnemyTier.Elite;
            return EnemyTier.Normal;
        }
    }
}
