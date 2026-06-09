using UnityEngine;
using Project.Combat;   // ← Player → Combat 단방향 의존. 이 using이 컴파일로 의존 방향을 실증한다.

namespace Project.Player
{
    /// <summary>
    /// 플레이어 상태 머신. 현재 상태 1개를 들고, 매 프레임 그 상태의 Tick을 돌린다.
    ///
    /// 이 컴포넌트가 보장하는 단 하나의 불변식: <see cref="ChangeState"/>는 항상
    /// <c>이전.OnExit() → 새.OnEnter()</c> 순서로 전이한다(T1.2 DoD 핵심).
    /// 왜 이 순서가 생명인가 = 회피 i-frame을 켠 채 다른 상태로 새면 영구 무적 버그가 된다.
    /// OnExit를 먼저 "반드시" 부르면 떠나는 상태가 자기가 켠 것을 끌 기회를 갖는다 →
    /// 상태 누수 0(데드락/무적 누수 차단의 구조적 토대).
    /// </summary>
    public class PlayerStateMachine : MonoBehaviour
    {
        [Header("Combat 루트")]
        // CombatActor 참조는 기능상 M1-D 락온/M2 데미지에서 본격 사용하지만, 지금 들고 있는 더 큰 이유는
        // 'Player → Combat 단방향 의존'을 컴파일로 증명하기 위해서다(M0-A에서 빈 asmdef라 미생성됐던 검증).
        [SerializeField] private CombatActor actor;

        [Header("입력")]
        [SerializeField] private PlayerInputReader inputReader;

        // M1-A는 Idle만 인스턴스화한다. Move/Dodge 상태 인스턴스는 각각 M1-B/M1-C에서 등록.
        private IdleState _idle;

        /// <summary>현재 활성 상태. 외부(디버그/테스트)는 읽기만.</summary>
        public IPlayerState CurrentState { get; private set; }

        public CombatActor Actor => actor;
        public PlayerInputReader Input => inputReader;

        private void Start()
        {
            _idle = new IdleState(this, inputReader);
            ChangeState(_idle);
        }

        private void Update()
        {
            CurrentState?.Tick();
        }

        /// <summary>
        /// 상태 전이. OnExit → (참조 교체) → OnEnter 순서를 절대 어기지 않는다.
        /// </summary>
        public void ChangeState(IPlayerState next)
        {
            CurrentState?.OnExit();   // 1) 떠나는 상태가 켠 것을 먼저 끈다(누수 차단)
            CurrentState = next;      // 2) 참조 교체
            CurrentState.OnEnter();   // 3) 새 상태 진입 훅
        }

        // --- M1-B+ 전이 진입점(상태들이 호출). 지금은 Idle만 존재하므로 빈 헬퍼 미정의. ---
        public void ToIdle() => ChangeState(_idle);
    }
}
