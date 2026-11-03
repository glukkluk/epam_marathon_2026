using APIHandler.Constants;
using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GroupReservations;
using AutoFixture;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Services;

public class GroupPreOrderServiceTests
{
    private readonly Mock<IReservationRepository> reservations = new();
    private readonly Mock<IGroupReservationRepository> groups = new();
    private readonly Mock<IGuestSessionRepository> sessions = new();
    private readonly Mock<IDishRepository> dishes = new();
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<ITableRepository> tables = new();
    private readonly GroupPreOrderService service;
    private readonly Reservation reservation;
    private readonly GroupReservation group;
    private readonly Dish dish;
    private readonly List<GuestSession> guests;

    public GroupPreOrderServiceTests()
    {
        var fixture = new Fixture();
        reservation = fixture.Build<Reservation>().With(r => r.Status, ReservationStatus.Reserved)
            .With(r => r.StartTime, DateTime.UtcNow.AddDays(1)).Create();
        group = fixture.Build<GroupReservation>().With(g => g.ReservationId, reservation.Id)
            .With(g => g.Status, GroupReservationStatus.Open).With(g => g.JoinedGuestCount, 2).Create();
        dish = fixture.Build<Dish>().With(d => d.Price, 12.5m).With(d => d.State, DishState.Available).Create();
        guests = [fixture.Build<GuestSession>().With(g => g.IsSubmitted, true)
            .With(g => g.Dishes, new List<OrderItem> { new() { DishId = dish.Id, Quantity = 2 } }).Create(),
            fixture.Build<GuestSession>().With(g => g.IsSubmitted, true)
            .With(g => g.Dishes, new List<OrderItem> { new() { DishId = dish.Id, Quantity = 1 } }).Create()];
        var table = fixture.Create<Table>();
        reservations.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        groups.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync(group);
        sessions.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId)).ReturnsAsync(guests);
        tables.Setup(r => r.GetTableAsync(reservation.TableId)).ReturnsAsync(table);
        dishes.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([dish]);
        dishes.Setup(r => r.GetDishesByIdsAsync(It.IsAny<string[]>())).ReturnsAsync([dish]);
        service = new(reservations.Object, groups.Object, sessions.Object, dishes.Object, orders.Object, tables.Object);
    }

    [Fact]
    public async Task Confirm_CombinesOwnerAndGuestDishes_AndNotifiesWaiter()
    {
        var ownerOrder = new Fixture().Build<Order>().With(o => o.Status, OrderStatus.Created)
            .With(o => o.Dishes, new List<OrderItem> { new() { DishId = dish.Id, Quantity = 1 } }).Create();
        orders.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync(ownerOrder);
        await service.ConfirmAsync(reservation.Id, reservation.CustomerId!);
        groups.Verify(r => r.SaveConfirmedOrderAsync(
            It.Is<GroupReservation>(g => g.Status == GroupReservationStatus.Submitted),
            It.Is<Order>(o => o.Status == OrderStatus.SubmittedByCustomer && o.Dishes.Count == 1 && o.Dishes[0].Quantity == 4),
            It.Is<Notification>(n => n.UserId == reservation.WaiterId && n.OrderId == ownerOrder.OrderId && n.ReservationId == reservation.Id)), Times.Once);
    }

    [Fact]
    public async Task Summary_ReturnsPerGuestAndTableTotals()
    {
        var result = JsonSerializer.SerializeToElement(await service.GetAsync(reservation.Id, reservation.CustomerId!));
        Assert.Equal(37.5m, result.GetProperty("total").GetDecimal());
        Assert.Equal(2, result.GetProperty("participants").GetArrayLength());
        Assert.Equal(25m, result.GetProperty("participants")[0].GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task OtherUser_CannotReadOrConfirm()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() => service.GetAsync(reservation.Id, "other"));
        await Assert.ThrowsAsync<ActionForbiddenException>(() => service.ConfirmAsync(reservation.Id, "other"));
        groups.Verify(r => r.SaveConfirmedOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>(), It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_RejectsUnsubmittedGuests()
    {
        guests[0].IsSubmitted = false;
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(reservation.Id, reservation.CustomerId!));
    }

    [Fact]
    public async Task Confirm_RejectsUnavailableDish()
    {
        dish.State = DishState.OnStop;
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(reservation.Id, reservation.CustomerId!));
        groups.Verify(r => r.SaveConfirmedOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>(), It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_RejectsLateReservation()
    {
        reservation.StartTime = DateTime.UtcNow.AddMinutes(15);
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(reservation.Id, reservation.CustomerId!));
    }

    [Fact]
    public async Task Confirm_RetryDoesNotDuplicateDishes()
    {
        await service.ConfirmAsync(reservation.Id, reservation.CustomerId!);
        await service.ConfirmAsync(reservation.Id, reservation.CustomerId!);
        groups.Verify(r => r.SaveConfirmedOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>(), It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_WaitsForAllSessionsToBeVisible()
    {
        group.JoinedGuestCount = 3;
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(reservation.Id, reservation.CustomerId!));
    }
}
