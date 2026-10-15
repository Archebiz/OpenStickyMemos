import {
  Component,
  OnInit,
  OnDestroy,
  HostListener,
  ElementRef,
  ChangeDetectorRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { ApiService } from '../core/api.service';
import { SignalRService } from '../core/signalr.service';
import { NotificationResponse } from '../models';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="bell-container">
      <button
        class="bell-btn"
        (click)="toggleDropdown($event)"
        title="Notificaciones y menciones"
        [class.has-unread]="unreadCount > 0"
      >
        <span class="bell-icon">🔔</span>
        <span *ngIf="unreadCount > 0" class="badge">
          {{ unreadCount > 9 ? '9+' : unreadCount }}
        </span>
      </button>

      <div *ngIf="isOpen" class="dropdown" (click)="$event.stopPropagation()">
        <div class="dropdown-header">
          <div class="title-row">
            <h4>Notificaciones</h4>
            <span *ngIf="unreadCount > 0" class="unread-pill">{{ unreadCount }} nuevas</span>
          </div>
          <button
            *ngIf="unreadCount > 0"
            class="btn-mark-all"
            (click)="markAllAsRead()"
          >
            Marcar todas como leídas
          </button>
        </div>

        <div class="dropdown-body">
          <div *ngIf="loading" class="loading-state">
            Cargando notificaciones...
          </div>

          <div *ngIf="!loading && notifications.length === 0" class="empty-state">
            <span class="empty-icon">🎉</span>
            <p>No tienes notificaciones pendientes</p>
          </div>

          <div
            *ngFor="let item of notifications"
            class="notification-item"
            [class.unread]="!item.isRead"
            (click)="onNotificationClick(item)"
          >
            <div class="avatar-col">
              <img
                *ngIf="item.authorAvatar"
                [src]="item.authorAvatar"
                alt="Avatar"
                class="item-avatar"
              />
              <div *ngIf="!item.authorAvatar" class="avatar-fallback">
                {{ (item.authorName || 'U').charAt(0).toUpperCase() }}
              </div>
            </div>

            <div class="content-col">
              <div class="item-message">
                <strong>{{ item.authorName }}</strong> te mencionó en
                <span class="note-highlight">{{ item.noteTitle || 'una nota' }}</span>
              </div>
              <div class="item-meta">
                <span class="project-tag">📁 {{ item.projectName }}</span>
                <span class="time-tag">{{ formatTime(item.createdAt) }}</span>
              </div>
            </div>

            <div class="unread-dot" *ngIf="!item.isRead"></div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .bell-container {
        position: relative;
        display: inline-block;
      }
      .bell-btn {
        position: relative;
        background: transparent;
        border: none;
        cursor: pointer;
        font-size: 18px;
        padding: 6px 10px;
        border-radius: 8px;
        display: flex;
        align-items: center;
        justify-content: center;
        transition: background 0.15s, transform 0.15s;
      }
      .bell-btn:hover {
        background: rgba(0, 0, 0, 0.05);
      }
      .bell-btn.has-unread {
        animation: bellShake 3s infinite ease-in-out;
      }
      @keyframes bellShake {
        0%, 85%, 100% { transform: rotate(0); }
        88% { transform: rotate(-12deg); }
        92% { transform: rotate(12deg); }
        96% { transform: rotate(-8deg); }
      }
      .badge {
        position: absolute;
        top: 2px;
        right: 4px;
        background: #ef4444;
        color: white;
        font-size: 10px;
        font-weight: 700;
        padding: 1px 5px;
        border-radius: 10px;
        min-width: 16px;
        text-align: center;
        line-height: 1.2;
        box-shadow: 0 1px 4px rgba(239, 68, 68, 0.5);
      }
      .dropdown {
        position: absolute;
        right: 0;
        top: calc(100% + 8px);
        width: 330px;
        max-height: 440px;
        background: white;
        border-radius: 12px;
        box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.15), 0 8px 10px -6px rgba(0, 0, 0, 0.1);
        border: 1px solid #e2e8f0;
        z-index: 1000;
        display: flex;
        flex-direction: column;
        overflow: hidden;
        animation: fadeInDown 0.18s ease-out;
      }
      @keyframes fadeInDown {
        from {
          opacity: 0;
          transform: translateY(-8px);
        }
        to {
          opacity: 1;
          transform: translateY(0);
        }
      }
      .dropdown-header {
        padding: 12px 14px;
        border-bottom: 1px solid #f1f5f9;
        display: flex;
        flex-direction: column;
        gap: 6px;
        background: #f8fafc;
      }
      .title-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
      }
      .title-row h4 {
        margin: 0;
        font-size: 14px;
        font-weight: 600;
        color: #0f172a;
      }
      .unread-pill {
        background: #eff6ff;
        color: #2563eb;
        font-size: 11px;
        font-weight: 600;
        padding: 2px 7px;
        border-radius: 12px;
        border: 1px solid #bfdbfe;
      }
      .btn-mark-all {
        background: none;
        border: none;
        padding: 0;
        color: #6366f1;
        font-size: 11px;
        font-weight: 500;
        cursor: pointer;
        text-align: left;
      }
      .btn-mark-all:hover {
        text-decoration: underline;
      }
      .dropdown-body {
        overflow-y: auto;
        max-height: 360px;
      }
      .loading-state,
      .empty-state {
        padding: 30px 16px;
        text-align: center;
        color: #64748b;
        font-size: 13px;
      }
      .empty-icon {
        font-size: 32px;
        display: block;
        margin-bottom: 6px;
      }
      .notification-item {
        display: flex;
        align-items: flex-start;
        gap: 10px;
        padding: 10px 14px;
        border-bottom: 1px solid #f1f5f9;
        cursor: pointer;
        transition: background 0.15s;
        position: relative;
      }
      .notification-item:hover {
        background: #f8fafc;
      }
      .notification-item.unread {
        background: #f0fdf4;
      }
      .item-avatar {
        width: 32px;
        height: 32px;
        border-radius: 50%;
        object-fit: cover;
      }
      .avatar-fallback {
        width: 32px;
        height: 32px;
        border-radius: 50%;
        background: #6366f1;
        color: white;
        display: flex;
        align-items: center;
        justify-content: center;
        font-weight: 600;
        font-size: 13px;
      }
      .content-col {
        flex: 1;
        min-width: 0;
      }
      .item-message {
        font-size: 12px;
        color: #334155;
        line-height: 1.4;
      }
      .note-highlight {
        color: #4f46e5;
        font-weight: 500;
      }
      .item-meta {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-top: 4px;
        font-size: 11px;
        color: #94a3b8;
      }
      .project-tag {
        background: #f1f5f9;
        padding: 1px 6px;
        border-radius: 4px;
        color: #475569;
        max-width: 140px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .unread-dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: #10b981;
        margin-top: 6px;
      }
    `,
  ],
})
export class NotificationBellComponent implements OnInit, OnDestroy {
  isOpen = false;
  unreadCount = 0;
  notifications: NotificationResponse[] = [];
  loading = false;

  private destroy$ = new Subject<void>();

  constructor(
    private api: ApiService,
    private signalR: SignalRService,
    private router: Router,
    private elementRef: ElementRef,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadUnreadCount();

    // Notificaciones recibidas en tiempo real
    this.signalR.notificationReceived$
      .pipe(takeUntil(this.destroy$))
      .subscribe((notif) => {
        this.notifications = [notif, ...this.notifications];
        this.unreadCount++;
        this.cdr.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.isOpen = false;
    }
  }

  toggleDropdown(event: MouseEvent): void {
    event.stopPropagation();
    this.isOpen = !this.isOpen;
    if (this.isOpen) {
      this.loadNotifications();
    }
  }

  loadUnreadCount(): void {
    this.api
      .getUnreadNotificationCount()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.unreadCount = res.count;
          this.cdr.markForCheck();
        },
        error: () => {},
      });
  }

  loadNotifications(): void {
    this.loading = true;
    this.api
      .getNotifications(25)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (items) => {
          this.notifications = items;
          this.unreadCount = items.filter((n) => !n.isRead).length;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  markAllAsRead(): void {
    this.api
      .markAllNotificationsAsRead()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.notifications = this.notifications.map((n) => ({ ...n, isRead: true }));
          this.unreadCount = 0;
          this.cdr.markForCheck();
        },
      });
  }

  onNotificationClick(notif: NotificationResponse): void {
    if (!notif.isRead) {
      this.api.markNotificationAsRead(notif.id).subscribe();
      notif.isRead = true;
      this.unreadCount = Math.max(0, this.unreadCount - 1);
    }
    this.isOpen = false;

    // Navegar directamente al proyecto y pasar el noteId como queryParam para resaltar
    const queryParams: any = {};
    if (notif.noteId) {
      queryParams.noteId = notif.noteId;
    }
    this.router.navigate(['/board', notif.projectId], { queryParams });
  }

  formatTime(dateStr: string): string {
    try {
      const date = new Date(dateStr);
      const diffMs = Date.now() - date.getTime();
      const diffSec = Math.floor(diffMs / 1000);
      if (diffSec < 60) return 'hace un momento';
      const diffMin = Math.floor(diffSec / 60);
      if (diffMin < 60) return `hace ${diffMin} min`;
      const diffH = Math.floor(diffMin / 60);
      if (diffH < 24) return `hace ${diffH} h`;
      const diffD = Math.floor(diffH / 24);
      return `hace ${diffD} d`;
    } catch {
      return '';
    }
  }
}
