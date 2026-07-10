import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="login-container">
      <div class="login-card">
        <div class="logo">
          <img src="assets/images/logo-nbg.png" alt="OpenStickyMemos" class="logo-img" />
        </div>
        @if (isLogin) {
          <form (ngSubmit)="onLogin()" class="auth-form">
            <input [(ngModel)]="email" name="e" type="email" placeholder="Correo" class="input" required />
            <input [(ngModel)]="password" name="p" type="password" placeholder="Contrasena" class="input" required />
            <a routerLink="/forgot-password" class="forgot-link">¿Olvidaste tu contraseña?</a>
            <button type="submit" class="btn-primary" [disabled]="loading">{{ loading ? '...' : 'Iniciar sesion' }}</button>
          </form>
          <p class="switch-text">No tienes cuenta? <a (click)="isLogin = false">Registrate</a></p>
        } @else {
          <form (ngSubmit)="onRegister()" class="auth-form">
            <input [(ngModel)]="displayName" name="dn" type="text" placeholder="Nombre" class="input" />
            <input [(ngModel)]="email" name="e2" type="email" placeholder="Correo" class="input" required />
            <input [(ngModel)]="password" name="p2" type="password" placeholder="Contrasena" class="input" required />
            <button type="submit" class="btn-primary" [disabled]="loading">{{ loading ? '...' : 'Crear cuenta' }}</button>
          </form>
          <p class="switch-text">Ya tienes cuenta? <a (click)="isLogin = true">Inicia sesion</a></p>
        }
        @if (error) { <p class="error-msg">{{ error }}</p> }
        <div class="divider"><span>o</span></div>
        <div class="providers">
          <button (click)="loginGoogle()" class="btn-google">
            <svg viewBox="0 0 48 48" width="20" height="20">
              <path fill="#4285F4" d="M45.12 24.5c0-1.56-.14-3.06-.4-4.5H24v8.51h11.84a10.12 10.12 0 0 1-4.4 6.63l.02.01 6.42 4.98.24.1A20.83 20.83 0 0 0 45.12 24.5Z"/>
              <path fill="#34A853" d="M24 46.12c5.76 0 10.6-1.9 14.12-5.16l-6.66-5.17a10.52 10.52 0 0 1-7.46 2.54 10.53 10.53 0 0 1-9.92-7.2l-.2-.01-6.55 5.07-.17.19a20.86 20.86 0 0 0 16.84 9.74Z"/>
              <path fill="#FBBC05" d="M14.08 28.87a10.52 10.52 0 0 1-.56-3.37c0-1.17.2-2.3.55-3.37l-.01-.14-6.64-5.15-.16.08a20.87 20.87 0 0 0 0 17.16l6.82-4.21Z"/>
              <path fill="#EA4335" d="M24 12.71a10.57 10.57 0 0 1 7.48 2.92l5.44-5.44A18.75 18.75 0 0 0 24 3.87a20.88 20.88 0 0 0-16.86 9.56l6.68 4.2a10.52 10.52 0 0 1 10.18-4.92Z"/>
            </svg>
            Continuar con Google
          </button>
          <button (click)="loginMicrosoft()" class="btn-microsoft">
            <svg viewBox="0 0 21 21" width="20" height="20">
              <rect x="1" y="1" width="9" height="9" fill="#F25022" rx="1"/>
              <rect x="11" y="1" width="9" height="9" fill="#7FBA00" rx="1"/>
              <rect x="1" y="11" width="9" height="9" fill="#00A4EF" rx="1"/>
              <rect x="11" y="11" width="9" height="9" fill="#FFB900" rx="1"/>
            </svg>
            Continuar con Microsoft
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-container { display:flex; justify-content:center; align-items:center; min-height:100vh; background:linear-gradient(135deg,#d8e8ff,#f0e8fe,#e0f7fa); }
    .login-card { background:rgba(255,255,255,.85); backdrop-filter:blur(12px); border-radius:20px; padding:40px; width:100%; max-width:420px; box-shadow:0 20px 60px rgba(0,0,0,.12); text-align:center; border:1px solid rgba(255,255,255,.6); }
    .logo-img { max-width:220px; height:auto; margin-bottom:24px; }
    .auth-form { display:flex; flex-direction:column; gap:10px; margin-bottom:8px; }
    .input { padding:10px 12px; border:1px solid rgba(176,196,222,.5); border-radius:10px; font-size:14px; background:rgba(255,255,255,.6); color:#2d3748; transition:all .2s; }
    .input:focus { outline:none; border-color:rgba(255,255,255,.7); background:rgba(255,255,255,.9); box-shadow:0 0 0 3px rgba(74,144,217,.15); }
    .input::placeholder { color:#a0aec0; }
    .btn-primary { padding:10px; border:none; border-radius:10px; background:linear-gradient(135deg,#4a90d9,#7c5cbf); color:white; cursor:pointer; font-weight:600; transition:opacity .2s; }
    .btn-primary:disabled { opacity:.5; }
    .forgot-link { font-size:12px; color:#a0aec0; text-decoration:none; text-align:right; margin:-2px 0 6px 0; cursor:pointer; display:block; }
    .forgot-link:hover { text-decoration:underline; }
    .switch-text { font-size:13px; color:#718096; margin:6px 0; }
    .switch-text a { color:#4a90d9; cursor:pointer; }
    .switch-text a:hover { text-decoration:underline; }
    .error-msg { color:#e53e3e; font-size:13px; margin:8px 0; }
    .divider { display:flex; align-items:center; gap:12px; margin:16px 0; color:#a0aec0; }
    .divider::before,.divider::after { content:''; flex:1; border-top:1px solid rgba(176,196,222,.4); }
    .providers { display:flex; flex-direction:column; gap:8px; }
    .providers button { display:flex; align-items:center; justify-content:center; gap:8px; padding:10px; border:1px solid #e0e3e8; border-radius:10px; background:#f8faff; font-size:13px; cursor:pointer; color:#4a5568; transition:all .2s; }
    .providers button:hover { background:white; border-color:#c0c8d8; box-shadow:0 2px 12px rgba(0,0,0,.08); }
    .btn-google svg, .btn-microsoft svg { flex-shrink:0; }
  `]
})
export class LoginComponent {
  isLogin = true;
  email = '';
  password = '';
  displayName = '';
  error = '';
  loading = false;

  private returnUrl = '';

  constructor(private auth: AuthService, private router: Router, private route: ActivatedRoute) {
    if (this.auth.isLoggedIn()) this.router.navigate(['/dashboard']);
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';
  }

  private goAfterAuth(): void {
    this.router.navigateByUrl(this.returnUrl);
  }

  onLogin(): void {
    if (!this.email || !this.password) return;
    this.loading = true; this.error = '';
    this.auth.login(this.email, this.password).subscribe({
      next: () => this.goAfterAuth(),
      error: (e) => {
        console.error('[Login] Error completo:', {
          status: e.status,
          statusText: e.statusText,
          url: e.url,
          message: e.message,
          error: e.error,
        });
        this.error = e.error?.error || 'Error al iniciar sesion';
        this.loading = false;
      },
    });
  }

  onRegister(): void {
    if (!this.email || !this.password) return;
    this.loading = true; this.error = '';
    this.auth.register(this.email, this.password, this.displayName || undefined).subscribe({
      next: () => this.goAfterAuth(),
      error: (e) => { this.error = e.error?.error || 'Error al registrarse'; this.loading = false; },
    });
  }

  loginGoogle(): void {}
  loginMicrosoft(): void {}
}
