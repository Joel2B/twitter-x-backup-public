import type {
  CapturedPostItem,
  CapturedPostsStore,
  ProcessedPost,
  ProcessedPostMedia,
  ProcessedPostProfile,
  ProcessedPostVariant,
  UploadApiDiagnosticsPayload,
  UploadApiResponsePayload,
  UploadCapturedPostsSummary,
  UploadNotificationItem,
  UploadNotificationsStore
} from "./post-capture-types.js";
import {
  CAPTURED_POSTS_UPDATE_SIGNAL_STORAGE_KEY,
  DEFAULT_UPLOAD_API_BASE_URL,
  DEFAULT_UPLOAD_ORIGIN,
  UPLOAD_NOTIFICATIONS_STORAGE_KEY,
  UPLOAD_NOTIFICATION_EXPIRE_AFTER_MS,
  UPLOAD_REQUEST_TIMEOUT_MS
} from "./constants.js";
import {
  deleteCapturedPostItems,
  getAllCapturedPostItems,
  getCapturedPostItemsByIds,
  getCapturedPostsMetaRecord,
  putCapturedPostsData,
  putCapturedPostsMetaRecord,
  type CapturedPostsMetaRecord
} from "./captured-posts-db.js";
import { isPlainObject, nowIso } from "./utils.js";

const UPLOAD_NOTIFICATIONS_LIMIT = 200;
const CAPTURED_POSTS_META_KEY = "store";

export function normalizeApiBaseUrl(value: unknown): string {
  if (typeof value !== "string") {
    return DEFAULT_UPLOAD_API_BASE_URL;
  }

  const trimmed = value.trim();

  if (!trimmed) {
    return DEFAULT_UPLOAD_API_BASE_URL;
  }

  return trimmed.replace(/\/+$/, "");
}

export function normalizeUploadOrigin(value: unknown): string {
  if (typeof value !== "string") {
    return DEFAULT_UPLOAD_ORIGIN;
  }

  const trimmed = value.trim();
  return trimmed || DEFAULT_UPLOAD_ORIGIN;
}

export function normalizeUploadUserId(value: unknown): string {
  if (typeof value !== "string") {
    return "";
  }

  return value.trim();
}

export function normalizeHashtagToken(value: unknown): string | null {
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

export function parseUploadApiResponse(value: unknown): UploadApiResponsePayload | null {
  if (!isPlainObject(value)) {
    return null;
  }

  const diagnosticsSource = readProperty(value, "diagnostics", "Diagnostics");
  const diagnostics: UploadApiDiagnosticsPayload | null = isPlainObject(diagnosticsSource)
    ? {
        beforeCount: toNullableNumber(
          readProperty(diagnosticsSource, "beforeCount", "BeforeCount")
        ),
        afterCount: toNullableNumber(readProperty(diagnosticsSource, "afterCount", "AfterCount")),
        deltaCount: toNullableNumber(readProperty(diagnosticsSource, "deltaCount", "DeltaCount")),
        ignoredPosts: toNullableNumber(
          readProperty(diagnosticsSource, "ignoredPosts", "IgnoredPosts")
        ),
        durationMs: toNullableNumber(readProperty(diagnosticsSource, "durationMs", "DurationMs"))
      }
    : null;

  return {
    receivedPosts: toNullableNumber(readProperty(value, "receivedPosts", "ReceivedPosts")),
    savedPosts: toNullableNumber(readProperty(value, "savedPosts", "SavedPosts")),
    diagnostics
  };
}

export function buildUploadSummary(
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

export function normalizeCaptureHashtags(value: unknown): string[] {
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

type CapturedPostsMeta = Omit<CapturedPostsStore, "items">;

function defaultCapturedPostsMeta(): CapturedPostsMeta {
  const store = defaultCapturedPostsStore();

  return {
    updatedAt: store.updatedAt,
    apiBaseUrl: store.apiBaseUrl,
    uploadUserId: store.uploadUserId,
    uploadOrigin: store.uploadOrigin,
    captureHashtags: store.captureHashtags
  };
}

function normalizeCapturedPostsMeta(source: unknown): CapturedPostsMeta {
  if (!isPlainObject(source))
    return defaultCapturedPostsMeta();

  return {
    updatedAt: typeof source.updatedAt === "string" ? source.updatedAt : nowIso(),
    apiBaseUrl: normalizeApiBaseUrl(source.apiBaseUrl),
    uploadUserId: normalizeUploadUserId(source.uploadUserId),
    uploadOrigin: normalizeUploadOrigin(source.uploadOrigin),
    captureHashtags: normalizeCaptureHashtags(source.captureHashtags)
  };
}

function toCapturedPostsMetaRecord(meta: CapturedPostsMeta): CapturedPostsMetaRecord {
  return {
    key: CAPTURED_POSTS_META_KEY,
    updatedAt: nowIso(),
    apiBaseUrl: normalizeApiBaseUrl(meta.apiBaseUrl),
    uploadUserId: normalizeUploadUserId(meta.uploadUserId),
    uploadOrigin: normalizeUploadOrigin(meta.uploadOrigin),
    captureHashtags: normalizeCaptureHashtags(meta.captureHashtags)
  };
}

function buildCapturedPostsStore(
  meta: CapturedPostsMeta,
  items: CapturedPostItem[]
): CapturedPostsStore {
  return {
    ...meta,
    items: Object.fromEntries(items.map((item) => [item.id, item]))
  };
}

async function notifyCapturedPostsUpdated(): Promise<void> {
  await chrome.storage.local.set({
    [CAPTURED_POSTS_UPDATE_SIGNAL_STORAGE_KEY]: nowIso()
  });
}

async function getCapturedPostsMeta(): Promise<CapturedPostsMeta> {
  const record = await getCapturedPostsMetaRecord();
  return record ? normalizeCapturedPostsMeta(record) : defaultCapturedPostsMeta();
}

export async function getCapturedPostsMetadata(): Promise<CapturedPostsMeta> {
  return getCapturedPostsMeta();
}

export async function getCapturedPostsStore(): Promise<CapturedPostsStore> {
  const [meta, items] = await Promise.all([getCapturedPostsMeta(), getAllCapturedPostItems()]);
  return buildCapturedPostsStore(meta, items);
}

export async function getCapturedPostsItemsMapByIds(
  ids: string[]
): Promise<Record<string, CapturedPostItem>> {
  return getCapturedPostItemsByIds(ids);
}

export async function upsertCapturedPosts(
  items: CapturedPostItem[],
  metaPatch?: Partial<Pick<CapturedPostsMeta, "uploadUserId">>
): Promise<void> {
  if (items.length === 0 && !metaPatch)
    return;

  const currentMeta = await getCapturedPostsMeta();
  const nextMeta = toCapturedPostsMetaRecord({
    ...currentMeta,
    uploadUserId:
      typeof metaPatch?.uploadUserId === "string"
        ? normalizeUploadUserId(metaPatch.uploadUserId)
        : currentMeta.uploadUserId
  });

  await putCapturedPostsData(nextMeta, items);
  await notifyCapturedPostsUpdated();
}

export async function markCapturedPostsUploaded(
  ids: string[],
  uploadedAt: string
): Promise<void> {
  const itemMap = await getCapturedPostItemsByIds(ids);
  const changedItems = Object.values(itemMap).map((item) => ({
    ...item,
    uploadedAt
  }));

  if (changedItems.length === 0)
    return;

  const currentMeta = await getCapturedPostsMeta();
  await putCapturedPostsData(toCapturedPostsMetaRecord(currentMeta), changedItems);
  await notifyCapturedPostsUpdated();
}

async function updateCapturedPostsMeta(meta: CapturedPostsMeta): Promise<void> {
  await putCapturedPostsMetaRecord(toCapturedPostsMetaRecord(meta));
  await notifyCapturedPostsUpdated();
}

function defaultUploadNotificationsStore(): UploadNotificationsStore {
  return {
    updatedAt: nowIso(),
    items: []
  };
}

function normalizeUploadNotificationStatus(value: unknown): UploadNotificationItem["status"] {
  if (value === "running" || value === "completed" || value === "failed" || value === "expired") {
    return value;
  }

  return "failed";
}

function buildExpiredNotificationError(startedAt: string): string {
  return `Upload expired after ${UPLOAD_REQUEST_TIMEOUT_MS} ms without a completion response (started ${startedAt}).`;
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
    apiBaseUrl:
      typeof value.apiBaseUrl === "string" ? value.apiBaseUrl : DEFAULT_UPLOAD_API_BASE_URL,
    uploadUserId: typeof value.uploadUserId === "string" ? value.uploadUserId : "",
    uploadOrigin:
      typeof value.uploadOrigin === "string" ? value.uploadOrigin : DEFAULT_UPLOAD_ORIGIN,
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

function expireStaleRunningNotifications(items: UploadNotificationItem[]): {
  changed: boolean;
  items: UploadNotificationItem[];
} {
  const nowEpoch = Date.now();
  let changed = false;

  const nextItems = items.map((item) => {
    if (item.status !== "running") {
      return item;
    }

    const startedAtEpoch = new Date(item.startedAt).getTime();

    if (!Number.isFinite(startedAtEpoch)) {
      return item;
    }

    if (nowEpoch - startedAtEpoch < UPLOAD_NOTIFICATION_EXPIRE_AFTER_MS) {
      return item;
    }

    changed = true;

    const expiredItem: UploadNotificationItem = {
      ...item,
      status: "expired",
      completedAt: item.completedAt || new Date(nowEpoch).toISOString(),
      failedPosts: Math.max(
        item.failedPosts,
        Math.max(0, item.attemptedPosts - item.uploadedPosts)
      ),
      error: item.error || buildExpiredNotificationError(item.startedAt)
    };

    return expiredItem;
  });

  return {
    changed,
    items: nextItems
  };
}

export async function getUploadNotificationsStore(): Promise<UploadNotificationsStore> {
  const raw = await chrome.storage.local.get(UPLOAD_NOTIFICATIONS_STORAGE_KEY);
  const source = raw?.[UPLOAD_NOTIFICATIONS_STORAGE_KEY];

  if (!isPlainObject(source)) {
    return defaultUploadNotificationsStore();
  }

  const store: UploadNotificationsStore = {
    updatedAt: typeof source.updatedAt === "string" ? source.updatedAt : nowIso(),
    items: normalizeUploadNotificationsItems(source.items)
  };

  const expired = expireStaleRunningNotifications(store.items);

  if (!expired.changed) {
    return store;
  }

  return saveUploadNotificationsStore({
    ...store,
    items: expired.items
  });
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

export async function createUploadNotification(input: {
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

export async function patchUploadNotification(
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
  const currentMeta = await getCapturedPostsMeta();
  const nextMeta: CapturedPostsMeta = {
    ...currentMeta,
    apiBaseUrl:
      typeof input.apiBaseUrl === "string"
        ? normalizeApiBaseUrl(input.apiBaseUrl)
        : currentMeta.apiBaseUrl,
    uploadUserId:
      typeof input.uploadUserId === "string"
        ? normalizeUploadUserId(input.uploadUserId)
        : currentMeta.uploadUserId,
    uploadOrigin:
      typeof input.uploadOrigin === "string"
        ? normalizeUploadOrigin(input.uploadOrigin)
        : currentMeta.uploadOrigin,
    captureHashtags: Array.isArray(input.captureHashtags)
      ? normalizeCaptureHashtags(input.captureHashtags)
      : currentMeta.captureHashtags
  };

  await updateCapturedPostsMeta(nextMeta);
  return getCapturedPostsStore();
}

export async function clearUploadedCapturedPosts(): Promise<CapturedPostsStore> {
  const [meta, items] = await Promise.all([getCapturedPostsMeta(), getAllCapturedPostItems()]);
  const uploadedIds = items.filter((item) => Boolean(item.uploadedAt)).map((item) => item.id);

  if (uploadedIds.length === 0)
    return buildCapturedPostsStore(meta, items);

  await putCapturedPostsMetaRecord(toCapturedPostsMetaRecord(meta));
  await deleteCapturedPostItems(uploadedIds);
  await notifyCapturedPostsUpdated();

  return buildCapturedPostsStore(
    { ...meta, updatedAt: nowIso() },
    items.filter((item) => !item.uploadedAt)
  );
}

export async function resetCapturedPostsUploadStatus(): Promise<CapturedPostsStore> {
  const [meta, items] = await Promise.all([getCapturedPostsMeta(), getAllCapturedPostItems()]);
  const changedItems = items.map((item) => ({
    ...item,
    uploadedAt: null
  }));

  await putCapturedPostsData(toCapturedPostsMetaRecord(meta), changedItems);
  await notifyCapturedPostsUpdated();

  return buildCapturedPostsStore(
    { ...meta, updatedAt: nowIso() },
    changedItems
  );
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
  const currentMeta = await getCapturedPostsMeta();

  if (importedItems.length === 0) {
    return getCapturedPostsStore();
  }

  const existingItems = await getCapturedPostItemsByIds(importedItems.map((item) => item.id));
  const mergedItems: CapturedPostItem[] = [];

  for (const item of importedItems) {
    const existing = existingItems[item.id];

    if (!existing) {
      mergedItems.push(item);
      continue;
    }

    mergedItems.push({
      ...item,
      capturedAt: takeLatestIso(existing.capturedAt, item.capturedAt),
      lastSeenAt: takeLatestIso(existing.lastSeenAt, item.lastSeenAt),
      uploadedAt: existing.uploadedAt || item.uploadedAt || null
    });
  }

  await putCapturedPostsData(toCapturedPostsMetaRecord(currentMeta), mergedItems);
  await notifyCapturedPostsUpdated();
  return getCapturedPostsStore();
}
