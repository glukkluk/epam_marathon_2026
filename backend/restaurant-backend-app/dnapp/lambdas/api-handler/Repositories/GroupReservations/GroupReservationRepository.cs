using Amazon.DynamoDBv2.DataModel;
using APIHandler.Constants;
using APIHandler.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GroupReservations;

public class GroupReservationRepository : IGroupReservationRepository
{
    private static readonly string GroupReservationsTableName = ResourceNames.GroupReservationsTableName;
    private readonly IDynamoDBContext _dynamoDbContext;

    public GroupReservationRepository(IDynamoDBContext dynamoDbContext)
    {
        _dynamoDbContext = dynamoDbContext ?? throw new ArgumentNullException(nameof(dynamoDbContext));
    }

    public async Task<GroupReservation?> GetGroupReservationByIdAsync(string groupReservationId)
    {
        return await _dynamoDbContext.LoadAsync<GroupReservation>(groupReservationId, DynamoConfig.LoadConfig(GroupReservationsTableName));
    }

    public async Task DeleteGroupReservationAsync(GroupReservation groupReservation)
    {
        await _dynamoDbContext.DeleteAsync(groupReservation, DynamoConfig.DeleteConfig(GroupReservationsTableName));
    }

    public async Task SaveGroupReservationAsync(GroupReservation groupReservation)
    {
        await _dynamoDbContext.SaveAsync(groupReservation, DynamoConfig.SaveConfig(GroupReservationsTableName));
    }

    public async Task SaveConfirmedOrderAsync(GroupReservation groupReservation, Order order, Notification? notification)
    {
        var groupWrite = _dynamoDbContext.CreateTransactWrite<GroupReservation>(new TransactWriteConfig { OverrideTableName = GroupReservationsTableName });
        var orderWrite = _dynamoDbContext.CreateTransactWrite<Order>(new TransactWriteConfig { OverrideTableName = ResourceNames.OrdersTableName });
        groupWrite.AddSaveItem(groupReservation);
        orderWrite.AddSaveItem(order);
        if (notification is null)
            await _dynamoDbContext.ExecuteTransactWriteAsync([groupWrite, orderWrite]);
        else
        {
            var notificationWrite = _dynamoDbContext.CreateTransactWrite<Notification>(new TransactWriteConfig { OverrideTableName = ResourceNames.NotificationsTableName });
            notificationWrite.AddSaveItem(notification);
            await _dynamoDbContext.ExecuteTransactWriteAsync([groupWrite, orderWrite, notificationWrite]);
        }
    }

    public async Task SaveGuestAsync(GroupReservation groupReservation, GuestSession session, bool delete = false)
    {
        var groupWrite = _dynamoDbContext.CreateTransactWrite<GroupReservation>(new TransactWriteConfig { OverrideTableName = GroupReservationsTableName });
        var guestWrite = _dynamoDbContext.CreateTransactWrite<GuestSession>(new TransactWriteConfig { OverrideTableName = ResourceNames.GuestSessionTableName });
        groupWrite.AddSaveItem(groupReservation);
        if (delete) guestWrite.AddDeleteItem(session);
        else guestWrite.AddSaveItem(session);
        await _dynamoDbContext.ExecuteTransactWriteAsync([groupWrite, guestWrite]);
    }

    public async Task<GroupReservation?> GetGroupReservationByTokenAsync(string token)
    {
        var results = await _dynamoDbContext.QueryAsync<GroupReservation?>(token, new QueryConfig
        {
            OverrideTableName = GroupReservationsTableName,
            IndexName = GlobalSecondaryIndexes.GroupReservation.ReservationToken,
        }).GetRemainingAsync();

        return results.FirstOrDefault();
    }

    public async Task<GroupReservation?> GetByReservationIdAsync(string reservationId)
    {
        var results = await _dynamoDbContext.QueryAsync<GroupReservation?>(reservationId, new QueryConfig
        {
            OverrideTableName = GroupReservationsTableName,
            IndexName = GlobalSecondaryIndexes.GroupReservation.ReservationId,
        }).GetRemainingAsync();

        return results.FirstOrDefault();
    }
}
