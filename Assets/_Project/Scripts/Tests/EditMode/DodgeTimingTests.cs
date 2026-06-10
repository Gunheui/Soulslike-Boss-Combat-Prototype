using NUnit.Framework;
using Project.Player;

namespace Project.Tests.EditMode
{
    /// <summary>
    /// M1-D / T1.5 DoD: 회피 무적창(i-frame) 타이밍이 feel-params B 명세대로인지 못 박는다.
    ///
    /// 왜 순수 struct를 따로 떼어 테스트하나 = i-frame 윈도우 경계(언제 켜지고 언제 꺼지는가)는
    /// 버그가 가장 잘 숨는 곳이고, 어긋나면 "무적 누수" 또는 "무적이 안 켜짐"이 된다. Unity 런타임
    /// 없이 <see cref="DodgeTiming"/>의 시간→판정을 직접 먹여 경계를 고정한다(M1-A ChangeState
    /// 순서 테스트와 같은 전략 — 핵심 로직을 순수 함수로 격리해 빠르게 회귀 검증).
    ///
    /// @60fps 기준: startup 3f / i-frame end 14f(지속 11f) / recovery 9f / total 23f.
    /// </summary>
    public class DodgeTimingTests
    {
        private const float F = 1f / 60f; // 1프레임(초)

        private static DodgeTiming Timing => DodgeTiming.Default60fps;

        [Test]
        public void NotInvulnerable_BeforeStartup()
        {
            // 입력 직후 ~ 3f 전: 아직 무적 아님(즉시 무적이 아니라 약간 늦는 게 손맛).
            Assert.IsFalse(Timing.IsInvulnerable(0f),         "t=0 무적이면 안 됨");
            Assert.IsFalse(Timing.IsInvulnerable(2.5f * F),   "startup(3f) 전엔 무적 아님");
        }

        [Test]
        public void Invulnerable_DuringWindow()
        {
            // [3f, 14f) 구간은 무적.
            Assert.IsTrue(Timing.IsInvulnerable(3f * F),  "startup(3f) 시점부터 무적");
            Assert.IsTrue(Timing.IsInvulnerable(8f * F),  "윈도우 중앙 무적");
            Assert.IsTrue(Timing.IsInvulnerable(13.9f * F), "i-frame end(14f) 직전까지 무적");
        }

        [Test]
        public void NotInvulnerable_AfterWindow()
        {
            // 14f 이후(후딜 포함)는 무적 해제 — 여기서 누수되면 영구 무적 버그.
            Assert.IsFalse(Timing.IsInvulnerable(14f * F), "i-frame end(14f)에 무적 해제");
            Assert.IsFalse(Timing.IsInvulnerable(20f * F), "후딜 구간엔 무적 아님");
        }

        [Test]
        public void InvulnerableDuration_Is11Frames()
        {
            // 무적 지속 = end - startup = 14f - 3f = 11f (feel-params B).
            float duration = Timing.IFrameEndSeconds - Timing.StartupSeconds;
            Assert.AreEqual(11f * F, duration, 1e-5f, "무적 지속은 11f여야 함");
        }

        [Test]
        public void Complete_AtTotal()
        {
            // total = iFrameEnd(14f) + recovery(9f) = 23f. 이 시점에 다음 상태로 탈출.
            Assert.IsFalse(Timing.IsComplete(22f * F), "23f 전엔 미완료");
            Assert.IsTrue(Timing.IsComplete(23f * F),  "total(23f)에 완료");
            Assert.AreEqual(23f * F, Timing.TotalSeconds, 1e-5f, "총 길이는 23f");
        }

        [Test]
        public void DodgeConfig_Default_DerivesSpeedFromDistance()
        {
            // 거리 3.5m / 구동시간으로 환산한 평균이 양수여야 함(0이면 회피가 안 움직임).
            var cfg = DodgeConfig.Default;
            Assert.AreEqual(3.5f, cfg.distance, 1e-5f, "기본 회피 거리 3.5m");
            Assert.Greater(cfg.MoveSpeed, 0f, "평균 속도는 양수");
            Assert.IsTrue(cfg.IsMoving(0f),  "시작 시 이동 중");
            Assert.IsFalse(cfg.IsMoving(cfg.moveDuration + F), "구동시간 이후엔 정지(후딜)");
        }

        [Test]
        public void DodgeConfig_EaseOut_StartsFast_EndsZero()
        {
            // ease-out: t=0은 peak(평균의 2배), 구동 끝에서 0으로 부드럽게 감속(하드스톱 제거).
            var cfg = DodgeConfig.Default;
            Assert.AreEqual(cfg.PeakSpeed, cfg.SpeedAt(0f), 1e-4f, "t=0은 peak 속도");
            Assert.AreEqual(2f * cfg.MoveSpeed, cfg.PeakSpeed, 1e-4f, "peak = 평균×2");
            Assert.AreEqual(0f, cfg.SpeedAt(cfg.moveDuration), 1e-4f, "구동 끝에서 v=0 (절벽 없음)");
            Assert.Greater(cfg.SpeedAt(0f), cfg.SpeedAt(cfg.moveDuration * 0.5f), "단조 감소");
        }

        [Test]
        public void DodgeConfig_EaseOut_IntegralEqualsDistance()
        {
            // 속도 프로필 적분(=총 이동거리)이 distance(3.5m)와 일치해야 함 — ease-out이 거리를 보존.
            var cfg = DodgeConfig.Default;
            float dt = 1f / 600f; // 미세 스텝으로 수치적분
            float dist = 0f;
            for (float t = 0f; t < cfg.moveDuration; t += dt)
                dist += cfg.SpeedAt(t) * dt;
            Assert.AreEqual(3.5f, dist, 0.02f, "ease-out 적분 거리 ≈ 3.5m");
        }
    }
}
