using ListView_IntentGrouping.Services;
using Syncfusion.Maui.DataSource;
using Syncfusion.Maui.ListView;

namespace ListView_IntentGrouping;

/// <summary>
/// Keeps an SfListView's <c>GroupDescriptor</c> in step with the view-model's
/// grouping mode. Bind <see cref="GroupingKind"/> to the VM; the list groups by
/// <c>FeedbackItem.GroupKey</c> whenever the kind is not
/// <see cref="Services.GroupingKind.None"/> and shows a flat list otherwise.
/// </summary>
public sealed class IntentGroupingBehavior : Behavior<SfListView>
{
    public static readonly BindableProperty GroupingKindProperty =
        BindableProperty.Create(
            nameof(GroupingKind),
            typeof(GroupingKind),
            typeof(IntentGroupingBehavior),
            Services.GroupingKind.None,
            propertyChanged: (b, _, _) => ((IntentGroupingBehavior)b).Apply());

    private SfListView? listView;

    public GroupingKind GroupingKind
    {
        get => (GroupingKind)GetValue(GroupingKindProperty);
        set => SetValue(GroupingKindProperty, value);
    }

    protected override void OnAttachedTo(SfListView bindable)
    {
        base.OnAttachedTo(bindable);
        listView = bindable;
        // Behaviors are not part of the visual tree, so inherit the list's
        // binding context explicitly for the GroupingKind binding.
        BindingContext = bindable.BindingContext;
        bindable.BindingContextChanged += OnListViewBindingContextChanged;
        bindable.DataSource ??= new DataSource();
        Apply();
    }

    protected override void OnDetachingFrom(SfListView bindable)
    {
        bindable.BindingContextChanged -= OnListViewBindingContextChanged;
        listView = null;
        base.OnDetachingFrom(bindable);
    }

    private void OnListViewBindingContextChanged(object? sender, EventArgs e)
        => BindingContext = listView?.BindingContext;

    private void Apply()
    {
        var dataSource = listView?.DataSource;
        if (listView is null || dataSource is null)
        {
            return;
        }

        var grouped = GroupingKind != Services.GroupingKind.None;
        dataSource.GroupDescriptors.Clear();
        if (grouped)
        {
            dataSource.GroupDescriptors.Add(new GroupDescriptor
            {
                PropertyName = nameof(Models.FeedbackItem.GroupKey)
            });
        }
        listView.IsStickyHeader = grouped;
        dataSource.Refresh();
    }
}
