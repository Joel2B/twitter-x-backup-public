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
  getCapturedPostsItemsMapByIds,
  getCapturedPostsMetadata,
  getCapturedPostsStore,
  getUploadNotificationsStore,
  importCapturedPosts,
  markCapturedPostsUploaded,
  normalizeApiBaseUrl,
  normalizeCaptureHashtags,
  parseUploadApiResponse,
  patchUploadNotification,
  resetCapturedPostsUploadStatus,
  upsertCapturedPosts,
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

    return;
  }

  const operationName = operation as string;

  if (!payload.body || payload.body.length === 0) {
    console.info("[XCC] empty response body for operation:", operation);
    return;
  }

  const parsedItems = parseCapturedPosts(operationName, payload.body);
  const meta = await getCapturedPostsMetadata();

  if (parsedItems.length === 0) {
    console.info("[XCC] parsed 0 posts from operation:", operationName, payload.url);
    return;
  }

  const captureHashtags = new Set(normalizeCaptureHashtags(meta.captureHashtags));
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
    return;
  }

  const existingItems = await getCapturedPostsItemsMapByIds(filteredItems.map((item) => item.id));
  const mergedItems = [];
  const seenAt =
    typeof payload.capturedAt === "string" && payload.capturedAt ? payload.capturedAt : nowIso();

  for (const parsed of filteredItems) {
    const existing = existingItems[parsed.id];

    mergedItems.push({
      ...parsed,
      capturedAt: existing?.capturedAt || parsed.capturedAt,
      lastSeenAt: seenAt,
      uploadedAt: existing?.uploadedAt || null
    });
  }

  const autoUserId = meta.uploadUserId || filteredItems[0]?.authorId || "";
  console.info(
    "[XCC] parsed posts (matched):",
    filteredItems.length,
    "of",
    parsedItems.length,
    "operation:",
    operationName,
    "new items in batch:",
    mergedItems.filter((item) => !existingItems[item.id]).length
  );

  await upsertCapturedPosts(mergedItems, { uploadUserId: autoUserId });
}

export async function uploadCapturedPosts(
  ids: string[]
): Promise<UploadCapturedPostsMessageResponse> {
  const meta = await getCapturedPostsMetadata();
  const uniqueIds = [...new Set(ids.filter((id) => typeof id === "string" && id.trim()))];
  const itemMap = await getCapturedPostsItemsMapByIds(uniqueIds);
  const candidates = uniqueIds
    .map((id) => itemMap[id])
    .filter((item) => Boolean(item && !item.uploadedAt));

  if (candidates.length === 0) {
    const store = await getCapturedPostsStore();
    return { ok: true, store, uploaded: [], failed: [], uploadSummary: null };
  }

  if (!meta.uploadUserId) {
    return { ok: false, error: "Upload userId is required." };
  }

  const endpointUrl =
    `${normalizeApiBaseUrl(meta.apiBaseUrl)}/api/v1/posts/processed` +
    `?userId=${encodeURIComponent(meta.uploadUserId)}` +
    `&origin=${encodeURIComponent(meta.uploadOrigin)}`;
  const notification = await createUploadNotification({
    attemptedPosts: candidates.length,
    apiBaseUrl: meta.apiBaseUrl,
    uploadUserId: meta.uploadUserId,
    uploadOrigin: meta.uploadOrigin
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
  await markCapturedPostsUploaded(
    candidates.map((item) => item.id),
    uploadedAt
  );
  const savedStore = await getCapturedPostsStore();
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
