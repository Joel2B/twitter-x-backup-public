import { useEffect, useRef, useState } from "react";

import {
  CAPTURED_POSTS_STORAGE_KEY,
  CAPTURE_STATE_STORAGE_KEY,
  DEFAULT_PROFILE_ID,
  ENDPOINTS,
  PROFILES_STORAGE_KEY,
  SETTINGS_STORAGE_KEY,
  UPLOAD_NOTIFICATIONS_STORAGE_KEY
} from "../popup/constants.js";
import { formatDate } from "../popup/format.js";
import { createPatch, maskPatchSensitiveData } from "../popup/model.js";
import type {
  ApiPatch,
  BackgroundMessage,
  CaptureState,
  CapturedPostsStore,
  EndpointDefinition,
  EndpointModel,
  UploadNotificationsStore,
  RollbackMessageResponse,
  StateMessageResponse
} from "../popup/models.js";
import {
  detectUsernameFromState,
  getEmptyStateSnapshot,
  getFallbackStateSnapshot
} from "../popup/helpers.js";
import { getHashtagHint, getProfileHint, getSensitiveHint, getUsernameHint } from "./view-model.js";
import type { ApplyStateOptions, OpenUrlOptions, UseCredentialCapturerResult } from "./types.js";
import { useCapturedPosts } from "./use-captured-posts.js";
import { useEndpointTests } from "./use-endpoint-tests.js";
import { usePopupSettings } from "./use-popup-settings.js";
import { useProfiles } from "./use-profiles.js";

export function useCredentialCapturer(): UseCredentialCapturerResult {
  const [captureState, setCaptureState] = useState<CaptureState | null>(null);
  const [copyPatchLabel, setCopyPatchLabel] = useState("Copy Api patch");
  const [patchOutput, setPatchOutput] = useState("");
  const [currentRawPatch, setCurrentRawPatch] = useState<ApiPatch | null>(null);

  const captureStateRef = useRef<CaptureState | null>(captureState);
  const scheduleActiveProfileSyncRef = useRef<() => void>(() => {});

  useEffect(() => {
    captureStateRef.current = captureState;
  }, [captureState]);

  function reportError(error: unknown) {
    const message = error instanceof Error ? error.message : String(error);
    setPatchOutput((previous) => `ERROR: ${message}\n\n${previous || ""}`);
  }

  function runAsync(action: () => Promise<unknown>): void {
    void action().catch(reportError);
  }

  async function sendMessage<T>(message: unknown): Promise<T> {
    return chrome.runtime.sendMessage(message) as Promise<T>;
  }

  async function setRemoteState(nextState: CaptureState): Promise<CaptureState> {
    const response = await sendMessage<StateMessageResponse>({
      type: "setState",
      state: nextState
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not set state");
    }

    return response.state;
  }

  const {
    applySettings,
    detectedUsername,
    hashtagDraft,
    persistHashtagFromInput,
    persistUsernameFromInput,
    saveSettings,
    scheduleHashtagPersist,
    scheduleUsernamePersist,
    settings,
    settingsRef,
    setHashtagDraft,
    setDetectedUsername,
    setUsernameDraft,
    usernameDraft
  } = usePopupSettings({
    onScheduleActiveProfileSync: () => scheduleActiveProfileSyncRef.current()
  });

  async function maybeAutoDetectUsername(state: CaptureState) {
    const detected = detectUsernameFromState(state) || "";
    setDetectedUsername(detected);

    if (!settingsRef.current.username && detected && !isApplyingProfileRef.current) {
      await saveSettings({ username: detected });
    }
  }

  function applyState(nextState: CaptureState | null, options: ApplyStateOptions = {}) {
    if (!nextState) {
      return;
    }

    const resetTests = Boolean(options.resetTests);
    captureStateRef.current = nextState;
    setCaptureState(nextState);

    if (resetTests) {
      clearAllTestRuntime();
    }

    runAsync(() => maybeAutoDetectUsername(nextState));
  }

  async function loadState() {
    const response = await sendMessage<StateMessageResponse>({
      type: "getState"
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not load state");
    }

    applyState(response.state, { resetTests: true });
  }

  async function loadSettings() {
    const data = await chrome.storage.local.get(SETTINGS_STORAGE_KEY);
    const stored = data?.[SETTINGS_STORAGE_KEY] || {};
    applySettings(stored, { syncInput: true, scheduleSync: false });
  }

  const {
    activateProfile,
    canDeleteProfile,
    createProfile,
    deleteSelectedProfile,
    handleProfilesStorageChange,
    isApplyingProfile,
    isApplyingProfileRef,
    loadProfiles,
    profiles,
    profilesStore,
    scheduleActiveProfileSync,
    selectedProfile
  } = useProfiles({
    applySettings,
    applyState,
    captureStateRef,
    getEmptyStateSnapshot,
    getFallbackStateSnapshot,
    settingsRef,
    setRemoteState
  });

  scheduleActiveProfileSyncRef.current = scheduleActiveProfileSync;

  function ensureCopyAllowed() {
    if (settingsRef.current.maskSensitive) {
      throw new Error("Sensitive guard is enabled. Disable it to copy real credentials.");
    }
  }

  async function copyPatch() {
    ensureCopyAllowed();

    const rawPatch = currentRawPatch || createPatch(captureStateRef.current);
    const text = JSON.stringify(rawPatch, null, 2);
    await navigator.clipboard.writeText(text);
    setCopyPatchLabel("Copied");
    setTimeout(() => {
      setCopyPatchLabel("Copy Api patch");
    }, 1500);
  }

  async function openUrlWithBypassCache(url: string, options: OpenUrlOptions = {}) {
    const active = options.active !== false;
    const bypassCache = options.bypassCache === true;
    const tab = await chrome.tabs.create({ url, active });

    if (!bypassCache || typeof tab?.id !== "number") {
      return;
    }

    await new Promise<void>((resolve) => {
      let settled = false;
      let timeoutId: ReturnType<typeof setTimeout> | null = setTimeout(() => {
        cleanup();
      }, 4000);

      function cleanup() {
        if (settled) {
          return;
        }

        settled = true;

        if (timeoutId) {
          clearTimeout(timeoutId);
          timeoutId = null;
        }

        chrome.tabs.onUpdated.removeListener(handleUpdated);
        resolve();
      }

      function handleUpdated(
        tabId: number,
        changeInfo: chrome.tabs.OnUpdatedInfo,
        updatedTab: chrome.tabs.Tab
      ) {
        if (tabId !== tab.id || changeInfo.status !== "complete") {
          return;
        }

        if (!updatedTab.url || updatedTab.url === "about:blank") {
          return;
        }

        chrome.tabs.reload(tab.id as number, { bypassCache: true }, () => {
          cleanup();
        });
      }

      chrome.tabs.onUpdated.addListener(handleUpdated);
    });
  }

  const {
    capturedPostsStore,
    selectedCapturedPostIds,
    capturedPostsSearchQuery,
    captureHashtagDraft,
    isUploadingCapturedPosts,
    uploadStatus,
    uploadNotifications,
    runningUploadNotificationsCount,
    capturedPostRows,
    captureHashtags,
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
    setCaptureHashtagDraft
  } = useCapturedPosts({
    sendMessage,
    openUrlWithBypassCache,
    sortOrder: settings.capturedPostsSort
  });

  const {
    clearAllTestRuntime,
    copyEndpoint,
    endpointRows,
    globalStatusOk,
    isBulkTesting,
    runAllTests,
    runSingleTest,
    setTestAllStatus,
    testAllStatus
  } = useEndpointTests({
    captureState,
    endpoints: ENDPOINTS,
    settings
  });

  async function clearState() {
    const response = await sendMessage<StateMessageResponse>({
      type: "clearState"
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not clear state");
    }

    applyState(response.state, { resetTests: true });
  }

  async function refreshCookies() {
    const response = await sendMessage<StateMessageResponse>({
      type: "refreshCookies"
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not refresh cookies");
    }

    applyState(response.state, { resetTests: false });
  }

  async function rollbackEndpointCapture(endpointId: string) {
    const response = await sendMessage<RollbackMessageResponse>({
      type: "rollbackEndpoint",
      endpointId
    } satisfies BackgroundMessage);

    if (!response?.ok) {
      throw new Error(response?.error || "Could not rollback endpoint");
    }

    applyState(response.state, { resetTests: false });
    const when = response?.restoredFrom ? ` from ${formatDate(response.restoredFrom)}` : "";
    setTestAllStatus(`Rolled back ${endpointId}${when}`);
  }

  useEffect(() => {
    function handleStorageChanged(
      changes: Record<string, chrome.storage.StorageChange>,
      areaName: chrome.storage.AreaName
    ) {
      if (areaName === "session") {
        const stateChange = changes?.[CAPTURE_STATE_STORAGE_KEY];

        if (!stateChange || typeof stateChange.newValue === "undefined") {
          return;
        }

        applyState(stateChange.newValue as CaptureState, { resetTests: false });
        scheduleActiveProfileSync();
        return;
      }

      if (areaName !== "local") {
        return;
      }

      const settingsChange = changes?.[SETTINGS_STORAGE_KEY];

      if (settingsChange && typeof settingsChange.newValue !== "undefined") {
        applySettings(settingsChange.newValue, { syncInput: true, scheduleSync: true });
      }

      const profilesChange = changes?.[PROFILES_STORAGE_KEY];

      if (profilesChange && typeof profilesChange.newValue !== "undefined") {
        runAsync(() => handleProfilesStorageChange(profilesChange.newValue));
      }

      const capturedPostsChange = changes?.[CAPTURED_POSTS_STORAGE_KEY];

      if (capturedPostsChange && typeof capturedPostsChange.newValue !== "undefined") {
        applyCapturedPostsStore(capturedPostsChange.newValue as CapturedPostsStore);
      }

      const uploadNotificationsChange = changes?.[UPLOAD_NOTIFICATIONS_STORAGE_KEY];

      if (uploadNotificationsChange && typeof uploadNotificationsChange.newValue !== "undefined") {
        applyUploadNotificationsStore(
          uploadNotificationsChange.newValue as UploadNotificationsStore
        );
      }
    }

    chrome.storage.onChanged.addListener(handleStorageChanged);

    return () => {
      chrome.storage.onChanged.removeListener(handleStorageChanged);
    };
  }, []);

  useEffect(() => {
    runAsync(async () => {
      await loadState();
      await loadSettings();
      await loadProfiles();
      await loadCapturedPosts();
      await loadUploadNotifications();
    });
  }, []);

  useEffect(() => {
    if (!captureState) {
      setCurrentRawPatch(null);
      setPatchOutput("");
      return;
    }

    const rawPatch = createPatch(captureState);
    setCurrentRawPatch(rawPatch);

    const previewPatch = settings.maskSensitive ? maskPatchSensitiveData(rawPatch) : rawPatch;
    setPatchOutput(JSON.stringify(previewPatch, null, 2));
  }, [captureState, settings.maskSensitive]);

  const profileHint = getProfileHint(selectedProfile);
  const sensitiveHint = getSensitiveHint(settings.maskSensitive);
  const usernameHint = getUsernameHint(settings, detectedUsername);
  const hashtagHint = getHashtagHint(settings);

  return {
    activeProfileId: profilesStore?.activeProfileId || DEFAULT_PROFILE_ID,
    canDeleteProfile,
    copyPatchLabel,
    endpointRows,
    capturedPostRows,
    uploadNotifications,
    runningUploadNotificationsCount,
    capturedPostsStore,
    globalStatusOk,
    isApplyingProfile,
    isBulkTesting,
    isUploadingCapturedPosts,
    patchOutput,
    profileHint,
    profiles,
    sensitiveHint,
    settings,
    selectedCapturedPostIds,
    capturedPostsSearchQuery,
    captureHashtagDraft,
    captureHashtags,
    testAllStatus,
    uploadStatus,
    hashtagDraft,
    hashtagHint,
    usernameDraft,
    usernameHint,
    onClearState: () => {
      runAsync(clearState);
    },
    onCopyEndpoint: (model: EndpointModel, endpointId: string) => {
      runAsync(() => copyEndpoint(model, endpointId));
    },
    onCopyPatch: () => {
      runAsync(copyPatch);
    },
    onCreateProfile: () => {
      runAsync(createProfile);
    },
    onDeleteProfile: () => {
      runAsync(deleteSelectedProfile);
    },
    onMaskSensitiveChange: (checked: boolean) => {
      runAsync(() => saveSettings({ maskSensitive: checked }));
    },
    onCapturedPostsViewChange: (value: "list" | "grid") => {
      runAsync(() => saveSettings({ capturedPostsView: value }));
    },
    onCapturedPostsGridColumnsChange: (value: number) => {
      runAsync(() => saveSettings({ capturedPostsGridColumns: value }));
    },
    onCapturedPostsShowThumbnailChange: (value: boolean) => {
      runAsync(() => saveSettings({ capturedPostsShowThumbnail: value }));
    },
    onCapturedPostsSortChange: (value: "latest-added" | "oldest-added" | "last-seen") => {
      runAsync(() => saveSettings({ capturedPostsSort: value }));
    },
    onOpenEndpointUrl: (url: string, openInBackground: boolean) => {
      runAsync(() => openUrlWithBypassCache(url, { active: !openInBackground, bypassCache: true }));
    },
    onProfileChange: (profileId: string) => {
      runAsync(() => activateProfile(profileId, { persistStore: true, resetTests: true }));
    },
    onRefreshCookies: () => {
      runAsync(refreshCookies);
    },
    onRollbackEndpoint: (endpointId: string) => {
      runAsync(() => rollbackEndpointCapture(endpointId));
    },
    onRunAllTests: () => {
      runAsync(runAllTests);
    },
    onToggleCapturedPost: (id: string, checked: boolean) => {
      toggleCapturedPostSelection(id, checked);
    },
    onSelectAllPendingCapturedPosts: () => {
      selectAllPendingCapturedPosts();
    },
    onClearCapturedPostSelection: () => {
      clearCapturedPostSelection();
    },
    onUploadSelectedCapturedPosts: () => {
      runAsync(uploadSelectedCapturedPosts);
    },
    onClearUploadedCapturedPosts: () => {
      runAsync(clearUploadedCapturedPosts);
    },
    onResetCapturedPostsUploadStatus: () => {
      runAsync(resetCapturedPostsUploadStatus);
    },
    onClearUploadNotifications: () => {
      runAsync(clearUploadNotifications);
    },
    onExportCapturedPosts: () => {
      runAsync(exportCapturedPosts);
    },
    onImportCapturedPosts: (file: File) => {
      runAsync(() => importCapturedPostsFromFile(file));
    },
    onUploadApiBaseUrlChange: (value: string) => {
      runAsync(() => updateUploadTarget({ apiBaseUrl: value }));
    },
    onUploadUserIdChange: (value: string) => {
      runAsync(() => updateUploadTarget({ uploadUserId: value }));
    },
    onUploadOriginChange: (value: string) => {
      runAsync(() => updateUploadTarget({ uploadOrigin: value }));
    },
    onCapturedPostsSearchQueryChange: (value: string) => {
      setCapturedPostsSearchQuery(value);
    },
    onCaptureHashtagDraftChange: (value: string) => {
      setCaptureHashtagDraft(value);
    },
    onCaptureHashtagDraftKeyDown: (event: { key: string; preventDefault: () => void }) => {
      if (event.key === "Enter" || event.key === " " || event.key === "Spacebar") {
        event.preventDefault();
        runAsync(addCaptureHashtagFromDraft);
      }
    },
    onAddCaptureHashtag: () => {
      runAsync(addCaptureHashtagFromDraft);
    },
    onOpenCapturedPostExternalUrl: (id: string) => {
      runAsync(() => openCapturedPostExternalUrl(id));
    },
    onOpenCaptureHashtag: (value: string) => {
      runAsync(() => openCaptureHashtag(value));
    },
    onOpenCaptureHashtagInWindow: (value: string) => {
      runAsync(() => openCaptureHashtagInWindow(value));
    },
    onRemoveCaptureHashtag: (value: string) => {
      runAsync(() => removeCaptureHashtag(value));
    },
    onTestEndpoint: (endpoint: EndpointDefinition) => {
      runAsync(() => runSingleTest(endpoint));
    },
    onUsernameChange: (value: string) => {
      setUsernameDraft(value);
      scheduleUsernamePersist();
    },
    onUsernameCommit: () => {
      runAsync(() => persistUsernameFromInput());
    },
    onHashtagChange: (value: string) => {
      setHashtagDraft(value);
      scheduleHashtagPersist();
    },
    onHashtagCommit: () => {
      runAsync(() => persistHashtagFromInput());
    }
  };
}
