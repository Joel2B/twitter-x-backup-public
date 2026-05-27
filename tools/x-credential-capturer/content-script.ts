const BRIDGE_SOURCE = "x-credential-capturer";
const BRIDGE_SCRIPT_ID = "xcc-inpage-bridge";

type BridgeMessage = {
  source: string;
  type: "XCC_GRAPHQL_RESPONSE";
  url: string;
  status?: number;
  body: string;
  capturedAt: string;
};

function isBridgeMessage(value: unknown): value is BridgeMessage {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return false;
  }

  const row = value as Record<string, unknown>;
  return (
    row.source === BRIDGE_SOURCE &&
    row.type === "XCC_GRAPHQL_RESPONSE" &&
    typeof row.url === "string" &&
    typeof row.body === "string" &&
    typeof row.capturedAt === "string"
  );
}

function injectBridgeScript(): void {
  if (document.getElementById(BRIDGE_SCRIPT_ID)) {
    return;
  }

  const script = document.createElement("script");
  script.id = BRIDGE_SCRIPT_ID;
  script.src = chrome.runtime.getURL("inpage-bridge.js");
  script.async = false;
  script.dataset.source = BRIDGE_SOURCE;
  script.onload = () => {
    console.info("[XCC] bridge script loaded");
    script.remove();
  };
  script.onerror = (event) => {
    console.error("[XCC] bridge script failed to load", event);
  };

  (document.head || document.documentElement).appendChild(script);
}

window.addEventListener("message", (event: MessageEvent<unknown>) => {
  if (event.source !== window || !isBridgeMessage(event.data)) {
    return;
  }

  console.info("[XCC] captured graphql response", event.data.url, event.data.status);

  void chrome.runtime.sendMessage({
    type: "captureGraphqlResponseBody",
    url: event.data.url,
    status: typeof event.data.status === "number" ? event.data.status : undefined,
    body: event.data.body,
    capturedAt: event.data.capturedAt
  });
});

injectBridgeScript();
