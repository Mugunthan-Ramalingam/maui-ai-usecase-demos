using System.ComponentModel;

namespace SmartAIComboBox;

/// <summary>
/// Represents a single fruit option shown in the AI MultiSelect ComboBox.
/// Each fruit carries a rich tag string describing its nutrition/health
/// profile so the AI can map ANY natural-language request (including
/// "healthy fruit", "fruits for immunity", etc.) to real fruits.
/// </summary>
public class FruitModel : INotifyPropertyChanged
{
    public string? Name { get; set; }
    public double Price { get; set; }
    public string? Category { get; set; }
    public string? Diet { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public override string ToString() => Name ?? string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;
}
