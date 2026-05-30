using System.Collections.Generic;

namespace ParkFlow.Application.Features.Users.DTOs;

public class UserCredentialsDto
{
    public string Email { get; set; } = string.Empty;
    public string PrimaryProvider { get; set; } = string.Empty;
    public string? ExternalProviderId { get; set; }
    public List<LinkedIdentityDto> LinkedIdentities { get; set; } = [];

    public UserCredentialsDto() { }

    public UserCredentialsDto(string email, string primaryProvider, string? externalProviderId, List<LinkedIdentityDto> linkedIdentities)
    {
        Email = email;
        PrimaryProvider = primaryProvider;
        ExternalProviderId = externalProviderId;
        LinkedIdentities = linkedIdentities;
    }
}

public class LinkedIdentityDto
{
    public string Provider { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ProviderId { get; set; }
    public bool IsVerified { get; set; }

    public LinkedIdentityDto() { }

    public LinkedIdentityDto(string provider, string? email, string? providerId, bool isVerified)
    {
        Provider = provider;
        Email = email;
        ProviderId = providerId;
        IsVerified = isVerified;
    }
}
