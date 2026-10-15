using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using OpenStickyMemos.Desktop.Services;

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

    public static readonly DependencyProperty IsArchivedProperty =
        DependencyProperty.Register(nameof(IsArchived), typeof(bool), typeof(NoteControl),
            new PropertyMetadata(false, OnArchivedChanged));

    public static readonly DependencyProperty PendingArchiveProperty =
        DependencyProperty.Register(nameof(PendingArchive), typeof(bool), typeof(NoteControl),
            new PropertyMetadata(false, OnPendingChanged));

    public static readonly DependencyProperty PendingDeleteProperty =
        DependencyProperty.Register(nameof(PendingDelete), typeof(bool), typeof(NoteControl),
            new PropertyMetadata(false, OnPendingChanged));

    public static readonly DependencyProperty IsTaskListProperty =
        DependencyProperty.Register(nameof(IsTaskList), typeof(bool), typeof(NoteControl),
            new PropertyMetadata(false, OnTaskListChanged));

    public static readonly DependencyProperty TagsProperty =
        DependencyProperty.Register(nameof(Tags), typeof(string), typeof(NoteControl),
            new PropertyMetadata(null, OnMetaChanged));

    public static readonly DependencyProperty ReminderAtProperty =
        DependencyProperty.Register(nameof(ReminderAt), typeof(DateTime?), typeof(NoteControl),
            new PropertyMetadata(null, OnMetaChanged));

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

    public bool IsArchived
    {
        get => (bool)GetValue(IsArchivedProperty);
        set => SetValue(IsArchivedProperty, value);
    }

    public bool PendingArchive
    {
        get => (bool)GetValue(PendingArchiveProperty);
        set => SetValue(PendingArchiveProperty, value);
    }

    public bool PendingDelete
    {
        get => (bool)GetValue(PendingDeleteProperty);
        set => SetValue(PendingDeleteProperty, value);
    }

    public bool IsTaskList
    {
        get => (bool)GetValue(IsTaskListProperty);
        set => SetValue(IsTaskListProperty, value);
    }

    public string? Tags
    {
        get => (string?)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public DateTime? ReminderAt
    {
        get => (DateTime?)GetValue(ReminderAtProperty);
        set => SetValue(ReminderAtProperty, value);
    }

    // ── Events ──

    public event RoutedEventHandler? DeleteClicked;
    public event Action<string, string, string>? ContentChanged; // noteId, title, content
    public event Action<string, bool>? PinToggled; // noteId, isPinned
    public event Action<string>? ColorClicked; // noteId
    public event Action<string>? ArchiveClicked; // noteId
    public event Action<string, bool>? TaskModeToggled; // noteId, isTaskList
    public event Action<string, string?, DateTime?>? MetaChanged; // noteId, tags, reminderAt
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
        SyncArchiveButton();
        UpdatePendingState();
        UpdateMetaView();
        BuildEmojiPanel();

        // Sutil fade-in desde opacidad alta para evitar intermediate render target
        // que causa ghosting al arrastrar notas nuevas (Opacity<1 + DropShadowEffect
        // obliga a WPF a crear un buffer intermedio que no se actualiza sincronizadamente
        // al cambiar Canvas.Left/Top durante el arrastre).
        this.Opacity = 0.92;
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0.92,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(200),
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

    public void Highlight()
    {
        var originalBrush = NoteBorder.BorderBrush;
        var originalThickness = NoteBorder.BorderThickness;

        NoteBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241));
        NoteBorder.BorderThickness = new Thickness(3);

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            NoteBorder.BorderBrush = originalBrush;
            NoteBorder.BorderThickness = originalThickness;
        };
        timer.Start();

        BringIntoView();
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

    private static void OnArchivedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl) ctrl.SyncArchiveButton();
    }

    private static void OnPendingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl) ctrl.UpdatePendingState();
    }

    private static void OnTaskListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl)
        {
            ctrl.UpdateTaskModeButtonVisual();
            ctrl.UpdateMetaView();
            if (!ctrl._isEditing)
                ctrl.SyncViewMode();
        }
    }

    private static void OnMetaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteControl ctrl) ctrl.UpdateMetaView();
    }

    // ── View/Edit mode ──

    private bool _isEditing;
    private bool _resizeStarted;

    public void StartEdit()
    {
        if (_isEditing) return;
        _isEditing = true;
        TitleBox.Text = Title;

        if (IsTaskList)
        {
            _editTasks = ParseTasks(NoteContent);
            RichEditor.Visibility = Visibility.Collapsed;
            TaskEditorScroll.Visibility = Visibility.Visible;
            AddTaskButton.Visibility = Visibility.Visible;
            RebuildTaskEditor();
        }
        else
        {
            RichEditor.Visibility = Visibility.Visible;
            TaskEditorScroll.Visibility = Visibility.Collapsed;
            AddTaskButton.Visibility = Visibility.Collapsed;
            RichEditor.Document = RichTextConverter.ToFlowDocument(NoteContent);
        }

        ViewPanel.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Visible;
        UpdateTaskModeButtonVisual();
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
            NoteContent = IsTaskList
                ? SerializeTasks(_editTasks ?? ParseTasks(NoteContent))
                : RichTextConverter.ToHtml(RichEditor.Document);
            SyncViewMode();
            ContentChanged?.Invoke(NoteId, Title, NoteContent);
        }
        _editTasks = null;
    }

    private void Editor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            EndEdit(false);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)))
        {
            EndEdit(true);
            e.Handled = true;
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

        if (IsTaskList)
        {
            // Modo tareas: mostrar filas con checkbox (se tachan al marcar)
            ViewRichContent.Visibility = Visibility.Collapsed;
            TaskListScroll.Visibility = Visibility.Visible;
            RebuildTaskView();
        }
        else
        {
            TaskListScroll.Visibility = Visibility.Collapsed;
            if (hasContent)
            {
                ViewRichContent.Document = RichTextConverter.ToFlowDocument(NoteContent);
                ViewRichContent.Visibility = Visibility.Visible;
            }
            else
            {
                ViewRichContent.Visibility = Visibility.Collapsed;
            }
        }

        // Show placeholder "Doble click para editar" when note is completely empty
        var isEmpty = !hasTitle && !hasContent && !IsTaskList;
        EmptyPlaceholder.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;

        UpdateMetaView();
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
            SyncViewMode();
        }
    }

    private void RichEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        // Don't exit edit mode - just save silently
        if (_isEditing)
        {
            NoteContent = RichTextConverter.ToHtml(RichEditor.Document);
            SyncViewMode();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        EndEdit(save: true);
    }

    private void CancelEditButton_Click(object sender, RoutedEventArgs e)
    {
        EndEdit(save: false);
    }

    private void ArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        ArchiveClicked?.Invoke(NoteId);
    }

    private void SyncArchiveButton()
    {
        if (ArchiveButton is null) return;
        if (IsArchived)
        {
            ArchiveButton.Content = "📥";
            ArchiveButton.ToolTip = "Restaurar nota";
        }
        else
        {
            ArchiveButton.Content = "🗄️";
            ArchiveButton.ToolTip = "Archivar nota";
        }
    }

    private void UpdatePendingState()
    {
        if (PendingOverlay is null || NoteBorder is null) return;
        var pending = PendingArchive || PendingDelete;
        PendingOverlay.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
        NoteBorder.Opacity = pending ? 0.65 : 1.0;
        if (pending)
            PendingText.Text = PendingDelete
                ? "Pendiente de aprobación para eliminar"
                : "Pendiente de aprobación para archivar";
    }

    // ── Formato enriquecido ──

    private void BoldButton_Click(object sender, RoutedEventArgs e)
    {
        var isBold = RichEditor.Selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight fw
            && fw == FontWeights.Bold;
        RichEditor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,
            isBold ? FontWeights.Normal : FontWeights.Bold);
        RichEditor.Focus();
    }

    private void ItalicButton_Click(object sender, RoutedEventArgs e)
    {
        var isItalic = RichEditor.Selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle fs
            && fs == FontStyles.Italic;
        RichEditor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty,
            isItalic ? FontStyles.Normal : FontStyles.Italic);
        RichEditor.Focus();
    }

    private void UnderlineButton_Click(object sender, RoutedEventArgs e)
    {
        var hasUnderline = RichEditor.Selection.GetPropertyValue(TextBlock.TextDecorationsProperty) is TextDecorationCollection dec
            && HasUnderline(dec);
        RichEditor.Selection.ApplyPropertyValue(TextBlock.TextDecorationsProperty,
            hasUnderline ? null : TextDecorations.Underline);
        RichEditor.Focus();
    }

    private static bool HasUnderline(TextDecorationCollection decorations)
    {
        foreach (var d in decorations)
            if (d.Location == TextDecorationLocation.Underline) return true;
        return false;
    }

    private void BulletButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleBullets();
        RichEditor.Focus();
    }

    private void ToggleBullets()
    {
        var current = RichEditor.Selection.Start.Paragraph;
        if (current is not null && current.Parent is ListItem item && item.Parent is List existingList)
        {
            UnwrapList(existingList);
        }
        else if (current is not null)
        {
            WrapParagraphInList(current);
        }
    }

    private void WrapParagraphInList(Paragraph paragraph)
    {
        if (paragraph is null) return;

        var list = new List();
        var listItem = new ListItem();
        var itemParagraph = new Paragraph();

        while (paragraph.Inlines.Count > 0)
        {
            var inline = paragraph.Inlines.FirstInline;
            paragraph.Inlines.Remove(inline);
            itemParagraph.Inlines.Add(inline);
        }

        listItem.Blocks.Add(itemParagraph);
        list.ListItems.Add(listItem);

        var doc = RichEditor.Document;
        doc.Blocks.InsertBefore(paragraph, list);
        doc.Blocks.Remove(paragraph);
    }

    private void UnwrapList(List list)
    {
        var doc = RichEditor.Document;

        Block? insertAfter = list;
        var paragraphs = list.ListItems
            .SelectMany(item => item.Blocks)
            .OfType<Paragraph>()
            .ToList();

        foreach (var p in paragraphs)
        {
            var para = new Paragraph();
            while (p.Inlines.Count > 0)
            {
                var inline = p.Inlines.FirstInline;
                p.Inlines.Remove(inline);
                para.Inlines.Add(inline);
            }
            doc.Blocks.InsertAfter(insertAfter, para);
            insertAfter = para;
        }

        doc.Blocks.Remove(list);
    }

    // ── Emojis ──

    private static readonly string[] Emojis =
    {
        "😀","😄","😊","🙂","😉","😍","😘","😎","🤔","😴",
        "👍","👎","👏","🙌","🤝","💪","🙏","👋","✋","👌",
        "❤️","💙","💚","💛","💜","⭐","✨","🔥","💯","🎉",
        "✅","❌","⚠️","📌","📝","📁","🗂️","🔔","⏰","💡",
        "🚀","⭐","🍕","☕","🍎","🌮","⚽","🎮","🎵","🌙"
    };

    private void BuildEmojiPanel()
    {
        if (EmojiPanel is null || EmojiPanel.Children.Count > 0) return;
        foreach (var emoji in Emojis)
        {
            var btn = new Button
            {
                Content = emoji,
                Width = 30,
                Height = 30,
                Margin = new Thickness(2),
                FontSize = 16,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = emoji
            };
            btn.Click += EmojiSelected_Click;
            EmojiPanel.Children.Add(btn);
        }
    }

    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        BuildEmojiPanel();
        EmojiPopup.IsOpen = true;
    }

    private void EmojiSelected_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string emoji)
        {
            RichEditor.CaretPosition.InsertTextInRun(emoji);
            RichEditor.Focus();
        }
        EmojiPopup.IsOpen = false;
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

    // ── Lista de tareas (modo checklist) ──

    private class TaskItem
    {
        public string Text { get; set; } = string.Empty;
        public bool Checked { get; set; }
    }

    private List<TaskItem>? _editTasks;

    private static List<TaskItem> ParseTasks(string? html)
    {
        var tasks = new List<TaskItem>();
        if (string.IsNullOrWhiteSpace(html)) return tasks;

        var regex = new System.Text.RegularExpressions.Regex(
            "<li(?<attrs>[^>]*)>(?<text>.*?)</li>",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match m in regex.Matches(html))
        {
            var attrs = m.Groups["attrs"].Value;
            tasks.Add(new TaskItem
            {
                Text = StripTags(m.Groups["text"].Value),
                Checked = attrs.Contains("checked", StringComparison.OrdinalIgnoreCase)
            });
        }
        return tasks;
    }

    private static string StripTags(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            System.Net.WebUtility.HtmlDecode(html), "<[^>]+>", string.Empty).Trim();
    }

    private static string SerializeTasks(IEnumerable<TaskItem> tasks)
    {
        var sb = new StringBuilder();
        sb.Append("<ul class=\"osm-tasks\">");
        foreach (var t in tasks)
        {
            sb.Append("<li");
            if (t.Checked) sb.Append(" class=\"checked\"");
            sb.Append(">").Append(System.Net.WebUtility.HtmlEncode(t.Text)).Append("</li>");
        }
        sb.Append("</ul>");
        return sb.ToString();
    }

    private static List<TaskItem> ContentToTasks(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return new List<TaskItem> { new() { Text = string.Empty } };
        if (content.Contains("osm-tasks", StringComparison.OrdinalIgnoreCase))
            return ParseTasks(content);

        var lines = StripTags(content)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tasks = lines.Select(l => new TaskItem { Text = l }).ToList();
        if (tasks.Count == 0) tasks.Add(new TaskItem { Text = string.Empty });
        return tasks;
    }

    private static string TasksToRichHtml(List<TaskItem> tasks)
    {
        var sb = new StringBuilder();
        sb.Append("<ul>");
        foreach (var t in tasks)
        {
            if (string.IsNullOrWhiteSpace(t.Text)) continue;
            sb.Append("<li>").Append(System.Net.WebUtility.HtmlEncode(t.Text)).Append("</li>");
        }
        sb.Append("</ul>");
        return sb.ToString();
    }

    private void RebuildTaskView()
    {
        if (TaskListPanel is null) return;
        TaskListPanel.Children.Clear();
        if (!IsTaskList) return;

        var tasks = ParseTasks(NoteContent);
        for (int i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var index = i;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            var cb = new CheckBox
            {
                IsChecked = task.Checked,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand
            };
            cb.Checked += (_, _) => OnTaskViewToggled(index, true);
            cb.Unchecked += (_, _) => OnTaskViewToggled(index, false);

            var tb = new TextBlock
            {
                Text = task.Text,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (task.Checked)
                tb.TextDecorations = TextDecorations.Strikethrough;

            row.Children.Add(cb);
            row.Children.Add(tb);
            TaskListPanel.Children.Add(row);
        }
    }

    private void OnTaskViewToggled(int index, bool isChecked)
    {
        var tasks = ParseTasks(NoteContent);
        if (index < tasks.Count)
        {
            tasks[index].Checked = isChecked;
            NoteContent = SerializeTasks(tasks);
            SyncViewMode();
            ContentChanged?.Invoke(NoteId, Title, NoteContent);
        }
    }

    private void RebuildTaskEditor()
    {
        if (TaskEditorItems is null) return;
        TaskEditorItems.Children.Clear();
        _editTasks ??= new List<TaskItem>();
        if (_editTasks.Count == 0)
            _editTasks.Add(new TaskItem { Text = string.Empty });
        foreach (var task in _editTasks)
            AddTaskRow(task);
    }

    private void AddTaskRow(TaskItem task)
    {
        var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cb = new CheckBox
        {
            IsChecked = task.Checked,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
            Cursor = Cursors.Hand
        };
        cb.Checked += (_, _) => task.Checked = true;
        cb.Unchecked += (_, _) => task.Checked = false;

        var tb = new TextBox
        {
            Text = task.Text,
            FontSize = 12,
            Padding = new Thickness(4, 2, 4, 2),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent
        };
        tb.TextChanged += (_, _) => task.Text = tb.Text;

        var remove = new Button
        {
            Content = "✕",
            Width = 18,
            Height = 18,
            FontSize = 9,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            Cursor = Cursors.Hand,
            ToolTip = "Quitar tarea",
            Tag = task
        };
        remove.Click += RemoveTask_Click;

        Grid.SetColumn(cb, 0);
        Grid.SetColumn(tb, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(cb);
        row.Children.Add(tb);
        row.Children.Add(remove);
        TaskEditorItems.Children.Add(row);
    }

    private void AddTaskButton_Click(object sender, RoutedEventArgs e)
    {
        _editTasks ??= new List<TaskItem>();
        var task = new TaskItem { Text = string.Empty };
        _editTasks.Add(task);
        AddTaskRow(task);
    }

    private void RemoveTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaskItem task && _editTasks is not null)
        {
            _editTasks.Remove(task);
            RebuildTaskEditor();
        }
    }

    private void TaskModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditing) return;

        IsTaskList = !IsTaskList;

        if (IsTaskList)
        {
            _editTasks = ContentToTasks(NoteContent);
            RichEditor.Visibility = Visibility.Collapsed;
            TaskEditorScroll.Visibility = Visibility.Visible;
            AddTaskButton.Visibility = Visibility.Visible;
            RebuildTaskEditor();
        }
        else
        {
            var html = TasksToRichHtml(_editTasks ?? ParseTasks(NoteContent));
            RichEditor.Document = RichTextConverter.ToFlowDocument(html);
            RichEditor.Visibility = Visibility.Visible;
            TaskEditorScroll.Visibility = Visibility.Collapsed;
            AddTaskButton.Visibility = Visibility.Collapsed;
            _editTasks = null;
        }

        UpdateTaskModeButtonVisual();
        TaskModeToggled?.Invoke(NoteId, IsTaskList);
    }

    private void UpdateTaskModeButtonVisual()
    {
        if (TaskModeButton is null) return;
        TaskModeButton.Background = IsTaskList
            ? new SolidColorBrush(Color.FromRgb(0xDD, 0xEA, 0xFB))
            : Brushes.Transparent;
        TaskModeButton.ToolTip = IsTaskList
            ? "Lista de tareas activa (clic para volver a texto)"
            : "Lista de tareas (modo checklist)";
    }

    // ── Etiquetas y recordatorio ──

    private void UpdateMetaView()
    {
        if (ReminderBadge is not null)
            ReminderBadge.Visibility = ReminderAt.HasValue ? Visibility.Visible : Visibility.Collapsed;

        if (TagChipsPanel is null) return;
        TagChipsPanel.Children.Clear();
        if (string.IsNullOrWhiteSpace(Tags))
        {
            TagChipsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var tag in Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            TagChipsPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xED, 0xF2, 0xF7)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 4, 2),
                Child = new TextBlock
                {
                    Text = "#" + tag,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
                }
            });
        }
        TagChipsPanel.Visibility = Visibility.Visible;
    }

    private void TagsButton_Click(object sender, RoutedEventArgs e)
    {
        TagsInput.Text = Tags ?? string.Empty;
        TagsPopup.IsOpen = true;
    }

    private void ApplyTags_Click(object sender, RoutedEventArgs e)
    {
        Tags = string.IsNullOrWhiteSpace(TagsInput.Text) ? null : TagsInput.Text;
        TagsPopup.IsOpen = false;
        UpdateMetaView();
        MetaChanged?.Invoke(NoteId, Tags, ReminderAt);
    }

    private void ReminderButton_Click(object sender, RoutedEventArgs e)
    {
        if (ReminderAt.HasValue)
        {
            var local = ReminderAt.Value.ToLocalTime();
            ReminderDatePicker.SelectedDate = local.Date;
            ReminderTimeInput.Text = local.ToString("HH:mm");
        }
        else
        {
            ReminderDatePicker.SelectedDate = DateTime.Today;
            ReminderTimeInput.Text = "09:00";
        }
        ReminderPopup.IsOpen = true;
    }

    private void SetReminder_Click(object sender, RoutedEventArgs e)
    {
        var date = ReminderDatePicker.SelectedDate;
        if (date is null || !TimeSpan.TryParse(ReminderTimeInput.Text, out var time))
        {
            MessageBox.Show("Ingresá una fecha y una hora válidas (HH:mm).", "Recordatorio",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ReminderAt = date.Value.Date.Add(time).ToUniversalTime();
        ReminderPopup.IsOpen = false;
        UpdateMetaView();
        MetaChanged?.Invoke(NoteId, Tags, ReminderAt);
    }

    private void ClearReminder_Click(object sender, RoutedEventArgs e)
    {
        ReminderAt = null;
        ReminderPopup.IsOpen = false;
        UpdateMetaView();
        MetaChanged?.Invoke(NoteId, Tags, ReminderAt);
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
        if (e.OriginalSource is not Thumb && e.ClickCount == 1 && !IsPinned && !_isEditing)
        {
            // No arrastrar al interactuar con checkboxes o el área de tareas
            if (IsTaskControlClick(e.OriginalSource)) return;
            NoteMouseDown?.Invoke(this, e);
        }
    }

    private bool IsTaskControlClick(object? source)
    {
        if (source is not DependencyObject d) return false;
        var current = d;
        while (current is not null)
        {
            if (ReferenceEquals(current, TaskListPanel) || ReferenceEquals(current, TaskEditorScroll))
                return true;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
