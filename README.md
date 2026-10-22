# Quick AI Assistant (Windows / C# / .NET Framework 4.7.2)

A starter WinForms project that opens a floating prompt with `Ctrl+Alt+Space`, sends captured clipboard text or image to Groq, and lets you insert the response into the previous app.

## Important behavior and limitations
- The app uses `Ctrl+C` to try to copy the current selection when the hotkey is pressed. This works in many editors and browsers, but not every application supports it.
- Image requests use a vision-capable model. The current Groq model is text-only; change `model` in `GroqClient.cs` to a currently available Groq vision model before expecting image questions to work. Check Groq's model catalog for current model names.
- API calls require internet and a Groq API key. Text capture, clipboard access, and insertion are local; the selected content is sent to Groq when you click **Ask Groq**.
- Response insertion uses the clipboard, replacing its current contents. Save anything important on the clipboard first.
- Windows startup is configured per user via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- The API key is protected with Windows DPAPI (`ProtectedData`, CurrentUser). This protects it at rest for the current Windows account, but it is not a substitute for securing the Windows account.
- Do not send confidential data to any cloud API unless you are allowed to do so.

## Prerequisites
- Windows 10/11
- Visual Studio 2022/2026 with the **.NET desktop development** workload
- .NET Framework 4.7.2 Developer Pack / targeting pack
- A Groq API key from https://console.groq.com/keys

## Build
1. Extract the ZIP.
2. Open `QuickAIAssistant.csproj` in Visual Studio.
3. Select Debug or Release and build.
4. Run `bin\Debug\QuickAIAssistant.exe`.
5. Right-click the tray icon or open the window and select **Settings**.
6. Paste your Groq API key and enable Windows startup if desired.
7. Select text in another app and press `Ctrl+Alt+Space`.
8. Edit the prompt and click **Ask Groq**. Review the result and click **Insert response**.

## Model configuration
`GroqClient.cs` currently uses `llama-3.3-70b-versatile` for text. Provider model availability can change. If Groq rejects this model, replace it with a currently supported text model from the Groq Console/model catalog.

For image input, configure a currently supported vision model in `GroqClient.cs`. A text-only model will not understand image content.

## Safety
This project does not silently auto-insert AI responses. You must click **Insert response**. The app also does not automatically upload content merely because the hotkey was pressed; you must click **Ask Groq**.
