using System;
using Avalonia;
using Avalonia.Controls;
using WorkoutLog.ViewModels;

namespace WorkoutLog.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var top = TopLevel.GetTopLevel(this);
        if (DataContext is MainViewModel vm)
            vm.StorageProvider = () => TopLevel.GetTopLevel(this)?.StorageProvider;

        // Keep content clear of the notch and the home indicator on iPhone.
        if (top?.InsetsManager is { } insets)
        {
            Padding = insets.SafeAreaPadding;
            insets.SafeAreaChanged += (_, _) => Padding = insets.SafeAreaPadding;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel vm)
            vm.StorageProvider = () => TopLevel.GetTopLevel(this)?.StorageProvider;
    }
}
