import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { AppConfigService } from '../../core/app-config.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="fp-container">
      <div class="fp-card">
        <div class="logo">
          <span class="icon">📝</span>
          <h1>OpenStickyMemos</h1>
        </div>

        <!-- Paso 1: Solicitar reset -->
        @if (!hasToken && !emailSent) {
          <h2>¿Olvidaste tu contraseña?</h2>
          <p class="desc">Ingresa tu correo y te enviaremos un enlace para restablecerla.</p>
          <form (ngSubmit)="onRequestReset()" class="fp-form">
            <input
              [(ngModel)]="email" name="email" type="email"
              placeholder="Correo electrónico" class="input" required />
            <button type="submit" class="btn-primary" [disabled]="loading || !email">
              {{ loading ? 'Enviando...' : 'Enviar enlace de recuperación' }}
            </button>
          </form>
          @if (error) { <p class="error-msg">{{ error }}</p> }
          <a routerLink="/login" class="back-link">← Volver al inicio de sesión</a>
        }

        <!-- Paso 1b: Confirmación de envío -->
        @if (emailSent) {
          <div class="success-state">
            <div class="success-icon">✅</div>
            <h2>Revisa tu correo</h2>
            <p>Si existe una cuenta con <strong>{{ email }}</strong>,
            recibirás un enlace para restablecer tu contraseña.</p>
            <p class="small">El enlace expira en 1 hora. Si no lo ves,
            revisa tu carpeta de spam.</p>
            <a routerLink="/login" class="back-link">← Volver al inicio de sesión</a>
          </div>
        }

        <!-- Paso 2: Establecer nueva contraseña (cuando hay token en URL) -->
        @if (hasToken) {
          <h2>Establece tu nueva contraseña</h2>
          <p class="desc">Ingresa una nueva contraseña para tu cuenta.</p>
          <form (ngSubmit)="onResetPassword()" class="fp-form">
            <input
              [(ngModel)]="newPassword" name="np" type="password"
              placeholder="Nueva contraseña (mín. 6 caracteres)" class="input" required
              minlength="6" />
            <input
              [(ngModel)]="confirmPassword" name="cp" type="password"
              placeholder="Confirmar contraseña" class="input" required />
            <button type="submit" class="btn-primary" [disabled]="loading || !canSubmit">
              {{ loading ? 'Guardando...' : 'Cambiar contraseña' }}
            </button>
          </form>
          @if (error) { <p class="error-msg">{{ error }}</p> }
          @if (resetSuccess) {
            <div class="success-state">
              <div class="success-icon">✅</div>
              <h2>Contraseña actualizada</h2>
              <p>Tu contraseña se ha cambiado exitosamente. Todas tus sesiones han sido cerradas por seguridad.</p>
              <a routerLink="/login" class="back-link">← Ir al inicio de sesión</a>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .fp-container { display:flex; justify-content:center; align-items:center; min-height:100vh; background:linear-gradient(135deg,#667eea,#764ba2); }
    .fp-card { background:white; border-radius:16px; padding:40px; width:100%; max-width:420px; box-shadow:0 20px 60px rgba(0,0,0,.3); text-align:center; }
    .fp-card h2 { font-size:20px; color:#333; margin:0 0 8px 0; }
    .desc { font-size:13px; color:#888; margin:0 0 20px 0; line-height:1.5; }
    .fp-form { display:flex; flex-direction:column; gap:10px; margin-bottom:8px; }
    .input { padding:12px 14px; border:1px solid #ddd; border-radius:8px; font-size:14px; }
    .input:focus { outline:none; border-color:#667eea; }
    .btn-primary { padding:12px; border:none; border-radius:8px; background:#667eea; color:white; cursor:pointer; font-size:14px; font-weight:600; }
    .btn-primary:disabled { opacity:.5; }
    .btn-primary:hover:not(:disabled) { background:#5a6fd6; }
    .error-msg { color:#E74C3C; font-size:13px; margin:8px 0; }
    .back-link { display:inline-block; margin-top:16px; color:#667eea; font-size:13px; text-decoration:none; }
    .back-link:hover { text-decoration:underline; }
    .success-state { padding:10px 0; }
    .success-icon { font-size:48px; margin-bottom:12px; }
    .success-state p { font-size:13px; color:#555; line-height:1.6; margin:8px 0; }
    .success-state .small { font-size:12px; color:#999; }
  `]
})
export class ForgotPasswordComponent implements OnInit {
  // Paso 1: solicitar reset
  email = '';
  emailSent = false;

  // Paso 2: reset con token
  hasToken = false;
  token = '';
  newPassword = '';
  confirmPassword = '';
  resetSuccess = false;

  error = '';
  loading = false;

  private get apiUrl() { return this.config.apiUrl; }

  constructor(
    private http: HttpClient,
    private config: AppConfigService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Verificar si hay token en la URL
    this.route.queryParams.subscribe(params => {
      this.token = params['token'] || '';
      this.hasToken = !!this.token;
    });
  }

  get canSubmit(): boolean {
    return this.newPassword.length >= 6 && this.newPassword === this.confirmPassword;
  }

  onRequestReset(): void {
    if (!this.email) return;
    this.loading = true;
    this.error = '';

    this.http.post<any>(`${this.apiUrl}/auth/forgot-password`, { email: this.email })
      .subscribe({
        next: (res) => {
          this.emailSent = true;
          this.loading = false;

          // Si hay debugResetLink (desarrollo local), mostrar en consola
          if (res.debugResetLink) {
            console.log('[ForgotPassword] Debug reset link:', res.debugResetLink);
          }
        },
        error: (e) => {
          // Siempre mostrar mensaje genérico por seguridad (no revelar si el email existe)
          this.emailSent = true;
          this.loading = false;
        }
      });
  }

  onResetPassword(): void {
    if (!this.canSubmit) {
      this.error = 'Las contraseñas no coinciden o son muy cortas';
      return;
    }

    this.loading = true;
    this.error = '';

    this.http.post<any>(`${this.apiUrl}/auth/reset-password`, {
      token: this.token,
      newPassword: this.newPassword
    }).subscribe({
      next: () => {
        this.resetSuccess = true;
        this.loading = false;
      },
      error: (e) => {
        this.error = e.error?.error || 'Error al restablecer la contraseña. El token puede haber expirado.';
        this.loading = false;
      }
    });
  }
}
