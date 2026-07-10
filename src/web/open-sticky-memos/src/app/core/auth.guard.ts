import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(_route: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean | UrlTree {
    if (this.authService.isLoggedIn()) {
      return true;
    }
    // Guardar la URL a la que intentaba acceder para redirigir después del login
    const returnUrl = state.url;
    return this.router.parseUrl(returnUrl ? `/login?returnUrl=${encodeURIComponent(returnUrl)}` : '/login');
  }
}
