import type { CapturedPostItem, CapturedPostsStore } from "../popup/models.js";

const DB_NAME = "xcc-captured-posts";
const DB_VERSION = 1;
const POSTS_STORE = "posts";
const META_STORE = "meta";
const META_KEY = "store";

export type CapturedPostsMetaRecord = Omit<CapturedPostsStore, "items"> & {
  key: typeof META_KEY;
};

let openDbPromise: Promise<IDBDatabase> | null = null;

function requestToPromise<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error("IndexedDB request failed."));
  });
}

function transactionDone(transaction: IDBTransaction): Promise<void> {
  return new Promise<void>((resolve, reject) => {
    transaction.oncomplete = () => resolve();
    transaction.onerror = () =>
      reject(transaction.error ?? new Error("IndexedDB transaction failed."));
    transaction.onabort = () =>
      reject(transaction.error ?? new Error("IndexedDB transaction aborted."));
  });
}

export async function openCapturedPostsDb(): Promise<IDBDatabase> {
  if (!openDbPromise) {
    openDbPromise = new Promise<IDBDatabase>((resolve, reject) => {
      const request = indexedDB.open(DB_NAME, DB_VERSION);

      request.onupgradeneeded = () => {
        const db = request.result;

        if (!db.objectStoreNames.contains(POSTS_STORE))
          db.createObjectStore(POSTS_STORE, { keyPath: "id" });

        if (!db.objectStoreNames.contains(META_STORE))
          db.createObjectStore(META_STORE, { keyPath: "key" });
      };

      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error ?? new Error("Could not open IndexedDB."));
    });
  }

  return openDbPromise;
}

export async function getCapturedPostsMetaRecord(): Promise<CapturedPostsMetaRecord | null> {
  const db = await openCapturedPostsDb();
  const transaction = db.transaction(META_STORE, "readonly");
  const store = transaction.objectStore(META_STORE);
  const record =
    (await requestToPromise(store.get(META_KEY))) as CapturedPostsMetaRecord | undefined;
  await transactionDone(transaction);
  return record ?? null;
}

export async function putCapturedPostsMetaRecord(record: CapturedPostsMetaRecord): Promise<void> {
  const db = await openCapturedPostsDb();
  const transaction = db.transaction(META_STORE, "readwrite");
  transaction.objectStore(META_STORE).put(record);
  await transactionDone(transaction);
}

export async function getAllCapturedPostItems(): Promise<CapturedPostItem[]> {
  const db = await openCapturedPostsDb();
  const transaction = db.transaction(POSTS_STORE, "readonly");
  const store = transaction.objectStore(POSTS_STORE);
  const items = (await requestToPromise(store.getAll())) as CapturedPostItem[];
  await transactionDone(transaction);
  return items;
}

export async function getCapturedPostItemsByIds(
  ids: string[]
): Promise<Record<string, CapturedPostItem>> {
  if (ids.length === 0)
    return {};

  const db = await openCapturedPostsDb();
  const transaction = db.transaction(POSTS_STORE, "readonly");
  const store = transaction.objectStore(POSTS_STORE);
  const items = await Promise.all(
    ids.map(async (id) => {
      const item = (await requestToPromise(store.get(id))) as CapturedPostItem | undefined;
      return item ?? null;
    })
  );
  await transactionDone(transaction);

  return Object.fromEntries(
    items
      .filter((item): item is CapturedPostItem => item !== null)
      .map((item) => [item.id, item])
  );
}

export async function putCapturedPostsData(
  meta: CapturedPostsMetaRecord,
  items: CapturedPostItem[]
): Promise<void> {
  const db = await openCapturedPostsDb();
  const transaction = db.transaction([META_STORE, POSTS_STORE], "readwrite");
  transaction.objectStore(META_STORE).put(meta);

  const postsStore = transaction.objectStore(POSTS_STORE);

  for (const item of items)
    postsStore.put(item);

  await transactionDone(transaction);
}

export async function replaceCapturedPostsData(
  meta: CapturedPostsMetaRecord,
  items: CapturedPostItem[]
): Promise<void> {
  const db = await openCapturedPostsDb();
  const transaction = db.transaction([META_STORE, POSTS_STORE], "readwrite");
  transaction.objectStore(META_STORE).put(meta);

  const postsStore = transaction.objectStore(POSTS_STORE);
  postsStore.clear();

  for (const item of items)
    postsStore.put(item);

  await transactionDone(transaction);
}

export async function deleteCapturedPostItems(ids: string[]): Promise<void> {
  if (ids.length === 0)
    return;

  const db = await openCapturedPostsDb();
  const transaction = db.transaction(POSTS_STORE, "readwrite");
  const store = transaction.objectStore(POSTS_STORE);

  for (const id of ids)
    store.delete(id);

  await transactionDone(transaction);
}
