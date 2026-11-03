import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CustomButtonComponent } from '@app/shared/custom-components/custom-button/custom-button.component';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { GuestInviteVm } from '@app/pages/guest-pre-order-page/state/guest-preorder.store';

@Component({
    standalone: true,
    selector: 'app-guest-name-modal',
    imports: [ModalComponent, FormsModule, CustomButtonComponent],
    templateUrl: './guest-name-modal.component.html',
    styleUrl: './guest-name-modal.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GuestNameModalComponent {
    readonly invite = input<GuestInviteVm | null>(null);
    readonly busy = input(false);
    readonly error = input<string | null>(null);

    readonly confirmed = output<string>();
    readonly closed = output<void>(); // x / esc / backdrop (we'll ignore this on the page)

    readonly name = signal('');
    readonly touched = signal(false);

    readonly trimmed = computed(() => this.name().trim());
    readonly valid = computed(() => this.trimmed().length > 0 && this.trimmed().length <= 80);

    submit() {
        this.touched.set(true);
        if (!this.valid() || this.busy() || !this.invite()) return;
        this.confirmed.emit(this.trimmed());
    }
}
