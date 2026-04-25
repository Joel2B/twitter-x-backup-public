import { DEFAULT_PROFILE_ID } from "../popup/constants.js";
import type {
  EndpointModel,
  EndpointTestRuntime,
  GlobalHeaders,
  PopupSettings
} from "../popup/models.js";
import { formatAgeFromIso } from "../popup/format.js";
import { pickFirstNonEmpty } from "../popup/utils.js";
import type { ProfileRecord, StatusBadge } from "./types.js";

export function getTestResultText(testRuntime: EndpointTestRuntime): string {
  if (!testRuntime?.result) {
    return "";
  }

  return testRuntime.result.message || "";
}

export function makeStatusBadge(model: EndpointModel): StatusBadge {
  if (model.endpoint.skipped) {
    return { className: "status skipped", label: "Skipped" };
  }

  if (model.ready) {
    return { className: "status ok", label: "Complete" };
  }

  return { className: "status pending", label: model.captured ? "Incomplete" : "Pending" };
}

export function getGlobalStatusOk(globalHeaders: GlobalHeaders): boolean {
  return Boolean(
    pickFirstNonEmpty(globalHeaders.cookie) &&
    pickFirstNonEmpty(globalHeaders["x-csrf-token"]) &&
    pickFirstNonEmpty(globalHeaders.authorization)
  );
}

export function sortProfiles(profiles: Record<string, ProfileRecord> | null | undefined) {
  const values = Object.values(profiles || {}) as ProfileRecord[];

  return values.sort((a, b) => {
    if (a.id === DEFAULT_PROFILE_ID) {
      return -1;
    }

    if (b.id === DEFAULT_PROFILE_ID) {
      return 1;
    }

    return a.name.localeCompare(b.name);
  });
}

export function getProfileHint(selectedProfile: ProfileRecord | null): string {
  if (!selectedProfile) {
    return "Profiles isolate captured credentials and username.";
  }

  const updated = selectedProfile.updatedAt
    ? ` Updated ${formatAgeFromIso(selectedProfile.updatedAt)}.`
    : "";

  return `Active profile: ${selectedProfile.name}.${updated}`;
}

export function getSensitiveHint(maskSensitive: boolean): string {
  return maskSensitive
    ? "Sensitive guard is ON. Disable it to copy real secrets."
    : "Sensitive guard is OFF. Copy will include real secrets.";
}

export function getUsernameHint(settings: PopupSettings, detectedUsername: string): string {
  if (settings.username && detectedUsername && settings.username !== detectedUsername) {
    return `Open links use @${settings.username} (detected @${detectedUsername})`;
  }

  if (settings.username) {
    return `Open links use @${settings.username}`;
  }

  if (detectedUsername) {
    return `Auto-detected @${detectedUsername}. You can edit it.`;
  }

  return "Used for posts/likes/media Open links";
}
