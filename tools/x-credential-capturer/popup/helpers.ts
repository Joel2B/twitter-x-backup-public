import { DEFAULT_PROFILE_ID, ENDPOINTS } from "./constants.js";
import { formatAgeFromIso } from "./format.js";
import type {
  CaptureState,
  EndpointModel,
  EndpointTestRuntime,
  FreshnessInfo,
  PopupSettings,
  ProfilesStore
} from "./models.js";
import { cloneJson, extractUsernameFromXUrl, isPlainObject, normalizeUsername } from "./utils.js";

const FRESHNESS_VALID_MS = 30 * 60 * 1000;
const FRESHNESS_EXPIRING_MS = 6 * 60 * 60 * 1000;

export const DEFAULT_SETTINGS = {
  username: "",
  maskSensitive: true
};

export function normalizeSettings(value: unknown): PopupSettings {
  const source = isPlainObject(value) ? value : {};

  return {
    username: normalizeUsername(source.username || ""),
    maskSensitive: typeof source.maskSensitive === "boolean" ? source.maskSensitive : true
  };
}

export function getFallbackStateSnapshot(currentState: CaptureState | null): CaptureState {
  return cloneJson(
    currentState || {
      updatedAt: new Date().toISOString(),
      global: {
        authorization: null,
        xCsrfToken: null,
        cookie: null
      },
      endpoints: {}
    }
  );
}

export function getEmptyStateSnapshot(): CaptureState {
  return {
    updatedAt: new Date().toISOString(),
    global: {
      authorization: null,
      xCsrfToken: null,
      cookie: null
    },
    endpoints: {}
  };
}

export function detectUsernameFromState(state: CaptureState | null): string {
  const counters = new Map();

  for (const endpoint of ENDPOINTS) {
    const capture = state?.endpoints?.[endpoint.id];
    const refs = [
      capture?.headers?.referer,
      capture?.headers?.all?.referer,
      capture?.headers?.all?.Referer
    ];

    for (const ref of refs) {
      const username = extractUsernameFromXUrl(ref);

      if (!username) {
        continue;
      }

      counters.set(username, (counters.get(username) || 0) + 1);
    }
  }

  let best = "";
  let bestScore = 0;
  for (const [username, score] of counters.entries()) {
    if (score > bestScore) {
      best = username;
      bestScore = score;
    }
  }

  return best;
}

export function getFreshnessInfo(
  model: EndpointModel,
  testRuntime: EndpointTestRuntime
): FreshnessInfo {
  if (model.endpoint.skipped) {
    return { className: "unknown", label: "Skipped" };
  }

  if (testRuntime?.result && !testRuntime.result.ok) {
    return { className: "expired", label: "Expired (test failed)" };
  }

  const lastSeen = model.capture?.response?.lastSeenAt || model.capture?.lastSeenAt;

  if (!lastSeen) {
    return { className: "unknown", label: "Unknown" };
  }

  const ageText = formatAgeFromIso(lastSeen);
  const ageMs = Date.now() - new Date(lastSeen).getTime();

  if (Number.isNaN(ageMs)) {
    return { className: "unknown", label: "Unknown" };
  }

  if (ageMs <= FRESHNESS_VALID_MS) {
    return { className: "valid", label: `Fresh · ${ageText}` };
  }

  if (ageMs <= FRESHNESS_EXPIRING_MS) {
    return { className: "expiring", label: `Aging · ${ageText}` };
  }

  return { className: "expired", label: `Stale · ${ageText}` };
}

export function normalizeProfileStore(
  value: unknown,
  options: { fallbackState: CaptureState; fallbackSettings: PopupSettings }
): ProfilesStore {
  const source = isPlainObject(value) ? value : {};
  const fallbackState = options.fallbackState;
  const fallbackSettings = options.fallbackSettings;
  const normalizedProfiles: ProfilesStore["profiles"] = {};

  if (isPlainObject(source.profiles)) {
    for (const [id, profile] of Object.entries(source.profiles)) {
      if (!id) {
        continue;
      }

      const profileEntry = isPlainObject(profile) ? profile : {};
      const profileName = typeof profileEntry.name === "string" ? profileEntry.name.trim() : "";

      normalizedProfiles[id] = {
        id,
        name: profileName || (id === DEFAULT_PROFILE_ID ? "Default" : `Profile ${id}`),
        state: cloneJson(
          (isPlainObject(profileEntry.state) ? profileEntry.state : fallbackState) as CaptureState
        ),
        settings: normalizeSettings(profileEntry.settings || fallbackSettings),
        updatedAt: typeof profileEntry.updatedAt === "string" ? profileEntry.updatedAt : null
      };
    }
  }

  if (!normalizedProfiles[DEFAULT_PROFILE_ID]) {
    normalizedProfiles[DEFAULT_PROFILE_ID] = {
      id: DEFAULT_PROFILE_ID,
      name: "Default",
      state: cloneJson(fallbackState),
      settings: normalizeSettings(fallbackSettings),
      updatedAt: null
    };
  }

  const profileIds = Object.keys(normalizedProfiles);
  const requestedActiveProfileId =
    typeof source.activeProfileId === "string" ? source.activeProfileId : null;
  const activeProfileId =
    requestedActiveProfileId && profileIds.includes(requestedActiveProfileId)
      ? requestedActiveProfileId
      : profileIds.includes(DEFAULT_PROFILE_ID)
        ? DEFAULT_PROFILE_ID
        : profileIds[0];

  return {
    activeProfileId,
    profiles: normalizedProfiles
  };
}

export function createProfileId(): string {
  return `profile_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
}
