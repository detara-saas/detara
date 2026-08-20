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
        const themeColor = document.querySelector('meta[name="theme-color"]');
        if (themeColor) themeColor.content = escuro ? '#111827' : '#FFFFFF';
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
    },
    editorEmail: {
        inicializar: (id, html, dotnetRef) => {
            const editor = document.getElementById(id);
            if (!editor) return;
            editor.innerHTML = html || "";
            const listener = () => dotnetRef.invokeMethodAsync('AtualizarHtml', editor.innerHTML);
            editor.detaraListener = listener;
            editor.addEventListener('input', listener);
        },
        definir: (id, html) => {
            const editor = document.getElementById(id);
            if (editor && editor.innerHTML !== (html || "")) editor.innerHTML = html || "";
        },
        formatar: (id, comando) => {
            const editor = document.getElementById(id);
            if (!editor) return;
            editor.focus();
            document.execCommand(comando, false, null);
            editor.dispatchEvent(new Event('input'));
        },
        adicionarLink: (id) => {
            const editor = document.getElementById(id);
            if (!editor) return;
            const endereco = window.prompt('Endereço seguro do link (https:// ou mailto:):');
            if (!endereco) return;
            editor.focus();
            document.execCommand('createLink', false, endereco);
            editor.dispatchEvent(new Event('input'));
        },
        cor: (id, cor) => {
            const editor = document.getElementById(id);
            if (!editor || !cor) return;
            editor.focus();
            document.execCommand('foreColor', false, cor);
            editor.dispatchEvent(new Event('input'));
        },
        destruir: (id) => {
            const editor = document.getElementById(id);
            if (editor?.detaraListener) editor.removeEventListener('input', editor.detaraListener);
        }
    }
};
