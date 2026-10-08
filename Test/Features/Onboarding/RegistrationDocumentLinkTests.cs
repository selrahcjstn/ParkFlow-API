using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;
using Xunit;

namespace Test.Features.Onboarding;

public class RegistrationDocumentLinkTests
{
    private static readonly string FileId = "12345678-1234-1234-1234-123456789abc";

    [Theory]
    [InlineData("uploads/parkflow/cor/", ".pdf")]
    [InlineData("/uploads/parkflow/cor/", ".png")]
    [InlineData("uploads/parkflow/orcr/", ".jpeg")]
    [InlineData("uploads/parkflow/motor-pictures/", ".webp")]
    public void ServerStorageFallbackLinksCanFinalizeRegistration(string folder, string extension)
    {
        var link = folder + FileId + extension;
        var result = new UpdateOnboardingCorValidator().Validate(new UpdateOnboardingCorCommand(Guid.NewGuid(), "2026-2027", link, link, link));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("uploads/parkflow/cor/../../secret.pdf")]
    [InlineData("uploads/parkflow/cor/not-generated.pdf")]
    [InlineData("uploads/other/12345678-1234-1234-1234-123456789abc.pdf")]
    [InlineData("uploads/parkflow/cor/12345678-1234-1234-1234-123456789abc.exe")]
    [InlineData("not-an-upload")]
    [InlineData("[object Object]")]
    public void ArbitraryRelativePathsRemainInvalid(string link)
    {
        var result = new UpdateOnboardingCorValidator().Validate(new UpdateOnboardingCorCommand(Guid.NewGuid(), "2026-2027", link, link, link));
        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void CloudinaryAbsoluteLinksRemainValid()
    {
        const string link = "https://res.cloudinary.com/test/raw/upload/v123/parkflow/cor/document.pdf";
        Assert.True(new UpdateOnboardingCorValidator().Validate(new UpdateOnboardingCorCommand(Guid.NewGuid(), "2026-2027", link)).IsValid);
    }
}
