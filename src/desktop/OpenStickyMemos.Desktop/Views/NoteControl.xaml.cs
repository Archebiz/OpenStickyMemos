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
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(string), typeof(NoteControl),
            new PropertyMetadata(string.Empty));

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

    public string Content
    {
        get => (string)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
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

    public NoteControl()
    {
        InitializeComponent();
        this.Loaded += (_, _) => ApplyColor();
        this.MouseEnter += (_, _) => NoteBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
        { BlurRadius = 12, Opacity = 0.3, ShadowDepth = 3 };
        this.MouseLeave += (_, _) => NoteBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
        { BlurRadius = 8, Opacity = 0.2, ShadowDepth = 2 };
    }

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl) ctrl.ApplyColor();
    }

    private static void OnPinnedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl) ctrl.PinIndicator.Visibility =
            (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyColor()
    {
        if (NoteBorder is not null && !string.IsNullOrEmpty(NoteColor))
        {
            try { NoteBorder.Background = (Brush)new BrushConverter().ConvertFrom(NoteColor)!; }
            catch { NoteBorder.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x66)); }
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteClicked?.Invoke(this, e);
    }

    private void TitleBox_GotFocus(object sender, RoutedEventArgs e) { }
    private void TitleBox_LostFocus(object sender, RoutedEventArgs e)
        => NotifyContentChanged();

    private void ContentBox_GotFocus(object sender, RoutedEventArgs e) { }
    private void ContentBox_LostFocus(object sender, RoutedEventArgs e)
        => NotifyContentChanged();

    private void NotifyContentChanged()
    {
        ContentChanged?.Invoke(NoteId, Title, Content);
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var newWidth = Math.Max(120, ActualWidth + e.HorizontalChange);
        var newHeight = Math.Max(80, ActualHeight + e.VerticalChange);
        Width = newWidth;
        Height = newHeight;
    }

    // ── Drag support via Thumb or Mouse ──

    public event Action<NoteControl, MouseEventArgs>? NoteMouseDown;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.OriginalSource is not Thumb)
            NoteMouseDown?.Invoke(this, e);
    }
}
