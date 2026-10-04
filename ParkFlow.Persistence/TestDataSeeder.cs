using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ParkFlow.Persistence;

public static class TestDataSeeder
{
    public static async Task SeedTestAccountsAndReservationsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userAccountRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var authIdentityRepository = scope.ServiceProvider.GetRequiredService<IAuthIdentityRepository>();
        var userProfileRepository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
        var personnelRepository = scope.ServiceProvider.GetRequiredService<IPersonnelRepository>();
        var vehicleRepository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var corSubmissionRepository = scope.ServiceProvider.GetRequiredService<ICorSubmissionRepository>();
        var parkingScheduleRepository = scope.ServiceProvider.GetRequiredService<IParkingScheduleRepository>();
        var reservationRepository = scope.ServiceProvider.GetRequiredService<IParkingReservationRepository>();
        var parkingLogRepository = scope.ServiceProvider.GetRequiredService<IParkingLogRepository>();
        var qrCodeService = scope.ServiceProvider.GetRequiredService<IQrCodeService>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var defaultPasswordHash = passwordHasher.HashPassword("TestUser123!");

        // Find Admin for approving reservations
        var admin = await dbContext.Admins.FirstOrDefaultAsync();
        var guard = await dbContext.Guards.FirstOrDefaultAsync();
        var adminId = admin?.UserProfileId ?? Guid.NewGuid();

        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var phToday = philippinesNow.Date;
        var resDateUtc = DateTime.SpecifyKind(phToday, DateTimeKind.Utc);

        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("UPDATE \"ParkingReservations\" SET \"StartTime\" = '14:00:00', \"EndTime\" = '15:00:00' WHERE \"ReferenceNumber\" LIKE 'RES-%';");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"ParkingSchedules\" WHERE \"DayOfWeek\" = 0;");
        }
        catch { }

        var namesData = new (string FirstName, string MiddleName, string LastName, bool IsPersonnel, string Identifier, string ProgramOrDept, string Section, int YearLevel, VehicleType VType, string PlateNumber, string Brand, TimeSpan StartTime, TimeSpan EndTime, ReservationType ResType, string Reason)[]
        {
            // 1-35 Students (2:00 PM to 3:00 PM)
            ("Juan", "Dela", "Cruz", false, "2024-10001", "BSCS", "4A", 4, VehicleType.Car, "TEST-101", "Toyota Vios 2022", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Thesis Defense & Capstone Final Presentation"),
            ("Maria Clara", "De Los", "Santos", false, "2024-10002", "BSIT", "3B", 3, VehicleType.Motorcycle, "TEST-102", "Honda Click 125i", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Mobile Application Development Lab Session"),
            ("Carlos Miguel", "Antonio", "Garcia", false, "2024-10003", "BSCpE", "4A", 4, VehicleType.Motorcycle, "TEST-103", "Yamaha NMAX 155", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Embedded Systems Project Assembly"),
            ("Sofia Angela", "Marie", "Reyes", false, "2024-10004", "BSA", "2A", 2, VehicleType.Car, "TEST-104", "Mitsubishi Mirage G4", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Auditing & Assurance Services Review"),
            ("Gabriel Antonio", "Vargas", "Ramos", false, "2024-10005", "BSECE", "4B", 4, VehicleType.Motorcycle, "TEST-105", "Honda ADV 160", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Robotics & Telecommunication Testing"),
            ("Andrea Nicole", "Tan", "Lim", false, "2024-10006", "BSCS", "3A", 3, VehicleType.Car, "TEST-106", "Ford Ranger Raptor", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "AI & Machine Learning Data Collection"),
            ("Mateo Lucas", "Cabrera", "Tan", false, "2024-10007", "BSIT", "2B", 2, VehicleType.Motorcycle, "TEST-107", "Vespa Primavera 150", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Web Systems Backend Development"),
            ("Isabella Rose", "Chavez", "Bautista", false, "2024-10008", "BSBA", "3A", 3, VehicleType.Car, "TEST-108", "Nissan Navara Pro-4X", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Marketing Research & Focus Group"),
            ("Rafael Jose", "Benitez", "Aquino", false, "2024-10009", "BSCE", "4A", 4, VehicleType.Motorcycle, "TEST-109", "Suzuki Raider R150", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Structural Design Analysis Session"),
            ("Chloe Grace", "Salazar", "Mendoza", false, "2024-10010", "BSN", "3C", 3, VehicleType.Car, "TEST-110", "Honda City RS", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Special, "University Health Center Duty & Seminar"),
            ("Elijah James", "Valdez", "Flores", false, "2024-10011", "BSCS", "2A", 2, VehicleType.Motorcycle, "TEST-111", "Yamaha Aerox 155", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Algorithms & Data Structures Competition"),
            ("Alyssa Mae", "Gutierrez", "Castro", false, "2024-10012", "BSIT", "4A", 4, VehicleType.Car, "TEST-112", "Mazda 3 Sedan", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Cybersecurity Defense Drill"),
            ("Dominic Kyle", "Navarro", "Villanueva", false, "2024-10013", "BSCpE", "3B", 3, VehicleType.Motorcycle, "TEST-113", "Kawasaki Ninja 400", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Microcontroller Interfacing Workshop"),
            ("Hannah Patricia", "Soriano", "Navarro", false, "2024-10014", "BSA", "4A", 4, VehicleType.Car, "TEST-114", "Hyundai Tucson", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Taxation Mock Board Examination"),
            ("Adrian Paul", "Estrada", "Corpuz", false, "2024-10015", "BSECE", "2A", 2, VehicleType.Motorcycle, "TEST-115", "Honda PCX 160", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Circuit Simulation & Breadboarding"),
            ("Bea Bianca", "Mercado", "Domingo", false, "2024-10016", "BSCS", "1A", 1, VehicleType.Car, "TEST-116", "Kia Seltos", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Freshmen Orientation & Coding Bootcamp"),
            ("Christian Luke", "Pascual", "Mercado", false, "2024-10017", "BSIT", "3A", 3, VehicleType.Motorcycle, "TEST-117", "Yamaha Sniper 155", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Database Administration Hands-on"),
            ("Patricia Joy", "Mariano", "Salazar", false, "2024-10018", "BSBA", "4B", 4, VehicleType.Car, "TEST-118", "Toyota Fortuner", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Strategic Management Feasibility Defense"),
            ("Nathaniel David", "Castillo", "Soriano", false, "2024-10019", "BSCE", "3A", 3, VehicleType.Motorcycle, "TEST-119", "Honda Beat 110", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Fluid Mechanics Laboratory Testing"),
            ("Samantha Gale", "Aguilar", "Pascual", false, "2024-10020", "BSN", "2A", 2, VehicleType.Car, "TEST-120", "Suzuki Jimny", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Special, "Medical Mission Preparation"),
            ("Joshua Miguel", "Tolentino", "Valdez", false, "2024-10021", "BSCS", "4B", 4, VehicleType.Motorcycle, "TEST-121", "Yamaha Mio Gravis", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Cloud Infrastructure Architecture Design"),
            ("Katrina Mae", "Rivera", "Ocampo", false, "2024-10022", "BSIT", "2A", 2, VehicleType.Car, "TEST-122", "Toyota Corolla Cross", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Human-Computer Interaction Usability Testing"),
            ("Daniel Keith", "Guzman", "Aguilar", false, "2024-10023", "BSCpE", "4A", 4, VehicleType.Motorcycle, "TEST-123", "KTM Duke 390", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "IoT Sensor Grid Deployment"),
            ("Monica Elaine", "Cortez", "Santiago", false, "2024-10024", "BSA", "3A", 3, VehicleType.Car, "TEST-124", "Honda Civic Type R", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Financial Accounting & Reporting Workshop"),
            ("Ezekiel John", "Del Rosario", "Del Rosario", false, "2024-10025", "BSECE", "3A", 3, VehicleType.Motorcycle, "TEST-125", "Vespa Sprint 150", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Digital Signal Processing Lab"),
            ("Jasmine Chloe", "Feliciano", "Cortez", false, "2024-10026", "BSCS", "3B", 3, VehicleType.Car, "TEST-126", "Subaru Forester", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Software Engineering Sprint Planning"),
            ("Tristan Sean", "Alcantara", "Rivera", false, "2024-10027", "BSIT", "4B", 4, VehicleType.Motorcycle, "TEST-127", "Honda Winner X", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Network Administration & Routing Lab"),
            ("Stephanie Anne", "Evangelista", "Guzman", false, "2024-10028", "BSBA", "1A", 1, VehicleType.Car, "TEST-128", "Toyota Hilux Conquest", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Business Economics Seminar"),
            ("Vincent Mark", "Padilla", "Tolentino", false, "2024-10029", "BSCE", "2B", 2, VehicleType.Motorcycle, "TEST-129", "Suzuki Burgman Street", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Surveying & Geomatics Fieldwork"),
            ("Clarisse Joy", "Morales", "Castillo", false, "2024-10030", "BSN", "4A", 4, VehicleType.Car, "TEST-130", "Mitsubishi Xpander", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Special, "Hospital Clinical Rotation Briefing"),
            ("Kenneth Roy", "Roxas", "Estrada", false, "2024-10031", "BSCS", "2B", 2, VehicleType.Motorcycle, "TEST-131", "Yamaha Fazzio 125", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Object-Oriented Programming Peer Study"),
            ("Nicole Amber", "Miranda", "De Leon", false, "2024-10032", "BSIT", "1B", 1, VehicleType.Car, "TEST-132", "Nissan Kicks e-POWER", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Introduction to Information Systems"),
            ("Timothy Grant", "Manalo", "Mariano", false, "2024-10033", "BSCpE", "1A", 1, VehicleType.Motorcycle, "TEST-133", "Royal Enfield Hunter 350", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Digital Logic Circuits Practical Exam"),
            ("Erica Lynn", "David", "Serrano", false, "2024-10034", "BSA", "1B", 1, VehicleType.Car, "TEST-134", "Hyundai Stargazer", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Basic Accounting Consultation"),
            ("Patrick Sean", "Henson", "Javier", false, "2024-10035", "BSECE", "1A", 1, VehicleType.Motorcycle, "TEST-135", "Honda CBR500R", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Engineering Physics & Magnetism Lab"),

            // 36-50 Personnel (2:00 PM to 3:00 PM)
            ("Prof. Fernando", "Alonzo", "Morales", true, "EMP-2024-001", "College of Computer Studies", "", 0, VehicleType.Car, "TEST-136", "Toyota Camry 2.5V", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Faculty Research Advisory & Lab Supervision"),
            ("Dr. Teresa", "Santos", "Evangelista", true, "EMP-2024-002", "College of Science", "", 0, VehicleType.Car, "TEST-137", "Honda HR-V Turbo", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Biology Department Curriculum Alignment"),
            ("Engr. Roberto", "Cruz", "Padilla", true, "EMP-2024-003", "College of Engineering", "", 0, VehicleType.Car, "TEST-138", "Ford Everest Titanium", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Engineering Laboratory Calibration"),
            ("Prof. Elena", "Reyes", "Fernandez", true, "EMP-2024-004", "College of Business Administration", "", 0, VehicleType.Car, "TEST-139", "Mitsubishi Pajero Sport", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Faculty Development & Accreditation Session"),
            ("Atty. Manuel", "Dela Rosa", "Roxas", true, "EMP-2024-005", "Office of the Legal Counsel", "", 0, VehicleType.Car, "TEST-140", "BMW 320i Sport", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Special, "University Board of Regents Meeting"),
            ("Carmela", "Bautista", "Miranda", true, "EMP-2024-006", "Office of the University Registrar", "", 0, VehicleType.Car, "TEST-141", "Toyota RAV4 Hybrid", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Graduation Clearance Processing"),
            ("Rodrigo", "Garcia", "Alcantara", true, "EMP-2024-007", "Facilities and General Services", "", 0, VehicleType.Car, "TEST-142", "Isuzu D-Max 3.0", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Campus Facilities Maintenance Inspection"),
            ("Vivian", "Aquino", "Manalo", true, "EMP-2024-008", "Accounting and Finance Office", "", 0, VehicleType.Car, "TEST-143", "Mazda CX-5", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Quarterly Financial Audit Deliberation"),
            ("Leonardo", "Villanueva", "David", true, "EMP-2024-009", "Information Technology Services", "", 0, VehicleType.Car, "TEST-144", "Hyundai Creta", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Campus Network Backbone Upgrading"),
            ("Victoria", "Castro", "Henson", true, "EMP-2024-010", "Human Resources Department", "", 0, VehicleType.Car, "TEST-145", "Nissan Terra VL", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "University Staff Recruitment Interviews"),
            ("Dr. Arturo", "Mendoza", "Henson", true, "EMP-2024-011", "College of Nursing", "", 0, VehicleType.Car, "TEST-146", "Honda CR-V Hybrid", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Nursing Skills Lab Examination"),
            ("Engr. Cristina", "Flores", "Pineda", true, "EMP-2024-012", "College of Engineering", "", 0, VehicleType.Car, "TEST-147", "Subaru XV", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Civil Engineering Material Testing"),
            ("Prof. Raymond", "Navarro", "Zulueta", true, "EMP-2024-013", "College of Arts and Letters", "", 0, VehicleType.Car, "TEST-148", "Toyota Rush", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "University Cultural Event Rehearsal"),
            ("Rowena", "Corpuz", "Feliciano", true, "EMP-2024-014", "Student Affairs Office", "", 0, VehicleType.Car, "TEST-149", "Suzuki Swift", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Student Council General Assembly"),
            ("Michael Angelo", "Domingo", "Lopez", true, "EMP-2024-015", "Security and Safety Department", "", 0, VehicleType.Car, "TEST-150", "Kia Carnival", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Special, "Campus Security Oversight & Protocol Review"),

            // 51-70 (2:00 PM to 3:00 PM)
            ("Justin", "Alvarez", "Mendez", false, "2024-10036", "BSCS", "3A", 3, VehicleType.Car, "TEST-151", "Toyota Raize 1.0 Turbo", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Software Engineering Consultation"),
            ("Danielle", "Sotto", "Tan", false, "2024-10037", "BSIT", "2A", 2, VehicleType.Motorcycle, "TEST-152", "Honda Click 150i", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Web Programming Laboratory"),
            ("Marcus", "Villafuerte", "Bernardo", false, "2024-10038", "BSCpE", "4B", 4, VehicleType.Car, "TEST-153", "Mitsubishi Montero Sport", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Computer Architecture Defense"),
            ("Giselle", "Laurel", "Ocampo", false, "2024-10039", "BSA", "3B", 3, VehicleType.Motorcycle, "TEST-154", "Yamaha Gravis 125", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Management Accounting Study Group"),
            ("Brian", "Alonzo", "Castaneda", false, "2024-10040", "BSECE", "3B", 3, VehicleType.Car, "TEST-155", "Mazda CX-30", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Signals and Communications Lab"),
            ("Kaye", "Concepcion", "Salas", false, "2024-10041", "BSBA", "2A", 2, VehicleType.Motorcycle, "TEST-156", "Honda Beat Street", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Operations Management Project"),
            ("Jerome", "Enriquez", "Mercado", false, "2024-10042", "BSCE", "4B", 4, VehicleType.Car, "TEST-157", "Nissan Terra 4x4", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Geotechnical Engineering Soil Testing"),
            ("Pauline", "Velasco", "Soriano", false, "2024-10043", "BSN", "3A", 3, VehicleType.Motorcycle, "TEST-158", "Yamaha Aerox V2", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Special, "Clinical Skills Lab Evaluation"),
            ("Kenneth", "Imperial", "Gomez", false, "2024-10044", "BSCS", "4A", 4, VehicleType.Car, "TEST-159", "Ford Territory Titanium", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Distributed Systems Workshop"),
            ("Janice", "Manahan", "Pascual", false, "2024-10045", "BSIT", "3A", 3, VehicleType.Motorcycle, "TEST-160", "Honda ADV 150", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Cloud Architecture Seminar"),
            ("Reginald", "Bautista", "Aquino", false, "2024-10046", "BSCpE", "2A", 2, VehicleType.Car, "TEST-161", "Toyota Yaris Cross", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Digital Logic Simulation"),
            ("Christine", "Zapata", "Rios", false, "2024-10047", "BSA", "4B", 4, VehicleType.Motorcycle, "TEST-162", "Suzuki Smash 115", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Auditing Practice Session"),
            ("Lorenzo", "Tiu", "Ang", false, "2024-10048", "BSBA", "4A", 4, VehicleType.Car, "TEST-163", "Honda CR-V AWD", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Business Strategy Case Presentation"),
            ("Monica", "Co", "Sy", false, "2024-10049", "BSECE", "4A", 4, VehicleType.Motorcycle, "TEST-164", "KTM RC 200", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Electromagnetics Laboratory Exam"),
            ("Gerard", "Macaraeg", "Valdez", false, "2024-10050", "BSCE", "3B", 3, VehicleType.Car, "TEST-165", "Isuzu mu-X 3.0", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Highway Engineering Design Review"),
            ("Prof. Arnold", "Guevarra", "Solis", true, "EMP-2024-016", "College of Computer Studies", "", 0, VehicleType.Car, "TEST-166", "Toyota Fortuner GR-S", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Faculty Committee Consultation"),
            ("Dr. Marissa", "Cunanan", "Tiongson", true, "EMP-2024-017", "College of Science", "", 0, VehicleType.Car, "TEST-167", "Honda Accord 1.5 Turbo", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Research Journal Editorial Meeting"),
            ("Engr. Felix", "Legaspi", "Marasigan", true, "EMP-2024-018", "College of Engineering", "", 0, VehicleType.Car, "TEST-168", "Mitsubishi Strada Athlete", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Evening Engineering Lab Consultation"),
            ("Marilou", "Dizon", "Samson", true, "EMP-2024-019", "Admissions and Registration", "", 0, VehicleType.Car, "TEST-169", "Hyundai Stargazer X", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Normal, "Late Admissions Document Verification"),
            ("Capt. Salvador", "Buenaventura", "Reyes", true, "EMP-2024-020", "Campus Safety and Security", "", 0, VehicleType.Car, "TEST-170", "Ford Ranger Wildtrak", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), ReservationType.Special, "Night Campus Security Briefing and Inspection")
        };

        for (int i = 0; i < namesData.Length; i++)
        {
            var item = namesData[i];
            var indexNumber = i + 1;
            var email = $"testuser{indexNumber:D2}@parkflow.com";
            var phone = $"+63917000{indexNumber:D4}";

            // 1. Account & Identity
            var existingAccount = await userAccountRepository.GetByEmailAsync(email);
            UserAccount user;
            if (existingAccount == null)
            {
                user = new UserAccount(defaultPasswordHash, phone);
                user.UpdateOnboardingStep(OnboardingStep.Done);
                user.Verify();
                await userAccountRepository.AddAsync(user);

                var identity = AuthIdentity.CreateManual(user.Id, email, defaultPasswordHash, isPrimary: true);
                identity.MarkVerified();
                await authIdentityRepository.AddAsync(identity);

                var profile = new UserProfile(user.Id, item.FirstName, item.LastName, item.MiddleName, null);
                await userProfileRepository.AddAsync(profile);

                if (item.IsPersonnel)
                {
                    var personnel = new Personnel(profile.Id, item.Identifier, item.ProgramOrDept, Roles.UniversityStaff);
                    await personnelRepository.AddAsync(personnel);
                }
                else
                {
                    var student = new Student(profile.Id, item.Identifier, item.ProgramOrDept, item.Section, item.YearLevel);
                    await studentRepository.AddAsync(student);
                }
            }
            else
            {
                user = existingAccount;
                user.Verify();
                user.UpdateOnboardingStep(OnboardingStep.Done);
                await userAccountRepository.UpdateAsync(user);
            }

            // 2. COR Submission & Schedule
            var existingCor = await dbContext.CorSubmissions.FirstOrDefaultAsync(c => c.UserAccountId == user.Id);
            CorSubmission cor;
            if (existingCor == null)
            {
                cor = new CorSubmission(
                    user.Id,
                    "2024-2025",
                    "https://parkflow.blob.core.windows.net/documents/sample_cor.pdf",
                    "https://parkflow.blob.core.windows.net/documents/sample_orcr.pdf",
                    "https://parkflow.blob.core.windows.net/documents/sample_vehicle.jpg",
                    CorVerificationStatus.Verified);
                await dbContext.CorSubmissions.AddAsync(cor);
                await dbContext.SaveChangesAsync();

                var schedules = new List<ParkingSchedule>
                {
                    new ParkingSchedule(cor.Id, DayOfWeek.Monday, new TimeSpan(7, 0, 0), new TimeSpan(21, 0, 0)),
                    new ParkingSchedule(cor.Id, DayOfWeek.Tuesday, new TimeSpan(7, 0, 0), new TimeSpan(21, 0, 0)),
                    new ParkingSchedule(cor.Id, DayOfWeek.Wednesday, new TimeSpan(7, 0, 0), new TimeSpan(21, 0, 0)),
                    new ParkingSchedule(cor.Id, DayOfWeek.Thursday, new TimeSpan(7, 0, 0), new TimeSpan(21, 0, 0)),
                    new ParkingSchedule(cor.Id, DayOfWeek.Friday, new TimeSpan(7, 0, 0), new TimeSpan(21, 0, 0))
                };
                await dbContext.ParkingSchedules.AddRangeAsync(schedules);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                cor = existingCor;
            }

            // 3. Vehicle
            var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(v => v.OwnerId == user.Id && v.PlateNumber == item.PlateNumber);
            if (vehicle == null)
            {
                var qrPayload = $"{user.Id}:{item.PlateNumber}:{item.Brand}";
                var qrBytes = qrCodeService.GenerateQrCode(qrPayload);
                var qrHash = Convert.ToBase64String(SHA256.HashData(qrBytes));

                vehicle = new Vehicle(user.Id, item.PlateNumber, item.Brand, qrHash, item.VType);
                vehicle.SetPrimary(true);
                vehicle.UpdateDocuments("https://parkflow.blob.core.windows.net/documents/sample_orcr.pdf", "https://parkflow.blob.core.windows.net/documents/sample_vehicle.jpg");
                vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
                await dbContext.Vehicles.AddAsync(vehicle);
                await dbContext.SaveChangesAsync();
            }

            // 4. Approved Active Reservation for Today (Updated to 2:30 PM / new times)
            var refNumber = $"RES-{phToday:yyyyMMdd}-{indexNumber:D4}";
            var existingRes = await dbContext.ParkingReservations.FirstOrDefaultAsync(r => r.ReferenceNumber == refNumber);
            if (existingRes == null)
            {
                var reservation = new ParkingReservation(
                    user.Id,
                    refNumber,
                    resDateUtc,
                    item.StartTime,
                    item.EndTime,
                    item.Reason,
                    item.ResType,
                    vehicle.Id);

                reservation.Approve(adminId, "Pre-approved test reservation for testing");
                await dbContext.ParkingReservations.AddAsync(reservation);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                // Update existing reservation StartTime and EndTime
                var startProp = typeof(ParkingReservation).GetProperty(nameof(ParkingReservation.StartTime));
                var endProp = typeof(ParkingReservation).GetProperty(nameof(ParkingReservation.EndTime));
                startProp?.SetValue(existingRes, item.StartTime);
                endProp?.SetValue(existingRes, item.EndTime);
                await dbContext.SaveChangesAsync();
            }

            // 5. Active Live Session for first 10 accounts
            if (indexNumber <= 10)
            {
                var activeLog = await dbContext.ParkingLogs.FirstOrDefaultAsync(p => p.VehicleId == vehicle.Id && p.Status == ParkingStatus.Parked && p.ExitTime == null);
                if (activeLog == null)
                {
                    var log = new ParkingLog(vehicle.Id, guard?.UserProfileId, ParkingStatus.Parked, EntryMethod.QrCode);
                    // Adjust EntryTime to be in the past (e.g. 15 to 150 minutes ago)
                    var entryTimeField = typeof(ParkingLog).GetProperty(nameof(ParkingLog.EntryTime));
                    entryTimeField?.SetValue(log, DateTime.UtcNow.AddMinutes(-15 * indexNumber));
                    await dbContext.ParkingLogs.AddAsync(log);
                    await dbContext.SaveChangesAsync();
                }
            }
        }
    }
}
