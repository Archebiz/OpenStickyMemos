using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

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

    public static readonly DependencyProperty AuthorNameProperty =
        DependencyProperty.Register(nameof(AuthorName), typeof(string), typeof(NoteControl),
            new PropertyMetadata(string.Empty, OnAuthorChanged));

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

    public string AuthorName
    {
        get => (string)GetValue(AuthorNameProperty);
        set => SetValue(AuthorNameProperty, value);
    }

    // ── Events ──

    public event RoutedEventHandler? DeleteClicked;
    public event Action<string, string, string>? ContentChanged; // noteId, title, content
    public event Action<string, bool>? PinToggled; // noteId, isPinned
    public event Action<string>? ColorClicked; // noteId
    public event Action<string>? BringToFrontClicked; // noteId
    public event Action<string>? SendToBackClicked; // noteId
    public event Action<string, double, double>? ResizeCompleted; // noteId, width, height
    public event Action<string>? ResizeStarted; // noteId

    // ── Glassmorphism shadow presets ──
    private static readonly DropShadowEffect NormalShadow = new()
    {
        BlurRadius = 28, Opacity = 0.18, ShadowDepth = 6, Color = Colors.Black
    };

    private static readonly DropShadowEffect HoverShadow = new()
    {
        BlurRadius = 34, Opacity = 0.22, ShadowDepth = 8, Color = Colors.Black
    };

    public NoteControl()
    {
        InitializeComponent();
        this.DataContext = this;
        this.Loaded += OnLoaded;
        this.MouseEnter += OnMouseEnterNote;
        this.MouseLeave += OnMouseLeaveNote;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyColor();
        ApplyPinStyle(IsPinned);
        SyncAuthorInfo();

        // Fade-in animation on load
        this.Opacity = 0;
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        this.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void OnMouseEnterNote(object sender, MouseEventArgs e)
    {
        NoteBorder.Effect = HoverShadow;
        ToolbarOverlay.Opacity = 1.0;

        // Subtle scale up
        var scaleUp = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1.0,
            To = 1.02,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        var scaleTransform = (NoteBorder.RenderTransform as ScaleTransform) ?? new ScaleTransform(1, 1);
        if (NoteBorder.RenderTransform != scaleTransform)
            NoteBorder.RenderTransform = scaleTransform;
        NoteBorder.RenderTransformOrigin = new Point(0.5, 0.5);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUp);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleUp);
    }

    private void OnMouseLeaveNote(object sender, MouseEventArgs e)
    {
        NoteBorder.Effect = NormalShadow;
        ToolbarOverlay.Opacity = 0.0;

        // Scale back to normal
        var scaleDown = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1.02,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        if (NoteBorder.RenderTransform is ScaleTransform st)
        {
            st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDown);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleDown);
        }
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

    private static void OnAuthorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl) ctrl.SyncAuthorInfo();
    }

    private static void OnPinnedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl)
        {
            var pinned = (bool)e.NewValue;
            ctrl.PinButton.Opacity = pinned ? 1.0 : 0.4;
            ctrl.PinButton.ToolTip = pinned ? "Quitar pin" : "Fijar nota";
            ctrl.PinIndicator.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
            ctrl.ApplyPinStyle(pinned);
        }
    }

    // ── View/Edit mode ──

    private bool _isEditing;
    private bool _resizeStarted;

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
        var hasTitle = !string.IsNullOrEmpty(Title);
        ViewTitle.Text = hasTitle ? Title : "(Sin título)";
        ViewTitle.FontStyle = hasTitle ? FontStyles.Normal : FontStyles.Italic;
        ViewTitle.Foreground = hasTitle
            ? new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
            : new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));

        var hasContent = !string.IsNullOrEmpty(NoteContent);
        ViewContent.Text = NoteContent ?? string.Empty;
        ViewContent.Visibility = hasContent ? Visibility.Visible : Visibility.Collapsed;

        // Show placeholder "Doble click para editar" when note is completely empty
        var isEmpty = !hasTitle && !hasContent;
        EmptyPlaceholder.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Author ──

    private void SyncAuthorInfo()
    {
        var hasAuthor = !string.IsNullOrEmpty(AuthorName);
        AuthorPanel.Visibility = hasAuthor ? Visibility.Visible : Visibility.Collapsed;
        AuthorText.Text = AuthorName ?? string.Empty;
    }

    // ── Color ──

    private void ApplyColor()
    {
        if (NoteBorder is null) return;

        Color baseColor;
        try { baseColor = (Color)ColorConverter.ConvertFromString(NoteColor)!; }
        catch { baseColor = Color.FromRgb(0xFF, 0xE0, 0x66); }

        if (NoteBgGradient is not null)
        {
            // Apply glass gradient: top at ~94% opacity, bottom at ~75%
            NoteBgGradient.GradientStops[0].Color = Color.FromArgb(0xF0, baseColor.R, baseColor.G, baseColor.B);
            NoteBgGradient.GradientStops[1].Color = Color.FromArgb(0xC0, baseColor.R, baseColor.G, baseColor.B);
        }
        else
        {
            // Fallback: semi-transparent solid
            NoteBorder.Background = new SolidColorBrush(Color.FromArgb(0xE0, baseColor.R, baseColor.G, baseColor.B));
        }
    }

    private void ApplyPinStyle(bool pinned)
    {
        if (NoteBorder is not null)
        {
            if (pinned)
            {
                NoteBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xD9));
                NoteBorder.BorderThickness = new Thickness(2.5);
                PinButton.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xD9));
                PinButton.FontSize = 11;
            }
            else
            {
                NoteBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
                NoteBorder.BorderThickness = new Thickness(1);
                PinButton.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
                PinButton.FontSize = 10;
            }
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
        if (IsPinned) return;
        if (!_resizeStarted)
        {
            _resizeStarted = true;
            ResizeStarted?.Invoke(NoteId);
        }
        var newWidth = Math.Max(120, ActualWidth + e.HorizontalChange);
        var newHeight = Math.Max(80, ActualHeight + e.VerticalChange);
        Width = newWidth;
        Height = newHeight;
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _resizeStarted = false;
        if (!IsPinned)
            ResizeCompleted?.Invoke(NoteId, Width, Height);
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
