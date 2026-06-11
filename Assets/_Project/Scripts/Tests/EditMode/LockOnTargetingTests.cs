using NUnit.Framework;
using Project.Player;

namespace Project.Tests.EditMode
{
    /// <summary>
    /// 락온 선택 로직(<see cref="LockOnTargeting"/>) 단위테스트. Camera·Transform 없이 측정값 배열만으로
    /// 규칙을 검증한다 — 범위 게이트·최근접 각도·좌우 전환(SwitchTarget DoD 포함).
    /// </summary>
    public class LockOnTargetingTests
    {
        // --- SelectInitial: 범위/각도 게이트 + 최소 각도 선택 ---

        [Test]
        public void SelectInitial_NoCandidates_ReturnsMinusOne()
        {
            int idx = LockOnTargeting.SelectInitial(new float[0], new float[0], 12f, 70f);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void SelectInitial_AllOutOfRange_ReturnsMinusOne()
        {
            var distances = new[] { 13f, 20f };
            var angles = new[] { 5f, 5f };
            int idx = LockOnTargeting.SelectInitial(distances, angles, 12f, 70f);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void SelectInitial_PicksMostCentered()
        {
            // 둘 다 범위·시야 안. 인덱스1이 각도 더 작음(더 중앙) → 거리(더 멀어도)와 무관하게 선택.
            var distances = new[] { 5f, 10f };
            var angles = new[] { 40f, 10f };
            int idx = LockOnTargeting.SelectInitial(distances, angles, 12f, 70f);
            Assert.AreEqual(1, idx);
        }

        [Test]
        public void SelectInitial_ExcludesOutOfAngle()
        {
            // 인덱스0은 각도 초과(시야 밖) → 제외. 인덱스1만 유효.
            var distances = new[] { 5f, 8f };
            var angles = new[] { 85f, 30f };
            int idx = LockOnTargeting.SelectInitial(distances, angles, 12f, 70f);
            Assert.AreEqual(1, idx);
        }

        // --- SelectSwitch: 화면X 기준 좌우 전환 ---

        [Test]
        public void SelectSwitch_Right_PicksNearestRight()
        {
            // 현재=인덱스1(x=0.5). 오른쪽(x>0.5)은 인덱스2(0.6)·3(0.9) → 가장 가까운 0.6 = 인덱스2.
            var screenX = new[] { 0.2f, 0.5f, 0.6f, 0.9f };
            int idx = LockOnTargeting.SelectSwitch(screenX, 1, +1f);
            Assert.AreEqual(2, idx);
        }

        [Test]
        public void SelectSwitch_Left_PicksNearestLeft()
        {
            // 현재=인덱스1(x=0.5). 왼쪽(x<0.5)은 인덱스0(0.2) → 인덱스0.
            var screenX = new[] { 0.2f, 0.5f, 0.6f, 0.9f };
            int idx = LockOnTargeting.SelectSwitch(screenX, 1, -1f);
            Assert.AreEqual(0, idx);
        }

        [Test]
        public void SelectSwitch_NoneOnSide_ReturnsMinusOne()
        {
            // 현재=인덱스2(x=0.9, 가장 오른쪽). 오른쪽엔 후보 없음 → -1(현재 유지).
            var screenX = new[] { 0.2f, 0.5f, 0.9f };
            int idx = LockOnTargeting.SelectSwitch(screenX, 2, +1f);
            Assert.AreEqual(-1, idx);
        }

        // --- IsTargetValid: 범위 경계 ---

        [Test]
        public void IsTargetValid_RangeBoundary()
        {
            Assert.IsTrue(LockOnTargeting.IsTargetValid(11.9f, 12f));
            Assert.IsTrue(LockOnTargeting.IsTargetValid(12f, 12f));     // 경계 포함
            Assert.IsFalse(LockOnTargeting.IsTargetValid(12.1f, 12f));  // 밖
        }
    }
}
