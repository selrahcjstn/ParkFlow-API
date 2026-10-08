public class Student
{
    public Guid UserProfileId { get; set; }   // FK + PK
    public UserProfile UserProfile { get; set; } = null!;

    public string StudentNumber { get; set; } = null!;
    public string? Course { get; set; }
    public string Section { get; set; } = null!;
    public int YearLevel { get; set; }

    private Student() { } // For EF Core

    public Student(Guid profileId, string studentNumber, string? course, string section, int yearLevel)
    {
        UserProfileId = profileId;

        StudentNumber = NormalizeNumber(studentNumber);
        Course = course;
        Section = section;
        YearLevel = yearLevel;
    }

    public void UpdateDetails(string studentNumber, string? course, string section, int yearLevel)
    {
        StudentNumber = NormalizeNumber(studentNumber);
        Course = course;
        Section = section;
        YearLevel = yearLevel;
    }
    public static string NormalizeNumber(string studentNumber) =>
        studentNumber.Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant();
}
