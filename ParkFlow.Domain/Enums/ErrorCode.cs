namespace ParkFlow.Domain.Enums;

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
