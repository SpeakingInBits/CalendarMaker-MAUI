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

    public PhotoSelectorModal(IEnumerable<ImageAsset> unassignedPhotos, string slotDescription)
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
        LoadPhotos(unassignedPhotos);
        
        // Update status
        UpdateStatus();
    }

    private void LoadPhotos(IEnumerable<ImageAsset> unassignedPhotos)
    {
        _photos.Clear();
        
        foreach (var asset in unassignedPhotos.OrderBy(a => a.Id))
        {
            _photos.Add(new PhotoItem
            {
                Asset = asset,
                Path = asset.Path,
                IsSelected = false
            });
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