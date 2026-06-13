window.chishaziAuth = (() => {
    const sheetsScope = "https://www.googleapis.com/auth/spreadsheets";
    const storageKeyPrefix = "chishazi:google-access-token:v1:";

    function getStorageKey(clientId) {
        return `${storageKeyPrefix}${clientId}`;
    }

    function readStoredToken(clientId) {
        try {
            const json = window.localStorage.getItem(getStorageKey(clientId));
            if (!json) {
                return null;
            }

            const stored = JSON.parse(json);
            const expiresAtUnixMs = Number(stored.expiresAtUnixMs || 0);
            if (!stored.accessToken || expiresAtUnixMs <= Date.now()) {
                window.localStorage.removeItem(getStorageKey(clientId));
                return null;
            }

            return {
                accessToken: stored.accessToken,
                expiresInSeconds: Math.max(
                    1,
                    Math.ceil((expiresAtUnixMs - Date.now()) / 1000))
            };
        } catch {
            return null;
        }
    }

    function storeToken(clientId, accessToken, expiresInSeconds) {
        try {
            window.localStorage.setItem(
                getStorageKey(clientId),
                JSON.stringify({
                    accessToken,
                    expiresAtUnixMs:
                        Date.now() + Math.max(0, expiresInSeconds) * 1000
                }));
        } catch {
            // Authorization still works with the in-memory C# cache.
        }
    }

    function invalidateAccessToken(clientId, accessToken) {
        try {
            const json = window.localStorage.getItem(getStorageKey(clientId));
            if (!json) {
                return;
            }

            const stored = JSON.parse(json);
            if (stored.accessToken === accessToken) {
                window.localStorage.removeItem(getStorageKey(clientId));
            }
        } catch {
            try {
                window.localStorage.removeItem(getStorageKey(clientId));
            } catch {
                // Storage is optional; there is nothing else to invalidate.
            }
        }
    }

    function waitForGoogleIdentity(timeoutMs = 10000) {
        return new Promise((resolve, reject) => {
            const startedAt = Date.now();
            const poll = () => {
                if (window.google?.accounts?.oauth2) {
                    resolve();
                    return;
                }

                if (Date.now() - startedAt >= timeoutMs) {
                    reject(new Error("CHISHAZI_GOOGLE_IDENTITY_UNAVAILABLE"));
                    return;
                }

                window.setTimeout(poll, 50);
            };

            poll();
        });
    }

    async function requestAccessToken(clientId) {
        const storedToken = readStoredToken(clientId);
        if (storedToken) {
            return storedToken;
        }

        await waitForGoogleIdentity();

        return new Promise((resolve, reject) => {
            const tokenClient = google.accounts.oauth2.initTokenClient({
                client_id: clientId,
                scope: sheetsScope,
                callback: response => {
                    if (response.error) {
                        reject(new Error("CHISHAZI_GOOGLE_AUTH_FAILED"));
                        return;
                    }

                    const expiresInSeconds = Number(response.expires_in || 0);
                    storeToken(
                        clientId,
                        response.access_token,
                        expiresInSeconds);
                    resolve({
                        accessToken: response.access_token,
                        expiresInSeconds
                    });
                },
                error_callback: error => {
                    reject(new Error("CHISHAZI_GOOGLE_AUTH_CANCELED"));
                }
            });

            tokenClient.requestAccessToken({ prompt: "" });
        });
    }

    return { requestAccessToken, invalidateAccessToken };
})();
