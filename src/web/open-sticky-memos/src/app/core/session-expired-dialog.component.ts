import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-session-expired-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Sesión expirada</h2>
    <mat-dialog-content>
      <p>Tu sesión ha expirado. Por favor, inicia sesión nuevamente para continuar.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-raised-button color="primary" [mat-dialog-close]="true">
        Ir a iniciar sesión
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content {
      min-width: 300px;
      padding: 16px 0;
    }
    mat-dialog-content p {
      margin: 0;
      font-size: 15px;
      line-height: 1.5;
      color: rgba(0, 0, 0, 0.72);
    }
    h2 {
      margin: 0;
      font-weight: 500;
    }
  `]
})
export class SessionExpiredDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<SessionExpiredDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: unknown
  ) {}
}
