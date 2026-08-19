#!/bin/sh
set -eu

mode="${1:-browser}"
revision="${2:-1228}"
expected_version="${3:-149.0.7827.55}"

if [ "$(id -u)" -eq 0 ]; then
  echo "Parity .NET container must run as a non-root user." >&2
  exit 1
fi

chromium="${PLAYWRIGHT_BROWSERS_PATH:-/ms-playwright}/chromium-${revision}/chrome-linux64/chrome"
headless_shell="${PLAYWRIGHT_BROWSERS_PATH:-/ms-playwright}/chromium_headless_shell-${revision}/chrome-headless-shell-linux64/chrome-headless-shell"

for executable in "$chromium" "$headless_shell"; do
  if [ ! -x "$executable" ]; then
    echo "Missing executable Playwright browser payload: $executable" >&2
    exit 1
  fi

  observed_version="$($executable --version)"
  case "$observed_version" in
    *"$expected_version"*) ;;
    *)
      echo "Unexpected Playwright browser version: $observed_version" >&2
      exit 1
      ;;
  esac
done

if [ "$mode" = "ready" ]; then
  curl --fail --silent --show-error http://127.0.0.1:5000/health >/dev/null
fi

