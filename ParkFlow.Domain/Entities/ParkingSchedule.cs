namespace ParkFlow.Domain.Entities;

public class ParkingSchedule : BaseEntity
{
    public Guid SubmissionId { get; private set; }
    public CorSubmission CorSubmission { get; private set; } = null!;

    public DayOfWeek DayOfWeek { get; private set; }
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }

    private ParkingSchedule() { }

    public ParkingSchedule(
        Guid submissionId,
        DayOfWeek dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime)
    {
        SubmissionId = submissionId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }
}
