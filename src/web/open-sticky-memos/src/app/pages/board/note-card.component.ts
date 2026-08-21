import {
  Component,
  Input,
  Output,
  EventEmitter,
  ViewChild,
  ElementRef,
  AfterViewInit,
  OnDestroy,
  HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface NoteCardData {
  id: string;
  projectId: string;
  title: string | null;
  content: string | null;
  color: string;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  isPinned: boolean;
  zIndex: number;
  isArchived: boolean;
  pendingArchive: boolean;
  pendingDelete: boolean;
  isOwner: boolean;
  isTaskList: boolean;
  tags: string | null;
  reminderAt: string | null;
}

@Component({
  selector: 'app-note-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="note-card"
      [style.left.px]="note.positionX"
      [style.top.px]="note.positionY"
      [style.width.px]="note.width"
      [style.height.px]="note.height"
      [style.background-color]="note.color"
      [style.z-index]="note.zIndex"
      [class.editing]="isEditing"
      [class.pinned]="note.isPinned"
      [class.pending]="note.pendingDelete || note.pendingArchive"
      [class.archived]="note.isArchived"
      (mousedown)="onMouseDown($event)"
      (touchstart)="onTouchStart($event)"
      (dblclick)="startEdit()"
    >
      <!-- Resize handle -->
      <div class="resize-handle" (mousedown)="onResizeStart($event)" (touchstart)="onResizeTouchStart($event)"></div>

      <!-- Pin indicator (top-left) -->
      @if (note.isPinned) {
        <span class="pin">📌</span>
      }

      <!-- Badge de pendiente de aprobación -->
      @if (note.pendingDelete || note.pendingArchive) {
        <div class="pending-badge">⏳ Pendiente de aprobación</div>
      }

      <!-- Action buttons (top-right) -->
      <div class="note-actions">
        <button class="action-btn" (click)="bringToFront()" title="Traer al frente">⬆</button>
        <button class="action-btn" (click)="sendToBack()" title="Enviar al fondo">⬇</button>
        <button
          class="action-btn"
          (click)="toggleArchive()"
          [title]="note.isArchived ? 'Restaurar nota' : 'Archivar nota'"
        >
          {{ note.isArchived ? '📥' : '🗄️' }}
        </button>
        <button class="delete-btn" (click)="deleteNote.emit(note.id); $event.stopPropagation()" title="Eliminar nota">×</button>
      </div>

      @if (isEditing) {
        <div class="edit-mode">
          <input
            #titleInput
            class="edit-title"
            [value]="editTitle"
            (input)="editTitle = $any($event.target).value"
            placeholder="Título"
            (keydown.escape)="cancelEdit()"
          />
          <div class="format-toolbar">
            <button type="button" (mousedown)="$event.preventDefault()" (click)="execCmd('bold')" title="Negrita"><b>B</b></button>
            <button type="button" (mousedown)="$event.preventDefault()" (click)="execCmd('italic')" title="Cursiva"><i>I</i></button>
            <button type="button" (mousedown)="$event.preventDefault()" (click)="execCmd('underline')" title="Subrayado"><u>U</u></button>
            <button type="button" (mousedown)="$event.preventDefault()" (click)="execCmd('insertUnorderedList')" title="Lista con viñetas">•</button>
            <button type="button" (mousedown)="$event.preventDefault()" (click)="toggleEmoji()" title="Emoji">😊</button>
            <span class="toolbar-sep"></span>
            <button type="button" (mousedown)="$event.preventDefault()" (click)="toggleTaskMode()" [class.active]="editTaskList" title="Lista de tareas (checklist)">☑</button>
            <button type="button" (mousedown)="$event.preventDefault()" (click)="toggleTagsEditor()" title="Etiquetas">🏷</button>
            <button type="button" (mousedown)="$event.preventDefault()" (click)="toggleReminderEditor()" title="Recordatorio">🔔</button>
          </div>

          @if (editTaskList) {
            <div class="task-editor">
              @for (t of editTasks; track $index) {
                <div class="task-edit-row">
                  <input type="checkbox" [checked]="t.checked" (change)="t.checked = $any($event.target).checked" />
                  <input class="task-input" [value]="t.text" (input)="t.text = $any($event.target).value" placeholder="Tarea..." (keydown.enter)="addTask()" />
                  <button type="button" class="task-remove" (click)="removeTask($index)" title="Quitar tarea">✕</button>
                </div>
              }
              <button type="button" class="task-add" (click)="addTask()">＋ Agregar tarea</button>
            </div>
          } @else {
            @if (showEmoji) {
              <div class="emoji-palette">
                @for (em of emojis; track em) {
                  <button type="button" (mousedown)="$event.preventDefault()" (click)="insertEmoji(em)">{{ em }}</button>
                }
              </div>
            }
            <div
              #editorEl
              class="edit-content"
              contenteditable="true"
              [innerHTML]="editContent"
              (keydown.escape)="cancelEdit()"
            ></div>
          }

          @if (showTagsEditor) {
            <div class="meta-editor">
              <input class="meta-input" [value]="editTags" (input)="editTags = $any($event.target).value" (keyup.enter)="applyTags()" placeholder="Etiquetas (separadas por coma)" />
              <button type="button" class="btn-sm" (click)="applyTags()">Aplicar</button>
            </div>
          }
          @if (showReminderEditor) {
            <div class="meta-editor">
              <input type="datetime-local" class="meta-input" [value]="editReminder" (input)="editReminder = $any($event.target).value" />
              <button type="button" class="btn-sm" (click)="applyReminder()">Fijar</button>
              <button type="button" class="btn-sm" (click)="clearReminder()">Quitar</button>
            </div>
          }

          <div class="edit-actions">
            <div class="color-picker">
              @for (c of colors; track c) {
                <button
                  class="color-btn"
                  [style.background]="c"
                  [class.selected]="c === editColor"
                  (click)="editColor = c"
                ></button>
              }
            </div>
            <div class="action-btns">
              <button
                class="pin-btn"
                [class.active]="editPinned"
                (click)="editPinned = !editPinned"
                title="Fijar nota"
              >
                📌
              </button>
              <button class="cancel-btn" (click)="cancelEdit()" title="Cancelar edición">✕</button>
              <button class="save-btn" (click)="saveEdit()" title="Guardar">✓</button>
            </div>
          </div>
        </div>
      } @else {
        <div class="view-mode">
          <div class="view-title-row">
            @if (note.title) {
              <h4 class="note-title">{{ note.title }}</h4>
            }
            @if (note.reminderAt) {
              <span class="reminder-badge" title="Recordatorio">🔔</span>
            }
          </div>
          @if (note.isTaskList) {
            <div class="task-view">
              @for (task of tasks; track $index) {
                <label class="task-item" [class.done]="task.checked">
                  <input type="checkbox" [checked]="task.checked" (change)="toggleTask($index, $any($event.target).checked)" />
                  <span class="task-text">{{ task.text }}</span>
                </label>
              } @empty {
                <p class="note-placeholder">Sin tareas</p>
              }
            </div>
          } @else {
            @if (note.content) {
              <div class="note-content" [innerHTML]="note.content"></div>
            } @else if (!note.title) {
              <p class="note-placeholder">Doble click para editar</p>
            }
          }
          @if (tagList.length > 0) {
            <div class="tag-chips">
              @for (tag of tagList; track tag) {
                <span class="tag-chip">#{{ tag }}</span>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [
    `
      .note-card {
        position: absolute;
        touch-action: none;
        border-radius: 8px;
        box-shadow: 0 3px 10px rgba(0, 0, 0, 0.15), 0 1px 2px rgba(0, 0, 0, 0.1);
        cursor: grab;
        overflow: hidden;
        transition: box-shadow 0.15s;
        user-select: none;
        font-family: 'Segoe UI', system-ui, sans-serif;
      }
      .note-card:hover {
        box-shadow: 0 6px 20px rgba(0, 0, 0, 0.2);
      }
      .note-card:active {
        cursor: grabbing;
      }
      .note-card.pinned {
        cursor: default;
        box-shadow: 0 3px 12px rgba(0, 0, 0, 0.2), 0 0 0 2px rgba(102, 126, 234, 0.3);
      }
      .note-card.pinned:active {
        cursor: default;
      }
      .note-card.editing {
        cursor: default;
        box-shadow: 0 6px 24px rgba(0, 0, 0, 0.25);
        z-index: 10;
      }
      .resize-handle {
        position: absolute;
        touch-action: none;
        bottom: 0;
        right: 0;
        width: 16px;
        height: 16px;
        cursor: nwse-resize;
        background: linear-gradient(135deg, transparent 50%, rgba(0, 0, 0, 0.15) 50%);
        border-radius: 0 0 8px 0;
      }
      .pin {
        position: absolute;
        top: 6px;
        left: 8px;
        font-size: 14px;
        opacity: 0.7;
        z-index: 6;
      }
      .note-actions {
        position: absolute;
        top: 4px;
        right: 4px;
        display: flex;
        gap: 2px;
        opacity: 0;
        transition: opacity 0.15s;
        z-index: 5;
      }
      .note-card:hover .note-actions {
        opacity: 1;
      }
      .action-btn {
        width: 24px;
        height: 24px;
        border-radius: 4px;
        border: none;
        background: rgba(0,0,0,0.08);
        color: #888;
        font-size: 12px;
        line-height: 1;
        cursor: pointer;
        display: flex;
        align-items: center;
        justify-content: center;
        transition: background 0.15s;
      }
      .action-btn:hover {
        background: rgba(0,0,0,0.15);
      }
      .view-mode {
        padding: 16px;
        height: 100%;
        box-sizing: border-box;
        overflow-y: auto;
      }
      .note-title {
        margin: 0 0 6px;
        font-size: 14px;
        font-weight: 600;
        color: rgba(0, 0, 0, 0.8);
      }
      .note-content {
        margin: 0;
        font-size: 13px;
        line-height: 1.5;
        color: rgba(0, 0, 0, 0.7);
        white-space: pre-wrap;
        word-wrap: break-word;
      }
      .note-placeholder {
        margin: 0;
        font-size: 12px;
        color: rgba(0, 0, 0, 0.3);
        font-style: italic;
      }
      .edit-mode {
        display: flex;
        flex-direction: column;
        height: 100%;
        padding: 12px;
        box-sizing: border-box;
      }
      .edit-title {
        border: none;
        background: rgba(255, 255, 255, 0.5);
        padding: 6px 8px;
        border-radius: 4px;
        font-size: 14px;
        font-weight: 600;
        margin-bottom: 6px;
        outline: none;
        font-family: inherit;
        width: 100%;
        box-sizing: border-box;
      }
      .edit-content {
        border: none;
        background: rgba(255, 255, 255, 0.5);
        padding: 6px 8px;
        border-radius: 4px;
        font-size: 13px;
        line-height: 1.5;
        resize: none;
        outline: none;
        flex: 1;
        font-family: inherit;
        width: 100%;
        box-sizing: border-box;
      }
      .edit-actions {
        display: flex;
        flex-direction: column;
        gap: 6px;
        margin-top: 8px;
      }
      .color-picker {
        display: flex;
        gap: 4px;
      }
      .color-btn {
        width: 20px;
        height: 20px;
        border-radius: 50%;
        border: 2px solid transparent;
        cursor: pointer;
        padding: 0;
      }
      .color-btn.selected {
        border-color: rgba(0, 0, 0, 0.5);
      }
      .action-btns {
        display: flex;
        gap: 4px;
        justify-content: flex-end;
      }
      .pin-btn,
      .save-btn {
        border: none;
        background: rgba(255, 255, 255, 0.6);
        border-radius: 4px;
        padding: 4px 8px;
        cursor: pointer;
        font-size: 14px;
      }
      .pin-btn.active {
        background: rgba(255, 255, 255, 0.9);
      }
      .save-btn {
        background: #667eea;
        color: white;
        font-weight: bold;
        padding: 4px 12px;
      }
      .delete-btn {
        width: 24px;
        height: 24px;
        border-radius: 4px;
        border: none;
        background: rgba(0,0,0,0.08);
        color: #888;
        font-size: 16px;
        line-height: 1;
        cursor: pointer;
        display: flex;
        align-items: center;
        justify-content: center;
        transition: background 0.15s;
      }
      .delete-btn:hover {
        background: #e74c3c;
        color: white;
      }
      .note-card.pending {
        opacity: 0.65;
      }
      .note-card.archived {
        filter: grayscale(0.5);
        opacity: 0.75;
      }
      .pending-badge {
        position: absolute;
        top: 6px;
        left: 50%;
        transform: translateX(-50%);
        background: rgba(0, 0, 0, 0.55);
        color: #fff;
        font-size: 10px;
        padding: 2px 8px;
        border-radius: 10px;
        z-index: 7;
        white-space: nowrap;
        pointer-events: none;
      }
      .format-toolbar {
        display: flex;
        gap: 2px;
        margin-bottom: 6px;
        background: rgba(255, 255, 255, 0.7);
        border-radius: 6px;
        padding: 3px;
        width: max-content;
      }
      .format-toolbar button {
        width: 26px;
        height: 24px;
        border: none;
        background: transparent;
        border-radius: 4px;
        cursor: pointer;
        font-size: 13px;
        color: #444;
      }
      .format-toolbar button:hover {
        background: rgba(0, 0, 0, 0.08);
      }
      .edit-content {
        border: none;
        background: rgba(255, 255, 255, 0.5);
        padding: 6px 8px;
        border-radius: 4px;
        font-size: 13px;
        line-height: 1.5;
        resize: none;
        outline: none;
        flex: 1;
        font-family: inherit;
        width: 100%;
        box-sizing: border-box;
        overflow-y: auto;
        min-height: 60px;
        text-align: left;
      }
      .emoji-palette {
        display: grid;
        grid-template-columns: repeat(10, 1fr);
        gap: 2px;
        background: white;
        border: 1px solid #ddd;
        border-radius: 8px;
        padding: 6px;
        max-height: 150px;
        overflow-y: auto;
        margin-bottom: 6px;
      }
      .emoji-palette button {
        border: none;
        background: transparent;
        cursor: pointer;
        font-size: 16px;
        padding: 2px;
        border-radius: 4px;
      }
      .emoji-palette button:hover {
        background: #f1f5f9;
      }
      .cancel-btn {
        border: none;
        background: rgba(0, 0, 0, 0.06);
        border-radius: 4px;
        padding: 4px 8px;
        cursor: pointer;
        font-size: 13px;
        color: #555;
      }
      .note-content {
        margin: 0;
        font-size: 13px;
        line-height: 1.5;
        color: rgba(0, 0, 0, 0.7);
        word-wrap: break-word;
        white-space: pre-wrap;
      }
      .note-content ul {
        margin: 4px 0;
        padding-left: 20px;
      }
      .toolbar-sep {
        width: 1px;
        height: 16px;
        background: rgba(0, 0, 0, 0.12);
        margin: 0 4px;
        align-self: center;
      }
      .format-toolbar button.active {
        background: rgba(102, 126, 234, 0.25);
      }
      .view-title-row {
        display: flex;
        align-items: center;
        gap: 6px;
      }
      .reminder-badge {
        font-size: 11px;
      }
      .task-view {
        display: flex;
        flex-direction: column;
        gap: 2px;
      }
      .task-item {
        display: flex;
        align-items: flex-start;
        gap: 6px;
        font-size: 13px;
        line-height: 1.4;
        color: rgba(0, 0, 0, 0.75);
        cursor: pointer;
      }
      .task-item input[type='checkbox'] {
        margin-top: 2px;
        cursor: pointer;
        flex-shrink: 0;
      }
      .task-item.done .task-text {
        text-decoration: line-through;
        color: rgba(0, 0, 0, 0.4);
      }
      .task-editor {
        display: flex;
        flex-direction: column;
        gap: 4px;
        padding: 4px 0;
      }
      .task-edit-row {
        display: flex;
        align-items: center;
        gap: 4px;
      }
      .task-edit-row input[type='checkbox'] {
        cursor: pointer;
        flex-shrink: 0;
      }
      .task-input {
        flex: 1;
        border: none;
        background: rgba(255, 255, 255, 0.5);
        padding: 4px 6px;
        border-radius: 4px;
        font-size: 13px;
        font-family: inherit;
        outline: none;
        min-width: 0;
      }
      .task-remove {
        border: none;
        background: transparent;
        color: #aaa;
        cursor: pointer;
        width: 20px;
        height: 20px;
        border-radius: 4px;
      }
      .task-remove:hover {
        background: rgba(0, 0, 0, 0.08);
        color: #e53e3e;
      }
      .task-add {
        border: none;
        background: transparent;
        color: #667eea;
        cursor: pointer;
        font-size: 12px;
        text-align: left;
        padding: 4px 6px;
      }
      .meta-editor {
        display: flex;
        align-items: center;
        gap: 4px;
        margin-top: 6px;
        background: rgba(255, 255, 255, 0.7);
        border-radius: 6px;
        padding: 4px;
      }
      .meta-input {
        flex: 1;
        border: 1px solid #e2e8f0;
        border-radius: 4px;
        padding: 5px 6px;
        font-size: 12px;
        font-family: inherit;
        min-width: 0;
        background: white;
      }
      .tag-chips {
        display: flex;
        flex-wrap: wrap;
        gap: 4px;
        margin-top: 6px;
      }
      .tag-chip {
        background: #edf2f7;
        color: #64748b;
        font-size: 10px;
        padding: 2px 8px;
        border-radius: 10px;
      }
    `,
  ],
})
export class NoteCardComponent implements AfterViewInit, OnDestroy {
  @Input() note!: NoteCardData;
  @Output() noteChange = new EventEmitter<NoteCardData>();
  private touchMoveHandler: ((e: TouchEvent) => void) | null = null;
  @Output() deleteNote = new EventEmitter<string>();
  @Output() archiveNote = new EventEmitter<string>();
  @Output() metaChange = new EventEmitter<{ noteId: string; tags: string; reminderAt: string | null; isTaskList: boolean }>();
  @Output() bringToFrontNote = new EventEmitter<string>();
  @Output() sendToBackNote = new EventEmitter<string>();
  @Output() dragStart = new EventEmitter<{ noteId: string; mouseX: number; mouseY: number }>();
  @Output() resizeStart = new EventEmitter<{ noteId: string; mouseX: number; mouseY: number }>();

  @ViewChild('editorEl') editorEl?: ElementRef<HTMLDivElement>;

  readonly colors = [
    '#FFE066', '#FFB3BA', '#BAFFC9', '#BAE1FF',
    '#E8BAFF', '#FFD9BA', '#BAFFEC', '#FFF3BA',
  ];

  readonly emojis = [
    '😀','😄','😊','🙂','😉','😍','😘','😎','🤔','😴',
    '👍','👎','👏','🙌','🤝','💪','🙏','👋','✋','👌',
    '❤️','💙','💚','💛','💜','⭐','✨','🔥','💯','🎉',
    '✅','❌','⚠️','📌','📝','📁','🗂️','🔔','⏰','💡',
    '🚀','🍕','☕','🍎','🌮','⚽','🎮','🎵','🌙','🎁',
  ];

  isEditing = false;
  editTitle = '';
  editContent = '';
  editColor = '';
  editPinned = false;
  showEmoji = false;

  // Lista de tareas
  editTaskList = false;
  editTasks: { text: string; checked: boolean }[] = [];

  // Etiquetas y recordatorio
  editTags = '';
  editReminder = '';
  showTagsEditor = false;
  showReminderEditor = false;

  /** Tareas parseadas desde el contenido (para la vista del modo tareas). */
  get tasks(): { text: string; checked: boolean }[] {
    return this.parseTasks(this.note.content);
  }

  /** Etiquetas como lista (para los chips). */
  get tagList(): string[] {
    if (!this.note.tags) return [];
    return this.note.tags
      .split(',')
      .map((t) => t.trim())
      .filter((t) => t.length > 0);
  }

  ngAfterViewInit(): void {}

  ngOnInit(): void {
    // Registrar con passive: false para que preventDefault() bloquee scroll/zoom
    this.touchMoveHandler = (e: TouchEvent) => this.onDocumentTouchMove(e);
    document.addEventListener('touchmove', this.touchMoveHandler, { passive: false });
  }

  ngOnDestroy(): void {
    if (this.touchMoveHandler) {
      document.removeEventListener('touchmove', this.touchMoveHandler);
      this.touchMoveHandler = null;
    }
  }

  startEdit(): void {
    this.editTitle = this.note.title ?? '';
    this.editContent = this.note.content ?? '';
    this.editColor = this.note.color;
    this.editPinned = this.note.isPinned;
    this.editTaskList = this.note.isTaskList;
    this.editTasks = this.parseTasks(this.note.content);
    this.editTags = this.note.tags ?? '';
    this.editReminder = this.toLocalInput(this.note.reminderAt);
    this.showEmoji = false;
    this.showTagsEditor = false;
    this.showReminderEditor = false;
    this.isEditing = true;
  }

  cancelEdit(): void {
    this.isEditing = false;
    this.showEmoji = false;
    this.showTagsEditor = false;
    this.showReminderEditor = false;
  }

  saveEdit(): void {
    let html: string;
    if (this.editTaskList) {
      html = this.serializeTasks(this.editTasks);
    } else {
      html = this.editorEl?.nativeElement.innerHTML.trim() ?? this.editContent;
    }
    this.isEditing = false;
    this.showEmoji = false;
    this.showTagsEditor = false;
    this.showReminderEditor = false;
    this.noteChange.emit({
      ...this.note,
      title: this.editTitle || null,
      content: html && html !== '<br>' ? html : null,
      color: this.editColor,
      isPinned: this.editPinned,
    });
    this.emitMeta();
  }

  private emitMeta(): void {
    this.metaChange.emit({
      noteId: this.note.id,
      tags: this.editTags.trim(),
      reminderAt: this.toIso(this.editReminder),
      isTaskList: this.editTaskList,
    });
  }

  // ── Lista de tareas ──

  private parseTasks(content: string | null): { text: string; checked: boolean }[] {
    const tasks: { text: string; checked: boolean }[] = [];
    if (!content) return tasks;
    const regex = /<li([^>]*)>([\s\S]*?)<\/li>/gi;
    let m: RegExpExecArray | null;
    while ((m = regex.exec(content)) !== null) {
      const attrs = m[1] || '';
      tasks.push({
        text: this.stripTags(m[2]),
        checked: attrs.toLowerCase().includes('checked'),
      });
    }
    return tasks;
  }

  private serializeTasks(tasks: { text: string; checked: boolean }[]): string {
    let html = '<ul class="osm-tasks">';
    for (const t of tasks) {
      html += `<li${t.checked ? ' class="checked"' : ''}>${this.escapeHtml(t.text)}</li>`;
    }
    html += '</ul>';
    return html;
  }

  private stripTags(html: string): string {
    const div = document.createElement('div');
    div.innerHTML = html;
    return (div.textContent || '').trim();
  }

  private escapeHtml(s: string): string {
    return s
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  private contentToTasks(content: string | null): { text: string; checked: boolean }[] {
    if (!content) return [{ text: '', checked: false }];
    const lines = this.stripTags(content)
      .split('\n')
      .map((l) => l.trim())
      .filter((l) => l.length > 0);
    return lines.map((l) => ({ text: l, checked: false }));
  }

  private tasksToRichHtml(tasks: { text: string; checked: boolean }[]): string {
    const items = tasks.filter((t) => t.text.trim().length > 0);
    if (items.length === 0) return '';
    return '<ul>' + items.map((t) => `<li>${this.escapeHtml(t.text)}</li>`).join('') + '</ul>';
  }

  /** Marca/desmarca una tarea desde la vista (sin entrar a edición). */
  toggleTask(index: number, checked: boolean): void {
    const tasks = this.parseTasks(this.note.content);
    if (index >= tasks.length) return;
    tasks[index].checked = checked;
    const content = this.serializeTasks(tasks);
    this.noteChange.emit({ ...this.note, content: content || null });
  }

  toggleTaskMode(): void {
    this.editTaskList = !this.editTaskList;
    if (this.editTaskList) {
      this.editTasks = this.editTasks.length ? this.editTasks : this.contentToTasks(this.editContent);
    } else {
      this.editContent = this.tasksToRichHtml(this.editTasks);
    }
  }

  addTask(): void {
    this.editTasks.push({ text: '', checked: false });
  }

  removeTask(index: number): void {
    this.editTasks.splice(index, 1);
  }

  // ── Etiquetas y recordatorio ──

  toggleTagsEditor(): void {
    this.showTagsEditor = !this.showTagsEditor;
    this.showReminderEditor = false;
  }

  applyTags(): void {
    this.showTagsEditor = false;
    this.emitMeta();
  }

  toggleReminderEditor(): void {
    this.showReminderEditor = !this.showReminderEditor;
    this.showTagsEditor = false;
  }

  applyReminder(): void {
    this.showReminderEditor = false;
    this.emitMeta();
  }

  clearReminder(): void {
    this.editReminder = '';
    this.showReminderEditor = false;
    this.emitMeta();
  }

  private toIso(input: string): string | null {
    if (!input) return null;
    const d = new Date(input);
    return isNaN(d.getTime()) ? null : d.toISOString();
  }

  private toLocalInput(iso: string | null): string {
    if (!iso) return '';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  toggleArchive(): void {
    this.archiveNote.emit(this.note.id);
  }

  execCmd(command: string): void {
    this.editorEl?.nativeElement.focus();
    document.execCommand(command, false);
  }

  toggleEmoji(): void {
    this.showEmoji = !this.showEmoji;
  }

  insertEmoji(emoji: string): void {
    this.editorEl?.nativeElement.focus();
    document.execCommand('insertText', false, emoji);
  }

  bringToFront(): void {
    this.bringToFrontNote.emit(this.note.id);
  }

  sendToBack(): void {
    this.sendToBackNote.emit(this.note.id);
  }

  onMouseDown(event: MouseEvent): void {
    if (this.isEditing || this.note.isPinned) return;
    this.dragStart.emit({
      noteId: this.note.id,
      mouseX: event.clientX,
      mouseY: event.clientY,
    });
  }

  onResizeStart(event: MouseEvent): void {
    if (this.note.isPinned) return;
    event.stopPropagation();
    this.resizeStart.emit({
      noteId: this.note.id,
      mouseX: event.clientX,
      mouseY: event.clientY,
    });
  }

  private touchStartPos: { x: number; y: number } | null = null;
  private touchIsDragging = false;

  onTouchStart(event: TouchEvent): void {
    if (this.isEditing) return;
    // Solo guardamos la posición; el drag se inicia solo si hay movimiento
    this.touchStartPos = {
      x: event.touches[0].clientX,
      y: event.touches[0].clientY,
    };
    this.touchIsDragging = false;
  }

  onResizeTouchStart(event: TouchEvent): void {
    if (this.note.isPinned) return;
    event.stopPropagation();
    this.resizeStart.emit({
      noteId: this.note.id,
      mouseX: event.touches[0].clientX,
      mouseY: event.touches[0].clientY,
    });
  }

  onDocumentTouchMove(event: TouchEvent): void {
    if (!this.touchStartPos || this.isEditing) return;
    // Prevenir scroll/zoom inmediatamente al tocar una nota
    event.preventDefault();
    if (this.note.isPinned) return;
    const dx = Math.abs(event.touches[0].clientX - this.touchStartPos.x);
    const dy = Math.abs(event.touches[0].clientY - this.touchStartPos.y);
    if (dx > 10 || dy > 10) {
      if (!this.touchIsDragging) {
        this.touchIsDragging = true;
        // Iniciamos el drag con la posición original del touchstart
        this.dragStart.emit({
          noteId: this.note.id,
          mouseX: this.touchStartPos.x,
          mouseY: this.touchStartPos.y,
        });
      }
    }
  }

  @HostListener('document:touchend')
  onDocumentTouchEnd(): void {
    if (this.touchStartPos) {
      if (!this.touchIsDragging) {
        // Fue un tap sin arrastre → editar la nota
        this.startEdit();
      }
      this.touchStartPos = null;
      this.touchIsDragging = false;
    }
  }
}
