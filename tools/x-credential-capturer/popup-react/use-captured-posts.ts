import { useMemo, useRef, useState } from "react";

import type {
  BackgroundMessage,
  CapturedPostsStore,
  CapturedPostsMessageResponse,
  UploadNotificationsStore,
  UploadNotificationsMessageResponse,
  UploadCapturedPostsMessageResponse
} from "../popup/models.js";
import type { OpenUrlOptions } from "./types.js";
import {
  buildCapturedPostRows,
  buildUploadNotificationRows,
  extractLastTcoUrlFromDescription,
  formatUploadSummary,
  normalizeCaptureHashtag,
  splitCaptureHashtagDraft
} from "./captured-posts-helpers.js";

type UseCapturedPostsInput = {
  sendMessage: <T>(message: unknown) => Promise<T>;
  openUrlWithBypassCache: (url: string, options?: OpenUrlOptions) => Promise<void>;
  sortOrder: "latest-added" | "oldest-added" | "last-seen";
};

export function useCapturedPosts(input: UseCapturedPostsInput) {
  const [capturedPostsStore, setCapturedPostsStore] = useState<CapturedPostsStore | null>(null);
  const [selectedCapturedPostIds, setSelectedCapturedPostIds] = useState<string[]>([]);
  const [capturedPostsSearchQuery, setCapturedPostsSearchQuery] = useState("");
  const [captureHashtagDraft, setCaptureHashtagDraft] = useState("");
  const [isUploadingCapturedPosts, setIsUploadingCapturedPosts] = useState(false);
  const [uploadStatus, setUploadStatus] = useState("");
  const [uploadNotificationsStore, setUploadNotificationsStore] =
    useState<UploadNotificationsStore | null>(null);

  const capturedPostsStoreRef = useRef<CapturedPostsStore | null>(capturedPostsStore);
  const captureHashtagDraftRef = useRef(captureHashtagDraft);

  function applyCapturedPostsStore(nextStore: CapturedPostsStore | null) {
    capturedPostsStoreRef.current = nextStore;
    setCapturedPostsStore(nextStore);

    if (!nextStore) {
      setSelectedCapturedPostIds([]);
      return;
    }

    const itemById = nextStore.items || {};

    setSelectedCapturedPostIds((previous) =>
      previous.filter((id) => {
        const item = itemById[id];
        return Boolean(item && !item.uploadedAt);
      })
    );
  }

  function applyUploadNotificationsStore(nextStore: UploadNotificationsStore | null) {
    setUploadNotificationsStore(nextStore);
  }

  function setCaptureHashtagDraftState(value: string) {
    captureHashtagDraftRef.current = value;
    setCaptureHashtagDraft(value);
  }

  async function loadCapturedPosts() {
    const response = await input.sendMessage<CapturedPostsMessageResponse>({
      type: "getCapturedPosts"
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not load captured posts");
    }

    applyCapturedPostsStore(response.store);
  }

  async function loadUploadNotifications() {
    const response = await input.sendMessage<UploadNotificationsMessageResponse>({
      type: "getUploadNotifications"
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not load upload notifications");
    }

    applyUploadNotificationsStore(response.store);
  }

  async function updateUploadTarget(inputValue: {
    apiBaseUrl?: string;
    uploadUserId?: string;
    uploadOrigin?: string;
    captureHashtags?: string[];
  }) {
    const response = await input.sendMessage<CapturedPostsMessageResponse>({
      type: "setUploadTarget",
      ...inputValue
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not update upload target");
    }

    applyCapturedPostsStore(response.store);
  }

  async function uploadSelectedCapturedPosts() {
    const selectedIds = [...selectedCapturedPostIds];

    if (selectedIds.length === 0) {
      setUploadStatus("Select at least one pending post.");
      return;
    }

    setIsUploadingCapturedPosts(true);
    setUploadStatus("");

    try {
      const response = await input.sendMessage<UploadCapturedPostsMessageResponse>({
        type: "uploadCapturedPosts",
        ids: selectedIds
      } satisfies BackgroundMessage);

      if (!response?.ok) {
        throw new Error(response?.error || "Upload failed");
      }

      applyCapturedPostsStore(response.store);
      setSelectedCapturedPostIds((previous) =>
        previous.filter((id) => !response.uploaded.includes(id))
      );
      setUploadStatus(formatUploadSummary(response));
    } catch (error) {
      const message = error instanceof Error ? error.message : "Upload failed";
      setUploadStatus(message);
    } finally {
      setIsUploadingCapturedPosts(false);
    }
  }

  async function clearUploadedCapturedPosts() {
    const response = await input.sendMessage<CapturedPostsMessageResponse>({
      type: "clearUploadedCapturedPosts"
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not clear uploaded posts");
    }

    applyCapturedPostsStore(response.store);
    setUploadStatus("Uploaded posts cleared.");
  }

  async function resetCapturedPostsUploadStatus() {
    const response = await input.sendMessage<CapturedPostsMessageResponse>({
      type: "resetCapturedPostsUploadStatus"
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not reset upload status");
    }

    applyCapturedPostsStore(response.store);
    setUploadStatus("All posts were marked as pending.");
  }

  async function clearUploadNotifications() {
    const response = await input.sendMessage<UploadNotificationsMessageResponse>({
      type: "clearUploadNotifications"
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not clear upload notifications");
    }

    applyUploadNotificationsStore(response.store);
    setUploadStatus("Upload notifications cleared.");
  }

  async function exportCapturedPosts() {
    const store = capturedPostsStoreRef.current;

    if (!store) {
      setUploadStatus("Nothing to export.");
      return;
    }

    const payload = {
      exportedAt: new Date().toISOString(),
      version: 1,
      ...store
    };

    const blob = new Blob([JSON.stringify(payload, null, 2)], {
      type: "application/json"
    });

    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `xcc-captured-posts-${new Date().toISOString().replace(/[:.]/g, "-")}.json`;
    anchor.click();
    URL.revokeObjectURL(url);
    setUploadStatus("Export complete.");
  }

  async function importCapturedPostsFromFile(file: File) {
    const text = await file.text();
    let payload: unknown;

    try {
      payload = JSON.parse(text);
    } catch (_error) {
      throw new Error("Invalid JSON file.");
    }

    const response = await input.sendMessage<CapturedPostsMessageResponse>({
      type: "importCapturedPosts",
      payload
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Import failed");
    }

    applyCapturedPostsStore(response.store);
    setUploadStatus("Import complete.");
  }

  async function addCaptureHashtagFromDraft() {
    const draftTokens = splitCaptureHashtagDraft(captureHashtagDraftRef.current);

    if (draftTokens.length === 0) {
      setCaptureHashtagDraftState("");
      return;
    }

    const current = capturedPostsStoreRef.current?.captureHashtags || [];
    const next = [...new Set([...current, ...draftTokens])];
    await updateUploadTarget({ captureHashtags: next });
    setCaptureHashtagDraftState("");
  }

  async function removeCaptureHashtag(value: string) {
    const normalized = normalizeCaptureHashtag(value);

    if (!normalized) {
      return;
    }

    const current = capturedPostsStoreRef.current?.captureHashtags || [];
    const next = current.filter((entry) => entry !== normalized);
    await updateUploadTarget({ captureHashtags: next });
  }

  async function openCaptureHashtag(value: string) {
    const hashtag = normalizeCaptureHashtag(value);

    if (!hashtag) {
      return;
    }

    const url = `https://x.com/hashtag/${encodeURIComponent(hashtag)}?f=media`;
    await input.openUrlWithBypassCache(url, { active: true, bypassCache: false });
  }

  async function openCaptureHashtagInWindow(value: string) {
    const hashtag = normalizeCaptureHashtag(value);

    if (!hashtag) {
      return;
    }

    const url = `https://x.com/hashtag/${encodeURIComponent(hashtag)}?f=media`;
    await chrome.windows.create({
      url,
      focused: true,
      type: "normal"
    });
  }

  async function openCapturedPostExternalUrl(capturedPostId: string) {
    const item = capturedPostsStoreRef.current?.items?.[capturedPostId];

    if (!item) {
      return;
    }

    const link =
      extractLastTcoUrlFromDescription(item.processed?.description) ||
      extractLastTcoUrlFromDescription(item.text);

    if (!link) {
      return;
    }

    await input.openUrlWithBypassCache(link, { active: true, bypassCache: false });
  }

  function toggleCapturedPostSelection(id: string, checked: boolean) {
    setSelectedCapturedPostIds((previous) => {
      const exists = previous.includes(id);

      if (checked && !exists) {
        return [...previous, id];
      }

      if (!checked && exists) {
        return previous.filter((entry) => entry !== id);
      }

      return previous;
    });
  }

  function selectAllPendingCapturedPosts() {
    const pendingIds = Object.values(capturedPostsStore?.items || {})
      .filter((item) => !item.uploadedAt)
      .map((item) => item.id);

    setSelectedCapturedPostIds(pendingIds);
  }

  function clearCapturedPostSelection() {
    setSelectedCapturedPostIds([]);
  }

  const capturedPostRows = useMemo(
    () =>
      buildCapturedPostRows({
        items: capturedPostsStore?.items,
        searchQuery: capturedPostsSearchQuery,
        selectedIds: selectedCapturedPostIds,
        sortOrder: input.sortOrder
      }),
    [capturedPostsSearchQuery, capturedPostsStore, input.sortOrder, selectedCapturedPostIds]
  );

  const uploadNotifications = useMemo(
    () => buildUploadNotificationRows(uploadNotificationsStore?.items),
    [uploadNotificationsStore]
  );

  const runningUploadNotificationsCount = useMemo(
    () => uploadNotifications.filter((entry) => entry.item.status === "running").length,
    [uploadNotifications]
  );

  return {
    capturedPostsStore,
    selectedCapturedPostIds,
    capturedPostsSearchQuery,
    captureHashtagDraft,
    isUploadingCapturedPosts,
    uploadStatus,
    uploadNotifications,
    runningUploadNotificationsCount,
    capturedPostRows,
    captureHashtags: capturedPostsStore?.captureHashtags || [],
    applyCapturedPostsStore,
    applyUploadNotificationsStore,
    loadCapturedPosts,
    loadUploadNotifications,
    updateUploadTarget,
    uploadSelectedCapturedPosts,
    clearUploadedCapturedPosts,
    resetCapturedPostsUploadStatus,
    clearUploadNotifications,
    exportCapturedPosts,
    importCapturedPostsFromFile,
    addCaptureHashtagFromDraft,
    removeCaptureHashtag,
    openCaptureHashtag,
    openCaptureHashtagInWindow,
    openCapturedPostExternalUrl,
    toggleCapturedPostSelection,
    selectAllPendingCapturedPosts,
    clearCapturedPostSelection,
    setCapturedPostsSearchQuery,
    setCaptureHashtagDraft: setCaptureHashtagDraftState
  };
}
