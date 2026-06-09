using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 대기 상태. 입력이 없을 때 머무는 FSM의 기본 상태.
    ///
    /// M1-A 범위에선 "전이 대상"이 아직 없다(Move는 M1-B에서 등록). 그래서 지금은 이동 입력을
    /// 감지하면 실제 전이 대신 '전이 의도'만 로그로 남긴다 — FSM 골격(진입/Tick/탈출 + ChangeState
    /// 순서)이 살아 있음을 먼저 증명하고, 상태 추가는 묶음 단위로 붙여 나간다.
    /// </summary>
    public class IdleState : IPlayerState
    {
        private readonly PlayerStateMachine _sm;
        private readonly PlayerInputReader _input;

        public IdleState(PlayerStateMachine sm, PlayerInputReader input)
        {
            _sm = sm;
            _input = input;
        }

        public void OnEnter()
        {
            Debug.Log("[FSM] → Idle");
            // 실제 정지(velocity 0)는 Locomotion이 붙는 M1-B에서. 지금은 진입 사실만 표시.
        }

        public void Tick()
        {
            // 이동 입력이 들어오면 Move로 전이할 자리. M1-B에서 _sm.ChangeState(moveState)로 연결된다.
            if (_input != null && _input.MoveIntent.sqrMagnitude > 0.01f)
            {
                Debug.Log("[FSM] Idle: 이동 입력 감지 → (M1-B에서 Move 전이 연결 예정)");
            }
        }

        public void OnExit()
        {
            // Idle은 켜 두는 자원이 없어 복구할 것이 없다. 인터페이스 계약 충족용 빈 구현.
        }
    }
}
