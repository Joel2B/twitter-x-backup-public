import { useEffect, useMemo, useRef, useState } from "react";

import type { CapturedPostRowView } from "../types.js";
import { CapturedPostCard } from "./captured-post-card.js";
import { CapturedPostsToolbar } from "./captured-posts-toolbar.js";

const INITIAL_VISIBLE_ROWS = 40;
const VISIBLE_ROWS_BATCH_SIZE = 40;
const JUMP_VISIBLE_ROWS = 120;
const JUMP_THUMB_MIN_HEIGHT = 48;
const JUMP_THUMB_MIN_RATIO = 0.08;

function getJumpThumbMetrics(railHeight: number, visibleCount: number, totalCount: number) {
  if (railHeight <= 0 || totalCount <= 0) {
    return {
      thumbHeightPx: JUMP_THUMB_MIN_HEIGHT,
      thumbTravelPx: 0
    };
  }

  const thumbHeightFromRatio = (visibleCount / totalCount) * railHeight;
  const thumbHeightFromMinimumRatio = JUMP_THUMB_MIN_RATIO * railHeight;
  const thumbHeightPx = Math.min(
    railHeight,
    Math.max(JUMP_THUMB_MIN_HEIGHT, thumbHeightFromRatio, thumbHeightFromMinimumRatio)
  );

  return {
    thumbHeightPx,
    thumbTravelPx: Math.max(0, railHeight - thumbHeightPx)
  };
}

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
  const jumpRailRef = useRef<HTMLDivElement | null>(null);
  const jumpThumbRef = useRef<HTMLDivElement | null>(null);
  const listTopRef = useRef<HTMLDivElement | null>(null);
  const listRef = useRef<HTMLDivElement | null>(null);
  const [visibleRowsCount, setVisibleRowsCount] = useState(INITIAL_VISIBLE_ROWS);
  const [rangeStartIndex, setRangeStartIndex] = useState(0);
  const [isJumpRailDragging, setIsJumpRailDragging] = useState(false);
  const [showJumpRail, setShowJumpRail] = useState(false);
  const [manualScrollRatio, setManualScrollRatio] = useState(0);

  useEffect(() => {
    setRangeStartIndex(0);
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

  useEffect(() => {
    if (!isJumpRailDragging) {
      return;
    }

    function stopDragging() {
      setIsJumpRailDragging(false);
    }

    window.addEventListener("mouseup", stopDragging);
    window.addEventListener("mouseleave", stopDragging);

    return () => {
      window.removeEventListener("mouseup", stopDragging);
      window.removeEventListener("mouseleave", stopDragging);
    };
  }, [isJumpRailDragging]);

  useEffect(() => {
    if (!isJumpRailDragging) {
      return;
    }

    function handleMouseMove(event: MouseEvent) {
      jumpToClientY(event.clientY);
    }

    window.addEventListener("mousemove", handleMouseMove);

    return () => {
      window.removeEventListener("mousemove", handleMouseMove);
    };
  }, [isJumpRailDragging, rows.length]);

  useEffect(() => {
    function updateJumpRailVisibility() {
      const listTop = listTopRef.current;

      if (!listTop) {
        setShowJumpRail(false);
        return;
      }

      const top = listTop.getBoundingClientRect().top;
      const isGridOnlyView = top <= 12;

      setShowJumpRail(isGridOnlyView);

      if (
        !isGridOnlyView &&
        !isJumpRailDragging &&
        (rangeStartIndex !== 0 || visibleRowsCount !== INITIAL_VISIBLE_ROWS)
      ) {
        setRangeStartIndex(0);
        setVisibleRowsCount(INITIAL_VISIBLE_ROWS);
        setManualScrollRatio(0);
      }
    }

    updateJumpRailVisibility();
    window.addEventListener("scroll", updateJumpRailVisibility, { passive: true });
    window.addEventListener("resize", updateJumpRailVisibility);

    return () => {
      window.removeEventListener("scroll", updateJumpRailVisibility);
      window.removeEventListener("resize", updateJumpRailVisibility);
    };
  }, [
    gridColumns,
    isJumpRailDragging,
    rangeStartIndex,
    rows.length,
    showThumbnail,
    sortOrder,
    viewMode,
    visibleRowsCount
  ]);

  useEffect(() => {
    function updateManualScrollRatio() {
      const listElement = listRef.current;

      if (!listElement) {
        setManualScrollRatio(0);
        return;
      }

      const bounds = listElement.getBoundingClientRect();
      const viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;
      const maxScrollableWithinList = Math.max(1, bounds.height - viewportHeight);
      const scrolledWithinList = Math.max(0, -bounds.top);
      const ratio = Math.max(0, Math.min(1, scrolledWithinList / maxScrollableWithinList));

      setManualScrollRatio(ratio);
    }

    updateManualScrollRatio();
    window.addEventListener("scroll", updateManualScrollRatio, { passive: true });
    window.addEventListener("resize", updateManualScrollRatio);

    return () => {
      window.removeEventListener("scroll", updateManualScrollRatio);
      window.removeEventListener("resize", updateManualScrollRatio);
    };
  }, [
    rangeStartIndex,
    rows.length,
    visibleRowsCount,
    viewMode,
    gridColumns,
    showThumbnail,
    sortOrder
  ]);

  function scrollPostsIntoView() {
    requestAnimationFrame(() => {
      listTopRef.current?.scrollIntoView({
        block: "start"
      });
    });
  }

  function jumpToRatio(rawRatio: number) {
    if (rows.length <= 0) {
      return;
    }

    const ratio = Math.max(0, Math.min(1, rawRatio));
    const nextVisibleRowsCount = Math.min(
      rows.length,
      Math.max(INITIAL_VISIBLE_ROWS, Math.min(JUMP_VISIBLE_ROWS, rows.length))
    );
    const maxStartIndex = Math.max(0, rows.length - nextVisibleRowsCount);
    const nextStartIndex = Math.round(maxStartIndex * ratio);

    setRangeStartIndex(nextStartIndex);
    setVisibleRowsCount(nextVisibleRowsCount);
    scrollPostsIntoView();
  }

  function jumpToClientY(clientY: number) {
    const rail = jumpRailRef.current;

    if (!rail) {
      return;
    }

    const bounds = rail.getBoundingClientRect();
    const { thumbHeightPx, thumbTravelPx } = getJumpThumbMetrics(
      bounds.height,
      visibleRows.length,
      rows.length
    );
    const thumbHalfHeight = thumbHeightPx / 2;
    const ratio = (clientY - bounds.top - thumbHalfHeight) / Math.max(1, thumbTravelPx);

    jumpToRatio(ratio);
  }

  const visibleRows = useMemo(() => {
    const endIndex = Math.min(rows.length, rangeStartIndex + visibleRowsCount);
    return rows.slice(rangeStartIndex, endIndex);
  }, [rangeStartIndex, rows, visibleRowsCount]);
  const pendingRowsCount = useMemo(() => rows.filter((row) => !row.item.uploadedAt).length, [rows]);
  const hasMoreRows = rangeStartIndex + visibleRows.length < rows.length;
  const jumpThumbStyle = useMemo(() => {
    const railHeight = jumpRailRef.current?.getBoundingClientRect().height || 0;
    const { thumbHeightPx, thumbTravelPx } = getJumpThumbMetrics(
      railHeight,
      visibleRows.length,
      rows.length
    );

    if (rows.length <= visibleRows.length) {
      return {
        height: `${thumbHeightPx}px`,
        top: "0px"
      };
    }

    const maxStartIndex = Math.max(1, rows.length - visibleRows.length);
    const baseRatio = rangeStartIndex / maxStartIndex;
    const currentWindowWeight = visibleRows.length / Math.max(rows.length, 1);
    const ratio = Math.max(0, Math.min(1, baseRatio + manualScrollRatio * currentWindowWeight));

    return {
      height: `${thumbHeightPx}px`,
      top: `${ratio * thumbTravelPx}px`
    };
  }, [manualScrollRatio, rangeStartIndex, rows.length, visibleRows.length]);

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

      <div ref={listTopRef} className="captured-posts-list-anchor" />

      <div
        ref={listRef}
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
            Showing {rangeStartIndex + 1}-{rangeStartIndex + visibleRows.length} of {rows.length}
          </span>
          {hasMoreRows && <div ref={loadMoreRef} className="captured-posts-load-more-sentinel" />}
        </div>
      )}

      {rows.length > 0 && (
        <div className="captured-posts-range-overlay">
          {rangeStartIndex + 1}-{rangeStartIndex + visibleRows.length} / {rows.length}
        </div>
      )}

      {rows.length > INITIAL_VISIBLE_ROWS && showJumpRail && (
        <div
          ref={jumpRailRef}
          className={`captured-posts-jump-rail ${isJumpRailDragging ? "is-dragging" : ""}`}
          onMouseDown={(event) => {
            setIsJumpRailDragging(true);
            jumpToClientY(event.clientY);
          }}
        >
          <div ref={jumpThumbRef} className="captured-posts-jump-thumb" style={jumpThumbStyle} />
        </div>
      )}
    </section>
  );
}
