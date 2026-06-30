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
        this.http.get<AppConfig>('/assets/config.json')
      );
    } catch {
      console.warn('Could not load config.json, using defaults');
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
