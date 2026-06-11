window.chishaziAuth = (() => {
    const scope = "https://www.googleapis.com/auth/spreadsheets.readonly";

    function waitForGoogleIdentity(timeoutMs = 10000) {
        return new Promise((resolve, reject) => {
            const startedAt = Date.now();
            const poll = () => {
                if (window.google?.accounts?.oauth2) {
                    resolve();
                    return;
                }

                if (Date.now() - startedAt >= timeoutMs) {
                    reject(new Error("Google Identity Services did not load. Check your connection and try again."));
                    return;
                }

                window.setTimeout(poll, 50);
            };

            poll();
        });
    }

    async function requestAccessToken(clientId) {
        await waitForGoogleIdentity();

        return new Promise((resolve, reject) => {
            const tokenClient = google.accounts.oauth2.initTokenClient({
                client_id: clientId,
                scope,
                callback: response => {
                    if (response.error) {
                        reject(new Error(response.error_description || response.error));
                        return;
                    }

                    resolve({
                        accessToken: response.access_token,
                        expiresInSeconds: Number(response.expires_in || 0)
                    });
                },
                error_callback: error => {
                    const message = error?.message || error?.type || "Google authorization was canceled.";
                    reject(new Error(message));
                }
            });

            tokenClient.requestAccessToken({ prompt: "" });
        });
    }

    return { requestAccessToken };
})();
