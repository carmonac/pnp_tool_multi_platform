using PnpTool.Core;

namespace PnpTool.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public string Title { get; } = $"{AppInfo.Name} {AppInfo.Version}";
}
