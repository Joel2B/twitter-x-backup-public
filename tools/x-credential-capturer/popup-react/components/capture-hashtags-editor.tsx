type CaptureHashtagsEditorProps = {
  captureHashtags: string[];
  captureHashtagDraft: string;
  onCaptureHashtagDraftChange: (value: string) => void;
  onCaptureHashtagDraftKeyDown: (event: { key: string; preventDefault: () => void }) => void;
  onAddCaptureHashtag: () => void;
  onOpenCaptureHashtag: (value: string) => void;
  onOpenCaptureHashtagInWindow: (value: string) => void;
  onRemoveCaptureHashtag: (value: string) => void;
};

export function CaptureHashtagsEditor({
  captureHashtags,
  captureHashtagDraft,
  onCaptureHashtagDraftChange,
  onCaptureHashtagDraftKeyDown,
  onAddCaptureHashtag,
  onOpenCaptureHashtag,
  onOpenCaptureHashtagInWindow,
  onRemoveCaptureHashtag
}: CaptureHashtagsEditorProps) {
  return (
    <div className="capture-hashtags-editor">
      <div className="capture-chip-list">
        {captureHashtags.map((hashtag) => (
          <span key={hashtag} className="capture-chip">
            <button
              className="capture-chip-open"
              type="button"
              title={`Open #${hashtag}`}
              onClick={() => {
                onOpenCaptureHashtag(hashtag);
              }}
              onMouseDown={(event) => {
                if (event.button !== 1) {
                  return;
                }

                event.preventDefault();
                onOpenCaptureHashtagInWindow(hashtag);
              }}
              onAuxClick={(event) => {
                if (event.button !== 1) {
                  return;
                }

                event.preventDefault();
              }}
            >
              #{hashtag}
            </button>
            <button
              className="capture-chip-remove"
              type="button"
              title={`Remove #${hashtag}`}
              onClick={(event) => {
                event.stopPropagation();
                onRemoveCaptureHashtag(hashtag);
              }}
            >
              ×
            </button>
          </span>
        ))}
        {captureHashtags.length === 0 && (
          <span className="meta">No hashtag filter: captures all SearchTimeline posts.</span>
        )}
      </div>
      <div className="capture-chip-input-row">
        <input
          id="captureHashtagInput"
          type="text"
          value={captureHashtagDraft}
          placeholder="Type hashtag and press Enter/Space"
          onChange={(event) => {
            onCaptureHashtagDraftChange(event.target.value);
          }}
          onKeyDown={(event) => {
            onCaptureHashtagDraftKeyDown(event);
          }}
        />
        <button className="btn secondary" type="button" onClick={onAddCaptureHashtag}>
          Add
        </button>
      </div>
    </div>
  );
}
