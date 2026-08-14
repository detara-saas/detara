window.detara = {
    preferenciasSistemaEscuro: () => window.matchMedia('(prefers-color-scheme: dark)').matches,
    observarTemaSistema: (dotnetRef) => {
        const media = window.matchMedia('(prefers-color-scheme: dark)');
        if (window.detaraThemeListener) media.removeEventListener('change', window.detaraThemeListener);
        window.detaraThemeListener = event => dotnetRef.invokeMethodAsync('AtualizarTemaSistema', event.matches);
        media.addEventListener('change', window.detaraThemeListener);
    },
    pararObservacaoTemaSistema: () => {
        if (!window.detaraThemeListener) return;
        window.matchMedia('(prefers-color-scheme: dark)').removeEventListener('change', window.detaraThemeListener);
        window.detaraThemeListener = null;
    },
    aplicarTema: (escuro) => {
        document.documentElement.dataset.theme = escuro ? 'dark' : 'light';
        document.documentElement.style.colorScheme = escuro ? 'dark' : 'light';
    }
};
