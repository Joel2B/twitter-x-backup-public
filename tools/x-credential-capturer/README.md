# X Credential Capturer (MVP)

Local Chrome MV3 extension to capture X GraphQL requests and generate a JSON patch in `Api` format.

## Includes

- Captures requests from `https://x.com/i/api/graphql/*`.
- Per-endpoint checklist.
- `Open` button per endpoint to navigate to the page that triggers that API.
- Configurable `X username` field in popup for user-specific Open links (posts/likes/media).
- Auto-detects username from intercepted referers and can auto-fill when empty.
- Username is persisted in `chrome.storage.local` (`captureSettingsV1`).
- Profile system with isolated snapshots (`Default`, custom profiles, switch/delete).
- `Test all` button for batch endpoint verification.
- Per-endpoint freshness chip (`Fresh`, `Aging`, `Stale`) based on recency and test result.
- Endpoint capture history with `Undo` rollback.
- Sensitive guard toggle masks secrets in preview and blocks copy while enabled.
- `Copy` button per endpoint to copy only that API (when complete).
- `Test` button per endpoint to validate whether the stored request still works.
- Automatically captures rate limit headers when responses are intercepted.
- `Test` also refreshes and displays those rate values.
- Captures `Query.Variables`, `Query.Features`, and `Query.FieldToggles` when present.
- Every endpoint is exported independently with its full `Request` (no dependency on `Fetch.Current`).
- Shows endpoint `MissingFields` only in the UI (not exported).
- Patch export includes only complete endpoints.
- Button to copy a JSON `Api` patch.
- Temporary storage in `chrome.storage.session`.

## Native CSS Modules

- `popup.css` is the entrypoint via `@import`.
- `styles/base.css` contains variables and a basic reset.
- `styles/layout.css` defines the general structure and layout blocks.
- `styles/table.css` contains checklist/table styles.
- `styles/components.css` groups buttons, status badges, and reusable components.
- `styles/output.css` contains JSON preview styles.

## TypeScript Source and JS Build

- Source lives in `.ts/.tsx` modules (`background.ts`, `background/*.ts`, `popup-react/*.tsx`, `popup/*.ts`).
- Popup UI is implemented in React (`popup-react/App.tsx` + `popup-react/main.tsx`).
- Build output is generated into `dist/` as `.js` files for Chrome MV3.
- `manifest.json` uses module service worker mode (`background.type = "module"`).
- `popup.html` loads `popup.js` as `type="module"` (from `dist/` after build).

## Build

1. Run:
   - `npm install`
2. Build extension output:
   - `npm run build`
3. Optional format:
   - `npm run format`

## Endpoints and Trigger Pages

- `Api.posts (UserTweets)` -> `https://x.com/{username}`
- `Api.likes (Likes)` -> `https://x.com/{username}/likes`
- `Api.bookmarks (Bookmarks)` -> `https://x.com/i/bookmarks`
- `Api.UserMedia` -> `https://x.com/{username}/media`
- `Api.UserByScreenName` -> `https://x.com/{username}/media`
- `Api.TweetDetail` -> `https://x.com/AmeDollVT/status/2047352297571725658`
- `Api.UsersByRestIds` -> currently unused (no button)

## How to Load

1. Open `chrome://extensions`.
2. Enable `Developer mode`.
3. Click `Load unpacked`.
4. Select this folder:
   - `tools/x-credential-capturer/dist`

## Recommended Flow

1. Open the extension popup.
2. Select or create the profile you want to use.
3. Set your `X username` in the popup (auto-detected when possible, saved automatically).
4. Use `Open` buttons to visit each target page.
5. Check `Status`, freshness, and `Missing` columns per endpoint.
6. Use `Test` or `Test all` to validate complete endpoints.
7. Disable `Sensitive guard` when you are ready to copy real credentials.
8. Click `Copy Api patch`.
9. Paste that JSON into your config update flow.

## Important Note

The `Authorization` header is not always visible via `webRequest` in MV3/Chromium.
If it appears as `null` in `Request.Headers.authorization`, fill it manually.
