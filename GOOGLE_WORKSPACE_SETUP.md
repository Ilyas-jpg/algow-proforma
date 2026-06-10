# Google Workspace / Gmail API Setup

This project is a .NET 8 WPF desktop app, not a browser SaaS app. The Google flow is therefore implemented as a browser-based OAuth loopback flow:

- App opens the user's browser.
- Google redirects to `http://127.0.0.1:8777/...`.
- The WPF app listens locally, validates `state`, exchanges the code, and stores Gmail tokens with Windows DPAPI.

SMTP remains available as fallback.

## Google Cloud Setup

1. Open Google Cloud Console.
2. Create or select a project for Algow Proforma.
3. Go to APIs & Services > Library.
4. Enable Gmail API.
5. Go to APIs & Services > OAuth consent screen.
6. Configure app name, support email, developer contact email.
7. Add scopes:
   - `openid`
   - `email`
   - `profile`
   - `https://www.googleapis.com/auth/gmail.send`
8. Go to APIs & Services > Credentials.
9. Create an OAuth client.
10. For this WPF MVP, use a Desktop app client if possible. If Google requires redirect URI registration for the loopback callback, use a Web application client and add the redirect URIs below.

## Redirect URIs

Default local redirect URIs:

- Google login: `http://127.0.0.1:8777/google-auth/callback/`
- Gmail connect: `http://127.0.0.1:8777/gmail/callback/`

The trailing slash matters. Keep the value in Google Cloud and the app settings exactly the same.

## Required Environment Variables

The app can read credentials from the Settings window or from environment variables. Environment variables win over Settings values.

```powershell
$env:GOOGLE_CLIENT_ID="your-client-id"
$env:GOOGLE_CLIENT_SECRET="your-client-secret"
$env:GOOGLE_AUTH_REDIRECT_URI="http://127.0.0.1:8777/google-auth/callback/"
$env:GOOGLE_GMAIL_REDIRECT_URI="http://127.0.0.1:8777/gmail/callback/"
$env:GOOGLE_OAUTH_ALLOWED_DOMAINS="your-domain.com"
$env:APP_URL="http://127.0.0.1:8777"
```

`GOOGLE_OAUTH_ALLOWED_DOMAINS` is optional. Use comma-separated domains if needed.

Existing SMTP settings are unchanged and still live in the app's Settings screen. The SMTP password is stored with DPAPI in `smtp.bin`.

## Local Development

1. Run the app from Visual Studio or:

```powershell
dotnet run --project .\AyTeknikKatalog\AyTeknikKatalog.csproj
```

2. Open Ayarlar.
3. Fill Google Client ID/Secret or set the env vars above.
4. Click `Continue with Google`.
5. Click `Connect Gmail / Workspace`.
6. Create or open a proforma quote with a recipient email.
7. Click `E-posta Onizle`.
8. Review recipient, subject, editable body, and attachment.
9. Click `Send with Gmail`.

## Data Storage

This desktop prototype does not have a relational database or migrations. The equivalent persisted fields are:

- `settings.json`: Google identity metadata and non-secret OAuth settings.
- `google-gmail.bin`: DPAPI-encrypted Gmail credential containing `google_sub`, `google_email`, access token, refresh token, expiry, granted scopes, and timestamps.
- `email-send-log.json`: send attempts and result status.

No Gmail access or refresh token is exposed to the UI or stored in plain JSON.

## Gmail Permission Model

Google sign-in and Gmail sending are separate buttons/flows.

- Sign-in flow requests only `openid email profile`.
- Gmail connection requests identity scopes plus the minimal Gmail sending scope: `https://www.googleapis.com/auth/gmail.send`.
- The app does not request Gmail read, modify, mailbox, Drive, or Docs scopes.
- The app sends directly with `users.messages.send`.
- It does not create drafts and does not read inbox contents.

Assumption: the Gmail connect flow includes identity scopes so the app can show the connected Google email. This does not grant mailbox read access.

## Production Notes

For a future SaaS/web version:

- Move OAuth callbacks to server routes.
- Store tokens in a server-side database with encryption at rest.
- Use per-user sessions and CSRF protection from the web framework.
- Keep Google login and Gmail connect as separate consent steps.
- Keep `gmail.send` as the only Gmail scope.

## Manual Test Checklist

- Google login opens the browser and returns to the app.
- OAuth callback rejects invalid `state`.
- Gmail connect stores a refresh token; if Google does not return one, disconnect/revoke the old grant and connect again.
- Settings shows connected Gmail email.
- Disconnect Gmail removes local DPAPI credentials and tries to revoke the token.
- Expired access token refreshes before sending.
- Proforma PDF is generated before preview.
- Preview shows company, contact, email, subject, editable body, and attachment filename.
- `Send with Gmail` sends a PDF attachment and writes `email-send-log.json`.
- With Gmail disconnected and SMTP password present, `Send with SMTP` still works.
- Gmail API errors show a user-readable message without logging tokens.

## Common Errors

- `GOOGLE_CLIENT_ID eksik`: Client ID is missing in env vars and Settings.
- `GOOGLE_CLIENT_SECRET eksik`: Client secret is missing in env vars and Settings.
- `redirect_uri_mismatch`: Google Cloud redirect URI does not exactly match the app setting, including trailing slash.
- `Google refresh token vermedi`: Google already granted consent before. Revoke app access from the Google account security page or use Disconnect Gmail, then connect again.
- `Bu Google domainine izin yok`: the account email domain is not in `GOOGLE_OAUTH_ALLOWED_DOMAINS`.
- Gmail send 403: Gmail API is disabled, scope was not granted, or the Workspace admin blocked the app.
