using UnityEngine;
using Project.Combat;   // ← Player → Combat 단방향 의존. 이 using이 컴파일로 의존 방향을 실증한다.

namespace Project.Player
{
    /// <summary>
    /// 플레이어 상태 머신. 현재 상태 1개를 들고, 매 프레임 그 상태의 Tick을 돌린다.
    /// 불변식: <see cref="ChangeState"/>는 항상 <c>이전.OnExit() → 새.OnEnter()</c> 순서 — 어기면 i-frame 누수(영구 무적).
    /// </summary>
    public class PlayerStateMachine : MonoBehaviour
    {
        [Header("Combat 루트")]
        // 본격 사용은 M1-D 락온/M2 데미지. 지금은 Player → Combat 단방향 의존을 컴파일로 고정하는 역할.
        [SerializeField] private CombatActor actor;

        [Header("입력")]
        [SerializeField] private PlayerInputReader inputReader;

        [Header("이동")]
        [SerializeField] private PlayerLocomotion locomotion;

        [Header("회피(Dodge)")]
        [SerializeField] private PlayerIFrame iFrame;
        [Tooltip("이동 회피(Dive Roll). 클립 1.0s, 무적 0.40s. 미설정(전부 0)이면 RollDefault 폴백.")]
        [SerializeField] private DodgeConfig rollConfig;
        [Tooltip("정지 회피(Standing Backstep). 클립 0.5s, 무적 0.10s. 미설정이면 BackstepDefault 폴백.")]
        [SerializeField] private DodgeConfig backstepConfig;

        [Header("애니메이션")]
        [Tooltip("로코모션/회피 애니 브리지. 보통 모델 자식. 비우면 자식에서 탐색.")]
        [SerializeField] private PlayerAnimationDriver animDriver;

        // 상태 인스턴스는 Start에서 1회 생성해 재사용(매 전이마다 new 하면 GC 부담 + 상태 보유 데이터 유실).
        private IdleState _idle;
        private MoveState _move;
        private DodgeState _dodge;

        /// <summary>현재 활성 상태. 외부(디버그/테스트)는 읽기만.</summary>
        public IPlayerState CurrentState { get; private set; }

        public CombatActor Actor => actor;
        public PlayerInputReader Input => inputReader;

        // 회피 튜닝값 노출 — DodgeState가 진입마다 최신값을 읽어 런타임 Inspector 튜닝이 즉시 반영된다.
        // struct라 생성자로 한 번 넘기면 복사본이 굳어 런타임 수정이 안 먹으므로 프로퍼티로 노출.
        public DodgeConfig RollConfig => rollConfig;
        public DodgeConfig BackstepConfig => backstepConfig;

        private void Start()
        {
            // 인스펙터 배선이 빠져도 같은 오브젝트의 컴포넌트는 자동 회수(설정 누락 방어).
            // 그래도 null이면 의존이 진짜 없는 것 — 침묵 대신 즉시 에러로 드러낸다.
            if (locomotion == null) locomotion = GetComponent<PlayerLocomotion>();
            if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();
            if (iFrame == null) iFrame = GetComponent<PlayerIFrame>();
            // Animator는 보통 모델 자식이라 GetComponentInChildren로 폴백.
            if (animDriver == null) animDriver = GetComponentInChildren<PlayerAnimationDriver>();

            if (locomotion == null)
                Debug.LogError("[PlayerStateMachine] PlayerLocomotion 미배선 — 같은 GameObject에 PlayerLocomotion 컴포넌트를 추가하거나 인스펙터 슬롯을 채워라.", this);
            if (iFrame == null)
                Debug.LogError("[PlayerStateMachine] PlayerIFrame 미배선 — 회피 i-frame이 동작하지 않는다. 같은 GameObject에 PlayerIFrame을 추가하거나 슬롯을 채워라.", this);

            // 각 DodgeConfig가 Inspector에서 미설정(전부 0)이면 출발값으로 폴백 — 무설정 회피가
            // distance 0/지속 0으로 죽는 것을 방지(첫 배치 편의).
            if (rollConfig.timing.TotalSeconds <= 0.0001f || rollConfig.distance <= 0.0001f)
                rollConfig = DodgeConfig.RollDefault;
            if (backstepConfig.timing.TotalSeconds <= 0.0001f || backstepConfig.distance <= 0.0001f)
                backstepConfig = DodgeConfig.BackstepDefault;

            _idle = new IdleState(this, inputReader, locomotion);
            _move = new MoveState(this, inputReader, locomotion);
            _dodge = new DodgeState(this, inputReader, locomotion, iFrame, animDriver);
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

        // --- 전이 진입점(상태들이 호출). 마일스톤마다 상태가 늘면 여기에 헬퍼 추가. ---
        public void ToIdle() => ChangeState(_idle);
        public void ToMove() => ChangeState(_move);
        public void ToDodge() => ChangeState(_dodge);
    }
}
