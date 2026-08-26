# Пакування для Linux / macOS

Ця тека містить шаблони для пакування Avalonia-версії.

## Linux (`linux/`)

- `ite-resource-calculator.desktop` — ярлик для меню застосунків (XDG Desktop Entry).
  При пакуванні копіюється в архів поруч із бінарником або в `/usr/share/applications/` для .deb.

**Ручне встановлення з tar.gz:**
```bash
tar -xzf ITE.ResourceCalculator-linux-x64.tar.gz
chmod +x ITE.ResourceCalculator
./ITE.ResourceCalculator
# опційно: встановити ярлик
cp ite-resource-calculator.desktop ~/.local/share/applications/
```

## macOS (`macos/`)

- `Info.plist` — шаблон бандлу `.app` (плейсхолдер `__APP_VERSION__` підставляє `release.ps1`).

**Структура бандлу:**
```
ITE.ResourceCalculator.app/
  Contents/
    Info.plist
    MacOS/ITE.ResourceCalculator  (бінарник, chmod +x)
    Resources/AppIcon.icns        (опційно, конвертується з icon.ico)
```

`release.ps1` збирає бандл автоматично та пакує в `ITE.ResourceCalculator-macos-*.zip`.

## Іконки

- Windows: `ResourceCalculator/icon.ico` (256x256, використовується і для Avalonia win-версії).
- Linux/macOS: `icon.ico` конвертується при потребі (`magick icon.ico AppIcon.png` / `iconutil` для icns).
  Поки icns не згенеровано — бандл працює без іконки (дефолтна).
