import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GuestCartItem } from '@app/pages/guest-pre-order-page/state/guest-preorder.store';

@Component({
    standalone: true,
    selector: 'app-guest-preorder-submitted',
    templateUrl: './guest-preorder-submitted.component.html',
    styleUrl: './guest-preorder-submitted.component.scss',
    imports: [CurrencyPipe, DatePipe],
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GuestPreorderSubmittedComponent {
    readonly invite = input<GuestInvite | null>(null);
    readonly guestName = input('');
    readonly items = input<GuestCartItem[]>([]);
    readonly total = input(0);

}
