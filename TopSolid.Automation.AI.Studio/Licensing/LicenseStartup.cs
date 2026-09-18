using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Licensing;

/// <summary>The main window factory is invoked only after a fresh, affirmative vendor check.</summary>
internal static class LicenseStartup
{
    internal static async Task<bool> RunAsync(Func<CancellationToken, Task<TopSolidLicenseStatus>> check,
        Action<TopSolidLicenseStatus> launch, Func<TopSolidLicenseStatus, Task> showFailure, CancellationToken cancellationToken)
    {
        TopSolidLicenseStatus status;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try { status = await check(deadline.Token); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        catch { status = new TopSolidLicenseStatus(); } // An unsupported server, timeout or failed query cannot grant access.
        if (cancellationToken.IsCancellationRequested) return false;
        if (status.CanStart) { launch(status); return true; }
        await showFailure(status);
        return false;
    }
}
