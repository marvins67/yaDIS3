using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using YaDIS3.Desktop.ViewModels;

namespace YaDIS3.Desktop.Views;

/// <summary>Fenêtre d'édition des sagas reproduisant SagaForm de yaDIS2.</summary>
public sealed class SagaEditorWindow : Window
{
    private readonly SagaEditorState _state;
    private readonly string _workspace;
    private readonly TextBox _name;
    private readonly TextBox _sortName;
    private readonly ListBox _members;
    private readonly Image _poster;
    private readonly Image _fanart;
    private Bitmap? _posterBitmap;
    private Bitmap? _fanartBitmap;
    private PointerPressedEventArgs? _pressed;
    private Point _pressPosition;
    private int _dragIndex = -1;
    private bool _dragging;

    /// <summary>Initialise l'éditeur sans modifier les groupes enregistrés.</summary>
    public SagaEditorWindow(
        SagaContextDraft original,
        string workspace
    )
    {
        _state = new SagaEditorState(original);
        _workspace = workspace;
        Title = "Éditer";
        Width = 660;
        Height = 337;
        MinWidth = 660;
        MaxWidth = 660;
        MinHeight = 337;
        MaxHeight = 337;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        _name = new TextBox { Text = original.Name };
        _sortName = new TextBox { Text = original.SortName };
        _members = new ListBox
        {
            SelectionMode = SelectionMode.Multiple,
            Height = 169,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _poster = new Image { Width = 127, Height = 169, Stretch = Stretch.Fill };
        _fanart = new Image { Width = 300, Height = 169, Stretch = Stretch.Fill };

        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("180,127,300"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,169,Auto"),
            ColumnSpacing = 6,
            RowSpacing = 4,
            Margin = new Thickness(12, 8),
        };
        Add(grid, Label("Nom de la saga:"), 0, 0);
        Add(grid, Label("Nom pour le tri:"), 1, 0);
        Add(grid, _name, 0, 1);
        _sortName.Width = 180;
        _sortName.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumnSpan(_sortName, 2);
        Add(grid, _sortName, 1, 1);
        Add(grid, Label("Membres de la saga:"), 0, 2);
        Add(grid, Label("Jaquette:"), 1, 2);
        Add(grid, Label("Fanart:"), 2, 2);

        // Les trois vues doivent rester alignées horizontalement.
        Grid pictures = new()
        {
            ColumnDefinitions = new ColumnDefinitions("180,127,300"),
            ColumnSpacing = 6,
        };
        Grid.SetRow(pictures, 3);
        Grid.SetColumnSpan(pictures, 3);
        Add(pictures, _members, 0, 0);
        Button posterButton = new()
        {
            Content = _poster,
            Padding = new Thickness(0),
            Width = 127,
            Height = 169,
        };
        Button fanartButton = new()
        {
            Content = _fanart,
            Padding = new Thickness(0),
            Width = 300,
            Height = 169,
        };
        Add(pictures, posterButton, 1, 0);
        Add(pictures, fanartButton, 2, 0);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        Button ok = new() { Content = "OK", MinWidth = 75 };
        Button cancel = new() { Content = "Annuler", MinWidth = 75 };
        actions.Children.Add(ok);
        actions.Children.Add(cancel);
        Grid.SetRow(actions, 4);
        Grid.SetColumnSpan(actions, 3);
        grid.Children.Add(actions);
        Content = grid;

        posterButton.Click += (_, _) =>
        {
            _state.NextArtwork(true);
            RefreshArtwork();
        };
        fanartButton.Click += (_, _) =>
        {
            _state.NextArtwork(false);
            RefreshArtwork();
        };
        cancel.Click += (_, _) => Close(null);
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                Close(null);
                args.Handled = true;
            }
        };
        ok.Click += (_, _) =>
        {
            _state.Name = _name.Text ?? "";
            _state.SortName = _sortName.Text ?? "";
            if (_state.Name.IndexOfAny(
                ['\\', '/', ':', '*', '?', '"', '<', '>', '|', '&']
            ) >= 0)
            {
                ShowValidationError();
                return;
            }
            Close(_state.ToDraft());
        };
        _members.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Delete)
            {
                _state.Remove(_members.SelectedItems?
                    .OfType<string>() ?? []);
                RefreshMembers();
                RefreshArtwork();
                args.Handled = true;
            }
        };
        DragDrop.SetAllowDrop(_members, true);
        _members.PointerPressed += (_, args) =>
        {
            _dragIndex = MemberIndex(args.Source);
            _pressed = args;
            _pressPosition = args.GetPosition(_members);
        };
        _members.PointerMoved += async (_, args) =>
        {
            if (_dragging || _pressed is null || _dragIndex < 0
                || !args.GetCurrentPoint(_members)
                    .Properties.IsLeftButtonPressed)
            {
                return;
            }
            Point current = args.GetPosition(_members);
            if (Math.Abs(current.X - _pressPosition.X) < 5
                && Math.Abs(current.Y - _pressPosition.Y) < 5)
            {
                return;
            }
            _dragging = true;
            int source = _dragIndex;
            PointerPressedEventArgs pressed = _pressed;
            _pressed = null;
            DataTransfer transfer = new();
            transfer.Add(DataTransferItem.CreateText(
                source.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ));
            try
            {
                await DragDrop.DoDragDropAsync(
                    pressed, transfer, DragDropEffects.Move
                );
            }
            finally
            {
                _dragging = false;
            }
        };
        _members.PointerReleased += (_, _) => _pressed = null;
        DragDrop.AddDragOverHandler(_members, (_, args) =>
        {
            args.DragEffects = args.DataTransfer.TryGetText() is null
                ? DragDropEffects.None
                : DragDropEffects.Move;
        });
        DragDrop.AddDropHandler(_members, (_, args) =>
        {
            if (int.TryParse(args.DataTransfer.TryGetText(), out int source))
            {
                int destination = MemberIndex(args.Source);
                if (destination >= 0)
                {
                    _state.Move(source, destination);
                    RefreshMembers();
                    _members.SelectedIndex = destination;
                    args.DragEffects = DragDropEffects.Move;
                }
            }
        });
        Closed += (_, _) =>
        {
            _posterBitmap?.Dispose();
            _fanartBitmap?.Dispose();
        };
        RefreshMembers();
        RefreshArtwork();
    }

    private static TextBlock Label(string text) =>
        new() { Text = text };

    private static void Add(
        Grid grid,
        Control control,
        int column,
        int row
    )
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private int MemberIndex(object? source)
    {
        ListBoxItem? item = (source as Visual)?
            .GetSelfAndVisualAncestors()
            .OfType<ListBoxItem>()
            .FirstOrDefault();
        return item?.Content is string movie
            ? _state.Members.ToList().IndexOf(movie)
            : -1;
    }

    private void RefreshMembers()
    {
        _members.ItemsSource = _state.Members.ToArray();
    }

    private void RefreshArtwork()
    {
        ReplaceImage(
            _poster,
            ref _posterBitmap,
            _state.Poster,
            "poster.jpg"
        );
        ReplaceImage(
            _fanart,
            ref _fanartBitmap,
            _state.Fanart,
            "fanart.jpg"
        );
    }

    private void ReplaceImage(
        Image image,
        ref Bitmap? previous,
        string member,
        string filename
    )
    {
        Bitmap? next = null;
        string path = Path.Combine(
            _workspace, "data", member, filename
        );
        try
        {
            if (member.Length > 0 && File.Exists(path))
            {
                using MemoryStream stream = new(
                    File.ReadAllBytes(path)
                );
                next = new Bitmap(stream);
            }
        }
        catch (Exception exception)
            when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or InvalidDataException)
        {
            // Comme PictureBox, une image absente laisse l'aperçu vide.
        }
        image.Source = next;
        previous?.Dispose();
        previous = next;
    }

    private async void ShowValidationError()
    {
        Window warning = new()
        {
            Title = "Nom invalide",
            Width = 330,
            Height = 145,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        Button close = new() { Content = "OK", MinWidth = 75 };
        close.Click += (_, _) => warning.Close();
        warning.Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = "Caractères interdits : \\ / : * ? \" < > | &",
                },
                close,
            },
        };
        await warning.ShowDialog(this);
    }
}
