using System;
using System.Collections.Generic;

namespace PokerTower.Core
{
    public enum Actor { Player, Enemy }

    /// <summary>
    /// 속도 기반 행동 순서 (CTB, 04.1 [확정] 2026-11-12).
    ///   행동 대기량 AV = 10000 / SPD
    ///   남은 AV가 가장 작은 유닛이 행동 → 그 값만큼 모두의 AV에서 뺌 → 행동한 유닛은 AV를 다시 채움
    /// 행동 비용은 카드 수·족보와 무관하게 동일합니다. SPD가 2배면 같은 시간에 2배 행동합니다.
    /// </summary>
    public sealed class TurnScheduler
    {
        private readonly PlayerUnit _player;
        private readonly EnemyUnit _enemy;

        public TurnScheduler(PlayerUnit player, EnemyUnit enemy)
        {
            _player = player;
            _enemy = enemy;
            _player.ActionValue = FullGauge(_player.BaseSpeed);
            _enemy.ActionValue = FullGauge(_enemy.BaseSpeed);
        }

        public static double FullGauge(double speed)
        {
            if (speed <= 0) return double.MaxValue;
            return GameConfig.ActionGauge / speed;
        }

        private static Actor Pick(double playerAv, double enemyAv)
        {
            if (Math.Abs(playerAv - enemyAv) < 1e-9)
                return GameConfig.PlayerWinsTies ? Actor.Player : Actor.Enemy;
            return playerAv < enemyAv ? Actor.Player : Actor.Enemy;
        }

        /// <summary>다음 행동 유닛을 정하고 시간을 그만큼 진행합니다.</summary>
        public Actor Advance()
        {
            Actor next = Pick(_player.ActionValue, _enemy.ActionValue);
            double elapsed = next == Actor.Player ? _player.ActionValue : _enemy.ActionValue;
            _player.ActionValue -= elapsed;
            _enemy.ActionValue -= elapsed;
            return next;
        }

        /// <summary>행동을 마친 유닛의 대기량을 다시 채웁니다. 플레이어는 수 속성 일시 SPD를 이때 1회 반영 후 소모.</summary>
        public void Reset(Actor actor)
        {
            if (actor == Actor.Player)
            {
                double spd = _player.BaseSpeed * (1.0 + _player.PendingSpeedBuff);
                _player.ActionValue = FullGauge(spd);
                _player.PendingSpeedBuff = 0;
            }
            else
            {
                _enemy.ActionValue = FullGauge(_enemy.BaseSpeed);
            }
        }

        /// <summary>
        /// 속도 바용 예상 순서. 현재 상태를 바꾸지 않습니다.
        /// pendingPlayerBuff: 아직 쓰지 않은 플레이어 일시 SPD (첫 플레이어 재충전에만 반영).
        /// </summary>
        public List<Actor> Preview(int count)
        {
            var list = new List<Actor>(count);
            double p = _player.ActionValue;
            double e = _enemy.ActionValue;
            double buff = _player.PendingSpeedBuff;
            for (int i = 0; i < count; i++)
            {
                Actor next = Pick(p, e);
                double elapsed = next == Actor.Player ? p : e;
                p -= elapsed;
                e -= elapsed;
                list.Add(next);
                if (next == Actor.Player)
                {
                    p = FullGauge(_player.BaseSpeed * (1.0 + buff));
                    buff = 0;
                }
                else
                {
                    e = FullGauge(_enemy.BaseSpeed);
                }
            }
            return list;
        }
    }
}
