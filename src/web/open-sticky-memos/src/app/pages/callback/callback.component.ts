import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-callback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="callback-container">
      <div class="loading-card">
        <div class="spinner"></div>
        <p>Iniciando sesión...</p>
      </div>
    </div>
  `,
  styles: [
    `
      .callback-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 100vh;
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      }
      .loading-card {
        background: white;
        border-radius: 16px;
        padding: 48px;
        text-align: center;
        box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
      }
      .spinner {
        width: 40px;
        height: 40px;
        border: 4px solid #e0e0e0;
        border-top: 4px solid #667eea;
        border-radius: 50%;
        animation: spin 0.8s linear infinite;
        margin: 0 auto 16px;
      }
      @keyframes spin {
        to { transform: rotate(360deg); }
      }
      p {
        color: #666;
        font-size: 16px;
        margin: 0;
      }
    `,
  ],
})
export class CallbackComponent implements OnInit {
  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    // Procesar el token devuelto por el proveedor OAuth
    this.route.queryParams.subscribe((params) => {
      const idToken = params['id_token'];
      const provider = params['provider'];

      if (idToken && provider) {
        const obs =
          provider === 'Google'
            ? this.authService.loginWithGoogle(idToken)
            : this.authService.loginWithMicrosoft(idToken);

        obs.subscribe({
          next: () => this.router.navigate(['/dashboard']),
          error: () => this.router.navigate(['/login']),
        });
      } else {
        this.router.navigate(['/login']);
      }
    });
  }
}
