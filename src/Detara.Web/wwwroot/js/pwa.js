(() => {
    const state = {
        browserOnline: navigator.onLine,
        installable: false,
        installed: window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true,
        updateAvailable: false
    };

    let deferredInstallPrompt = null;
    let dotNetReference = null;
    let registration = null;
    let reloadRequested = false;
    let reloadHandled = false;
    let lastUpdateCheck = 0;

    const notify = () => {
        if (!dotNetReference) return;
        dotNetReference.invokeMethodAsync(
            'AtualizarEstadoPwa',
            state.browserOnline,
            state.installable,
            state.installed,
            state.updateAvailable);
    };

    const setUpdateAvailable = value => {
        state.updateAvailable = value;
        notify();
    };

    const observeRegistration = value => {
        registration = value;
        if (registration.waiting && navigator.serviceWorker.controller) {
            setUpdateAvailable(true);
        }

        registration.addEventListener('updatefound', () => {
            const installingWorker = registration.installing;
            if (!installingWorker) return;

            installingWorker.addEventListener('statechange', () => {
                if (installingWorker.state === 'installed' && navigator.serviceWorker.controller) {
                    setUpdateAvailable(true);
                }
            });
        });
    };

    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        deferredInstallPrompt = event;
        state.installable = !state.installed;
        notify();
    });

    window.addEventListener('appinstalled', () => {
        deferredInstallPrompt = null;
        state.installable = false;
        state.installed = true;
        notify();
    });

    window.addEventListener('online', () => {
        state.browserOnline = true;
        notify();
    });

    window.addEventListener('offline', () => {
        state.browserOnline = false;
        notify();
    });

    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            setUpdateAvailable(false);
            if (reloadRequested && !reloadHandled) {
                reloadHandled = true;
                window.location.reload();
            }
        });

        navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' })
            .then(value => {
                observeRegistration(value);
                lastUpdateCheck = Date.now();
                return value.update();
            })
            .catch(error => console.warn('Não foi possível registrar o Service Worker da Detara.', error));

        document.addEventListener('visibilitychange', () => {
            const thirtyMinutes = 30 * 60 * 1000;
            if (document.visibilityState === 'visible' &&
                registration &&
                Date.now() - lastUpdateCheck >= thirtyMinutes) {
                lastUpdateCheck = Date.now();
                registration.update().catch(() => { });
            }
        });
    }

    window.detaraPwa = {
        inicializar: reference => {
            dotNetReference = reference;
            notify();
        },
        instalar: async () => {
            if (!deferredInstallPrompt || state.installed) return false;

            const prompt = deferredInstallPrompt;
            deferredInstallPrompt = null;
            state.installable = false;
            notify();
            await prompt.prompt();
            const choice = await prompt.userChoice;
            return choice.outcome === 'accepted';
        },
        atualizar: () => {
            if (!registration?.waiting) return false;
            reloadRequested = true;
            registration.waiting.postMessage({ type: 'SKIP_WAITING' });
            return true;
        },
        destruir: () => {
            dotNetReference = null;
        }
    };
})();
