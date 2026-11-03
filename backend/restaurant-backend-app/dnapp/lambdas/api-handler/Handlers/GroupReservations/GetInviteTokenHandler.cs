using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using System;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

public class GetInviteTokenHandler(IGroupReservationService service, GroupPreOrderService preOrders) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("reservationId", out string? reservationId))
            return ResponseBuilder.CreateErrorResponse(404, "Reservation was not found", context.Logger);

        try
        {
            var userId = UserContextHelper.GetAuthenticatedUserId(request);
            if (string.IsNullOrEmpty(userId))
                return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);
            await preOrders.RequireOwnerAsync(reservationId, userId);
            GetGroupInviteTokenResponse result = await service.GetGroupReservationInviteTokenAsync(reservationId);
            return ResponseBuilder.CreateJsonResponse(200, result);
        }
        catch (ActionForbiddenException exception)
        {
            return ResponseBuilder.CreateErrorResponse(403, exception.Message, context.Logger);
        }
        catch (ReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger, exception);
        }
        catch(ValidationException exception)
        {
            return ResponseBuilder.CreateErrorCodeResponse(exception);
        }
    }   
}
