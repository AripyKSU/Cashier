using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>손님 구성 선택기의 영업일별 성별 교대와 속성 조합을 검사한다.</summary>
public sealed class CustomerCompositionSelectorTests
{
    private const CustomerAttributes GenderMask = CustomerAttributes.Male | CustomerAttributes.Female;

    /// <summary>null 난수원을 선택기에 연결하지 못하게 한다.</summary>
    [Test]
    public void RejectsNullRandom()
    {
        Assert.Throws<ArgumentNullException>(() => new CustomerCompositionSelector(null));
    }

    /// <summary>첫 손님은 seed로 재현되고 이후 성별은 영업일 동안 정확히 교대한다.</summary>
    [Test]
    public void FirstGenderIsRandomAndFollowingGendersAlternate()
    {
        var left = new CustomerCompositionSelector(new Random(73));
        var right = new CustomerCompositionSelector(new Random(73));
        CustomerAttributes previous = CustomerAttributes.None;

        for (int i = 0; i < 100; i++)
        {
            CustomerAttributes leftAttributes = left.SelectAttributes();
            CustomerAttributes rightAttributes = right.SelectAttributes();
            CustomerAttributes leftGender = leftAttributes & GenderMask;
            CustomerAttributes rightGender = rightAttributes & GenderMask;

            Assert.That(leftAttributes, Is.EqualTo(rightAttributes));
            if (previous != CustomerAttributes.None)
            {
                Assert.That(leftGender, Is.Not.EqualTo(previous));
                Assert.That(leftGender, Is.EqualTo(previous == CustomerAttributes.Male
                    ? CustomerAttributes.Female
                    : CustomerAttributes.Male));
            }

            previous = leftGender;
        }
    }

    /// <summary>새 선택기 인스턴스가 새 영업일처럼 첫 성별을 다시 무작위로 선택한다.</summary>
    [Test]
    public void NewSelectorStartsWithFreshFirstGenderState()
    {
        var firstDay = new CustomerCompositionSelector(new Random(11));
        firstDay.SelectAttributes();
        firstDay.SelectAttributes();

        var newDay = new CustomerCompositionSelector(new Random(11));
        var fresh = new CustomerCompositionSelector(new Random(11));

        Assert.That(newDay.SelectAttributes(), Is.EqualTo(fresh.SelectAttributes()));
    }

    /// <summary>모든 생성 속성에 성별·연령·현재 특수 속성이 정확히 하나씩 포함된다.</summary>
    [Test]
    public void SelectsCompleteAttributeCombinations()
    {
        var selector = new CustomerCompositionSelector(new Random(5));
        var combinations = new HashSet<CustomerAttributes>();

        for (int i = 0; i < 600; i++)
        {
            CustomerAttributes attributes = selector.SelectAttributes();
            Assert.DoesNotThrow(() => CustomerProfileValidation.ValidateCompleteAttributes(attributes));
            combinations.Add(attributes);
        }

        Assert.That(combinations, Is.EquivalentTo(new[]
        {
            CustomerAttributes.Male | CustomerAttributes.Adult | CustomerAttributes.Normal,
            CustomerAttributes.Male | CustomerAttributes.Child | CustomerAttributes.Normal,
            CustomerAttributes.Male | CustomerAttributes.Elderly | CustomerAttributes.Normal,
            CustomerAttributes.Female | CustomerAttributes.Adult | CustomerAttributes.Normal,
            CustomerAttributes.Female | CustomerAttributes.Child | CustomerAttributes.Normal,
            CustomerAttributes.Female | CustomerAttributes.Elderly | CustomerAttributes.Normal
        }));
    }
}
