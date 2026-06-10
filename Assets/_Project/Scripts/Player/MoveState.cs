using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 이동 상태. 이동 입력이 있는 동안 머물며 매 Tick <see cref="PlayerLocomotion"/>을 구동한다.
    ///
    /// 상태는 "언제 들어오고 나가는가"만 판정하고, 실제 이동 물리는 Locomotion에 위임한다
    /// (관심사 분리 — 같은 Locomotion을 Dodge 상태도 재사용).
    /// M1-C 범위 전이: Move ↔ Idle 왕복만. Attack/Dodge/Guard/Hit 전이는 해당 마일스톤에서 추가.
    /// </summary>
    public class MoveState : IPlayerState
    {
        private readonly PlayerStateMachine _sm;
        private readonly PlayerInputReader _input;
        private readonly PlayerLocomotion _loco;

        public MoveState(PlayerStateMachine sm, PlayerInputReader input, PlayerLocomotion loco)
        {
            _sm = sm;
            _input = input;
            _loco = loco;
        }

        public void OnEnter()
        {
            Debug.Log("[FSM] → Move");
        }

        public void Tick()
        {
            // 회피는 이동보다 우선 — 이동 중 회피 입력이 들어오면 즉시 Dodge로 전이.
            if (_input.ConsumeDodge())
            {
                _sm.ToDodge();
                return;
            }

            // 입력이 끊기면 Idle로 복귀. 임계값은 스틱 드리프트/미세 입력 무시용(Idle 전이 판정과 동일 기준).
            if (_input.MoveIntent.sqrMagnitude <= 0.01f)
            {
                _sm.ToIdle();
                return;
            }

            _loco.Move(_input.MoveIntent, _input.SprintHeld);
        }

        public void OnExit()
        {
            // Move를 떠날 때 수평 속도를 끊어 잔류 미끄러짐을 막는다(상태 누수 차단의 마지막 방어선).
            _loco.Stop();
        }
    }
}
