using System.Collections.Generic;
using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 락온 타겟 선택의 <b>순수 판정 로직</b>. Unity 객체(Camera/Transform/CombatActor)를 일절 만지지 않고
    /// float 배열만 받아 "어느 후보를 고를지"를 인덱스로 답한다.
    ///
    /// 왜 actor가 아니라 인덱스인가 = CombatActor는 MonoBehaviour라 테스트에서 <c>new</c>로 못 만든다.
    /// 그래서 거리/각도/화면X 같은 <i>측정값 배열</i>만 입력받아 선택 인덱스를 돌려주면,
    /// <see cref="LockOnSystem"/>(글루)이 그 인덱스로 실제 후보를 집는다.
    /// 덕분에 선택 규칙(범위 게이트·최근접 각도·좌우 전환)을 EditMode에서 카메라 없이 검증할 수 있다
    /// (DodgeTiming struct와 같은 분리 전략).
    /// </summary>
    public static class LockOnTargeting
    {
        /// <summary>
        /// 최초 획득 시 후보 중 하나를 고른다. 유효 후보 = 범위 안(distance ≤ lockRange) AND
        /// 카메라 정면 각도 안(angle ≤ maxAngle, 화면 밖·등 뒤 배제). 그중 <b>가장 중앙(최소 각도)</b>을 택한다.
        /// 거리는 "탈 수 있는가"의 게이트, 각도는 "어느 걸 보고 있나"의 선택 기준 — 소울라이크 관례.
        /// </summary>
        /// <returns>선택된 후보 인덱스. 유효 후보 없으면 -1.</returns>
        public static int SelectInitial(IReadOnlyList<float> distances, IReadOnlyList<float> angles,
                                        float lockRange, float maxAngle)
        {
            int best = -1;
            float bestAngle = float.MaxValue;
            for (int i = 0; i < distances.Count; i++)
            {
                if (distances[i] > lockRange) continue;   // 범위 밖
                if (angles[i] > maxAngle) continue;        // 시야 밖
                if (angles[i] < bestAngle)
                {
                    bestAngle = angles[i];
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 락온 중 좌우 타겟 전환. 후보들의 화면X(viewport x) 위치를 기준으로 현재 타겟의
        /// <paramref name="direction"/> 방향(양수=오른쪽/음수=왼쪽)에서 <b>가장 가까운</b> 후보를 고른다.
        /// 그 방향에 후보가 없으면 -1(호출측은 현재 타겟 유지 — wrap-around 없음).
        /// </summary>
        public static int SelectSwitch(IReadOnlyList<float> screenX, int currentIndex, float direction)
        {
            if (currentIndex < 0 || currentIndex >= screenX.Count) return -1;

            float cur = screenX[currentIndex];
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < screenX.Count; i++)
            {
                if (i == currentIndex) continue;
                float delta = screenX[i] - cur;
                bool onSide = (direction > 0f && delta > 0f) || (direction < 0f && delta < 0f);
                if (!onSide) continue;

                float adist = Mathf.Abs(delta);
                if (adist < bestDist)
                {
                    bestDist = adist;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>현재 타겟이 아직 유효한가(범위 안). 매 프레임 자동 해제 판정에 사용.</summary>
        public static bool IsTargetValid(float distance, float lockRange) => distance <= lockRange;
    }
}
