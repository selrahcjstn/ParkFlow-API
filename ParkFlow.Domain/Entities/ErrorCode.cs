namespace ParkFlow.Domain.Entities;

public enum ErrorCode
{
    None,
    UserNotFound,
    InvalidPassword,
    AccountLocked,
    EmailAlreadyExists,
    PhoneNumberAlreadyExists,
    InvalidRole
}
