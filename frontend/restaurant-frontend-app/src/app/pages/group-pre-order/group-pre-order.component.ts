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
  template: `
    <main>
      <a routerLink="/reservations">← My reservations</a>
      <h1>Group pre-order</h1>
      <p>Invite your guests, review everyone's dishes and confirm one order for the table.</p>
      @if (error()) { <p role="alert">{{ error() }}</p> }
      @if (order(); as group) {
        <p>{{ group.startTime | date:'medium' }} · {{ group.status }}</p>
        @if (group.status === 'Open') {
          <button class="button button--primary" [disabled]="busy()" (click)="invite()">Get invitation link</button>
          @if (inviteLink()) {
            <label for="invite-link">Share this link with your guests</label>
            <input id="invite-link" readonly [value]="inviteLink()" />
            <button class="button" (click)="copyLink()">{{ copied() ? 'Copied!' : 'Copy link' }}</button>
          }
          <p>Guests do not need an account. Confirm at least 30 minutes before arrival.</p>
        }
        <button class="button" [disabled]="busy()" (click)="load()">Refresh selections</button>
        @if (group.ownerItems.length) {
          <section><h2>Your selection</h2>
            @for (item of group.ownerItems; track item.dishId) {
              <p>{{ item.name }} × {{ item.quantity }} — {{ item.price * item.quantity | currency }}</p>
            }
          </section>
        }
        @for (guest of group.participants; track guest.id) {
          <section>
            <h2>{{ guest.name }} · {{ guest.isSubmitted ? 'Ready' : 'Choosing dishes' }}</h2>
            @for (item of guest.items; track item.dishId) {
              <p>{{ item.name }} × {{ item.quantity }} — {{ item.price * item.quantity | currency }}</p>
            } @empty { <p>No dishes selected yet.</p> }
            <strong>Subtotal: {{ guest.total | currency }}</strong>
          </section>
        } @empty { <p>No guests have joined yet. Share the invitation link to get started.</p> }
        <h2>Table total: {{ group.total | currency }}</h2>
        @if (group.status === 'Open') {
          @if (!ready()) { <p>Wait for all joined guests to submit their selections.</p> }
          <button class="button button--primary" [disabled]="busy() || !ready() || group.total <= 0" (click)="confirm()">
            {{ busy() ? 'Please wait…' : 'Confirm group pre-order' }}
          </button>
        } @else if (group.status === 'Submitted') {
          <p role="status">The shared order has been sent to your restaurant.</p>
        }
      } @else if (busy()) { <p role="status">Loading group pre-order…</p> }
      @if (!order() && !busy()) { <button class="button" (click)="load()">Retry</button> }
    </main>
  `,
  styles: [`
    main { max-width: 850px; margin: 2rem auto; padding: 1rem; }
    section { border: 1px solid #ddd; border-radius: 12px; padding: 1rem; margin: 1rem 0; }
    button { margin: .5rem .5rem .5rem 0; }
    label, input { display: block; margin-top: 1rem; }
    input { width: 100%; padding: .75rem; box-sizing: border-box; }
    [role=alert] { color: #a32121; }
  `],
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
