import {
  Component,
  OnInit,
  OnDestroy,
  HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, debounceTime } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { SignalRService } from '../../core/signalr.service';
import { NoteCardComponent, NoteCardData } from './note-card.component';
import { NoteResponse, ProjectResponse, InvitationResponse } from '../../models';

@Component({
  selector: 'app-sticky-board',
  standalone: true,
  imports: [CommonModule, FormsModule, NoteCardComponent],
  template: `
    <div class="board-container">
      <!-- Top bar -->
      <div class="board-header">
        <button class="back-btn" (click)="goBack()">← Volver</button>
        @if (project) {
          <h2>{{ project.name }}</h2>
        }
        <div class="header-actions">
          @if (project && project.ownerId === userId) {
            <button class="btn-share" (click)="openShareDialog()">
              👥 Invitar
            </button>
          }
          <button class="btn-add" [disabled]="addingNote" (click)="addNote()">
            {{ addingNote ? '...' : '+ Nueva nota' }}
          </button>
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
                      <span class="member-role-badge">{{ m.role }}</span>
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
        (mousemove)="onMouseMove($event)"
        (mouseup)="onMouseUp()"
        (mouseleave)="onMouseUp()"
      >
        @for (note of notes; track note.id) {
          <app-note-card
            [note]="note"
            (noteChange)="onNoteChange($event)"
            (deleteNote)="onDeleteNote($event)"
            (bringToFrontNote)="onBringToFront($event)"
            (sendToBackNote)="onSendToBack($event)"
            (dragStart)="onDragStart($event)"
            (resizeStart)="onResizeStart($event)"
          />
        }

        @if (notes.length === 0) {
          <div class="empty-canvas">
            <p>No hay notas aún</p>
            <button class="btn-add" [disabled]="addingNote" (click)="addNote()">{{ addingNote ? '...' : 'Crear primera nota' }}</button>
          </div>
        }
      </div>
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
        gap: 16px;
        padding: 12px 20px;
        background: white;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
        z-index: 20;
      }
      .board-header h2 {
        margin: 0;
        font-size: 16px;
        flex: 1;
      }
      .header-actions {
        display: flex;
        gap: 8px;
      }
      .back-btn,
      .btn-share,
      .btn-add {
        padding: 8px 16px;
        border-radius: 8px;
        border: none;
        font-size: 13px;
        cursor: pointer;
        font-weight: 500;
      }
      .back-btn {
        background: transparent;
        color: #667eea;
      }
      .btn-share {
        background: #f0f0f0;
        color: #555;
      }
      .btn-add {
        background: #667eea;
        color: white;
      }
      .btn-primary {
        padding: 8px 20px;
        border-radius: 8px;
        border: none;
        font-size: 14px;
        cursor: pointer;
        background: #667eea;
        color: white;
        font-weight: 500;
      }
      .btn-secondary {
        padding: 8px 20px;
        border-radius: 8px;
        border: none;
        font-size: 14px;
        cursor: pointer;
        background: #f0f0f0;
        color: #555;
        margin-top: 8px;
        width: 100%;
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
        color: #999;
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
      .member-role-badge {
        font-size: 10px;
        background: #edf2f7;
        padding: 2px 8px;
        border-radius: 10px;
        color: #666;
        text-transform: capitalize;
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

  // Debounce for auto-save
  private saveSubject = new Subject<{ noteId: string; changes: Partial<NoteCardData> }>();
  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: ApiService,
    private signalR: SignalRService
  ) {}

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id')!;
    this.userId = JSON.parse(localStorage.getItem('osm_user') || '{}').id;

    this.loadProject();
    this.loadNotes();
    this.joinSignalR();

    // Auto-save with 500ms debounce
    this.saveSubject.pipe(debounceTime(500)).subscribe(({ noteId, changes }) => {
      this.api.updateNote(this.projectId, noteId, changes).subscribe();
    });

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

  @HostListener('document:keydown.delete')
  handleKeydown(): void {
    // Delete is handled via the note card (for now)
  }

  private loadProject(): void {
    this.api.getProject(this.projectId).subscribe({
      next: (p) => (this.project = p),
    });
  }

  private loadNotes(): void {
    this.api.getNotes(this.projectId).subscribe({
      next: (data) => {
        this.notes = data.map((n) => this.toCardData(n));
      },
    });
  }

  private joinSignalR(): void {
    this.signalR.start().then(() => {
      this.signalR.joinProject(this.projectId);
    });

    this.signalR.noteCreated$.subscribe((note) => {
      if (note.projectId === this.projectId) {
        const exists = this.notes.some((n) => n.id === note.id);
        if (!exists) {
          this.notes = [...this.notes, this.toCardData(note)];
        }
      }
    });

    this.signalR.noteUpdated$.subscribe((note) => {
      if (note.projectId === this.projectId) {
        this.notes = this.notes.map((n) =>
          n.id === note.id ? this.toCardData(note) : n
        );
      }
    });

    this.signalR.noteDeleted$.subscribe((data) => {
      if (data.projectId === this.projectId) {
        this.notes = this.notes.filter((n) => n.id !== data.noteId);
      }
    });

    this.signalR.memberAdded$.subscribe(() => {
      this.loadProject(); // Refresh member list
    });
  }

  // ── CRUD ──

  addNote(): void {
    if (this.addingNote) return;
    this.addingNote = true;
    const offset = this.notes.length * 30;
    this._maxWebZIndex++;
    this.api
      .createNote(this.projectId, {
        positionX: 50 + offset,
        positionY: 50 + offset,
        width: 280,
        height: 240,
        color: '#FFE066',
        zIndex: this._maxWebZIndex,
      })
      .subscribe({
        next: (note) => {
          this.notes = [...this.notes, this.toCardData(note)];
          this.addingNote = false;
        },
        error: () => (this.addingNote = false),
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

  onDeleteNote(noteId: string): void {
    this.api.deleteNote(this.projectId, noteId).subscribe({
      next: () => {
        this.notes = this.notes.filter((n) => n.id !== noteId);
      },
    });
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
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent): void {
    if (this.dragging) {
      const dx = event.clientX - this.dragging.startX;
      const dy = event.clientY - this.dragging.startY;
      this.notes = this.notes.map((n) =>
        n.id === this.dragging!.noteId
          ? { ...n, positionX: this.dragging!.origX + dx, positionY: this.dragging!.origY + dy }
          : n
      );
    }
    if (this.resizing) {
      const dx = event.clientX - this.resizing.startX;
      const dy = event.clientY - this.resizing.startY;
      this.notes = this.notes.map((n) =>
        n.id === this.resizing!.noteId
          ? { ...n, width: Math.max(120, this.resizing!.origW + dx), height: Math.max(80, this.resizing!.origH + dy) }
          : n
      );
    }
  }

  onTouchMove(event: TouchEvent): void {
    if (!this.dragging && !this.resizing) return;
    event.preventDefault();
    if (this.dragging) {
      const dx = event.touches[0].clientX - this.dragging.startX;
      const dy = event.touches[0].clientY - this.dragging.startY;
      this.notes = this.notes.map((n) =>
        n.id === this.dragging!.noteId
          ? { ...n, positionX: this.dragging!.origX + dx, positionY: this.dragging!.origY + dy }
          : n
      );
    }
    if (this.resizing) {
      const dx = event.touches[0].clientX - this.resizing.startX;
      const dy = event.touches[0].clientY - this.resizing.startY;
      this.notes = this.notes.map((n) =>
        n.id === this.resizing!.noteId
          ? { ...n, width: Math.max(120, this.resizing!.origW + dx), height: Math.max(80, this.resizing!.origH + dy) }
          : n
      );
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
        this.saveSubject.next({
          noteId: note.id,
          changes: { positionX: note.positionX, positionY: note.positionY },
        });
      }
      this.dragging = null;
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

  private toCardData(n: NoteResponse): NoteCardData {
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
    };
  }
}
