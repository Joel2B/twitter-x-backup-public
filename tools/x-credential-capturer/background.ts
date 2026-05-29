import type {
  BackgroundMessage,
  CaptureState,
  CapturedPostsMessageResponse,
  UploadNotificationsMessageResponse,
  StateMessageResponse,
  UploadCapturedPostsMessageResponse
} from "./popup/models.js";
import { captureFromRequest, captureFromResponse, rollbackEndpoint } from "./background/capture.js";
import { GRAPHQL_FILTER } from "./background/constants.js";
import {
  capturePostsFromGraphqlResponseBody,
  clearUploadedCapturedPosts,
  clearUploadNotifications,
  getCapturedPostsStore,
  getUploadNotificationsStore,
  importCapturedPosts,
  resetCapturedPostsUploadStatus,
  setUploadTarget,
  uploadCapturedPosts
} from "./background/post-capture.js";
import { enqueueUpdate } from "./background/queue.js";
import { defaultState, ensureStateInitialized, getState, saveState } from "./background/state.js";
import { buildCookieHeader, getCookieValue, isPlainObject } from "./background/utils.js";

function asBackgroundMessage(message: unknown): BackgroundMessage | null {
  if (!isPlainObject(message) || typeof message.type !== "string") {
    return null;
  }

  switch (message.type) {
    case "getState":
    case "clearState":
    case "refreshCookies":
      return { type: message.type };
    case "setState":
      return {
        type: "setState",
        state: (message.state as CaptureState | undefined) || undefined
      };
    case "rollbackEndpoint":
      return {
        type: "rollbackEndpoint",
        endpointId: typeof message.endpointId === "string" ? message.endpointId : undefined
      };
    case "getCapturedPosts":
      return { type: "getCapturedPosts" };
    case "setUploadTarget":
      return {
        type: "setUploadTarget",
        apiBaseUrl: typeof message.apiBaseUrl === "string" ? message.apiBaseUrl : undefined,
        uploadUserId: typeof message.uploadUserId === "string" ? message.uploadUserId : undefined,
        uploadOrigin: typeof message.uploadOrigin === "string" ? message.uploadOrigin : undefined,
        captureHashtags: Array.isArray(message.captureHashtags)
          ? message.captureHashtags.filter((entry) => typeof entry === "string")
          : undefined
      };
    case "uploadCapturedPosts":
      return {
        type: "uploadCapturedPosts",
        ids: Array.isArray(message.ids) ? message.ids.filter((id) => typeof id === "string") : []
      };
    case "clearUploadedCapturedPosts":
      return { type: "clearUploadedCapturedPosts" };
    case "resetCapturedPostsUploadStatus":
      return { type: "resetCapturedPostsUploadStatus" };
    case "getUploadNotifications":
      return { type: "getUploadNotifications" };
    case "clearUploadNotifications":
      return { type: "clearUploadNotifications" };
    case "importCapturedPosts":
      return {
        type: "importCapturedPosts",
        payload: message.payload
      };
    case "captureGraphqlResponseBody":
      return {
        type: "captureGraphqlResponseBody",
        url: typeof message.url === "string" ? message.url : undefined,
        status: typeof message.status === "number" ? message.status : undefined,
        body: typeof message.body === "string" ? message.body : undefined,
        capturedAt: typeof message.capturedAt === "string" ? message.capturedAt : undefined
      };
    default:
      return null;
  }
}

chrome.webRequest.onBeforeSendHeaders.addListener(
  (details) => {
    void enqueueUpdate(async () => {
      await captureFromRequest(details);
    });

    return undefined;
  },
  GRAPHQL_FILTER,
  ["requestHeaders", "extraHeaders"]
);

chrome.webRequest.onCompleted.addListener(
  (details) => {
    void enqueueUpdate(async () => {
      await captureFromResponse(details);
    });
  },
  GRAPHQL_FILTER,
  ["responseHeaders", "extraHeaders"]
);

chrome.runtime.onInstalled.addListener(() => {
  void ensureStateInitialized();
});

chrome.runtime.onStartup.addListener(() => {
  void ensureStateInitialized();
});

chrome.runtime.onMessage.addListener((message: unknown, _sender, sendResponse) => {
  const parsed = asBackgroundMessage(message);

  if (!parsed) {
    return false;
  }

  if (parsed.type === "getState") {
    getState()
      .then((state) => sendResponse({ ok: true, state } satisfies StateMessageResponse))
      .catch((error: unknown) =>
        sendResponse({
          ok: false,
          error: error instanceof Error ? error.message : "getState failed"
        })
      );
    return true;
  }

  if (parsed.type === "clearState") {
    const state = defaultState();
    saveState(state)
      .then(() => sendResponse({ ok: true, state } satisfies StateMessageResponse))
      .catch((error: unknown) =>
        sendResponse({
          ok: false,
          error: error instanceof Error ? error.message : "clearState failed"
        })
      );
    return true;
  }

  if (parsed.type === "setState") {
    enqueueUpdate(async () => {
      const state = parsed.state || defaultState();
      await saveState(state);
      const normalized = await getState();
      sendResponse({ ok: true, state: normalized } satisfies StateMessageResponse);
    }).catch((error: unknown) => {
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "setState failed"
      });
    });

    return true;
  }

  if (parsed.type === "refreshCookies") {
    enqueueUpdate(async () => {
      const state = await getState();
      const cookie = await buildCookieHeader();

      if (cookie) {
        state.global.cookie = cookie;

        const ct0 = getCookieValue(cookie, "ct0");

        if (ct0) {
          state.global.xCsrfToken = ct0;
        }
      }

      await saveState(state);
      sendResponse({ ok: true, state } satisfies StateMessageResponse);
    }).catch((error: unknown) => {
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "refreshCookies failed"
      });
    });

    return true;
  }

  if (parsed.type === "rollbackEndpoint") {
    enqueueUpdate(async () => {
      const result = await rollbackEndpoint(parsed.endpointId || "");
      sendResponse(result);
    }).catch((error: unknown) => {
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "rollbackEndpoint failed"
      });
    });

    return true;
  }

  if (parsed.type === "getCapturedPosts") {
    getCapturedPostsStore()
      .then((store) => sendResponse({ ok: true, store } satisfies CapturedPostsMessageResponse))
      .catch((error: unknown) =>
        sendResponse({
          ok: false,
          error: error instanceof Error ? error.message : "getCapturedPosts failed"
        } satisfies CapturedPostsMessageResponse)
      );

    return true;
  }

  if (parsed.type === "setUploadTarget") {
    enqueueUpdate(async () => {
      const store = await setUploadTarget({
        apiBaseUrl: parsed.apiBaseUrl,
        uploadUserId: parsed.uploadUserId,
        uploadOrigin: parsed.uploadOrigin,
        captureHashtags: parsed.captureHashtags
      });
      sendResponse({ ok: true, store } satisfies CapturedPostsMessageResponse);
    }).catch((error: unknown) => {
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "setUploadTarget failed"
      } satisfies CapturedPostsMessageResponse);
    });

    return true;
  }

  if (parsed.type === "uploadCapturedPosts") {
    enqueueUpdate(async () => {
      const result = await uploadCapturedPosts(parsed.ids || []);
      sendResponse(result satisfies UploadCapturedPostsMessageResponse);
    }).catch((error: unknown) => {
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "uploadCapturedPosts failed"
      } satisfies UploadCapturedPostsMessageResponse);
    });

    return true;
  }

  if (parsed.type === "clearUploadedCapturedPosts") {
    enqueueUpdate(async () => {
      const store = await clearUploadedCapturedPosts();
      sendResponse({ ok: true, store } satisfies CapturedPostsMessageResponse);
    }).catch((error: unknown) => {
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "clearUploadedCapturedPosts failed"
      } satisfies CapturedPostsMessageResponse);
    });

    return true;
  }

  if (parsed.type === "resetCapturedPostsUploadStatus") {
    enqueueUpdate(async () => {
      const store = await resetCapturedPostsUploadStatus();
      sendResponse({ ok: true, store } satisfies CapturedPostsMessageResponse);
    }).catch((error: unknown) => {
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "resetCapturedPostsUploadStatus failed"
      } satisfies CapturedPostsMessageResponse);
    });

    return true;
  }

  if (parsed.type === "importCapturedPosts") {
    enqueueUpdate(async () => {
      const store = await importCapturedPosts(parsed.payload);
      sendResponse({ ok: true, store } satisfies CapturedPostsMessageResponse);
    }).catch((error: unknown) => {
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "importCapturedPosts failed"
      } satisfies CapturedPostsMessageResponse);
    });

    return true;
  }

  if (parsed.type === "getUploadNotifications") {
    getUploadNotificationsStore()
      .then((store) => sendResponse({ ok: true, store } satisfies UploadNotificationsMessageResponse))
      .catch((error: unknown) =>
        sendResponse({
          ok: false,
          error: error instanceof Error ? error.message : "getUploadNotifications failed"
        } satisfies UploadNotificationsMessageResponse)
      );

    return true;
  }

  if (parsed.type === "clearUploadNotifications") {
    enqueueUpdate(async () => {
      const store = await clearUploadNotifications();
      sendResponse({ ok: true, store } satisfies UploadNotificationsMessageResponse);
    }).catch((error: unknown) => {
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "clearUploadNotifications failed"
      } satisfies UploadNotificationsMessageResponse);
    });

    return true;
  }

  if (parsed.type === "captureGraphqlResponseBody") {
    if (!parsed.url || !parsed.body) {
      sendResponse({ ok: false, error: "missing payload" });
      return false;
    }

    enqueueUpdate(async () => {
      console.info("[XCC] background received response body:", parsed.url, parsed.status);
      await capturePostsFromGraphqlResponseBody({
        url: parsed.url as string,
        status: parsed.status,
        body: parsed.body as string,
        capturedAt: parsed.capturedAt
      });
      sendResponse({ ok: true });
    }).catch((error: unknown) => {
      sendResponse({
        ok: false,
        error: error instanceof Error ? error.message : "captureGraphqlResponseBody failed"
      });
    });

    return true;
  }

  return false;
});
