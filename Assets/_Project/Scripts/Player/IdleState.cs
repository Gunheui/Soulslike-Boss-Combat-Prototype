using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 대기 상태. 입력이 없을 때 머무는 FSM의 기본 상태.
    ///
    /// 이동 입력이 감지되면 Move로 전이한다(M1-C에서 실연결). 진입 시 Locomotion을 정지시켜
    /// 직전 상태의 잔류 속도를 끊는다.
    /// </summary>
    public class IdleState : IPlayerState
    {
        private readonly PlayerStateMachine _sm;
        private readonly PlayerInputReader _input;
        private readonly PlayerLocomotion _loco;

        public IdleState(PlayerStateMachine sm, PlayerInputReader input, PlayerLocomotion loco)
        {
            _sm = sm;
            _input = input;
            _loco = loco;
        }

        public void OnEnter()
        {
            Debug.Log("[FSM] → Idle");
            _loco.Stop();
        }

        public void Tick()
        {
            // 회피는 이동/대기보다 우선 — 입력이 큐에 있으면 즉시 Dodge로(엣지 트리거, 1회 소비).
            if (_input.ConsumeDodge())
            {
                _sm.ToDodge();
                return;
            }

            // 정지 중에도 중력은 유지해야 바닥에 붙는다(공중 진입/경사 대비).
            _loco.Stop();

            if (_input.MoveIntent.sqrMagnitude > 0.01f)
                _sm.ToMove();
        }

        public void OnExit()
        {
            // Idle은 켜 두는 자원이 없어 복구할 것이 없다. 인터페이스 계약 충족용 빈 구현.
        }
    }
}
