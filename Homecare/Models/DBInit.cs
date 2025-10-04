using Homecare.Models;
using Microsoft.EntityFrameworkCore;

namespace Homecare.Models
{
    public static class DBInit
    {
        public static void Seed(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Geliştirme/test için:
            //db.Database.EnsureDeleted();   // İSTEMEZSEN YORUMLA
            db.Database.EnsureCreated();

            if (!db.Users.Any())
            {
                db.Users.AddRange(
                    new User { UserId = 1, Name = "Admin One", Email = "admin@hc.test", PasswordHash = "Admin!23", Role = UserRole.Admin },
                    new User { UserId = 2, Name = "Nurse A", Email = "nurse.a@hc.test", PasswordHash = "P@ss", Role = UserRole.Personnel },
                    new User { UserId = 3, Name = "Nurse B", Email = "nurse.b@hc.test", PasswordHash = "P@ss", Role = UserRole.Personnel },
                    new User { UserId = 4, Name = "Nurse C", Email = "nurse.c@hc.test", PasswordHash = "P@ss", Role = UserRole.Personnel },
                    new User { UserId = 5, Name = "Nurse D", Email = "nurse.d@hc.test", PasswordHash = "P@ss", Role = UserRole.Personnel },
                    new User { UserId = 6, Name = "Nurse E", Email = "nurse.e@hc.test", PasswordHash = "P@ss", Role = UserRole.Personnel },
                    new User { UserId = 10, Name = "Client Ali", Email = "client.ali@hc.test", PasswordHash = "1234", Role = UserRole.Client },
                    new User { UserId = 11, Name = "Client Eva", Email = "client.eva@hc.test", PasswordHash = "1234", Role = UserRole.Client },
                    new User { UserId = 12, Name = "Client Leo", Email = "client.leo@hc.test", PasswordHash = "1234", Role = UserRole.Client },
                    new User { UserId = 13, Name = "Client Mia", Email = "client.mia@hc.test", PasswordHash = "1234", Role = UserRole.Client },
                    new User { UserId = 14, Name = "Client Yan", Email = "client.yan@hc.test", PasswordHash = "1234", Role = UserRole.Client }
                );
                db.SaveChanges();
            }

            if (!db.CareTasks.Any())
            {
                db.CareTasks.AddRange(
                    new CareTask { CareTaskId = 1, Description = "Medication reminder" },
                    new CareTask { CareTaskId = 2, Description = "Assistance with daily living" },
                    new CareTask { CareTaskId = 3, Description = "Shopping / groceries" },
                    new CareTask { CareTaskId = 4, Description = "Light cleaning" }
                );
                db.SaveChanges();
            }

            // Yarın için tüm hemşirelere 3 slot (sabah/öğlen/akşam)
            var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            var nurses = db.Users.Where(u => u.Role == UserRole.Personnel).ToList();

            foreach (var nurse in nurses)
            {
                if (db.AvailableSlots.Any(s => s.PersonnelId == nurse.UserId && s.Day == tomorrow))
                    continue;

                var templates = new (TimeOnly Start, TimeOnly End)[]
                {
                    (new TimeOnly(9,  0), new TimeOnly(11, 0)), // Morning
                    (new TimeOnly(12, 0), new TimeOnly(14, 0)), // Noon
                    (new TimeOnly(16, 0), new TimeOnly(18, 0)), // Evening
                };

                foreach (var t in templates)
                {
                    db.AvailableSlots.Add(new AvailableSlot
                    {
                        PersonnelId = nurse.UserId,
                        Day = tomorrow,
                        StartTime = t.Start,
                        EndTime = t.End
                    });
                }
            }
            db.SaveChanges();

            if (!db.Appointments.Any())
            {
                var firstFree = db.AvailableSlots.Include(s => s.Appointment)
                                 .FirstOrDefault(s => s.Day == tomorrow && s.Appointment == null);
                if (firstFree != null)
                {
                    var appt = new Appointment
                    {
                        ClientId = 10,
                        AvailableSlotId = firstFree.AvailableSlotId,
                        Status = AppointmentStatus.Scheduled,
                        Description = "Initial visit"
                    };
                    db.Appointments.Add(appt);
                    db.SaveChanges();

                    db.TaskLists.AddRange(
                        new TaskList { AppointmentId = appt.AppointmentId, CareTaskId = 1 },
                        new TaskList { AppointmentId = appt.AppointmentId, CareTaskId = 3 }
                    );
                    db.SaveChanges();
                }
            }
        }
    }
}
