using Xunit;

namespace Test.Features.Verification;

// Local samples are opt-in; personal documents are never copied into the repository.
public sealed class LocalCorSampleFactAttribute : FactAttribute
{
    public LocalCorSampleFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PARKFLOW_COR_SAMPLE")))
            Skip = "Set PARKFLOW_COR_SAMPLE to the local reference COR to check its layout.";
    }
}
