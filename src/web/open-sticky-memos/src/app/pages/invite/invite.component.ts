import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { InvitationPublicResponse } from '../../models';

@Component({
  selector: 'app-invite',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="invite-container">
      <div class="invite-card">
        <!-- Loading -->
        @if (loading) {
          <div class="state">
            <div class="spinner"></div>
            <p>Cargando invitación...</p>
          </div>
        }

        <!-- Error / No encontrada -->
        @if (!loading && error) {
          <div class="state error-state">
            <span class="icon">❌</span>
            <h2>Invitación no válida</h2>
            <p>{{ error }}</p>
            <a routerLink="/dashboard" class="btn-primary">Ir al dashboard</a>
          </div>
        }

        <!-- Invitación expirada -->
        @if (!loading && invitation && invitation.isExpired) {
          <div class="state error-state">
            <span class="icon">⏰</span>
            <h2>Invitación expirada</h2>
            <p>Esta invitación ya no es válida. Contacta al creador del proyecto para que te envíe una nueva.</p>
            <a routerLink="/dashboard" class="btn-primary">Ir al dashboard</a>
          </div>
        }

        <!-- Invitación ya aceptada -->
        @if (!loading && invitation && invitation.isAccepted && !invitation.isExpired) {
          <div class="state success-state">
            <span class="icon">✅</span>
            <h2>Invitación ya aceptada</h2>
            <p>Ya eres miembro de este proyecto.</p>
            <a [routerLink]="'/board/' + invitation.projectId" class="btn-primary">Ir al proyecto</a>
          </div>
        }

        <!-- Invitación válida - mostrar detalles -->
        @if (!loading && invitation && !invitation.isExpired && !invitation.isAccepted) {
          <div class="state">
            <span class="icon">📋</span>
            <h2>Invitación a proyecto</h2>
            <div class="invite-details">
              <div class="detail-row">
                <span class="label">Proyecto</span>
                <span class="value">{{ invitation.projectName }}</span>
              </div>
              @if (invitation.projectDescription) {
                <div class="detail-row">
                  <span class="label">Descripción</span>
                  <span class="value">{{ invitation.projectDescription }}</span>
                </div>
              }
              <div class="detail-row">
                <span class="label">Invitado por</span>
                <span class="value">{{ invitation.createdByName }}</span>
              </div>
              <div class="detail-row">
                <span class="label">Vence</span>
                <span class="value">{{ invitation.expiresAt | date:'medium' }}</span>
              </div>
              @if (invitation.invitedEmail) {
                <div class="detail-row">
                  <span class="label">Email</span>
                  <span class="value">{{ invitation.invitedEmail }}</span>
                </div>
              }
            </div>

            @if (!isLoggedIn) {
              <div class="auth-required">
                <p>Debes iniciar sesión para aceptar la invitación.</p>
                <a [routerLink]="'/login'" [queryParams]="{returnUrl: '/invite/' + token}" class="btn-primary">
                  Iniciar sesión
                </a>
                <p class="register-hint">
                  ¿No tienes cuenta? <a [routerLink]="'/login'" [queryParams]="{returnUrl: '/invite/' + token}">Regístrate</a>
                </p>
              </div>
            } @else {
              <button class="btn-primary btn-accept" [disabled]="accepting" (click)="acceptInvitation()">
                {{ accepting ? 'Aceptando...' : '✅ Aceptar invitación' }}
              </button>
              @if (acceptError) {
                <p class="error-msg">{{ acceptError }}</p>
              }
            }
          </div>
        }

        <!-- Aceptada exitosamente -->
        @if (!loading && accepted) {
          <div class="state success-state">
            <span class="icon">🎉</span>
            <h2>¡Invitación aceptada!</h2>
            <p>Ya eres miembro del proyecto <strong>{{ acceptedProjectName }}</strong>.</p>
            <a [routerLink]="'/board/' + acceptedProjectId" class="btn-primary">Ir al proyecto</a>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .invite-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      background: linear-gradient(135deg, #d8e8ff, #f0e8fe, #e0f7fa);
    }
    .invite-card {
      background: rgba(255,255,255,.85);
      backdrop-filter: blur(12px);
      border-radius: 20px;
      padding: 40px;
      width: 100%;
      max-width: 440px;
      box-shadow: 0 20px 60px rgba(0,0,0,.12);
      border: 1px solid rgba(255,255,255,.6);
    }
    .state {
      text-align: center;
    }
    .state .icon {
      font-size: 48px;
      display: block;
      margin-bottom: 12px;
    }
    .state h2 {
      margin: 0 0 8px;
      font-size: 20px;
      color: #2d3748;
    }
    .state p {
      margin: 0 0 20px;
      color: #718096;
      font-size: 14px;
    }
    .error-state h2 { color: #e53e3e; }
    .success-state h2 { color: #38a169; }
    .invite-details {
      text-align: left;
      margin: 16px 0;
      background: #f7fafc;
      border-radius: 12px;
      padding: 16px;
    }
    .detail-row {
      display: flex;
      justify-content: space-between;
      padding: 6px 0;
      font-size: 13px;
      border-bottom: 1px solid #edf2f7;
    }
    .detail-row:last-child { border-bottom: none; }
    .detail-row .label { color: #a0aec0; font-weight: 500; }
    .detail-row .value { color: #2d3748; font-weight: 500; text-align: right; }
    .auth-required {
      margin-top: 16px;
    }
    .auth-required p {
      margin-bottom: 12px;
    }
    .register-hint {
      font-size: 13px;
      margin-top: 12px !important;
    }
    .register-hint a {
      color: #4a90d9;
      cursor: pointer;
      text-decoration: none;
    }
    .register-hint a:hover { text-decoration: underline; }
    .btn-primary {
      display: inline-block;
      padding: 12px 24px;
      border: none;
      border-radius: 10px;
      background: linear-gradient(135deg, #4a90d9, #7c5cbf);
      color: white;
      cursor: pointer;
      font-weight: 600;
      font-size: 14px;
      text-decoration: none;
      transition: opacity .2s;
    }
    .btn-primary:disabled { opacity: .5; cursor: not-allowed; }
    .btn-accept { width: 100%; margin-top: 8px; }
    .error-msg { color: #e53e3e; font-size: 13px; margin-top: 8px !important; }
    .spinner {
      width: 32px;
      height: 32px;
      border: 3px solid #e2e8f0;
      border-top: 3px solid #4a90d9;
      border-radius: 50%;
      animation: spin .8s linear infinite;
      margin: 0 auto 12px;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class InviteComponent implements OnInit {
  token = '';
  invitation: InvitationPublicResponse | null = null;
  loading = true;
  error = '';
  accepting = false;
  acceptError = '';
  accepted = false;
  acceptedProjectId = '';
  acceptedProjectName = '';
  isLoggedIn = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: ApiService,
    private auth: AuthService
  ) {}

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') || '';
    this.isLoggedIn = this.auth.isLoggedIn();

    if (!this.token) {
      this.error = 'Link de invitación inválido.';
      this.loading = false;
      return;
    }

    this.loadInvitation();
  }

  private loadInvitation(): void {
    this.loading = true;
    this.error = '';

    this.api.getInvitationPublicInfo(this.token).subscribe({
      next: (info) => {
        this.invitation = info;
        this.loading = false;
      },
      error: () => {
        this.error = 'No se encontró la invitación o el link es inválido.';
        this.loading = false;
      },
    });
  }

  acceptInvitation(): void {
    if (this.accepting || !this.token) return;

    this.accepting = true;
    this.acceptError = '';

    this.api.acceptInvitation(this.token).subscribe({
      next: (res) => {
        this.accepted = true;
        this.acceptedProjectId = res.projectId;
        this.acceptedProjectName = res.projectName;
        this.accepting = false;
      },
      error: (err) => {
        this.acceptError = err.error?.error || 'Error al aceptar la invitación.';
        this.accepting = false;
      },
    });
  }
}
