using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 로코모션 애니 브리지. <see cref="PlayerLocomotion"/>의 이동 속도를 읽어 Animator 블렌드 트리
    /// 파라미터(MoveX/MoveY/Speed)로 흘려보낸다. 한 방향(velocity → Animator)으로만 읽어가므로
    /// 상태/물리 코드는 Animator를 몰라도 된다(M1-C/D 관심사 분리 연장).
    ///
    /// <b>왜 별도 컴포넌트인가</b> = 이동 물리(Locomotion)와 표현(애니)을 분리하면 애니 시스템을
    /// 갈아끼워도 전투 결정성(i-frame/히트박스 타이밍)이 영향받지 않는다. 브리지는 읽기 전용.
    ///
    /// <b>Root Motion OFF</b> = 클립은 in-place(제자리), 실제 이동은 CharacterController가 한다.
    /// Root Motion을 켜면 애니가 위치를 밀어 전투 이벤트 타이밍이 이동 속도에 끌려간다(소울라이크 금기).
    /// </summary>
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("로코모션 클립을 재생할 모델의 Animator(보통 자식). 비우면 자식에서 탐색.")]
        [SerializeField] private Animator animator;
        [Tooltip("속도 단일 진실원. 비우면 같은 GameObject에서 탐색.")]
        [SerializeField] private PlayerLocomotion loco;

        [Header("블렌드 댐핑")]
        // 가속 램프의 주체는 PlayerLocomotion(속도 자체가 부드럽게 오른다) — 이 dampTime은 1프레임
        // jitter만 제거하는 보조값. 크게 잡으면(예: 0.5) 물리 속도와 따로 놀아 foot-slide를 만든다.
        [Tooltip("파라미터 보간 시간(초). jitter 제거용 최소값. 가속 곡선은 PlayerLocomotion이 담당.")]
        [SerializeField] private float dampTime = 0.05f;

        // 문자열 대신 해시로 SetFloat — 매 프레임 호출이라 문자열 룩업 비용 제거.
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        // 락온 = strafe 서브트리 전환 게이트(bool). 회피 = 전용 Dodge 클립 1회 재생(trigger).
        private static readonly int LockedOnHash = Animator.StringToHash("LockedOn");
        private static readonly int DodgeHash = Animator.StringToHash("Dodge");
        // 회피 클립 분기(엘든링 규칙): 이동 입력 있으면 방향 구르기, 없으면 제자리 백스텝.
        // DodgeBack=true → Standing Dodge Backward, false → Running Dive Roll.
        private static readonly int DodgeBackHash = Animator.StringToHash("DodgeBack");

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (loco == null) loco = GetComponent<PlayerLocomotion>();

            if (animator == null)
                Debug.LogError("[PlayerAnimationDriver] Animator 미배선 — 모델 자식에 Animator가 있어야 로코모션 애니가 재생된다.", this);
            if (loco == null)
                Debug.LogError("[PlayerAnimationDriver] PlayerLocomotion 미배선 — 속도 소스가 없어 애니가 idle에 고정된다.", this);

            // in-place 클립 전제 — 애니가 위치를 밀지 않게 강제(인스펙터 실수 방어).
            if (animator != null) animator.applyRootMotion = false;
        }

        private void Update()
        {
            if (animator == null || loco == null) return;

            // 월드 속도를 캐릭터 facing 기준 로컬로 변환 → 블렌드 트리 축(전/후/좌/우)에 직접 대응.
            // 프리룩: 진행 방향을 바라보므로 local.z≈속력, local.x≈0(전진축). 락온 strafe 방향성은 F3.
            Vector3 local = transform.InverseTransformDirection(loco.PlanarVelocity);

            animator.SetFloat(MoveXHash, local.x, dampTime, Time.deltaTime);
            animator.SetFloat(MoveYHash, local.z, dampTime, Time.deltaTime);
            animator.SetFloat(SpeedHash, loco.PlanarVelocity.magnitude, dampTime, Time.deltaTime);

            // 락온이면 strafe 서브트리(상체 정면 고정·하체 스텝)로 전환. facing=타겟이라 velocity가
            // 자동으로 옆/뒤 축으로 로컬화돼 MoveX/MoveY가 strafe 6방향에 그대로 대응한다.
            // 단 락온 sprint 중엔 free-look 서브트리 — 몸이 진행 방향을 보므로(strafe 깨짐) local.z≈속력이
            // 되어 전방 달리기 클립이 그대로 재생된다(옆걸음 전력질주 모션 방지, 새 애니 작업 0).
            animator.SetBool(LockedOnHash, loco.LockOnTarget != null && !loco.IsLockOnSprint);
        }

        /// <summary>
        /// 회피 전용 클립을 1회 재생(Any → Dodge 트리거). <see cref="DodgeState"/>가 진입 시 호출한다.
        /// 회피 중 PlanarVelocity 피크(~15 m/s)가 로코모션 블렌드를 Run으로 밀던 문제를 덮는다 —
        /// Dodge 상태에 있는 동안 MoveX/MoveY/Speed는 블렌드에 영향 없음. i-frame은 타이머가 권한
        /// (이 트리거는 표현만, 전투 결정성 무관).
        /// </summary>
        /// <param name="back">true면 백스텝(Standing Dodge Backward), false면 방향 구르기(Running Dive Roll).</param>
        public void PlayDodge(bool back)
        {
            if (animator == null) return;
            // 트리거 전에 분기 bool을 먼저 세팅 — Any-State 전이가 같은 평가에서 올바른 클립을 고른다.
            animator.SetBool(DodgeBackHash, back);
            animator.SetTrigger(DodgeHash);
        }
    }
}
