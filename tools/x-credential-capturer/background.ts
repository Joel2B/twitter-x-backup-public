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

type SendResponse = (response: unknown) => void;

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

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

function sendAsyncResponse<T>(
  sendResponse: SendResponse,
  work: () => Promise<T>,
  toErrorResponse: (message: string) => unknown,
  toSuccessResponse: (value: T) => unknown
): void {
  work()
    .then((value) => {
      sendResponse(toSuccessResponse(value));
    })
    .catch((error: unknown) => {
      sendResponse(toErrorResponse(getErrorMessage(error, "request failed")));
    });
}

function sendQueuedResponse<T>(
  sendResponse: SendResponse,
  work: () => Promise<T>,
  toErrorResponse: (message: string) => unknown,
  toSuccessResponse: (value: T) => unknown
): void {
  sendAsyncResponse(sendResponse, () => enqueueUpdate(work), toErrorResponse, toSuccessResponse);
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

  switch (parsed.type) {
    case "getState":
      sendAsyncResponse(
        sendResponse,
        () => getState(),
        (message) => ({ ok: false, error: message }),
        (state) => ({ ok: true, state }) satisfies StateMessageResponse
      );
      return true;
    case "clearState":
      sendAsyncResponse(
        sendResponse,
        async () => {
          const state = defaultState();
          await saveState(state);
          return state;
        },
        (message) => ({ ok: false, error: message }),
        (state) => ({ ok: true, state }) satisfies StateMessageResponse
      );
      return true;
    case "setState":
      sendQueuedResponse(
        sendResponse,
        async () => {
          const state = parsed.state || defaultState();
          await saveState(state);
          return getState();
        },
        (message) => ({ ok: false, error: message }),
        (state) => ({ ok: true, state }) satisfies StateMessageResponse
      );
      return true;
    case "refreshCookies":
      sendQueuedResponse(
        sendResponse,
        async () => {
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
          return state;
        },
        (message) => ({ ok: false, error: message }),
        (state) => ({ ok: true, state }) satisfies StateMessageResponse
      );
      return true;
    case "rollbackEndpoint":
      sendQueuedResponse(
        sendResponse,
        () => rollbackEndpoint(parsed.endpointId || ""),
        (message) => ({ ok: false, error: message }),
        (result) => result
      );
      return true;
    case "getCapturedPosts":
      sendAsyncResponse(
        sendResponse,
        () => getCapturedPostsStore(),
        (message) => ({ ok: false, error: message }) satisfies CapturedPostsMessageResponse,
        (store) => ({ ok: true, store }) satisfies CapturedPostsMessageResponse
      );
      return true;
    case "setUploadTarget":
      sendQueuedResponse(
        sendResponse,
        () =>
          setUploadTarget({
            apiBaseUrl: parsed.apiBaseUrl,
            uploadUserId: parsed.uploadUserId,
            uploadOrigin: parsed.uploadOrigin,
            captureHashtags: parsed.captureHashtags
          }),
        (message) => ({ ok: false, error: message }) satisfies CapturedPostsMessageResponse,
        (store) => ({ ok: true, store }) satisfies CapturedPostsMessageResponse
      );
      return true;
    case "uploadCapturedPosts":
      sendQueuedResponse(
        sendResponse,
        () => uploadCapturedPosts(parsed.ids || []),
        (message) => ({ ok: false, error: message }) satisfies UploadCapturedPostsMessageResponse,
        (result) => result satisfies UploadCapturedPostsMessageResponse
      );
      return true;
    case "clearUploadedCapturedPosts":
      sendQueuedResponse(
        sendResponse,
        () => clearUploadedCapturedPosts(),
        (message) => ({ ok: false, error: message }) satisfies CapturedPostsMessageResponse,
        (store) => ({ ok: true, store }) satisfies CapturedPostsMessageResponse
      );
      return true;
    case "resetCapturedPostsUploadStatus":
      sendQueuedResponse(
        sendResponse,
        () => resetCapturedPostsUploadStatus(),
        (message) => ({ ok: false, error: message }) satisfies CapturedPostsMessageResponse,
        (store) => ({ ok: true, store }) satisfies CapturedPostsMessageResponse
      );
      return true;
    case "importCapturedPosts":
      sendQueuedResponse(
        sendResponse,
        () => importCapturedPosts(parsed.payload),
        (message) => ({ ok: false, error: message }) satisfies CapturedPostsMessageResponse,
        (store) => ({ ok: true, store }) satisfies CapturedPostsMessageResponse
      );
      return true;
    case "getUploadNotifications":
      sendAsyncResponse(
        sendResponse,
        () => getUploadNotificationsStore(),
        (message) => ({ ok: false, error: message }) satisfies UploadNotificationsMessageResponse,
        (store) => ({ ok: true, store }) satisfies UploadNotificationsMessageResponse
      );
      return true;
    case "clearUploadNotifications":
      sendQueuedResponse(
        sendResponse,
        () => clearUploadNotifications(),
        (message) => ({ ok: false, error: message }) satisfies UploadNotificationsMessageResponse,
        (store) => ({ ok: true, store }) satisfies UploadNotificationsMessageResponse
      );
      return true;
    case "captureGraphqlResponseBody":
      if (!parsed.url || !parsed.body) {
        sendResponse({ ok: false, error: "missing payload" });
        return false;
      }

      {
        const url = parsed.url;
        const body = parsed.body;
        const status = parsed.status;
        const capturedAt = parsed.capturedAt;

        sendQueuedResponse(
          sendResponse,
          async () => {
            console.info("[XCC] background received response body:", url, status);
            await capturePostsFromGraphqlResponseBody({
              url,
              status,
              body,
              capturedAt
            });
            return { ok: true };
          },
          (message) => ({ ok: false, error: message }),
          (result) => result
        );
      }
      return true;
    default:
      return false;
  }
});
