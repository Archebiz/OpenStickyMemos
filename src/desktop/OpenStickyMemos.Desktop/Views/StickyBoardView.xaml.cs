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
        _ = _vm.LoadBoardAsync();
        _vm.NoteAdded += AddNoteToCanvas;
        _vm.NoteUpdated += UpdateNoteOnCanvas;
        _vm.NoteRemoved += RemoveNoteFromCanvas;
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
            AuthorName = note.AuthorName ?? string.Empty,
            Width = note.Width,
            Height = note.Height,
        };

        ctrl.NoteMouseDown += OnNoteMouseDown;
        ctrl.ContentChanged += OnNoteContentChanged;
        ctrl.DeleteClicked += OnNoteDeleteClicked;
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

            // Notify VM of new position
            _vm.UpdateNotePosition(
                _dragNote.NoteId,
                Canvas.GetLeft(_dragNote),
                Canvas.GetTop(_dragNote));

            _localChanges.Remove(_dragNote.NoteId);
            _dragNote = null;
        }
    }

    private void OnNoteContentChanged(string noteId, string title, string content)
    {
        _vm.UpdateNoteContent(noteId, title, content);
    }

    private async void OnNoteDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is NoteControl nc)
        {
            await _vm.DeleteNoteAsync(nc.NoteId);
        }
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
        _colorPickerOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            Child = new Border
            {
                Width = 300, Height = 60,
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = CreateColorPalette(),
            }
        };
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
}
