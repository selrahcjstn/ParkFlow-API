using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces
{
    public interface IFeedbackRepository
    {
        Task AddAsync(Feedback feedback);
        Task<Feedback?> GetByIdAsync(Guid id);
        Task<IEnumerable<Feedback>> GetAllAsync(string? category = null, int? status = null);
        Task<IEnumerable<Feedback>> GetByUserIdAsync(Guid userId);
        Task UpdateAsync(Feedback feedback);
    }
}
