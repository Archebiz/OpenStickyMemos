import {
  Component,
  OnInit,
  OnDestroy,
  HostListener,
  ViewChild,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, debounceTime, groupBy, mergeMap, switchMap, takeUntil } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { SignalRService } from '../../core/signalr.service';
import { NoteCardComponent, NoteCardData } from './note-card.component';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog.component';
import { NotificationBellComponent } from '../../shared/notification-bell.component';
import { NoteResponse, ProjectResponse, InvitationResponse, NoteActionRequestResponse } from '../../models';

@Component({
  selector: 'app-sticky-board',
  standalone: true,
  imports: [CommonModule, FormsModule, NoteCardComponent, ConfirmDialogComponent, NotificationBellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="board-container">
      <!-- Top bar -->
      <div class="board-header">
        <button class="back-btn" (click)="goBack()" title="Volver al listado de proyectos">
          ← Proyectos
        </button>

        @if (project) {
          <div
            class="project-title-container"
            [class.editable]="project.ownerId === userId"
            (click)="project.ownerId === userId ? openRenameDialog() : null"
            [title]="project.ownerId === userId ? 'Clic para renombrar proyecto' : ''"
          >
            <h2>{{ project.name }}</h2>
            @if (project.ownerId === userId) {
              <span class="edit-title-icon" title="Renombrar proyecto">✏️</span>
            }
          </div>
        }

        <div class="header-actions">
          <div class="search-box">
            <span class="search-icon">🔍</span>
            <input
              class="tag-filter"
              [(ngModel)]="tagFilter"
              placeholder="Buscar en notas..."
            />
            @if (tagFilter) {
              <button class="clear-search-btn" (click)="tagFilter = ''" title="Limpiar búsqueda">✕</button>
            }
          </div>

          @if (project && project.ownerId === userId && pendingCount > 0) {
            <button class="btn-approvals-alert" (click)="openApprovals()" title="Solicitudes pendientes de aprobación">
              ⏳ Solicitudes <span class="badge-count">{{ pendingCount }}</span>
            </button>
          }

          @if (project && project.ownerId === userId) {
            <button class="btn-action btn-share" (click)="openShareDialog()" title="Gestionar colaboradores y enlaces">
              👥 Compartir
            </button>
          }

          <button
            class="btn-action"
            [class.active-filter]="showArchived"
            (click)="toggleArchive()"
            [title]="showArchived ? 'Mostrar notas activas' : 'Ver notas archivadas'"
          >
            {{ showArchived ? '📂 Activas' : '🗄️ Archivadas' }}
          </button>

          <button class="btn-primary-add" [disabled]="addingNote" (click)="addNote()" title="Crear nueva nota [Atajo: N]">
            {{ addingNote ? '...' : '+ Nota' }}
            <kbd class="kbd-hint">N</kbd>
          </button>

          <app-notification-bell></app-notification-bell>
        </div>
      </div>

      <!-- Share dialog -->
      @if (showShareDialog) {
        <div class="dialog-overlay" (click)="showShareDialog = false">
          <div class="dialog" (click)="$event.stopPropagation()">
            <div class="dialog-tabs">
              <button [class.active]="shareTab === 'email'" (click)="shareTab = 'email'">Por email</button>
              <button [class.active]="shareTab === 'link'" (click)="shareTab = 'link'">Link de invitación</button>
              <button [class.active]="shareTab === 'members'" (click)="shareTab = 'members'">Miembros</button>
            </div>

            <!-- Tab: Invitar por email -->
            @if (shareTab === 'email') {
              <h4>Invitar por email</h4>
              <input
                #emailInput
                type="email"
                placeholder="Email del usuario"
                class="input"
                (keyup.enter)="inviteMember(emailInput.value); emailInput.value = ''"
              />
              <button
                class="btn-primary"
                [disabled]="inviting"
                (click)="inviteMember(emailInput.value); emailInput.value = ''"
              >
                {{ inviting ? '...' : 'Invitar' }}
              </button>
              @if (inviteMessage) {
                <p class="invite-msg">{{ inviteMessage }}</p>
              }
            }

            <!-- Tab: Link de invitación -->
            @if (shareTab === 'link') {
              <h4>Link de invitación</h4>
              <p class="help-text">Generá un link para compartir con cualquier persona. Al aceptarlo se unirá al proyecto automáticamente.</p>

              <div class="link-options">
                <label class="checkbox-label">
                  <input type="checkbox" [(ngModel)]="inviteLinkRestrictEmail" [disabled]="generatingLink" />
                  Restringir a un email específico
                </label>
                @if (inviteLinkRestrictEmail) {
                  <input
                    type="email"
                    [(ngModel)]="inviteLinkEmail"
                    placeholder="Email (solo este podrá aceptar)"
                    class="input"
                  />
                }
                <div class="expires-row">
                  <label>Vence en:</label>
                  <select [(ngModel)]="inviteLinkExpiresDays" [disabled]="generatingLink">
                    <option [value]="1">1 día</option>
                    <option [value]="3">3 días</option>
                    <option [value]="7">7 días</option>
                    <option [value]="14">14 días</option>
                    <option [value]="30">30 días</option>
                  </select>
                </div>
              </div>

              <button class="btn-primary" [disabled]="generatingLink" (click)="generateInvitationLink()">
                {{ generatingLink ? 'Generando...' : '🔗 Generar link' }}
              </button>

              @if (invitationLinkError) {
                <p class="error-text">{{ invitationLinkError }}</p>
              }

              @if (invitations.length > 0) {
                <div class="invitation-list">
                  <h5>Links activos ({{ invitations.length }})</h5>
                  @for (inv of invitations; track inv.id) {
                    <div class="invitation-item">
                      <div class="invitation-info">
                        <span class="invitation-link-text">{{ inv.invitationLink }}</span>
                        <span class="invitation-meta">
                          @if (inv.invitedEmail) {
                            <span>📧 {{ inv.invitedEmail }}</span>
                          }
                          <span>⏳ Vence {{ inv.expiresAt | date:'short' }}</span>
                        </span>
                      </div>
                      <div class="invitation-actions">
                        <button class="btn-sm btn-copy" (click)="copyToClipboard(inv.invitationLink)" title="Copiar link">
                          📋
                        </button>
                        @if (!inv.isAccepted) {
                          <button class="btn-sm btn-revoke" (click)="revokeInvitation(inv.id)" title="Revocar">
                            🗑️
                          </button>
                        }
                      </div>
                    </div>
                  }
                </div>
              }
            }

            <!-- Tab: Miembros -->
            @if (shareTab === 'members') {
              <h4>Miembros ({{ project?.members?.length || 0 }})</h4>
              @if (project?.members && project!.members.length > 0) {
                <div class="member-list">
                  @for (m of project!.members; track m.userId) {
                    <div class="member-item">
                      <div class="member-info">
                        <span class="member-name">{{ m.displayName }}</span>
                        <span class="member-email">{{ m.email }}</span>
                      </div>
                      <div class="member-actions">
                        <span class="member-role-badge">{{ m.role }}</span>
                        @if (project!.ownerId === userId && m.userId !== project!.ownerId) {
                          <button class="btn-remove" (click)="removeMember(m.userId, m.displayName)" title="Remover miembro">✕</button>
                        }
                      </div>
                    </div>
                  }
                </div>
              } @else {
                <p class="help-text">No hay miembros aún.</p>
              }
            }

            <button class="btn-secondary" (click)="showShareDialog = false">
              Cerrar
            </button>
          </div>
        </div>
      }

      <!-- Canvas -->
      <div
        class="canvas"
        (mouseup)="onMouseUp()"
        (mouseleave)="onMouseUp()"
        (dblclick)="onCanvasDblClick($event)"
      >
        @for (note of visibleNotes; track note.id) {
          <app-note-card
            [note]="note"
            [isHighlighted]="note.id === highlightedNoteId"
            [projectMembers]="projectMembersList"
            (noteChange)="onNoteChange($event)"
            (deleteNote)="onDeleteNote($event)"
            (archiveNote)="onArchiveNote($event)"
            (metaChange)="onNoteMetaChanged($event)"
            (bringToFrontNote)="onBringToFront($event)"
            (sendToBackNote)="onSendToBack($event)"
            (dragStart)="onDragStart($event)"
            (resizeStart)="onResizeStart($event)"
          />
        }

        @if (notes.length === 0) {
          <div class="empty-canvas">
            <span class="empty-canvas-icon">🗒️</span>
            <h3>Tu tablero está listo</h3>
            <p>Haz doble clic en cualquier parte o pulsa <strong>[N]</strong> para crear tu primera nota.</p>
            <button class="btn-primary-add" [disabled]="addingNote" (click)="addNote()">
              {{ addingNote ? '...' : '+ Crear primera nota' }}
            </button>
          </div>
        }
      </div>

      <!-- Live status bar -->
      <div class="board-footer-bar">
        <span class="footer-hint">💡 Tip: Haz doble clic en el lienzo para crear una nota rápida</span>
        <div class="live-status" [class.connected]="signalRConnected">
          <span class="status-dot"></span>
          <span class="status-text">{{ signalRConnected ? 'Sincronizado en tiempo real' : 'Reconectando...' }}</span>
        </div>
      </div>

      <!-- Sidebar para mover notas a otro proyecto (visible al arrastrar) -->
      @if (moveSidebarVisible) {
        <div class="move-sidebar" (mouseleave)="hoveredProjectId = ''">
          <div class="move-sidebar-title">📋 Mover nota a...</div>
          <div class="move-sidebar-sub">Soltá la nota sobre un proyecto para moverla</div>
          @for (p of moveTargets; track p.id) {
            <button class="move-target" (mouseover)="hoveredProjectId = p.id">📋 {{ p.name }}</button>
          }
        </div>
      }

      <!-- Renombrar proyecto -->
      @if (showRename) {
        <div class="dialog-overlay" (click)="showRename = false">
          <div class="dialog" (click)="$event.stopPropagation()">
            <h4>Renombrar proyecto</h4>
            <input
              class="input"
              [(ngModel)]="renameName"
              (keyup.enter)="saveRename()"
              placeholder="Nuevo nombre del proyecto"
            />
            <button class="btn-primary" (click)="saveRename()">Guardar nombre</button>
            <button class="btn-secondary" (click)="showRename = false">Cancelar</button>
          </div>
        </div>
      }

      <!-- Solicitudes de aprobación (owner) -->
      @if (showApprovals) {
        <div class="dialog-overlay" (click)="showApprovals = false">
          <div class="dialog" (click)="$event.stopPropagation()">
            <h4>Solicitudes pendientes ({{ pendingRequests.length }})</h4>
            @if (pendingRequests.length === 0) {
              <p class="help-text">No hay solicitudes pendientes.</p>
            }
            <div class="member-list">
              @for (req of pendingRequests; track req.id) {
                <div class="member-item">
                  <div class="member-info">
                    <span class="member-name">{{ req.noteTitle || '(Sin título)' }}</span>
                    <span class="member-email">
                      {{ req.requesterName }} solicita {{ req.type === 'Delete' ? 'eliminar' : 'archivar' }}
                    </span>
                  </div>
                  <div class="member-actions">
                    <button class="btn-sm" style="background:#dcfce7;color:#15803d" (click)="approveRequest(req.id)">✓ Aprobar</button>
                    <button class="btn-sm" style="background:#fee2e2;color:#b91c1c" (click)="rejectRequest(req.id)">✕ Rechazar</button>
                  </div>
                </div>
              }
            </div>
            <button class="btn-secondary" (click)="showApprovals = false">Cerrar</button>
          </div>
        </div>
      }

      <!-- Toast de notificación -->
      @if (toastMessage) {
        <div class="board-toast" (click)="toastMessage = ''">🔔 {{ toastMessage }}</div>
      }

      <!-- Confirm dialog -->
      <app-confirm-dialog #confirmDialog></app-confirm-dialog>
    </div>
  `,
  styles: [
    `
      .board-container {
        display: flex;
        flex-direction: column;
        height: 100vh;
        background: #f0f2f5;
      }
      .board-header {
        display: flex;
        align-items: center;
        gap: 14px;
        padding: 10px 20px;
        background: white;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
        z-index: 20;
      }
      .project-title-container {
        display: flex;
        align-items: center;
        gap: 6px;
        cursor: default;
        user-select: none;
      }
      .project-title-container.editable {
        cursor: pointer;
        padding: 4px 8px;
        border-radius: 6px;
        transition: background 0.15s;
      }
      .project-title-container.editable:hover {
        background: #f1f5f9;
      }
      .project-title-container h2 {
        margin: 0;
        font-size: 16px;
        font-weight: 600;
        color: #1e293b;
      }
      .edit-title-icon {
        font-size: 12px;
        opacity: 0.5;
        transition: opacity 0.15s;
      }
      .project-title-container.editable:hover .edit-title-icon {
        opacity: 1;
      }
      .header-actions {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-left: auto;
      }
      .search-box {
        position: relative;
        display: flex;
        align-items: center;
      }
      .search-icon {
        position: absolute;
        left: 10px;
        font-size: 12px;
        pointer-events: none;
        opacity: 0.5;
      }
      .tag-filter {
        padding: 7px 28px 7px 28px;
        border: 1px solid #e2e8f0;
        border-radius: 8px;
        font-size: 12px;
        outline: none;
        width: 170px;
        background: #f8fafc;
        transition: all 0.2s;
      }
      .tag-filter:focus {
        width: 220px;
        background: white;
        border-color: #667eea;
        box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.12);
      }
      .clear-search-btn {
        position: absolute;
        right: 8px;
        background: transparent;
        border: none;
        font-size: 11px;
        color: #94a3b8;
        cursor: pointer;
        padding: 2px;
      }
      .clear-search-btn:hover {
        color: #475569;
      }
      .back-btn {
        padding: 7px 12px;
        border-radius: 8px;
        border: none;
        font-size: 13px;
        cursor: pointer;
        font-weight: 500;
        background: transparent;
        color: #667eea;
        display: flex;
        align-items: center;
        gap: 4px;
        transition: background 0.15s;
      }
      .back-btn:hover {
        background: #eef2ff;
      }
      .btn-action {
        padding: 7px 13px;
        border-radius: 8px;
        border: 1px solid #e2e8f0;
        font-size: 12px;
        cursor: pointer;
        font-weight: 500;
        background: white;
        color: #475569;
        display: flex;
        align-items: center;
        gap: 6px;
        transition: all 0.15s;
      }
      .btn-action:hover {
        background: #f8fafc;
        border-color: #cbd5e1;
      }
      .btn-action.active-filter {
        background: #eff6ff;
        border-color: #93c5fd;
        color: #1d4ed8;
      }
      .btn-approvals-alert {
        padding: 7px 12px;
        border-radius: 8px;
        border: 1px solid #fde047;
        font-size: 12px;
        cursor: pointer;
        font-weight: 500;
        background: #fefce8;
        color: #a16207;
        display: flex;
        align-items: center;
        gap: 6px;
        animation: pulse 2s infinite;
      }
      .badge-count {
        background: #ca8a04;
        color: white;
        border-radius: 10px;
        padding: 1px 6px;
        font-size: 10px;
        font-weight: bold;
      }
      .btn-primary-add {
        padding: 7px 14px;
        border-radius: 8px;
        border: none;
        font-size: 13px;
        cursor: pointer;
        background: linear-gradient(135deg, #667eea, #764ba2);
        color: white;
        font-weight: 600;
        display: flex;
        align-items: center;
        gap: 6px;
        box-shadow: 0 2px 6px rgba(102, 126, 234, 0.35);
        transition: transform 0.15s, box-shadow 0.15s;
      }
      .btn-primary-add:hover {
        transform: translateY(-1px);
        box-shadow: 0 4px 10px rgba(102, 126, 234, 0.45);
      }
      .btn-primary-add:active {
        transform: translateY(0);
      }
      .kbd-hint {
        background: rgba(255, 255, 255, 0.25);
        padding: 1px 5px;
        border-radius: 4px;
        font-size: 10px;
        font-family: monospace;
      }
      .canvas {
        flex: 1;
        position: relative;
        overflow: auto;
        padding: 20px;
      }
      .empty-canvas {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        text-align: center;
        color: #64748b;
        max-width: 320px;
      }
      .empty-canvas-icon {
        font-size: 40px;
        display: block;
        margin-bottom: 8px;
      }
      .empty-canvas h3 {
        margin: 0 0 6px;
        font-size: 16px;
        color: #1e293b;
      }
      .empty-canvas p {
        margin: 0 0 16px;
        font-size: 13px;
        line-height: 1.4;
      }
      .board-footer-bar {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 6px 16px;
        background: rgba(255, 255, 255, 0.85);
        backdrop-filter: blur(8px);
        border-top: 1px solid rgba(0, 0, 0, 0.05);
        font-size: 11px;
        color: #94a3b8;
        z-index: 10;
      }
      .live-status {
        display: flex;
        align-items: center;
        gap: 6px;
      }
      .status-dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: #f59e0b;
      }
      .live-status.connected .status-dot {
        background: #10b981;
        box-shadow: 0 0 6px rgba(16, 185, 129, 0.6);
      }
      .live-status.connected .status-text {
        color: #059669;
        font-weight: 500;
      }
      .empty-canvas p {
        margin-bottom: 12px;
      }
      .dialog-overlay {
        position: fixed;
        top: 0; left: 0; right: 0; bottom: 0;
        background: rgba(0,0,0,0.4);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 100;
      }
      .dialog {
        background: white;
        border-radius: 12px;
        padding: 24px;
        width: 90%;
        max-width: 400px;
        box-shadow: 0 20px 60px rgba(0,0,0,0.3);
      }
      .dialog h4 { margin: 0 0 12px; font-size: 15px; }
      .dialog h5 { margin: 16px 0 8px; font-size: 13px; color: #666; }
      .input {
        width: 100%;
        padding: 10px 12px;
        border: 1px solid #ddd;
        border-radius: 8px;
        font-size: 14px;
        margin-bottom: 8px;
        box-sizing: border-box;
      }
      .input:focus { outline: none; border-color: #667eea; }
      .help-text { font-size: 12px; color: #888; margin: 0 0 12px; }
      .error-text { font-size: 13px; color: #e53e3e; margin: 4px 0; }
      .invite-msg { font-size: 13px; color: #667eea; margin: 4px 0; }
      .member-list { margin-top: 12px; }
      .member-item {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 8px 0;
        font-size: 13px;
        border-bottom: 1px solid #f0f0f0;
      }
      .member-info { display: flex; flex-direction: column; }
      .member-name { font-weight: 500; color: #333; }
      .member-email { font-size: 11px; color: #999; }
      .member-actions {
        display: flex;
        align-items: center;
        gap: 8px;
      }
      .member-role-badge {
        font-size: 10px;
        background: #edf2f7;
        padding: 2px 8px;
        border-radius: 10px;
        color: #666;
        text-transform: capitalize;
      }
      .btn-remove {
        padding: 2px 6px;
        border: none;
        border-radius: 4px;
        font-size: 12px;
        cursor: pointer;
        background: transparent;
        color: #999;
        transition: all .2s;
      }
      .btn-remove:hover {
        background: #fee2e2;
        color: #dc2626;
      }
      .dialog-tabs {
        display: flex;
        gap: 4px;
        margin-bottom: 16px;
        background: #f1f5f9;
        border-radius: 10px;
        padding: 3px;
      }
      .dialog-tabs button {
        flex: 1;
        padding: 8px 12px;
        border: none;
        background: transparent;
        border-radius: 8px;
        font-size: 12px;
        font-weight: 500;
        cursor: pointer;
        color: #64748b;
        transition: all .2s;
      }
      .dialog-tabs button.active {
        background: white;
        color: #334155;
        box-shadow: 0 1px 3px rgba(0,0,0,0.1);
      }
      .link-options {
        margin: 12px 0;
        padding: 12px;
        background: #f8fafc;
        border-radius: 10px;
      }
      .checkbox-label {
        display: flex;
        align-items: center;
        gap: 6px;
        font-size: 12px;
        color: #555;
        margin-bottom: 8px;
        cursor: pointer;
      }
      .checkbox-label input[type="checkbox"] { cursor: pointer; }
      .expires-row {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-top: 8px;
      }
      .expires-row label { font-size: 12px; color: #555; }
      .expires-row select {
        padding: 6px 10px;
        border: 1px solid #ddd;
        border-radius: 8px;
        font-size: 12px;
        background: white;
      }
      .invitation-list { margin-top: 16px; }
      .invitation-item {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 8px 0;
        border-bottom: 1px solid #f0f0f0;
        gap: 8px;
      }
      .invitation-info {
        display: flex;
        flex-direction: column;
        gap: 2px;
        flex: 1;
        min-width: 0;
      }
      .invitation-link-text {
        font-size: 11px;
        color: #667eea;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .invitation-meta {
        display: flex;
        gap: 8px;
        font-size: 10px;
        color: #999;
      }
      .invitation-actions {
        display: flex;
        gap: 4px;
        flex-shrink: 0;
      }
      .btn-sm {
        padding: 4px 8px;
        border: none;
        border-radius: 6px;
        font-size: 14px;
        cursor: pointer;
        background: #f1f5f9;
        transition: background .2s;
      }
      .btn-sm:hover { background: #e2e8f0; }
      .btn-revoke:hover { background: #fee2e2; }
      .move-sidebar {
        position: fixed;
        top: 0;
        right: 0;
        bottom: 0;
        width: 260px;
        background: #f4f7fb;
        border-left: 1px solid #e2e8f0;
        box-shadow: -6px 0 20px rgba(0, 0, 0, 0.08);
        padding: 20px 14px;
        box-sizing: border-box;
        z-index: 2147483000;
      }
      .move-sidebar-title {
        font-size: 15px;
        font-weight: 600;
        color: #334155;
        margin-bottom: 4px;
      }
      .move-sidebar-sub {
        font-size: 11px;
        color: #94a3b8;
        margin-bottom: 14px;
      }
      .move-target {
        display: block;
        width: 100%;
        text-align: left;
        padding: 10px 12px;
        margin-bottom: 8px;
        border: none;
        border-radius: 10px;
        background: white;
        color: #334155;
        font-size: 13px;
        cursor: pointer;
        box-shadow: 0 1px 2px rgba(0, 0, 0, 0.06);
        transition: background .15s;
      }
      .move-target:hover {
        background: #667eea;
        color: white;
      }
      .board-toast {
        position: fixed;
        right: 16px;
        bottom: 16px;
        background: #334155;
        color: white;
        padding: 12px 16px;
        border-radius: 10px;
        box-shadow: 0 6px 20px rgba(0, 0, 0, 0.25);
        font-size: 13px;
        z-index: 9999;
        max-width: 320px;
        cursor: pointer;
      }
    `,
  ],
})
export class StickyBoardComponent implements OnInit, OnDestroy {
  projectId = '';
  userId = '';
  project: ProjectResponse | null = null;
  notes: NoteCardData[] = [];
  showShareDialog = false;
  inviteMessage = '';
  addingNote = false;
  inviting = false;

  // Archivo / renombre / aprobaciones
  showArchived = false;
  showRename = false;
  renameName = '';
  showApprovals = false;
  actionRequests: NoteActionRequestResponse[] = [];
  allProjects: ProjectResponse[] = [];
  moveSidebarVisible = false;
  moveTargets: ProjectResponse[] = [];
  hoveredProjectId = '';
  tagFilter = '';
  toastMessage = '';
  signalRConnected = false;
  highlightedNoteId: string | null = null;
  private scheduledReminders = new Set<string>();
  private notifiedReminders = new Set<string>();

  get projectMembersList(): { userId: string; displayName: string; email: string }[] {
    if (!this.project) return [];
    const list: { userId: string; displayName: string; email: string }[] = [];
    if (this.project.ownerId && this.project.ownerName) {
      list.push({
        userId: this.project.ownerId,
        displayName: this.project.ownerName,
        email: '',
      });
    }
    for (const m of this.project.members ?? []) {
      if (!list.some((x) => x.userId === m.userId)) {
        list.push({
          userId: m.userId,
          displayName: m.displayName,
          email: m.email,
        });
      }
    }
    return list;
  }

  get visibleNotes(): NoteCardData[] {
    const q = this.tagFilter.trim().toLowerCase();
    if (!q) return this.notes;
    return this.notes.filter((n) =>
      (n.tags ?? '')
        .toLowerCase()
        .split(',')
        .some((t) => t.trim().includes(q)) ||
      (n.title ?? '').toLowerCase().includes(q) ||
      (n.content ?? '').toLowerCase().includes(q)
    );
  }

  get pendingCount(): number {
    return this.actionRequests.filter((r) => r.status === 'Pending').length;
  }

  get pendingRequests(): NoteActionRequestResponse[] {
    return this.actionRequests.filter((r) => r.status === 'Pending');
  }

  // Invitation link state
  shareTab: 'email' | 'link' | 'members' = 'email';
  invitations: InvitationResponse[] = [];
  generatingLink = false;
  invitationLinkError = '';
  inviteLinkRestrictEmail = false;
  inviteLinkEmail = '';
  inviteLinkExpiresDays = 7;

  // Drag / resize state
  private dragging: { noteId: string; startX: number; startY: number; origX: number; origY: number } | null = null;
  private resizing: { noteId: string; startX: number; startY: number; origW: number; origH: number } | null = null;
  private touchMoveHandler: ((e: TouchEvent) => void) | null = null;

  @ViewChild('confirmDialog') confirmDialog!: ConfirmDialogComponent;

  // Debounce for auto-save
  private saveSubject = new Subject<{ noteId: string; changes: Partial<NoteCardData> }>();
  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: ApiService,
    private signalR: SignalRService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id')!;
    this.userId = JSON.parse(localStorage.getItem('osm_user') || '{}').id;

    this.loadProject();
    this.loadNotes();
    this.loadActionRequests();
    this.joinSignalR();

    // Resaltar nota si viene en queryParams
    this.route.queryParams
      .pipe(takeUntil(this.destroy$))
      .subscribe((params) => {
        if (params['noteId']) {
          this.highlightNote(params['noteId']);
        }
      });

    // Estado de conexión SignalR para feedback visual
    this.signalR.connectionState$
      .pipe(takeUntil(this.destroy$))
      .subscribe((connected) => {
        this.signalRConnected = connected;
        this.cdr.markForCheck();
      });

    // Proyectos del usuario (para mover notas y renombrar)
    this.api
      .getProjects()
      .pipe(takeUntil(this.destroy$))
      .subscribe((ps) => {
        this.allProjects = ps;
        this.cdr.markForCheck();
      });

    // Pedir permiso para notificaciones (recordatorios)
    if ('Notification' in window && Notification.permission === 'default') {
      Notification.requestPermission();
    }

    // Auto-save debounced por noteId para evitar pérdida de cambios
    this.saveSubject
      .pipe(
        groupBy((item) => item.noteId),
        mergeMap((group$) =>
          group$.pipe(
            debounceTime(300),
            switchMap(({ noteId, changes }) => {
              if (changes.positionX !== undefined && changes.positionY !== undefined) {
                return this.api.updateNotePosition(this.projectId, noteId, {
                  positionX: changes.positionX,
                  positionY: changes.positionY,
                });
              }
              return this.api.updateNote(this.projectId, noteId, changes);
            })
          )
        ),
        takeUntil(this.destroy$)
      )
      .subscribe();

    // Registrar touchmove con passive: false para poder prevenir scroll/zoom
    this.touchMoveHandler = (e: TouchEvent) => this.onTouchMove(e);
    document.addEventListener('touchmove', this.touchMoveHandler, { passive: false });
  }

  ngOnDestroy(): void {
    this.signalR.leaveProject(this.projectId);
    this.destroy$.next();
    this.destroy$.complete();

    if (this.touchMoveHandler) {
      document.removeEventListener('touchmove', this.touchMoveHandler);
      this.touchMoveHandler = null;
    }
  }

  @HostListener('window:keydown', ['$event'])
  handleGlobalShortcuts(event: KeyboardEvent): void {
    if (event.key === 'n' || event.key === 'N') {
      const activeTag = document.activeElement?.tagName?.toLowerCase();
      const isEditable = (document.activeElement as HTMLElement)?.isContentEditable;
      if (activeTag === 'input' || activeTag === 'textarea' || activeTag === 'select' || isEditable) {
        return;
      }
      if (!this.showShareDialog && !this.showRename && !this.showApprovals) {
        event.preventDefault();
        this.addNote();
      }
    }
  }

  private loadProject(): void {
    this.api
      .getProject(this.projectId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (p) => {
          this.project = p;
          this.cdr.markForCheck();
        },
      });
  }

  private loadNotes(): void {
    this.api
      .getNotes(this.projectId, this.showArchived)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.notes = data.map((n) => this.toCardData(n));
          this.scheduleReminders();
          const targetNoteId = this.route.snapshot.queryParams['noteId'];
          if (targetNoteId) {
            this.highlightNote(targetNoteId);
          }
          this.cdr.markForCheck();
        },
      });
  }

  highlightNote(noteId: string): void {
    this.highlightedNoteId = noteId;
    this.cdr.markForCheck();
    setTimeout(() => {
      if (this.highlightedNoteId === noteId) {
        this.highlightedNoteId = null;
        this.cdr.markForCheck();
      }
    }, 5000);
  }

  private loadActionRequests(): void {
    this.api
      .getActionRequests(this.projectId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (reqs) => {
          this.actionRequests = reqs;
          this.cdr.markForCheck();
        },
      });
  }

  private joinSignalR(): void {
    this.signalR.start().then(() => {
      this.signalR.joinProject(this.projectId);
    });

    this.signalR.noteCreated$
      .pipe(takeUntil(this.destroy$))
      .subscribe((note) => {
        if (note.projectId === this.projectId) {
          const exists = this.notes.some((n) => n.id === note.id);
          if (!exists) {
            this.notes = [...this.notes, this.toCardData(note)];
            this.cdr.markForCheck();
          }
        }
      });

    this.signalR.noteUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe((note) => {
        if (note.projectId === this.projectId) {
          if (note.isArchived && !this.showArchived) {
            this.notes = this.notes.filter((n) => n.id !== note.id);
            this.cdr.markForCheck();
            return;
          }
          this.notes = this.notes.map((n) =>
            n.id === note.id ? this.toCardData(note) : n
          );
          this.scheduleReminders();
          this.cdr.markForCheck();
        }
      });

    this.signalR.noteDeleted$
      .pipe(takeUntil(this.destroy$))
      .subscribe((data) => {
        if (data.projectId === this.projectId) {
          this.notes = this.notes.filter((n) => n.id !== data.noteId);
          this.cdr.markForCheck();
        }
      });

    this.signalR.actionRequestCreated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.loadActionRequests();
      });

    this.signalR.actionRequestResolved$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.loadActionRequests();
      });

    this.signalR.memberAdded$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.loadProject();
      });
  }

  // ── CRUD ──

  addNote(): void {
    const offset = (this.notes.length % 10) * 30;
    this.addNoteAt(60 + offset, 60 + offset);
  }

  onCanvasDblClick(e: MouseEvent): void {
    const target = e.target as HTMLElement;
    if (target.classList.contains('canvas') || target.closest('.empty-canvas')) {
      const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
      const x = Math.max(20, e.clientX - rect.left);
      const y = Math.max(20, e.clientY - rect.top);
      this.addNoteAt(x, y);
    }
  }

  addNoteAt(x: number, y: number): void {
    if (this.addingNote) return;
    this.addingNote = true;
    this._maxWebZIndex++;
    this.api
      .createNote(this.projectId, {
        positionX: Math.round(x),
        positionY: Math.round(y),
        width: 280,
        height: 240,
        color: '#FFE066',
        zIndex: this._maxWebZIndex,
      })
      .subscribe({
        next: (note) => {
          this.notes = [...this.notes, this.toCardData(note)];
          this.addingNote = false;
          this.scheduleReminders();
          this.cdr.markForCheck();
        },
        error: () => {
          this.addingNote = false;
          this.cdr.markForCheck();
        },
      });
  }

  onNoteChange(data: NoteCardData): void {
    this.notes = this.notes.map((n) => (n.id === data.id ? data : n));
    this.saveSubject.next({
      noteId: data.id,
      changes: {
        title: data.title,
        content: data.content,
        color: data.color,
        isPinned: data.isPinned,
      },
    });
  }

  onNoteMetaChanged(meta: { noteId: string; tags: string; reminderAt: string | null; isTaskList: boolean }): void {
    const note = this.notes.find((n) => n.id === meta.noteId);
    if (!note) return;
    note.isTaskList = meta.isTaskList;
    note.tags = meta.tags || null;
    note.reminderAt = meta.reminderAt;
    this.notes = [...this.notes];
    this.api
      .updateNote(this.projectId, meta.noteId, {
        tags: meta.tags,
        reminderAt: meta.reminderAt,
        clearReminder: !meta.reminderAt,
        isTaskList: meta.isTaskList,
      })
      .subscribe(() => this.scheduleReminders());
  }

  // ── Recordatorios / notificaciones ──

  private scheduleReminders(): void {
    const now = Date.now();
    for (const note of this.notes) {
      if (!note.reminderAt || this.scheduledReminders.has(note.id)) continue;
      const due = new Date(note.reminderAt).getTime();
      if (isNaN(due)) continue;
      this.scheduledReminders.add(note.id);
      const delay = due - now;
      if (delay <= 0) {
        if (!this.notifiedReminders.has(note.id)) {
          this.notifiedReminders.add(note.id);
          this.notify(note);
        }
      } else {
        setTimeout(() => {
          if (!this.notifiedReminders.has(note.id)) {
            this.notifiedReminders.add(note.id);
            this.notify(note);
          }
        }, delay);
      }
    }
  }

  private notify(note: NoteCardData): void {
    if ('Notification' in window && Notification.permission === 'granted') {
      try {
        new Notification(note.title || 'Recordatorio', {
          body: this.stripHtml(note.content || ''),
          tag: note.id,
        });
        return;
      } catch {
        // fallback al toast
      }
    }
    this.showToast(note.title || 'Recordatorio');
  }

  private showToast(msg: string): void {
    this.toastMessage = msg;
    setTimeout(() => (this.toastMessage = ''), 6000);
  }

  private stripHtml(html: string): string {
    const div = document.createElement('div');
    div.innerHTML = html;
    return (div.textContent || '').trim();
  }

  onDeleteNote(noteId: string): void {
    const note = this.notes.find((n) => n.id === noteId);
    const isOwner = note?.isOwner ?? this.project?.ownerId === this.userId;

    if (isOwner) {
      this.confirmDialog.open(
        'Eliminar nota',
        '¿Estás seguro de eliminar esta nota? Esta acción no se puede deshacer.',
        () => this.deleteNoteNow(noteId)
      );
    } else {
      const ok = window.confirm(
        'Como miembro, eliminar una nota requiere la aprobación del propietario.\nSe enviará una solicitud al owner. ¿Continuar?'
      );
      if (ok) this.deleteNoteNow(noteId);
    }
  }

  private deleteNoteNow(noteId: string): void {
    this.api.deleteNote(this.projectId, noteId).subscribe({
      next: (result) => {
        if (result?.requestCreated) {
          // Miembro: la nota queda marcada como pendiente de aprobación
          this.loadNotes();
          this.loadActionRequests();
        } else if (result?.deleted) {
          this.notes = this.notes.filter((n) => n.id !== noteId);
        }
      },
    });
  }

  onArchiveNote(noteId: string): void {
    const note = this.notes.find((n) => n.id === noteId);
    if (!note) return;

    // Restaurar: todos los miembros pueden
    if (note.isArchived) {
      this.api.unarchiveNote(this.projectId, noteId).subscribe(() => this.loadNotes());
      return;
    }

    const isOwner = note.isOwner ?? this.project?.ownerId === this.userId;
    if (isOwner) {
      this.api.archiveNote(this.projectId, noteId).subscribe(() => this.loadNotes());
      return;
    }

    const ok = window.confirm(
      'Como miembro, archivar una nota requiere la aprobación del propietario.\nSe enviará una solicitud al owner. ¿Continuar?'
    );
    if (ok) {
      this.api.archiveNote(this.projectId, noteId).subscribe(() => {
        this.loadNotes();
        this.loadActionRequests();
      });
    }
  }

  toggleArchive(): void {
    this.showArchived = !this.showArchived;
    this.loadNotes();
  }

  // ── Renombrar proyecto ──

  openRenameDialog(): void {
    this.renameName = this.project?.name ?? '';
    this.showRename = true;
  }

  saveRename(): void {
    const name = this.renameName.trim();
    if (!name) return;
    this.api
      .updateProject(this.projectId, { name, description: this.project?.description })
      .subscribe({
        next: (p) => {
          this.project = p;
          this.showRename = false;
        },
      });
  }

  // ── Solicitudes de aprobación (owner) ──

  openApprovals(): void {
    this.loadActionRequests();
    this.showApprovals = true;
  }

  approveRequest(requestId: string): void {
    this.api.approveActionRequest(this.projectId, requestId).subscribe(() => {
      this.loadActionRequests();
    });
  }

  rejectRequest(requestId: string): void {
    this.api.rejectActionRequest(this.projectId, requestId).subscribe(() => {
      this.loadActionRequests();
    });
  }

  // ── Mover nota a otro proyecto ──

  private showMoveSidebar(): void {
    this.moveTargets = this.allProjects.filter((p) => p.id !== this.projectId);
    this.moveSidebarVisible = this.moveTargets.length > 0;
    this.hoveredProjectId = '';
  }

  private hideMoveSidebar(): void {
    this.moveSidebarVisible = false;
    this.hoveredProjectId = '';
  }

  private _maxWebZIndex = 100;

  onBringToFront(noteId: string): void {
    const note = this.notes.find((n) => n.id === noteId);
    if (!note) return;
    this._maxWebZIndex++;
    note.zIndex = this._maxWebZIndex;
    this.api.updateNote(this.projectId, noteId, { zIndex: note.zIndex }).subscribe();
    // Refresh array to trigger change detection
    this.notes = [...this.notes];
  }

  onSendToBack(noteId: string): void {
    const note = this.notes.find((n) => n.id === noteId);
    if (!note) return;
    this._maxWebZIndex = Math.max(this._maxWebZIndex, note.zIndex);
    note.zIndex = 0;
    this.api.updateNote(this.projectId, noteId, { zIndex: 0 }).subscribe();
    this.notes = [...this.notes];
  }

  // ── Drag ──

  onDragStart(event: { noteId: string; mouseX: number; mouseY: number }): void {
    const note = this.notes.find((n) => n.id === event.noteId);
    if (!note) return;
    this.dragging = {
      noteId: event.noteId,
      startX: event.mouseX,
      startY: event.mouseY,
      origX: note.positionX,
      origY: note.positionY,
    };
    this.showMoveSidebar();
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent): void {
    if (this.dragging) {
      const dx = event.clientX - this.dragging.startX;
      const dy = event.clientY - this.dragging.startY;
      const note = this.notes.find((n) => n.id === this.dragging!.noteId);
      if (note) {
        note.positionX = this.dragging.origX + dx;
        note.positionY = this.dragging.origY + dy;
        this.cdr.markForCheck();
      }
    }
    if (this.resizing) {
      const dx = event.clientX - this.resizing.startX;
      const dy = event.clientY - this.resizing.startY;
      const note = this.notes.find((n) => n.id === this.resizing!.noteId);
      if (note) {
        note.width = Math.max(120, this.resizing.origW + dx);
        note.height = Math.max(80, this.resizing.origH + dy);
        this.cdr.markForCheck();
      }
    }
  }

  onTouchMove(event: TouchEvent): void {
    if (!this.dragging && !this.resizing) return;
    event.preventDefault();
    if (this.dragging) {
      const dx = event.touches[0].clientX - this.dragging.startX;
      const dy = event.touches[0].clientY - this.dragging.startY;
      const note = this.notes.find((n) => n.id === this.dragging!.noteId);
      if (note) {
        note.positionX = this.dragging.origX + dx;
        note.positionY = this.dragging.origY + dy;
        this.cdr.markForCheck();
      }
    }
    if (this.resizing) {
      const dx = event.touches[0].clientX - this.resizing.startX;
      const dy = event.touches[0].clientY - this.resizing.startY;
      const note = this.notes.find((n) => n.id === this.resizing!.noteId);
      if (note) {
        note.width = Math.max(120, this.resizing.origW + dx);
        note.height = Math.max(80, this.resizing.origH + dy);
        this.cdr.markForCheck();
      }
    }
  }

  @HostListener('document:touchend')
  onTouchEnd(): void {
    this.onMouseUp();
  }

  @HostListener('document:mouseup')
  onMouseUp(): void {
    if (this.dragging) {
      const note = this.notes.find((n) => n.id === this.dragging!.noteId);
      if (note) {
        if (this.moveSidebarVisible && this.hoveredProjectId) {
          const target = this.hoveredProjectId;
          this.api.moveNote(this.projectId, note.id, target).subscribe(() => {
            this.notes = this.notes.filter((n) => n.id !== note.id);
          });
        } else {
          this.saveSubject.next({
            noteId: note.id,
            changes: { positionX: note.positionX, positionY: note.positionY },
          });
        }
      }
      this.dragging = null;
      this.hideMoveSidebar();
    }
    if (this.resizing) {
      const note = this.notes.find((n) => n.id === this.resizing!.noteId);
      if (note) {
        this.saveSubject.next({
          noteId: note.id,
          changes: { width: note.width, height: note.height },
        });
      }
      this.resizing = null;
    }
  }

  // ── Resize ──

  onResizeStart(event: { noteId: string; mouseX: number; mouseY: number }): void {
    const note = this.notes.find((n) => n.id === event.noteId);
    if (!note) return;
    this.resizing = {
      noteId: event.noteId,
      startX: event.mouseX,
      startY: event.mouseY,
      origW: note.width,
      origH: note.height,
    };
  }

  // ── Share / Invitations ──

  openShareDialog(): void {
    this.showShareDialog = true;
    this.shareTab = 'email';
    this.inviteMessage = '';
    this.invitationLinkError = '';
    this.loadInvitations();
  }

  inviteMember(email: string): void {
    if (!email.trim() || this.inviting) return;
    this.inviting = true;
    this.api.addMember(this.projectId, { email: email.trim() }).subscribe({
      next: () => {
        this.inviteMessage = `✅ ${email.trim()} agregado al proyecto`;
        this.loadProject();
        this.inviting = false;
      },
      error: () => {
        this.inviteMessage = '❌ No se pudo agregar. ¿El usuario existe?';
        this.inviting = false;
      },
    });
  }

  generateInvitationLink(): void {
    if (this.generatingLink) return;
    this.generatingLink = true;
    this.invitationLinkError = '';

    const req: any = { expiresInDays: this.inviteLinkExpiresDays };
    if (this.inviteLinkRestrictEmail && this.inviteLinkEmail.trim()) {
      req.invitedEmail = this.inviteLinkEmail.trim();
    }

    this.api.createInvitation(this.projectId, req).subscribe({
      next: (inv) => {
        this.fixInvitationLink(inv);
        this.invitations.unshift(inv);
        this.generatingLink = false;
      },
      error: () => {
        this.invitationLinkError = 'Error al generar el link de invitación.';
        this.generatingLink = false;
      },
    });
  }

  revokeInvitation(invitationId: string): void {
    this.api.revokeInvitation(this.projectId, invitationId).subscribe({
      next: () => {
        this.invitations = this.invitations.filter((i) => i.id !== invitationId);
      },
    });
  }

  loadInvitations(): void {
    this.api.getProjectInvitations(this.projectId).subscribe({
      next: (list) => {
        list.forEach((inv) => this.fixInvitationLink(inv));
        this.invitations = list;
      },
    });
  }

  removeMember(userId: string, displayName: string): void {
    this.confirmDialog.open(
      'Remover miembro',
      `¿Estás seguro de eliminar a ${displayName} del proyecto?`,
      () => {
        this.api.removeMember(this.projectId, userId).subscribe({
          next: () => {
            if (this.project) {
              this.project.members = this.project.members.filter((m) => m.userId !== userId);
            }
          },
        });
      }
    );
  }

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      // Opcional: mostrar tooltip o feedback
    });
  }

  goBack(): void {
    this.router.navigate(['/dashboard']);
  }

  /**
   * Corrige el invitationLink usando la URL actual del frontend,
   * para evitar que muestre localhost si el backend devuelve otra cosa.
   */
  private fixInvitationLink(inv: any): void {
    const origin = window.location.origin.replace(/\/+$/, '');
    if (inv?.token) {
      inv.invitationLink = `${origin}/invite/${inv.token}`;
    }
  }

  // ── Helpers ──

  private toCardData(n: NoteResponse | { id: string; projectId: string; title: string | null; content: string | null; color: string; positionX: number; positionY: number; width: number; height: number; isPinned: boolean; zIndex: number; isArchived: boolean; pendingArchive: boolean; pendingDelete: boolean; isOwner: boolean; isTaskList: boolean; tags: string | null; reminderAt: string | null }): NoteCardData {
    return {
      id: n.id,
      projectId: n.projectId,
      title: n.title,
      content: n.content,
      color: n.color,
      positionX: n.positionX,
      positionY: n.positionY,
      width: n.width,
      height: n.height,
      isPinned: n.isPinned,
      zIndex: n.zIndex,
      isArchived: n.isArchived,
      pendingArchive: n.pendingArchive,
      pendingDelete: n.pendingDelete,
      isOwner: n.isOwner,
      isTaskList: n.isTaskList,
      tags: n.tags,
      reminderAt: n.reminderAt,
    };
  }
}
