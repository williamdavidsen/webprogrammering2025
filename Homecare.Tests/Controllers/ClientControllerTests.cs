using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Homecare.Controllers;
using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Homecare.Tests.Controllers
{
    public class ClientControllerTests
    {
        // -------------------------------------------------------
        // küçük yardımcı: Appointment + Slot üret
        // -------------------------------------------------------
        private static Appointment MakeAppt(int clientId, int personnelId, DateTime start, DateTime end)
        {
            var day = DateOnly.FromDateTime(start.Date);
            return new Appointment
            {
                ClientId = clientId,
                Status = AppointmentStatus.Scheduled,
                AvailableSlot = new AvailableSlot
                {
                    AvailableSlotId = new Random().Next(1000, 9999),
                    PersonnelId = personnelId,
                    Day = day,
                    StartTime = new TimeOnly(start.Hour, start.Minute),
                    EndTime = new TimeOnly(end.Hour, end.Minute),
                    Personnel = new User { UserId = personnelId, Name = "Nurse Test" }
                }
            };
        }

        // -------------------------------------------------------
        // SlotsForDay → Json döner mi (temel)
        // -------------------------------------------------------
        [Fact]
        public async Task SlotsForDay_Returns_FreeSlots_AsJson()
        {
            // arrange
            var apptRepo = new Mock<IAppointmentRepository>();
            var slotRepo = new Mock<IAvailableSlotRepository>();
            var userRepo = new Mock<IUserRepository>();
            var taskRepo = new Mock<ICareTaskRepository>();

            var d = DateOnly.FromDateTime(new DateTime(2025, 10, 21));

            slotRepo.Setup(r => r.GetFreeSlotsByDayAsync(d)).ReturnsAsync(new List<AvailableSlot>
            {
                new AvailableSlot{
                    AvailableSlotId = 1, Day = d,
                    StartTime = new TimeOnly(9,0), EndTime = new TimeOnly(11,0),
                    Personnel = new User{ UserId = 2, Name="Nurse A"}
                },
                new AvailableSlot{
                    AvailableSlotId = 2, Day = d,
                    StartTime = new TimeOnly(12,0), EndTime = new TimeOnly(14,0),
                    Personnel = new User{ UserId = 3, Name="Nurse B"}
                }
            });

            var sut = new ClientController(apptRepo.Object, slotRepo.Object, userRepo.Object, taskRepo.Object);

            // act
            var result = await sut.SlotsForDay(d.ToString("yyyy-MM-dd"));

            // assert
            var json = Assert.IsType<JsonResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<object>>(json.Value);
            Assert.Equal(2, list.Count());
        }

        // -------------------------------------------------------
        // Create POST → slot dolu ise Create view + ModelError
        // -------------------------------------------------------
        [Fact]
        public async Task Create_Post_WhenSlotBooked_Returns_Create_View_With_ModelError()
        {
            // arrange
            var apptRepo = new Mock<IAppointmentRepository>();
            var slotRepo = new Mock<IAvailableSlotRepository>();
            var userRepo = new Mock<IUserRepository>();
            var taskRepo = new Mock<ICareTaskRepository>();

            // RefillCreateForm içinde çağrılanlar
            apptRepo.Setup(r => r.SlotIsBookedAsync(It.IsAny<int>(), (int?)null)).ReturnsAsync(true /* veya false */);

            taskRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CareTask>());

            // slot dolu → iki parametre veriyoruz (availableSlotId, ignoreId=null)
            apptRepo.Setup(r => r.SlotIsBookedAsync(It.IsAny<int>(), (int?)null))
                    .ReturnsAsync(true);

            var sut = new ClientController(apptRepo.Object, slotRepo.Object, userRepo.Object, taskRepo.Object);

            var model = new Appointment { ClientId = 10, AvailableSlotId = 99, Status = AppointmentStatus.Scheduled };

            // act
            // (aksi̇yon imzan Createint ise: await sut.Createint(10, model, Array.Empty<int>()); )
            var result = await sut.Create(10, model, Array.Empty<int>());

            // assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Create", view.ViewName); // RefillCreateForm View("Create", model) döner
            Assert.False(sut.ModelState.IsValid);
            Assert.True(sut.ModelState.ContainsKey(nameof(model.AvailableSlotId)));
        }

        // -------------------------------------------------------
        // Create POST → başarılı ise Redirect Dashboard
        // -------------------------------------------------------
        [Fact]
        public async Task Create_Post_Valid_Redirects_To_Dashboard()
        {
            // arrange
            var apptRepo = new Mock<IAppointmentRepository>();
            var slotRepo = new Mock<IAvailableSlotRepository>();
            var userRepo = new Mock<IUserRepository>();
            var taskRepo = new Mock<ICareTaskRepository>();

            apptRepo.Setup(r => r.SlotIsBookedAsync(It.IsAny<int>(), (int?)null))
                    .ReturnsAsync(false);

            var sut = new ClientController(apptRepo.Object, slotRepo.Object, userRepo.Object, taskRepo.Object);

            var model = new Appointment { ClientId = 10, AvailableSlotId = 123, Status = AppointmentStatus.Scheduled };

            // act
            var result = await sut.Create(10, model, Array.Empty<int>());

            // assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirect.ActionName);
            Assert.Equal(10, redirect.RouteValues["clientId"]);
            apptRepo.Verify(r => r.AddAsync(It.Is<Appointment>(a => a.ClientId == 10 && a.AvailableSlotId == 123)), Times.Once);
        }

        // -------------------------------------------------------
        // Dashboard → Upcoming / Past ayrılır mı?
        // -------------------------------------------------------
        [Fact]
        public async Task Dashboard_Splits_Upcoming_And_Past()
        {
            // arrange
            var apptRepo = new Mock<IAppointmentRepository>();
            var slotRepo = new Mock<IAvailableSlotRepository>();
            var userRepo = new Mock<IUserRepository>();
            var taskRepo = new Mock<ICareTaskRepository>();

            int clientId = 77;

            userRepo.Setup(r => r.GetAsync(clientId))
                    .ReturnsAsync(new User { UserId = clientId, Name = "Client X" });

            var now = DateTime.Now;
            var future = MakeAppt(clientId, 2, now.AddDays(1).Date.AddHours(10), now.AddDays(1).Date.AddHours(12));
            var past = MakeAppt(clientId, 2, now.AddDays(-2).Date.AddHours(10), now.AddDays(-2).Date.AddHours(12));

            apptRepo.Setup(r => r.GetByClientAsync(clientId))
                    .ReturnsAsync(new List<Appointment> { future, past });

            var sut = new ClientController(apptRepo.Object, slotRepo.Object, userRepo.Object, taskRepo.Object);

            // act
            var res = await sut.Dashboard(clientId);

            // assert
            var view = Assert.IsType<ViewResult>(res);
            var upcoming = Assert.IsAssignableFrom<IEnumerable<Appointment>>(view.ViewData["Upcoming"]);
            var pastList = Assert.IsAssignableFrom<IEnumerable<Appointment>>(view.ViewData["Past"]);

            Assert.Single(upcoming);
            Assert.Single(pastList);
        }
    }
}
