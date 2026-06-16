using System;
using NUnit.Framework;
using UnityEngine;
using Project.Combat;

namespace Project.Tests.EditMode
{
    /// <summary>
    /// M2-A / T2.1·T2.2 DoD: 데미지 파이프라인 계약을 검증 — DamageInfo 13필드·DamageResult 5필드·enum 값 집합 회귀 잠금,
    /// 그리고 info(입력)→Resolver→result(출력)→Damageable의 단방향 흐름을 더미 구현으로 실증한다.
    /// </summary>
    public class DamageContractTests
    {
        // 계약을 실제로 구현해 보는 최소 더미 — "인터페이스가 끼워지는가"의 증거.
        // 공격을 무조건 Hit으로 처리하는 스텁(M2-C 허수아비 스파이크가 쓸 것과 같은 결).
        private sealed class AlwaysHitResolver : IDamageResolver
        {
            public DamageResult Resolve(in DamageInfo info) => new DamageResult
            {
                outcome = DamageOutcome.Hit,
                finalDamage = info.amount,   // 입력의 amount를 그대로 실데미지로
                chipDamage = 0f,
                recoverableAdded = 0f,
                staggeredTarget = false
            };
        }

        // 결과를 체력에 적용하는 더미 — IDamageable 계약 구현 증거.
        private sealed class DummyHealth : IDamageable
        {
            private float _hp;
            public DamageResult LastApplied;
            public DummyHealth(float hp) => _hp = hp;
            public void ApplyDamage(in DamageResult result)
            {
                LastApplied = result;
                _hp -= result.finalDamage;
            }
            public bool IsDead => _hp <= 0f;
        }

        [Test]
        public void DamageInfo_CarriesAll13Fields()
        {
            // 13필드를 전부 구분되는 값으로 채워 "한 타격 스냅샷"이 정보를 잃지 않음을 못 박는다.
            var src = new GameObject("Attacker");
            try
            {
                var info = new DamageInfo
                {
                    amount = 18f,
                    poiseDamage = 10f,
                    staggerDamage = 8f,
                    type = DamageType.Normal,
                    isPerfectGuardable = true,
                    sourcePos = new Vector3(1f, 2f, 3f),
                    source = src,
                    sourceTeam = Team.Player,
                    knockback = 1.5f,
                    hitstunFrames = 12f,
                    dotPerSec = 3f,
                    dotDuration = 3f,
                    isCritical = false
                };

                Assert.AreEqual(18f, info.amount);
                Assert.AreEqual(10f, info.poiseDamage);
                Assert.AreEqual(8f, info.staggerDamage);
                Assert.AreEqual(DamageType.Normal, info.type);
                Assert.IsTrue(info.isPerfectGuardable);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), info.sourcePos);
                Assert.AreSame(src, info.source);
                Assert.AreEqual(Team.Player, info.sourceTeam);
                Assert.AreEqual(1.5f, info.knockback);
                Assert.AreEqual(12f, info.hitstunFrames);
                Assert.AreEqual(3f, info.dotPerSec);
                Assert.AreEqual(3f, info.dotDuration);
                Assert.IsFalse(info.isCritical);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(src);
            }
        }

        [Test]
        public void DamageResult_CarriesAll5Fields()
        {
            var result = new DamageResult
            {
                outcome = DamageOutcome.Blocked,
                finalDamage = 0f,
                chipDamage = 3.24f,
                recoverableAdded = 3.24f,
                staggeredTarget = true
            };

            Assert.AreEqual(DamageOutcome.Blocked, result.outcome);
            Assert.AreEqual(0f, result.finalDamage);
            Assert.AreEqual(3.24f, result.chipDamage);
            Assert.AreEqual(3.24f, result.recoverableAdded);
            Assert.IsTrue(result.staggeredTarget);
        }

        [Test]
        public void DamageType_HasExactlyThreeKinds()
        {
            // enum 값 집합 회귀 잠금 — 분기 키라 함부로 늘면 Resolver 분기가 깨진다.
            Assert.AreEqual(3, Enum.GetValues(typeof(DamageType)).Length);
            Assert.IsTrue(Enum.IsDefined(typeof(DamageType), DamageType.Normal));
            Assert.IsTrue(Enum.IsDefined(typeof(DamageType), DamageType.Unblockable));
            Assert.IsTrue(Enum.IsDefined(typeof(DamageType), DamageType.Grab));
        }

        [Test]
        public void DamageOutcome_HasExactlySevenKinds()
        {
            Assert.AreEqual(7, Enum.GetValues(typeof(DamageOutcome)).Length);
        }

        [Test]
        public void Contract_Info_FlowsThroughResolver_IntoDamageable()
        {
            // 단방향 계약 실증: 공격자 info → Resolver.Resolve → result → Damageable.ApplyDamage.
            IDamageResolver resolver = new AlwaysHitResolver();
            var health = new DummyHealth(hp: 100f);

            var info = new DamageInfo { amount = 40f, type = DamageType.Normal };
            DamageResult result = resolver.Resolve(in info);
            health.ApplyDamage(in result);

            // 입력의 데미지가 결과를 거쳐 체력까지 한 방향으로 도달했는가.
            Assert.AreEqual(DamageOutcome.Hit, result.outcome);
            Assert.AreEqual(40f, result.finalDamage);
            Assert.AreEqual(40f, health.LastApplied.finalDamage);
            Assert.IsFalse(health.IsDead, "100 - 40 = 60, 아직 살아있어야 한다.");

            // 방어자는 info를 수정하지 않는다(값타입 + in 파라미터). 원본 불변 확인.
            Assert.AreEqual(40f, info.amount);
        }

        [Test]
        public void Contract_LethalDamage_FlagsDead()
        {
            IDamageResolver resolver = new AlwaysHitResolver();
            var health = new DummyHealth(hp: 30f);

            var info = new DamageInfo { amount = 40f };
            health.ApplyDamage(resolver.Resolve(in info));

            Assert.IsTrue(health.IsDead, "30 - 40 <= 0 이면 사망 플래그가 서야 한다.");
        }
    }
}
