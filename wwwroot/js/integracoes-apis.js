// Apenas edição do formulário. Nenhuma expressão/código de contratos é executada.
(() => {
    let index = 0;
    document.addEventListener('click', event => {
        const remove = event.target.closest('[data-remover]');
        if (remove) { remove.closest('tr').remove(); return; }
        const add = event.target.closest('[data-adicionar]');
        if (!add) return;
        const kind = add.dataset.adicionar;
        const template = document.getElementById('template-' + kind);
        const target = document.getElementById(kind === 'parametro' ? 'parametros' : 'mapeamentos');
        if (!template || !target) return;
        const row = template.content.cloneNode(true);
        const key = 'novo_' + (++index);
        row.querySelectorAll('[name]').forEach(field => {
            field.name = field.name.replaceAll('__i__', key);
            if (field.value === '__i__') field.value = key;
        });
        target.appendChild(row);
    });
})();

// Somente campos pertinentes ao método de autenticação escolhido.
(() => {
 const select = document.querySelector('[name="Autenticacao.Tipo"]');
 if (!select) return;
 const update = () => document.querySelectorAll('[data-auth-mode]').forEach(block => {
   block.hidden = !block.dataset.authMode.split(' ').includes(select.value);
 });
 select.addEventListener('change', update); update();
})();
