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
    baixarArquivo: async (nome, tipo, streamReference) => {
        const buffer = await streamReference.arrayBuffer();
        const arquivo = new Blob([buffer], { type: tipo });
        const url = URL.createObjectURL(arquivo);
        const link = document.createElement('a');
        link.href = url;
        link.download = nome;
        link.rel = 'noopener';
        link.style.display = 'none';
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 60000);
    },
    visualizarArquivo: async (tipo, streamReference) => {
        const buffer = await streamReference.arrayBuffer();
        const url = URL.createObjectURL(new Blob([buffer], { type: tipo }));
        const link = document.createElement('a');
        link.href = url;
        link.target = '_blank';
        link.rel = 'noopener';
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 60000);
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
    obterFragmento: (nome) => new URLSearchParams(window.location.hash.substring(1)).get(nome),
    limparFragmento: () => window.history.replaceState(null, document.title, window.location.pathname + window.location.search),
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
