import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ProjectResponse } from '../../models';

@Component({
  selector: 'app-project-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="project-list">
      <div class="list-header">
        <h3>Mis Proyectos</h3>
        <button class="btn-primary" (click)="showCreate = true">
          + Nuevo proyecto
        </button>
      </div>

      <!-- Create dialog inline -->
      @if (showCreate) {
        <div class="dialog-overlay" (click)="showCreate = false">
          <div class="dialog" (click)="$event.stopPropagation()">
            <h4>Crear proyecto</h4>
            <input
              [(ngModel)]="newProjectName"
              placeholder="Nombre del proyecto"
              class="input"
              (keyup.enter)="createProject()"
            />
            <textarea
              [(ngModel)]="newProjectDesc"
              placeholder="Descripción (opcional)"
              class="input textarea"
              rows="3"
            ></textarea>
            <div class="dialog-actions">
              <button class="btn-secondary" (click)="showCreate = false">
                Cancelar
              </button>
              <button
                class="btn-primary"
                [disabled]="!newProjectName.trim() || creating"
                (click)="createProject()"
              >
                {{ creating ? 'Creando...' : 'Crear' }}
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Delete dialog -->
      @if (showDeleteDialog && deleteTarget) {
        <div class="dialog-overlay" (click)="showDeleteDialog = false">
          <div class="dialog" (click)="$event.stopPropagation()">
            <h4>Eliminar proyecto</h4>
            <p class="delete-warning">
              Esta acción no se puede deshacer.
              Escribe <strong>{{ deleteTarget.name }}</strong> para confirmar:
            </p>
            <input
              [(ngModel)]="deleteConfirmName"
              placeholder="Nombre del proyecto"
              class="input"
              (keyup.enter)="confirmDelete()"
            />
            <div class="dialog-actions">
              <button class="btn-secondary" (click)="showDeleteDialog = false">
                Cancelar
              </button>
              <button
                class="btn-danger"
                [disabled]="deleteConfirmName !== deleteTarget.name || deleting"
                (click)="confirmDelete()"
              >
                {{ deleting ? 'Eliminando...' : 'Eliminar' }}
              </button>
            </div>
          </div>
        </div>
      }

      @if (loading) {
        <div class="loading">Cargando proyectos...</div>
      } @else if (projects.length === 0) {
        <div class="empty">
          <p>No tienes proyectos aún</p>
          <p class="hint">Crea uno nuevo para empezar</p>
        </div>
      } @else {
        <div class="projects">
          @for (project of projects; track project.id) {
            <div class="project-card" (click)="openProject(project.id)">
              <div class="card-header">
                <h4>{{ project.name }}</h4>
                <span class="badge">{{ project.noteCount }} notas</span>
              </div>
              @if (project.description) {
                <p class="desc">{{ project.description }}</p>
              }
              <div class="card-footer">
                <span class="owner">{{ project.ownerName }}</span>
                <span class="members">{{ project.memberCount }} miembros</span>
                @if (project.ownerId === userId) {
                  <button
                    class="btn-delete"
                    (click)="$event.stopPropagation(); openDeleteDialog(project)"
                    title="Eliminar proyecto"
                  >🗑️</button>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [
    `
      .project-list {
        padding: 24px;
        max-width: 900px;
        margin: 0 auto;
      }
      .list-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
      }
      .list-header h3 {
        margin: 0;
        font-size: 20px;
        color: #1a1a2e;
      }
      .projects {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
        gap: 16px;
      }
      .project-card {
        background: white;
        border-radius: 10px;
        padding: 20px;
        cursor: pointer;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
        transition: all 0.2s;
        border: 1px solid #eee;
      }
      .project-card:hover {
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.12);
        transform: translateY(-2px);
      }
      .card-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 8px;
      }
      .card-header h4 {
        margin: 0;
        font-size: 16px;
        color: #1a1a2e;
      }
      .badge {
        font-size: 11px;
        background: #f0f0ff;
        color: #667eea;
        padding: 2px 8px;
        border-radius: 12px;
      }
      .desc {
        font-size: 13px;
        color: #888;
        margin: 0 0 12px;
        line-height: 1.4;
      }
      .card-footer {
        display: flex;
        justify-content: space-between;
        font-size: 12px;
        color: #999;
      }
      .loading,
      .empty {
        text-align: center;
        padding: 60px 24px;
        color: #888;
      }
      .hint {
        font-size: 13px;
        color: #bbb;
      }
      .dialog-overlay {
        position: fixed;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        background: rgba(0, 0, 0, 0.4);
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
        max-width: 420px;
        box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
      }
      .dialog h4 {
        margin: 0 0 16px;
        font-size: 18px;
      }
      .input {
        width: 100%;
        padding: 10px 12px;
        border: 1px solid #ddd;
        border-radius: 8px;
        font-size: 14px;
        margin-bottom: 12px;
        box-sizing: border-box;
        font-family: inherit;
      }
      .input:focus {
        outline: none;
        border-color: #667eea;
      }
      .textarea {
        resize: vertical;
      }
      .dialog-actions {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
        margin-top: 8px;
      }
      .btn-primary,
      .btn-secondary {
        padding: 8px 20px;
        border-radius: 8px;
        border: none;
        font-size: 14px;
        cursor: pointer;
        font-weight: 500;
      }
      .btn-primary {
        background: #667eea;
        color: white;
      }
      .btn-primary:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
      .btn-secondary {
        background: #f0f0f0;
        color: #555;
      }
      .btn-secondary:hover {
        background: #e0e0e0;
      }
      .btn-danger {
        padding: 8px 20px;
        border-radius: 8px;
        border: none;
        font-size: 14px;
        cursor: pointer;
        font-weight: 500;
        background: #e74c3c;
        color: white;
      }
      .btn-danger:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
      .btn-delete {
        background: none;
        border: none;
        cursor: pointer;
        padding: 2px 4px;
        font-size: 14px;
        opacity: 0.4;
        transition: opacity 0.15s;
      }
      .btn-delete:hover {
        opacity: 1;
      }
      .delete-warning {
        font-size: 13px;
        color: #888;
        margin: 0 0 12px;
        line-height: 1.5;
      }
    `,
  ],
})
export class ProjectListComponent implements OnInit {
  projects: ProjectResponse[] = [];
  loading = true;
  creating = false;
  showCreate = false;
  newProjectName = '';
  newProjectDesc = '';

  // Delete state
  showDeleteDialog = false;
  deleteTarget: ProjectResponse | null = null;
  deleteConfirmName = '';
  deleting = false;

  userId = '';

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private router: Router
  ) {
    this.userId = this.auth.getStoredUser()?.id ?? '';
  }

  ngOnInit(): void {
    this.loadProjects();
  }

  loadProjects(): void {
    this.loading = true;
    this.api.getProjects().subscribe({
      next: (data) => {
        this.projects = data;
        this.loading = false;
      },
      error: () => (this.loading = false),
    });
  }

  createProject(): void {
    if (!this.newProjectName.trim() || this.creating) return;
    this.creating = true;
    this.api
      .createProject({
        name: this.newProjectName.trim(),
        description: this.newProjectDesc.trim() || null,
      })
      .subscribe({
        next: (project) => {
          this.projects.unshift(project);
          this.showCreate = false;
          this.newProjectName = '';
          this.newProjectDesc = '';
          this.creating = false;
        },
        error: () => (this.creating = false),
      });
  }

  openDeleteDialog(project: ProjectResponse): void {
    this.deleteTarget = project;
    this.deleteConfirmName = '';
    this.showDeleteDialog = true;
  }

  confirmDelete(): void {
    if (!this.deleteTarget || this.deleteConfirmName !== this.deleteTarget.name || this.deleting) return;
    this.deleting = true;
    this.api.deleteProject(this.deleteTarget.id).subscribe({
      next: () => {
        this.projects = this.projects.filter((p) => p.id !== this.deleteTarget!.id);
        this.showDeleteDialog = false;
        this.deleteTarget = null;
        this.deleteConfirmName = '';
        this.deleting = false;
      },
      error: () => (this.deleting = false),
    });
  }

  openProject(id: string): void {
    this.router.navigate(['/board', id]);
  }
}
