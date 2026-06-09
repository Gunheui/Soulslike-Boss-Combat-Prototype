namespace Project.Player
{
    /// <summary>
    /// 플레이어 FSM의 상태 1개가 지켜야 할 계약.
    ///
    /// 왜 State 패턴(클래스 기반)인가 = 플레이어는 9상태(Idle/Move/Dodge/Attack/Guard/
    /// PerfectGuard/Hit/Staggered/Dead)이고 상태별 진입·탈출 시 자원/콜라이더/입력잠금을
    /// 정밀히 켜고 꺼야 한다(예: 회피 i-frame 누수 차단). enum+switch는 이 진입/탈출 훅이
    /// 흩어져 버그를 부른다. 상태마다 클래스로 OnEnter/OnExit를 묶으면 "이 상태에 들어올 때
    /// 무엇을 켜고 나갈 때 무엇을 끄는가"가 한곳에 응집된다. (보스는 상태가 단순해 enum+switch.)
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>상태 진입 시 1회. 자원 변화·애니 트리거·플래그 set.</summary>
        void OnEnter();

        /// <summary>
        /// 매 프레임. <see cref="UnityEngine.MonoBehaviour"/>의 Update와 구분하려고 Tick으로 명명
        /// (StateMachine의 Update가 현재 상태의 Tick을 대신 호출한다).
        /// </summary>
        void Tick();

        /// <summary>상태 탈출 시 1회. 켜둔 것을 강제 복구(누수 차단의 마지막 방어선).</summary>
        void OnExit();
    }
}
