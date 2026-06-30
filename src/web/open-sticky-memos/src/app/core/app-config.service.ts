import { Injectable, APP_INITIALIZER } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { lastValueFrom } from 'rxjs';

export interface AppConfig {
  apiUrl: string;
  signalrUrl: string;
}

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private config: AppConfig = { apiUrl: 'http://localhost:5000', signalrUrl: 'http://localhost:5000/hubs/notes' };

  constructor(private http: HttpClient) {}

  async load(): Promise<void> {
    try {
      this.config = await lastValueFrom(
        this.http.get<AppConfig>('/assets/config.json', { responseType: 'json' })
      );
      console.log('[AppConfig] Config loaded:', this.config);
    } catch (err: any) {
      console.warn('[AppConfig] No se pudo cargar config.json. Esto ocurre si:');
      console.warn('[AppConfig]   1) API_URL no está como build variable en Railway (se usó localhost por defecto)');
      console.warn('[AppConfig]   2) angular.json no incluye src/assets en la sección "assets" (faltó copiar config.json al output)');
      if (err?.status === 200 && typeof err?.error === 'string' && err.error.startsWith('<!')) {
        console.warn('[AppConfig]   → Recibió HTML en vez de JSON: config.json no existe en el servidor (SPA fallback)');
      }
      console.warn('[AppConfig] Usando defaults:', this.config);
    }
  }

  get apiUrl(): string {
    return this.config.apiUrl.replace(/\/+$/, '');
  }

  get signalrUrl(): string {
    return this.config.signalrUrl || `${this.apiUrl}/hubs/notes`;
  }
}

export function initializeAppConfig(appConfig: AppConfigService) {
  return () => appConfig.load();
}

export const APP_CONFIG_PROVIDER = {
  provide: APP_INITIALIZER,
  useFactory: initializeAppConfig,
  deps: [AppConfigService],
  multi: true,
};
