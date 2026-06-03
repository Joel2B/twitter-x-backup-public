import { formatDurationFromMs } from "../popup/format.js";
import type {
  CapturedPostItem,
  UploadCapturedPostsMessageResponse,
  UploadNotificationItem
} from "../popup/models.js";
import type { CapturedPostRowView, UploadNotificationRowView } from "./types.js";

export function normalizeCaptureHashtag(value: string): string {
  return value.trim().replace(/^#+/, "").toLowerCase();
}

export function splitCaptureHashtagDraft(value: string): string[] {
  const values = value
    .split(/[\s,]+/g)
    .map((entry) => normalizeCaptureHashtag(entry))
    .filter((entry) => entry.length > 0);

  return [...new Set(values)];
}

export function postMatchesSearch(item: CapturedPostItem, searchQuery: string): boolean {
  if (!searchQuery) {
    return true;
  }

  const parts: string[] = [
    item.id,
    item.operation,
    item.text || "",
    item.authorUserName || "",
    item.authorName || "",
    item.authorId || "",
    item.postUrl || "",
    item.capturedAt || "",
    item.lastSeenAt || "",
    item.uploadedAt || "",
    ...item.mediaUrls
  ];

  try {
    parts.push(JSON.stringify(item.processed));
  } catch (_error) {
    // Ignore serialization errors and keep matching with available fields.
  }

  return parts.join("\n").toLowerCase().includes(searchQuery);
}

export function normalizeDetectedUrl(value: string): string | null {
  const trimmed = value.trim().replace(/[)\],.;!?]+$/g, "");

  if (!trimmed) {
    return null;
  }

  return /^https?:\/\//i.test(trimmed) ? trimmed : `https://${trimmed}`;
}

export function extractLastTcoUrlFromDescription(
  description: string | null | undefined
): string | null {
  if (!description) {
    return null;
  }

  const matches = description.match(/\b(?:https?:\/\/)?t\.co\/[A-Za-z0-9]+(?:[^\s]*)?/gi) || [];

  if (matches.length === 0) {
    return null;
  }

  return normalizeDetectedUrl(matches[matches.length - 1]);
}

export function formatUploadSummary(response: UploadCapturedPostsMessageResponse): string {
  if (!response.ok) {
    return "";
  }

  const summary = response.uploadSummary;

  if (!summary) {
    return `Uploaded: ${response.uploaded.length}`;
  }

  const segments: string[] = [`Uploaded: ${response.uploaded.length}/${summary.attemptedPosts}`];

  if (summary.receivedPosts !== null) {
    segments.push(`received: ${summary.receivedPosts}`);
  }

  if (summary.savedPosts !== null) {
    segments.push(`saved: ${summary.savedPosts}`);
  }

  if (summary.ignoredPosts !== null) {
    segments.push(`ignored: ${summary.ignoredPosts}`);
  }

  if (summary.beforeCount !== null && summary.afterCount !== null) {
    const delta =
      summary.deltaCount !== null ? summary.deltaCount : summary.afterCount - summary.beforeCount;
    const deltaSign = delta >= 0 ? "+" : "";
    segments.push(`total: ${summary.beforeCount} -> ${summary.afterCount} (${deltaSign}${delta})`);
  }

  if (summary.durationMs !== null) {
    segments.push(
      `duration: ${summary.durationMs} ms (${formatDurationFromMs(summary.durationMs)})`
    );
  }

  return segments.join(" | ");
}

function asEpoch(value: string | null | undefined): number {
  const parsed = value ? new Date(value).getTime() : Number.NaN;
  return Number.isFinite(parsed) ? parsed : 0;
}

export function buildCapturedPostRows(input: {
  items: Record<string, CapturedPostItem> | null | undefined;
  searchQuery: string;
  selectedIds: string[];
  sortOrder: "latest-added" | "oldest-added" | "last-seen";
}): CapturedPostRowView[] {
  const items = Object.values(input.items || {});
  const normalizedSearchQuery = input.searchQuery.trim().toLowerCase();

  return items
    .sort((a, b) => {
      if (input.sortOrder === "oldest-added") {
        return asEpoch(a.capturedAt) - asEpoch(b.capturedAt);
      }

      if (input.sortOrder === "last-seen") {
        return asEpoch(b.lastSeenAt) - asEpoch(a.lastSeenAt);
      }

      return asEpoch(b.capturedAt) - asEpoch(a.capturedAt);
    })
    .filter((item) => postMatchesSearch(item, normalizedSearchQuery))
    .map((item) => {
      const preview =
        (item.text && item.text.trim()) ||
        (item.mediaUrls[0] ? `MEDIA: ${item.mediaUrls[0]}` : "(no text/media)");
      const externalUrl =
        extractLastTcoUrlFromDescription(item.processed?.description) ||
        extractLastTcoUrlFromDescription(item.text);

      return {
        item,
        preview,
        externalUrl,
        selected: input.selectedIds.includes(item.id),
        selectable: !item.uploadedAt
      };
    });
}

export function buildUploadNotificationRows(
  items: UploadNotificationItem[] | null | undefined
): UploadNotificationRowView[] {
  const now = Date.now();

  return (items || [])
    .slice()
    .sort((a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime())
    .map((item) => {
      const startedMs = new Date(item.startedAt).getTime();
      const endMs = item.completedAt ? new Date(item.completedAt).getTime() : now;
      const progressDurationMs =
        Number.isFinite(startedMs) && Number.isFinite(endMs) && endMs >= startedMs
          ? endMs - startedMs
          : 0;

      return {
        item,
        progressDurationMs
      };
    });
}
