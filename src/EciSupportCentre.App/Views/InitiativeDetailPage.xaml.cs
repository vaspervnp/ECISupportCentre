using EciSupportCentre.App.ViewModels;

namespace EciSupportCentre.App.Views;

/// <summary>Details on a phone. Opened with the Shell route <c>initiative?id=…</c>.</summary>
public partial class InitiativeDetailPage : ContentPage, IQueryAttributable
{
    private readonly InitiativeDetailViewModel _viewModel;

    public InitiativeDetailPage(InitiativeDetailViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _viewModel.ApplyQueryAttributes(query);
}
