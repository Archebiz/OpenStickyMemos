import { Injectable, Injector } from '@angular/core';
import {
  HttpInterceptor,
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpErrorResponse,
} from '@angular/common/http';
import { Observable, throwError, from } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { AuthService } from './auth.service';

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

    // Agregar token a todas las peticiones excepto auth
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
        if (error.status === 401 && !this.isRefreshing) {
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
        authService.logout();
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
}
