public class Student
{
    public Guid UserProfileId { get; set; }   // FK + PK
    public UserProfile UserProfile { get; set; } = null!;

    public string StudentNumber { get; set; } = null!;
    public string Course { get; set; } = null!;
    public string Section { get; set; } = null!;
    public int YearLevel { get; set; }

    private Student() { } // For EF Core

    public Student(UserProfile userProfile, string studentNumber, string course, string section, int yearLevel)
    {
        UserProfile = userProfile;
        UserProfileId = userProfile.Id;

        StudentNumber = studentNumber;
        Course = course;
        Section = section;
        YearLevel = yearLevel;
    }

    public void UpdateDetails(string studentNumber, string course, string section, int yearLevel)
    {
        StudentNumber = studentNumber;
        Course = course;
        Section = section;
        YearLevel = yearLevel;
    }
}