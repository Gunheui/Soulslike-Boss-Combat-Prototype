using UnityEngine;
using Unity.Cinemachine;
using Project.Combat;

namespace Project.Player
{
    /// <summary>
    /// 락온/free-look 상태를 CM_Player OrbitalFollow의 recentering·PositionDamping으로 반영하는 드라이버.
    /// recentering 설정의 단일 출처 — 씬 저장값을 무시하고 매 전이마다 덮어쓴다.
    ///
    /// 두 프로필(엘든링 free 카메라 관례): free-look = 이동 중에만 수평 recentering(멈추면 각도 유지,
    /// 우스틱 양보, 피치 보존), 락온 = 수평+수직 즉시 추종 + 우스틱 차단(타겟 azimuth로 수렴).
    ///
    /// 왜 LockOnSystem과 분리 = 엔진 의존(Cinemachine)을 여기 가둬 LockOnSystem을 EditMode에서 자유롭게.
    /// 왜 단일 vcam + recentering(엘든링) = 2-vcam priority 블렌딩은 토글마다 position이 점프해 출렁였다.
    /// 단일 vcam은 같은 궤도 위 회전만 일어나 점프가 없고, 축 Value가 연속이라 해제 시 보던 시점이 유지된다.
    ///
    /// 핵심 트릭 둘(상세 메커니즘은 아래 멤버·메서드 주석): ① recenter pivot — vcam TrackingTarget을 몸이
    /// 아닌 런타임 pivot으로 바꿔, 락온 중 회피로 몸이 돌아도 카메라는 타겟 방향 뒤를 유지(추종 끊김 제거).
    /// ② 좌우 프레이밍 오프셋 — strafe 실속도로 프레임을 이동 반대쪽에 밀어 캐릭터를 화면 한쪽에 치우치게.
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
        [Tooltip("진행 방향이 카메라 정면과 이 각도(°) 이상 벌어지면(=카메라 쪽으로 걸어오면) 수평 추종을 끈다. " +
                 "camera-relative 입력과 recentering이 서로를 쫓는 피드백(빙글빙글 원/8자 회전) 차단. 엘든링도 동일하게 카메라를 향해 달릴 땐 추종 안 함.")]
        [SerializeField] private float recenterBlockAngle = 110f;

        [Header("좌우 프레이밍 오프셋 (엘든링식)")]
        [Tooltip("락온 시 최대 치우침(m). strafe에서 캐릭터가 이동 방향 쪽으로 이만큼 화면 이동(보스는 중앙 유지).")]
        [SerializeField] private float lateralOffsetLockOn = 0.6f;
        [Tooltip("free-look 시 최대 치우침(m). 락온보다 은은하게.")]
        [SerializeField] private float lateralOffsetFree = 0.35f;
        [Tooltip("이 측면 속도(m/s)에서 최대 오프셋에 도달. strafe 속도와 맞추면 strafe 중 항상 최대치.")]
        [SerializeField] private float lateralRefSpeed = 2f;
        [Tooltip("오프셋 스무딩 시간(s, SmoothDamp). 클수록 느슨하게 따라붙고 복귀도 느리다.")]
        [SerializeField] private float lateralSmoothTime = 0.35f;

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
        // free-look 조준점(런타임 생성, 락온 해제 시 vcam.LookAt). 위치=_defaultLookAt+측면 오프셋.
        // _defaultLookAt(플레이어)을 직접 LookAt으로 쓰면 composer가 캐릭터를 항상 중앙에 재조준해
        // 치우침 효과가 0이 되므로, 오프셋이 실린 대리 transform을 조준점으로 쓴다.
        private Transform _lookPivot;
        // 출력 카메라 캐시(Awake). 측면 오프셋의 "화면 좌우" 기준 = 이 카메라의 right(XZ 투영).
        // Brain이 LateUpdate 마지막에 갱신하므로 여기서 읽는 값은 직전 프레임 — SmoothDamp이 흡수.
        private Transform _camTransform;
        // 측면 오프셋 현재값(m)·SmoothDamp 속도·마지막 유효 카메라 right(수직 시점 퇴화 가드).
        private float _lateral;
        private float _lateralVel;
        private Vector3 _lastCamRight = Vector3.right;

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

                // free-look 조준점 대리 transform. 초기 위치=_defaultLookAt(오프셋 0)이라 시작 프레이밍
                // 무변화. ⚠ LookAt 교체를 Apply에만 두면 첫 락온 토글 전까지 free-look 오프셋이 죽는다
                // (Apply는 락온 변경 시에만 호출) — 여기서 즉시 교체.
                if (_defaultLookAt != null)
                {
                    _lookPivot = new GameObject("CameraLookPivot").transform;
                    _lookPivot.position = _defaultLookAt.position;
                    vcam.LookAt = _lookPivot;
                }
            }

            // 측면 오프셋의 화면 좌우 기준 카메라(Brain이 붙은 출력 카메라).
            _camTransform = Camera.main != null ? Camera.main.transform : null;
        }

        private void OnDestroy()
        {
            if (_pivot != null) Destroy(_pivot.gameObject);
            if (_lookPivot != null) Destroy(_lookPivot.gameObject);
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

            Vector3 lateralOffset = ComputeLateralOffset();
            _pivot.position = transform.position + lateralOffset;
            // free-look 조준점도 같은 양만큼 평행이동 — 카메라 위치(pivot)와 조준(lookPivot)이 함께 밀려야
            // 프레임이 통째로 이동해 캐릭터가 화면에서 치우친다(락온 중엔 LookAt=보스라 자동 무효).
            if (_lookPivot != null && _defaultLookAt != null)
                _lookPivot.position = _defaultLookAt.position + lateralOffset;

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

        // 좌우 프레이밍 오프셋 산출(카메라 right 방향 월드 벡터). LateUpdate에서 매 프레임 호출.
        // 정지/회피 시 목표가 0이 되어 SmoothDamp이 중앙으로 부드럽게 복귀시킨다.
        private Vector3 ComputeLateralOffset()
        {
            // 화면 좌우 기준 = 출력 카메라 right의 XZ 투영. 카메라가 수직을 보면 투영이 퇴화(0 수렴)
            // — 그때는 마지막 유효값을 유지해 오프셋 방향이 튀지 않게 한다.
            if (_camTransform != null)
            {
                Vector3 r = _camTransform.right;
                r.y = 0f;
                if (r.sqrMagnitude > 0.0001f) _lastCamRight = r.normalized;
            }

            // 목표 = 측면 속도 비율(-1~1) × 모드별 최대 오프셋. 입력이 아니라 실제 속도라 가감속과 동기.
            // 회피 중엔 0 — 고속 회피 이동이 측면 속도로 잡혀 카메라가 좌우로 휩쓸리는 것을 막는다.
            float target = 0f;
            bool dodging = stateMachine != null && stateMachine.CurrentState is DodgeState;
            if (!dodging && locomotion != null)
            {
                float amount = Mathf.Clamp(
                    Vector3.Dot(locomotion.PlanarVelocity, _lastCamRight) / Mathf.Max(lateralRefSpeed, 0.01f),
                    -1f, 1f);
                // 부호: 프레임 중심(카메라+조준점)을 이동 '반대쪽'으로 밀어야 캐릭터가 이동 방향 쪽에 보인다.
                target = -amount * (_locked ? lateralOffsetLockOn : lateralOffsetFree);
            }

            _lateral = Mathf.SmoothDamp(_lateral, target, ref _lateralVel, lateralSmoothTime);
            return _lastCamRight * _lateral;
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
            // 단, 카메라 쪽으로 걸어올 땐 추종을 끈다 — recentering 목표(몸 forward 뒤)가 항상 ~180° 앞에
            // 있는데 이동이 camera-relative라 카메라가 돌면 몸도 같이 돌아, 목표에 영원히 못 닿는 무한
            // 추격(원 회전)이 되고 정후방 경계에선 최단경로 방향이 좌우로 뒤집혀 8자가 된다.
            bool moving = locomotion != null && locomotion.PlanarVelocity.sqrMagnitude > moveThreshold * moveThreshold;
            return moving && !MovingTowardCamera()
                ? new RecenterProfile(true, false, freeLookRecenterWait, freeLookRecenterTime)
                : RecenterProfile.Off;
        }

        // 진행 방향이 카메라 정면에서 recenterBlockAngle(°) 이상 벌어졌는가(=카메라를 향해 이동 중인가).
        // XZ 평면 비교 — 카메라가 수직을 보면 forward 투영이 퇴화하므로 false(추종 허용, 다음 프레임 복구).
        private bool MovingTowardCamera()
        {
            if (_camTransform == null || locomotion == null) return false;
            Vector3 camFwd = _camTransform.forward;
            camFwd.y = 0f;
            Vector3 vel = locomotion.PlanarVelocity;
            if (camFwd.sqrMagnitude < 0.0001f || vel.sqrMagnitude < 0.0001f) return false;
            return Vector3.Angle(camFwd, vel) > recenterBlockAngle;
        }

        private void Apply(CombatActor target)
        {
            _locked = target != null;
            _targetTransform = target != null ? target.transform : null;   // LateUpdate pivot 회전용 캐시

            // 카메라 '진짜 타겟 추적': 락온이면 vcam.LookAt=타겟 transform → RotationComposer가 그 점을
            // 화면 중앙에 조준한다(몸/회피 회전과 무관하게 보스 고정). 해제면 _lookPivot(플레이어+측면
            // 오프셋 대리점)으로 복원 → free-look이 다시 캐릭터를 바라보며 돈다. (null로 두면 조준이 깨진다.)
            // 타겟은 Transform 하나라 보스 전체든 부위(다중 락온 포인트)든 코드 무변경 — 그저 가리키는 점만 바뀜.
            if (vcam != null)
                vcam.LookAt = target != null ? target.transform
                            : _lookPivot != null ? _lookPivot : _defaultLookAt;

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
