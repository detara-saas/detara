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
    },
    baixarArquivoBase64: (nome, tipo, base64) => {
        const link = document.createElement('a');
        link.href = `data:${tipo};base64,${base64}`;
        link.download = nome;
        document.body.appendChild(link);
        link.click();
        link.remove();
    },
    criarUrlImagem: async (streamReference, contentType) => {
        const buffer = await streamReference.arrayBuffer();
        return URL.createObjectURL(new Blob([buffer], { type: contentType }));
    },
    revogarUrlImagem: (url) => URL.revokeObjectURL(url),
    limparInputArquivo: (id) => {
        const input = document.getElementById(id);
        if (input) input.value = "";
    }
};
