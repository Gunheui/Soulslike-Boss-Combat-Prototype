namespace Project.Player
{
    /// <summary>플레이어 FSM의 상태 1개가 지켜야 할 계약(진입/매프레임/탈출 훅).</summary>
    public interface IPlayerState
    {
        /// <summary>상태 진입 시 1회. 자원 변화·애니 트리거·플래그 set.</summary>
        void OnEnter();

        /// <summary>매 프레임. StateMachine.Update가 현재 상태의 Tick을 대신 호출한다.</summary>
        void Tick();

        /// <summary>상태 탈출 시 1회. 켜둔 것을 강제 복구(누수 차단의 마지막 방어선).</summary>
        void OnExit();
    }
}
