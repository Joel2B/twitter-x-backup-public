import { useEffect, useRef, useState } from "react";
import type { MutableRefObject } from "react";

import { SETTINGS_STORAGE_KEY } from "../popup/constants.js";
import { DEFAULT_SETTINGS, normalizeSettings } from "../popup/helpers.js";
import type { PopupSettings } from "../popup/models.js";
import { normalizeHashtag, normalizeUsername } from "../popup/utils.js";
import type { ApplySettingsOptions } from "./types.js";

const USERNAME_SAVE_DEBOUNCE_MS = 250;
const HASHTAG_SAVE_DEBOUNCE_MS = 250;

type UsePopupSettingsOptions = {
  onScheduleActiveProfileSync: () => void;
};

type UsePopupSettingsResult = {
  applySettings: (nextSettings: unknown, options?: ApplySettingsOptions) => void;
  detectedUsername: string;
  hashtagDraft: string;
  persistHashtagFromInput: () => Promise<void>;
  persistUsernameFromInput: () => Promise<void>;
  saveSettings: (nextPartial: Partial<PopupSettings>) => Promise<void>;
  scheduleHashtagPersist: () => void;
  scheduleUsernamePersist: () => void;
  settings: PopupSettings;
  settingsRef: MutableRefObject<PopupSettings>;
  setHashtagDraft: (value: string) => void;
  setDetectedUsername: (value: string) => void;
  setUsernameDraft: (value: string) => void;
  usernameDraft: string;
};

export function usePopupSettings({
  onScheduleActiveProfileSync
}: UsePopupSettingsOptions): UsePopupSettingsResult {
  const [settings, setSettings] = useState<PopupSettings>({ ...DEFAULT_SETTINGS });
  const [detectedUsername, setDetectedUsername] = useState("");
  const [usernameDraft, setUsernameDraft] = useState("");
  const [hashtagDraft, setHashtagDraft] = useState("");

  const settingsRef = useRef<PopupSettings>(settings);
  const usernameDraftRef = useRef(usernameDraft);
  const hashtagDraftRef = useRef(hashtagDraft);
  const usernameSaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const hashtagSaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    settingsRef.current = settings;
  }, [settings]);

  useEffect(() => {
    usernameDraftRef.current = usernameDraft;
  }, [usernameDraft]);

  useEffect(() => {
    hashtagDraftRef.current = hashtagDraft;
  }, [hashtagDraft]);

  useEffect(() => {
    return () => {
      if (usernameSaveTimerRef.current) {
        clearTimeout(usernameSaveTimerRef.current);
      }

      if (hashtagSaveTimerRef.current) {
        clearTimeout(hashtagSaveTimerRef.current);
      }
    };
  }, []);

  function setSettingsState(nextSettings: PopupSettings) {
    settingsRef.current = nextSettings;
    setSettings(nextSettings);
  }

  function applySettings(nextSettings: unknown, options: ApplySettingsOptions = {}) {
    const normalized = normalizeSettings(nextSettings);
    setSettingsState(normalized);

    if (options.syncInput !== false) {
      setUsernameDraft(normalized.username);
      usernameDraftRef.current = normalized.username;
      setHashtagDraft(normalized.hashtag);
      hashtagDraftRef.current = normalized.hashtag;
    }

    if (options.scheduleSync) {
      onScheduleActiveProfileSync();
    }
  }

  async function saveSettings(nextPartial: Partial<PopupSettings>) {
    const next = normalizeSettings({ ...settingsRef.current, ...(nextPartial || {}) });
    applySettings(next, { syncInput: true, scheduleSync: true });
    await chrome.storage.local.set({ [SETTINGS_STORAGE_KEY]: next });
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
      void persistUsernameFromInput();
    }, USERNAME_SAVE_DEBOUNCE_MS);
  }

  async function persistHashtagFromInput() {
    const normalized = normalizeHashtag(hashtagDraftRef.current);

    if (normalized !== hashtagDraftRef.current) {
      setHashtagDraft(normalized);
      hashtagDraftRef.current = normalized;
    }

    if (normalized === settingsRef.current.hashtag) {
      return;
    }

    await saveSettings({ hashtag: normalized });
  }

  function scheduleHashtagPersist() {
    if (hashtagSaveTimerRef.current) {
      clearTimeout(hashtagSaveTimerRef.current);
    }

    hashtagSaveTimerRef.current = setTimeout(() => {
      void persistHashtagFromInput();
    }, HASHTAG_SAVE_DEBOUNCE_MS);
  }

  return {
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
  };
}
