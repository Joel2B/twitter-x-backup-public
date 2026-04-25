import { DEFAULT_PROFILE_ID } from "../../popup/constants.js";
import type { ProfileRecord } from "../types.js";

type GlobalSectionProps = {
  activeProfileId: string;
  canDeleteProfile: boolean;
  globalStatusOk: boolean;
  isApplyingProfile: boolean;
  profileHint: string;
  profiles: ProfileRecord[];
  sensitiveHint: string;
  usernameDraft: string;
  usernameHint: string;
  maskSensitive: boolean;
  onClearState: () => void;
  onCreateProfile: () => void;
  onDeleteProfile: () => void;
  onMaskSensitiveChange: (checked: boolean) => void;
  onProfileChange: (profileId: string) => void;
  onRefreshCookies: () => void;
  onUsernameChange: (value: string) => void;
  onUsernameCommit: () => void;
};

export function GlobalSection({
  activeProfileId,
  canDeleteProfile,
  globalStatusOk,
  isApplyingProfile,
  maskSensitive,
  onClearState,
  onCreateProfile,
  onDeleteProfile,
  onMaskSensitiveChange,
  onProfileChange,
  onRefreshCookies,
  onUsernameChange,
  onUsernameCommit,
  profileHint,
  profiles,
  sensitiveHint,
  usernameDraft,
  usernameHint
}: GlobalSectionProps) {
  return (
    <section className="global">
      <div className="global-row">
        <span>Detected base headers (cookie + x-csrf-token + authorization)</span>
        <strong className={`status ${globalStatusOk ? "ok" : "pending"}`}>
          {globalStatusOk ? "OK" : "Pending"}
        </strong>
      </div>

      <div className="settings-row">
        <label htmlFor="profileSelect">Profile</label>
        <div className="settings-inline">
          <select
            id="profileSelect"
            value={activeProfileId || DEFAULT_PROFILE_ID}
            onChange={(event) => {
              onProfileChange(event.target.value);
            }}
          >
            {profiles.map((profile) => (
              <option key={profile.id} value={profile.id}>
                {profile.name}
              </option>
            ))}
          </select>
          <button className="btn secondary" type="button" onClick={onCreateProfile}>
            New
          </button>
          <button
            className="btn danger"
            type="button"
            disabled={!canDeleteProfile || isApplyingProfile}
            title={
              canDeleteProfile ? "Delete selected profile" : "Default profile cannot be deleted"
            }
            onClick={onDeleteProfile}
          >
            Delete
          </button>
        </div>
        <span className="meta">{profileHint}</span>
      </div>

      <div className="settings-row">
        <label htmlFor="usernameInput">X username</label>
        <input
          id="usernameInput"
          type="text"
          placeholder="@your_user or https://x.com/your_user"
          spellCheck={false}
          autoComplete="off"
          value={usernameDraft}
          onInput={(event) => {
            const target = event.target as HTMLInputElement;
            onUsernameChange(target.value);
          }}
          onChange={onUsernameCommit}
        />
        <span className="meta">{usernameHint}</span>
      </div>

      <div className="settings-row">
        <label htmlFor="maskSensitiveToggle">Sensitive guard</label>
        <div className="settings-inline">
          <label className="checkbox-inline">
            <input
              id="maskSensitiveToggle"
              type="checkbox"
              checked={maskSensitive}
              onChange={(event) => {
                onMaskSensitiveChange(event.target.checked);
              }}
            />
            <span>Mask secrets in preview and block copy</span>
          </label>
        </div>
        <span className="meta">{sensitiveHint}</span>
      </div>

      <div className="global-actions">
        <button className="btn secondary" onClick={onRefreshCookies}>
          Refresh cookies
        </button>
        <button className="btn danger" onClick={onClearState}>
          Clear
        </button>
      </div>
    </section>
  );
}
