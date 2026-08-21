using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenStickyMemos.Desktop.Services;
using OpenStickyMemos.Desktop.ViewModels;

namespace OpenStickyMemos.Desktop.Views;

public partial class StickyBoardView : UserControl
{
    private StickyBoardViewModel _vm = null!;
    private Point _dragStart;
    private bool _isDragging;
    private NoteControl? _dragNote;

    // Color picker state (used by individual note 🎨 buttons)
    private readonly string[] _colors = { "#FFE066", "#FFB3BA", "#BAFFC9", "#BAE1FF", "#E8BAFF", "#FFD9BA", "#BAFFEC", "#FFF3BA" };
    private Border? _colorPickerOverlay;

    public StickyBoardView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = (StickyBoardViewModel)DataContext;
        _vm.NoteAdded += AddNoteToCanvas;
        _vm.NoteUpdated += UpdateNoteOnCanvas;
        _vm.NoteRemoved += RemoveNoteFromCanvas;
        _vm.BoardCleared += ClearNotesCanvas;
        _vm.ActionRequestsChanged += UpdateApprovalsBadge;
        _vm.ReminderDue += ShowReminderToast;
        _ = LoadBoardAndRefreshAsync();
    }

    private async Task LoadBoardAndRefreshAsync()
    {
        await _vm.LoadBoardAsync();
        RefreshHeaderButtons();
    }

    private void RefreshHeaderButtons()
    {
        if (_vm is null) return;
        RenameProjectButton.Visibility = _vm.IsOwner ? Visibility.Visible : Visibility.Collapsed;
        ApprovalsButton.Visibility = _vm.IsOwner ? Visibility.Visible : Visibility.Collapsed;
        UpdateApprovalsBadge();
    }

    private void UpdateApprovalsBadge()
    {
        if (_vm is null || ApprovalsButton is null) return;
        var pending = _vm.ActionRequests.Count(r => r.Status == "Pending");
        ApprovalsButton.Content = $"⏳ {pending}";
        ApprovalsButton.ToolTip = pending == 0
            ? "Sin solicitudes pendientes"
            : $"{pending} solicitud(es) pendiente(s) de aprobación";
    }

    private void ClearNotesCanvas()
    {
        Dispatcher.Invoke(() =>
        {
            NotesCanvas.Children.Clear();
            _maxZIndex = 0;
        });
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoBack();
    }

    private async void AddNote_Click(object sender, RoutedEventArgs e)
    {
        var random = new Random();
        var note = await _vm.CreateNoteAsync(
            positionX: random.Next(50, 300),
            positionY: random.Next(50, 300),
            color: "#FFE066");
    }

    private void AddNoteToCanvas(NoteItem note)
    {
        Dispatcher.Invoke(() =>
        {
            // Safety: eliminar cualquier NoteControl existente con el mismo Id
            // para prevenir duplicados por race conditions (SignalR vs HTTP)
            for (int i = NotesCanvas.Children.Count - 1; i >= 0; i--)
            {
                if (NotesCanvas.Children[i] is NoteControl existing && existing.NoteId == note.Id)
                {
                    NotesCanvas.Children.RemoveAt(i);
                }
            }

            var ctrl = CreateNoteControl(note);
            Canvas.SetLeft(ctrl, note.PositionX);
            Canvas.SetTop(ctrl, note.PositionY);
            Panel.SetZIndex(ctrl, note.ZIndex);
            NotesCanvas.Children.Add(ctrl);
        });
    }

    private void UpdateNoteOnCanvas(NoteItem note)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var child in NotesCanvas.Children)
            {
                if (child is NoteControl nc && nc.NoteId == note.Id)
                {
                    nc.Title = note.Title ?? string.Empty;
                    nc.NoteContent = note.Content ?? string.Empty;
                    nc.NoteColor = note.Color;
                    nc.IsPinned = note.IsPinned;
                    nc.IsArchived = note.IsArchived;
                    nc.PendingArchive = note.PendingArchive;
                    nc.PendingDelete = note.PendingDelete;
                    nc.IsTaskList = note.IsTaskList;
                    nc.Tags = note.Tags;
                    nc.ReminderAt = note.ReminderAt;
                    Panel.SetZIndex(nc, note.ZIndex);

                    // No sobreescribir posición/tamaño si estamos arrastrando/redimensionando localmente
                    if (!_localChanges.Contains(note.Id))
                    {
                        Canvas.SetLeft(nc, note.PositionX);
                        Canvas.SetTop(nc, note.PositionY);
                        nc.Width = note.Width;
                        nc.Height = note.Height;
                    }
                    break;
                }
            }
        });
    }

    private void RemoveNoteFromCanvas(string noteId)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var child in NotesCanvas.Children)
            {
                if (child is NoteControl nc && nc.NoteId == noteId)
                {
                    NotesCanvas.Children.Remove(nc);
                    break;
                }
            }
        });
    }

    private NoteControl CreateNoteControl(NoteItem note)
    {
        var ctrl = new NoteControl
        {
            NoteId = note.Id,
            Title = note.Title ?? string.Empty,
            NoteContent = note.Content ?? string.Empty,
            NoteColor = note.Color,
            IsPinned = note.IsPinned,
            IsArchived = note.IsArchived,
            PendingArchive = note.PendingArchive,
            PendingDelete = note.PendingDelete,
            IsTaskList = note.IsTaskList,
            Tags = note.Tags,
            ReminderAt = note.ReminderAt,
            AuthorName = note.AuthorName ?? string.Empty,
            Width = note.Width,
            Height = note.Height,
        };

        ctrl.NoteMouseDown += OnNoteMouseDown;
        ctrl.ContentChanged += OnNoteContentChanged;
        ctrl.DeleteClicked += OnNoteDeleteClicked;
        ctrl.ArchiveClicked += OnNoteArchiveClicked;
        ctrl.TaskModeToggled += OnNoteTaskModeToggled;
        ctrl.MetaChanged += OnNoteMetaChanged;
        ctrl.PinToggled += OnNotePinToggled;
        ctrl.ColorClicked += OnNoteColorClicked;
        ctrl.BringToFrontClicked += OnBringToFront;
        ctrl.SendToBackClicked += OnSendToBack;
        ctrl.ResizeCompleted += OnResizeCompleted;
        ctrl.ResizeStarted += OnResizeStarted;

        // Mouse move/up on canvas handles dragging
        return ctrl;
    }

    private int _maxZIndex;
    private NoteControl? _selectedNote;
    private readonly HashSet<string> _localChanges = new();

    private void OnResizeStarted(string noteId)
    {
        _localChanges.Add(noteId);
    }

    private void OnNoteMouseDown(NoteControl note, MouseEventArgs e)
    {
        _localChanges.Add(note.NoteId);
        _dragNote = note;
        _selectedNote = note;
        _dragStart = e.GetPosition(NotesCanvas);
        _isDragging = true;
        note.CaptureMouse();

        // Mostrar sidebar para mover la nota a otro proyecto
        ShowMoveSidebar();

        // Bring to front
        _maxZIndex++;
        Panel.SetZIndex(note, _maxZIndex);
        _vm.UpdateNoteZIndex(note.NoteId, _maxZIndex);
    }

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _selectedNote = null;
        // Click on canvas clears selection
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging && _dragNote is not null)
        {
            var pos = e.GetPosition(NotesCanvas);
            var dx = pos.X - _dragStart.X;
            var dy = pos.Y - _dragStart.Y;

            Canvas.SetLeft(_dragNote, Canvas.GetLeft(_dragNote) + dx);
            Canvas.SetTop(_dragNote, Canvas.GetTop(_dragNote) + dy);

            _dragStart = pos;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_isDragging && _dragNote is not null)
        {
            _isDragging = false;
            _dragNote.ReleaseMouseCapture();

            var noteId = _dragNote.NoteId;
            var left = Canvas.GetLeft(_dragNote);
            var top = Canvas.GetTop(_dragNote);

            // ¿Soltó sobre un proyecto del sidebar para mover la nota?
            var targetProjectId = GetProjectUnderPointer(e.GetPosition(MoveSidebarOverlay));
            HideMoveSidebar();

            if (!string.IsNullOrEmpty(targetProjectId))
            {
                _ = _vm.MoveNoteToProjectAsync(noteId, targetProjectId);
            }
            else
            {
                // Notify VM of new position
                _vm.UpdateNotePosition(noteId, left, top);
            }

            _localChanges.Remove(noteId);
            _dragNote = null;
        }
    }

    private void OnNoteContentChanged(string noteId, string title, string content)
    {
        _vm.UpdateNoteContent(noteId, title, content);
    }

    private void OnNoteTaskModeToggled(string noteId, bool isTaskList)
    {
        var ctrl = NotesCanvas.Children.OfType<NoteControl>().FirstOrDefault(c => c.NoteId == noteId);
        if (ctrl is null) return;
        _vm.UpdateNoteMetaAsync(noteId, ctrl.Tags, ctrl.ReminderAt, isTaskList);
    }

    private void OnNoteMetaChanged(string noteId, string? tags, DateTime? reminderAt)
    {
        _vm.UpdateNoteMetaAsync(noteId, tags, reminderAt, isTaskList: null);
    }

    private async void OnNoteDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not NoteControl nc) return;

        if (_vm.IsOwner)
        {
            var confirm = MessageBox.Show(
                "¿Estás seguro de eliminar esta nota? Esta acción no se puede deshacer.",
                "Eliminar nota",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
        }
        else
        {
            var confirm = MessageBox.Show(
                "Como miembro, eliminar una nota requiere la aprobación del propietario.\nSe enviará una solicitud al owner. ¿Continuar?",
                "Eliminar nota",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        await _vm.DeleteNoteAsync(nc.NoteId);
    }

    private async void OnNoteArchiveClicked(string noteId)
    {
        var ctrl = NotesCanvas.Children.OfType<NoteControl>().FirstOrDefault(c => c.NoteId == noteId);
        if (ctrl is null) return;

        if (ctrl.IsArchived)
        {
            // Restaurar directamente (todos los miembros pueden restaurar)
            await _vm.UnarchiveNoteAsync(noteId);
            return;
        }

        if (!_vm.IsOwner)
        {
            var confirm = MessageBox.Show(
                "Como miembro, archivar una nota requiere la aprobación del propietario.\nSe enviará una solicitud al owner. ¿Continuar?",
                "Archivar nota",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        await _vm.ArchiveNoteAsync(noteId);
    }

    private void OnNotePinToggled(string noteId, bool isPinned)
    {
        _vm.UpdateNotePin(noteId, isPinned);
    }

    private void OnNoteColorClicked(string noteId)
    {
        if (_colorPickerOverlay is not null)
            HideColorPicker();

        _pendingColorNoteId = noteId;
        ShowColorPicker();
    }

    private string? _pendingColorNoteId;

    private void OnBringToFront(string noteId)
    {
        _maxZIndex++;
        var ctrl = NotesCanvas.Children.OfType<NoteControl>().FirstOrDefault(c => c.NoteId == noteId);
        if (ctrl is not null) Panel.SetZIndex(ctrl, _maxZIndex);
        _vm.UpdateNoteZIndex(noteId, _maxZIndex);
    }

    private void OnSendToBack(string noteId)
    {
        var ctrl = NotesCanvas.Children.OfType<NoteControl>().FirstOrDefault(c => c.NoteId == noteId);
        if (ctrl is not null) Panel.SetZIndex(ctrl, 0);
        _vm.UpdateNoteZIndex(noteId, 0);
    }

    private void OnResizeCompleted(string noteId, double width, double height)
    {
        _localChanges.Remove(noteId);
        _vm.UpdateNoteSize(noteId, width, height);
    }

    private void ShowColorPicker()
    {
        var palette = new StackPanel();
        palette.Children.Add(CreateColorPalette());

        var cancelBtn = new Button
        {
            Content = "✕ Cancelar",
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(14, 5, 14, 5),
            FontSize = 12,
            Cursor = Cursors.Hand,
            Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
        };
        cancelBtn.Click += (_, _) => HideColorPicker();
        palette.Children.Add(cancelBtn);

        var inner = new Border
        {
            Width = 300,
            Background = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = palette,
        };

        _colorPickerOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            Child = inner
        };
        _colorPickerOverlay.MouseLeftButtonDown += (_, _) => HideColorPicker();
        Grid.SetRow(_colorPickerOverlay, 1);
        ((Grid)Content).Children.Add(_colorPickerOverlay);
    }

    private StackPanel CreateColorPalette()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var color in _colors)
        {
            var btn = new Button
            {
                Width = 32, Height = 32,
                Margin = new Thickness(4),
                Background = (Brush)new BrushConverter().ConvertFrom(color)!,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Cursor = Cursors.Hand,
                Tag = color
            };
            btn.Click += ColorButton_Click;
            panel.Children.Add(btn);
        }
        return panel;
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string color && _pendingColorNoteId is not null)
        {
            _vm.UpdateNoteColor(_pendingColorNoteId, color);
            HideColorPicker();
        }
    }

    private void HideColorPicker()
    {
        if (_colorPickerOverlay is not null && ((Grid)Content).Children.Contains(_colorPickerOverlay))
        {
            ((Grid)Content).Children.Remove(_colorPickerOverlay);
        }
    }

    // ── Invitation Dialog ──

    private List<InvitationResponse> _activeInvitations = new();

    private void InviteButton_Click(object sender, RoutedEventArgs e)
    {
        InvitationOverlay.Visibility = Visibility.Visible;
        ShowEmailTab_Click(sender, e);
        _ = LoadInvitationsAsync();
    }

    private void ShowEmailTab_Click(object sender, RoutedEventArgs e)
    {
        EmailTabPanel.Visibility = Visibility.Visible;
        LinkTabPanel.Visibility = Visibility.Collapsed;
        EmailTabBtn.Background = (System.Windows.Media.Brush)FindResource("BrandBlueBrush");
        EmailTabBtn.Foreground = System.Windows.Media.Brushes.White;
        LinkTabBtn.Background = System.Windows.Media.Brushes.Transparent;
        LinkTabBtn.Foreground = System.Windows.Media.Brushes.Black;
    }

    private void ShowLinkTab_Click(object sender, RoutedEventArgs e)
    {
        EmailTabPanel.Visibility = Visibility.Collapsed;
        LinkTabPanel.Visibility = Visibility.Visible;
        LinkTabBtn.Background = (System.Windows.Media.Brush)FindResource("BrandBlueBrush");
        LinkTabBtn.Foreground = System.Windows.Media.Brushes.White;
        EmailTabBtn.Background = System.Windows.Media.Brushes.Transparent;
        EmailTabBtn.Foreground = System.Windows.Media.Brushes.Black;
    }

    private async void InviteByEmail_Click(object sender, RoutedEventArgs e)
    {
        var email = InviteEmailInput.Text.Trim();
        if (string.IsNullOrEmpty(email))
        {
            InviteEmailMessage.Text = "Ingresá un email válido.";
            InviteEmailMessage.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        var result = await _vm.AddMemberAsync(email);
        if (result is not null)
        {
            InviteEmailMessage.Text = $"✅ {email} agregado al proyecto";
            InviteEmailMessage.Foreground = (System.Windows.Media.Brush)FindResource("BrandBlueBrush");
            InviteEmailInput.Text = "";
        }
        else
        {
            InviteEmailMessage.Text = "❌ No se pudo agregar. ¿El usuario existe?";
            InviteEmailMessage.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private void RestrictEmail_Checked(object sender, RoutedEventArgs e)
    {
        RestrictEmailPanel.Visibility = Visibility.Visible;
    }

    private void RestrictEmail_Unchecked(object sender, RoutedEventArgs e)
    {
        RestrictEmailPanel.Visibility = Visibility.Collapsed;
    }

    private async void GenerateInvitationLink_Click(object sender, RoutedEventArgs e)
    {
        GenerateLinkBtn.IsEnabled = false;
        GenerateLinkBtn.Content = "Generando...";
        InvitationLinkError.Text = "";

        var expiresDays = 7;
        if (ExpiresDaysCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var days))
            expiresDays = days;

        object request;
        if (RestrictEmailCheck.IsChecked == true && !string.IsNullOrWhiteSpace(InviteLinkEmailInput.Text))
            request = new { expiresInDays = expiresDays, invitedEmail = InviteLinkEmailInput.Text.Trim() };
        else
            request = new { expiresInDays = expiresDays };

        var result = await _vm.CreateInvitationAsync(request);
        if (result is not null)
        {
            _activeInvitations.Insert(0, result);
            RefreshActiveLinksList();
        }
        else
        {
            InvitationLinkError.Text = "Error al generar el link de invitación.";
        }

        GenerateLinkBtn.IsEnabled = true;
        GenerateLinkBtn.Content = "🔗 Generar link";
    }

    private async Task LoadInvitationsAsync()
    {
        var invitations = await _vm.GetProjectInvitationsAsync();
        _activeInvitations = invitations;
        RefreshActiveLinksList();
    }

    private void RefreshActiveLinksList()
    {
        if (_activeInvitations.Count > 0)
        {
            ActiveLinksPanel.Visibility = Visibility.Visible;
            ActiveLinksTitle.Text = $"Links activos ({_activeInvitations.Count})";
            ActiveLinksList.ItemsSource = null;
            ActiveLinksList.ItemsSource = _activeInvitations;
        }
        else
        {
            ActiveLinksPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void CopyLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.DataContext is InvitationResponse inv)
        {
            System.Windows.Clipboard.SetText(inv.InvitationLink);
        }
    }

    private void CloseInvitationDialog_Click(object sender, RoutedEventArgs e)
    {
        InvitationOverlay.Visibility = Visibility.Collapsed;
    }

    // ── Members Tab ──

    private string? _projectOwnerId;
    private List<MemberInfo> _projectMembers = new();

    private void ShowMembersTab_Click(object sender, RoutedEventArgs e)
    {
        EmailTabPanel.Visibility = Visibility.Collapsed;
        LinkTabPanel.Visibility = Visibility.Collapsed;
        MembersTabPanel.Visibility = Visibility.Visible;
        MembersTabBtn.Background = (System.Windows.Media.Brush)FindResource("BrandBlueBrush");
        MembersTabBtn.Foreground = System.Windows.Media.Brushes.White;
        EmailTabBtn.Background = System.Windows.Media.Brushes.Transparent;
        EmailTabBtn.Foreground = System.Windows.Media.Brushes.Black;
        LinkTabBtn.Background = System.Windows.Media.Brushes.Transparent;
        LinkTabBtn.Foreground = System.Windows.Media.Brushes.Black;

        _ = LoadMembersAsync();
    }

    private async Task LoadMembersAsync()
    {
        var project = await _vm.GetProjectAsync();
        if (project is null) return;

        _projectOwnerId = project.OwnerId;
        _projectMembers = project.Members;

        MembersTitle.Text = $"Miembros ({project.Members.Count})";
        MembersList.ItemsSource = null;
        MembersList.ItemsSource = project.Members;
    }

    private async void RemoveMember_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.DataContext is MemberInfo member)
        {
            // No permitir remover al owner
            if (member.UserId == _projectOwnerId)
            {
                MessageBox.Show("No puedes remover al propietario del proyecto.", "Acción no permitida",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"¿Estás seguro de eliminar a {member.DisplayName} del proyecto?",
                "Remover miembro",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var removed = await _vm.RemoveMemberAsync(member.UserId);
                if (removed)
                {
                    _projectMembers.Remove(member);
                    MembersTitle.Text = $"Miembros ({_projectMembers.Count})";
                    MembersList.ItemsSource = null;
                    MembersList.ItemsSource = _projectMembers;
                }
            }
        }
    }

    // ── Revoke Invitation Link ──

    private async void RevokeInvitation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.DataContext is InvitationResponse inv)
        {
            var result = MessageBox.Show(
                "¿Estás seguro de revocar este link de invitación?",
                "Revocar invitación",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _activeInvitations.Remove(inv);
                RefreshActiveLinksList();
                await _vm.RevokeInvitationAsync(inv.Id);
            }
        }
    }

    // ── Mover nota a otro proyecto (drag & drop al sidebar) ──

    private void ShowMoveSidebar()
    {
        if (_vm is null || _vm.Projects.Count <= 1 || _dragNote is null)
            return;

        MoveProjectPanel.Children.Clear();
        var currentProjectId = _vm.ProjectId;
        foreach (var p in _vm.Projects)
        {
            if (p.Id == currentProjectId) continue;

            var btn = new Button
            {
                Content = $"📋 {p.Name}",
                Tag = p.Id,
                Margin = new Thickness(12, 4, 12, 4),
                Padding = new Thickness(12, 9, 12, 9),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                FontSize = 12,
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x42, 0x55))
            };
            btn.Click += MoveProjectButton_Click;
            MoveProjectPanel.Children.Add(btn);
        }

        MoveSidebarOverlay.Visibility = Visibility.Visible;
    }

    private void HideMoveSidebar()
    {
        MoveSidebarOverlay.Visibility = Visibility.Collapsed;
    }

    private string? GetProjectUnderPointer(Point posInSidebar)
    {
        if (MoveSidebarOverlay.Visibility != Visibility.Visible)
            return null;

        foreach (var child in MoveProjectPanel.Children)
        {
            if (child is Button btn)
            {
                var btnPos = btn.TranslatePoint(new Point(0, 0), MoveSidebarOverlay);
                if (new Rect(btnPos, btn.RenderSize).Contains(posInSidebar))
                    return btn.Tag as string;
            }
        }
        return null;
    }

    private async void MoveProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string projectId && _dragNote is not null)
        {
            var noteId = _dragNote.NoteId;
            _isDragging = false;
            _dragNote.ReleaseMouseCapture();
            HideMoveSidebar();
            await _vm.MoveNoteToProjectAsync(noteId, projectId);
            _dragNote = null;
        }
    }

    // ── Archivo (ver notas archivadas) ──

    private async void ArchiveFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.ShowArchived = !_vm.ShowArchived;
        ArchiveFilterButton.Content = _vm.ShowArchived ? "📂" : "🗄️";
        ArchiveFilterButton.ToolTip = _vm.ShowArchived ? "Mostrar notas activas" : "Ver notas archivadas";
        await _vm.LoadNotesAsync();
    }

    // ── Renombrar proyecto ──

    private void RenameProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        RenameProjectInput.Text = _vm.ProjectName;
        RenameOverlay.Visibility = Visibility.Visible;
        RenameProjectInput.Focus();
        RenameProjectInput.SelectAll();
    }

    private async void SaveRename_Click(object sender, RoutedEventArgs e)
    {
        var ok = await _vm.RenameProjectAsync(RenameProjectInput.Text);
        if (ok)
            RenameOverlay.Visibility = Visibility.Collapsed;
        else
            MessageBox.Show("No se pudo renombrar el proyecto.", "Renombrar proyecto",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void RenameProjectInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveRename_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseRename_Click(sender, e);
            e.Handled = true;
        }
    }

    private void CloseRename_Click(object sender, RoutedEventArgs e)
    {
        RenameOverlay.Visibility = Visibility.Collapsed;
    }

    // ── Solicitudes de aprobación (owner) ──

    private async void ApprovalsButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.LoadActionRequestsAsync();
        ApprovalsList.ItemsSource = null;
        ApprovalsList.ItemsSource = _vm.ActionRequests;
        var pendingCount = _vm.ActionRequests.Count(r => r.Status == "Pending");
        ApprovalsTitle.Text = $"Solicitudes pendientes ({pendingCount})";
        ApprovalsOverlay.Visibility = Visibility.Visible;
    }

    private async void ApproveRequest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string requestId)
        {
            await _vm.ApproveActionRequestAsync(requestId);
            await ReloadApprovalsAsync();
        }
    }

    private async void RejectRequest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string requestId)
        {
            await _vm.RejectActionRequestAsync(requestId);
            await ReloadApprovalsAsync();
        }
    }

    private async Task ReloadApprovalsAsync()
    {
        await _vm.LoadActionRequestsAsync();
        ApprovalsList.ItemsSource = null;
        ApprovalsList.ItemsSource = _vm.ActionRequests;
        var pendingCount = _vm.ActionRequests.Count(r => r.Status == "Pending");
        ApprovalsTitle.Text = $"Solicitudes pendientes ({pendingCount})";
    }

    private void CloseApprovals_Click(object sender, RoutedEventArgs e)
    {
        ApprovalsOverlay.Visibility = Visibility.Collapsed;
    }

    // ── Notificación de recordatorio ──

    private void ShowReminderToast(NoteItem note)
    {
        Dispatcher.Invoke(() =>
        {
            var toast = new Window
            {
                Title = "Recordatorio",
                Width = 320,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = SystemParameters.WorkArea.Right - 350,
                Top = SystemParameters.WorkArea.Bottom - 160,
                Background = (Brush)FindResource("BgGradientWarm"),
                Content = new Border
                {
                    Padding = new Thickness(16),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "⏰ Recordatorio",
                                FontSize = 13,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = (Brush)FindResource("BrandBlueBrush")
                            },
                            new TextBlock
                            {
                                Text = string.IsNullOrWhiteSpace(note.Title) ? "(Sin título)" : note.Title,
                                FontSize = 14,
                                FontWeight = FontWeights.SemiBold,
                                Margin = new Thickness(0, 6, 0, 0),
                                TextWrapping = TextWrapping.Wrap
                            },
                            new TextBlock
                            {
                                Text = StripHtml(note.Content ?? string.Empty),
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                                Margin = new Thickness(0, 4, 0, 0),
                                TextWrapping = TextWrapping.Wrap,
                                MaxHeight = 60
                            }
                        }
                    }
                }
            };

            toast.Show();

            var closeTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(8)
            };
            closeTimer.Tick += (_, _) =>
            {
                closeTimer.Stop();
                toast.Close();
            };
            closeTimer.Start();
        });
    }

    private static string StripHtml(string html)
    {
        var decoded = System.Net.WebUtility.HtmlDecode(html);
        return System.Text.RegularExpressions.Regex.Replace(decoded, "<[^>]+>", string.Empty);
    }
}
