using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PokerTower.Core
{
    /// <summary>스탯 6종 (v1.2 [확정] 5종 + v1.3 [확정] 치명타율). 순서는 저장 문자열 순서와 같으니 바꾸지 마세요.</summary>
    public enum StatKind { MaxHp = 0, Speed = 1, ExtraDraw = 2, Def = 3, Damage = 4, Crit = 5 }

    /// <summary>스탯별 투자 레벨과 그에 따른 최종 능력치. 수치는 GameConfig에서 옵니다.</summary>
    public sealed class PlayerStats
    {
        public const int Count = 6;
        public static readonly StatKind[] All = { StatKind.MaxHp, StatKind.Speed, StatKind.ExtraDraw, StatKind.Def, StatKind.Damage, StatKind.Crit };

        /// <summary>스탯별 투자 레벨 (0부터).</summary>
        public readonly int[] Levels = new int[Count];
        /// <summary>속성 강화 중첩 수 (Element 순서, 보스 클리어 보상).</summary>
        public readonly int[] ElementBoosts = new int[ElementInfo.All.Length];

        public int this[StatKind k] { get { return Levels[(int)k]; } }

        public int MaxHp { get { return GameConfig.PlayerMaxHp + GameConfig.StatHpPerPoint * Levels[(int)StatKind.MaxHp]; } }
        public double Speed { get { return GameConfig.PlayerBaseSpeed + GameConfig.StatSpeedPerPoint * Levels[(int)StatKind.Speed]; } }
        public double ExtraDrawChance { get { return GameConfig.StatExtraDrawPerPoint * Levels[(int)StatKind.ExtraDraw]; } }
        public double Def { get { return GameConfig.PlayerDef + GameConfig.StatDefPerPoint * Levels[(int)StatKind.Def]; } }
        public double DamageMultiplier { get { return 1.0 + GameConfig.StatDamagePerPoint * Levels[(int)StatKind.Damage]; } }
        public double CritChance { get { return Math.Min(GameConfig.PlayerCritMax, GameConfig.StatCritPerPoint * Levels[(int)StatKind.Crit]); } }

        /// <summary>[확정 v1.3] 레벨 level로 올리는 비용 = level점 (1레벨 1점, 2레벨 2점…).</summary>
        public static int LevelCost(int level) { return level; }

        /// <summary>레벨 from → to 로 올리는 데 드는 총 포인트 (to &lt; from이면 음수 = 환불).</summary>
        public static int CostBetween(int from, int to)
        {
            // 1 + 2 + … + n = n(n+1)/2
            return to * (to + 1) / 2 - from * (from + 1) / 2;
        }

        /// <summary>레벨을 add만큼 더했다고 가정한 사본. 스탯 화면 미리보기용.</summary>
        public PlayerStats With(int[] add)
        {
            var s = new PlayerStats();
            for (int i = 0; i < Count; i++) s.Levels[i] = Levels[i] + (add != null ? add[i] : 0);
            Array.Copy(ElementBoosts, s.ElementBoosts, ElementBoosts.Length);
            return s;
        }

        public static string Name(StatKind k)
        {
            switch (k)
            {
                case StatKind.MaxHp: return "최대 체력";
                case StatKind.Speed: return "속도";
                case StatKind.ExtraDraw: return "카드 추가 드로우";
                case StatKind.Def: return "방어력";
                case StatKind.Damage: return "데미지 증가";
                case StatKind.Crit: return "치명타율";
            }
            return "?";
        }

        public static string PerPoint(StatKind k)
        {
            switch (k)
            {
                case StatKind.MaxHp: return "+" + GameConfig.StatHpPerPoint;
                case StatKind.Speed: return "+" + GameConfig.StatSpeedPerPoint;
                case StatKind.ExtraDraw: return "+" + Percent(GameConfig.StatExtraDrawPerPoint) + "%p";
                case StatKind.Def: return "+" + GameConfig.StatDefPerPoint;
                case StatKind.Damage: return "+" + Percent(GameConfig.StatDamagePerPoint) + "%";
                case StatKind.Crit: return "+" + Percent(GameConfig.StatCritPerPoint) + "%p";
            }
            return "";
        }

        public string Value(StatKind k)
        {
            switch (k)
            {
                case StatKind.MaxHp: return MaxHp.ToString();
                case StatKind.Speed: return Speed.ToString("0.#");
                case StatKind.ExtraDraw: return Percent(ExtraDrawChance) + "%";
                case StatKind.Def: return Def.ToString("0.#");
                case StatKind.Damage: return "+" + Percent(DamageMultiplier - 1.0) + "%";
                case StatKind.Crit: return Percent(CritChance) + "%";
            }
            return "";
        }

        public static string Percent(double v) { return Math.Round(v * 100, 1).ToString("0.#", CultureInfo.InvariantCulture); }
    }

    /// <summary>
    /// 런 진행(스탯 포인트) 상태. 저장·불러오기는 문자열로 주고받고, 실제 저장소(PlayerPrefs)는 Game 쪽이 맡습니다.
    /// [확정 v1.3] 클리어 포인트는 층 등급별(일반 1·엘리트 2·보스 3), 같은 층은 처음 클리어할 때 한 번만.
    /// [확정 v1.3] 투자 비용은 레벨마다 1점씩 증가 (N레벨에 N점). [가정] 스탯마다 따로 셈.
    /// [가정] 미사용 포인트 보유 가능, '확정'하면 되돌릴 수 없음, 패배 후 재도전해도 유지, 새로하기 시 초기화.
    /// </summary>
    public sealed class PlayerProgress
    {
        public readonly PlayerStats Stats = new PlayerStats();
        public int UnspentPoints;
        /// <summary>포인트를 받은 가장 높은 층 (같은 층 재클리어 시 중복 지급 방지).</summary>
        public int HighestClearedFloor;
        /// <summary>아직 고르지 않은 속성 강화 후보 (보스 첫 처치 시 3개). 비어 있으면 고를 것 없음. 저장됨.</summary>
        public readonly List<Element> PendingElementChoices = new List<Element>();

        /// <summary>
        /// 층 클리어 보상. 처음 클리어한 층이면 등급별 포인트를 주고 그 값을, 아니면 0을 돌려줌.
        /// 보스 층이면 속성 강화 후보 3개도 정함 (seed로 무작위).
        /// </summary>
        public int AwardClear(int floor, int seed = 0)
        {
            if (floor <= HighestClearedFloor) return 0;
            HighestClearedFloor = floor;
            int pts = GameConfig.StatPointsFor(floor);
            UnspentPoints += pts;
            if (GameConfig.TierOf(floor) == EnemyTier.Boss)
            {
                var all = ElementInfo.All.ToList();
                new GameRng(seed ^ 0x2F6B1A3D).Shuffle(all);
                PendingElementChoices.Clear();
                PendingElementChoices.AddRange(all.Take(GameConfig.ElementBoostChoices).OrderBy(e => (int)e));
            }
            return pts;
        }

        /// <summary>후보 중 하나를 골라 속성 강화 +1 중첩. 후보가 아니면 예외.</summary>
        public void ChooseElementBoost(Element e)
        {
            if (!PendingElementChoices.Contains(e)) throw new InvalidOperationException("고를 수 있는 속성이 아닙니다.");
            Stats.ElementBoosts[(int)e]++;
            PendingElementChoices.Clear();
        }

        /// <summary>레벨을 add[i]만큼 올리는 데 드는 총 포인트 (레벨별 비용 합).</summary>
        public int CostOf(int[] add)
        {
            int cost = 0;
            for (int i = 0; i < PlayerStats.Count; i++)
                cost += PlayerStats.CostBetween(Stats.Levels[i], Stats.Levels[i] + add[i]);
            return cost;
        }

        /// <summary>스탯 k를 미리보기 레벨(현재 + pending)에서 한 레벨 더 올리는 비용.</summary>
        public int NextLevelCost(StatKind k, int pending)
        {
            return PlayerStats.LevelCost(Stats[k] + pending + 1);
        }

        /// <summary>배분 확정 가능 여부. add[i] ≥ 0, 레벨별 비용 합계 ≤ 남은 포인트.</summary>
        public bool CanCommit(int[] add, out string reason)
        {
            reason = "";
            if (add == null || add.Length != PlayerStats.Count) { reason = "배분 정보가 올바르지 않습니다."; return false; }
            foreach (int a in add)
                if (a < 0) { reason = "레벨을 낮출 수는 없습니다."; return false; }
            int cost = CostOf(add);
            if (cost > UnspentPoints) { reason = "포인트가 부족합니다 (필요 " + cost + "점, 보유 " + UnspentPoints + "점)."; return false; }
            return true;
        }

        public void Commit(int[] add)
        {
            string reason;
            if (!CanCommit(add, out reason)) throw new InvalidOperationException(reason);
            UnspentPoints -= CostOf(add);
            for (int i = 0; i < PlayerStats.Count; i++) Stats.Levels[i] += add[i];
        }

        public void Reset()
        {
            for (int i = 0; i < PlayerStats.Count; i++) Stats.Levels[i] = 0;
            UnspentPoints = 0;
            HighestClearedFloor = 0;
            Array.Clear(Stats.ElementBoosts, 0, Stats.ElementBoosts.Length);
            PendingElementChoices.Clear();
        }

        // 저장 형식: "v3;hp,spd,draw,def,dmg,crit;남은포인트;최고클리어층;속성강화7개;고를후보(속성번호,…)"
        // v2(속성 강화 없음)·v1(스탯 5개, 치명타 0)도 읽음
        public string Serialize()
        {
            return "v3;" + string.Join(",", Stats.Levels) + ";" + UnspentPoints + ";" + HighestClearedFloor
                 + ";" + string.Join(",", Stats.ElementBoosts) + ";" + string.Join(",", PendingElementChoices.Select(e => (int)e));
        }

        /// <summary>읽을 수 없는 문자열이면 초기 상태를 돌려줍니다.</summary>
        public static PlayerProgress Parse(string text)
        {
            var p = new PlayerProgress();
            if (string.IsNullOrEmpty(text)) return p;
            var parts = text.Split(';');
            if (parts.Length != (parts[0] == "v3" ? 6 : 4)) return p;
            int expected = parts[0] == "v3" || parts[0] == "v2" ? PlayerStats.Count : parts[0] == "v1" ? 5 : -1;
            if (expected < 0) return p;
            var lv = parts[1].Split(',');
            if (lv.Length != expected) return p;
            var tmp = new int[PlayerStats.Count];
            int unspent, highest;
            for (int i = 0; i < expected; i++)
                if (!int.TryParse(lv[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out tmp[i]) || tmp[i] < 0) return p;
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out unspent) || unspent < 0) return p;
            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out highest) || highest < 0) return p;
            if (parts[0] == "v3")
            {
                var bs = parts[4].Split(',');
                if (bs.Length != p.Stats.ElementBoosts.Length) return new PlayerProgress();
                for (int i = 0; i < bs.Length; i++)
                    if (!int.TryParse(bs[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out p.Stats.ElementBoosts[i]) || p.Stats.ElementBoosts[i] < 0)
                        return new PlayerProgress();
                if (parts[5].Length > 0)
                    foreach (var s in parts[5].Split(','))
                    {
                        int e;
                        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out e) || e < 0 || e >= ElementInfo.All.Length)
                            return new PlayerProgress();
                        p.PendingElementChoices.Add((Element)e);
                    }
            }
            Array.Copy(tmp, p.Stats.Levels, PlayerStats.Count);
            p.UnspentPoints = unspent;
            p.HighestClearedFloor = highest;
            return p;
        }
    }
}
