namespace ParkFlow.Application.Features.Users.DTOs
{
    public sealed record AuthResponse(
        string Token,
        bool IsNewAccount);
}
