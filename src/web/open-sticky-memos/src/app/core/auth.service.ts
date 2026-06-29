import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';

export interface UserInfo {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  authProvider: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserInfo;
}

@Injectable()
export class AuthService {
  private readonly apiUrl = environment.apiUrl;
  private readonly tokenKey = 'osm_access_token';
  private readonly refreshKey = 'osm_refresh_token';
  private readonly userKey = 'osm_user';

  private userSubject = new BehaviorSubject<UserInfo | null>(this.getStoredUser());
  user$ = this.userSubject.asObservable();

  constructor(private http: HttpClient) {}

  /** Login con Google — envía el id_token al backend */
  loginWithGoogle(idToken: string): Observable<AuthResponse> {
    return this.exchangeToken(idToken, 'Google');
  }

  /** Login con Microsoft — envía el id_token al backend */
  loginWithMicrosoft(idToken: string): Observable<AuthResponse> {
    return this.exchangeToken(idToken, 'Microsoft');
  }

  /** Refresca el access token */
  refreshToken(): Observable<AuthResponse> {
    const refresh = this.getRefreshToken();
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/auth/refresh`, {
        refreshToken: refresh,
      })
      .pipe(tap((res) => this.storeSession(res)));
  }

  /** Logout — limpia la sesión local */
  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.refreshKey);
    localStorage.removeItem(this.userKey);
    this.userSubject.next(null);
  }

  /** Obtiene el access token actual */
  getAccessToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  /** Obtiene el refresh token */
  getRefreshToken(): string | null {
    return localStorage.getItem(this.refreshKey);
  }

  /** Obtiene el usuario almacenado */
  getStoredUser(): UserInfo | null {
    const stored = localStorage.getItem(this.userKey);
    return stored ? JSON.parse(stored) : null;
  }

  /** Verifica si hay sesión activa */
  isLoggedIn(): boolean {
    return !!this.getAccessToken();
  }

  // ── Privado ──

  private exchangeToken(idToken: string, provider: string): Observable<AuthResponse> {
    const endpoint = provider === 'Google' ? 'google' : 'microsoft';
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/auth/${endpoint}`, {
        idToken,
        provider,
      })
      .pipe(tap((res) => this.storeSession(res)));
  }

  private storeSession(res: AuthResponse): void {
    localStorage.setItem(this.tokenKey, res.accessToken);
    localStorage.setItem(this.refreshKey, res.refreshToken);
    localStorage.setItem(this.userKey, JSON.stringify(res.user));
    this.userSubject.next(res.user);
  }
}
