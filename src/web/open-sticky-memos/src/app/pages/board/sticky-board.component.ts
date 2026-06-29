import {
  Component,
  OnInit,
  OnDestroy,
  HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, debounceTime } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { SignalRService } from '../../core/signalr.service';
import { NoteCardComponent, NoteCardData } from './note-card.component';
import { NoteResponse, ProjectResponse } from '../../models';

@Component({
  selector: 'app-sticky-board',
  standalone: true,
  imports: [CommonModule, NoteCardComponent],
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
            <button class="btn-share" (click)="showShareDialog = true">
              👥 Invitar
            </button>
          }
          <button class="btn-add" (click)="addNote()">+ Nueva nota</button>
        </div>
      </div>

      <!-- Share dialog -->
      @if (showShareDialog) {
        <div class="dialog-overlay" (click)="showShareDialog = false">
          <div class="dialog" (click)="$event.stopPropagation()">
            <h4>Invitar miembros</h4>
            <input
              #emailInput
              type="email"
              placeholder="Email del usuario"
              class="input"
              (keyup.enter)="inviteMember(emailInput.value); emailInput.value = ''"
            />
            <button
              class="btn-primary"
              (click)="inviteMember(emailInput.value); emailInput.value = ''"
            >
              Invitar
            </button>
            @if (inviteMessage) {
              <p class="invite-msg">{{ inviteMessage }}</p>
            }
            @if (project?.members && project!.members.length > 0) {
              <div class="member-list">
                <h5>Miembros ({{ project!.members.length }})</h5>
                @for (m of project!.members; track m.userId) {
                  <div class="member-item">
                    <span>{{ m.displayName }}</span>
                    <span class="member-role">{{ m.role }}</span>
                  </div>
                }
              </div>
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
            (dragStart)="onDragStart($event)"
            (resizeStart)="onResizeStart($event)"
          />
        }

        @if (notes.length === 0) {
          <div class="empty-canvas">
            <p>No hay notas aún</p>
            <button class="btn-add" (click)="addNote()">Crear primera nota</button>
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
      .dialog h4 { margin: 0 0 12px; }
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
      .invite-msg { font-size: 13px; color: #667eea; margin: 4px 0; }
      .member-list { margin-top: 12px; }
      .member-item {
        display: flex;
        justify-content: space-between;
        padding: 6px 0;
        font-size: 13px;
        border-bottom: 1px solid #f0f0f0;
      }
      .member-role { color: #999; font-size: 11px; text-transform: capitalize; }
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

  // Drag / resize state
  private dragging: { noteId: string; startX: number; startY: number; origX: number; origY: number } | null = null;
  private resizing: { noteId: string; startX: number; startY: number; origW: number; origH: number } | null = null;

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
  }

  ngOnDestroy(): void {
    this.signalR.leaveProject(this.projectId);
    this.destroy$.next();
    this.destroy$.complete();
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
    const offset = this.notes.length * 30;
    this.api
      .createNote(this.projectId, {
        positionX: 50 + offset,
        positionY: 50 + offset,
        width: 250,
        height: 200,
        color: '#FFE066',
      })
      .subscribe({
        next: (note) => {
          this.notes = [...this.notes, this.toCardData(note)];
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

  onDeleteNote(noteId: string): void {
    this.api.deleteNote(this.projectId, noteId).subscribe({
      next: () => {
        this.notes = this.notes.filter((n) => n.id !== noteId);
      },
    });
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

  // ── Share ──

  inviteMember(email: string): void {
    if (!email.trim()) return;
    this.api.addMember(this.projectId, { email: email.trim() }).subscribe({
      next: () => {
        this.inviteMessage = `✅ ${email.trim()} agregado al proyecto`;
        this.loadProject();
      },
      error: () => {
        this.inviteMessage = '❌ No se pudo agregar. ¿El usuario existe?';
      },
    });
  }

  goBack(): void {
    this.router.navigate(['/dashboard']);
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
    };
  }
}
