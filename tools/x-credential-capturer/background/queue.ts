let updateQueue: Promise<unknown> = Promise.resolve();

export function enqueueUpdate<T>(work: () => Promise<T> | T): Promise<T> {
  updateQueue = updateQueue.then(work, work);
  return updateQueue as Promise<T>;
}
