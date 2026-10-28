using Microsoft.Extensions.Options;
using ParkFlow.Infrastructure.Cloudinary;
using Xunit;

namespace Test.Features.Files;

public class SecureStorageUrlTests
{
    [Fact]
    public void SignedStorageUrlUsesHttpsWithoutMakingANetworkRequest()
    {
        var service = new CloudinaryService(Options.Create(new CloudinarySettings { CloudName = "synthetic", ApiKey = "synthetic", ApiSecret = "synthetic" }));
        var result = service.GetAuthenticatedDownloadUrl("https://res.cloudinary.com/synthetic/raw/upload/v1/parkflow/cor/synthetic.pdf");
        Assert.NotNull(result); Assert.Equal("https", new Uri(result).Scheme); Assert.Empty(new Uri(result).UserInfo);
    }
}
