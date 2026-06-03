import { useEffect, useMemo, useState } from "react";

import { formatDate, formatDurationFromMs } from "../../popup/format.js";
import type { UploadNotificationRowView } from "../types.js";

type NotificationsPanelProps = {
  notifications: UploadNotificationRowView[];
  runningCount: number;
  onClear: () => void;
};

function resolveStatusClass(status: UploadNotificationRowView["item"]["status"]): string {
  if (status === "completed") {
    return "ok";
  }

  if (status === "running") {
    return "pending";
  }

  if (status === "expired") {
    return "expired";
  }

  return "failed";
}

function resolveStatusLabel(status: UploadNotificationRowView["item"]["status"]): string {
  if (status === "completed") {
    return "Completed";
  }

  if (status === "running") {
    return "Running";
  }

  if (status === "expired") {
    return "Expired";
  }

  return "Failed";
}

export function NotificationsPanel({
  notifications,
  runningCount,
  onClear
}: NotificationsPanelProps) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (runningCount === 0) {
      return;
    }

    const timer = setInterval(() => {
      setNow(Date.now());
    }, 1000);

    return () => {
      clearInterval(timer);
    };
  }, [runningCount]);

  const rows = useMemo(() => {
    return notifications.map((row) => {
      if (row.item.status !== "running") {
        return row;
      }

      const startedAtMs = new Date(row.item.startedAt).getTime();

      if (!Number.isFinite(startedAtMs)) {
        return row;
      }

      return {
        ...row,
        progressDurationMs: Math.max(0, now - startedAtMs)
      };
    });
  }, [notifications, now]);

  return (
    <section className="notifications-panel">
      <div className="notifications-header">
        <h2>Notifications</h2>
        <span className="meta">
          {rows.length} jobs • {runningCount} running
        </span>
      </div>

      <div className="notifications-actions">
        <button
          className="btn secondary"
          type="button"
          onClick={onClear}
          disabled={rows.length === 0}
        >
          Clear notifications
        </button>
      </div>

      <div className="notifications-list">
        {rows.length === 0 && <p className="meta">No notifications yet.</p>}

        {rows.map((row) => (
          <article key={row.item.id} className="notification-card">
            <div className="notification-card-head">
              <span className={`status ${resolveStatusClass(row.item.status)}`}>
                {resolveStatusLabel(row.item.status)}
              </span>
              <strong>{row.item.id}</strong>
            </div>

            <div className="notification-grid">
              <span>Created: {formatDate(row.item.createdAt)}</span>
              <span>Started: {formatDate(row.item.startedAt)}</span>
              <span>Finished: {formatDate(row.item.completedAt)}</span>
              <span>Progress: {formatDurationFromMs(row.progressDurationMs)}</span>
              <span>Attempted: {row.item.attemptedPosts}</span>
              <span>
                Result: {row.item.uploadedPosts} uploaded / {row.item.failedPosts} failed
              </span>
              <span>UserId: {row.item.uploadUserId || "-"}</span>
              <span>Origin: {row.item.uploadOrigin || "-"}</span>
              <span className="notification-url">API: {row.item.apiBaseUrl || "-"}</span>
            </div>

            {row.item.uploadSummary && (
              <div className="notification-summary">
                <span>Received: {row.item.uploadSummary.receivedPosts ?? "-"}</span>
                <span>Saved: {row.item.uploadSummary.savedPosts ?? "-"}</span>
                <span>Ignored: {row.item.uploadSummary.ignoredPosts ?? "-"}</span>
                <span>
                  Total: {row.item.uploadSummary.beforeCount ?? "-"} {"->"}{" "}
                  {row.item.uploadSummary.afterCount ?? "-"}
                </span>
                <span>Delta: {row.item.uploadSummary.deltaCount ?? "-"}</span>
                <span>
                  API Duration:{" "}
                  {row.item.uploadSummary.durationMs !== null
                    ? formatDurationFromMs(row.item.uploadSummary.durationMs)
                    : "-"}
                </span>
              </div>
            )}

            {row.item.error && <p className="notification-error">Error: {row.item.error}</p>}
          </article>
        ))}
      </div>
    </section>
  );
}
