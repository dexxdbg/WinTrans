# WinTrans (WinUI 3)

Глобальный переводчик через API Claude. По нажатию **Ctrl+Shift+T**:

- Если в любом окне есть выделенный текст — программа копирует его (Ctrl+C),
  переводит через Claude API, и **автоматически вставляет перевод на место выделения** (Ctrl+V).
- Если выделения нет — открывается окно, где можно ввести текст руками.

Можно выбирать:
- целевой язык (рус/англ/укр/нем/фр/исп/ит/кит/яп/кор/польск/тур и т.д.);
- стиль перевода (нейтральный, формальный, разговорный, технический,
  литературный, юридический, маркетинговый, сленг).

API-ключ и выбор языка/стиля сохраняются в
`%APPDATA%\WinTrans\settings.json`.

## Требования

- Windows 10 1809+ / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Windows App SDK runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads) (если запуск unpackaged)

## Сборка

```powershell
cd WinTrans
dotnet restore
dotnet build -c Release
```

## Запуск

```powershell
dotnet run -c Release --project WinTrans
```

Или собрать exe:

```powershell
dotnet publish WinTrans -c Release -r win-x64 --self-contained false
```

## Использование

1. При первом запуске окно можно вызвать хоткеем **Ctrl+Shift+T**.
2. Вставьте свой API-ключ Claude (получить: https://console.anthropic.com/).
3. Нажмите «Сохранить ключ».
4. Выберите язык и стиль.
5. Дальше:
   - выделите текст где угодно → **Ctrl+Shift+T** → перевод сам заменит выделение;
   - либо нажмите **Ctrl+Shift+T** без выделения, введите текст, нажмите «Перевести».

## Смена хоткея

В `MainWindow.xaml.cs`:

```csharp
// MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8
private const uint MOD_ALT     = 0x0001;
private const uint MOD_CONTROL = 0x0002;
private const uint VK_T        = 0x54;   // любой Virtual-Key Code
// ...
_hotkeyManager.Register(MOD_CONTROL | MOD_ALT, VK_T);
```

## Смена модели Claude

В `Services/ClaudeApiClient.cs`:

```csharp
private const string Model = "claude-sonnet-4-5";
```

Можно поменять на `claude-opus-4-5`, `claude-haiku-4-5` и т.п.
