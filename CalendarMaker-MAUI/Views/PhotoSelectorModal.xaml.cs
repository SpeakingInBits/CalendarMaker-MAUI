using CalendarMaker_MAUI.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CalendarMaker_MAUI.Views;

public partial class PhotoSelectorModal : ContentPage
{
    private readonly ObservableCollection<PhotoItem> _photos = new();
    private PhotoItem? _selectedPhoto;

    public event EventHandler<PhotoSelectedEventArgs>? PhotoSelected;
    public event EventHandler? RemoveRequested;
    public event EventHandler? Cancelled;

    public PhotoSelectorModal(IEnumerable<ImageAsset> allPhotos, string slotDescription)
    {
        InitializeComponent();

        PhotosCollectionView.ItemsSource = _photos;

        // Update header text
        if (FindByName("HeaderLabel") is Label headerLabel)
        {
            headerLabel.Text = $"Select Photo for {slotDescription}";
        }

        // Wire up events
        CancelBtn.Clicked += OnCancelClicked;
        RemoveBtn.Clicked += OnRemoveClicked;
        AssignBtn.Clicked += OnAssignClicked;

        // Load photos
        LoadPhotos(allPhotos);

        // Update status
        UpdateStatus();
    }

    private void LoadPhotos(IEnumerable<ImageAsset> allPhotos)
    {
        _photos.Clear();

        // Group by path to determine if unassigned version exists and if photo is in use
        var photosByPath = allPhotos.GroupBy(a => a.Path).ToList();

        // Sort: not in use first, then by filename
        var sortedPhotos = photosByPath
            .Select(group =>
            {
                var representative = group.First();
                bool hasUnassigned = group.Any(a => a.Role == "unassigned");
                bool isInUse = group.Any(a => a.Role != "unassigned");

                return new PhotoItem
                {
                    Asset = representative,
                    Path = representative.Path,
                    FileName = System.IO.Path.GetFileName(representative.Path),
                    IsUnassigned = hasUnassigned,
                    IsInUse = isInUse,
                    IsSelected = false
                };
            })
            .OrderBy(p => p.IsInUse) // Not in use first (false < true)
            .ThenBy(p => p.FileName) // Then alphabetically by filename
            .ToList();

        foreach (var photo in sortedPhotos)
        {
            _photos.Add(photo);
        }

        if (!_photos.Any())
        {
            StatusLabel.Text = "No photos available. Import photos first using the Add Photos button.";
            AssignBtn.IsEnabled = false;
        }
    }

    private void OnPhotoSelected(object? sender, SelectionChangedEventArgs e)
    {
        // Clear previous selection
        if (_selectedPhoto != null)
        {
            _selectedPhoto.IsSelected = false;
        }

        // Set new selection
        _selectedPhoto = e.CurrentSelection?.FirstOrDefault() as PhotoItem;

        if (_selectedPhoto != null)
        {
            _selectedPhoto.IsSelected = true;
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_selectedPhoto != null)
        {
            StatusLabel.Text = "Photo selected. Tap Assign to confirm.";
            AssignBtn.IsEnabled = true;
        }
        else
        {
            StatusLabel.Text = _photos.Any() ? "Tap a photo to select, then tap Assign" : "No photos available.";
            AssignBtn.IsEnabled = false;
        }
    }

    private void OnAssignClicked(object? sender, EventArgs e)
    {
        if (_selectedPhoto?.Asset != null)
        {
            PhotoSelected?.Invoke(this, new PhotoSelectedEventArgs(_selectedPhoto.Asset));
        }
    }

    private void OnRemoveClicked(object? sender, EventArgs e)
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}

public class PhotoItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public ImageAsset? Asset { get; set; }
    public string? Path { get; set; }
    public string? FileName { get; set; }
    public bool IsUnassigned { get; set; }
    public bool IsInUse { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PhotoSelectedEventArgs : EventArgs
{
    public ImageAsset SelectedAsset { get; }

    public PhotoSelectedEventArgs(ImageAsset selectedAsset)
    {
        SelectedAsset = selectedAsset;
    }
}