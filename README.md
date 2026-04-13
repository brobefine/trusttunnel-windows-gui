# TrustTunnel GUI (Windows, WinUI 3)

GUI-враппер для официального CLI-клиента [TrustTunnel](https://github.com/TrustTunnel/TrustTunnelClient).

## Архитектура

GUI **не** реализует сам протокол TrustTunnel. Сетевую работу делает официальный
`trusttunnel_client.exe` (C++/Rust). Это приложение:

- управляет профилями серверов (UI вместо ручной правки TOML),
- генерирует `trusttunnel_client.toml` для каждого профиля,
- запускает/останавливает CLI-процесс, ловит stdout/stderr в реальном времени,
- умеет ставить/удалять Windows-сервис через `--service-install` / `--service-uninstall`,
- хранит профили в `%LOCALAPPDATA%\TrustTunnelGui\profiles.json`.

Тот же подход, что у NekoBox (sing-box) или Hiddify (xray) — единственный вменяемый
путь для GUI поверх сложного протокола, который уже хорошо реализован в нативном бинаре.

## Возможности

Все функции CLI-клиента доступны через UI:

- **Endpoint:** hostname, addresses, IPv6, креды, TLS-сертификат (PEM / skip-verify),
  транспорт http2/http3 + fallback, anti-DPI, client_random.
- **Killswitch:** вкл/выкл, разрешённые порты.
- **Криптография:** post-quantum key exchange.
- **DNS:** список апстримов (DoT/DoH/DoQ).
- **Split tunneling:** список исключений (домены/CIDR/IP/wildcard).
- **TUN-листенер:** включение, bound interface, included/excluded routes.
- **SOCKS5-листенер:** адрес, креды.
- **Логи:** real-time в окне, сохранение в файл, очистка, автоскролл.
- **Импорт/экспорт TOML.**
- **Windows-сервис:** установка с активным профилем, удаление.

## Установка

1. Скачай ZIP из [Releases](../../releases) (`x64` или `arm64`).
2. Распакуй в любую папку.
3. Скачай `trusttunnel_client.exe` из [официальных релизов](https://github.com/TrustTunnel/TrustTunnelClient/releases),
   положи рядом с `TrustTunnelGui.exe`.
4. `wintun.dll` уже включён в ZIP.
5. Запусти `TrustTunnelGui.exe` (от админа — нужно для TUN и установки сервиса).

Если бинаря нет, GUI скажет где его ждёт.

## Сборка локально

Нужно: Visual Studio 2022 + workload "Windows App SDK C# Templates", .NET 8 SDK.

```cmd
cd src
dotnet restore -r win-x64
dotnet publish TrustTunnelGui.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64
```

Результат в `src\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\`.

## Сборка через GitHub Actions

Push в `main`/`master` или ручной dispatch — собирает x64 и arm64, заливает артефакты.
Push тега `v*` — собирает + создаёт GitHub Release с ZIP-файлами.

## Структура проекта

```
src/
  TrustTunnelGui.csproj
  app.manifest                 — requireAdministrator
  App.xaml(.cs)                — singleton-сервисы
  MainWindow.xaml(.cs)         — NavigationView shell
  Models/
    ServerProfile.cs           — все поля TOML, ObservableObject
  Services/
    ConfigService.cs           — генерация TOML (Tomlyn)
    TomlImport.cs              — импорт чужих конфигов
    ProfileStore.cs            — JSON-хранилище профилей
    BinaryManager.cs           — поиск trusttunnel_client.exe / wintun.dll
    TrustTunnelService.cs      — Process, логи, сервис install/uninstall
    Ui.cs                      — DispatcherQueue helper
  Views/
    ConnectionPage             — выбор активного, connect/disconnect, тейл логов
    ServersPage                — список профилей (CRUD, импорт)
    ServerEditPage             — форма со всеми полями
    LogsPage                   — полный лог + сохранение
    SettingsPage               — пути, сервис, инфо
.github/workflows/build.yml    — matrix x64/arm64, publish + release
```

## Известные нюансы

- Точный флаг для удаления сервиса - `--service-uninstall` (по аналогии с `--service-install`).
  Если в актуальной версии CLI он называется иначе (`--service-remove` и т.п.) -
  поправь в `Services/TrustTunnelService.cs::UninstallServiceAsync`.
- Для иконок (`Assets/`) добавь свои PNG до первой публикации, иначе MSBuild ругнётся
  warnings (но соберёт). См. `src/Assets/README.txt`.
- `WindowsPackageType=None` - приложение распространяется как папка, а не MSIX.
  Если нужен MSIX - отдельный publish profile с `<WindowsPackageType>MSIX</WindowsPackageType>`.

## Лицензия

MIT (для GUI). Бинарь `trusttunnel_client.exe` — Apache 2.0 от AdGuard.
