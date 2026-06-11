window.chishaziCache = (() => {
    const databaseName = "chishazi-cache";
    const databaseVersion = 1;
    const storeName = "entries";

    function openDatabase() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(databaseName, databaseVersion);

            request.onupgradeneeded = () => {
                const database = request.result;
                if (!database.objectStoreNames.contains(storeName)) {
                    database.createObjectStore(storeName);
                }
            };

            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }

    async function runTransaction(mode, operation) {
        const database = await openDatabase();

        try {
            return await new Promise((resolve, reject) => {
                const transaction = database.transaction(storeName, mode);
                const store = transaction.objectStore(storeName);
                const request = operation(store);
                let result = null;

                request.onsuccess = () => {
                    result = request.result ?? null;
                };
                request.onerror = () => reject(request.error);
                transaction.oncomplete = () => resolve(result);
                transaction.onabort = () => reject(transaction.error);
                transaction.onerror = () => reject(transaction.error);
            });
        } finally {
            database.close();
        }
    }

    function get(key) {
        return runTransaction("readonly", store => store.get(key));
    }

    function set(key, value) {
        return runTransaction("readwrite", store => store.put(value, key));
    }

    function remove(key) {
        return runTransaction("readwrite", store => store.delete(key));
    }

    return { get, set, remove };
})();
