import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ProjectResponse,
  CreateProjectRequest,
  UpdateProjectRequest,
  AddMemberRequest,
  MemberInfo,
  NoteResponse,
  CreateNoteRequest,
  UpdateNoteRequest,
  UpdateNotePositionRequest,
} from '../models';

@Injectable()
export class ApiService {
  private readonly apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // ── Proyectos ──

  getProjects(): Observable<ProjectResponse[]> {
    return this.http.get<ProjectResponse[]>(`${this.apiUrl}/projects`);
  }

  getProject(id: string): Observable<ProjectResponse> {
    return this.http.get<ProjectResponse>(`${this.apiUrl}/projects/${id}`);
  }

  createProject(req: CreateProjectRequest): Observable<ProjectResponse> {
    return this.http.post<ProjectResponse>(`${this.apiUrl}/projects`, req);
  }

  updateProject(id: string, req: UpdateProjectRequest): Observable<ProjectResponse> {
    return this.http.put<ProjectResponse>(`${this.apiUrl}/projects/${id}`, req);
  }

  deleteProject(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/projects/${id}`);
  }

  addMember(projectId: string, req: AddMemberRequest): Observable<MemberInfo> {
    return this.http.post<MemberInfo>(`${this.apiUrl}/projects/${projectId}/members`, req);
  }

  removeMember(projectId: string, memberId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/projects/${projectId}/members/${memberId}`);
  }

  // ── Notas ──

  getNotes(projectId: string): Observable<NoteResponse[]> {
    return this.http.get<NoteResponse[]>(`${this.apiUrl}/projects/${projectId}/notes`);
  }

  getNote(projectId: string, noteId: string): Observable<NoteResponse> {
    return this.http.get<NoteResponse>(`${this.apiUrl}/projects/${projectId}/notes/${noteId}`);
  }

  createNote(projectId: string, req: CreateNoteRequest): Observable<NoteResponse> {
    return this.http.post<NoteResponse>(`${this.apiUrl}/projects/${projectId}/notes`, req);
  }

  updateNote(projectId: string, noteId: string, req: UpdateNoteRequest): Observable<NoteResponse> {
    return this.http.put<NoteResponse>(`${this.apiUrl}/projects/${projectId}/notes/${noteId}`, req);
  }

  updateNotePosition(
    projectId: string,
    noteId: string,
    req: UpdateNotePositionRequest
  ): Observable<NoteResponse> {
    return this.http.patch<NoteResponse>(
      `${this.apiUrl}/projects/${projectId}/notes/${noteId}/position`,
      req
    );
  }

  deleteNote(projectId: string, noteId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/projects/${projectId}/notes/${noteId}`);
  }
}
