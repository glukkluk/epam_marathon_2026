# Group pre-order

## User flow

1. A signed-in customer books a table for at least two people. The existing reservation service creates the group and invitation token.
2. In **My reservations → Group pre-order**, the owner generates/copies the invitation URL (`/join/:token`).
3. Guests open that public URL, enter a name and select dishes from the reserved restaurant's menu. Each guest receives an independent bearer session token; no Cognito account is needed.
4. Guests submit their selections. Reloading the invitation restores their server-side cart and submission status.
5. The owner uses **Refresh selections** to review each guest, readiness, subtotals and the table total, including their own existing draft order.
6. **Confirm group pre-order** creates a consolidated order for the existing waiter dashboard and an unread waiter notification. Duplicate dishes are combined by quantity. Guest selections remain available in the owner summary.

## API

Both new endpoints require Cognito authentication and enforce reservation ownership:

- `GET /reservations/{reservationId}/group-pre-order`: returns `reservationId`, `startTime`, string `status`, `participants[]` (`id`, `name`, `isSubmitted`, `items`, `total`), `ownerItems`, `total`.
- `POST /reservations/{reservationId}/group-pre-order`: confirms and returns the same summary; body `{}`.

Each summary item has `dishId`, `name`, `quantity`, `price`. Amounts are calculated server-side from the catalog. Existing guest endpoints and invitation endpoints are reused; invitation-token retrieval now checks ownership.

Confirmation requires an active reservation more than 30 minutes away, all joined guests to have submitted, and at least one available dish from this restaurant. The owner's existing order must still be a draft. Validation failures return 400, unauthenticated requests 401, another owner's requests 403, missing reservations 404, and concurrent transaction conflicts 409 (refresh/retry).

Group state, consolidated order and waiter notification are saved in one DynamoDB transaction. The group version guards concurrent confirmation/join operations. Guest creation/removal updates an atomic participant counter with the session in a transaction, preventing concurrent joins from exceeding capacity. Existing group records without a counter initialize it from their sessions. New nullable attributes require no table migration. GSI propagation is checked against the participant counter before confirmation.

## Deployment and verification

Deploy the backend including the updated API Gateway resources in `deployment_resources.json`, then build/deploy the frontend using the normal README procedure.

```sh
dotnet test Tests/APIHandler.Tests.csproj
```

From `frontend/restaurant-frontend-app`:

```sh
npx ng build --configuration development
```

For an end-to-end check against a deployed API, book a future table for three; join from two separate browser profiles, submit different quantities of the same dish, refresh the owner summary and confirm. Verify the combined quantity in the assigned waiter's dashboard/notification, guest restoration after reload, refusal of another owner's access, and refusal of late/unavailable/unsubmitted selections.
