namespace WebView2Utilities.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
