import { Component, EventEmitter, Output, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (visible) {
      <div class="confirm-overlay" (click)="onCancel()">
        <div class="confirm-dialog" (click)="$event.stopPropagation()">
          <div class="confirm-icon">⚠️</div>
          <h4>{{ title }}</h4>
          <p>{{ message }}</p>
          <div class="confirm-actions">
            <button class="btn-cancel" (click)="onCancel()">Cancelar</button>
            <button class="btn-confirm" (click)="onConfirm()">Eliminar</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .confirm-overlay {
      position: fixed;
      top: 0; left: 0; right: 0; bottom: 0;
      background: rgba(0,0,0,0.45);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 9999;
    }
    .confirm-dialog {
      background: white;
      border-radius: 16px;
      padding: 32px;
      width: 90%;
      max-width: 360px;
      text-align: center;
      box-shadow: 0 20px 60px rgba(0,0,0,0.3);
    }
    .confirm-icon {
      font-size: 40px;
      margin-bottom: 8px;
    }
    .confirm-dialog h4 {
      margin: 0 0 8px;
      font-size: 17px;
      color: #2d3748;
    }
    .confirm-dialog p {
      margin: 0 0 24px;
      font-size: 14px;
      color: #718096;
    }
    .confirm-actions {
      display: flex;
      gap: 10px;
    }
    .btn-cancel, .btn-confirm {
      flex: 1;
      padding: 10px;
      border: none;
      border-radius: 10px;
      font-size: 14px;
      font-weight: 600;
      cursor: pointer;
      transition: opacity .2s;
    }
    .btn-cancel {
      background: #f1f5f9;
      color: #64748b;
    }
    .btn-cancel:hover { background: #e2e8f0; }
    .btn-confirm {
      background: #dc2626;
      color: white;
    }
    .btn-confirm:hover { opacity: 0.9; }
  `]
})
export class ConfirmDialogComponent {
  @Input() visible = false;
  @Input() title = 'Confirmar';
  @Input() message = '¿Estás seguro?';

  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  /** Callback a ejecutar si se confirma */
  private _onConfirm: (() => void) | null = null;

  open(title: string, message: string, onConfirm: () => void): void {
    this.title = title;
    this.message = message;
    this._onConfirm = onConfirm;
    this.visible = true;
  }

  onConfirm(): void {
    this.visible = false;
    this._onConfirm?.();
    this._onConfirm = null;
    this.confirm.emit();
  }

  onCancel(): void {
    this.visible = false;
    this._onConfirm = null;
    this.cancel.emit();
  }
}
