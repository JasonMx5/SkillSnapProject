window.skillSnapAuthStorage = (() => {
    let dotNetReference;
    let storageKey;
    let listener;

    return {
        subscribe(reference, key) {
            this.unsubscribe();
            dotNetReference = reference;
            storageKey = key;
            listener = event => {
                if (event.key === storageKey || event.key === null) {
                    dotNetReference
                        .invokeMethodAsync("SyncFromBrowserStorageAsync")
                        .catch(error => console.error("Could not sync the SkillSnap session.", error));
                }
            };
            window.addEventListener("storage", listener);
        },
        unsubscribe() {
            if (listener) {
                window.removeEventListener("storage", listener);
            }

            dotNetReference = null;
            storageKey = null;
            listener = null;
        }
    };
})();
