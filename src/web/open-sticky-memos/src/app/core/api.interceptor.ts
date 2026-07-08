import { Injectable, Injector } from '@angular/core';
import {
  HttpInterceptor,
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpErrorResponse,
} from '@angular/common/http';
import { Observable, throwError, from } from 'rxjs';
import { catchError, switchMap, filter, take } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { SessionExpiredDialogComponent } from './session-expired-dialog.component';

@Injectable()
export class ApiInterceptor implements HttpInterceptor {
  private isRefreshing = false;

  constructor(private injector: Injector) {}

  intercept(
    req: HttpRequest<unknown>,
    next: HttpHandler
  ): Observable<HttpEvent<unknown>> {
    const authService = this.injector.get(AuthService);
    const token = authService.getAccessToken();

    // Agregar token a todas las peticiones excepto /auth/ (login, register, refresh, google, microsoft, logout)
    let authReq = req;
    if (token && !req.url.includes('/auth/')) {
      authReq = req.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`,
        },
      });
    }

    return next.handle(authReq).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401 && !req.url.includes('/auth/refresh') && !this.isRefreshing) {
          const auth = this.injector.get(AuthService);
          return this.handle401(authReq, next, auth);
        }
        return throwError(() => error);
      })
    );
  }

  private handle401(
    req: HttpRequest<unknown>,
    next: HttpHandler,
    authService: AuthService
  ): Observable<HttpEvent<unknown>> {
    this.isRefreshing = true;

    return from(this.tryRefreshToken(authService)).pipe(
      switchMap((newToken) => {
        this.isRefreshing = false;
        const cloned = req.clone({
          setHeaders: { Authorization: `Bearer ${newToken}` },
        });
        return next.handle(cloned);
      }),
      catchError((err) => {
        this.isRefreshing = false;
        authService.clearSession();
        this.showSessionExpiredDialog();
        return throwError(() => err);
      })
    );
  }

  private async tryRefreshToken(authService: AuthService): Promise<string> {
    return new Promise((resolve, reject) => {
      authService.refreshToken().subscribe({
        next: (res) => resolve(res.accessToken),
        error: (err) => reject(err),
      });
    });
  }

  private showSessionExpiredDialog(): void {
    // Abrir el diálogo usando el injector para evitar dependencia circular
    const dialog = this.injector.get(MatDialog);
    const router = this.injector.get(Router);

    dialog
      .open(SessionExpiredDialogComponent, {
        disableClose: true,
        panelClass: 'session-expired-panel',
      })
      .afterClosed()
      .subscribe(() => {
        router.navigate(['/login']);
      });
  }
}
