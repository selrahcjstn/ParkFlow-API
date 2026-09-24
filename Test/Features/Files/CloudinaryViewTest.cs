using ParkFlow.Infrastructure.Cloudinary;
using Xunit;

namespace Test.Features.Files;

public class CloudinaryViewTest
{
    [Theory]
    [InlineData("https://res.cloudinary.com/dp5p5kba8/raw/upload/v1790270235/parkflow/cor/wyelm3t3lwqwrbpcxcir.pdf", "parkflow/cor/wyelm3t3lwqwrbpcxcir.pdf")]
    [InlineData("https://res.cloudinary.com/dp5p5kba8/raw/upload/s--abc123--/v1790270235/parkflow/cor/wyelm3t3lwqwrbpcxcir.pdf", "parkflow/cor/wyelm3t3lwqwrbpcxcir.pdf")]
    [InlineData("https://res.cloudinary.com/dp5p5kba8/image/upload/v12345/parkflow/orcr/doc.pdf", "parkflow/orcr/doc.pdf")]
    [InlineData("parkflow/cor/wyelm3t3lwqwrbpcxcir.pdf", "parkflow/cor/wyelm3t3lwqwrbpcxcir.pdf")]
    [InlineData("uploads/parkflow/cor/sample.pdf", "parkflow/cor/sample.pdf")]
    [InlineData("/uploads/parkflow/cor/sample.pdf", "parkflow/cor/sample.pdf")]
    public void ExtractPublicId_ShouldCorrectlyExtractPublicId(string input, string expected)
    {
        var result = CloudinaryService.ExtractPublicId(input);
        Assert.Equal(expected, result);
    }
}
