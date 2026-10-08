const state = {
    mode: "login",
    requestToken: null,
    username: null,
    busy: false
};

const elements = {
    connection: document.querySelector(".connection"),
    connectionLabel: document.querySelector("#connection-label"),
    originLabel: document.querySelector("#origin-label"),
    sessionState: document.querySelector("#session-state"),
    sessionLabel: document.querySelector("#session-label"),
    form: document.querySelector("#account-form"),
    username: document.querySelector("#username"),
    password: document.querySelector("#password"),
    rememberRow: document.querySelector("#remember-row"),
    rememberMe: document.querySelector("#remember-me"),
    submitButton: document.querySelector("#submit-button"),
    submitLabel: document.querySelector("#submit-label"),
    lobbyButton: document.querySelector("#lobby-button"),
    logoutButton: document.querySelector("#logout-button"),
    tokenLabel: document.querySelector("#token-label"),
    tokenStatus: document.querySelector("#token-status"),
    responseCode: document.querySelector("#response-code"),
    responseMethod: document.querySelector("#response-method"),
    responseRoute: document.querySelector("#response-route"),
    responseTime: document.querySelector("#response-time"),
    responseBody: document.querySelector("#response-body code"),
    activityList: document.querySelector("#activity-list")
};

const origin = window.location.origin;
document.querySelector("#footer-origin").textContent = origin;
elements.originLabel.textContent = window.location.host;

async function request(path, options = {}) {
    const method = options.method ?? "GET";
    const headers = new Headers(options.headers ?? {});
    if (options.body !== undefined) headers.set("Content-Type", "application/json");
    if (!["GET", "HEAD", "OPTIONS"].includes(method)) {
        if (!state.requestToken) throw new Error("Antiforgery token is not ready.");
        headers.set("X-CSRF-TOKEN", state.requestToken);
    }

    const startedAt = performance.now();
    let response;
    let data = null;
    try {
        response = await fetch(path, {
            method,
            headers,
            body: options.body === undefined ? undefined : JSON.stringify(options.body),
            credentials: "same-origin"
        });
        const text = await response.text();
        if (text) {
            try { data = JSON.parse(text); }
            catch { data = text; }
        }
    } catch (error) {
        data = { error: error.message || "Network request failed" };
    }

    const elapsed = Math.round(performance.now() - startedAt);
    const status = response?.status ?? "ERR";
    renderResponse(method, path, status, elapsed, data);
    addActivity(method, path, status);
    return { ok: response?.ok ?? false, status, data };
}

function renderResponse(method, path, status, elapsed, data) {
    elements.responseMethod.textContent = method;
    elements.responseRoute.textContent = path;
    elements.responseTime.textContent = `${elapsed} ms`;
    elements.responseCode.textContent = status;
    elements.responseCode.classList.toggle("is-success", typeof status === "number" && status < 400);
    elements.responseCode.classList.toggle("is-error", status === "ERR" || (typeof status === "number" && status >= 400));
    elements.responseBody.textContent = data === null ? "No response body" : JSON.stringify(data, null, 2);
}

function addActivity(method, path, status) {
    const item = document.createElement("li");
    item.className = "activity-item";
    const verb = document.createElement("span");
    verb.className = "activity-verb";
    verb.textContent = method;
    const route = document.createElement("span");
    route.className = "activity-path";
    route.textContent = path;
    const result = document.createElement("span");
    result.className = "activity-status";
    result.textContent = status;
    result.classList.toggle("is-error", status === "ERR" || (typeof status === "number" && status >= 400));
    item.append(verb, route, result);
    elements.activityList.prepend(item);
    while (elements.activityList.children.length > 8) elements.activityList.lastElementChild.remove();
}

function setSession(username) {
    state.username = username;
    const signedIn = Boolean(username);
    elements.sessionLabel.textContent = signedIn ? `Signed in as ${username}` : "Signed out";
    elements.sessionState.classList.toggle("is-authenticated", signedIn);
    elements.logoutButton.disabled = !signedIn || state.busy;
}

function setBusy(busy) {
    state.busy = busy;
    elements.submitButton.disabled = busy || !state.requestToken;
    elements.lobbyButton.disabled = busy;
    elements.logoutButton.disabled = busy || !state.username;
    document.querySelectorAll(".mode-button").forEach(button => button.disabled = busy);
}

function setMode(mode) {
    state.mode = mode;
    const isLogin = mode === "login";
    document.querySelectorAll(".mode-button").forEach(button => {
        const active = button.dataset.mode === mode;
        button.classList.toggle("is-active", active);
        button.setAttribute("aria-selected", String(active));
    });
    elements.rememberRow.hidden = !isLogin;
    elements.rememberMe.checked = false;
    elements.password.autocomplete = isLogin ? "current-password" : "new-password";
    elements.submitLabel.textContent = isLogin ? "Sign in" : "Create account";
}

document.querySelectorAll(".mode-button").forEach(button => {
    button.addEventListener("click", () => setMode(button.dataset.mode));
});

elements.form.addEventListener("submit", async event => {
    event.preventDefault();
    setBusy(true);
    const isLogin = state.mode === "login";
    const body = {
        username: elements.username.value.trim(),
        password: elements.password.value
    };
    if (isLogin) body.rememberMe = elements.rememberMe.checked;

    try {
        const result = await request(isLogin ? "/login" : "/register", { method: "POST", body });
        if (result.ok) {
            state.requestToken = result.data.requestToken;
            elements.tokenLabel.textContent = "Rotated for signed-in session";
            elements.tokenStatus.textContent = "READY";
            elements.tokenStatus.classList.add("is-ready");
            setSession(result.data.username);
            elements.password.value = "";
        }
    } finally {
        setBusy(false);
    }
});

elements.lobbyButton.addEventListener("click", async () => {
    setBusy(true);
    const result = await request("/lobby");
    if (result.ok) setSession(result.data.username);
    else if (result.status === 401) setSession(null);
    setBusy(false);
});

elements.logoutButton.addEventListener("click", async () => {
    setBusy(true);
    const result = await request("/logout", { method: "POST" });
    if (result.ok) {
        state.requestToken = result.data.requestToken;
        elements.tokenLabel.textContent = "Rotated for anonymous session";
        elements.tokenStatus.textContent = "READY";
        setSession(null);
    }
    setBusy(false);
});

document.querySelector("#clear-activity").addEventListener("click", () => {
    elements.activityList.replaceChildren();
});

async function initialize() {
    try {
        const token = await request("/antiforgerytoken");
        if (!token.ok || !token.data?.requestToken) throw new Error("Token endpoint did not return a request token.");
        state.requestToken = token.data.requestToken;
        elements.tokenLabel.textContent = "Ready from /antiforgerytoken";
        elements.tokenStatus.textContent = "READY";
        elements.tokenStatus.classList.add("is-ready");
        elements.connection.classList.add("is-online");
        elements.connectionLabel.textContent = "API connected";
        setBusy(false);

        const lobby = await request("/lobby");
        if (lobby.ok) setSession(lobby.data.username);
        else if (lobby.status === 401) setSession(null);
        else elements.sessionLabel.textContent = "Session check failed";
    } catch (error) {
        elements.connectionLabel.textContent = "API unavailable";
        elements.tokenLabel.textContent = error.message;
        elements.tokenStatus.textContent = "ERROR";
        elements.tokenStatus.classList.remove("is-ready");
        renderResponse("GET", "/antiforgerytoken", "ERR", 0, { error: error.message });
    }
}

setBusy(true);
initialize();