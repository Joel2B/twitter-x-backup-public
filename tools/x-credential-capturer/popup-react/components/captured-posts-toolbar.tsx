import { CaptureHashtagsEditor } from "./capture-hashtags-editor.js";

type CapturedPostsToolbarProps = {
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
  selectedCount: number;
  onApiBaseUrlChange: (value: string) => void;
  onUploadUserIdChange: (value: string) => void;
  onUploadOriginChange: (value: string) => void;
  onCapturedPostsSearchQueryChange: (value: string) => void;
  onCaptureHashtagDraftChange: (value: string) => void;
  onCaptureHashtagDraftKeyDown: (event: { key: string; preventDefault: () => void }) => void;
  onAddCaptureHashtag: () => void;
  onOpenCaptureHashtag: (value: string) => void;
  onOpenCaptureHashtagInWindow: (value: string) => void;
  onRemoveCaptureHashtag: (value: string) => void;
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

export function CapturedPostsToolbar({
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
  selectedCount,
  onApiBaseUrlChange,
  onUploadUserIdChange,
  onUploadOriginChange,
  onCapturedPostsSearchQueryChange,
  onCaptureHashtagDraftChange,
  onCaptureHashtagDraftKeyDown,
  onAddCaptureHashtag,
  onOpenCaptureHashtag,
  onOpenCaptureHashtagInWindow,
  onRemoveCaptureHashtag,
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
}: CapturedPostsToolbarProps) {
  return (
    <>
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
    </>
  );
}
