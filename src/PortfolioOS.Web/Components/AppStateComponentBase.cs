using Microsoft.AspNetCore.Components;
using PortfolioOS.Web.Services;

namespace PortfolioOS.Web.Components;

/// <summary>
/// Base component for pages that render values formatted through <see cref="AppState"/>.
/// Re-renders the component whenever privacy mode or the display currency is toggled,
/// so the numbers on screen follow the app-bar buttons without a page reload.
/// </summary>
public abstract class AppStateComponentBase : ComponentBase, IDisposable
{
    [Inject] protected AppState AppState { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        AppState.OnChange += OnAppStateChanged;
    }

    private void OnAppStateChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose()
    {
        AppState.OnChange -= OnAppStateChanged;
    }
}
