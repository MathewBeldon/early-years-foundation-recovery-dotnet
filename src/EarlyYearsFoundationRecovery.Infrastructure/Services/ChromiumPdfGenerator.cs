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
        CertificatePdfContent certificate,
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

        var recipient = WebUtility.HtmlEncode(certificate.RecipientName);
        var module = WebUtility.HtmlEncode(certificate.ModuleTitle);
        var criteria = certificate.CriteriaHtml;
        var completionDate = certificate.CompletedAt is DateTime completedAt
            ? WebUtility.HtmlEncode(completedAt.ToString("d MMMM yyyy"))
            : string.Empty;
        var completionDateMarkup = certificate.CompletedAt is not null
            ? $"<p id=\"certificate-date\">Date completed: {completionDate}</p>"
            : string.Empty;
        var completionCopy = certificate.IsCompleted
            ? "has completed the following module:"
            : string.Empty;
        var html = $$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <style>
              @page { size: A4 landscape; margin: 18mm; }
              body { font-family: Arial, sans-serif; color: #0b0c0c; border: 10px solid #1d70b8; padding: 55px; text-align: center; }
              h1 { font-size: 42px; margin: 0 0 45px; } h2 { font-size: 30px; } p { font-size: 24px; } strong { font-size: 30px; } ul { text-align: left; font-size: 20px; }
            </style></head><body>
            <h1>Certificate of achievement</h1>
            {{(certificate.IsCompleted ? $"<h2>{recipient}</h2><p>{completionCopy}</p>" : "<h2>Your name will appear here</h2>")}}
            <h2>{{module}}</h2>
            {{completionDateMarkup}}
            <p>This module has covered:</p>
            {{criteria}}
            <p>Certified by the Department for Education</p>
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
