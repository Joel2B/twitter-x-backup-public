const BRIDGE_SOURCE = "x-credential-capturer";
const GRAPHQL_SEGMENT = "/i/api/graphql/";

type BridgeWindow = Window & {
  __xccBridgeInstalled?: boolean;
};

type BridgePayload = {
  source: string;
  type: "XCC_GRAPHQL_RESPONSE";
  url: string;
  status?: number;
  body: string;
  capturedAt: string;
};

function isGraphqlUrl(url: string): boolean {
  return typeof url === "string" && url.includes(GRAPHQL_SEGMENT);
}

function emit(url: string, status: number | undefined, body: string): void {
  if (!isGraphqlUrl(url) || !body) {
    return;
  }

  const payload: BridgePayload = {
    source: BRIDGE_SOURCE,
    type: "XCC_GRAPHQL_RESPONSE",
    url,
    status,
    body,
    capturedAt: new Date().toISOString()
  };

  window.postMessage(payload, "*");
}

function installBridge(): void {
  const w = window as BridgeWindow;

  if (w.__xccBridgeInstalled) {
    return;
  }

  w.__xccBridgeInstalled = true;
  console.info("[XCC] in-page bridge installed");

  const originalFetch = window.fetch.bind(window);
  window.fetch = async (...args: Parameters<typeof fetch>): Promise<Response> => {
    const response = await originalFetch(...args);

    try {
      const input = args[0];
      const url =
        typeof input === "string"
          ? input
          : input && typeof input === "object" && "url" in input
            ? String((input as Request).url || "")
            : "";

      if (isGraphqlUrl(url)) {
        const text = await response.clone().text();
        emit(url, response.status, text);
      }
    } catch (_error) {
      // ignore capture errors
    }

    return response;
  };

  const originalOpen = XMLHttpRequest.prototype.open;
  const originalSend = XMLHttpRequest.prototype.send;

  XMLHttpRequest.prototype.open = function (
    this: XMLHttpRequest & { __xccUrl?: string },
    method: string,
    url: string | URL,
    ...rest: [async?: boolean, username?: string | null, password?: string | null]
  ): void {
    const normalizedUrl = String(url || "");
    this.__xccUrl = normalizedUrl;
    return (originalOpen as (...args: unknown[]) => void).call(
      this,
      method,
      normalizedUrl,
      ...rest
    );
  };

  XMLHttpRequest.prototype.send = function (
    this: XMLHttpRequest & { __xccUrl?: string },
    body?: Document | XMLHttpRequestBodyInit | null
  ): void {
    if (isGraphqlUrl(this.__xccUrl || "")) {
      this.addEventListener(
        "load",
        () => {
          try {
            emit(
              this.__xccUrl || "",
              this.status,
              typeof this.responseText === "string" ? this.responseText : ""
            );
          } catch (_error) {
            // ignore capture errors
          }
        },
        { once: true }
      );
    }

    return originalSend.call(this, body);
  };
}

installBridge();
