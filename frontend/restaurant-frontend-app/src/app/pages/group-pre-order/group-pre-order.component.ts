import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';

interface Item { dishId: string; name: string; quantity: number; price: number }
interface GroupOrder {
  reservationId: string;
  startTime: string;
  status: string;
  participants: { id: string; name: string; isSubmitted: boolean; items: Item[]; total: number }[];
  ownerItems: Item[];
  total: number;
}

@Component({
  selector: 'app-group-pre-order',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './group-pre-order.component.html',
  styleUrl: './group-pre-order.component.scss',
})
export class GroupPreOrderComponent {
  private readonly http = inject(HttpClient);
  private readonly invites = inject(GuestInviteService);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('reservationId')!;
  private readonly url = `${inject(API_BASE_URL)}/reservations/${encodeURIComponent(this.id)}/group-pre-order`;
  readonly order = signal<GroupOrder | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly inviteLink = signal('');
  readonly copied = signal(false);
  readonly ready = computed(() => this.order()?.participants.every(g => g.isSubmitted) ?? false);
  readonly ownerTotal = computed(() => this.order()?.ownerItems.reduce((sum, item) => sum + item.price * item.quantity, 0) ?? 0);
  readonly guestsTotal = computed(() => this.order()?.participants.reduce((sum, guest) => sum + guest.total, 0) ?? 0);
  readonly readyCount = computed(() => this.order()?.participants.filter(guest => guest.isSubmitted).length ?? 0);

  constructor() { void this.load(); }

  async load() { await this.request(false); }
  async confirm() { await this.request(true); }

  private async request(confirm: boolean) {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      this.order.set(await firstValueFrom(confirm
        ? this.http.post<GroupOrder>(this.url, {}) : this.http.get<GroupOrder>(this.url)));
    } catch (error) { this.showError(error); }
    finally { this.busy.set(false); }
  }

  async invite() {
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await firstValueFrom(this.invites.getInviteToken$(this.id));
      this.inviteLink.set(new URL(`join/${encodeURIComponent(result.token)}`, document.baseURI).href);
    } catch (error) { this.showError(error); }
    finally { this.busy.set(false); }
  }

  async copyLink() {
    try { await navigator.clipboard.writeText(this.inviteLink()); this.copied.set(true); }
    catch { this.error.set('Please select and copy the invitation link above.'); }
  }

  private showError(error: unknown) {
    this.error.set(error instanceof HttpErrorResponse && typeof error.error?.error === 'string'
      ? error.error.error : 'Could not load the group pre-order. Please try again.');
  }
}
