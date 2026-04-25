import type { CapturedQuery, EndpointDefinition, JsonObject } from "./models.js";

export function pickFirstNonEmpty(...values: unknown[]): string | null {
  for (const value of values) {
    if (typeof value === "string" && value.trim()) {
      return value;
    }
  }

  return null;
}

export function parseCookieValue(
  cookieHeader: string | null | undefined,
  name: string
): string | null {
  if (!cookieHeader) {
    return null;
  }

  const parts = cookieHeader.split(";").map((item) => item.trim());
  for (const part of parts) {
    const idx = part.indexOf("=");

    if (idx <= 0) {
      continue;
    }

    const key = part.slice(0, idx).trim();
    const value = part.slice(idx + 1);

    if (key === name) {
      return value;
    }
  }

  return null;
}

export function isPlainObject(value: unknown): value is JsonObject {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

export function cloneObject(value: unknown): JsonObject {
  return isPlainObject(value) ? { ...value } : {};
}

export function hasKeys(value: unknown): boolean {
  return isPlainObject(value) && Object.keys(value).length > 0;
}

export function normalizeCapturedQuery(query: unknown): CapturedQuery {
  const source = isPlainObject(query) ? query : {};

  return {
    Variables: isPlainObject(source.Variables) ? source.Variables : {},
    Features: isPlainObject(source.Features) ? source.Features : {},
    FieldToggles: isPlainObject(source.FieldToggles) ? source.FieldToggles : {}
  };
}

export function hasAnyQueryData(query: unknown): boolean {
  const source = isPlainObject(query) ? query : {};

  return hasKeys(source.Variables) || hasKeys(source.Features) || hasKeys(source.FieldToggles);
}

export function normalizeUrlWithoutParams(value: unknown): string | null {
  const url = pickFirstNonEmpty(value);

  if (!url) {
    return null;
  }

  try {
    const parsed = new URL(url);
    return `${parsed.origin}${parsed.pathname}`;
  } catch (_) {
    return url.split("?")[0];
  }
}

export function getHeaderValueCaseInsensitive(headers: unknown, targetName: string): string | null {
  if (!isPlainObject(headers)) {
    return null;
  }

  const exact = pickFirstNonEmpty(headers[targetName]);

  if (exact) {
    return exact;
  }

  const lowerTarget = targetName.toLowerCase();
  for (const [key, value] of Object.entries(headers)) {
    if (key.toLowerCase() === lowerTarget) {
      const found = pickFirstNonEmpty(value);

      if (found) {
        return found;
      }
    }
  }

  return null;
}

export function normalizeUsername(value: unknown): string {
  if (typeof value !== "string") {
    return "";
  }

  let username = value.trim();

  if (!username) {
    return "";
  }

  username = username.replace(/^@+/, "");
  username = username.replace(/^https?:\/\/(?:www\.)?x\.com\//i, "");
  username = username.split(/[/?#]/)[0];

  return username.trim();
}

export function resolveEndpointPageUrl(
  endpoint: EndpointDefinition,
  username: unknown
): string | null {
  const directUrl = pickFirstNonEmpty(endpoint?.pageUrl);

  if (directUrl) {
    return directUrl;
  }

  const template = pickFirstNonEmpty(endpoint?.pageUrlTemplate);
  const normalizedUsername = normalizeUsername(username);

  if (!template || !normalizedUsername) {
    return null;
  }

  return template.replace("{username}", encodeURIComponent(normalizedUsername));
}

export function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value));
}

export function extractUsernameFromXUrl(value: unknown): string | null {
  const input = pickFirstNonEmpty(value);

  if (!input) {
    return null;
  }

  try {
    const url = new URL(input);

    if (!/(\.|^)x\.com$/i.test(url.hostname)) {
      return null;
    }

    const first = (url.pathname.split("/").filter(Boolean)[0] || "").trim();

    if (!first) {
      return null;
    }

    const reserved = new Set([
      "i",
      "home",
      "explore",
      "search",
      "settings",
      "notifications",
      "messages",
      "compose",
      "tos",
      "privacy"
    ]);

    if (reserved.has(first.toLowerCase())) {
      return null;
    }

    return normalizeUsername(first) || null;
  } catch (_) {
    return null;
  }
}
