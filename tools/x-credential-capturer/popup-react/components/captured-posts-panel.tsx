import { useEffect, useMemo, useRef, useState } from "react";

import type { CapturedPostRowView } from "../types.js";
import { CapturedPostCard } from "./captured-post-card.js";
import { CapturedPostsToolbar } from "./captured-posts-toolbar.js";

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

      <CapturedPostsToolbar
        apiBaseUrl={apiBaseUrl}
        uploadUserId={uploadUserId}
        uploadOrigin={uploadOrigin}
        capturedPostsSearchQuery={capturedPostsSearchQuery}
        captureHashtagDraft={captureHashtagDraft}
        captureHashtags={captureHashtags}
        uploadStatus={uploadStatus}
        isUploading={isUploading}
        viewMode={viewMode}
        gridColumns={gridColumns}
        showThumbnail={showThumbnail}
        sortOrder={sortOrder}
        selectedCount={selectedCount}
        onApiBaseUrlChange={onApiBaseUrlChange}
        onUploadUserIdChange={onUploadUserIdChange}
        onUploadOriginChange={onUploadOriginChange}
        onCapturedPostsSearchQueryChange={onCapturedPostsSearchQueryChange}
        onCaptureHashtagDraftChange={onCaptureHashtagDraftChange}
        onCaptureHashtagDraftKeyDown={onCaptureHashtagDraftKeyDown}
        onAddCaptureHashtag={onAddCaptureHashtag}
        onOpenCaptureHashtag={onOpenCaptureHashtag}
        onOpenCaptureHashtagInWindow={onOpenCaptureHashtagInWindow}
        onRemoveCaptureHashtag={onRemoveCaptureHashtag}
        onSelectAllPending={onSelectAllPending}
        onClearSelection={onClearSelection}
        onUploadSelected={onUploadSelected}
        onClearUploaded={onClearUploaded}
        onResetUploadStatus={onResetUploadStatus}
        onExport={onExport}
        onImport={onImport}
        onViewModeChange={onViewModeChange}
        onGridColumnsChange={onGridColumnsChange}
        onShowThumbnailChange={onShowThumbnailChange}
        onSortOrderChange={onSortOrderChange}
      />

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
