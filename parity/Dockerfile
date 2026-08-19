FROM mcr.microsoft.com/dotnet/sdk:10.0-noble

ARG PLAYWRIGHT_VERSION=1.61.0
ARG PLAYWRIGHT_CHROMIUM_REVISION=1228
ARG PLAYWRIGHT_CHROMIUM_VERSION=149.0.7827.55

ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright \
    HOME=/home/app

WORKDIR /opt/parity-playwright
COPY PlaywrightInstaller/ ./

# Playwright owns the distro dependency list. Installing through its pinned
# package at image-build time makes both native libraries and browser payload
# immutable at container runtime.
RUN test "$(sed -n 's/.*Microsoft.Playwright.*Version="\([^"]*\)".*/\1/p' PlaywrightInstaller.csproj)" = "$PLAYWRIGHT_VERSION" \
    && dotnet run --configuration Release -- install --with-deps chromium \
    && test -x "/ms-playwright/chromium-${PLAYWRIGHT_CHROMIUM_REVISION}/chrome-linux64/chrome" \
    && test -x "/ms-playwright/chromium_headless_shell-${PLAYWRIGHT_CHROMIUM_REVISION}/chrome-headless-shell-linux64/chrome-headless-shell" \
    && rm -rf /opt/parity-playwright

COPY parity-container-smoke.sh /usr/local/bin/parity-container-smoke
RUN chmod 0755 /usr/local/bin/parity-container-smoke \
    && chown -R app:app /home/app \
    && command -v curl >/dev/null

WORKDIR /workspace
USER app

# Prove the pinned payload is executable as the same non-root runtime user.
RUN parity-container-smoke browser "$PLAYWRIGHT_CHROMIUM_REVISION" "$PLAYWRIGHT_CHROMIUM_VERSION"
