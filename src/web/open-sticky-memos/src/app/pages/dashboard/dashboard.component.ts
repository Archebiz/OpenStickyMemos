import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService, UserInfo } from '../../core/auth.service';
import { SignalRService } from '../../core/signalr.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="dashboard">
      <header class="header">
        <div class="header-left">
          <h2>OpenStickyMemos</h2>
        </div>
        <div class="header-right">
          <div class="user-info" *ngIf="user">
            <img
              *ngIf="user.avatarUrl"
              [src]="user.avatarUrl"
              alt="Avatar"
              class="avatar"
            />
            <span>{{ user.displayName }}</span>
          </div>
          <button class="btn-logout" (click)="logout()">Cerrar sesión</button>
        </div>
      </header>

      <main class="content">
        <div class="empty-state">
          <h1>📋</h1>
          <h3>Tus proyectos aparecerán aquí</h3>
          <p>Próximamente: lista de proyectos y board de notas</p>
        </div>
      </main>
    </div>
  `,
  styles: [
    `
      .dashboard {
        min-height: 100vh;
        background: #f5f5f5;
      }
      .header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 12px 24px;
        background: white;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
      }
      .header-left h2 {
        margin: 0;
        font-size: 18px;
        color: #1a1a2e;
      }
      .header-right {
        display: flex;
        align-items: center;
        gap: 16px;
      }
      .user-info {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 14px;
        color: #555;
      }
      .avatar {
        width: 32px;
        height: 32px;
        border-radius: 50%;
        object-fit: cover;
      }
      .btn-logout {
        padding: 6px 16px;
        border: 1px solid #ddd;
        border-radius: 6px;
        background: white;
        cursor: pointer;
        font-size: 13px;
        color: #666;
      }
      .btn-logout:hover {
        background: #f5f5f5;
      }
      .content {
        padding: 48px 24px;
        max-width: 1200px;
        margin: 0 auto;
      }
      .empty-state {
        text-align: center;
        padding: 80px 24px;
        color: #888;
      }
      .empty-state h1 {
        font-size: 64px;
        margin: 0;
      }
      .empty-state h3 {
        margin: 16px 0 8px;
        color: #555;
      }
      .empty-state p {
        margin: 0;
        color: #999;
      }
    `,
  ],
})
export class DashboardComponent implements OnInit {
  user: UserInfo | null = null;

  constructor(
    private authService: AuthService,
    private signalR: SignalRService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.user = this.authService.getStoredUser();
    this.signalR.start();
  }

  logout(): void {
    this.signalR.stop();
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
