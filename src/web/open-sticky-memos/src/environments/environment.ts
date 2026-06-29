export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000',
  signalrUrl: 'http://localhost:5000/hubs/notes',
  oauth: {
    google: {
      clientId: '',
      redirectUri: 'http://localhost:4200/auth/callback',
    },
    microsoft: {
      clientId: '',
      tenantId: 'common',
      redirectUri: 'http://localhost:4200/auth/callback',
    },
  },
};
