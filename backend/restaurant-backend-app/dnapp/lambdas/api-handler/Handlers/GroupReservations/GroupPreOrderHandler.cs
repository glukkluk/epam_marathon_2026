using System;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Services.GroupReservations;

namespace APIHandler.Handlers.GroupReservations;

public class GroupPreOrderHandler(GroupPreOrderService service) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            var userId = UserContextHelper.GetAuthenticatedUserId(request)
                ?? throw new UnauthorizedAccessException("Sign in to manage your group pre-order.");
            var id = request.PathParameters["reservationId"];
            if (request.HttpMethod == "POST") await service.ConfirmAsync(id, userId);
            return ResponseBuilder.CreateJsonResponse(200, await service.GetAsync(id, userId));
        }
        catch (UnauthorizedAccessException ex) { return ResponseBuilder.CreateErrorResponse(401, ex.Message, context.Logger); }
        catch (ActionForbiddenException ex) { return ResponseBuilder.CreateErrorResponse(403, ex.Message, context.Logger); }
        catch (ReservationNotFoundException ex) { return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger); }
        catch (ValidationException ex) { return ResponseBuilder.CreateErrorResponse(400, ex.Message, context.Logger); }
        catch (Amazon.DynamoDBv2.Model.TransactionCanceledException) { return ResponseBuilder.CreateErrorResponse(409, "The group changed. Refresh and try again.", context.Logger); }
    }
}
