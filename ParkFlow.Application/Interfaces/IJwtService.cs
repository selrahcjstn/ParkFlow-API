namespace ParkFlow.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(UserAccount user, string profileType);
    string GenerateToken(UserAccount user, string profileType, string authenticationMethod) => GenerateToken(user, profileType);
}
