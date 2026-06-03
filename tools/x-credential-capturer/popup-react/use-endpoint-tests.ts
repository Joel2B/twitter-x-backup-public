import { useEffect, useMemo, useRef, useState } from "react";

import { formatDurationFromMs } from "../popup/format.js";
import { getFreshnessInfo } from "../popup/helpers.js";
import {
  buildEndpointModel,
  createSingleEndpointPatch,
  resolveGlobalHeaders
} from "../popup/model.js";
import type {
  CaptureState,
  EndpointDefinition,
  EndpointModel,
  EndpointTestRuntime,
  GlobalHeaders,
  PopupSettings
} from "../popup/models.js";
import { getCapturedRateEntries, getRateEntries, runEndpointTest } from "../popup/testing.js";
import { resolveEndpointPageUrl } from "../popup/utils.js";
import { getGlobalStatusOk, getTestResultText, makeStatusBadge } from "./view-model.js";
import type { EndpointRowView } from "./types.js";

type UseEndpointTestsOptions = {
  captureState: CaptureState | null;
  endpoints: EndpointDefinition[];
  settings: PopupSettings;
};

type UseEndpointTestsResult = {
  clearAllTestRuntime: () => void;
  copyEndpoint: (model: EndpointModel, endpointId: string) => Promise<void>;
  endpointRows: EndpointRowView[];
  globalHeaders: GlobalHeaders;
  globalStatusOk: boolean;
  isBulkTesting: boolean;
  runAllTests: () => Promise<void>;
  runSingleTest: (endpoint: EndpointDefinition) => Promise<void>;
  setTestAllStatus: (value: string) => void;
  testAllStatus: string;
};

function createErrorTestResult(error: unknown) {
  return {
    ok: false,
    status: 0,
    hasData: false,
    rate: null,
    message: `Error: ${error instanceof Error ? error.message : String(error)}`,
    bodySnippet: ""
  };
}

export function useEndpointTests({
  captureState,
  endpoints,
  settings
}: UseEndpointTestsOptions): UseEndpointTestsResult {
  const [endpointTestState, setEndpointTestState] = useState<Record<string, EndpointTestRuntime>>(
    {}
  );
  const [endpointCopyLabels, setEndpointCopyLabels] = useState<Record<string, string>>({});
  const [isBulkTesting, setIsBulkTesting] = useState(false);
  const [testAllStatus, setTestAllStatus] = useState("");

  const captureStateRef = useRef<CaptureState | null>(captureState);
  const settingsRef = useRef(settings);

  useEffect(() => {
    captureStateRef.current = captureState;
  }, [captureState]);

  useEffect(() => {
    settingsRef.current = settings;
  }, [settings]);

  const globalHeaders = useMemo(() => resolveGlobalHeaders(captureState), [captureState]);
  const globalStatusOk = useMemo(() => getGlobalStatusOk(globalHeaders), [globalHeaders]);

  function clearAllTestRuntime() {
    setEndpointTestState({});
  }

  function setTestRuntime(endpointId: string, nextValue: Partial<EndpointTestRuntime>) {
    setEndpointTestState((previous) => {
      const merged = {
        ...(previous[endpointId] || { running: false, result: null }),
        ...nextValue
      };

      return {
        ...previous,
        [endpointId]: merged
      };
    });
  }

  async function copyEndpoint(model: EndpointModel, endpointId: string) {
    if (settingsRef.current.maskSensitive) {
      throw new Error("Sensitive guard is enabled. Disable it to copy real credentials.");
    }

    const singlePatch = createSingleEndpointPatch(model);
    const text = JSON.stringify(singlePatch, null, 2);
    await navigator.clipboard.writeText(text);

    setEndpointCopyLabels((previous) => ({
      ...previous,
      [endpointId]: "Copied"
    }));

    setTimeout(() => {
      setEndpointCopyLabels((previous) => ({
        ...previous,
        [endpointId]: "Copy"
      }));
    }, 1200);
  }

  async function runSingleTest(endpoint: EndpointDefinition) {
    setTestRuntime(endpoint.id, { running: true, result: null });

    try {
      const stateNow = captureStateRef.current;
      const globalNow = resolveGlobalHeaders(stateNow);
      const modelNow = buildEndpointModel(endpoint, stateNow, globalNow);
      const result = await runEndpointTest(modelNow);
      setTestRuntime(endpoint.id, { running: false, result });
    } catch (error) {
      setTestRuntime(endpoint.id, {
        running: false,
        result: createErrorTestResult(error)
      });
    }
  }

  async function runAllTests() {
    const stateNow = captureStateRef.current;

    if (isBulkTesting || !stateNow) {
      return;
    }

    const globalNow = resolveGlobalHeaders(stateNow);
    const testable = endpoints.filter((endpoint) => {
      const model = buildEndpointModel(endpoint, stateNow, globalNow);
      return !endpoint.skipped && model.ready;
    });

    if (testable.length === 0) {
      setTestAllStatus("No complete endpoints available to test.");
      return;
    }

    setIsBulkTesting(true);

    let okCount = 0;
    let failCount = 0;
    const startedAt = Date.now();

    try {
      for (const endpoint of testable) {
        setTestRuntime(endpoint.id, { running: true, result: null });

        try {
          const latestState = captureStateRef.current;
          const latestGlobalHeaders = resolveGlobalHeaders(latestState);
          const modelNow = buildEndpointModel(endpoint, latestState, latestGlobalHeaders);
          const result = await runEndpointTest(modelNow);
          setTestRuntime(endpoint.id, { running: false, result });

          if (result.ok) {
            okCount += 1;
          } else {
            failCount += 1;
          }
        } catch (error) {
          failCount += 1;
          setTestRuntime(endpoint.id, {
            running: false,
            result: createErrorTestResult(error)
          });
        }
      }
    } finally {
      setIsBulkTesting(false);
    }

    const elapsed = formatDurationFromMs(Date.now() - startedAt);
    setTestAllStatus(`Done: ${okCount} OK, ${failCount} failed (${elapsed})`);
  }

  const endpointRows: EndpointRowView[] = useMemo(() => {
    return endpoints.map((endpoint) => {
      const model = buildEndpointModel(endpoint, captureState, globalHeaders);
      const testRuntime: EndpointTestRuntime = endpointTestState[endpoint.id] || {
        running: false,
        result: null
      };

      const endpointPageUrl = resolveEndpointPageUrl(endpoint, settings.username, settings.hashtag);
      const freshness = getFreshnessInfo(model, testRuntime);
      const testedRateEntries = getRateEntries(testRuntime.result);
      const capturedRateEntries = getCapturedRateEntries(model.capture);
      const rateEntries = testedRateEntries.length > 0 ? testedRateEntries : capturedRateEntries;
      const statusBadge = makeStatusBadge(model);

      const copyDisabled = settings.maskSensitive || endpoint.skipped || !model.ready;
      const copyTitle = settings.maskSensitive
        ? "Disable Sensitive guard to copy real credentials"
        : endpoint.skipped
          ? "Endpoint skipped"
          : !model.ready
            ? "Complete missing fields to copy"
            : "";

      return {
        endpoint,
        model,
        endpointPageUrl,
        testRuntime,
        testResultText: getTestResultText(testRuntime),
        statusBadge,
        freshness,
        rateEntries,
        copyLabel: endpointCopyLabels[endpoint.id] || "Copy",
        copyDisabled,
        copyTitle,
        testDisabled: endpoint.skipped || !model.ready || testRuntime.running || isBulkTesting
      };
    });
  }, [
    captureState,
    endpointCopyLabels,
    endpointTestState,
    endpoints,
    globalHeaders,
    isBulkTesting,
    settings
  ]);

  return {
    clearAllTestRuntime,
    copyEndpoint,
    endpointRows,
    globalHeaders,
    globalStatusOk,
    isBulkTesting,
    runAllTests,
    runSingleTest,
    setTestAllStatus,
    testAllStatus
  };
}
