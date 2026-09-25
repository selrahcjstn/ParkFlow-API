using System;

namespace ParkFlow.Application.Features.Feedbacks.DTOs
{
    public record UserRatingDto(int? Rating, DateTime? RatedAt, bool HasRated);
}
