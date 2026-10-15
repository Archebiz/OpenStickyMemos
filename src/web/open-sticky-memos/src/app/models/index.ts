// ── Proyectos ──

export interface ProjectResponse {
  id: string;
  name: string;
  description: string | null;
  ownerId: string;
  ownerName: string;
  ownerAvatar: string | null;
  createdAt: string;
  updatedAt: string;
  memberCount: number;
  noteCount: number;
  members: MemberInfo[];
}

export interface CreateProjectRequest {
  name: string;
  description?: string | null;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string | null;
}

export interface AddMemberRequest {
  email: string;
}

export interface MemberInfo {
  id: string;
  userId: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  role: string;
  joinedAt: string;
}

// ── Notas ──

export interface NoteResponse {
  id: string;
  projectId: string;
  authorId: string;
  authorName: string;
  authorAvatar: string | null;
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
  createdAt: string;
  updatedAt: string;
}

export interface CreateNoteRequest {
  title?: string | null;
  content?: string | null;
  color?: string;
  positionX?: number;
  positionY?: number;
  width?: number;
  height?: number;
  isPinned?: boolean;
  zIndex?: number;
  isTaskList?: boolean;
  tags?: string | null;
  reminderAt?: string | null;
}

export interface UpdateNoteRequest {
  title?: string | null;
  content?: string | null;
  color?: string | null;
  positionX?: number | null;
  positionY?: number | null;
  width?: number | null;
  height?: number | null;
  zIndex?: number | null;
  isPinned?: boolean | null;
  isTaskList?: boolean | null;
  tags?: string | null;
  reminderAt?: string | null;
  clearReminder?: boolean;
}

export interface UpdateNotePositionRequest {
  positionX: number;
  positionY: number;
}

// ── Notas: acciones (archivar / eliminar / mover / aprobaciones) ──

export interface MoveNoteRequest {
  targetProjectId: string;
}

export interface NoteActionResponse {
  note?: NoteResponse | null;
  performed: boolean;
  requestCreated: boolean;
  deleted: boolean;
  requestId?: string | null;
  message?: string;
}

export interface NoteActionRequestResponse {
  id: string;
  noteId: string;
  projectId: string;
  type: 'Archive' | 'Delete';
  status: 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
  requesterId: string;
  requesterName: string;
  requesterAvatar: string | null;
  noteTitle: string;
  noteColor: string;
  createdAt: string;
  decidedAt: string | null;
  decidedById: string | null;
}

// ── Invitaciones ──

export interface CreateInvitationRequest {
  invitedEmail?: string | null;
  expiresInDays?: number;
}

export interface InvitationResponse {
  id: string;
  projectId: string;
  projectName: string;
  invitedEmail: string | null;
  token: string;
  invitationLink: string;
  createdById: string;
  createdByName: string;
  createdAt: string;
  expiresAt: string;
  isAccepted: boolean;
  acceptedByUserId: string | null;
  acceptedAt: string | null;
}

export interface InvitationPublicResponse {
  projectId: string;
  projectName: string;
  projectDescription: string | null;
  invitedEmail: string | null;
  createdByName: string;
  expiresAt: string;
  isExpired: boolean;
  isAccepted: boolean;
}

// ── Notificaciones ──

export interface NotificationResponse {
  id: string;
  userId: string;
  authorId: string;
  authorName: string;
  authorAvatar?: string | null;
  projectId: string;
  projectName: string;
  noteId?: string | null;
  noteTitle?: string | null;
  type: string;
  message: string;
  isRead: boolean;
  createdAt: string;
}

