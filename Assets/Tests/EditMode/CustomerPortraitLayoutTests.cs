using NUnit.Framework;

public sealed class CustomerPortraitLayoutTests
{
    [Test]
    public void ChildUsesAdultHeightForScaleAndRise()
    {
        CustomerPortraitLayout layout = CustomerPortraitLayout.Calculate(CustomerAttributes.Male | CustomerAttributes.Child, 430f);

        Assert.That(layout.DisplayScale, Is.EqualTo(.6f).Within(.0001f));
        Assert.That(layout.DisplayHeight, Is.EqualTo(258f).Within(.0001f));
        Assert.That(layout.RisePixels, Is.EqualTo(-7.530909f).Within(.0001f));
    }

    [Test]
    public void EnlargedChildSinksEightPixelsBelowPreviousBottomEdge()
    {
        const float adultHeight = 430f;
        const float bottomCover = 12f;
        float previousHeight = adultHeight * .4f;
        float previousBottom = -bottomCover - 3f * previousHeight / 550f;
        CustomerPortraitLayout layout = CustomerPortraitLayout.Calculate(CustomerAttributes.Male | CustomerAttributes.Child, adultHeight);
        float enlargedBottom = -bottomCover - 3f * layout.DisplayHeight / 550f + layout.RisePixels;

        Assert.That(enlargedBottom, Is.EqualTo(previousBottom - 8f).Within(.0001f));
    }

    [Test]
    public void AdultAndElderlyKeepAdultHeightAndNoRise()
    {
        CustomerPortraitLayout adult = CustomerPortraitLayout.Calculate(CustomerAttributes.Female | CustomerAttributes.Adult, 430f);
        CustomerPortraitLayout elderly = CustomerPortraitLayout.Calculate(CustomerAttributes.Male | CustomerAttributes.Elderly, 430f);

        Assert.That(adult.DisplayScale, Is.EqualTo(1f));
        Assert.That(adult.DisplayHeight, Is.EqualTo(430f));
        Assert.That(adult.RisePixels, Is.Zero);
        Assert.That(elderly.DisplayScale, Is.EqualTo(1f));
        Assert.That(elderly.DisplayHeight, Is.EqualTo(430f));
        Assert.That(elderly.RisePixels, Is.Zero);
    }

    [Test]
    public void GenderDoesNotChangeLayout()
    {
        CustomerPortraitLayout male = CustomerPortraitLayout.Calculate(CustomerAttributes.Male | CustomerAttributes.Child, 430f);
        CustomerPortraitLayout female = CustomerPortraitLayout.Calculate(CustomerAttributes.Female | CustomerAttributes.Child, 430f);

        Assert.That(female.DisplayScale, Is.EqualTo(male.DisplayScale));
        Assert.That(female.DisplayHeight, Is.EqualTo(male.DisplayHeight));
        Assert.That(female.RisePixels, Is.EqualTo(male.RisePixels));
    }

    [Test]
    public void NormalFlagAloneDoesNotMakePortraitChild()
    {
        CustomerPortraitLayout layout = CustomerPortraitLayout.Calculate(CustomerAttributes.Normal, 430f);

        Assert.That(layout.DisplayScale, Is.EqualTo(1f));
        Assert.That(layout.DisplayHeight, Is.EqualTo(430f));
        Assert.That(layout.RisePixels, Is.Zero);
    }

    [Test]
    public void InvalidParametersAreRejected()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => CustomerPortraitLayout.Calculate(CustomerAttributes.Child, 0f));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => CustomerPortraitLayout.Calculate(CustomerAttributes.Child, 430f, .14f));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => CustomerPortraitLayout.Calculate(CustomerAttributes.Child, 430f, .4f, .61f));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => CustomerPortraitLayout.Calculate(CustomerAttributes.Child, 430f, float.NaN));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => CustomerPortraitLayout.Calculate(CustomerAttributes.Child, 430f, .4f, float.PositiveInfinity));
    }
}
