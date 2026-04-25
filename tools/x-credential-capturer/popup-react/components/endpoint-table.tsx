import { Fragment } from "react";

import { GRID_COLUMN_COUNT } from "../../popup/constants.js";
import { formatDate, formatMissingFields, formatRateValue } from "../../popup/format.js";
import type { EndpointDefinition, EndpointModel } from "../../popup/models.js";
import type { EndpointRowView } from "../types.js";

type EndpointTableProps = {
  endpointRows: EndpointRowView[];
  onCopyEndpoint: (model: EndpointModel, endpointId: string) => void;
  onOpenEndpointUrl: (url: string, openInBackground: boolean) => void;
  onRollbackEndpoint: (endpointId: string) => void;
  onTestEndpoint: (endpoint: EndpointDefinition) => void;
};

export function EndpointTable({
  endpointRows,
  onCopyEndpoint,
  onOpenEndpointUrl,
  onRollbackEndpoint,
  onTestEndpoint
}: EndpointTableProps) {
  return (
    <section>
      <table className="grid">
        <thead>
          <tr>
            <th>Endpoint</th>
            <th>Status</th>
            <th>Last Seen</th>
            <th>Missing</th>
            <th>Action</th>
          </tr>
        </thead>
        <tbody>
          {endpointRows.map((row) => (
            <Fragment key={row.endpoint.id}>
              <tr>
                <td>{row.endpoint.title}</td>
                <td>
                  <span className={row.statusBadge.className}>{row.statusBadge.label}</span>
                  <span className={`freshness ${row.freshness.className}`}>
                    {row.freshness.label}
                  </span>
                </td>
                <td>{formatDate(row.model.capture?.lastSeenAt)}</td>
                <td className="missing">
                  {row.endpoint.skipped ? "N/A" : formatMissingFields(row.model.missingFields)}
                </td>
                <td>
                  <div className="action-wrap">
                    {row.endpointPageUrl && (
                      <a
                        className="btn nav btn-link"
                        href={row.endpointPageUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        onClick={(event) => {
                          event.preventDefault();
                          const openInBackground = event.ctrlKey || event.metaKey;
                          onOpenEndpointUrl(row.endpointPageUrl as string, openInBackground);
                        }}
                        onAuxClick={(event) => {
                          if (event.button !== 1) {
                            return;
                          }

                          event.preventDefault();
                          onOpenEndpointUrl(row.endpointPageUrl as string, true);
                        }}
                      >
                        Open
                      </a>
                    )}

                    <button
                      className="btn copy"
                      disabled={row.copyDisabled}
                      title={row.copyTitle}
                      onClick={() => {
                        onCopyEndpoint(row.model, row.endpoint.id);
                      }}
                    >
                      {row.copyLabel}
                    </button>

                    <button
                      className="btn secondary"
                      disabled={row.model.historyCount === 0}
                      title={
                        row.model.historyCount > 0
                          ? "Rollback to previous captured snapshot"
                          : "No history available"
                      }
                      onClick={() => {
                        onRollbackEndpoint(row.endpoint.id);
                      }}
                    >
                      {row.model.historyCount > 0 ? `Undo (${row.model.historyCount})` : "Undo"}
                    </button>

                    <button
                      className="btn test"
                      disabled={row.testDisabled}
                      onClick={() => {
                        onTestEndpoint(row.endpoint);
                      }}
                    >
                      {row.testRuntime.running ? "Testing..." : "Test"}
                    </button>

                    {row.testResultText && (
                      <span className={`test-result ${row.testRuntime.result?.ok ? "ok" : "bad"}`}>
                        {row.testResultText}
                      </span>
                    )}

                    {!row.endpointPageUrl &&
                      row.endpoint.pageUrlTemplate &&
                      !row.endpoint.skipped && (
                        <span className="meta">Set X username to enable Open</span>
                      )}

                    {!row.endpointPageUrl && row.endpoint.skipped && (
                      <span className="meta">No direct page</span>
                    )}
                  </div>
                </td>
              </tr>

              {row.rateEntries.length > 0 && (
                <tr className="rate-full-row">
                  <td colSpan={GRID_COLUMN_COUNT}>
                    <div className="rate-box">
                      {row.rateEntries.map(([key, value]) => (
                        <div key={key} className="rate-row">
                          <span className="rate-key">{key}</span>
                          <span className="rate-value">{formatRateValue(key, value)}</span>
                        </div>
                      ))}
                    </div>
                  </td>
                </tr>
              )}
            </Fragment>
          ))}
        </tbody>
      </table>
    </section>
  );
}
