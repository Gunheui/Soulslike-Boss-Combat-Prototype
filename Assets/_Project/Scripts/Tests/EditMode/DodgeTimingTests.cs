using NUnit.Framework;
using Project.Player;

namespace Project.Tests.EditMode
{
    /// <summary>
    /// 회피 무적창(i-frame) 타이밍이 명세대로인지 못 박는다 — 구르기/백스텝 2개 프리셋 분리.
    ///
    /// 왜 순수 struct를 따로 떼어 테스트하나 = i-frame 윈도우 경계(언제 켜지고 언제 꺼지는가)는
    /// 버그가 가장 잘 숨는 곳이고, 어긋나면 "무적 누수" 또는 "무적이 안 켜짐"이 된다. Unity 런타임
    /// 없이 <see cref="DodgeTiming"/>의 시간→판정을 직접 먹여 경계를 고정한다.
    ///
    /// 클립 30fps 기준:
    /// - 구르기(Roll, 0.53s): startup 0.05s / i-frame end 0.45s(지속 0.40s) / recovery 0.08s / total 0.53s.
    /// - 백스텝(Backstep, 0.5s): startup 0.05s / i-frame end 0.15s(지속 0.10s) / recovery 0.35s / total 0.5s.
    /// </summary>
    public class DodgeTimingTests
    {
        private static DodgeTiming Roll => DodgeTiming.Roll;
        private static DodgeTiming Backstep => DodgeTiming.Backstep;

        // ── 구르기(Roll) 무적창 경계 ──────────────────────────────────────

        [Test]
        public void Roll_NotInvulnerable_BeforeStartup()
        {
            Assert.IsFalse(Roll.IsInvulnerable(0f),     "t=0 무적이면 안 됨");
            Assert.IsFalse(Roll.IsInvulnerable(0.04f),  "startup(0.05s) 전엔 무적 아님");
        }

        [Test]
        public void Roll_Invulnerable_DuringWindow()
        {
            Assert.IsTrue(Roll.IsInvulnerable(0.05f), "startup(0.05s) 시점부터 무적");
            Assert.IsTrue(Roll.IsInvulnerable(0.25f), "윈도우 중앙 무적");
            Assert.IsTrue(Roll.IsInvulnerable(0.44f), "i-frame end(0.45s) 직전까지 무적");
        }

        [Test]
        public void Roll_NotInvulnerable_AfterWindow()
        {
            Assert.IsFalse(Roll.IsInvulnerable(0.45f), "i-frame end(0.45s)에 무적 해제");
            Assert.IsFalse(Roll.IsInvulnerable(0.8f),  "후딜 구간엔 무적 아님");
        }

        [Test]
        public void Roll_InvulnerableDuration_Is040s()
        {
            float duration = Roll.IFrameEndSeconds - Roll.StartupSeconds;
            Assert.AreEqual(0.40f, duration, 1e-5f, "구르기 무적 지속은 0.40s");
        }

        [Test]
        public void Roll_Complete_AtTotal()
        {
            Assert.IsFalse(Roll.IsComplete(0.52f), "0.53s 전엔 미완료");
            Assert.IsTrue(Roll.IsComplete(0.53f),  "total(0.53s)에 완료");
            Assert.AreEqual(0.53f, Roll.TotalSeconds, 1e-5f, "구르기 총 길이는 0.53s(무적 0.45 + 후딜 0.08)");
        }

        // ── 백스텝(Backstep) 무적창 경계 ──────────────────────────────────

        [Test]
        public void Backstep_NotInvulnerable_BeforeStartup()
        {
            Assert.IsFalse(Backstep.IsInvulnerable(0f),    "t=0 무적이면 안 됨");
            Assert.IsFalse(Backstep.IsInvulnerable(0.04f), "startup(0.05s) 전엔 무적 아님");
        }

        [Test]
        public void Backstep_Invulnerable_DuringWindow()
        {
            Assert.IsTrue(Backstep.IsInvulnerable(0.05f), "startup(0.05s) 시점부터 무적");
            Assert.IsTrue(Backstep.IsInvulnerable(0.10f), "윈도우 중앙 무적");
            Assert.IsTrue(Backstep.IsInvulnerable(0.14f), "i-frame end(0.15s) 직전까지 무적");
        }

        [Test]
        public void Backstep_NotInvulnerable_AfterWindow()
        {
            Assert.IsFalse(Backstep.IsInvulnerable(0.15f), "i-frame end(0.15s)에 무적 해제");
            Assert.IsFalse(Backstep.IsInvulnerable(0.4f),  "후딜 구간엔 무적 아님");
        }

        [Test]
        public void Backstep_InvulnerableDuration_Is010s()
        {
            float duration = Backstep.IFrameEndSeconds - Backstep.StartupSeconds;
            Assert.AreEqual(0.10f, duration, 1e-5f, "백스텝 무적 지속은 0.10s");
        }

        [Test]
        public void Backstep_Complete_AtTotal()
        {
            Assert.IsFalse(Backstep.IsComplete(0.49f), "0.5s 전엔 미완료");
            Assert.IsTrue(Backstep.IsComplete(0.5f),   "total(0.5s)에 완료");
            Assert.AreEqual(0.5f, Backstep.TotalSeconds, 1e-5f, "백스텝 총 길이는 0.5s");
        }

        // ── 이동 프로필(ease-out·거리 보존) — 두 프리셋 ──────────────────

        [Test]
        public void RollDefault_DerivesSpeedFromDistance()
        {
            var cfg = DodgeConfig.RollDefault;
            Assert.AreEqual(3.5f, cfg.distance, 1e-5f, "구르기 거리 3.5m");
            Assert.Greater(cfg.MoveSpeed, 0f, "평균 속도는 양수");
            Assert.IsTrue(cfg.IsMoving(0f), "시작 시 이동 중");
            Assert.IsFalse(cfg.IsMoving(cfg.moveDuration + 0.01f), "구동시간 이후엔 정지(후딜)");
        }

        [Test]
        public void BackstepDefault_DerivesSpeedFromDistance()
        {
            var cfg = DodgeConfig.BackstepDefault;
            Assert.AreEqual(2.0f, cfg.distance, 1e-5f, "백스텝 거리 2.0m");
            Assert.Greater(cfg.MoveSpeed, 0f, "평균 속도는 양수");
            Assert.IsTrue(cfg.IsMoving(0f), "시작 시 이동 중");
            Assert.IsFalse(cfg.IsMoving(cfg.moveDuration + 0.01f), "구동시간 이후엔 정지(후딜)");
        }

        [Test]
        public void EaseOut_StartsFast_EndsZero([Values("roll", "backstep")] string which)
        {
            var cfg = which == "roll" ? DodgeConfig.RollDefault : DodgeConfig.BackstepDefault;
            Assert.AreEqual(cfg.PeakSpeed, cfg.SpeedAt(0f), 1e-4f, "t=0은 peak 속도");
            Assert.AreEqual(2f * cfg.MoveSpeed, cfg.PeakSpeed, 1e-4f, "peak = 평균×2");
            Assert.AreEqual(0f, cfg.SpeedAt(cfg.moveDuration), 1e-4f, "구동 끝에서 v=0 (절벽 없음)");
            Assert.Greater(cfg.SpeedAt(0f), cfg.SpeedAt(cfg.moveDuration * 0.5f), "단조 감소");
        }

        [Test]
        public void EaseOut_IntegralEqualsDistance([Values("roll", "backstep")] string which)
        {
            // 속도 프로필 적분(=총 이동거리)이 distance와 일치 — ease-out이 거리를 보존.
            // 선형 감속이라 중점합이 정확(좌측합은 과대평가로 거짓 실패).
            var cfg = which == "roll" ? DodgeConfig.RollDefault : DodgeConfig.BackstepDefault;
            float dt = 1f / 600f;
            float dist = 0f;
            for (float t = 0f; t < cfg.moveDuration; t += dt)
                dist += cfg.SpeedAt(t + dt * 0.5f) * dt;
            Assert.AreEqual(cfg.distance, dist, 1e-3f, "ease-out 적분 거리 ≈ distance");
        }
    }
}
