using System.Collections.Generic;
using UnityEngine;
using Project.Combat;

namespace Project.Player
{
    /// <summary>
    /// 락온(타겟 고정) 시스템. 적 <see cref="CombatActor"/>를 가리키고, 범위를 벗어나면 자동 해제하며,
    /// 좌우 전환을 지원한다.
    ///
    /// <b>왜 FSM 상태가 아니라 독립 컴포넌트인가</b> = 락온은 Idle/Move/Dodge/Attack 어느 상태에서나
    /// 직교로 켜고 끈다. 상태로 만들면 모든 상태에 락온 분기가 번진다. 이 컴포넌트는 매 프레임 타겟만
    /// 검증해 <see cref="PlayerLocomotion.LockOnTarget"/>에 꽂아주고, 상태들은 그대로 둔다(무변경).
    ///
    /// <b>왜 보스가 아니라 CombatActor인가</b> = Player 어셈블리는 Boss를 import하지 않는다(단방향 의존).
    /// 보스를 Combat이 정의한 CombatActor로만 다뤄 경계를 깨지 않고 가리킨다(의존성 역전).
    ///
    /// ⚠ 프로토타입 스코프: 후보 수집은 획득/전환 시점에만 <see cref="Object.FindObjectsByType"/>로 전수
    /// 조사한다(보스 1체, 매 프레임 아님 → 비용 무시 가능). 적이 많아지면 공간 쿼리(OverlapSphere)나
    /// 등록 레지스트리로 교체할 자리 — 지금 만들지 않는다(과확장 금지).
    /// </summary>
    public class LockOnSystem : MonoBehaviour
    {
        [Header("범위 — T1.6 튜닝값")]
        [Tooltip("이 거리(m) 밖으로 멀어지면 락온 자동 해제.")]
        [SerializeField] private float lockRange = 12f;
        [Tooltip("최초 획득 시 카메라 정면에서 이 각도(°) 안의 적만 후보. 화면 밖·등 뒤 타겟 배제.")]
        [SerializeField] private float maxAcquireAngle = 70f;

        [Header("참조 (비우면 같은 GameObject에서 자동 회수)")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerLocomotion locomotion;
        [SerializeField] private CombatActor selfActor;

        private Camera _cam;

        /// <summary>현재 락온 타겟(없으면 null). 카메라 드라이버·HUD가 읽음.</summary>
        public CombatActor CurrentTarget { get; private set; }
        public bool IsLockedOn => CurrentTarget != null;

        /// <summary>타겟 변경 방송(null=해제). <see cref="PlayerCameraDriver"/>·HUD(M7)가 구독해 반응.</summary>
        public event System.Action<CombatActor> OnTargetChanged;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (locomotion == null) locomotion = GetComponent<PlayerLocomotion>();
            if (selfActor == null) selfActor = GetComponent<CombatActor>();
            _cam = Camera.main;

            if (selfActor == null)
                Debug.LogWarning("[LockOnSystem] selfActor 미배선 — 자기 자신/동팀 배제가 불완전해질 수 있다.", this);
        }

        private void Update()
        {
            if (input != null && input.ConsumeLockOn())
            {
                if (IsLockedOn) Drop();
                else Acquire();
            }

            // 매 프레임 유효성 검사 — 타겟 파괴(Unity null) 또는 범위 이탈 시 해제(DoD: 12m 밖 해제).
            if (IsLockedOn)
            {
                if (CurrentTarget == null) { Drop(); return; }
                float d = Vector3.Distance(transform.position, CurrentTarget.transform.position);
                if (!LockOnTargeting.IsTargetValid(d, lockRange)) Drop();
            }
        }

        private void Acquire()
        {
            var candidates = GatherEnemies();
            if (candidates.Count == 0) return;

            var distances = new float[candidates.Count];
            var angles = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 p = candidates[i].transform.position;
                distances[i] = Vector3.Distance(transform.position, p);
                angles[i] = AngleFromCamera(p);
            }

            int idx = LockOnTargeting.SelectInitial(distances, angles, lockRange, maxAcquireAngle);
            if (idx >= 0) SetTarget(candidates[idx]);
        }

        /// <summary>
        /// 좌우 타겟 전환(양수=오른쪽/음수=왼쪽). 락온 중일 때만 동작.
        /// 현재 보스 1체라 인게임 전환은 no-op이지만, 로직은 EditMode 테스트로 검증된다(T1.6 DoD).
        /// 전용 입력 바인딩(우스틱 플릭)은 2번째 타겟이 생기면 추가 — 지금은 외부 호출용 공개 API만.
        /// </summary>
        public void SwitchTarget(float direction)
        {
            if (!IsLockedOn) return;

            var candidates = GatherEnemies();
            int current = candidates.IndexOf(CurrentTarget);
            if (current < 0) return;

            var screenX = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
                screenX[i] = ScreenX(candidates[i].transform.position);

            int idx = LockOnTargeting.SelectSwitch(screenX, current, direction);
            if (idx >= 0) SetTarget(candidates[idx]);
        }

        private void Drop() => SetTarget(null);

        private void SetTarget(CombatActor t)
        {
            CurrentTarget = t;
            if (locomotion != null)
                locomotion.LockOnTarget = t != null ? t.transform : null;
            OnTargetChanged?.Invoke(t);
            Debug.Log(t != null ? $"[LockOn] → {t.name}" : "[LockOn] 해제");
        }

        // 적 후보 수집: 자기 자신·동팀 제외. selfActor 미배선이면 팀 필터만 생략(자기 제외는 유지).
        private List<CombatActor> GatherEnemies()
        {
            var all = FindObjectsByType<CombatActor>(FindObjectsInactive.Exclude);
            var list = new List<CombatActor>(all.Length);
            foreach (var a in all)
            {
                if (a == selfActor) continue;
                if (selfActor != null && a.Team == selfActor.Team) continue;
                list.Add(a);
            }
            return list;
        }

        // 카메라 정면과 타겟 방향 사이 각도(°). 화면 중앙에 가까울수록 작다 → 획득 우선순위.
        private float AngleFromCamera(Vector3 worldPos)
        {
            if (_cam == null) return 0f;
            return Vector3.Angle(_cam.transform.forward, worldPos - _cam.transform.position);
        }

        // 타겟의 viewport x(0=좌, 1=우). 좌우 전환 순서 판정용.
        private float ScreenX(Vector3 worldPos)
        {
            if (_cam == null) return 0f;
            return _cam.WorldToViewportPoint(worldPos).x;
        }
    }
}
