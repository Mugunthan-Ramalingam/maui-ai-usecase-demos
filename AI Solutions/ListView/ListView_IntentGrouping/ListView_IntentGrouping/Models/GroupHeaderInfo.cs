using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ListView_IntentGrouping.Models;

/// <summary>
/// One row in the SfListView's group header.
///
/// Each <see cref="FeedbackItem.GroupKey"/> is set to one of these
/// records, and the GroupDescriptor groups by reference. The header
/// template binds <c>Key.Title</c> and <c>Key.ItemCount</c>; every
/// group shares the same gray chrome declared in MainPage.xaml, so
/// the record only needs to carry the title and the item count.
/// </summary>
public sealed class GroupHeaderInfo : INotifyPropertyChanged
{
    private int _itemCount;

    public GroupHeaderInfo(string title, int itemCount)
    {
        Title = title;
        _itemCount = itemCount;
    }

    /// <summary>
    /// Convenience factory used by the user-driven grouping pipeline
    /// (rating buckets, AI intents, senders, months).
    /// </summary>
    public static GroupHeaderInfo FromBucket(string title, int itemCount) =>
        new(title, itemCount);

    /// <summary>
    /// Friendly label for a 1-5 rating bucket so the group header
    /// reads naturally ("3 Stars") instead of the raw "3".
    /// </summary>
    public static string GetRatingBucketTitle(int rating) =>
        rating switch
        {
            1 => "1 Star",
            2 => "2 Stars",
            3 => "3 Stars",
            4 => "4 Stars",
            5 => "5 Stars",
            _ => $"{rating} Stars"
        };

    public string Title { get; }

    public int ItemCount
    {
        get => _itemCount;
        set
        {
            if (_itemCount == value) return;
            _itemCount = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}