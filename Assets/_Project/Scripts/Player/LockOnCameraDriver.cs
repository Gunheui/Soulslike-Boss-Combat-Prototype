using UnityEngine;
using Unity.Cinemachine;
using Project.Combat;

namespace Project.Player
{
    /// <summary>
    /// 락온 상태를 Cinemachine 카메라로 반영하는 드라이버. <see cref="LockOnSystem.OnTargetChanged"/>를
    /// 구독해, 락온 시 플레이어+타겟을 한 화면에 담는 전용 vcam으로 전환하고 해제 시 free-look으로 되돌린다.
    ///
    /// <b>왜 LockOnSystem과 분리하나</b> = 엔진 의존(Unity.Cinemachine)을 이 드라이버 한 곳에 가둔다.
    /// LockOnSystem은 Cinemachine을 몰라 EditMode 테스트에서 자유롭고, 카메라 연출만 여기서 갈아끼운다
    /// (관심사 분리 — 상태=로직 / 드라이버=연출).
    ///
    /// <b>전환 방식</b> = 락온 vcam GameObject를 켜고/끈다. 켜진 동안 free-look vcam보다 Priority가 높게
    /// 씬에서 설정돼 있어, CinemachineBrain이 자동으로 블렌딩한다. 끄면 free-look으로 복귀.
    /// (Transform LookAt 수동 폴백은 폐기 — DoD.)
    /// </summary>
    public class LockOnCameraDriver : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private LockOnSystem lockOn;
        [Tooltip("락온 전용 CinemachineCamera. free-look vcam보다 Priority 높게 설정. 시작 시 비활성.")]
        [SerializeField] private CinemachineCamera lockVcam;
        [Tooltip("플레이어+타겟을 담는 타겟 그룹. lockVcam의 Tracking/LookAt 대상으로 씬에서 배선.")]
        [SerializeField] private CinemachineTargetGroup targetGroup;

        [Header("타겟 그룹 가중치 — 프레이밍 튜닝값")]
        [Tooltip("동적 추가되는 타겟(보스)의 그룹 내 가중치/반경. 플레이어 멤버는 씬에서 멤버0으로 미리 배선.")]
        [SerializeField] private float targetWeight = 1f;
        [SerializeField] private float targetRadius = 1.5f;

        // 그룹에 동적으로 끼운 현재 타겟 멤버 — 해제/교체 시 정확히 이것만 제거.
        private Transform _dynamicMember;

        private void Awake()
        {
            if (lockOn == null) lockOn = GetComponent<LockOnSystem>();
            // 시작은 항상 free-look(락온 vcam off). 씬 저장 상태와 무관하게 정규화.
            if (lockVcam != null) lockVcam.gameObject.SetActive(false);
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
            // 1) 이전 동적 멤버 제거(있다면). 플레이어(멤버0)는 건드리지 않는다.
            if (targetGroup != null && _dynamicMember != null)
            {
                targetGroup.RemoveMember(_dynamicMember);
                _dynamicMember = null;
            }

            // 2) 새 타겟을 그룹에 추가.
            if (target != null && targetGroup != null)
            {
                _dynamicMember = target.transform;
                targetGroup.AddMember(_dynamicMember, targetWeight, targetRadius);
            }

            // 3) 락온 vcam 토글 → Brain이 free-look ↔ 락온 cam 블렌딩.
            if (lockVcam != null)
                lockVcam.gameObject.SetActive(target != null);
        }
    }
}
