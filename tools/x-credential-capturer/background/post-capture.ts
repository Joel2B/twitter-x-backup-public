import type {
  CaptureResponseBodyPayload,
  UploadCapturedPostsMessageResponse
} from "./post-capture-types.js";
import { parseOperation, nowIso } from "./utils.js";
import {
  buildUploadSummary,
  clearUploadNotifications,
  clearUploadedCapturedPosts,
  createUploadNotification,
  getCapturedPostsStore,
  getUploadNotificationsStore,
  importCapturedPosts,
  normalizeApiBaseUrl,
  normalizeCaptureHashtags,
  parseUploadApiResponse,
  patchUploadNotification,
  resetCapturedPostsUploadStatus,
  saveCapturedPostsStore,
  setUploadTarget
} from "./post-capture-storage.js";
import { postMatchesCaptureHashtags, parseCapturedPosts } from "./post-capture-parser.js";
import { UPLOAD_REQUEST_TIMEOUT_MS } from "./constants.js";

function isSupportedOperation(operation: string | null): boolean {
  if (!operation) {
    return false;
  }

  return operation.toLowerCase().includes("searchtimeline");
}

export {
  clearUploadNotifications,
  clearUploadedCapturedPosts,
  getCapturedPostsStore,
  getUploadNotificationsStore,
  importCapturedPosts,
  resetCapturedPostsUploadStatus,
  setUploadTarget
};

export async function capturePostsFromGraphqlResponseBody(payload: CaptureResponseBodyPayload) {
  const operation = parseOperation(payload.url);

  if (!isSupportedOperation(operation)) {
    if (operation) {
      console.info("[XCC] skip operation:", operation);
    }

    return getCapturedPostsStore();
  }

  const operationName = operation as string;

  if (!payload.body || payload.body.length === 0) {
    console.info("[XCC] empty response body for operation:", operation);
    return getCapturedPostsStore();
  }

  const parsedItems = parseCapturedPosts(operationName, payload.body);
  const store = await getCapturedPostsStore();

  if (parsedItems.length === 0) {
    console.info("[XCC] parsed 0 posts from operation:", operationName, payload.url);
    return store;
  }

  const captureHashtags = new Set(normalizeCaptureHashtags(store.captureHashtags));
  const filteredItems = parsedItems.filter((item) =>
    postMatchesCaptureHashtags(item, captureHashtags)
  );

  if (filteredItems.length === 0) {
    console.info(
      "[XCC] parsed posts ignored by hashtag filter:",
      parsedItems.length,
      "operation:",
      operationName
    );
    return store;
  }

  const nextItems = { ...store.items };
  const seenAt =
    typeof payload.capturedAt === "string" && payload.capturedAt ? payload.capturedAt : nowIso();

  for (const parsed of filteredItems) {
    const existing = nextItems[parsed.id];

    nextItems[parsed.id] = {
      ...parsed,
      capturedAt: existing?.capturedAt || parsed.capturedAt,
      lastSeenAt: seenAt,
      uploadedAt: existing?.uploadedAt || null
    };
  }

  const autoUserId = store.uploadUserId || filteredItems[0]?.authorId || "";
  console.info(
    "[XCC] parsed posts (matched):",
    filteredItems.length,
    "of",
    parsedItems.length,
    "operation:",
    operationName,
    "store total after merge:",
    Object.keys(nextItems).length
  );

  return saveCapturedPostsStore({
    ...store,
    uploadUserId: autoUserId,
    items: nextItems
  });
}

export async function uploadCapturedPosts(
  ids: string[]
): Promise<UploadCapturedPostsMessageResponse> {
  const store = await getCapturedPostsStore();
  const uniqueIds = [...new Set(ids.filter((id) => typeof id === "string" && id.trim()))];
  const candidates = uniqueIds
    .map((id) => store.items[id])
    .filter((item) => Boolean(item && !item.uploadedAt));

  if (candidates.length === 0) {
    return { ok: true, store, uploaded: [], failed: [], uploadSummary: null };
  }

  if (!store.uploadUserId) {
    return { ok: false, error: "Upload userId is required." };
  }

  const endpointUrl =
    `${normalizeApiBaseUrl(store.apiBaseUrl)}/api/posts/processed` +
    `?userId=${encodeURIComponent(store.uploadUserId)}` +
    `&origin=${encodeURIComponent(store.uploadOrigin)}`;
  const notification = await createUploadNotification({
    attemptedPosts: candidates.length,
    apiBaseUrl: store.apiBaseUrl,
    uploadUserId: store.uploadUserId,
    uploadOrigin: store.uploadOrigin
  });
  const startedAtEpoch = Date.now();
  const timeoutMessage = `Upload expired after ${UPLOAD_REQUEST_TIMEOUT_MS} ms waiting for API response.`;
  const abortController = new AbortController();
  let requestTimedOut = false;
  const timeoutId = setTimeout(() => {
    requestTimedOut = true;
    abortController.abort();
  }, UPLOAD_REQUEST_TIMEOUT_MS);

  let response: Response;

  try {
    response = await fetch(endpointUrl, {
      method: "POST",
      headers: {
        "content-type": "application/json"
      },
      body: JSON.stringify(candidates.map((item) => item.processed)),
      signal: abortController.signal
    });
  } catch (error) {
    clearTimeout(timeoutId);

    if (requestTimedOut || (error instanceof Error && error.name === "AbortError")) {
      await patchUploadNotification(notification.id, {
        status: "expired",
        completedAt: nowIso(),
        uploadedPosts: 0,
        failedPosts: candidates.length,
        error: timeoutMessage
      });

      return {
        ok: false,
        error: timeoutMessage
      };
    }

    await patchUploadNotification(notification.id, {
      status: "failed",
      completedAt: nowIso(),
      uploadedPosts: 0,
      failedPosts: candidates.length,
      error: error instanceof Error ? error.message : "Upload request failed."
    });

    return {
      ok: false,
      error: error instanceof Error ? error.message : "Upload request failed."
    };
  }

  clearTimeout(timeoutId);

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    const message = text?.trim() || `HTTP ${response.status}`;
    await patchUploadNotification(notification.id, {
      status: "failed",
      completedAt: nowIso(),
      uploadedPosts: 0,
      failedPosts: candidates.length,
      error: `Upload failed: ${message}`
    });
    return { ok: false, error: `Upload failed: ${message}` };
  }

  const apiPayload = await response.json().catch(() => null);
  const apiResult = parseUploadApiResponse(apiPayload);
  const uploadSummary = buildUploadSummary(candidates.length, apiResult);
  const uploadedAt = nowIso();
  const nextStore = {
    ...store,
    items: { ...store.items }
  };

  for (const item of candidates) {
    const current = nextStore.items[item.id];

    if (!current) {
      continue;
    }

    nextStore.items[item.id] = {
      ...current,
      uploadedAt
    };
  }

  const savedStore = await saveCapturedPostsStore(nextStore);
  const finishedAt = nowIso();
  const durationMs = Date.now() - startedAtEpoch;
  const finalSummary = uploadSummary
    ? {
        ...uploadSummary,
        durationMs: uploadSummary.durationMs ?? durationMs
      }
    : null;

  await patchUploadNotification(notification.id, {
    status: "completed",
    completedAt: finishedAt,
    uploadedPosts: candidates.length,
    failedPosts: 0,
    uploadSummary: finalSummary,
    error: null
  });

  return {
    ok: true,
    store: savedStore,
    uploaded: candidates.map((item) => item.id),
    failed: [],
    uploadSummary
  };
}
