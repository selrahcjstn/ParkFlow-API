using System.Text.Json;
using ParkFlow.Application.Features.Auth.Registration;
using Xunit;

namespace Test.Features.Auth;

public class EBikeRegistrationDetailsTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    public async Task EBikeRegistrationWithoutPlateIsNotRejectedForTheVehicle(string plate)
    {
        var json = "{\"email\":\"a@b.com\",\"password\":\"Passw0rd!x\",\"role\":2,\"firstName\":\"Ana\",\"lastName\":\"Cruz\",\"middleName\":null," +
            "\"phoneNumber\":\"09171234567\",\"idCardNumber\":\"EMP-1\",\"department\":\"Office\",\"plateNumber\":" + plate +
            ",\"brand\":\"Fiido\",\"vehicleType\":1,\"academicYear\":\"\",\"semester\":\"\",\"college\":null}";
        var details = JsonSerializer.Deserialize<RegistrationDetails>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var command = new CompleteRegistration(Guid.NewGuid(), details, "a@b.com", "staged", "staged", "staged", null, "staged", "staged");
        var errors = await CompleteRegistrationHandler.ValidateAsync(command, default);
        Assert.DoesNotContain(errors, e => e.Contains("plate", StringComparison.OrdinalIgnoreCase) || e.Contains("Complete all required"));
    }
}

public class EBikeOnboardingRequestTests
{
    [Fact]
    public void OnboardingVehicleRequestAcceptsANullPlateForAnEBike()
    {
        // ASP.NET treats non-nullable reference properties as [Required], which returned HTTP 400 for e-bikes.
        var property = typeof(ParkFlow.Application.Features.Onboarding.DTOs.OnboardingVehicleRequest).GetConstructors()[0]
            .GetParameters().Single(p => p.Name == "PlateNumber");
        var nullable = new System.Reflection.NullabilityInfoContext().Create(property);
        Assert.Equal(System.Reflection.NullabilityState.Nullable, nullable.WriteState);
    }
}
