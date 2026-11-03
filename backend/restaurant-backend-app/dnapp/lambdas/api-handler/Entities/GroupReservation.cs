using Amazon.DynamoDBv2.DataModel;

namespace APIHandler.Entities;

public class GroupReservation
{
    [DynamoDBVersion]
    public int? Version { get; set; }

    [DynamoDBProperty("joinedGuestCount")]
    public int? JoinedGuestCount { get; set; }

    [DynamoDBProperty("ownerDishes")]
    public System.Collections.Generic.List<OrderItem> OwnerDishes { get; set; } = [];

    [DynamoDBHashKey("groupReservationId")]
    public required string GroupReservationId { get; set; }

    [DynamoDBProperty("reservationId")]
    [DynamoDBGlobalSecondaryIndexHashKey("reservationId")]
    public required string ReservationId { get; set; }

    [DynamoDBProperty("reservationToken")]
    [DynamoDBGlobalSecondaryIndexHashKey("reservationToken")]
    public required string ReservationToken { get; set; }

    [DynamoDBProperty("status")]
    public GroupReservationStatus Status { get; set; }
}
