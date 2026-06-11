using UnityEngine;
using Unity.Cinemachine;
using Project.Combat;

namespace Project.Player
{
    /// <summary>
    /// 락온 상태를 Cinemachine 카메라로 반영하는 드라이버. <see cref="LockOnSystem.OnTargetChanged"/>를
    /// 구독해, 락온 중에는 free-look vcam(CM_Player)의 OrbitalFollow recentering을 켜 카메라가
    /// 플레이어 뒤(=타겟 방향)로 따라돌게 하고, 해제 시 recentering만 끈다.
    ///
    /// <b>왜 LockOnSystem과 분리하나</b> = 엔진 의존(Unity.Cinemachine)을 이 드라이버 한 곳에 가둔다.
    /// LockOnSystem은 Cinemachine을 몰라 EditMode 테스트에서 자유롭고, 카메라 연출만 여기서 갈아끼운다
    /// (관심사 분리 — 상태=로직 / 드라이버=연출).
    ///
    /// <b>왜 락온 전용 vcam이 아니라 단일 vcam + recentering인가</b> (엘든링 방식) = 이전의 2-vcam
    /// priority 블렌딩은 두 vcam의 position 모델이 달라(고정 FollowOffset vs 궤도) 락온 토글 때마다
    /// 카메라 위치가 출렁였다. 단일 vcam은 락온 중에도 <i>같은 궤도 위 회전</i>만 일어나 position 점프가
    /// 원천적으로 없고, 축 Value가 연속이라 해제 순간 보던 시점이 그대로 유지된다(옛 각도 복귀 없음).
    /// 해제 시 yaw를 역산해 동기화하던 코드도 통째로 불필요해졌다.
    ///
    /// <b>동작 원리</b> = CM_Player OrbitalFollow의 RecenteringTarget=TrackingTarget이라, recentering이
    /// 켜지면 HorizontalAxis.Center가 매 프레임 "플레이어 forward의 뒤"로 갱신된다. 락온 중에는
    /// <see cref="PlayerLocomotion"/>의 FaceTarget이 플레이어를 타겟으로 돌려세우므로, 카메라는
    /// 자동으로 타겟 방향 azimuth로 수렴한다 — 타겟 위치를 여기서 직접 다룰 필요가 없다.
    /// </summary>
    public class LockOnCameraDriver : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private LockOnSystem lockOn;
        [Tooltip("CM_Player의 OrbitalFollow. 락온 중 수평/수직 recentering을 켠다.")]
        [SerializeField] private CinemachineOrbitalFollow orbital;
        [Tooltip("CM_Player의 카메라 입력 컨트롤러. 락온 중 우스틱 카메라 조작 차단(엘든링식).")]
        [SerializeField] private CinemachineInputAxisController cameraInput;

        [Header("튜닝값")]
        [Tooltip("락온 중 카메라가 타겟 방향 azimuth에 도달하는 시간(s). 작을수록 빳빳하게 추종.")]
        [SerializeField] private float recenterTime = 0.75f;

        private void Awake()
        {
            if (lockOn == null) lockOn = GetComponent<LockOnSystem>();
            // 시작은 항상 free-look. 씬 저장 상태와 무관하게 recentering off로 정규화.
            if (orbital != null) SetRecentering(false);
        }

        private void OnEnable()
        {
            if (lockOn != null) lockOn.OnTargetChanged += Apply;
        }

        private void OnDisable()
        {
            if (lockOn != null) lockOn.OnTargetChanged -= Apply;
        }

        private void Apply(CombatActor target)
        {
            bool locked = target != null;

            // 락온 중 우스틱 카메라 조작 차단. recentering은 OrbitalFollow가 자체 구동하므로 이 컴포넌트를
            // 꺼도 죽지 않는다 — 오히려 입력이 살아 있으면 축 Value 변화 감지(TrackValueChange)가
            // recentering을 매 프레임 취소하므로 반드시 꺼야 한다.
            if (cameraInput != null) cameraInput.enabled = !locked;

            if (orbital != null) SetRecentering(locked);
        }

        private void SetRecentering(bool on)
        {
            // Wait=0 = "입력 멈춘 지 0초 후" = 매 프레임 즉시 추종. 씬에 저장된 Wait/Time은 무시하고
            // 드라이버가 단일 출처로 덮어쓴다(튜닝값 recenterTime 한 곳만 만지면 되게).
            var s = new InputAxis.RecenteringSettings { Enabled = on, Wait = 0f, Time = recenterTime };
            orbital.HorizontalAxis.Recentering = s;
            // 수직(피치)도 함께 — 극단 앵글로 올려보다 락온해도 씬 Center(17.5°)로 정규화돼 프레이밍이
            // 일관된다. 같은 Time을 줘 수평/수직이 동시에 도달, 호가 자연스럽다.
            orbital.VerticalAxis.Recentering = s;

            if (!on)
            {
                // 잔류 recentering 속도/상태 정리. Value는 건드리지 않는다 — 그래서 해제 순간
                // 카메라가 보던 시점이 그대로 유지된다(옛 각도 복귀 없음).
                orbital.HorizontalAxis.CancelRecentering();
                orbital.VerticalAxis.CancelRecentering();
            }
        }
    }
}
