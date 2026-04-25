import { useEffect, useMemo, useRef, useState } from "react";

import {
  CAPTURE_STATE_STORAGE_KEY,
  DEFAULT_PROFILE_ID,
  ENDPOINTS,
  PROFILES_STORAGE_KEY,
  SETTINGS_STORAGE_KEY
} from "../popup/constants.js";
import { formatDate, formatDurationFromMs } from "../popup/format.js";
import {
  buildEndpointModel,
  createPatch,
  createSingleEndpointPatch,
  maskPatchSensitiveData,
  resolveGlobalHeaders
} from "../popup/model.js";
import type {
  ApiPatch,
  BackgroundMessage,
  CaptureState,
  EndpointDefinition,
  EndpointModel,
  EndpointTestRuntime,
  RollbackMessageResponse,
  StateMessageResponse
} from "../popup/models.js";
import {
  DEFAULT_SETTINGS,
  createProfileId,
  detectUsernameFromState,
  getEmptyStateSnapshot,
  getFallbackStateSnapshot,
  getFreshnessInfo,
  normalizeProfileStore,
  normalizeSettings
} from "../popup/helpers.js";
import { getCapturedRateEntries, getRateEntries, runEndpointTest } from "../popup/testing.js";
import { cloneJson, normalizeUsername, resolveEndpointPageUrl } from "../popup/utils.js";
import {
  getGlobalStatusOk,
  getProfileHint,
  getSensitiveHint,
  getTestResultText,
  getUsernameHint,
  makeStatusBadge,
  sortProfiles
} from "./view-model.js";
import type {
  ApplySettingsOptions,
  ApplyStateOptions,
  EndpointRowView,
  OpenUrlOptions,
  PopupSettings,
  ProfileRecord,
  ProfilesStore,
  UseCredentialCapturerResult
} from "./types.js";

const PROFILE_SYNC_DEBOUNCE_MS = 400;
const USERNAME_SAVE_DEBOUNCE_MS = 250;

export function useCredentialCapturer(): UseCredentialCapturerResult {
  const [captureState, setCaptureState] = useState<CaptureState | null>(null);
  const [settings, setSettings] = useState<PopupSettings>({ ...DEFAULT_SETTINGS });
  const [detectedUsername, setDetectedUsername] = useState("");
  const [usernameDraft, setUsernameDraft] = useState("");
  const [profilesStore, setProfilesStore] = useState<ProfilesStore | null>(null);
  const [isApplyingProfile, setIsApplyingProfile] = useState(false);
  const [endpointTestState, setEndpointTestState] = useState<Record<string, EndpointTestRuntime>>(
    {}
  );
  const [isBulkTesting, setIsBulkTesting] = useState(false);
  const [testAllStatus, setTestAllStatus] = useState("");
  const [copyPatchLabel, setCopyPatchLabel] = useState("Copy Api patch");
  const [endpointCopyLabels, setEndpointCopyLabels] = useState<Record<string, string>>({});
  const [patchOutput, setPatchOutput] = useState("");
  const [currentRawPatch, setCurrentRawPatch] = useState<ApiPatch | null>(null);

  const captureStateRef = useRef<CaptureState | null>(captureState);
  const settingsRef = useRef<PopupSettings>(settings);
  const profilesStoreRef = useRef<ProfilesStore | null>(profilesStore);
  const isApplyingProfileRef = useRef(isApplyingProfile);
  const usernameDraftRef = useRef(usernameDraft);
  const usernameSaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const profileSyncTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    captureStateRef.current = captureState;
  }, [captureState]);

  useEffect(() => {
    settingsRef.current = settings;
  }, [settings]);

  useEffect(() => {
    profilesStoreRef.current = profilesStore;
  }, [profilesStore]);

  useEffect(() => {
    isApplyingProfileRef.current = isApplyingProfile;
  }, [isApplyingProfile]);

  useEffect(() => {
    usernameDraftRef.current = usernameDraft;
  }, [usernameDraft]);

  function reportError(error: unknown) {
    const message = error instanceof Error ? error.message : String(error);
    setPatchOutput((previous) => `ERROR: ${message}\n\n${previous || ""}`);
  }

  function runAsync(action: () => Promise<unknown>): void {
    void action().catch(reportError);
  }

  function setProfilesStoreState(nextStore: ProfilesStore | null) {
    profilesStoreRef.current = nextStore;
    setProfilesStore(nextStore);
  }

  function setSettingsState(nextSettings: PopupSettings) {
    settingsRef.current = nextSettings;
    setSettings(nextSettings);
  }

  function clearAllTestRuntime() {
    setEndpointTestState({});
  }

  function setTestRuntime(endpointId: string, nextValue: Partial<EndpointTestRuntime>) {
    setEndpointTestState((previous) => {
      const merged = {
        ...(previous[endpointId] || { running: false, result: null }),
        ...nextValue
      };

      return {
        ...previous,
        [endpointId]: merged
      };
    });
  }

  async function sendMessage<T>(message: unknown): Promise<T> {
    return chrome.runtime.sendMessage(message) as Promise<T>;
  }

  function applySettings(nextSettings: unknown, options: ApplySettingsOptions = {}) {
    const normalized = normalizeSettings(nextSettings);
    setSettingsState(normalized);

    if (options.syncInput !== false) {
      setUsernameDraft(normalized.username);
      usernameDraftRef.current = normalized.username;
    }

    if (options.scheduleSync) {
      scheduleActiveProfileSync();
    }
  }

  async function saveSettings(nextPartial: Partial<PopupSettings>) {
    const next = normalizeSettings({ ...settingsRef.current, ...(nextPartial || {}) });
    applySettings(next, { syncInput: true, scheduleSync: true });
    await chrome.storage.local.set({ [SETTINGS_STORAGE_KEY]: next });
  }

  async function maybeAutoDetectUsername(state: CaptureState) {
    const detected = detectUsernameFromState(state);
    const normalizedDetected = detected || "";
    setDetectedUsername(normalizedDetected);

    if (!settingsRef.current.username && normalizedDetected && !isApplyingProfileRef.current) {
      await saveSettings({ username: normalizedDetected });
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

  function buildNormalizedProfileStore(rawStore: unknown): ProfilesStore {
    return normalizeProfileStore(rawStore, {
      fallbackState: getFallbackStateSnapshot(captureStateRef.current),
      fallbackSettings: normalizeSettings(settingsRef.current)
    });
  }

  async function persistProfilesStore(nextStore: ProfilesStore) {
    setProfilesStoreState(nextStore);
    await chrome.storage.local.set({ [PROFILES_STORAGE_KEY]: nextStore });
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

  function scheduleActiveProfileSync() {
    const currentState = captureStateRef.current;
    const currentStore = profilesStoreRef.current;

    if (isApplyingProfileRef.current || !currentState || !currentStore) {
      return;
    }

    if (profileSyncTimerRef.current) {
      clearTimeout(profileSyncTimerRef.current);
    }

    profileSyncTimerRef.current = setTimeout(() => {
      runAsync(async () => {
        const latestState = captureStateRef.current;
        const latestStore = profilesStoreRef.current;

        if (isApplyingProfileRef.current || !latestState || !latestStore) {
          return;
        }

        const activeId = latestStore.activeProfileId;
        const activeProfile = latestStore.profiles[activeId];

        if (!activeProfile) {
          return;
        }

        const nextStore: ProfilesStore = {
          ...latestStore,
          profiles: {
            ...latestStore.profiles,
            [activeId]: {
              ...activeProfile,
              state: cloneJson(latestState),
              settings: cloneJson(settingsRef.current),
              updatedAt: new Date().toISOString()
            }
          }
        };

        await persistProfilesStore(nextStore);
      });
    }, PROFILE_SYNC_DEBOUNCE_MS);
  }

  async function activateProfile(
    profileId: string,
    options: { resetTests?: boolean; persistStore?: boolean } = {},
    sourceStore: ProfilesStore | null = null
  ) {
    const store = sourceStore || profilesStoreRef.current;

    if (!store) {
      throw new Error("Profiles store is not loaded");
    }

    const profile = store.profiles[profileId];

    if (!profile) {
      throw new Error("Profile not found");
    }

    const resetTests = options.resetTests !== false;
    const persistStore = options.persistStore !== false;

    isApplyingProfileRef.current = true;
    setIsApplyingProfile(true);

    try {
      let nextStore: ProfilesStore = {
        ...store,
        activeProfileId: profileId
      };
      setProfilesStoreState(nextStore);

      const nextSettings = normalizeSettings(profile.settings);
      applySettings(nextSettings, { syncInput: true, scheduleSync: false });
      await chrome.storage.local.set({ [SETTINGS_STORAGE_KEY]: nextSettings });

      const fallbackState = getFallbackStateSnapshot(captureStateRef.current);
      const remoteState = await setRemoteState(cloneJson(profile.state || fallbackState));
      applyState(remoteState, { resetTests });

      const activeProfile = nextStore.profiles[profileId];

      if (!activeProfile) {
        throw new Error("Profile not found");
      }

      nextStore = {
        ...nextStore,
        profiles: {
          ...nextStore.profiles,
          [profileId]: {
            ...activeProfile,
            state: cloneJson(remoteState),
            settings: cloneJson(nextSettings),
            updatedAt: new Date().toISOString()
          }
        }
      };

      if (persistStore) {
        await persistProfilesStore(nextStore);
      } else {
        setProfilesStoreState(nextStore);
      }
    } finally {
      isApplyingProfileRef.current = false;
      setIsApplyingProfile(false);
    }
  }

  async function handleProfilesStorageChange(nextProfilesStore: unknown) {
    const previousActive = profilesStoreRef.current?.activeProfileId || DEFAULT_PROFILE_ID;
    const normalizedStore = buildNormalizedProfileStore(nextProfilesStore);
    setProfilesStoreState(normalizedStore);

    if (!isApplyingProfileRef.current && normalizedStore.activeProfileId !== previousActive) {
      await activateProfile(
        normalizedStore.activeProfileId,
        { persistStore: false, resetTests: true },
        normalizedStore
      );
    }
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

  async function loadProfiles() {
    const data = await chrome.storage.local.get(PROFILES_STORAGE_KEY);
    let nextStore = buildNormalizedProfileStore(data?.[PROFILES_STORAGE_KEY]);
    setProfilesStoreState(nextStore);

    if (!data?.[PROFILES_STORAGE_KEY]) {
      await chrome.storage.local.set({ [PROFILES_STORAGE_KEY]: nextStore });
    }

    const activeId = nextStore.activeProfileId;
    const activeProfile = nextStore.profiles[activeId];

    if (!activeProfile) {
      return;
    }

    applySettings(activeProfile.settings, { syncInput: true, scheduleSync: false });

    const currentState = captureStateRef.current;

    if (currentState) {
      nextStore = {
        ...nextStore,
        profiles: {
          ...nextStore.profiles,
          [activeId]: {
            ...activeProfile,
            state: cloneJson(currentState),
            settings: cloneJson(settingsRef.current),
            updatedAt: new Date().toISOString()
          }
        }
      };
      await persistProfilesStore(nextStore);
      return;
    }

    await activateProfile(activeId, { persistStore: true, resetTests: true }, nextStore);
  }

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
    const tab = await chrome.tabs.create({ url, active });

    if (typeof tab?.id === "number") {
      try {
        await chrome.tabs.reload(tab.id, { bypassCache: true });
      } catch (_error) {
        // Ignore reload errors; tab was already opened.
      }
    }
  }

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

  async function persistUsernameFromInput() {
    const normalized = normalizeUsername(usernameDraftRef.current);

    if (normalized !== usernameDraftRef.current) {
      setUsernameDraft(normalized);
      usernameDraftRef.current = normalized;
    }

    if (normalized === settingsRef.current.username) {
      return;
    }

    await saveSettings({ username: normalized });
  }

  function scheduleUsernamePersist() {
    if (usernameSaveTimerRef.current) {
      clearTimeout(usernameSaveTimerRef.current);
    }

    usernameSaveTimerRef.current = setTimeout(() => {
      runAsync(persistUsernameFromInput);
    }, USERNAME_SAVE_DEBOUNCE_MS);
  }

  async function copyEndpoint(model: EndpointModel, endpointId: string) {
    ensureCopyAllowed();
    const singlePatch = createSingleEndpointPatch(model);
    const text = JSON.stringify(singlePatch, null, 2);
    await navigator.clipboard.writeText(text);

    setEndpointCopyLabels((previous) => ({
      ...previous,
      [endpointId]: "Copied"
    }));

    setTimeout(() => {
      setEndpointCopyLabels((previous) => ({
        ...previous,
        [endpointId]: "Copy"
      }));
    }, 1200);
  }

  async function runSingleTest(endpoint: EndpointDefinition) {
    setTestRuntime(endpoint.id, { running: true, result: null });

    try {
      const stateNow = captureStateRef.current;
      const globalNow = resolveGlobalHeaders(stateNow);
      const modelNow = buildEndpointModel(endpoint, stateNow, globalNow);
      const result = await runEndpointTest(modelNow);
      setTestRuntime(endpoint.id, { running: false, result });
    } catch (error) {
      setTestRuntime(endpoint.id, {
        running: false,
        result: {
          ok: false,
          status: 0,
          hasData: false,
          rate: null,
          message: `Error: ${error instanceof Error ? error.message : String(error)}`,
          bodySnippet: ""
        }
      });
    }
  }

  async function runAllTests() {
    const stateNow = captureStateRef.current;

    if (isBulkTesting || !stateNow) {
      return;
    }

    const globalHeaders = resolveGlobalHeaders(stateNow);
    const testable = ENDPOINTS.filter((endpoint) => {
      const model = buildEndpointModel(endpoint, stateNow, globalHeaders);
      return !endpoint.skipped && model.ready;
    });

    if (testable.length === 0) {
      setTestAllStatus("No complete endpoints available to test.");
      return;
    }

    setIsBulkTesting(true);

    let okCount = 0;
    let failCount = 0;
    const startedAt = Date.now();

    try {
      for (const endpoint of testable) {
        setTestRuntime(endpoint.id, { running: true, result: null });

        try {
          const latestState = captureStateRef.current;
          const globalNow = resolveGlobalHeaders(latestState);
          const modelNow = buildEndpointModel(endpoint, latestState, globalNow);
          const result = await runEndpointTest(modelNow);
          setTestRuntime(endpoint.id, { running: false, result });

          if (result.ok) {
            okCount += 1;
          } else {
            failCount += 1;
          }
        } catch (error) {
          failCount += 1;
          setTestRuntime(endpoint.id, {
            running: false,
            result: {
              ok: false,
              status: 0,
              hasData: false,
              rate: null,
              message: `Error: ${error instanceof Error ? error.message : String(error)}`,
              bodySnippet: ""
            }
          });
        }
      }
    } finally {
      setIsBulkTesting(false);
    }

    const elapsed = formatDurationFromMs(Date.now() - startedAt);
    setTestAllStatus(`Done: ${okCount} OK, ${failCount} failed (${elapsed})`);
  }

  async function createProfile() {
    const store = profilesStoreRef.current;

    if (!store) {
      return;
    }

    const defaultName = `Profile ${Object.keys(store.profiles).length + 1}`;
    const name = window.prompt("New profile name:", defaultName);

    if (typeof name !== "string") {
      return;
    }

    const cleanName = name.trim();

    if (!cleanName) {
      return;
    }

    const profileId = createProfileId();
    const nextStore: ProfilesStore = {
      ...store,
      profiles: {
        ...store.profiles,
        [profileId]: {
          id: profileId,
          name: cleanName,
          state: cloneJson(getEmptyStateSnapshot()),
          settings: cloneJson(settingsRef.current),
          updatedAt: new Date().toISOString()
        }
      }
    };

    setProfilesStoreState(nextStore);
    await activateProfile(profileId, { persistStore: true, resetTests: false }, nextStore);
  }

  async function deleteSelectedProfile() {
    const store = profilesStoreRef.current;

    if (!store) {
      return;
    }

    const targetId = store.activeProfileId;

    if (!targetId || targetId === DEFAULT_PROFILE_ID) {
      return;
    }

    const targetProfile = store.profiles[targetId];

    if (!targetProfile) {
      return;
    }

    const confirmed = window.confirm(`Delete profile "${targetProfile.name}"?`);

    if (!confirmed) {
      return;
    }

    const nextProfiles: Record<string, ProfileRecord> = { ...store.profiles };
    delete nextProfiles[targetId];

    if (!nextProfiles[DEFAULT_PROFILE_ID]) {
      nextProfiles[DEFAULT_PROFILE_ID] = {
        id: DEFAULT_PROFILE_ID,
        name: "Default",
        state: cloneJson(getFallbackStateSnapshot(captureStateRef.current)),
        settings: cloneJson(settingsRef.current),
        updatedAt: new Date().toISOString()
      };
    }

    const remainingIds = Object.keys(nextProfiles);

    if (remainingIds.length === 0) {
      return;
    }

    const nextActive = remainingIds.includes(DEFAULT_PROFILE_ID)
      ? DEFAULT_PROFILE_ID
      : remainingIds[0];
    const nextStore: ProfilesStore = {
      activeProfileId: nextActive,
      profiles: nextProfiles
    };

    setProfilesStoreState(nextStore);
    await activateProfile(nextActive, { persistStore: true, resetTests: false }, nextStore);
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
    });

    return () => {
      if (usernameSaveTimerRef.current) {
        clearTimeout(usernameSaveTimerRef.current);
        usernameSaveTimerRef.current = null;
      }

      if (profileSyncTimerRef.current) {
        clearTimeout(profileSyncTimerRef.current);
        profileSyncTimerRef.current = null;
      }
    };
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

  const globalHeaders = useMemo(() => resolveGlobalHeaders(captureState), [captureState]);
  const globalStatusOk = useMemo(() => getGlobalStatusOk(globalHeaders), [globalHeaders]);

  const selectedProfile =
    profilesStore && profilesStore.profiles
      ? profilesStore.profiles[profilesStore.activeProfileId] || null
      : null;

  const profiles = useMemo(() => sortProfiles(profilesStore?.profiles), [profilesStore]);

  const canDeleteProfile = Boolean(
    selectedProfile &&
    selectedProfile.id !== DEFAULT_PROFILE_ID &&
    Object.keys(profilesStore?.profiles || {}).length > 1
  );

  const profileHint = getProfileHint(selectedProfile);
  const sensitiveHint = getSensitiveHint(settings.maskSensitive);
  const usernameHint = getUsernameHint(settings, detectedUsername);

  const endpointRows: EndpointRowView[] = useMemo(() => {
    return ENDPOINTS.map((endpoint) => {
      const model = buildEndpointModel(endpoint, captureState, globalHeaders);
      const testRuntime: EndpointTestRuntime = endpointTestState[endpoint.id] || {
        running: false,
        result: null
      };
      const endpointPageUrl = resolveEndpointPageUrl(endpoint, settings.username);
      const freshness = getFreshnessInfo(model, testRuntime);
      const testedRateEntries = getRateEntries(testRuntime.result);
      const capturedRateEntries = getCapturedRateEntries(model.capture);
      const rateEntries = testedRateEntries.length > 0 ? testedRateEntries : capturedRateEntries;
      const statusBadge = makeStatusBadge(model);

      const copyDisabled = settings.maskSensitive || endpoint.skipped || !model.ready;
      const copyTitle = settings.maskSensitive
        ? "Disable Sensitive guard to copy real credentials"
        : endpoint.skipped
          ? "Endpoint skipped"
          : !model.ready
            ? "Complete missing fields to copy"
            : "";

      return {
        endpoint,
        model,
        endpointPageUrl,
        testRuntime,
        testResultText: getTestResultText(testRuntime),
        statusBadge,
        freshness,
        rateEntries,
        copyLabel: endpointCopyLabels[endpoint.id] || "Copy",
        copyDisabled,
        copyTitle,
        testDisabled: endpoint.skipped || !model.ready || testRuntime.running || isBulkTesting
      };
    });
  }, [captureState, endpointCopyLabels, endpointTestState, globalHeaders, isBulkTesting, settings]);

  return {
    activeProfileId: profilesStore?.activeProfileId || DEFAULT_PROFILE_ID,
    canDeleteProfile,
    copyPatchLabel,
    endpointRows,
    globalStatusOk,
    isApplyingProfile,
    isBulkTesting,
    patchOutput,
    profileHint,
    profiles,
    sensitiveHint,
    settings,
    testAllStatus,
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
    onOpenEndpointUrl: (url: string, openInBackground: boolean) => {
      runAsync(() => openUrlWithBypassCache(url, { active: !openInBackground }));
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
    onTestEndpoint: (endpoint: EndpointDefinition) => {
      runAsync(() => runSingleTest(endpoint));
    },
    onUsernameChange: (value: string) => {
      setUsernameDraft(value);
      usernameDraftRef.current = value;
      scheduleUsernamePersist();
    },
    onUsernameCommit: () => {
      runAsync(persistUsernameFromInput);
    }
  };
}
