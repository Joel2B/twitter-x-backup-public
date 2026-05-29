import type {
  CapturedPostItem,
  CapturedPostsStore,
  ProcessedPost,
  ProcessedPostMedia,
  ProcessedPostProfile,
  ProcessedPostVariant,
  UploadNotificationItem,
  UploadNotificationsStore,
  UploadCapturedPostsSummary,
  UploadCapturedPostsMessageResponse
} from "../popup/models.js";
import {
  CAPTURED_POSTS_STORAGE_KEY,
  UPLOAD_NOTIFICATIONS_STORAGE_KEY,
  DEFAULT_UPLOAD_API_BASE_URL,
  DEFAULT_UPLOAD_ORIGIN
} from "./constants.js";
import { isPlainObject, nowIso, parseOperation } from "./utils.js";

type CaptureResponseBodyPayload = {
  url: string;
  status?: number;
  body: string;
  capturedAt?: string;
};

type UploadApiDiagnosticsPayload = {
  beforeCount: number | null;
  afterCount: number | null;
  deltaCount: number | null;
  ignoredPosts: number | null;
  durationMs: number | null;
};

type UploadApiResponsePayload = {
  receivedPosts: number | null;
  savedPosts: number | null;
  diagnostics: UploadApiDiagnosticsPayload | null;
};

const UPLOAD_NOTIFICATIONS_LIMIT = 200;

function isSupportedOperation(operation: string | null): boolean {
  if (!operation) {
    return false;
  }

  return operation.toLowerCase().includes("searchtimeline");
}

function defaultCapturedPostsStore(): CapturedPostsStore {
  return {
    updatedAt: nowIso(),
    apiBaseUrl: DEFAULT_UPLOAD_API_BASE_URL,
    uploadUserId: "",
    uploadOrigin: DEFAULT_UPLOAD_ORIGIN,
    captureHashtags: [],
    items: {}
  };
}

function normalizeApiBaseUrl(value: unknown): string {
  if (typeof value !== "string") {
    return DEFAULT_UPLOAD_API_BASE_URL;
  }

  const trimmed = value.trim();

  if (!trimmed) {
    return DEFAULT_UPLOAD_API_BASE_URL;
  }

  return trimmed.replace(/\/+$/, "");
}

function normalizeUploadOrigin(value: unknown): string {
  if (typeof value !== "string") {
    return DEFAULT_UPLOAD_ORIGIN;
  }

  const trimmed = value.trim();
  return trimmed || DEFAULT_UPLOAD_ORIGIN;
}

function normalizeUploadUserId(value: unknown): string {
  if (typeof value !== "string") {
    return "";
  }

  return value.trim();
}

function normalizeHashtagToken(value: unknown): string | null {
  if (typeof value !== "string") {
    return null;
  }

  const normalized = value.trim().replace(/^#+/, "").toLowerCase();
  return normalized || null;
}

function toNullableNumber(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function readProperty(source: unknown, camelName: string, pascalName: string): unknown {
  if (!isPlainObject(source)) {
    return null;
  }

  return source[camelName] ?? source[pascalName] ?? null;
}

function parseUploadApiResponse(value: unknown): UploadApiResponsePayload | null {
  if (!isPlainObject(value)) {
    return null;
  }

  const diagnosticsSource = readProperty(value, "diagnostics", "Diagnostics");
  const diagnostics: UploadApiDiagnosticsPayload | null = isPlainObject(diagnosticsSource)
    ? {
        beforeCount: toNullableNumber(readProperty(diagnosticsSource, "beforeCount", "BeforeCount")),
        afterCount: toNullableNumber(readProperty(diagnosticsSource, "afterCount", "AfterCount")),
        deltaCount: toNullableNumber(readProperty(diagnosticsSource, "deltaCount", "DeltaCount")),
        ignoredPosts: toNullableNumber(readProperty(diagnosticsSource, "ignoredPosts", "IgnoredPosts")),
        durationMs: toNullableNumber(readProperty(diagnosticsSource, "durationMs", "DurationMs"))
      }
    : null;

  return {
    receivedPosts: toNullableNumber(readProperty(value, "receivedPosts", "ReceivedPosts")),
    savedPosts: toNullableNumber(readProperty(value, "savedPosts", "SavedPosts")),
    diagnostics
  };
}

function buildUploadSummary(
  attemptedPosts: number,
  apiResult: UploadApiResponsePayload | null
): UploadCapturedPostsSummary {
  const diagnostics = apiResult?.diagnostics;

  return {
    attemptedPosts,
    receivedPosts: apiResult?.receivedPosts ?? null,
    savedPosts: apiResult?.savedPosts ?? null,
    ignoredPosts: diagnostics?.ignoredPosts ?? null,
    beforeCount: diagnostics?.beforeCount ?? null,
    afterCount: diagnostics?.afterCount ?? null,
    deltaCount: diagnostics?.deltaCount ?? null,
    durationMs: diagnostics?.durationMs ?? null
  };
}

function normalizeUploadSummary(
  value: unknown,
  attemptedFallback: number
): UploadCapturedPostsSummary | null {
  if (!isPlainObject(value)) {
    return null;
  }

  const attemptedPosts =
    typeof value.attemptedPosts === "number" ? value.attemptedPosts : attemptedFallback;

  return {
    attemptedPosts,
    receivedPosts: toNullableNumber(value.receivedPosts),
    savedPosts: toNullableNumber(value.savedPosts),
    ignoredPosts: toNullableNumber(value.ignoredPosts),
    beforeCount: toNullableNumber(value.beforeCount),
    afterCount: toNullableNumber(value.afterCount),
    deltaCount: toNullableNumber(value.deltaCount),
    durationMs: toNullableNumber(value.durationMs)
  };
}

function normalizeCaptureHashtags(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  const deduped = new Set<string>();

  for (const entry of value) {
    const normalized = normalizeHashtagToken(entry);

    if (!normalized) {
      continue;
    }

    deduped.add(normalized);
  }

  return [...deduped];
}

function normalizeCapturedPostItem(value: unknown): CapturedPostItem | null {
  if (!isPlainObject(value)) {
    return null;
  }

  const id = typeof value.id === "string" ? value.id : null;

  if (!id) {
    return null;
  }

  const processed = normalizeProcessedPost(value.processed);

  if (!processed) {
    return null;
  }

  return {
    id,
    operation: typeof value.operation === "string" ? value.operation : "unknown",
    capturedAt: typeof value.capturedAt === "string" ? value.capturedAt : nowIso(),
    lastSeenAt: typeof value.lastSeenAt === "string" ? value.lastSeenAt : nowIso(),
    uploadedAt: typeof value.uploadedAt === "string" ? value.uploadedAt : null,
    text: typeof value.text === "string" && value.text.trim() ? value.text : null,
    mediaUrls: Array.isArray(value.mediaUrls)
      ? value.mediaUrls.filter((entry): entry is string => typeof entry === "string")
      : [],
    authorUserName:
      typeof value.authorUserName === "string" && value.authorUserName.trim()
        ? value.authorUserName
        : null,
    authorName:
      typeof value.authorName === "string" && value.authorName.trim() ? value.authorName : null,
    authorId: typeof value.authorId === "string" ? value.authorId : "",
    postUrl: typeof value.postUrl === "string" && value.postUrl.trim() ? value.postUrl : null,
    processed
  };
}

function normalizeProcessedPost(value: unknown): ProcessedPost | null {
  if (!isPlainObject(value)) {
    return null;
  }

  const id = typeof value.id === "string" ? value.id : null;
  const description = typeof value.description === "string" ? value.description : null;
  const createdAt = typeof value.createdAt === "string" ? value.createdAt : null;
  const profile = normalizeProcessedProfile(value.profile);

  if (!id || !description || !createdAt || !profile) {
    return null;
  }

  return {
    id,
    profile,
    description,
    retweeted: value.retweeted === true,
    favorited: value.favorited === true,
    bookmarked: value.bookmarked === true,
    createdAt,
    hashtags: Array.isArray(value.hashtags)
      ? value.hashtags.filter((entry): entry is string => typeof entry === "string")
      : null,
    medias: Array.isArray(value.medias)
      ? value.medias
          .map((entry) => normalizeProcessedMedia(entry))
          .filter((entry): entry is ProcessedPostMedia => entry !== null)
      : null,
    deleted: value.deleted === true
  };
}

function normalizeProcessedProfile(value: unknown): ProcessedPostProfile | null {
  if (!isPlainObject(value) || typeof value.id !== "string") {
    return null;
  }

  return {
    id: value.id,
    userName: typeof value.userName === "string" ? value.userName : null,
    name: typeof value.name === "string" ? value.name : null,
    bannerUrl: typeof value.bannerUrl === "string" ? value.bannerUrl : null,
    imageUrl: typeof value.imageUrl === "string" ? value.imageUrl : null,
    following: typeof value.following === "boolean" ? value.following : null
  };
}

function normalizeProcessedMedia(value: unknown): ProcessedPostMedia | null {
  if (!isPlainObject(value)) {
    return null;
  }

  if (
    typeof value.id !== "string" ||
    typeof value.url !== "string" ||
    typeof value.type !== "string"
  ) {
    return null;
  }

  const variantsRaw =
    isPlainObject(value.videoInfo) && Array.isArray(value.videoInfo.variants)
      ? value.videoInfo.variants
      : [];
  const variants = variantsRaw
    .map((entry) => normalizeProcessedVariant(entry))
    .filter((entry): entry is ProcessedPostVariant => entry !== null);

  return {
    id: value.id,
    url: value.url,
    type: value.type,
    videoInfo: isPlainObject(value.videoInfo)
      ? {
          durationMilis:
            typeof value.videoInfo.durationMilis === "number"
              ? value.videoInfo.durationMilis
              : null,
          variants: variants.length > 0 ? variants : null
        }
      : null
  };
}

function normalizeProcessedVariant(value: unknown): ProcessedPostVariant | null {
  if (!isPlainObject(value)) {
    return null;
  }

  if (typeof value.contentType !== "string" || typeof value.url !== "string") {
    return null;
  }

  return {
    contentType: value.contentType,
    bitrate: typeof value.bitrate === "number" ? value.bitrate : null,
    url: value.url
  };
}

export async function getCapturedPostsStore(): Promise<CapturedPostsStore> {
  const raw = await chrome.storage.local.get(CAPTURED_POSTS_STORAGE_KEY);
  const source = raw?.[CAPTURED_POSTS_STORAGE_KEY];

  if (!isPlainObject(source)) {
    return defaultCapturedPostsStore();
  }

  const itemsSource = isPlainObject(source.items) ? source.items : {};
  const items: Record<string, CapturedPostItem> = {};

  for (const [id, itemRaw] of Object.entries(itemsSource)) {
    const item = normalizeCapturedPostItem(itemRaw);

    if (!item) {
      continue;
    }

    items[id] = item;
  }

  return {
    updatedAt: typeof source.updatedAt === "string" ? source.updatedAt : nowIso(),
    apiBaseUrl: normalizeApiBaseUrl(source.apiBaseUrl),
    uploadUserId: normalizeUploadUserId(source.uploadUserId),
    uploadOrigin: normalizeUploadOrigin(source.uploadOrigin),
    captureHashtags: normalizeCaptureHashtags(source.captureHashtags),
    items
  };
}

async function saveCapturedPostsStore(store: CapturedPostsStore): Promise<CapturedPostsStore> {
  const normalized: CapturedPostsStore = {
    ...store,
    updatedAt: nowIso(),
    apiBaseUrl: normalizeApiBaseUrl(store.apiBaseUrl),
    uploadUserId: normalizeUploadUserId(store.uploadUserId),
    uploadOrigin: normalizeUploadOrigin(store.uploadOrigin),
    captureHashtags: normalizeCaptureHashtags(store.captureHashtags)
  };

  await chrome.storage.local.set({
    [CAPTURED_POSTS_STORAGE_KEY]: normalized
  });

  return normalized;
}

function defaultUploadNotificationsStore(): UploadNotificationsStore {
  return {
    updatedAt: nowIso(),
    items: []
  };
}

function normalizeUploadNotificationStatus(value: unknown): UploadNotificationItem["status"] {
  if (value === "running" || value === "completed" || value === "failed") {
    return value;
  }

  return "failed";
}

function normalizeUploadNotificationItem(value: unknown): UploadNotificationItem | null {
  if (!isPlainObject(value) || typeof value.id !== "string") {
    return null;
  }

  return {
    id: value.id,
    status: normalizeUploadNotificationStatus(value.status),
    createdAt: typeof value.createdAt === "string" ? value.createdAt : nowIso(),
    startedAt: typeof value.startedAt === "string" ? value.startedAt : nowIso(),
    completedAt: typeof value.completedAt === "string" ? value.completedAt : null,
    attemptedPosts: typeof value.attemptedPosts === "number" ? value.attemptedPosts : 0,
    uploadedPosts: typeof value.uploadedPosts === "number" ? value.uploadedPosts : 0,
    failedPosts: typeof value.failedPosts === "number" ? value.failedPosts : 0,
    apiBaseUrl: typeof value.apiBaseUrl === "string" ? value.apiBaseUrl : DEFAULT_UPLOAD_API_BASE_URL,
    uploadUserId: typeof value.uploadUserId === "string" ? value.uploadUserId : "",
    uploadOrigin: typeof value.uploadOrigin === "string" ? value.uploadOrigin : DEFAULT_UPLOAD_ORIGIN,
    uploadSummary: normalizeUploadSummary(
      value.uploadSummary,
      typeof value.attemptedPosts === "number" ? value.attemptedPosts : 0
    ),
    error: typeof value.error === "string" ? value.error : null
  };
}

function normalizeUploadNotificationsItems(items: unknown): UploadNotificationItem[] {
  if (!Array.isArray(items)) {
    return [];
  }

  return items
    .map((item) => normalizeUploadNotificationItem(item))
    .filter((item): item is UploadNotificationItem => item !== null)
    .slice(0, UPLOAD_NOTIFICATIONS_LIMIT);
}

export async function getUploadNotificationsStore(): Promise<UploadNotificationsStore> {
  const raw = await chrome.storage.local.get(UPLOAD_NOTIFICATIONS_STORAGE_KEY);
  const source = raw?.[UPLOAD_NOTIFICATIONS_STORAGE_KEY];

  if (!isPlainObject(source)) {
    return defaultUploadNotificationsStore();
  }

  return {
    updatedAt: typeof source.updatedAt === "string" ? source.updatedAt : nowIso(),
    items: normalizeUploadNotificationsItems(source.items)
  };
}

async function saveUploadNotificationsStore(
  store: UploadNotificationsStore
): Promise<UploadNotificationsStore> {
  const normalized: UploadNotificationsStore = {
    updatedAt: nowIso(),
    items: normalizeUploadNotificationsItems(store.items)
  };

  await chrome.storage.local.set({
    [UPLOAD_NOTIFICATIONS_STORAGE_KEY]: normalized
  });

  return normalized;
}

function createUploadNotificationId(): string {
  const random = Math.random().toString(36).slice(2, 10);
  return `upload-${Date.now()}-${random}`;
}

async function createUploadNotification(input: {
  attemptedPosts: number;
  apiBaseUrl: string;
  uploadUserId: string;
  uploadOrigin: string;
}): Promise<UploadNotificationItem> {
  const store = await getUploadNotificationsStore();
  const startedAt = nowIso();
  const item: UploadNotificationItem = {
    id: createUploadNotificationId(),
    status: "running",
    createdAt: startedAt,
    startedAt,
    completedAt: null,
    attemptedPosts: input.attemptedPosts,
    uploadedPosts: 0,
    failedPosts: 0,
    apiBaseUrl: normalizeApiBaseUrl(input.apiBaseUrl),
    uploadUserId: normalizeUploadUserId(input.uploadUserId),
    uploadOrigin: normalizeUploadOrigin(input.uploadOrigin),
    uploadSummary: null,
    error: null
  };

  const nextItems = [item, ...store.items].slice(0, UPLOAD_NOTIFICATIONS_LIMIT);
  await saveUploadNotificationsStore({
    ...store,
    items: nextItems
  });

  return item;
}

async function patchUploadNotification(
  id: string,
  patch: Partial<Omit<UploadNotificationItem, "id" | "createdAt" | "startedAt">>
): Promise<void> {
  const store = await getUploadNotificationsStore();
  let changed = false;

  const nextItems = store.items.map((item) => {
    if (item.id !== id) {
      return item;
    }

    changed = true;
    return {
      ...item,
      ...patch
    };
  });

  if (!changed) {
    return;
  }

  await saveUploadNotificationsStore({
    ...store,
    items: nextItems
  });
}

export async function clearUploadNotifications(): Promise<UploadNotificationsStore> {
  return saveUploadNotificationsStore(defaultUploadNotificationsStore());
}

export async function setUploadTarget(input: {
  apiBaseUrl?: string;
  uploadUserId?: string;
  uploadOrigin?: string;
  captureHashtags?: string[];
}): Promise<CapturedPostsStore> {
  const store = await getCapturedPostsStore();

  const next: CapturedPostsStore = {
    ...store,
    apiBaseUrl:
      typeof input.apiBaseUrl === "string"
        ? normalizeApiBaseUrl(input.apiBaseUrl)
        : store.apiBaseUrl,
    uploadUserId:
      typeof input.uploadUserId === "string"
        ? normalizeUploadUserId(input.uploadUserId)
        : store.uploadUserId,
    uploadOrigin:
      typeof input.uploadOrigin === "string"
        ? normalizeUploadOrigin(input.uploadOrigin)
        : store.uploadOrigin,
    captureHashtags: Array.isArray(input.captureHashtags)
      ? normalizeCaptureHashtags(input.captureHashtags)
      : store.captureHashtags
  };

  return saveCapturedPostsStore(next);
}

export async function clearUploadedCapturedPosts(): Promise<CapturedPostsStore> {
  const store = await getCapturedPostsStore();
  const items: Record<string, CapturedPostItem> = {};

  for (const [id, item] of Object.entries(store.items)) {
    if (item.uploadedAt) {
      continue;
    }

    items[id] = item;
  }

  return saveCapturedPostsStore({
    ...store,
    items
  });
}

export async function resetCapturedPostsUploadStatus(): Promise<CapturedPostsStore> {
  const store = await getCapturedPostsStore();
  const items: Record<string, CapturedPostItem> = {};

  for (const [id, item] of Object.entries(store.items)) {
    items[id] = {
      ...item,
      uploadedAt: null
    };
  }

  return saveCapturedPostsStore({
    ...store,
    items
  });
}

function parseIsoTime(value: string | null | undefined): number {
  if (!value) {
    return 0;
  }

  const epoch = new Date(value).getTime();
  return Number.isFinite(epoch) ? epoch : 0;
}

function takeLatestIso(a: string | null | undefined, b: string | null | undefined): string {
  const aEpoch = parseIsoTime(a);
  const bEpoch = parseIsoTime(b);
  return bEpoch > aEpoch ? b || nowIso() : a || b || nowIso();
}

function extractImportedItems(payload: unknown): CapturedPostItem[] {
  if (Array.isArray(payload)) {
    return payload
      .map((entry) => normalizeCapturedPostItem(entry))
      .filter((entry): entry is CapturedPostItem => entry !== null);
  }

  if (!isPlainObject(payload)) {
    return [];
  }

  const sourceItems =
    isPlainObject(payload.items) && Object.keys(payload.items).length > 0
      ? Object.values(payload.items)
      : isPlainObject(payload.posts) && Object.keys(payload.posts).length > 0
        ? Object.values(payload.posts)
        : [];

  return sourceItems
    .map((entry) => normalizeCapturedPostItem(entry))
    .filter((entry): entry is CapturedPostItem => entry !== null);
}

export async function importCapturedPosts(payload: unknown): Promise<CapturedPostsStore> {
  const importedItems = extractImportedItems(payload);
  const current = await getCapturedPostsStore();

  if (importedItems.length === 0) {
    return current;
  }

  const nextItems: Record<string, CapturedPostItem> = { ...current.items };

  for (const item of importedItems) {
    const existing = nextItems[item.id];

    if (!existing) {
      nextItems[item.id] = item;
      continue;
    }

    nextItems[item.id] = {
      ...item,
      capturedAt: takeLatestIso(existing.capturedAt, item.capturedAt),
      lastSeenAt: takeLatestIso(existing.lastSeenAt, item.lastSeenAt),
      uploadedAt: existing.uploadedAt || item.uploadedAt || null
    };
  }

  return saveCapturedPostsStore({
    ...current,
    items: nextItems
  });
}

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

function parseCapturedPosts(operation: string, body: string): CapturedPostItem[] {
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

function postMatchesCaptureHashtags(item: CapturedPostItem, captureHashtags: Set<string>): boolean {
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

export async function capturePostsFromGraphqlResponseBody(
  payload: CaptureResponseBodyPayload
): Promise<CapturedPostsStore> {
  const operation = parseOperation(payload.url);

  if (!isSupportedOperation(operation)) {
    if (operation) {
      console.info("[XCC] skip operation:", operation);
    }
    return getCapturedPostsStore();
  }
  const operationName = operation as string;

  if (!payload.body || payload.body.length === 0) {
    console.info("[XCC] empty response body for operation:", operation);
    return getCapturedPostsStore();
  }

  const parsedItems = parseCapturedPosts(operationName, payload.body);
  const store = await getCapturedPostsStore();

  if (parsedItems.length === 0) {
    console.info("[XCC] parsed 0 posts from operation:", operationName, payload.url);
    return store;
  }

  const captureHashtags = new Set(normalizeCaptureHashtags(store.captureHashtags));
  const filteredItems = parsedItems.filter((item) =>
    postMatchesCaptureHashtags(item, captureHashtags)
  );

  if (filteredItems.length === 0) {
    console.info(
      "[XCC] parsed posts ignored by hashtag filter:",
      parsedItems.length,
      "operation:",
      operationName
    );
    return store;
  }

  const nextItems: Record<string, CapturedPostItem> = { ...store.items };
  const seenAt =
    typeof payload.capturedAt === "string" && payload.capturedAt ? payload.capturedAt : nowIso();

  for (const parsed of filteredItems) {
    const existing = nextItems[parsed.id];

    nextItems[parsed.id] = {
      ...parsed,
      capturedAt: existing?.capturedAt || parsed.capturedAt,
      lastSeenAt: seenAt,
      uploadedAt: existing?.uploadedAt || null
    };
  }

  const autoUserId = store.uploadUserId || filteredItems[0]?.authorId || "";
  console.info(
    "[XCC] parsed posts (matched):",
    filteredItems.length,
    "of",
    parsedItems.length,
    "operation:",
    operationName,
    "store total after merge:",
    Object.keys(nextItems).length
  );

  return saveCapturedPostsStore({
    ...store,
    uploadUserId: autoUserId,
    items: nextItems
  });
}

export async function uploadCapturedPosts(
  ids: string[]
): Promise<UploadCapturedPostsMessageResponse> {
  const store = await getCapturedPostsStore();
  const uniqueIds = [...new Set(ids.filter((id) => typeof id === "string" && id.trim()))];
  const candidates = uniqueIds
    .map((id) => store.items[id])
    .filter((item): item is CapturedPostItem => Boolean(item && !item.uploadedAt));

  if (candidates.length === 0) {
    return { ok: true, store, uploaded: [], failed: [], uploadSummary: null };
  }

  if (!store.uploadUserId) {
    return { ok: false, error: "Upload userId is required." };
  }

  const endpointUrl =
    `${normalizeApiBaseUrl(store.apiBaseUrl)}/api/posts/processed` +
    `?userId=${encodeURIComponent(store.uploadUserId)}` +
    `&origin=${encodeURIComponent(store.uploadOrigin)}`;
  const notification = await createUploadNotification({
    attemptedPosts: candidates.length,
    apiBaseUrl: store.apiBaseUrl,
    uploadUserId: store.uploadUserId,
    uploadOrigin: store.uploadOrigin
  });
  const startedAtEpoch = Date.now();
  let response: Response;

  try {
    response = await fetch(endpointUrl, {
      method: "POST",
      headers: {
        "content-type": "application/json"
      },
      body: JSON.stringify(candidates.map((item) => item.processed))
    });
  } catch (error) {
    await patchUploadNotification(notification.id, {
      status: "failed",
      completedAt: nowIso(),
      uploadedPosts: 0,
      failedPosts: candidates.length,
      error: error instanceof Error ? error.message : "Upload request failed."
    });
    return {
      ok: false,
      error: error instanceof Error ? error.message : "Upload request failed."
    };
  }

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    const message = text?.trim() || `HTTP ${response.status}`;
    await patchUploadNotification(notification.id, {
      status: "failed",
      completedAt: nowIso(),
      uploadedPosts: 0,
      failedPosts: candidates.length,
      error: `Upload failed: ${message}`
    });
    return { ok: false, error: `Upload failed: ${message}` };
  }

  const apiPayload = await response.json().catch(() => null);
  const apiResult = parseUploadApiResponse(apiPayload);
  const uploadSummary = buildUploadSummary(candidates.length, apiResult);

  const uploadedAt = nowIso();
  const nextStore: CapturedPostsStore = {
    ...store,
    items: { ...store.items }
  };

  for (const item of candidates) {
    const current = nextStore.items[item.id];

    if (!current) {
      continue;
    }

    nextStore.items[item.id] = {
      ...current,
      uploadedAt
    };
  }

  const savedStore = await saveCapturedPostsStore(nextStore);
  const finishedAt = nowIso();
  const durationMs = Date.now() - startedAtEpoch;
  const finalSummary = uploadSummary
    ? {
        ...uploadSummary,
        durationMs: uploadSummary.durationMs ?? durationMs
      }
    : null;
  await patchUploadNotification(notification.id, {
    status: "completed",
    completedAt: finishedAt,
    uploadedPosts: candidates.length,
    failedPosts: 0,
    uploadSummary: finalSummary,
    error: null
  });
  return {
    ok: true,
    store: savedStore,
    uploaded: candidates.map((item) => item.id),
    failed: [],
    uploadSummary
  };
}
