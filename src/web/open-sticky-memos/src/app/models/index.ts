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
}

export interface UpdateNoteRequest {
  title?: string | null;
  content?: string | null;
  color?: string | null;
  positionX?: number | null;
  positionY?: number | null;
  width?: number | null;
  height?: number | null;
  isPinned?: boolean | null;
}

export interface UpdateNotePositionRequest {
  positionX: number;
  positionY: number;
}
