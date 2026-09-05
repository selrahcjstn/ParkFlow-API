using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Persistence.Repositories
{
    public class FeedbackRepository : IFeedbackRepository
    {
        private readonly AppDbContext _context;

        public FeedbackRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Feedback feedback)
        {
            await _context.Feedbacks.AddAsync(feedback);
            await _context.SaveChangesAsync();
        }

        public async Task<Feedback?> GetByIdAsync(Guid id)
        {
            return await _context.Feedbacks
                .Include(f => f.UserProfile)
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<IEnumerable<Feedback>> GetAllAsync(string? category = null, int? status = null)
        {
            var query = _context.Feedbacks
                .Include(f => f.UserProfile)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) && category.ToLower() != "all")
            {
                var catLower = category.Trim().ToLower();
                query = query.Where(f => f.Category.ToLower() == catLower);
            }

            if (status.HasValue && status.Value > 0)
            {
                var statusEnum = (FeedbackStatus)status.Value;
                query = query.Where(f => f.Status == statusEnum);
            }

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<Feedback>> GetByUserIdAsync(Guid userId)
        {
            return await _context.Feedbacks
                .Include(f => f.UserProfile)
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateAsync(Feedback feedback)
        {
            _context.Feedbacks.Update(feedback);
            await _context.SaveChangesAsync();
        }
    }
}
