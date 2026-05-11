namespace ParkFlow.Domain.Entities;

public class ParkingSchedule : BaseEntity
{
    public Guid SubmissionId { get; private set; }
    public CorSubmission CorSubmission { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
}
