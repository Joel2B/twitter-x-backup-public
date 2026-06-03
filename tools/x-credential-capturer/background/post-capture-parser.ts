import type {
  CapturedPostItem,
  ProcessedPost,
  ProcessedPostMedia,
  ProcessedPostProfile,
  ProcessedPostVariant
} from "./post-capture-types.js";
import { nowIso, isPlainObject } from "./utils.js";
import { normalizeHashtagToken } from "./post-capture-storage.js";

function normalizeResultNode(raw: unknown): Record<string, unknown> | null {
  if (!isPlainObject(raw)) {
    return null;
  }

  let current: Record<string, unknown> = raw;

  if (isPlainObject(current.tweet)) {
    current = current.tweet;
  }

  const legacy = isPlainObject(current.legacy) ? current.legacy : null;
  const retweetedStatusResult =
    legacy && isPlainObject(legacy.retweeted_status_result) ? legacy.retweeted_status_result : null;
  const retweetedResult =
    retweetedStatusResult && isPlainObject(retweetedStatusResult.result)
      ? retweetedStatusResult.result
      : null;

  if (retweetedResult) {
    current = retweetedResult;
  }

  if (isPlainObject(current.tweet)) {
    current = current.tweet;
  }

  if (!isPlainObject(current.legacy)) {
    return null;
  }

  return current;
}

function collectResultNodes(input: unknown, output: Record<string, unknown>[]): void {
  if (Array.isArray(input)) {
    for (const entry of input) {
      collectResultNodes(entry, output);
    }

    return;
  }

  if (!isPlainObject(input)) {
    return;
  }

  const normalized = normalizeResultNode(input);
  const legacy = normalized && isPlainObject(normalized.legacy) ? normalized.legacy : null;
  const legacyId = legacy && typeof legacy.id_str === "string" ? legacy.id_str : null;

  if (normalized && legacyId) {
    output.push(normalized);
  }

  for (const value of Object.values(input)) {
    if (typeof value === "object" && value !== null) {
      collectResultNodes(value, output);
    }
  }
}

function asString(value: unknown): string | null {
  return typeof value === "string" && value.trim() ? value : null;
}

function asBoolean(value: unknown): boolean {
  return value === true;
}

function toProcessedMedia(value: unknown): ProcessedPostMedia | null {
  if (!isPlainObject(value)) {
    return null;
  }

  const id = asString(value.id_str);
  const url = asString(value.media_url_https);
  const type = asString(value.type);

  if (!id || !url || !type) {
    return null;
  }

  const videoInfo = isPlainObject(value.video_info) ? value.video_info : null;
  const variantValues = videoInfo && Array.isArray(videoInfo.variants) ? videoInfo.variants : [];
  const variants = variantValues
    .map((variantRaw): ProcessedPostVariant | null => {
      if (!isPlainObject(variantRaw)) {
        return null;
      }

      const contentType = asString(variantRaw.content_type);
      const variantUrl = asString(variantRaw.url);

      if (!contentType || !variantUrl) {
        return null;
      }

      return {
        contentType,
        bitrate: typeof variantRaw.bitrate === "number" ? variantRaw.bitrate : null,
        url: variantUrl
      };
    })
    .filter((entry): entry is ProcessedPostVariant => entry !== null);

  return {
    id,
    type,
    url,
    videoInfo:
      videoInfo !== null
        ? {
            durationMilis:
              typeof videoInfo.duration_millis === "number" ? videoInfo.duration_millis : null,
            variants: variants.length > 0 ? variants : null
          }
        : null
  };
}

function toCapturedPostItem(
  result: Record<string, unknown>,
  operation: string
): CapturedPostItem | null {
  const legacy = isPlainObject(result.legacy) ? result.legacy : null;

  if (!legacy) {
    return null;
  }

  const id = asString(legacy.id_str);
  const description = asString(legacy.full_text);
  const createdAt = asString(legacy.created_at);

  if (!id || description === null || !createdAt) {
    return null;
  }

  const core = isPlainObject(result.core) ? result.core : {};
  const userResults = isPlainObject(core.user_results) ? core.user_results : {};
  const userResult = isPlainObject(userResults.result) ? userResults.result : {};
  const userLegacy = isPlainObject(userResult.legacy) ? userResult.legacy : {};

  const profileId =
    asString(legacy.user_id_str) ||
    asString(userResult.rest_id) ||
    asString(userLegacy.id_str) ||
    "";

  if (!profileId) {
    return null;
  }

  const profile: ProcessedPostProfile = {
    id: profileId,
    userName: asString(core.screen_name) || asString(userLegacy.screen_name),
    name: asString(core.name) || asString(userLegacy.name),
    bannerUrl: asString(userLegacy.profile_banner_url),
    imageUrl: asString(userLegacy.profile_image_url_https) || asString(userResult.avatar_image_url),
    following: typeof userLegacy.following === "boolean" ? userLegacy.following : null
  };

  const entities = isPlainObject(legacy.entities) ? legacy.entities : {};
  const hashtagsRaw = Array.isArray(entities.hashtags) ? entities.hashtags : [];
  const hashtags = hashtagsRaw
    .map((entry) => (isPlainObject(entry) ? asString(entry.text) : null))
    .filter((entry): entry is string => entry !== null);

  const extendedEntities = isPlainObject(legacy.extended_entities) ? legacy.extended_entities : {};
  const mediaSource = Array.isArray(extendedEntities.media)
    ? extendedEntities.media
    : Array.isArray(entities.media)
      ? entities.media
      : [];

  const medias = mediaSource
    .map((entry) => toProcessedMedia(entry))
    .filter((entry): entry is ProcessedPostMedia => entry !== null);

  const processed: ProcessedPost = {
    id,
    profile,
    description,
    retweeted: asBoolean(legacy.retweeted),
    favorited: asBoolean(legacy.favorited),
    bookmarked: asBoolean(legacy.bookmarked),
    createdAt,
    hashtags: hashtags.length > 0 ? hashtags : null,
    medias: medias.length > 0 ? medias : null,
    deleted: false
  };

  const postUrl =
    profile.userName && id
      ? `https://x.com/${encodeURIComponent(profile.userName)}/status/${id}`
      : null;

  const previewText = description.trim() ? description : null;
  const mediaUrls = medias.map((media) => media.url);
  const capturedAt = nowIso();

  return {
    id,
    operation,
    capturedAt,
    lastSeenAt: capturedAt,
    uploadedAt: null,
    text: previewText,
    mediaUrls,
    authorUserName: profile.userName || null,
    authorName: profile.name || null,
    authorId: profile.id,
    postUrl,
    processed
  };
}

export function parseCapturedPosts(operation: string, body: string): CapturedPostItem[] {
  let parsed: unknown;

  try {
    parsed = JSON.parse(body);
  } catch (_error) {
    return [];
  }

  const nodes: Record<string, unknown>[] = [];
  collectResultNodes(parsed, nodes);

  const byId = new Map<string, CapturedPostItem>();

  for (const node of nodes) {
    const item = toCapturedPostItem(node, operation);

    if (!item) {
      continue;
    }

    byId.set(item.id, item);
  }

  return [...byId.values()];
}

export function postMatchesCaptureHashtags(
  item: CapturedPostItem,
  captureHashtags: Set<string>
): boolean {
  if (captureHashtags.size === 0) {
    return true;
  }

  const hashtags = item.processed.hashtags || [];

  for (const hashtag of hashtags) {
    const normalized = normalizeHashtagToken(hashtag);

    if (normalized && captureHashtags.has(normalized)) {
      return true;
    }
  }

  return false;
}
