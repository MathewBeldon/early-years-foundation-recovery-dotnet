using System.Net;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class ChromiumPdfGenerator : IPdfGenerator, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<byte[]> GenerateCertificateAsync(
        string moduleName,
        string recipientName,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _playwright ??= await Playwright.CreateAsync();
            _browser ??= await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        }
        catch (PlaywrightException ex)
        {
            throw new InvalidOperationException(
                "Chromium is not installed. Run the generated playwright install script documented in README.md.", ex);
        }
        finally
        {
            _gate.Release();
        }

        var recipient = WebUtility.HtmlEncode(recipientName);
        var module = WebUtility.HtmlEncode(moduleName);
        var html = $$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <style>
              @page { size: A4 landscape; margin: 18mm; }
              body { font-family: Arial, sans-serif; color: #0b0c0c; border: 10px solid #1d70b8; padding: 55px; text-align: center; }
              h1 { font-size: 42px; margin: 0 0 45px; } p { font-size: 24px; } strong { font-size: 30px; }
            </style></head><body>
            <h1>Certificate of achievement</h1>
            <p>This certificate is awarded to</p><p><strong>{{recipient}}</strong></p>
            <p>for completing</p><p><strong>{{module}}</strong></p>
            <p>Early years child development training</p>
            </body></html>
            """;

        var page = await _browser!.NewPageAsync();
        try
        {
            await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.Load });
            return await page.PdfAsync(new() { Format = "A4", Landscape = true, PrintBackground = true });
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        _gate.Dispose();
    }
}
