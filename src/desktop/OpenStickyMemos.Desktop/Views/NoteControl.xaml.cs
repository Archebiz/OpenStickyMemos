using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenStickyMemos.Desktop.Views;

public partial class NoteControl : UserControl
{
    // ── Dependency Properties ──

    public static readonly DependencyProperty NoteIdProperty =
        DependencyProperty.Register(nameof(NoteId), typeof(string), typeof(NoteControl));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(NoteControl),
            new PropertyMetadata(string.Empty, OnTitleChanged));

    public static readonly DependencyProperty NoteContentProperty =
        DependencyProperty.Register(nameof(NoteContent), typeof(string), typeof(NoteControl),
            new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty NoteColorProperty =
        DependencyProperty.Register(nameof(NoteColor), typeof(string), typeof(NoteControl),
            new PropertyMetadata("#FFE066", OnColorChanged));

    public static readonly DependencyProperty IsPinnedProperty =
        DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(NoteControl),
            new PropertyMetadata(false, OnPinnedChanged));

    public string NoteId
    {
        get => (string)GetValue(NoteIdProperty);
        set => SetValue(NoteIdProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string NoteContent
    {
        get => (string)GetValue(NoteContentProperty);
        set => SetValue(NoteContentProperty, value);
    }

    public string NoteColor
    {
        get => (string)GetValue(NoteColorProperty);
        set => SetValue(NoteColorProperty, value);
    }

    public bool IsPinned
    {
        get => (bool)GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    // ── Events ──

    public event RoutedEventHandler? DeleteClicked;
    public event Action<string, string, string>? ContentChanged; // noteId, title, content
    public event Action<string, bool>? PinToggled; // noteId, isPinned
    public event Action<string>? ColorClicked; // noteId
    public event Action<string>? BringToFrontClicked; // noteId
    public event Action<string>? SendToBackClicked; // noteId
    public event Action<string, double, double>? ResizeCompleted; // noteId, width, height

    public NoteControl()
    {
        InitializeComponent();
        this.DataContext = this;
        this.Loaded += (_, _) => ApplyColor();
        this.MouseEnter += (_, _) => NoteBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
        { BlurRadius = 12, Opacity = 0.3, ShadowDepth = 3 };
        this.MouseLeave += (_, _) => NoteBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
        { BlurRadius = 8, Opacity = 0.2, ShadowDepth = 2 };
    }

    // ── Property changed callbacks ──

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl) ctrl.SyncViewMode();
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl) ctrl.SyncViewMode();
    }

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl) ctrl.ApplyColor();
    }

    private static void OnPinnedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl)
        {
            var pinned = (bool)e.NewValue;
            ctrl.PinButton.Opacity = pinned ? 1.0 : 0.4;
            ctrl.PinButton.ToolTip = pinned ? "Quitar pin" : "Fijar nota";
        }
    }

    // ── View/Edit mode ──

    private bool _isEditing;

    public void StartEdit()
    {
        if (_isEditing) return;
        _isEditing = true;
        TitleBox.Text = Title;
        ContentBox.Text = NoteContent;
        ViewPanel.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Visible;
        TitleBox.Focus();
        TitleBox.SelectAll();
    }

    public void EndEdit(bool save = true)
    {
        if (!_isEditing) return;
        _isEditing = false;
        EditPanel.Visibility = Visibility.Collapsed;
        ViewPanel.Visibility = Visibility.Visible;
        if (save)
        {
            Title = TitleBox.Text;
            NoteContent = ContentBox.Text;
            SyncViewMode();
            ContentChanged?.Invoke(NoteId, Title, NoteContent);
        }
    }

    private void SyncViewMode()
    {
        ViewTitle.Text = string.IsNullOrEmpty(Title) ? "(Sin título)" : Title;
        ViewTitle.FontStyle = string.IsNullOrEmpty(Title) ? FontStyles.Italic : FontStyles.Normal;
        ViewTitle.Foreground = string.IsNullOrEmpty(Title)
            ? new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA))
            : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        ViewContent.Text = NoteContent ?? string.Empty;
        ViewContent.Visibility = string.IsNullOrEmpty(NoteContent) ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── Color ──

    private void ApplyColor()
    {
        if (NoteBorder is not null && !string.IsNullOrEmpty(NoteColor))
        {
            try { NoteBorder.Background = (Brush)new BrushConverter().ConvertFrom(NoteColor)!; }
            catch { NoteBorder.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x66)); }
        }
    }

    // ── Event Handlers ──

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        IsPinned = !IsPinned;
        PinToggled?.Invoke(NoteId, IsPinned);
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        ColorClicked?.Invoke(NoteId);
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteClicked?.Invoke(this, e);
    }

    private void TitleBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Don't exit edit mode - just save silently
        if (_isEditing)
        {
            Title = TitleBox.Text;
            NoteContent = ContentBox.Text;
            SyncViewMode();
        }
    }

    private void ContentBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Don't exit edit mode - just save silently
        if (_isEditing)
        {
            Title = TitleBox.Text;
            NoteContent = ContentBox.Text;
            SyncViewMode();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        EndEdit(save: true);
    }

    private void BringFrontButton_Click(object sender, RoutedEventArgs e)
    {
        BringToFrontClicked?.Invoke(NoteId);
    }

    private void SendBackButton_Click(object sender, RoutedEventArgs e)
    {
        SendToBackClicked?.Invoke(NoteId);
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var newWidth = Math.Max(120, ActualWidth + e.HorizontalChange);
        var newHeight = Math.Max(80, ActualHeight + e.VerticalChange);
        Width = newWidth;
        Height = newHeight;
        // Fire resize event for API persistence
        ResizeCompleted?.Invoke(NoteId, newWidth, newHeight);
    }

    // ── Double-click to edit ──

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ClickCount == 2 && !_isEditing)
        {
            StartEdit();
            e.Handled = true;
        }
    }

    // ── Drag support ──

    public event Action<NoteControl, MouseEventArgs>? NoteMouseDown;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.OriginalSource is not Thumb && e.ClickCount == 1 && !IsPinned)
            NoteMouseDown?.Invoke(this, e);
    }
}
