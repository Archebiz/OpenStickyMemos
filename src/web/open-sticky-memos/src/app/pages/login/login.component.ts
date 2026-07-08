import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="login-container">
      <div class="login-card">
        <div class="logo">
          <span class="icon">📝</span>
          <h1>OpenStickyMemos</h1>
          <p class="subtitle">Notas colaborativas en tiempo real</p>
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
          <button (click)="loginGoogle()">G Google</button>
          <button (click)="loginMicrosoft()">M Microsoft</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-container { display:flex; justify-content:center; align-items:center; min-height:100vh; background:linear-gradient(135deg,#667eea,#764ba2); }
    .login-card { background:white; border-radius:16px; padding:40px; width:100%; max-width:400px; box-shadow:0 20px 60px rgba(0,0,0,.3); text-align:center; }
    .auth-form { display:flex; flex-direction:column; gap:10px; margin-bottom:8px; }
    .input { padding:10px 12px; border:1px solid #ddd; border-radius:8px; font-size:14px; }
    .input:focus { outline:none; border-color:#667eea; }
    .btn-primary { padding:10px; border:none; border-radius:8px; background:#667eea; color:white; cursor:pointer; }
    .btn-primary:disabled { opacity:.5; }
    .forgot-link { font-size:12px; color:#667eea; text-decoration:none; text-align:right; margin:-2px 0 6px 0; cursor:pointer; display:block; }
    .forgot-link:hover { text-decoration:underline; }
    .switch-text { font-size:13px; color:#888; margin:6px 0; }
    .switch-text a { color:#667eea; cursor:pointer; }
    .error-msg { color:#E74C3C; font-size:13px; margin:8px 0; }
    .divider { display:flex; align-items:center; gap:12px; margin:16px 0; color:#ccc; }
    .divider::before,.divider::after { content:''; flex:1; border-top:1px solid #eee; }
    .providers { display:flex; gap:8px; }
    .providers button { flex:1; padding:10px; border:1px solid #ddd; border-radius:8px; background:white; font-size:13px; cursor:pointer; }
  `]
})
export class LoginComponent {
  isLogin = true;
  email = '';
  password = '';
  displayName = '';
  error = '';
  loading = false;

  constructor(private auth: AuthService, private router: Router) {
    if (this.auth.isLoggedIn()) this.router.navigate(['/dashboard']);
  }

  onLogin(): void {
    if (!this.email || !this.password) return;
    this.loading = true; this.error = '';
    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigate(['/dashboard']),
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
      next: () => this.router.navigate(['/dashboard']),
      error: (e) => { this.error = e.error?.error || 'Error al registrarse'; this.loading = false; },
    });
  }

  loginGoogle(): void {}
  loginMicrosoft(): void {}
}
