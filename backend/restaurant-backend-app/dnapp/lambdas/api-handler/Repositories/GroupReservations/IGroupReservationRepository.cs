using APIHandler.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GroupReservations;

public interface IGroupReservationRepository
{
    Task<GroupReservation?> GetByReservationIdAsync(string reservationId);
    Task<GroupReservation?> GetGroupReservationByIdAsync(string groupReservationId);
    Task DeleteGroupReservationAsync(GroupReservation groupReservation);
    Task SaveGroupReservationAsync(GroupReservation groupReservation);
    Task SaveConfirmedOrderAsync(GroupReservation groupReservation, Order order, Notification? notification);
    Task SaveGuestAsync(GroupReservation groupReservation, GuestSession session, bool delete = false);
    Task<GroupReservation?> GetGroupReservationByTokenAsync(string token);
}
