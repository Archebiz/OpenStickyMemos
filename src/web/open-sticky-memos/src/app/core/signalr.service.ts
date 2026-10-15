import { Injectable, NgZone, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import { AuthService } from './auth.service';
import { AppConfigService } from './app-config.service';
import { NotificationResponse } from '../models';

export interface NoteEvent {
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

export interface NoteActionRequestEvent {
  id: string;
  noteId: string;
  projectId: string;
  type: string;
  status: string;
  requesterId: string;
  requesterName: string;
  requesterAvatar: string | null;
  noteTitle: string;
  noteColor: string;
  createdAt: string;
  decidedAt: string | null;
  decidedById: string | null;
}

export interface NoteDeletedEvent {
  noteId: string;
  projectId: string;
}

export interface MemberEvent {
  id: string;
  userId: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  role: string;
  joinedAt: string;
}

@Injectable({ providedIn: 'root' })
export class SignalRService implements OnDestroy {
  private hubConnection?: signalR.HubConnection;
  private isConnected = false;

  private noteCreatedSubject = new Subject<NoteEvent>();
  private noteUpdatedSubject = new Subject<NoteEvent>();
  private noteDeletedSubject = new Subject<NoteDeletedEvent>();
  private memberAddedSubject = new Subject<MemberEvent>();
  private actionRequestCreatedSubject = new Subject<NoteActionRequestEvent>();
  private actionRequestResolvedSubject = new Subject<NoteActionRequestEvent>();
  private notificationReceivedSubject = new Subject<NotificationResponse>();
  private connectionStateSubject = new BehaviorSubject<boolean>(false);

  noteCreated$: Observable<NoteEvent> = this.noteCreatedSubject.asObservable();
  noteUpdated$: Observable<NoteEvent> = this.noteUpdatedSubject.asObservable();
  noteDeleted$: Observable<NoteDeletedEvent> = this.noteDeletedSubject.asObservable();
  memberAdded$: Observable<MemberEvent> = this.memberAddedSubject.asObservable();
  actionRequestCreated$: Observable<NoteActionRequestEvent> = this.actionRequestCreatedSubject.asObservable();
  actionRequestResolved$: Observable<NoteActionRequestEvent> = this.actionRequestResolvedSubject.asObservable();
  notificationReceived$: Observable<NotificationResponse> = this.notificationReceivedSubject.asObservable();
  connectionState$: Observable<boolean> = this.connectionStateSubject.asObservable();

  constructor(
    private authService: AuthService,
    private config: AppConfigService,
    private router: Router,
    private ngZone: NgZone
  ) {}

  /** Inicia la conexión SignalR */
  async start(): Promise<void> {
    if (this.isConnected) return;

    const token = this.authService.getAccessToken();
    if (!token) return;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.config.signalrUrl, {
        accessTokenFactory: () => this.authService.getAccessToken() ?? '',
        withCredentials: true,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    this.registerEvents();

    try {
      await this.hubConnection.start();
      this.isConnected = true;
      this.connectionStateSubject.next(true);
    } catch (err) {
      console.error('SignalR connection failed:', err);
      this.connectionStateSubject.next(false);
    }

    this.hubConnection.onreconnecting(() => {
      this.connectionStateSubject.next(false);
    });

    this.hubConnection.onreconnected(() => {
      this.connectionStateSubject.next(true);
    });

    this.hubConnection.onclose(() => {
      this.isConnected = false;
      this.connectionStateSubject.next(false);
    });
  }

  /** Se une al grupo de un proyecto */
  async joinProject(projectId: string): Promise<void> {
    try {
      await this.hubConnection?.invoke('JoinProject', projectId);
    } catch (err) {
      console.error('Error joining project group:', err);
    }
  }

  /** Abandona el grupo de un proyecto */
  async leaveProject(projectId: string): Promise<void> {
    try {
      await this.hubConnection?.invoke('LeaveProject', projectId);
    } catch (err) {
      console.error('Error leaving project group:', err);
    }
  }

  /** Detiene la conexión */
  async stop(): Promise<void> {
    this.isConnected = false;
    this.connectionStateSubject.next(false);
    await this.hubConnection?.stop();
  }

  ngOnDestroy(): void {
    this.stop();
    this.noteCreatedSubject.complete();
    this.noteUpdatedSubject.complete();
    this.noteDeletedSubject.complete();
    this.memberAddedSubject.complete();
    this.actionRequestCreatedSubject.complete();
    this.actionRequestResolvedSubject.complete();
    this.notificationReceivedSubject.complete();
    this.connectionStateSubject.complete();
  }

  // ── Privado ──

  private registerEvents(): void {
    this.hubConnection?.on('NoteCreated', (note: NoteEvent) => {
      this.ngZone.run(() => this.noteCreatedSubject.next(note));
    });

    this.hubConnection?.on('NoteUpdated', (note: NoteEvent) => {
      this.ngZone.run(() => this.noteUpdatedSubject.next(note));
    });

    this.hubConnection?.on('NoteDeleted', (data: NoteDeletedEvent) => {
      this.ngZone.run(() => this.noteDeletedSubject.next(data));
    });

    this.hubConnection?.on('ActionRequestCreated', (request: NoteActionRequestEvent) => {
      this.ngZone.run(() => this.actionRequestCreatedSubject.next(request));
    });

    this.hubConnection?.on('ActionRequestResolved', (request: NoteActionRequestEvent) => {
      this.ngZone.run(() => this.actionRequestResolvedSubject.next(request));
    });

    this.hubConnection?.on('MemberAdded', (member: MemberEvent) => {
      this.ngZone.run(() => this.memberAddedSubject.next(member));
    });

    this.hubConnection?.on('NotificationReceived', (notification: NotificationResponse) => {
      this.ngZone.run(() => this.notificationReceivedSubject.next(notification));
    });
  }
}
