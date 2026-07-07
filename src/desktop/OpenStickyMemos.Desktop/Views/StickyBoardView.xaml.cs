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

    // Color picker state
    private readonly string[] _colors = { "#FFE066", "#FFB3BA", "#BAFFC9", "#BAE1FF", "#E8BAFF", "#FFD9BA", "#BAFFEC", "#FFF3BA" };
    private bool _showColorPicker;
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
        var ctrl = CreateNoteControl(note);
        Canvas.SetLeft(ctrl, note.PositionX);
        Canvas.SetTop(ctrl, note.PositionY);
        NotesCanvas.Children.Add(ctrl);
    }

    private void UpdateNoteOnCanvas(NoteItem note)
    {
        foreach (var child in NotesCanvas.Children)
        {
            if (child is NoteControl nc && nc.NoteId == note.Id)
            {
                nc.Title = note.Title ?? string.Empty;
                nc.Content = note.Content ?? string.Empty;
                nc.NoteColor = note.Color;
                nc.IsPinned = note.IsPinned;
                Canvas.SetLeft(nc, note.PositionX);
                Canvas.SetTop(nc, note.PositionY);
                nc.Width = note.Width;
                nc.Height = note.Height;
                break;
            }
        }
    }

    private void RemoveNoteFromCanvas(string noteId)
    {
        foreach (var child in NotesCanvas.Children)
        {
            if (child is NoteControl nc && nc.NoteId == noteId)
            {
                NotesCanvas.Children.Remove(nc);
                break;
            }
        }
    }

    private NoteControl CreateNoteControl(NoteItem note)
    {
        var ctrl = new NoteControl
        {
            NoteId = note.Id,
            Title = note.Title ?? string.Empty,
            Content = note.Content ?? string.Empty,
            NoteColor = note.Color,
            IsPinned = note.IsPinned,
            Width = note.Width,
            Height = note.Height,
        };

        ctrl.NoteMouseDown += OnNoteMouseDown;
        ctrl.ContentChanged += OnNoteContentChanged;
        ctrl.DeleteClicked += OnNoteDeleteClicked;

        // Mouse move/up on canvas handles dragging
        return ctrl;
    }

    private void OnNoteMouseDown(NoteControl note, MouseEventArgs e)
    {
        _dragNote = note;
        _dragStart = e.GetPosition(NotesCanvas);
        _isDragging = true;
        note.CaptureMouse();
        Panel.SetZIndex(note, 10);
    }

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
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
            Panel.SetZIndex(_dragNote, 0);

            // Notify VM of new position
            _vm.UpdateNotePosition(
                _dragNote.NoteId,
                Canvas.GetLeft(_dragNote),
                Canvas.GetTop(_dragNote));

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

    private void ColorPicker_Click(object sender, RoutedEventArgs e)
    {
        _showColorPicker = !_showColorPicker;
        if (_showColorPicker)
            ShowColorPicker();
        else
            HideColorPicker();
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
        if (sender is Button btn && btn.Tag is string color)
        {
            // Cambiar color de la nota seleccionada (si hay alguna)
            // Por simplicidad, se aplica a la primera nota en el ViewModel
            // En una versión completa, manejar selección
            HideColorPicker();
        }
    }

    private void HideColorPicker()
    {
        if (_colorPickerOverlay is not null && ((Grid)Content).Children.Contains(_colorPickerOverlay))
        {
            ((Grid)Content).Children.Remove(_colorPickerOverlay);
        }
        _showColorPicker = false;
    }
}
