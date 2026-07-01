import {
  Component,
  Input,
  Output,
  EventEmitter,
  ViewChild,
  ElementRef,
  AfterViewInit,
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
      [class.editing]="isEditing"
      (mousedown)="onMouseDown($event)"
      (dblclick)="startEdit()"
    >
      <!-- Resize handle -->
      <div class="resize-handle" (mousedown)="onResizeStart($event)"></div>

      <!-- Pin indicator -->
      @if (note.isPinned) {
        <span class="pin">📌</span>
      }

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
          <textarea
            class="edit-content"
            [value]="editContent"
            (input)="editContent = $any($event.target).value"
            placeholder="Escribe tu nota..."
            (keydown.escape)="cancelEdit()"
          ></textarea>
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
              <button class="save-btn" (click)="saveEdit()" title="Guardar">✓</button>
            </div>
          </div>
        </div>
      } @else {
        <div class="view-mode">
          @if (note.title) {
            <h4 class="note-title">{{ note.title }}</h4>
          }
          @if (note.content) {
            <p class="note-content">{{ note.content }}</p>
          } @else if (!note.title) {
            <p class="note-placeholder">Doble click para editar</p>
          }
        </div>
      }
    </div>
  `,
  styles: [
    `
      .note-card {
        position: absolute;
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
      .note-card.editing {
        cursor: default;
        box-shadow: 0 6px 24px rgba(0, 0, 0, 0.25);
        z-index: 10;
      }
      .resize-handle {
        position: absolute;
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
        right: 8px;
        font-size: 14px;
        opacity: 0.7;
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
    `,
  ],
})
export class NoteCardComponent implements AfterViewInit {
  @Input() note!: NoteCardData;
  @Output() noteChange = new EventEmitter<NoteCardData>();
  @Output() deleteNote = new EventEmitter<string>();
  @Output() dragStart = new EventEmitter<{ noteId: string; mouseX: number; mouseY: number }>();
  @Output() resizeStart = new EventEmitter<{ noteId: string; mouseX: number; mouseY: number }>();

  readonly colors = [
    '#FFE066', '#FFB3BA', '#BAFFC9', '#BAE1FF',
    '#E8BAFF', '#FFD9BA', '#BAFFEC', '#FFF3BA',
  ];

  isEditing = false;
  editTitle = '';
  editContent = '';
  editColor = '';
  editPinned = false;

  ngAfterViewInit(): void {}

  startEdit(): void {
    this.editTitle = this.note.title ?? '';
    this.editContent = this.note.content ?? '';
    this.editColor = this.note.color;
    this.editPinned = this.note.isPinned;
    this.isEditing = true;
  }

  cancelEdit(): void {
    this.isEditing = false;
  }

  saveEdit(): void {
    this.isEditing = false;
    this.noteChange.emit({
      ...this.note,
      title: this.editTitle || null,
      content: this.editContent || null,
      color: this.editColor,
      isPinned: this.editPinned,
    });
  }

  onMouseDown(event: MouseEvent): void {
    if (this.isEditing) return;
    this.dragStart.emit({
      noteId: this.note.id,
      mouseX: event.clientX,
      mouseY: event.clientY,
    });
  }

  onResizeStart(event: MouseEvent): void {
    event.stopPropagation();
    this.resizeStart.emit({
      noteId: this.note.id,
      mouseX: event.clientX,
      mouseY: event.clientY,
    });
  }
}
