using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIHandler.Constants;
using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Tables;

namespace APIHandler.Services.GroupReservations;

public class GroupPreOrderService(
    IReservationRepository reservations, IGroupReservationRepository groups,
    IGuestSessionRepository sessions, IDishRepository dishes, IOrderRepository orders,
    ITableRepository tables)
{
    public async Task<Reservation> RequireOwnerAsync(string reservationId, string userId)
    {
        var reservation = await reservations.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException("Reservation not found.");
        if (reservation.CustomerId != userId)
            throw new ActionForbiddenException("Only the reservation owner can manage this group pre-order.");
        return reservation;
    }

    public async Task<object> GetAsync(string reservationId, string userId)
    {
        var reservation = await RequireOwnerAsync(reservationId, userId);
        var group = await groups.GetByReservationIdAsync(reservationId)
            ?? throw new ValidationException("This reservation has no group pre-order.");
        var guests = await sessions.GetAllByGroupReservationIdAsync(group.GroupReservationId);
        var ownerOrder = await orders.GetOrderByReservationIdAsync(reservationId);
        var allItems = guests.SelectMany(g => g.Dishes).ToList();
        if (ownerOrder != null && group.Status == GroupReservationStatus.Open)
            allItems.AddRange(ownerOrder.Dishes);
        var catalog = (await dishes.GetDishesByIdsAsync(allItems.Concat(ownerOrder?.Dishes ?? []).Select(d => d.DishId).Distinct().ToArray()))
            .ToDictionary(d => d.Id);
        object[] Items(System.Collections.Generic.IEnumerable<OrderItem> items) => items.Select(i => (object)new {
            dishId = i.DishId, name = catalog.GetValueOrDefault(i.DishId)?.Name ?? "Unavailable dish",
            quantity = i.Quantity, price = catalog.GetValueOrDefault(i.DishId)?.Price ?? 0,
        }).ToArray();
        decimal Total(System.Collections.Generic.IEnumerable<OrderItem> items) => items.Sum(i => i.Quantity * (catalog.GetValueOrDefault(i.DishId)?.Price ?? 0));
        return new {
            reservationId, startTime = reservation.StartTime, status = group.Status.ToString(),
            participants = guests.Select(g => new { id = g.Id, name = g.GuestName, isSubmitted = g.IsSubmitted,
                items = Items(g.Dishes), total = Total(g.Dishes) }),
            ownerItems = Items(group.Status == GroupReservationStatus.Open ? ownerOrder?.Dishes ?? [] : group.OwnerDishes),
            // After confirmation the order contains the consolidated quantities for the whole table;
            // don't replace that total with only the owner's original selection.
            total = group.Status == GroupReservationStatus.Open ? Total(allItems) : Total(ownerOrder?.Dishes ?? []),
        };
    }

    public async Task ConfirmAsync(string reservationId, string userId)
    {
        var reservation = await RequireOwnerAsync(reservationId, userId);
        var group = await groups.GetByReservationIdAsync(reservationId)
            ?? throw new ValidationException("This reservation has no group pre-order.");
        if (group.Status == GroupReservationStatus.Submitted) return;
        if (group.Status != GroupReservationStatus.Open || reservation.Status != ReservationStatus.Reserved ||
            DateTime.UtcNow >= reservation.StartTime.ToUniversalTime().AddMinutes(-30))
            throw new ValidationException("Group pre-orders close 30 minutes before the reservation.");
        var guests = await sessions.GetAllByGroupReservationIdAsync(group.GroupReservationId);
        if (guests.Any(g => !g.IsSubmitted))
            throw new ValidationException("Wait until every joined guest submits their selection.");
        if (group.JoinedGuestCount.HasValue && guests.Count != group.JoinedGuestCount.Value)
            throw new ValidationException("Guest selections are still syncing. Please refresh and try again.");
        var order = await orders.GetOrderByReservationIdAsync(reservationId);
        if (order != null && order.Status != OrderStatus.Created)
            throw new ValidationException("The existing order is already submitted. Reopen it before confirming the group.");
        var items = guests.SelectMany(g => g.Dishes).Concat(order?.Dishes ?? [])
            .GroupBy(i => i.DishId).Select(g => new OrderItem {
                DishId = g.Key, Quantity = g.Sum(i => i.Quantity), State = OrderItemState.Pending
            }).ToList();
        if (items.Count == 0) throw new ValidationException("Choose at least one dish before confirming.");
        var table = await tables.GetTableAsync(reservation.TableId)
            ?? throw new ValidationException("Table not found.");
        var menu = (await dishes.GetDishesByLocationIdAsync(table.LocationId)).ToDictionary(d => d.Id);
        if (items.Any(i => i.Quantity <= 0 || !menu.TryGetValue(i.DishId, out var dish) || dish.State != DishState.Available))
            throw new ValidationException("One or more selected dishes are no longer available at this restaurant.");
        group.OwnerDishes = order?.Dishes.ToList() ?? [];
        order ??= new Order {
            OrderId = group.GroupReservationId, CustomerId = userId, ReservationId = reservationId,
            ReservationAddress = $"Table {table.TableNumber}", ReservationTime = reservation.StartTime.ToString("O"), Dishes = []
        };
        order.Dishes = items;
        order.Status = OrderStatus.SubmittedByCustomer;
        group.Status = GroupReservationStatus.Submitted;
        var notification = string.IsNullOrEmpty(reservation.WaiterId) ? null : new Notification {
            Id = $"group-pre-order-{group.GroupReservationId}", UserId = reservation.WaiterId,
            OrderId = order.OrderId, ReservationId = reservationId, CreatedAt = DateTime.UtcNow,
            Message = $"Group pre-order confirmed for table {table.TableNumber}, {reservation.StartTime:yyyy-MM-dd HH:mm}: " +
                string.Join(", ", items.Select(i => $"{menu[i.DishId].Name} × {i.Quantity}"))
        };
        await groups.SaveConfirmedOrderAsync(group, order, notification);
    }
}
