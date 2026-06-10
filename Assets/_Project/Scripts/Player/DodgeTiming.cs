using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 회피(Dodge)의 <b>순수 타이밍</b> 정의. Unity 런타임에 의존하지 않는 값/함수만 담는다 —
    /// 그래야 i-frame 윈도우 계산(버그가 가장 잘 숨는 곳)을 EditMode 단위테스트로 격리 검증할 수 있다
    /// (M1-A가 ChangeState 순서를 EditMode로 검증한 것과 같은 전략).
    ///
    /// 단위는 <b>초(seconds)</b>다. 표는 60fps 프레임으로 적혀 있지만(feel-params B), 실제 판정은
    /// <c>Time.deltaTime</c> 누적 실시간 델타로 한다 — 프레임 드랍이 나도 무적 길이가 변하지 않는다.
    ///
    /// 타임라인(@60fps): f0 입력 → f3 i-frame ON → f14 i-frame OFF(지속 11f) → f23 모션종료.
    /// </summary>
    [System.Serializable]
    public struct DodgeTiming
    {
        [Tooltip("입력 후 무적이 켜지기까지 지연(초). 즉시 무적이 아니라 약간 늦는 게 손맛. @60fps 3f=0.05s")]
        [SerializeField] private float startupSeconds;

        [Tooltip("무적이 꺼지는 시각(초, 입력 기준). @60fps 14f=0.233s → 무적 지속 11f")]
        [SerializeField] private float iFrameEndSeconds;

        [Tooltip("무적 종료 후 입력이 잠기는 후딜(초). 회피의 기회비용. @60fps 9f=0.15s")]
        [SerializeField] private float recoverySeconds;

        public float StartupSeconds => startupSeconds;
        public float IFrameEndSeconds => iFrameEndSeconds;
        public float RecoverySeconds => recoverySeconds;

        /// <summary>회피 모션 총 길이(초). 무적 종료 + 후딜. 이 시각에 Idle/Move로 탈출.</summary>
        public float TotalSeconds => iFrameEndSeconds + recoverySeconds;

        /// <summary>입력 후 경과 t(초)에 무적인가. [startup, iFrameEnd) 반열린 구간.</summary>
        public bool IsInvulnerable(float t) => t >= startupSeconds && t < iFrameEndSeconds;

        /// <summary>회피 모션이 끝났는가(이 시점에 다음 상태로 전이).</summary>
        public bool IsComplete(float t) => t >= TotalSeconds;

        /// <summary>feel-params B 출발값(@60fps): startup 3f / iFrameEnd 14f / recovery 9f.</summary>
        public static DodgeTiming Default60fps => new DodgeTiming
        {
            startupSeconds   = 3f / 60f,   // 0.050s
            iFrameEndSeconds = 14f / 60f,  // 0.233s (무적 11f 지속)
            recoverySeconds  = 9f / 60f,   // 0.150s
        };
    }

    /// <summary>
    /// 회피 1회의 전체 튜닝값 묶음. <see cref="DodgeTiming"/>(무적/후딜 타이밍) + 이동(거리·구동시간) + 회전 정책.
    /// PlayerStateMachine이 Inspector로 노출하고 <see cref="DodgeState"/>에 주입한다.
    /// </summary>
    [System.Serializable]
    public struct DodgeConfig
    {
        public DodgeTiming timing;

        [Tooltip("회피 총 이동 거리(m). feel-params B = 3.5m")]
        public float distance;

        [Tooltip("이동이 실제로 일어나는 구간 길이(초). 보통 무적 종료(iFrameEnd)와 비슷. 이후 후딜은 정지.")]
        public float moveDuration;

        /// <summary>거리/구동시간으로 환산한 평균 속도(m/s). ease-out의 평균.</summary>
        public readonly float MoveSpeed => moveDuration > 0.0001f ? distance / moveDuration : 0f;

        /// <summary>
        /// ease-out 선형 감속의 최고 속도(t=0). 삼각형 속도 프로필 면적이 정확히 distance가 되도록
        /// peak = 2·평균 = 2·distance/moveDuration.
        /// </summary>
        public readonly float PeakSpeed => 2f * MoveSpeed;

        /// <summary>
        /// 경과 t(초)의 회피 속도(m/s). <b>ease-out</b>: 시작이 빠르고 구동 끝에서 0으로 부드럽게 감속.
        /// 등속+하드스톱의 "절벽"(카메라 댐핑 착시로 꿀렁임)을 제거한다. 면적=distance 유지(3.5m 보존).
        /// </summary>
        public readonly float SpeedAt(float t)
        {
            if (moveDuration <= 0.0001f) return 0f;
            float p = Mathf.Clamp01(t / moveDuration);   // 0→1 진행도
            return PeakSpeed * (1f - p);                  // 선형 감속, t=moveDuration에서 v=0
        }

        /// <summary>이동이 아직 진행 중인 경과시간인가(후딜 진입 전).</summary>
        public readonly bool IsMoving(float t) => t < moveDuration;

        public static DodgeConfig Default => new DodgeConfig
        {
            timing       = DodgeTiming.Default60fps,
            distance     = 3.5f,
            moveDuration = 14f / 60f, // 무적 종료까지 이동, 후딜 9f는 정지
        };
    }
}
