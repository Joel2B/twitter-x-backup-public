import type { BackgroundMessage, CaptureState, StateMessageResponse } from "./popup/models.js";
import { captureFromRequest, captureFromResponse, rollbackEndpoint } from "./background/capture.js";
import { GRAPHQL_FILTER } from "./background/constants.js";
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

  return false;
});
