using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Homecare.Controllers;
using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace Homecare.Tests.Controllers
{
    public class PersonnelControllerTests
    {
        private static PersonnelController BuildSut(
            out Mock<IAppointmentRepository> apptRepo,
            out Mock<IAvailableSlotRepository> slotRepo,
            out Mock<IUserRepository> userRepo)
        {
            apptRepo = new Mock<IAppointmentRepository>();
            slotRepo = new Mock<IAvailableSlotRepository>();
            userRepo = new Mock<IUserRepository>();

            // --- NULL döndürmeyelim; boş LIST verelim ---
            slotRepo.Setup(r => r.GetWorkDaysAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
                    .ReturnsAsync(new List<DateOnly>());

            slotRepo.Setup(r => r.GetLockedDaysAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
                    .ReturnsAsync(new List<DateOnly>());

            slotRepo.Setup(r => r.GetSlotsForPersonnelOnDayAsync(It.IsAny<int>(), It.IsAny<DateOnly>()))
                    .ReturnsAsync(new List<AvailableSlot>());

            slotRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<AvailableSlot>>()))
                    .Returns(Task.CompletedTask);

            slotRepo.Setup(r => r.RemoveRangeAsync(It.IsAny<IEnumerable<AvailableSlot>>()))
                    .Returns(Task.CompletedTask);

            var sut = new PersonnelController(apptRepo.Object, slotRepo.Object, userRepo.Object)
            {
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };
            return sut;
        }

        [Fact]
        public async Task CreateDay_WhenNoExisting_AddsThreeSlots()
        {
            // Arrange
            var sut = BuildSut(out var apptRepo, out var slotRepo, out var userRepo);

            var day = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            var daysParam = day.ToString("yyyy-MM-dd");

            IEnumerable<AvailableSlot>? captured = null;
            slotRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<AvailableSlot>>()))
                    .Callback<IEnumerable<AvailableSlot>>(arg => captured = arg)
                    .Returns(Task.CompletedTask);

            // Act
            var result = await sut.CreateDay(personnelId: 2, days: daysParam);

            // Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirect.ActionName);

            Assert.NotNull(captured);
            Assert.Equal(3, captured!.Count());
            Assert.All(captured!, s =>
            {
                Assert.Equal(2, s.PersonnelId);
                Assert.Equal(day, s.Day);
            });
        }

        [Fact]
        public async Task CreateDay_WhenDuplicateDayForPersonnel_RedirectsWithError_NoAdd()
        {
            // Arrange
            var sut = BuildSut(out var apptRepo, out var slotRepo, out var userRepo);

            var day = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            var daysParam = day.ToString("yyyy-MM-dd");

            // O gün zaten varmış gibi davran (List!)
            slotRepo.Setup(r => r.GetWorkDaysAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
                    .ReturnsAsync(new List<DateOnly> { day });

            // Act
            var result = await sut.CreateDay(personnelId: 9, days: daysParam);

            // Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirect.ActionName);

            slotRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<AvailableSlot>>()), Times.Never);
        }
    }
}
