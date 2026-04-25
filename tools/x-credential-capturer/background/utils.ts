import type { CapturedQuery, JsonObject, QueryData } from "../popup/models.js";

export function nowIso(): string {
  return new Date().toISOString();
}

export function isPlainObject(value: unknown): value is JsonObject {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

export function toStringMapFromObject(value: unknown): Record<string, string> {
  if (!isPlainObject(value)) {
    return {};
  }

  const map: Record<string, string> = {};

  for (const [key, entryValue] of Object.entries(value)) {
    if (typeof entryValue === "string") {
      map[key] = entryValue;
    }
  }

  return map;
}

export function normalizeQueryState(query: unknown): CapturedQuery {
  const source = isPlainObject(query) ? query : {};

  return {
    Variables: isPlainObject(source.Variables) ? source.Variables : {},
    Features: isPlainObject(source.Features) ? source.Features : {},
    FieldToggles: isPlainObject(source.FieldToggles) ? source.FieldToggles : {}
  };
}

export function parseOperation(detailsUrl: string): string | null {
  try {
    const url = new URL(detailsUrl);
    const parts = url.pathname.split("/").filter(Boolean);

    if (parts.length < 2) {
      return null;
    }

    return parts[parts.length - 1];
  } catch (_) {
    return null;
  }
}

export function parseQueryId(detailsUrl: string): string | null {
  try {
    const url = new URL(detailsUrl);
    const parts = url.pathname.split("/").filter(Boolean);

    if (parts.length < 2) {
      return null;
    }

    return parts[parts.length - 2];
  } catch (_) {
    return null;
  }
}

function parseObjectParam(
  searchParams: URLSearchParams,
  key: string
): { found: boolean; value: QueryData } {
  const raw = searchParams.get(key);

  if (raw === null) {
    return { found: false, value: {} };
  }

  try {
    const parsed = JSON.parse(raw);
    return {
      found: true,
      value: isPlainObject(parsed) ? parsed : {}
    };
  } catch (_) {
    return { found: true, value: {} };
  }
}

export function parseRequestQuery(detailsUrl: string): CapturedQuery | null {
  try {
    const url = new URL(detailsUrl);
    const variables = parseObjectParam(url.searchParams, "variables");
    const features = parseObjectParam(url.searchParams, "features");
    const fieldTogglesLower = parseObjectParam(url.searchParams, "fieldToggles");
    const fieldTogglesUpper = parseObjectParam(url.searchParams, "FieldToggles");
    const fieldTogglesFlat = parseObjectParam(url.searchParams, "fieldtoggles");

    let fieldToggles = fieldTogglesFlat;

    if (fieldTogglesUpper.found) {
      fieldToggles = fieldTogglesUpper;
    }

    if (fieldTogglesLower.found) {
      fieldToggles = fieldTogglesLower;
    }

    const foundAny = variables.found || features.found || fieldToggles.found;

    if (!foundAny) {
      return null;
    }

    return {
      Variables: variables.value,
      Features: features.value,
      FieldToggles: fieldToggles.value
    };
  } catch (_) {
    return null;
  }
}

export function toHeaderMap(
  headers: chrome.webRequest.HttpHeader[] | undefined
): Record<string, string> {
  const map: Record<string, string> = {};

  for (const header of headers || []) {
    const name = (header.name || "").toLowerCase().trim();
    const value = (header.value || "").trim();

    if (!name) {
      continue;
    }

    map[name] = value;
  }

  return map;
}

export function getCookieValue(
  cookieHeader: string | null | undefined,
  name: string
): string | null {
  if (!cookieHeader) {
    return null;
  }

  const pairs = cookieHeader.split(";").map((item: string) => item.trim());

  for (const pair of pairs) {
    const idx = pair.indexOf("=");

    if (idx <= 0) {
      continue;
    }

    const key = pair.slice(0, idx).trim();
    const value = pair.slice(idx + 1);

    if (key === name) {
      return value;
    }
  }

  return null;
}

export async function buildCookieHeader(): Promise<string | null> {
  try {
    const cookies = await chrome.cookies.getAll({ domain: "x.com" });

    if (!cookies || cookies.length === 0) {
      return null;
    }

    const sorted = [...cookies].sort((a, b) => a.name.localeCompare(b.name));
    return sorted.map((cookie) => `${cookie.name}=${cookie.value}`).join("; ");
  } catch (_) {
    return null;
  }
}
