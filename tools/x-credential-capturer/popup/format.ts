import { pickFirstNonEmpty } from "./utils.js";

export function formatDate(value: string | number | Date | null | undefined): string {
  if (!value) {
    return "-";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return date.toLocaleString();
}

export function formatDurationFromMs(durationMs: number): string {
  const totalSeconds = Math.max(0, Math.floor(durationMs / 1000));
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  if (days > 0) {
    return `${days}d ${hours}h ${minutes}m`;
  }

  if (hours > 0) {
    return `${hours}h ${minutes}m ${seconds}s`;
  }

  if (minutes > 0) {
    return `${minutes}m ${seconds}s`;
  }

  return `${seconds}s`;
}

function formatRateResetDate(value: string | null | undefined): string | null {
  const raw = pickFirstNonEmpty(value);

  if (!raw) {
    return null;
  }

  const epoch = Number(raw);

  if (!Number.isFinite(epoch)) {
    return null;
  }

  const millis = epoch > 1e12 ? epoch : epoch * 1000;
  const date = new Date(millis);

  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return date.toLocaleString();
}

function formatRateResetCountdown(value: string | null | undefined): string | null {
  const raw = pickFirstNonEmpty(value);

  if (!raw) {
    return null;
  }

  const epoch = Number(raw);

  if (!Number.isFinite(epoch)) {
    return null;
  }

  const millis = epoch > 1e12 ? epoch : epoch * 1000;
  const diff = millis - Date.now();

  if (diff >= 0) {
    return `${formatDurationFromMs(diff)} remaining`;
  }

  return `${formatDurationFromMs(Math.abs(diff))} ago`;
}

export function formatRateValue(key: string, value: string | null | undefined): string {
  const raw = pickFirstNonEmpty(value);

  if (!raw) {
    return "-";
  }

  if (key !== "x-rate-limit-reset") {
    return raw;
  }

  const formattedDate = formatRateResetDate(raw);

  if (!formattedDate) {
    return raw;
  }

  const countdown = formatRateResetCountdown(raw);

  if (!countdown) {
    return `${raw} (${formattedDate})`;
  }

  return `${raw} (${formattedDate}; ${countdown})`;
}

export function formatMissingFields(missingFields: string[] | null | undefined): string {
  if (!missingFields || missingFields.length === 0) {
    return "-";
  }

  return missingFields.join(", ");
}

export function formatAgeFromIso(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  const millis = date.getTime();

  if (Number.isNaN(millis)) {
    return null;
  }

  const diff = Date.now() - millis;

  if (diff < 0) {
    return `in ${formatDurationFromMs(Math.abs(diff))}`;
  }

  return `${formatDurationFromMs(diff)} ago`;
}
