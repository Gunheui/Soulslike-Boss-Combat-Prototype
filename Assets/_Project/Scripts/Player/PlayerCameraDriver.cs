using UnityEngine;
using Unity.Cinemachine;
using Project.Combat;

namespace Project.Player
{
    /// <summary>
    /// 락온/free-look 상태를 Cinemachine 카메라로 반영하는 드라이버. CM_Player OrbitalFollow의
    /// recentering·PositionDamping을 상태에 따라 갈아끼운다. recentering 설정의 <b>단일 출처</b>라,
    /// 씬에 저장된 Recentering/Damping 값은 무시하고 여기 튜닝값이 매 전이마다 덮어쓴다.
    ///
    /// <b>두 가지 추종 프로필</b>(둘 다 엘든링 free 카메라 관례):
    /// <list type="bullet">
    /// <item><b>free-look</b> — 이동 중에만 수평 recentering(Wait>0). 걸으면 카메라가 진행 방향 뒤로
    ///   서서히 정렬되고, 멈추면 그 각도를 유지하며, 우스틱을 만지면 즉시 양보(자유 궤도). 수직(피치)은
    ///   건드리지 않아 플레이어가 올려본 앵글이 보존된다.</item>
    /// <item><b>락온</b> — 수평+수직 recentering 즉시(Wait=0) + 우스틱 차단. 플레이어가 타겟을 마주보므로
    ///   (FaceTarget) 카메라는 타겟 방향 azimuth로, 피치는 씬 Center(17.5°)로 수렴해 프레이밍이 일관.</item>
    /// </list>
    ///
    /// <b>왜 LockOnSystem과 분리하나</b> = 엔진 의존(Unity.Cinemachine)을 이 드라이버 한 곳에 가둔다.
    /// LockOnSystem은 Cinemachine을 몰라 EditMode 테스트에서 자유롭고, 카메라 연출만 여기서 갈아끼운다.
    ///
    /// <b>왜 단일 vcam + recentering인가</b> (엘든링 방식) = 2-vcam priority 블렌딩은 position 모델이
    /// 달라 락온 토글마다 카메라가 출렁였다. 단일 vcam은 <i>같은 궤도 위 회전</i>만 일어나 position 점프가
    /// 없고, 축 Value가 연속이라 해제 순간 보던 시점이 그대로 유지된다(옛 각도 복귀 없음).
    ///
    /// <b>동작 원리</b> = vcam의 TrackingTarget을 플레이어 몸이 아니라 런타임 생성 <b>recenter pivot</b>으로
    /// 바꾼다(Awake). pivot은 위치=플레이어, 회전=락온이면 "플레이어→타겟" yaw / free-look이면 몸 forward
    /// 미러(LateUpdate). RecenteringTarget=TrackingTarget이라 recentering Center가 "pivot forward 뒤"로
    /// 매 프레임 갱신 — free-look은 기존(진행 방향 뒤)과 동일하고, 락온은 몸 회전과 <b>무관하게</b> 항상
    /// 타겟 방향 뒤로 수렴한다. 덕분에 회피로 몸이 구르는 방향을 향해도 카메라가 휩쓸리지 않아,
    /// 락온 중엔 회피에도 recentering을 끄지 않고 연속 추종한다(동결→재무장 2단 끊김 제거).
    /// </summary>
    public class PlayerCameraDriver : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private LockOnSystem lockOn;
        [Tooltip("CM_Player의 CinemachineCamera. 락온 시 LookAt=타겟으로 설정해 RotationComposer가 타겟을 화면 중앙에 조준한다(몸/회피 회전과 무관하게 보스 고정). 해제 시 null.")]
        [SerializeField] private CinemachineCamera vcam;
        [Tooltip("CM_Player의 OrbitalFollow. recentering·PositionDamping을 상태별로 갈아끼운다.")]
        [SerializeField] private CinemachineOrbitalFollow orbital;
        [Tooltip("CM_Player의 카메라 입력 컨트롤러. 락온 중 우스틱 카메라 조작 차단(엘든링식).")]
        [SerializeField] private CinemachineInputAxisController cameraInput;
        [Tooltip("회피 상태 감지용. free-look 구르기 동안 recentering을 멈춰 카메라가 구르는 방향으로 휩쓸리는 것을 막는다(락온은 pivot이 타겟 기준이라 계속 추종). 비우면 같은 GameObject에서 자동 회수.")]
        [SerializeField] private PlayerStateMachine stateMachine;
        [Tooltip("이동 속도 판정용(free-look 추종을 이동 중에만 켠다). 비우면 같은 GameObject에서 자동 회수.")]
        [SerializeField] private PlayerLocomotion locomotion;

        [Header("락온 튜닝값")]
        [Tooltip("락온 중 카메라가 타겟 방향 azimuth/피치에 도달하는 시간(s). 작을수록 빳빳하게 추종.")]
        [SerializeField] private float recenterTime = 0.75f;
        [Tooltip("락온 시 PositionDamping. 작을수록 빳빳하게 추종 — 타겟 프레이밍이 덜 출렁인다.")]
        [SerializeField] private float lockOnDamping = 0.2f;

        [Header("free-look 튜닝값 (엘든링식 진행방향 추종)")]
        [Tooltip("free-look 시 OrbitalFollow PositionDamping(x/y/z 동일값). 클수록 부드럽게 지연 추종.")]
        [SerializeField] private float freeLookDamping = 0.4f;
        [Tooltip("이동을 멈춘 뒤(우스틱 안 만진 채) 이 시간(s) 지나면 수평 추종 시작. 클수록 늦게 따라붙어 더 느슨.")]
        [SerializeField] private float freeLookRecenterWait = 0.5f;
        [Tooltip("free-look 수평 추종이 진행 방향 뒤에 도달하는 시간(s). 락온보다 길게 줘 부드럽게.")]
        [SerializeField] private float freeLookRecenterTime = 1.0f;
        [Tooltip("이 속도(m/s) 초과로 이동할 때만 free-look 추종을 켠다. 멈춰 서면 현재 각도 유지.")]
        [SerializeField] private float moveThreshold = 0.2f;

        // 현재 락온 여부(Apply가 갱신). Update가 프로필 선택에 쓴다.
        private bool _locked;
        // free-look 시 카메라가 바라볼 기본 LookAt(보통 플레이어) — 씬 원본을 Awake에서 캡처해 둔다.
        // 락온 시 vcam.LookAt을 보스로 바꿨다가, 해제하면 이걸로 복원(null로 지우면 RotationComposer가
        // 비활성돼 카메라가 캐릭터를 안 보고 한 점만 본 채 도는 버그가 난다).
        private Transform _defaultLookAt;
        // 마지막으로 적용한 recentering 프로필. 매 프레임 재적용(=CancelRecentering 스팸)을 막아 변경 시에만 토글.
        // 초깃값은 어떤 실제 프로필과도 다른 sentinel(Wait/Time=-1) — Awake의 첫 정규화가 반드시 쓰이도록.
        private RecenterProfile _applied = new RecenterProfile(true, true, -1f, -1f);
        // recentering 기준 pivot(런타임 생성, vcam TrackingTarget). 몸 forward와 카메라 추종 기준을 분리한다
        // — 락온 중 회피로 몸이 돌아도 카메라는 타겟 방향 뒤를 유지. 플레이어 자식이 아님(부모 회전 상속 방지).
        private Transform _pivot;
        // 락온 타겟 transform 캐시(Apply가 갱신). LateUpdate의 pivot 회전 산출용.
        private Transform _targetTransform;

        // 적용된 recentering 상태 스냅샷. 같은 프로필이면 재적용 생략(Equals 비교).
        private readonly struct RecenterProfile : System.IEquatable<RecenterProfile>
        {
            public readonly bool Horizontal, Vertical;
            public readonly float Wait, Time;
            public RecenterProfile(bool h, bool v, float wait, float time)
            { Horizontal = h; Vertical = v; Wait = wait; Time = time; }
            public bool Equals(RecenterProfile o) =>
                Horizontal == o.Horizontal && Vertical == o.Vertical && Wait == o.Wait && Time == o.Time;
            public static readonly RecenterProfile Off = new RecenterProfile(false, false, 0f, 0f);
        }

        private void Awake()
        {
            if (lockOn == null) lockOn = GetComponent<LockOnSystem>();
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
            if (locomotion == null) locomotion = GetComponent<PlayerLocomotion>();
            // 씬 저장 상태와 무관하게 free-look 댐핑 + recentering off + LookAt 해제로 정규화하고 시작.
            if (orbital != null)
            {
                ApplyRecentering(RecenterProfile.Off);
                SetPositionDamping(freeLookDamping);
            }
            // 씬 원본 LookAt(보통 플레이어)을 캡처 — free-look은 이걸 봐야 카메라가 캐릭터를 바라보며 돈다.
            // ⚠ 여기서 null로 지우면 free-look 조준이 깨진다(캐릭터 안 보고 한 점만 봄). 지우지 않는다.
            // ⚠ 캡처는 반드시 아래 Follow 교체 전 — 씬에 LookAt 미설정이면 getter가 TrackingTarget(플레이어)로
            //   폴백하는데, 교체 후엔 pivot으로 폴백해 잘못된 기본값이 잡힌다.
            if (vcam != null)
            {
                _defaultLookAt = vcam.LookAt;

                // recentering 기준 pivot 생성 + Follow 교체(씬 직렬화 무변경). 위치=플레이어와 동일하게
                // 시작하므로 카메라 점프 없음. 회전은 LateUpdate가 매 프레임 갱신.
                _pivot = new GameObject("CameraRecenterPivot").transform;
                _pivot.SetPositionAndRotation(transform.position, transform.rotation);
                vcam.Follow = _pivot;
            }
        }

        private void OnDestroy()
        {
            if (_pivot != null) Destroy(_pivot.gameObject);
        }

        // LockOnSystem의 타겟 변경을 구독해 락온 프로필(우스틱 차단·댐핑)을 갈아끼운다.
        // Awake에서 자동 회수한 lockOn을 쓰므로 OnEnable이 Awake 뒤에 도는 순서에 의존.
        private void OnEnable()
        {
            if (lockOn != null) lockOn.OnTargetChanged += Apply;
        }

        private void OnDisable()
        {
            if (lockOn != null) lockOn.OnTargetChanged -= Apply;
        }

        private void Update()
        {
            if (orbital == null) return;
            ApplyRecentering(SelectProfile());
        }

        // pivot 구동. LateUpdate인 이유 = 플레이어 이동/회전(Update의 FSM Tick) 이후·CinemachineBrain
        // (실행 순서가 높아 일반 LateUpdate 뒤) 이전에 끼어야 1프레임 지연 지터가 없다.
        private void LateUpdate()
        {
            if (_pivot == null) return;

            _pivot.position = transform.position;

            if (_locked && _targetTransform != null)
            {
                // 락온: "플레이어→타겟" yaw — 몸이 회피/strafe로 어디를 보든 카메라 기준은 타겟 방향.
                Vector3 dir = _targetTransform.position - transform.position;
                dir.y = 0f;
                // 타겟이 거의 머리 위/발밑이면 방향이 퇴화 — 직전 회전 유지(스냅 방지).
                if (dir.sqrMagnitude > 0.0001f)
                    _pivot.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
            else
            {
                // free-look: 몸 forward 그대로 미러 — 기존(TrackingTarget=플레이어) 동작과 동일.
                _pivot.rotation = transform.rotation;
            }
        }

        // 매 프레임 상태로부터 원하는 recentering 프로필을 고른다.
        private RecenterProfile SelectProfile()
        {
            // free-look 회피 중만 recentering off — SnapFacing으로 몸이 회피 방향으로 홱 돌 때
            //   Center(pivot=몸 forward 미러)가 같이 돌아 카메라가 휩쓸리기 때문. 위치를 얼려 보존.
            //   락온은 pivot이 몸이 아니라 타겟 방향을 보므로 회피 회전에 영향받지 않는다 → 끄지 않고
            //   연속 추종(끄면 회피 동안의 azimuth 변위가 종료 시 재무장으로 몰려 2단 끊김이 생긴다).
            bool dodging = stateMachine != null && stateMachine.CurrentState is DodgeState;
            if (dodging && !_locked) return RecenterProfile.Off;

            if (_locked)
                // 락온: 수평+수직 즉시 추종(타겟 방향·정규 피치로 수렴).
                return new RecenterProfile(true, true, 0f, recenterTime);

            // free-look: 이동 중에만 수평 추종(진행 방향 뒤로 정렬). 멈추면 off로 현재 각도 유지.
            // 우스틱을 만지면 OrbitalFollow가 축 Value 변화를 감지해 recentering을 양보하므로(cameraInput
            // 살아 있음), 켜 둬도 조작을 방해하지 않는다. 수직은 free-look에서 건드리지 않는다(피치 보존).
            bool moving = locomotion != null && locomotion.PlanarVelocity.sqrMagnitude > moveThreshold * moveThreshold;
            return moving
                ? new RecenterProfile(true, false, freeLookRecenterWait, freeLookRecenterTime)
                : RecenterProfile.Off;
        }

        private void Apply(CombatActor target)
        {
            _locked = target != null;
            _targetTransform = target != null ? target.transform : null;   // LateUpdate pivot 회전용 캐시

            // 카메라 '진짜 타겟 추적': 락온이면 vcam.LookAt=타겟 transform → RotationComposer가 그 점을
            // 화면 중앙에 조준한다(몸/회피 회전과 무관하게 보스 고정). 해제면 _defaultLookAt(플레이어)로
            // 복원 → free-look이 다시 캐릭터를 바라보며 돈다. (null로 두면 조준이 깨진다.)
            // 타겟은 Transform 하나라 보스 전체든 부위(다중 락온 포인트)든 코드 무변경 — 그저 가리키는 점만 바뀜.
            if (vcam != null) vcam.LookAt = target != null ? target.transform : _defaultLookAt;

            // 락온 중 우스틱 카메라 조작 차단. free-look은 우스틱이 살아 있어야 자유 궤도 + recentering 양보가 된다.
            if (cameraInput != null) cameraInput.enabled = !_locked;
            // PositionDamping은 락온 토글에만 의존(이동·회피와 무관) — 여기서 바로 적용.
            if (orbital != null) SetPositionDamping(_locked ? lockOnDamping : freeLookDamping);
        }

        // OrbitalFollow의 위치 추종 댐핑(x/y/z 동일값). TrackerSettings는 public 필드라 멤버 직접 대입 가능.
        private void SetPositionDamping(float d)
        {
            orbital.TrackerSettings.PositionDamping = new Vector3(d, d, d);
        }

        // recentering 프로필 적용. 직전과 같으면 생략(매 프레임 CancelRecentering 스팸·재무장 방지).
        private void ApplyRecentering(RecenterProfile p)
        {
            if (p.Equals(_applied)) return;
            _applied = p;

            orbital.HorizontalAxis.Recentering =
                new InputAxis.RecenteringSettings { Enabled = p.Horizontal, Wait = p.Wait, Time = p.Time };
            orbital.VerticalAxis.Recentering =
                new InputAxis.RecenteringSettings { Enabled = p.Vertical, Wait = p.Wait, Time = p.Time };

            // 꺼진 축은 잔류 recentering 속도/상태를 정리. Value는 건드리지 않아 보던 시점이 그대로 유지된다.
            if (!p.Horizontal) orbital.HorizontalAxis.CancelRecentering();
            if (!p.Vertical) orbital.VerticalAxis.CancelRecentering();
        }
    }
}
