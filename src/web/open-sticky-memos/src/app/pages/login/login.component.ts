import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-container">
      <div class="login-card">
        <div class="logo">
          <span class="icon">📝</span>
          <h1>OpenStickyMemos</h1>
          <p class="subtitle">Notas colaborativas en tiempo real</p>
        </div>

        <div class="providers">
          <button class="btn-google" (click)="loginGoogle()">
            <span class="btn-icon">
              <svg viewBox="0 0 48 48" width="20" height="20">
                <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/>
                <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/>
                <path fill="#FBBC05" d="M10.54 28.59A14.5 14.5 0 0 1 9.5 24c0-1.59.28-3.14.76-4.59l-7.98-6.19A23.99 23.99 0 0 0 0 24c0 4.13 1.05 8.02 2.88 11.41l7.66-5.82z"/>
                <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/>
              </svg>
            </span>
            Iniciar sesión con Google
          </button>

          <button class="btn-microsoft" (click)="loginMicrosoft()">
            <span class="btn-icon">
              <svg viewBox="0 0 21 21" width="20" height="20">
                <rect x="1" y="1" width="9" height="9" fill="#f25022"/>
                <rect x="11" y="1" width="9" height="9" fill="#7fba00"/>
                <rect x="1" y="11" width="9" height="9" fill="#00a4ef"/>
                <rect x="11" y="11" width="9" height="9" fill="#ffb900"/>
              </svg>
            </span>
            Iniciar sesión con Microsoft
          </button>
        </div>

        <p class="footer-text">
          Al iniciar sesión, aceptas compartir tu email y nombre de perfil.
        </p>
      </div>
    </div>
  `,
  styles: [
    `
      .login-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 100vh;
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      }
      .login-card {
        background: white;
        border-radius: 16px;
        padding: 48px 40px;
        width: 100%;
        max-width: 400px;
        box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
        text-align: center;
      }
      .logo {
        margin-bottom: 32px;
      }
      .icon {
        font-size: 48px;
      }
      h1 {
        margin: 8px 0 4px;
        font-size: 24px;
        color: #1a1a2e;
      }
      .subtitle {
        color: #666;
        font-size: 14px;
        margin: 0;
      }
      .providers {
        display: flex;
        flex-direction: column;
        gap: 12px;
        margin-bottom: 24px;
      }
      button {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 12px;
        width: 100%;
        padding: 12px 24px;
        border-radius: 8px;
        border: 1px solid #ddd;
        background: white;
        font-size: 15px;
        font-weight: 500;
        cursor: pointer;
        transition: all 0.2s;
      }
      button:hover {
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
        transform: translateY(-1px);
      }
      .btn-google:hover {
        border-color: #4285f4;
      }
      .btn-microsoft:hover {
        border-color: #00a4ef;
      }
      .btn-icon {
        display: flex;
        align-items: center;
      }
      .footer-text {
        font-size: 12px;
        color: #999;
        margin: 0;
      }
    `,
  ],
})
export class LoginComponent {
  constructor(
    private authService: AuthService,
    private router: Router
  ) {
    if (this.authService.isLoggedIn()) {
      this.router.navigate(['/dashboard']);
    }
  }

  loginGoogle(): void {
    // TODO: Integrar Google Identity Services (GSI)
    // Por ahora redirige al backend OAuth
    console.log('Google login - pendiente integración GSI');
  }

  loginMicrosoft(): void {
    // TODO: Integrar MSAL
    console.log('Microsoft login - pendiente integración MSAL');
  }
}
