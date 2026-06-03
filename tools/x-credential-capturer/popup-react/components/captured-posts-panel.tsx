import { useEffect, useMemo, useRef, useState } from "react";

import type { CapturedPostRowView } from "../types.js";
import { CaptureHashtagsEditor } from "./capture-hashtags-editor.js";
import { CapturedPostCard } from "./captured-post-card.js";

const INITIAL_VISIBLE_ROWS = 40;
const VISIBLE_ROWS_BATCH_SIZE = 40;

type CapturedPostsPanelProps = {
  apiBaseUrl: string;
  uploadUserId: string;
  uploadOrigin: string;
  capturedPostsSearchQuery: string;
  captureHashtagDraft: string;
  captureHashtags: string[];
  uploadStatus: string;
  isUploading: boolean;
  viewMode: "list" | "grid";
  gridColumns: number;
  showThumbnail: boolean;
  sortOrder: "latest-added" | "oldest-added" | "last-seen";
  rows: CapturedPostRowView[];
  selectedCount: number;
  onApiBaseUrlChange: (value: string) => void;
  onUploadUserIdChange: (value: string) => void;
  onUploadOriginChange: (value: string) => void;
  onCapturedPostsSearchQueryChange: (value: string) => void;
  onCaptureHashtagDraftChange: (value: string) => void;
  onCaptureHashtagDraftKeyDown: (event: { key: string; preventDefault: () => void }) => void;
  onAddCaptureHashtag: () => void;
  onOpenCapturedPostExternalUrl: (id: string) => void;
  onOpenCaptureHashtag: (value: string) => void;
  onOpenCaptureHashtagInWindow: (value: string) => void;
  onRemoveCaptureHashtag: (value: string) => void;
  onToggleRow: (id: string, checked: boolean) => void;
  onSelectAllPending: () => void;
  onClearSelection: () => void;
  onUploadSelected: () => void;
  onClearUploaded: () => void;
  onResetUploadStatus: () => void;
  onExport: () => void;
  onImport: (file: File) => void;
  onViewModeChange: (value: "list" | "grid") => void;
  onGridColumnsChange: (value: number) => void;
  onShowThumbnailChange: (value: boolean) => void;
  onSortOrderChange: (value: "latest-added" | "oldest-added" | "last-seen") => void;
};

export function CapturedPostsPanel({
  apiBaseUrl,
  uploadUserId,
  uploadOrigin,
  capturedPostsSearchQuery,
  captureHashtagDraft,
  captureHashtags,
  uploadStatus,
  isUploading,
  viewMode,
  gridColumns,
  showThumbnail,
  sortOrder,
  rows,
  selectedCount,
  onApiBaseUrlChange,
  onUploadUserIdChange,
  onUploadOriginChange,
  onCapturedPostsSearchQueryChange,
  onCaptureHashtagDraftChange,
  onCaptureHashtagDraftKeyDown,
  onAddCaptureHashtag,
  onOpenCapturedPostExternalUrl,
  onOpenCaptureHashtag,
  onOpenCaptureHashtagInWindow,
  onRemoveCaptureHashtag,
  onToggleRow,
  onSelectAllPending,
  onClearSelection,
  onUploadSelected,
  onClearUploaded,
  onResetUploadStatus,
  onExport,
  onImport,
  onViewModeChange,
  onGridColumnsChange,
  onShowThumbnailChange,
  onSortOrderChange
}: CapturedPostsPanelProps) {
  const loadMoreRef = useRef<HTMLDivElement | null>(null);
  const [visibleRowsCount, setVisibleRowsCount] = useState(INITIAL_VISIBLE_ROWS);

  useEffect(() => {
    setVisibleRowsCount(INITIAL_VISIBLE_ROWS);
  }, [rows.length, capturedPostsSearchQuery, viewMode, gridColumns, showThumbnail, sortOrder]);

  useEffect(() => {
    const target = loadMoreRef.current;

    if (!target || visibleRowsCount >= rows.length) {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        const firstEntry = entries[0];

        if (!firstEntry?.isIntersecting) {
          return;
        }

        setVisibleRowsCount((previous) =>
          Math.min(rows.length, previous + VISIBLE_ROWS_BATCH_SIZE)
        );
      },
      {
        root: null,
        rootMargin: "120px 0px",
        threshold: 0
      }
    );

    observer.observe(target);

    return () => {
      observer.disconnect();
    };
  }, [rows.length, visibleRowsCount]);

  const visibleRows = useMemo(
    () => rows.slice(0, Math.min(rows.length, visibleRowsCount)),
    [rows, visibleRowsCount]
  );
  const pendingRowsCount = useMemo(() => rows.filter((row) => !row.item.uploadedAt).length, [rows]);
  const hasMoreRows = visibleRows.length < rows.length;

  return (
    <section className="captured-posts">
      <div className="captured-posts-header">
        <h2>Captured SearchTimeline posts</h2>
        <span className="meta">
          {rows.length} total • {pendingRowsCount} pending
        </span>
      </div>

      <div className="settings-row">
        <label htmlFor="uploadApiBaseUrl">API base URL</label>
        <input
          id="uploadApiBaseUrl"
          type="text"
          value={apiBaseUrl}
          onChange={(event) => {
            onApiBaseUrlChange(event.target.value);
          }}
        />
      </div>

      <div className="settings-row">
        <label htmlFor="uploadUserId">Upload userId</label>
        <input
          id="uploadUserId"
          type="text"
          value={uploadUserId}
          onChange={(event) => {
            onUploadUserIdChange(event.target.value);
          }}
        />
      </div>

      <div className="settings-row">
        <label htmlFor="uploadOrigin">Upload origin</label>
        <input
          id="uploadOrigin"
          type="text"
          value={uploadOrigin}
          onChange={(event) => {
            onUploadOriginChange(event.target.value);
          }}
        />
      </div>

      <div className="settings-row">
        <label htmlFor="captureHashtagInput">Capture hashtags</label>
        <CaptureHashtagsEditor
          captureHashtags={captureHashtags}
          captureHashtagDraft={captureHashtagDraft}
          onCaptureHashtagDraftChange={onCaptureHashtagDraftChange}
          onCaptureHashtagDraftKeyDown={onCaptureHashtagDraftKeyDown}
          onAddCaptureHashtag={onAddCaptureHashtag}
          onOpenCaptureHashtag={onOpenCaptureHashtag}
          onOpenCaptureHashtagInWindow={onOpenCaptureHashtagInWindow}
          onRemoveCaptureHashtag={onRemoveCaptureHashtag}
        />
      </div>

      <div className="captured-posts-actions">
        <div className="captured-posts-view-toggle">
          <button
            className={`btn secondary ${viewMode === "list" ? "is-active" : ""}`}
            type="button"
            onClick={() => {
              onViewModeChange("list");
            }}
          >
            List
          </button>
          <button
            className={`btn secondary ${viewMode === "grid" ? "is-active" : ""}`}
            type="button"
            onClick={() => {
              onViewModeChange("grid");
            }}
          >
            Grid
          </button>
        </div>
        <button className="btn secondary" type="button" onClick={onSelectAllPending}>
          Select pending
        </button>
        <button className="btn secondary" type="button" onClick={onClearSelection}>
          Clear selection
        </button>
        <button
          className="btn primary"
          type="button"
          disabled={selectedCount === 0 || isUploading}
          onClick={onUploadSelected}
        >
          {isUploading ? "Uploading..." : `Upload selected (${selectedCount})`}
        </button>
        <button
          className="btn danger"
          type="button"
          disabled={isUploading}
          onClick={onClearUploaded}
        >
          Clear uploaded
        </button>
        <button
          className="btn secondary"
          type="button"
          disabled={isUploading}
          onClick={onResetUploadStatus}
        >
          Reset upload status
        </button>
        <button className="btn secondary" type="button" disabled={isUploading} onClick={onExport}>
          Export
        </button>
        <label className="btn secondary import-btn">
          Import
          <input
            type="file"
            accept=".json,application/json"
            onChange={(event) => {
              const file = event.target.files?.[0];

              if (file) {
                onImport(file);
              }

              event.currentTarget.value = "";
            }}
          />
        </label>
        <div className="captured-posts-options">
          <label>
            Columns
            <select
              value={gridColumns}
              onChange={(event) => {
                onGridColumnsChange(Number(event.target.value));
              }}
            >
              {[1, 2, 3, 4, 5, 6].map((size) => (
                <option key={size} value={size}>
                  {size}
                </option>
              ))}
            </select>
          </label>
          <label className="checkbox-inline">
            <input
              type="checkbox"
              checked={showThumbnail}
              onChange={(event) => {
                onShowThumbnailChange(event.target.checked);
              }}
            />
            <span>Thumbnail</span>
          </label>
          <label>
            Order
            <select
              value={sortOrder}
              onChange={(event) => {
                onSortOrderChange(
                  event.target.value as "latest-added" | "oldest-added" | "last-seen"
                );
              }}
            >
              <option value="latest-added">Latest added</option>
              <option value="oldest-added">Oldest added</option>
              <option value="last-seen">Last seen</option>
            </select>
          </label>
          <label className="captured-post-search">
            <span>Search</span>
            <input
              type="text"
              value={capturedPostsSearchQuery}
              placeholder="id, text, media, author..."
              onChange={(event) => {
                onCapturedPostsSearchQueryChange(event.target.value);
              }}
            />
          </label>
        </div>
        <span className="meta">{uploadStatus}</span>
      </div>

      <div
        className={`captured-posts-list ${viewMode === "grid" ? "grid-view" : "list-view"}`}
        style={
          viewMode === "grid"
            ? { gridTemplateColumns: `repeat(${Math.max(1, gridColumns)}, minmax(0, 1fr))` }
            : undefined
        }
      >
        {rows.length === 0 && (
          <p className="meta">
            {capturedPostsSearchQuery.trim()
              ? "No captured posts match your search."
              : "No captured posts yet. Open hashtag media timeline on X."}
          </p>
        )}

        {visibleRows.map((row) => (
          <CapturedPostCard
            key={row.item.id}
            row={row}
            showThumbnail={showThumbnail}
            isUploading={isUploading}
            onToggleRow={onToggleRow}
            onOpenCapturedPostExternalUrl={onOpenCapturedPostExternalUrl}
          />
        ))}
      </div>

      {rows.length > 0 && (
        <div className="captured-posts-virtual-footer">
          <span className="meta">
            Showing {visibleRows.length} of {rows.length}
          </span>
          {hasMoreRows && <div ref={loadMoreRef} className="captured-posts-load-more-sentinel" />}
        </div>
      )}
    </section>
  );
}
