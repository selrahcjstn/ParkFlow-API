namespace ParkFlow.Application.Interfaces;

public interface IEmailVerificationReceipt
{
    string Issue(string email);
    bool Validate(string email, string token);
}
