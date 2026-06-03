import { formatDate } from "../../popup/format.js";
import type { CapturedPostRowView } from "../types.js";

type CapturedPostCardProps = {
  row: CapturedPostRowView;
  showThumbnail: boolean;
  isUploading: boolean;
  onToggleRow: (id: string, checked: boolean) => void;
  onOpenCapturedPostExternalUrl: (id: string) => void;
};

export function CapturedPostCard({
  row,
  showThumbnail,
  isUploading,
  onToggleRow,
  onOpenCapturedPostExternalUrl
}: CapturedPostCardProps) {
  return (
    <article className={`captured-post-card ${row.item.uploadedAt ? "uploaded" : ""}`}>
      {showThumbnail ? (
        <div className="captured-post-thumb-wrap">
          {row.item.mediaUrls[0] ? (
            <img
              className={`captured-post-thumb ${row.externalUrl ? "clickable" : ""}`}
              src={row.item.mediaUrls[0]}
              alt="media thumbnail"
              loading="lazy"
              onClick={() => {
                if (!row.externalUrl) {
                  return;
                }

                onOpenCapturedPostExternalUrl(row.item.id);
              }}
            />
          ) : (
            <div className="captured-post-thumb captured-post-thumb-empty">No thumbnail</div>
          )}

          <div className="captured-post-overlay">
            <label className="checkbox-inline captured-post-overlay-check">
              <input
                type="checkbox"
                checked={row.selected}
                disabled={!row.selectable || isUploading}
                onChange={(event) => {
                  onToggleRow(row.item.id, event.target.checked);
                }}
              />
              <span />
            </label>
            <span className={`status ${row.item.uploadedAt ? "ok" : "pending"}`}>
              {row.item.uploadedAt ? "Uploaded" : "Pending"}
            </span>
          </div>
        </div>
      ) : (
        <>
          <div className="captured-post-card-top">
            <label className="checkbox-inline">
              <input
                type="checkbox"
                checked={row.selected}
                disabled={!row.selectable || isUploading}
                onChange={(event) => {
                  onToggleRow(row.item.id, event.target.checked);
                }}
              />
              <span />
            </label>
            <button
              className={`captured-post-id-link ${row.externalUrl ? "clickable" : ""}`}
              type="button"
              onClick={() => {
                if (!row.externalUrl) {
                  return;
                }

                onOpenCapturedPostExternalUrl(row.item.id);
              }}
              title={row.externalUrl ? "Open link from description (last t.co)" : "No t.co link"}
            >
              {row.item.id}
            </button>
            <span className={`status ${row.item.uploadedAt ? "ok" : "pending"}`}>
              {row.item.uploadedAt ? "Uploaded" : "Pending"}
            </span>
          </div>

          <p className="captured-post-preview">{row.preview}</p>

          <div className="captured-post-card-meta">
            <span>Author: {row.item.authorUserName || row.item.authorId}</span>
            <span>Seen: {formatDate(row.item.lastSeenAt)}</span>
            {row.item.uploadedAt && <span>Uploaded: {formatDate(row.item.uploadedAt)}</span>}
          </div>
        </>
      )}
    </article>
  );
}
