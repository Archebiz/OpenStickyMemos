import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { AppConfigService } from './app-config.service';
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
  NoteActionResponse,
  NoteActionRequestResponse,
  CreateInvitationRequest,
  InvitationResponse,
  InvitationPublicResponse,
  NotificationResponse,
} from '../models';

@Injectable()
export class ApiService {
  private get apiUrl() { return this.config.apiUrl; }

  constructor(private http: HttpClient, private config: AppConfigService) {}

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

  getNotes(projectId: string, includeArchived = false): Observable<NoteResponse[]> {
    const params = includeArchived ? '?includeArchived=true' : '';
    return this.http.get<NoteResponse[]>(`${this.apiUrl}/projects/${projectId}/notes${params}`);
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

  deleteNote(projectId: string, noteId: string): Observable<NoteActionResponse | null> {
    // 204 → eliminada (owner). 202 → solicitud creada (miembro).
    return this.http.delete<NoteActionResponse | null>(
      `${this.apiUrl}/projects/${projectId}/notes/${noteId}`,
      { observe: 'response' }
    ).pipe(
      map((resp: HttpResponse<NoteActionResponse | null>) => {
        if (resp.status === 204) {
          return { performed: true, deleted: true, message: 'Nota eliminada' } as NoteActionResponse;
        }
        return resp.body ?? null;
      })
    );
  }

  archiveNote(projectId: string, noteId: string): Observable<NoteActionResponse> {
    return this.http.post<NoteActionResponse>(
      `${this.apiUrl}/projects/${projectId}/notes/${noteId}/archive`,
      {}
    );
  }

  unarchiveNote(projectId: string, noteId: string): Observable<NoteActionResponse> {
    return this.http.post<NoteActionResponse>(
      `${this.apiUrl}/projects/${projectId}/notes/${noteId}/unarchive`,
      {}
    );
  }

  moveNote(sourceProjectId: string, noteId: string, targetProjectId: string): Observable<NoteResponse> {
    return this.http.patch<NoteResponse>(
      `${this.apiUrl}/projects/${sourceProjectId}/notes/${noteId}/move`,
      { targetProjectId }
    );
  }

  getActionRequests(projectId: string): Observable<NoteActionRequestResponse[]> {
    return this.http.get<NoteActionRequestResponse[]>(
      `${this.apiUrl}/projects/${projectId}/notes/requests`
    );
  }

  approveActionRequest(projectId: string, requestId: string): Observable<void> {
    return this.http.post<void>(
      `${this.apiUrl}/projects/${projectId}/notes/requests/${requestId}/approve`,
      {}
    );
  }

  rejectActionRequest(projectId: string, requestId: string): Observable<void> {
    return this.http.post<void>(
      `${this.apiUrl}/projects/${projectId}/notes/requests/${requestId}/reject`,
      {}
    );
  }

  // ── Invitaciones ──

  createInvitation(projectId: string, req: CreateInvitationRequest): Observable<InvitationResponse> {
    return this.http.post<InvitationResponse>(`${this.apiUrl}/projects/${projectId}/invitations`, req);
  }

  getProjectInvitations(projectId: string): Observable<InvitationResponse[]> {
    return this.http.get<InvitationResponse[]>(`${this.apiUrl}/projects/${projectId}/invitations`);
  }

  revokeInvitation(projectId: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/projects/${projectId}/invitations/${invitationId}`);
  }

  /** Público (sin auth): obtiene info de una invitación por token */
  getInvitationPublicInfo(token: string): Observable<InvitationPublicResponse> {
    return this.http.get<InvitationPublicResponse>(`${this.apiUrl}/invitations/${token}`);
  }

  /** Acepta una invitación (requiere auth) */
  acceptInvitation(token: string): Observable<InvitationResponse> {
    return this.http.post<InvitationResponse>(`${this.apiUrl}/invitations/${token}/accept`, {});
  }

  // ── Notificaciones ──

  getNotifications(limit = 30): Observable<NotificationResponse[]> {
    return this.http.get<NotificationResponse[]>(`${this.apiUrl}/notifications?limit=${limit}`);
  }

  getUnreadNotificationCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.apiUrl}/notifications/unread-count`);
  }

  markNotificationAsRead(id: string): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/notifications/${id}/read`, {});
  }

  markAllNotificationsAsRead(): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/notifications/read-all`, {});
  }
}
