using NUnit.Framework;

namespace Project.Tests.EditMode
{
    // M0-C: 테스트 하네스가 살아있음을 증명하는 스모크 테스트.
    // 보스/PG 로직을 검증하는 게 아니라 "테스트 파이프라인 자체"를 검증한다.
    //  - Project.Tests asmdef 가 EditMode 탭에 노출되는가
    //  - NUnit 참조(overrideReferences + nunit.framework.dll)가 풀리는가
    //  - UNITY_INCLUDE_TESTS 제약 하에서 그린이 뜨는가
    // 이게 그린이면 M1+ 부터는 이 폴더에 테스트 파일을 "추가만" 하면 된다(셋업 비용 0).
    public class HarnessSmokeTest
    {
        [Test]
        public void Harness_IsAlive()
        {
            Assert.Pass("EditMode 테스트 어셈블리 그린.");
        }
    }
}
