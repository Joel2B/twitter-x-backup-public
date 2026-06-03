import { useEffect, useMemo, useRef, useState } from "react";
import type { MutableRefObject } from "react";

import {
  DEFAULT_PROFILE_ID,
  PROFILES_STORAGE_KEY,
  SETTINGS_STORAGE_KEY
} from "../popup/constants.js";
import { createProfileId, normalizeProfileStore, normalizeSettings } from "../popup/helpers.js";
import type { CaptureState, PopupSettings } from "../popup/models.js";
import { cloneJson } from "../popup/utils.js";
import { sortProfiles } from "./view-model.js";
import type { ApplyStateOptions, ProfileRecord, ProfilesStore } from "./types.js";

const PROFILE_SYNC_DEBOUNCE_MS = 400;

type UseProfilesOptions = {
  applySettings: (nextSettings: unknown, options?: { syncInput?: boolean; scheduleSync?: boolean }) => void;
  applyState: (nextState: CaptureState | null, options?: ApplyStateOptions) => void;
  captureStateRef: MutableRefObject<CaptureState | null>;
  getEmptyStateSnapshot: () => CaptureState;
  getFallbackStateSnapshot: (currentState: CaptureState | null) => CaptureState;
  settingsRef: MutableRefObject<PopupSettings>;
  setRemoteState: (nextState: CaptureState) => Promise<CaptureState>;
};

type UseProfilesResult = {
  activateProfile: (
    profileId: string,
    options?: { resetTests?: boolean; persistStore?: boolean },
    sourceStore?: ProfilesStore | null
  ) => Promise<void>;
  canDeleteProfile: boolean;
  createProfile: () => Promise<void>;
  deleteSelectedProfile: () => Promise<void>;
  handleProfilesStorageChange: (nextProfilesStore: unknown) => Promise<void>;
  isApplyingProfile: boolean;
  isApplyingProfileRef: MutableRefObject<boolean>;
  loadProfiles: () => Promise<void>;
  profiles: ProfileRecord[];
  profilesStore: ProfilesStore | null;
  scheduleActiveProfileSync: () => void;
  selectedProfile: ProfileRecord | null;
};

export function useProfiles({
  applySettings,
  applyState,
  captureStateRef,
  getEmptyStateSnapshot,
  getFallbackStateSnapshot,
  settingsRef,
  setRemoteState
}: UseProfilesOptions): UseProfilesResult {
  const [profilesStore, setProfilesStore] = useState<ProfilesStore | null>(null);
  const [isApplyingProfile, setIsApplyingProfile] = useState(false);

  const profilesStoreRef = useRef<ProfilesStore | null>(profilesStore);
  const isApplyingProfileRef = useRef(isApplyingProfile);
  const profileSyncTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    profilesStoreRef.current = profilesStore;
  }, [profilesStore]);

  useEffect(() => {
    isApplyingProfileRef.current = isApplyingProfile;
  }, [isApplyingProfile]);

  useEffect(() => {
    return () => {
      if (profileSyncTimerRef.current) {
        clearTimeout(profileSyncTimerRef.current);
      }
    };
  }, []);

  function setProfilesStoreState(nextStore: ProfilesStore | null) {
    profilesStoreRef.current = nextStore;
    setProfilesStore(nextStore);
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
      void (async () => {
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
      })();
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

  return {
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
  };
}
