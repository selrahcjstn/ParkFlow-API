using System;

namespace ParkFlow.Application.Interfaces;

public interface IUserContext
{
    Guid GetUserId();
}
